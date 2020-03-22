using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace kellerkompanie_sync
{
    [JsonConverter(typeof(FilePathConverter))]
    public class FilePath : IComparable
    {
        private string value;
        public string Value
        {
            get
            {
                return value;
            }
            set
            {
                this.value = value.ToLower();
            }
        }

        public int Length
        {
            get
            {
                return value.Length;
            }
        }

        public bool Contains(string str)
        {
            return value.Contains(str);
        }

        public override bool Equals(object obj)
        {
            return obj is FilePath path &&
                   value == path.value;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(value);
        }

        public int IndexOf(string str)
        {
            return value.IndexOf(str);
        }

        public FilePath SubPath(int index)
        {
            var filePath = new FilePath
            {
                Value = value.Substring(index)
            };
            return filePath;
        }

        public FilePath SubPath(int start, int end)
        {
            var filePath = new FilePath { Value = value.Substring(start, end) };
            return filePath;
        }

        public override string ToString()
        {
            return value;
        }

        public FilePath Replace(string oldValue, string newValue)
        {
            return new FilePath { Value = value.Replace(oldValue, newValue) };
        }

        public int CompareTo(object obj)
        {
            if (obj == null) return 1;

            FilePath otherFilePath = obj as FilePath;
            return value.CompareTo(otherFilePath.Value);
        }
    }

    public class FilePathConverter : JsonConverter
    {
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            string value = (string)reader.Value;
            return new FilePath { Value = value };
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            FilePath filePath = (FilePath)value;
            writer.WriteValue(filePath.Value);
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(FilePath);
        }
    }

    public class LocalAddon
    {
        public string Name { get; set; }
        public string Uuid { get; set; }
        public string Version { get; set; }
        public FilePath AbsoluteFilepath { get; set; }
        public Dictionary<FilePath, LocalFileIndex> Files { get; set; }

        public LocalAddon() { }

        public LocalAddon(FilePath addonName, FilePath absoluteFilepath)
        {
            Name = addonName.Value;
            Files = new Dictionary<FilePath, LocalFileIndex>();
            Version = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            AbsoluteFilepath = absoluteFilepath;
            Uuid = FileIndexer.Instance.LookUpAddonName(addonName.Value);
        }

        public override bool Equals(object obj)
        {
            return obj is LocalAddon addon && Uuid == addon.Uuid;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Uuid);
        }
    }

    public class LocalFileIndex
    {
        public FilePath Relative_filepath { get; set; }
        public FilePath Absolute_filepath { get; set; }
        public DateTime Created { get; set; }
        public long Filesize { get; set; }
        public string Hash { get; set; }

        public LocalFileIndex() { }

        public LocalFileIndex(FilePath filePath)
        {
            if (!filePath.Contains("@"))
            {
                Log.Error("LocalFileIndex: Parameter must contain @ in path");
                throw new ArgumentException("Parameter must contain @ in path");
            }

            FileInfo fileInfo = new FileInfo(filePath.Value);
            Created = fileInfo.CreationTime;
            Filesize = fileInfo.Length;
            Absolute_filepath = filePath;

            int index = filePath.IndexOf("@");
            Relative_filepath = filePath.SubPath(index);

            using (SHA256 mySHA256 = SHA256.Create())
            {
                FileStream fileStream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                fileStream.Position = 0;
                byte[] hashValue = mySHA256.ComputeHash(fileStream);
                fileStream.Close();
                fileStream.Dispose();

                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < hashValue.Length; i++)
                {
                    sb.Append(hashValue[i].ToString("x2"));
                }
                Hash = sb.ToString().ToUpper();
            }
        }
    }

    public class FileIndexer
    {
        private static FileIndexer instance;

        private Dictionary<FilePath, LocalAddon> Index { get; set; }
        private ProgressBar ProgressBar { get; set; }
        private TextBlock ProgressBarText { get; set; }
        public ObservableCollection<AddonGroup> AddonGroups { get; set; } = new ObservableCollection<AddonGroup>();

        // uuid --> LocalAddon
        public Dictionary<string, List<LocalAddon>> addonUuidToLocalAddonMap = new Dictionary<string, List<LocalAddon>>();
        public RemoteIndex RemoteIndex;

        private FileIndexer(ProgressBar progressBar, TextBlock progressBarText)
        {
            LoadIndex();
            ProgressBar = progressBar;
            ProgressBarText = progressBarText;

            RemoteIndex = WebAPI.GetIndex();
            foreach (WebAddonGroup addon in RemoteIndex.AddonGroups)
            {
                AddonGroup addonGroup = new AddonGroup(addon);
                AddonGroups.Add(addonGroup);
            }
        }

        public static void Setup(ProgressBar progressBar, TextBlock progressBarText)
        {
            instance = new FileIndexer(progressBar, progressBarText);
        }

        public static FileIndexer Instance
        {
            get
            {
                if (instance == null)
                {
                    Log.Error("FileIndexer: You need to initialize FileIndexer first");
                    throw new FieldAccessException("You need to initialize FileIndexer first");
                }
                return instance;
            }
        }

        public HashSet<FilePath> GetAllFilePaths()
        {
            HashSet<FilePath> allFilePaths = new HashSet<FilePath>();
            foreach (string addonSearchDirectory in Settings.Instance.GetAddonSearchDirectories())
            {
                string[] allfiles = Directory.GetFiles(addonSearchDirectory, "*.*", SearchOption.AllDirectories);
                foreach (string file in allfiles)
                {
                    if (file.Contains("@"))
                    {
                        allFilePaths.Add(new FilePath { Value = file });
                    }
                }
            }
            return allFilePaths;
        }

        public void SaveIndex()
        {
            Directory.CreateDirectory(Settings.SettingsDirectory);

            using StreamWriter file = File.CreateText(Settings.IndexFile);
            JsonSerializerSettings settings = new JsonSerializerSettings();
            settings.Formatting = Formatting.Indented;
            JsonSerializer serializer = JsonSerializer.Create(settings);
            serializer.Serialize(file, Index);
        }

        public void LoadIndex()
        {
            if (!File.Exists(Settings.IndexFile))
            {
                Index = new Dictionary<FilePath, LocalAddon>();
            }
            else
            {
                using StreamReader file = File.OpenText(Settings.IndexFile);
                string content = file.ReadToEnd();
                JObject jRoot = JObject.Parse(content);

                Index = new Dictionary<FilePath, LocalAddon>();
                foreach (var jIndexEntry in jRoot)
                {
                    JToken jLocalAddon = jIndexEntry.Value;
                    string localAddonName = (string)jLocalAddon["Name"];
                    string localAddonUuid = (string)jLocalAddon["Uuid"];
                    string localAddonVersion = (string)jLocalAddon["Version"];
                    FilePath localAddonAbsoluteFilePath = new FilePath { Value = (string)jLocalAddon["AbsoluteFilepath"] };

                    Dictionary<FilePath, LocalFileIndex> files = new Dictionary<FilePath, LocalFileIndex>();
                    foreach (JToken jFile in jLocalAddon["Files"])
                    {
                        var jFileContent = ((JProperty)jFile).Value;
                        FilePath fileRelativeFilePath = new FilePath { Value = (string)jFileContent["Relative_filepath"] };
                        FilePath fileAbsoluteFilePath = new FilePath { Value = (string)jFileContent["Absolute_filepath"] };
                        DateTime fileCreated = (DateTime)jFileContent["Created"];
                        long fileSize = (long)jFileContent["Filesize"];
                        string fileHash = (string)jFileContent["Hash"];

                        LocalFileIndex localFileIndex = new LocalFileIndex
                        {
                            Relative_filepath = fileRelativeFilePath,
                            Absolute_filepath = fileAbsoluteFilePath,
                            Created = fileCreated,
                            Filesize = fileSize,
                            Hash = fileHash,
                        };
                        files.Add(fileAbsoluteFilePath, localFileIndex);
                    }

                    LocalAddon localAddon = new LocalAddon()
                    {
                        Name = localAddonName,
                        Uuid = localAddonUuid,
                        Version = localAddonVersion,
                        AbsoluteFilepath = localAddonAbsoluteFilePath,
                        Files = files,
                    };
                    Index.Add(localAddonAbsoluteFilePath, localAddon);
                }               
            }
        }

        public static FilePath ExtractAddonName(FilePath filePath)
        {
            if (!filePath.Contains("@"))
            {
                return null;
            }

            int index = filePath.IndexOf("@");
            FilePath relativeFilePath = filePath.SubPath(index);
            index = relativeFilePath.IndexOf("\\");
            return relativeFilePath.SubPath(0, index);
        }

        public string LookUpAddonName(string addonName)
        {
            addonName = addonName.ToLower();
            foreach (string remoteAddonName in RemoteIndex.FilesIndex.Keys)
            {
                if (remoteAddonName.ToLower().Equals(addonName))
                {
                    RemoteAddon remoteAddon = RemoteIndex.FilesIndex[remoteAddonName];
                    return remoteAddon.Uuid;
                }
            }

            return null;
        }

        public void UpdateLocalIndex()
        {
            Debug.WriteLine("FileIndexer UpdateLocalIndex()");
            BackgroundWorker worker = new BackgroundWorker();
            worker.WorkerReportsProgress = true;
            worker.DoWork += LocalIndexingWorker_DoWork;
            worker.ProgressChanged += LocalIndexingWorker_ProgressChanged;
            worker.RunWorkerCompleted += LocalIndexingWorker_RunWorkerCompleted;
            worker.RunWorkerAsync();
        }

        public void UpdateAddonGroups()
        {
            AddonGroups.Clear();
            RemoteIndex = WebAPI.GetIndex();
            foreach (WebAddonGroup addon in RemoteIndex.AddonGroups)
            {
                AddonGroup addonGroup = new AddonGroup(addon);
                AddonGroups.Add(addonGroup);
            }
            ModsPage.Instance?.ListViewAddonGroups.Items.Refresh();

            UpdateLocalIndex();
        }

        private FilePath ExtractAbsoluteAddonPath(FilePath filePath)
        {
            FilePath addonName = ExtractAddonName(filePath);
            int index = filePath.IndexOf("@") + addonName.Length;
            return filePath.SubPath(0, index);
        }

        void LocalIndexingWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            // get all files from addon search directories
            HashSet<FilePath> files = GetAllFilePaths();

            // remove addons from index that are not on disk anymore            
            List<FilePath> removals = new List<FilePath>();
            foreach (FilePath indexKey in Index.Keys)
            {
                bool remove = true;
                foreach (FilePath file in files)
                {
                    FilePath absoluteAddonPath = ExtractAbsoluteAddonPath(file);
                    if (indexKey.Equals(absoluteAddonPath))
                    {
                        remove = false;
                        break;
                    }
                }

                if (remove)
                {
                    removals.Add(indexKey);
                }
            }
            foreach (FilePath indexKey in removals)
            {
                Index.Remove(indexKey);
            }

            // remove files from index which are not on disk anymore
            foreach (LocalAddon localAddon in Index.Values)
            {
                removals = new List<FilePath>();
                foreach (FilePath filePath in localAddon.Files.Keys)
                {
                    if (!files.Contains(filePath))
                    {
                        removals.Add(filePath);
                    }
                }
                foreach (FilePath key in removals)
                {
                    localAddon.Files.Remove(key);
                }
            }


            int i = 0;
            foreach (FilePath file in files)
            {
                FilePath absoluteAddonPath = ExtractAbsoluteAddonPath(file);
                LocalAddon localAddon;
                if (Index.ContainsKey(absoluteAddonPath))
                {
                    // load addon from existing index
                    Log.Debug("loading existing addon: " + absoluteAddonPath);
                    localAddon = Index[absoluteAddonPath];
                }
                else
                {
                    // create new addon
                    Log.Debug("creating new addon: " + absoluteAddonPath);
                    FilePath addonName = ExtractAddonName(file);
                    localAddon = new LocalAddon(addonName, absoluteAddonPath);
                    Index.Add(absoluteAddonPath, localAddon);
                }

                if (!addonUuidToLocalAddonMap.ContainsKey(localAddon.Uuid))
                {
                    addonUuidToLocalAddonMap.Add(localAddon.Uuid, new List<LocalAddon>());
                }
                List<LocalAddon> localAddons = addonUuidToLocalAddonMap[localAddon.Uuid];
                if (!localAddons.Contains(localAddon))
                {
                    localAddons.Add(localAddon);
                }

                if (localAddon.Files.ContainsKey(file))
                {
                    // compare if files differ
                    LocalFileIndex existingIndex = localAddon.Files[file];

                    FileInfo fileInfo = new FileInfo(file.Value);
                    DateTime created = fileInfo.CreationTime;
                    long filesize = fileInfo.Length;

                    if (!existingIndex.Created.Equals(created) || existingIndex.Filesize != filesize)
                    {
                        Log.Debug("out of date file: " + file);
                        // file is not up to date, index it                        
                        LocalFileIndex fileIndex = new LocalFileIndex(file);
                        localAddon.Files[fileIndex.Absolute_filepath] = fileIndex;
                    }
                }
                else
                {
                    // file is not indexed yet, index it
                    Log.Debug("file not indexed yet: " + file);
                    LocalFileIndex fileIndex = new LocalFileIndex(file);
                    localAddon.Files.Add(fileIndex.Absolute_filepath, fileIndex);
                }

                int percentage = (int)Math.Floor((double)++i / files.Count * 100);
                (sender as BackgroundWorker).ReportProgress(percentage);
            }

            SaveIndex();
        }

        void LocalIndexingWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                ProgressBar.Value = e.ProgressPercentage;
                ProgressBarText.Text = string.Format("{0} ({1}%)", Properties.Resources.ProgressIndexingFiles, e.ProgressPercentage);
            }));
        }

        void LocalIndexingWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                ProgressBar.Value = 0;
                ProgressBarText.Text = Properties.Resources.ProgressIndexingComplete;
            }));

            UpdateAddonGroupStates();
        }

        public void UpdateAddonGroupStates()
        {
            BackgroundWorker worker = new BackgroundWorker();
            worker.WorkerReportsProgress = true;
            worker.DoWork += AddonGroupStateWorker_DoWork;
            worker.ProgressChanged += AddonGroupStateWorker_ProgressChanged;
            worker.RunWorkerCompleted += AddonGroupStateWorker_RunWorkerCompleted;
            worker.RunWorkerAsync();
        }

        void AddonGroupStateWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            List<(AddonGroup, AddonGroupState)> changes = new List<(AddonGroup, AddonGroupState)>();

            double i = 0;
            foreach (AddonGroup addonGroup in AddonGroups)
            {
                // link all web addons to existing local addons for later use
                WebAddonGroup webAddonGroup = addonGroup.WebAddonGroup;
                List<WebAddon> webAddons = webAddonGroup.Addons;
                foreach (WebAddon webAddon in webAddons)
                {
                    string addonUuid = webAddon.Uuid;
                    if (addonUuidToLocalAddonMap.ContainsKey(addonUuid))
                    {
                        LocalAddon localAddon = addonUuidToLocalAddonMap[addonUuid][0];
                        if (!addonGroup.WebAddonToLocalAddonDict.ContainsKey(webAddon))
                        {
                            addonGroup.WebAddonToLocalAddonDict.Add(webAddon, localAddon);
                        }
                        else
                        {
                            addonGroup.WebAddonToLocalAddonDict[webAddon] = localAddon;
                        }
                    }
                }

                string remoteUuid = addonGroup.WebAddonGroup.Uuid;
                string remoteVersion = addonGroup.WebAddonGroup.Version;

                if (Settings.Instance.SubscribedAddonGroups.ContainsKey(remoteUuid))
                {
                    // AddonGroup is subscribed    
                    string localVersion = Settings.Instance.SubscribedAddonGroups[remoteUuid];
                    if (localVersion.Equals(remoteVersion))
                    {
                        // even if versions match we have to check for changes in filesystem
                        bool upToDate = true;
                        foreach (WebAddon webAddon in addonGroup.WebAddonGroup.Addons)
                        {
                            if (!addonUuidToLocalAddonMap.ContainsKey(webAddon.Uuid))
                            {
                                // addon is not on disk anymore, needs update
                                changes.Add((addonGroup, AddonGroupState.NeedsUpdate));
                                upToDate = false;
                                break;
                            }
                            else
                            {
                                // check if files are still there 
                                List<LocalAddon> localAddons = addonUuidToLocalAddonMap[webAddon.Uuid];
                                foreach (LocalAddon localAddon in localAddons)
                                {
                                    List<FilePath> localAddonFiles = new List<FilePath>(localAddon.Files.Values.Select(fileIndex => fileIndex.Relative_filepath));
                                    List<FilePath> remoteAddonFiles = GetRemoteFilenamesForAddon(webAddon.Uuid);
                                    localAddonFiles.Sort();
                                    remoteAddonFiles.Sort();
                                    var localNotRemote = localAddonFiles.Except(remoteAddonFiles).ToList();
                                    var remoteNotLocal = remoteAddonFiles.Except(localAddonFiles).ToList();

                                    if (localNotRemote.Any() || remoteNotLocal.Any())
                                    {
                                        changes.Add((addonGroup, AddonGroupState.NeedsUpdate));
                                        upToDate = false;
                                        break;
                                    }
                                }
                            }
                        }

                        if (upToDate)
                        {
                            changes.Add((addonGroup, AddonGroupState.Ready));
                        }
                    }
                    else
                    {
                        changes.Add((addonGroup, AddonGroupState.NeedsUpdate));
                    }
                }
                else
                {
                    // AddonGroup is not subscribed
                    int foundAddonsLocally = 0;
                    foreach (WebAddon webAddon in webAddons)
                    {
                        string addonUuid = webAddon.Uuid;
                        if (addonUuidToLocalAddonMap.ContainsKey(addonUuid))
                        {
                            foundAddonsLocally++;
                        }
                    }

                    if (foundAddonsLocally == webAddons.Count)
                    {
                        changes.Add((addonGroup, AddonGroupState.CompleteButNotSubscribed));
                    }
                    else if (foundAddonsLocally > 0)
                    {
                        changes.Add((addonGroup, AddonGroupState.Partial));
                    }
                    else
                    {
                        changes.Add((addonGroup, AddonGroupState.NonExistent));
                    }
                }

                i += 1;
                int percentage = (int)Math.Floor(i / AddonGroups.Count * 100);
                (sender as BackgroundWorker).ReportProgress(percentage);
            }

            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                foreach ((AddonGroup addonGroup, AddonGroupState addonGroupState) in changes)
                {
                    addonGroup.State = addonGroupState;
                }
                ModsPage.Instance?.ListViewAddonGroups.Items.Refresh();
            }));
        }

        private List<FilePath> GetRemoteFilenamesForAddon(string uuid)
        {
            foreach (RemoteAddon remoteAddon in RemoteIndex.FilesIndex.Values)
            {
                if (remoteAddon.Uuid.Equals(uuid))
                {
                    List<FilePath> remoteFileNames = new List<FilePath>();
                    foreach (FilePath remoteFileName in remoteAddon.Files.Keys)
                    {
                        remoteFileNames.Add(remoteFileName.Replace("/", "\\"));
                    }
                    return remoteFileNames;
                }
            }

            return null;
        }

        void AddonGroupStateWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                ProgressBar.Value = e.ProgressPercentage;
                ProgressBarText.Text = string.Format("{0} ({1}%)", Properties.Resources.ProgressComparingOnlineVersion, e.ProgressPercentage);
            }));
        }

        void AddonGroupStateWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                ProgressBar.Value = 0;
                ProgressBarText.Text = Properties.Resources.EverythingUpToDate;
            }));
        }
    }
}

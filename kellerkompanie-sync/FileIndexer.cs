using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace kellerkompanie_sync
{
    public class LocalAddon
    {
        public string Name { get; set; }
        public string Uuid { get; set; }
        public string Version { get; set; }
        public string AbsoluteFilepath { get; set; }
        public Dictionary<string, LocalFileIndex> Files { get; set; }

        public LocalAddon() { }

        public LocalAddon(string addonName, string absoluteFilepath)
        {
            Name = addonName;
            Files = new Dictionary<string, LocalFileIndex>();
            Version = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            AbsoluteFilepath = absoluteFilepath;
            Uuid = FileIndexer.Instance.LookUpAddonName(addonName);
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
        public string Relative_filepath { get; set; }
        public string Absolute_filepath { get; set; }
        public DateTime Created { get; set; }
        public long Filesize { get; set; }
        public string Hash { get; set; }       

        public LocalFileIndex() { }
        
        public LocalFileIndex(string filePath)
        {
            if (!filePath.Contains("@"))
            {
                Log.Error("LocalFileIndex: Parameter must contain @ in path");
                throw new ArgumentException("Parameter must contain @ in path");
            }

            FileInfo fileInfo = new FileInfo(filePath);
            Created = fileInfo.CreationTime;
            Filesize = fileInfo.Length;
            Absolute_filepath = filePath;

            int index = filePath.IndexOf("@");
            Relative_filepath = filePath.Substring(index);

            using (SHA256 mySHA256 = SHA256.Create())
            {
                FileStream fileStream = fileInfo.Open(FileMode.Open);
                fileStream.Position = 0;
                byte[] hashValue = mySHA256.ComputeHash(fileStream);
                fileStream.Close();

                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < hashValue.Length; i++)
                {
                    sb.Append(hashValue[i].ToString("x2"));
                }
                Hash = sb.ToString();
            }
        }
    }

    public class FileIndexer
    {
        private static FileIndexer instance;
        private static readonly string IndexFile = Path.Combine(Settings.SettingsDirectory, "file_index.json");
        private Dictionary<string, LocalAddon> Index { get; set; }
        private ProgressBar ProgressBar { get; set; }
        private TextBlock ProgressBarText { get; set; }
        public ObservableCollection<AddonGroup> AddonGroups { get; set; } = new ObservableCollection<AddonGroup>();
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

        public HashSet<string> GetAllFilePaths()
        {
            HashSet<string> allFilePaths = new HashSet<string>();
            foreach (string addonSearchDirectory in Settings.Instance.GetAddonSearchDirectories())
            {
                string[] allfiles = Directory.GetFiles(addonSearchDirectory, "*.*", SearchOption.AllDirectories);
                foreach (string file in allfiles)
                {
                    if(file.Contains("@"))
                    {
                        allFilePaths.Add(file);
                    }                    
                }                
            }
            return allFilePaths;
        }

        public void SaveIndex()
        {
            Directory.CreateDirectory(Settings.SettingsDirectory);

            using StreamWriter file = File.CreateText(IndexFile);
            JsonSerializer serializer = new JsonSerializer();
            serializer.Formatting = Formatting.Indented;
            serializer.Serialize(file, Index);
        }

        public void LoadIndex()
        {
            if (!File.Exists(IndexFile))
            {
                Index = new Dictionary<string, LocalAddon>();
            }
            else
            {
                using StreamReader file = File.OpenText(IndexFile);
                JsonSerializer serializer = new JsonSerializer();
                Dictionary<string, LocalAddon> index = (Dictionary<string, LocalAddon>)serializer.Deserialize(file, typeof(Dictionary<string, LocalAddon>));
                Index = index;
            }            
        }

        public static string ExtractAddonName(string filePath)
        {
            if (!filePath.Contains("@"))
                return null;

            int index = filePath.IndexOf("@");
            string relativeFilePath = filePath.Substring(index);
            index = relativeFilePath.IndexOf("\\");
            return relativeFilePath.Substring(0, index);
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
            BackgroundWorker worker = new BackgroundWorker();
            worker.WorkerReportsProgress = true;
            worker.DoWork += LocalIndexingWorker_DoWork;
            worker.ProgressChanged += LocalIndexingWorker_ProgressChanged;
            worker.RunWorkerCompleted += LocalIndexingWorker_RunWorkerCompleted;
            worker.RunWorkerAsync();
        }

        private string ExtractAbsoluteAddonPath(string filePath)
        {
            string addonName = ExtractAddonName(filePath);
            int index = filePath.IndexOf("@") + addonName.Length;
            return filePath.Substring(0, index);
        }

        void LocalIndexingWorker_DoWork(object sender, DoWorkEventArgs e)
        {            
            // get all files from addon search directories
            HashSet<string> files = GetAllFilePaths();

            // remove addons from index that are not on disk anymore            
            List<string> removals = new List<string>();
            foreach (string indexKey in Index.Keys)
            {
                bool remove = true;
                foreach (string file in files)
                {
                    string absoluteAddonPath = ExtractAbsoluteAddonPath(file);
                    if (indexKey.Equals(absoluteAddonPath))
                    {
                        remove = false;
                        break;
                    }
                }

                if (remove)
                    removals.Add(indexKey);
            }
            foreach (string indexKey in removals)
            {
                Index.Remove(indexKey);                
            }

            // remove files from index which are not on disk anymore
            foreach (LocalAddon localAddon in Index.Values) 
            {
                removals = new List<string>();
                foreach (string filePath in localAddon.Files.Keys)
                {
                    if (!files.Contains(filePath))
                    {
                        removals.Add(filePath);
                    }
                }
                foreach (string key in removals)
                {
                    localAddon.Files.Remove(key);
                }
            }
            

            int i = 0;
            foreach (string file in files)
            {
                string absoluteAddonPath = ExtractAbsoluteAddonPath(file);
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
                    string addonName = ExtractAddonName(file);
                    localAddon = new LocalAddon(addonName, absoluteAddonPath);
                    Index.Add(absoluteAddonPath, localAddon);
                }

                if (!addonUuidToLocalAddonMap.ContainsKey(localAddon.Uuid))
                {
                    addonUuidToLocalAddonMap.Add(localAddon.Uuid, new List<LocalAddon>());
                }
                addonUuidToLocalAddonMap[localAddon.Uuid].Add(localAddon);
                    
                if(localAddon.Files.ContainsKey(file))
                {
                    // compare if files differ
                    LocalFileIndex existingIndex = localAddon.Files[file];

                    FileInfo fileInfo = new FileInfo(file);
                    DateTime created = fileInfo.CreationTime;
                    long filesize = fileInfo.Length;

                    if (!existingIndex.Created.Equals(created) || existingIndex.Filesize != filesize)
                    {
                        Log.Debug("out of date file: " + file);
                        // file is not up to date, index it
                        LocalFileIndex fileIndex = new LocalFileIndex(file);
                        localAddon.Files.Add(fileIndex.Absolute_filepath, fileIndex);
                    }
                }
                else
                {
                    // file is not indexed yet, index it
                    Log.Debug("file not indexed yet: " + file);
                    LocalFileIndex fileIndex = new LocalFileIndex(file);
                    localAddon.Files.Add(fileIndex.Absolute_filepath, fileIndex);
                }
                
                int percentage = (int)Math.Floor((double) ++i / files.Count * 100);
                (sender as BackgroundWorker).ReportProgress(percentage);
            }

            SaveIndex();
        }

        void LocalIndexingWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            ProgressBar.Value = e.ProgressPercentage;
            ProgressBarText.Text = string.Format("{0} ({1}%)", Properties.Resources.ProgressIndexingFiles, e.ProgressPercentage);
        }

        void LocalIndexingWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            ProgressBar.Value = 0;
            ProgressBarText.Text = Properties.Resources.ProgressIndexingComplete;
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
                        Debug.WriteLine(string.Format("linking webAddon={0} to localAddon={1}", webAddon.Name, localAddon.Name));
                        addonGroup.WebAddonToLocalAddonDict.Add(webAddon, localAddon);
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
                        changes.Add((addonGroup, AddonGroupState.Ready));
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

            Application.Current.Dispatcher.Invoke(new Action(() => {
                foreach ((AddonGroup addonGroup, AddonGroupState addonGroupState) in changes)
                {
                    addonGroup.State = addonGroupState;                 
                }
            }));
        }

        void AddonGroupStateWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            ProgressBar.Value = e.ProgressPercentage;
            ProgressBarText.Text = string.Format("{0} ({1}%)", Properties.Resources.ProgressComparingOnlineVersion, e.ProgressPercentage);
        }

        void AddonGroupStateWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            ProgressBar.Value = 0;
            ProgressBarText.Text = Properties.Resources.EverythingUpToDate;
        }
    }
}

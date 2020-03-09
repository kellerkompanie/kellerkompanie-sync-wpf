using Newtonsoft.Json;
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
            this.Name = addonName;
            this.Files = new Dictionary<string, LocalFileIndex>();
            this.Version = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            this.AbsoluteFilepath = absoluteFilepath;
            this.Uuid = WebAPI.LookUpAddonName(addonName);
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
                throw new ArgumentException("Parameter must contain @ in path");

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
        
        private FileIndexer(ProgressBar progressBar, TextBlock progressBarText)
        {
            LoadIndex();
            this.ProgressBar = progressBar;
            this.ProgressBarText = progressBarText;

            foreach (WebAddonGroupBase addon in WebAPI.GetAddonGroups())
            {
                AddonGroup addonGroup = new AddonGroup(addon);
                AddonGroups.Add(addonGroup);
            }
        }

        public void SetAddonGroupState(AddonGroup addonGroup, AddonGroupState state)
        {
            int index = AddonGroups.IndexOf(addonGroup);
            AddonGroups.RemoveAt(index);
            addonGroup.SetState(state);
            AddonGroups.Insert(index, addonGroup);
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
                    throw new FieldAccessException("You need to initialize FileIndexer first");
                return instance;
            }
        }

        public HashSet<string> GetAllFilePaths()
        {
            HashSet<string> allFilePaths = new HashSet<string>();
            foreach (string addonSearchDirectory in Settings.Instance.AddonSearchDirectories)
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
            

            double i = 0;
            foreach (string file in files)
            {
                string absoluteAddonPath = ExtractAbsoluteAddonPath(file);
                LocalAddon localAddon;
                if (Index.ContainsKey(absoluteAddonPath))
                {
                    // load addon from existing index
                    Debug.WriteLine("loading existing addon: " + absoluteAddonPath);
                    localAddon = Index[absoluteAddonPath];
                }
                else
                {
                    // create new addon
                    Debug.WriteLine("creating new addon: " + absoluteAddonPath);
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
                        Debug.WriteLine("out of date file: " + file);
                        // file is not up to date, index it
                        LocalFileIndex fileIndex = new LocalFileIndex(file);
                        localAddon.Files.Add(fileIndex.Absolute_filepath, fileIndex);
                    }
                }
                else
                {
                    // file is not indexed yet, index it
                    Debug.WriteLine("file not indexed yet: " + file);
                    LocalFileIndex fileIndex = new LocalFileIndex(file);
                    localAddon.Files.Add(fileIndex.Absolute_filepath, fileIndex);
                }
                

                i += 1;
                int percentage = (int)Math.Floor(i / files.Count * 100);
                (sender as BackgroundWorker).ReportProgress(percentage);
            }

            SaveIndex();
        }

        void LocalIndexingWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            ProgressBar.Value = e.ProgressPercentage;
            ProgressBarText.Text = "Indexing your files... (" + e.ProgressPercentage + "%)";
        }

        void LocalIndexingWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            ProgressBar.Value = 0;
            ProgressBarText.Text = "Indexing completed";
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
                string remoteUuid = addonGroup.WebAddonGroupBase.Uuid;
                string remoteVersion = addonGroup.WebAddonGroupBase.Version;
                
                if (Settings.Instance.SubscribedAddonGroups.ContainsKey(remoteUuid))
                {
                    string localVersion = Settings.Instance.SubscribedAddonGroups[remoteUuid];
                    if (localVersion.Equals(remoteVersion))
                    {
                        changes.Add((addonGroup, AddonGroupState.Ready));
                        //addonGroup.SetState(AddonGroupState.Ready);
                    }
                    else
                    {
                        changes.Add((addonGroup, AddonGroupState.NeedsUpdate));
                        //addonGroup.SetState(AddonGroupState.NeedsUpdate);
                    }
                }
                else
                {
                    WebAddonGroup webAddonGroup = WebAPI.GetAddonGroup(addonGroup.WebAddonGroupBase);
                    List<WebAddon> webAddons = webAddonGroup.Addons;
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
                        //addonGroup.SetState(AddonGroupState.CompleteButNotSubscribed);
                    }
                    else if (foundAddonsLocally > 0)
                    {
                        changes.Add((addonGroup, AddonGroupState.Partial));
                        //addonGroup.SetState(AddonGroupState.Partial);
                    }
                    else
                    {
                        changes.Add((addonGroup, AddonGroupState.NonExistent));
                        //addonGroup.SetState(AddonGroupState.NonExistent);
                    }
                }

                i += 1;
                int percentage = (int)Math.Floor(i / AddonGroups.Count * 100);
                (sender as BackgroundWorker).ReportProgress(percentage);
            }

            foreach((AddonGroup addonGroup, AddonGroupState addonGroupState) in changes)
            {
                Application.Current.Dispatcher.Invoke(new Action(() => { SetAddonGroupState(addonGroup, addonGroupState); }));                
            }
        }

        void AddonGroupStateWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            ProgressBar.Value = e.ProgressPercentage;
            ProgressBarText.Text = "Comparing online version... (" + e.ProgressPercentage + "%)";
        }

        void AddonGroupStateWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            ProgressBar.Value = 0;
            ProgressBarText.Text = "Everything up-to-date";
        }
    }
}

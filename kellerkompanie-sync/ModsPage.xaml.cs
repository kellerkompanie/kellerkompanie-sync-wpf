using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Path = System.IO.Path;

namespace kellerkompanie_sync
{
    public enum AddonGroupState
    {
        Unknown,
        NonExistent,
        Partial,
        CompleteButNotSubscribed,
        NeedsUpdate,
        Ready
    }

    public class AddonGroup : INotifyPropertyChanged
    {
        // public AddonGroupState State { get; set; }
        public WebAddonGroupBase WebAddonGroupBase { get; set; }
        public string Icon { get; set; }
        public string IconTooltip { get; set; }
        public string IconColor { get; set; }
        public string ButtonText { get; set; }
        public bool ButtonIsEnabled { get; set; }
        public string ButtonVisibility { get; set; }
        public string CheckBoxVisibility { get; set; }
        public bool CheckBoxIsSelected { get; set; }
        public string StatusText { get; set; }
        public Visibility StatusVisibility { get; set; }
        
        private AddonGroupState state;
        public AddonGroupState State
        {
            get { return state; }
            set
            {
                if (state != value)
                {
                    state = value;
                    NotifyPropertyChanged("State");
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void NotifyPropertyChanged(string propName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

        public AddonGroup(WebAddonGroupBase WebAddonGroupBase)
        {
            this.WebAddonGroupBase = WebAddonGroupBase;
            SetState(AddonGroupState.Unknown);
        }
                
        public void SetState(AddonGroupState newState)
        {
            Debug.WriteLine("setting " + WebAddonGroupBase.Name + " to " + newState);
            State = newState;
            switch(newState)
            {
                case AddonGroupState.Unknown:
                    Icon = "/Images/questionmark.png";
                    IconTooltip = "Unknown";
                    IconColor = "#d9534f";

                    CheckBoxVisibility = "Hidden";

                    ButtonVisibility = "Hidden";
                    ButtonText = "";
                    ButtonIsEnabled = false;
                    break;

                case AddonGroupState.NonExistent:
                    Icon = "/Images/link.png";
                    IconTooltip = "All mods missing";
                    IconColor = "#d9534f";

                    CheckBoxVisibility = "Hidden";

                    ButtonVisibility = "Visible";
                    ButtonText = "Subscribe";
                    ButtonIsEnabled = true;
                    break;

                case AddonGroupState.CompleteButNotSubscribed:
                    Icon = "/Images/link.png";
                    IconTooltip = "All mods downloaded, but not subscribed";
                    IconColor = "#5cb85c";

                    CheckBoxVisibility = "Hidden";

                    ButtonVisibility = "Visible";
                    ButtonText = "Subscribe";
                    ButtonIsEnabled = true;
                    break;

                case AddonGroupState.Partial:
                    Icon = "/Images/link.png";
                    IconTooltip = "Some mods already downloaded";
                    IconColor = "#f7c516";

                    CheckBoxVisibility = "Hidden";

                    ButtonVisibility = "Visible";
                    ButtonText = "Subscribe";
                    ButtonIsEnabled = true;
                    break;

                case AddonGroupState.NeedsUpdate:
                    Icon = "/Images/download.png";
                    IconTooltip = "Needs update";
                    IconColor = "#f7c516";

                    CheckBoxVisibility = "Hidden";

                    ButtonVisibility = "Visible";
                    ButtonText = "Update";
                    ButtonIsEnabled = true;
                    break;

                case AddonGroupState.Ready:
                    Icon = "/Images/checkmark.png";
                    IconTooltip = "Ready";
                    IconColor = "#5cb85c";

                    CheckBoxVisibility = "Visible";

                    ButtonVisibility = "Hidden";
                    ButtonText = "";
                    ButtonIsEnabled = false;
                    break;
            }
        }
    }

    public partial class ModsPage : Page
    {
        public ModsPage()
        {
            InitializeComponent();

            ListViewAddonGroups.ItemsSource = FileIndexer.Instance.AddonGroups;
        }

        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            CheckBox checkBox = (CheckBox)sender;
            AddonGroup addonGroup = (AddonGroup)checkBox.DataContext;
            addonGroup.CheckBoxIsSelected = checkBox.IsChecked == true;
        }

        private void ButtonDownload_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            AddonGroup addonGroup = (AddonGroup)button.DataContext;

            addonGroup.StatusText = "(downloading...)";
            addonGroup.StatusVisibility = Visibility.Visible;

            string downloadDirectory = null;
            if (addonGroup.State == AddonGroupState.CompleteButNotSubscribed)
            {
                // all addons are already downloaded, directly proceed with validation
                ValidateAddonGroup(addonGroup);
            }
            else { 
                // decide where to download missing addons to
                switch (Settings.Instance.AddonSearchDirectories.Count)
                {
                    case 0:
                        MessageBox.Show("Please first add a addon search directory under settings.", "kellerkompanie-sync");
                        return;

                    case 1:
                        foreach (string addonSearchDirectory in Settings.Instance.AddonSearchDirectories)
                        {
                            downloadDirectory = addonSearchDirectory;
                            break;
                        }
                        break;

                    default:
                        ChooseDirectoryWindow inputDialog = new ChooseDirectoryWindow();
                        if (inputDialog.ShowDialog() == true)
                            downloadDirectory = inputDialog.ChosenDirectory;
                        else
                            return;
                        break;
                }

                DownloadToDirectory(addonGroup, downloadDirectory);
            }
        }

        class DownloadArguments
        {
            public AddonGroup AddonGroup { get; set; }
            public string DownloadDirectory { get; set; }
        }

        private void DownloadToDirectory(AddonGroup addonGroup, string downloadDirectory)
        {
            BackgroundWorker worker = new BackgroundWorker();
            worker.WorkerReportsProgress = true;
            worker.DoWork += DownloadWorker_DoWork;
            worker.ProgressChanged += DownloadWorker_ProgressChanged;
            worker.RunWorkerCompleted += DownloadWorker_RunWorkerCompleted;
            DownloadArguments args = new DownloadArguments();
            args.AddonGroup = addonGroup;
            args.DownloadDirectory = downloadDirectory;
            worker.RunWorkerAsync(args);
        }
                       
        void DownloadWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            DownloadArguments args = (DownloadArguments)e.Argument;

            RemoteIndex remoteIndex = WebAPI.GetRemoteIndex();            

            // determine files to delete            
            WebAddonGroup webAddonGroup = WebAPI.GetAddonGroup(args.AddonGroup.WebAddonGroupBase);
            foreach (WebAddon webAddon in webAddonGroup.Addons)
            {
                string webAddonUuid = webAddon.Uuid;
                if (!FileIndexer.Instance.addonUuidToLocalAddonMap.ContainsKey(webAddonUuid))
                    continue;

                RemoteAddon remAddon = remoteIndex.Map[webAddon.Foldername];

                List<LocalAddon> localAddons = FileIndexer.Instance.addonUuidToLocalAddonMap[webAddonUuid];
                foreach (LocalAddon localAddon in localAddons)
                {
                    List<string> removals = new List<string>();
                    foreach (LocalFileIndex fileIndex in localAddon.Files.Values)
                    {
                        string relativeFilepath = fileIndex.Relative_filepath;
                        if (!remAddon.AddonFiles.ContainsKey(relativeFilepath.Replace("\\", "/")))
                        {
                            string filePath = fileIndex.Absolute_filepath;
                            Log.Debug("deleting " + filePath);
                            removals.Add(filePath);
                            File.Delete(@filePath);
                        }
                    }
                    foreach (string removal in removals)
                    {
                        localAddon.Files.Remove(removal);
                    }
                }
            }

            // determine files to download
            List<(string, string, long)> downloads = new List<(string, string, long)>();
            foreach (WebAddon webAddon in webAddonGroup.Addons)
            {
                RemoteAddon remoteAddon = remoteIndex.Map[webAddon.Foldername];
                string uuid = webAddon.Uuid;
                string name = webAddon.Name;

                if (!remoteAddon.AddonUuid.Equals(uuid))
                {
                    throw new InvalidOperationException("uuid " + uuid + " of local addon " + name + " does not match remote uuid " + remoteAddon.AddonUuid + " of addon " + remoteAddon.AddonName);
                }

                string destinationFolder = args.DownloadDirectory;
                foreach (string addonSearchDirectory in Settings.Instance.AddonSearchDirectories)
                {
                    destinationFolder = addonSearchDirectory;
                    break;
                }
                               
                if (!FileIndexer.Instance.addonUuidToLocalAddonMap.ContainsKey(uuid))
                {
                    // download all
                    foreach (RemoteAddonFile remoteAddonFile in remoteAddon.AddonFiles.Values)
                    {
                        string remoteFilePath = remoteAddonFile.FilePath;
                        string destinationFilePath = Path.Combine(destinationFolder, remoteFilePath.Replace("/", "\\"));
                        downloads.Add((WebAPI.RepoUrl + "/" + remoteFilePath, destinationFilePath, remoteAddonFile.FileSize));
                    }
                }
                else
                {
                    List<LocalAddon> localAddons = FileIndexer.Instance.addonUuidToLocalAddonMap[uuid];
                    foreach (RemoteAddonFile remoteAddonFile in remoteAddon.AddonFiles.Values)
                    {
                        string remoteFilePath = remoteAddonFile.FilePath;
                        string remoteHash = remoteAddonFile.FileHash;

                        foreach (LocalAddon localAddon in localAddons)
                        {
                            foreach (LocalFileIndex fileIndex in localAddon.Files.Values)
                            {
                                if (remoteFilePath.Equals(fileIndex.Relative_filepath))
                                {
                                    if (!fileIndex.Hash.Equals(remoteHash))
                                    {
                                        string destinationFilepath = Path.Combine(destinationFolder, remoteFilePath.Replace("/", "\\"));
                                        downloads.Add((WebAPI.RepoUrl + "/" + remoteFilePath, destinationFilepath, remoteAddonFile.FileSize));
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            DownloadManager downloadManager = new DownloadManager(args.AddonGroup);
            downloadManager.DownloadsFinished += DownloadManager_DownloadsFinished;
            downloadManager.ProgressChanged += DownloadManager_ProgressChanged;
                        
            foreach ((string url, string destinationFile, long filesize) in downloads)
                downloadManager.AddDownload(url, destinationFile, filesize);

            downloadManager.StartDownloads();
        }

        void DownloadManager_DownloadsFinished(object sender, bool status)
        {
            DownloadManager downloadManager = sender as DownloadManager;

            ValidateAddonGroup(downloadManager.AddonGroup);
            
            Application.Current.Dispatcher.Invoke(new Action(() => {
                MainWindow wnd = (MainWindow)Window.GetWindow(this);
                wnd.ProgressBar.Value = 0;
                wnd.ProgressBarText.Text = "Everything up-to-date";                                     
            }));            
        }
        
        void DownloadManager_ProgressChanged(object sender, DownloadProgress downloadProgress)
        {
            Debug.WriteLine("progress: {0}", downloadProgress.Progress);

            Application.Current.Dispatcher.Invoke(new Action(() => {
                MainWindow wnd = (MainWindow)Window.GetWindow(this);
                wnd.ProgressBar.Value = downloadProgress.Progress;
                                
                string downloadSize = string.Format("{0:n1}/{1:n1} MB", downloadProgress.CurrentDownloadSize / 1024 / 1024, downloadProgress.TotalDownloadSize / 1024 / 1024);
                string downloadSpeed = string.Format("{0:n1} MB/s", downloadProgress.DownloadSpeed / 1024);
                TimeSpan t = TimeSpan.FromSeconds(downloadProgress.RemainingTime);
                string remainingTime;
                if (t.Hours > 0)
                {
                    remainingTime = string.Format("{0}h:{1}m:{2}s left", t.Hours, t.Minutes, t.Seconds);
                }
                else if(t.Hours == 0 && t.Minutes > 0)
                {
                    remainingTime = string.Format("{0}m:{1}s left", t.Minutes, t.Seconds);
                }
                else
                {
                    remainingTime = string.Format("{0}s left", t.Seconds);
                }
                
                wnd.ProgressBarText.Text = string.Format("Downloading mods... ({0} @ {1}, {2})", downloadSize, downloadSpeed, remainingTime);
            }));
        }          

        void DownloadWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            
        }

        void DownloadWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            
        }

        private void ValidateAddonGroup(AddonGroup addonGroup)
        {
            // TODO update existing information, i.e., versions, hashes etc.

            Application.Current.Dispatcher.Invoke(new Action(() => {
                addonGroup.StatusText = "";
                addonGroup.StatusVisibility = Visibility.Hidden;
                FileIndexer.Instance.SetAddonGroupState(addonGroup, AddonGroupState.Ready); 
            }));            
        }
    }
}

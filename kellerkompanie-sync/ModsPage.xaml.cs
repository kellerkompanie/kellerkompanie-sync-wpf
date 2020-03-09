using kellerkompanie_sync;
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
            this.State = newState;
            switch(newState)
            {
                case AddonGroupState.Unknown:
                    Icon = "/Images/questionmark.png";
                    IconTooltip = "Unknown";
                    IconColor = "Red";

                    CheckBoxVisibility = "Hidden";

                    ButtonVisibility = "Hidden";
                    ButtonText = "";
                    ButtonIsEnabled = false;
                    break;

                case AddonGroupState.NonExistent:
                    Icon = "/Images/link.png";
                    IconTooltip = "All mods missing";
                    IconColor = "Red";

                    CheckBoxVisibility = "Hidden";

                    ButtonVisibility = "Visible";
                    ButtonText = "Subscribe";
                    ButtonIsEnabled = true;
                    break;

                case AddonGroupState.CompleteButNotSubscribed:
                    Icon = "/Images/link.png";
                    IconTooltip = "All mods downloaded, but not subscribed";
                    IconColor = "Green";

                    CheckBoxVisibility = "Hidden";

                    ButtonVisibility = "Visible";
                    ButtonText = "Subscribe";
                    ButtonIsEnabled = true;
                    break;

                case AddonGroupState.Partial:
                    Icon = "/Images/link.png";
                    IconTooltip = "Some mods already downloaded";
                    IconColor = "Yellow";

                    CheckBoxVisibility = "Hidden";

                    ButtonVisibility = "Visible";
                    ButtonText = "Subscribe";
                    ButtonIsEnabled = true;
                    break;

                case AddonGroupState.NeedsUpdate:
                    Icon = "/Images/download.png";
                    IconTooltip = "Needs update";
                    IconColor = "Yellow";

                    CheckBoxVisibility = "Hidden";

                    ButtonVisibility = "Visible";
                    ButtonText = "Update";
                    ButtonIsEnabled = true;
                    break;

                case AddonGroupState.Ready:
                    Icon = "/Images/checkmark.png";
                    IconTooltip = "Ready";
                    IconColor = "Green";

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

            BackgroundWorker worker = new BackgroundWorker();
            worker.WorkerReportsProgress = true;
            worker.DoWork += DownloadWorker_DoWork;
            worker.ProgressChanged += DownloadWorker_ProgressChanged;
            worker.RunWorkerCompleted += DownloadWorker_RunWorkerCompleted;
            worker.RunWorkerAsync(addonGroup);
        }
                
        void DownloadWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            AddonGroup addonGroup = (AddonGroup)e.Argument;

            RemoteIndex remoteIndex = WebAPI.GetRemoteIndex();            

            // determine files to delete            
            WebAddonGroup webAddonGroup = WebAPI.GetAddonGroup(addonGroup.WebAddonGroupBase);
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
                            Debug.WriteLine("deleting " + filePath);
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

                // TODO select proper destination folder on disk, maybe ask user
                string destinationFolder = "";
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

            DownloadManager downloadManager = new DownloadManager(addonGroup);
            downloadManager.DownloadsFinished += DownloadManager_DownloadsFinished;
            downloadManager.ProgressChanged += DownloadManager_ProgressChanged;
            downloadManager.SpeedChanged += DownloadManager_SpeedChanged;
                        
            foreach ((string url, string destinationFile, long filesize) in downloads)
                downloadManager.AddDownload(url, destinationFile);

            downloadManager.StartDownloads();
        }

        void DownloadManager_DownloadsFinished(object sender, bool status)
        {
            DownloadManager downloadManager = sender as DownloadManager;
            Application.Current.Dispatcher.Invoke(new Action(() => {
                MainWindow wnd = (MainWindow)Window.GetWindow(this);
                wnd.ProgressBar.Value = 0;
                wnd.ProgressBarText.Text = "Everything up-to-date";

                FileIndexer.Instance.SetAddonGroupState(downloadManager.AddonGroup, AddonGroupState.Ready);             
            }));            
        }

        void DownloadManager_SpeedChanged(object sender, double speed)
        {
            Speed = speed;
            UpdateProgressUI();
        }

        void DownloadManager_ProgressChanged(object sender, double progress)
        {
            Progress = progress;
            UpdateProgressUI();
        }

        private double Progress = 0.0;
        private double Speed = 0.0;
        private void UpdateProgressUI()
        {
            Application.Current.Dispatcher.Invoke(new Action(() => {
                MainWindow wnd = (MainWindow)Window.GetWindow(this);
                wnd.ProgressBar.Value = Progress;
                wnd.ProgressBarText.Text = "Downloading mods... (" + (int)Progress + "% @ " + string.Format("{0:n1} Kb/s", Speed) + ")";
            }));
        }            

        void DownloadWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            
        }

        void DownloadWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            // TODO update existing information, i.e., versions, hashes etc.
        }
    }
}

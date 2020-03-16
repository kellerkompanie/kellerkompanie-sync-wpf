using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Path = System.IO.Path;

namespace kellerkompanie_sync
{
    public partial class ModsPage : Page
    {
        public ModsPage()
        {
            InitializeComponent();

            foreach (AddonGroup addonGroup in FileIndexer.Instance.AddonGroups)
            {
                addonGroup.Parent = ListViewAddonGroups;
            }
            ListViewAddonGroups.ItemsSource = FileIndexer.Instance.AddonGroups;

            //BackgroundWorker worker = new BackgroundWorker();
            //worker.DoWork += DebugNotifier_DoWork;
            //worker.RunWorkerAsync();
        }

        void DebugNotifier_DoWork(object sender, DoWorkEventArgs e)
        {
            List<AddonGroupState> addonGroupStates = new List<AddonGroupState>() 
            { 
                AddonGroupState.CompleteButNotSubscribed,
                AddonGroupState.NeedsUpdate,
                AddonGroupState.NonExistent,
                AddonGroupState.Partial,
                AddonGroupState.Ready,
                AddonGroupState.Unknown
            };
            Random random = new Random();
            while (true)
            {
                int n = FileIndexer.Instance.AddonGroups.Count;
                AddonGroup randomAddonGroup = FileIndexer.Instance.AddonGroups[random.Next(n)];
                var randomState = addonGroupStates[random.Next(addonGroupStates.Count)];
                randomAddonGroup.State = randomState;
                Debug.WriteLine(string.Format("setting group {0} to {1}", randomAddonGroup, randomState));
                foreach (AddonGroup addonGroup in FileIndexer.Instance.AddonGroups)
                {
                    addonGroup.ButtonVisibility = random.Next(0, 2) == 0 ? Visibility.Visible : Visibility.Hidden;
                    addonGroup.StatusVisibility = random.Next(0, 2) == 0 ? Visibility.Visible : Visibility.Hidden;
                    addonGroup.CheckBoxVisibility = random.Next(0, 2) == 0 ? Visibility.Visible : Visibility.Hidden;
                }               
                
                Thread.Sleep(1000);
            }
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
            else
            {
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

            RemoteFileIndex remoteIndex = WebAPI.GetFileIndex();

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
                        if (!remAddon.Files.ContainsKey(relativeFilepath.Replace("\\", "/")))
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

                if (!remoteAddon.Uuid.Equals(uuid))
                {
                    throw new InvalidOperationException("uuid " + uuid + " of local addon " + name + " does not match remote uuid " + remoteAddon.Uuid + " of addon " + remoteAddon.Name);
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
                    foreach (RemoteAddonFile remoteAddonFile in remoteAddon.Files.Values)
                    {
                        string remoteFilePath = remoteAddonFile.Path;
                        string destinationFilePath = Path.Combine(destinationFolder, remoteFilePath.Replace("/", "\\"));
                        downloads.Add((WebAPI.RepoUrl + "/" + remoteFilePath, destinationFilePath, remoteAddonFile.Size));
                    }
                }
                else
                {
                    List<LocalAddon> localAddons = FileIndexer.Instance.addonUuidToLocalAddonMap[uuid];
                    foreach (RemoteAddonFile remoteAddonFile in remoteAddon.Files.Values)
                    {
                        string remoteFilePath = remoteAddonFile.Path;
                        string remoteHash = remoteAddonFile.Hash;

                        foreach (LocalAddon localAddon in localAddons)
                        {
                            foreach (LocalFileIndex fileIndex in localAddon.Files.Values)
                            {
                                if (remoteFilePath.Equals(fileIndex.Relative_filepath))
                                {
                                    if (!fileIndex.Hash.Equals(remoteHash))
                                    {
                                        string destinationFilepath = Path.Combine(destinationFolder, remoteFilePath.Replace("/", "\\"));
                                        downloads.Add((WebAPI.RepoUrl + "/" + remoteFilePath, destinationFilepath, remoteAddonFile.Size));
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

            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                MainWindow wnd = (MainWindow)Window.GetWindow(this);
                wnd.ProgressBar.Value = 0;
                wnd.ProgressBarText.Text = "Everything up-to-date";
            }));
        }

        void DownloadManager_ProgressChanged(object sender, DownloadProgress downloadProgress)
        {
            Debug.WriteLine("progress: {0}", downloadProgress.Progress);

            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
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
                else if (t.Hours == 0 && t.Minutes > 0)
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

            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                addonGroup.StatusText = "";
                addonGroup.StatusVisibility = Visibility.Hidden;
                FileIndexer.Instance.SetAddonGroupState(addonGroup, AddonGroupState.Ready);
            }));
        }
    }
}

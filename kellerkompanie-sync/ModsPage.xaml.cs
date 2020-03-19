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
        }

        private DownloadManager downloadManager = null;

        private void ButtonDownload_Click(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            AddonGroup addonGroup = (AddonGroup)button.DataContext;

            if (downloadManager == null)
            {
                // start new download
                foreach (AddonGroup addonGrp in FileIndexer.Instance.AddonGroups)
                {
                    if (addonGroup != addonGrp)
                    {
                        addonGrp.ButtonIsEnabled = false;
                    }
                }

                MainWindow.Instance.PlayUpdateButton.IsEnabled = false;

                string downloadDirectory = null;
                if (addonGroup.State == AddonGroupState.CompleteButNotSubscribed)
                {
                    // all addons are already downloaded, directly proceed with validation
                    addonGroup.StatusText = Properties.Resources.ProgressValidating;
                    addonGroup.StatusVisibility = Visibility.Visible;
                    ValidateAddonGroup(addonGroup);
                }
                else
                {
                    // decide where to download missing addons to
                    switch (Settings.Instance.AddonSearchDirectories.Count)
                    {
                        case 0:
                            MessageBox.Show(Properties.Resources.MissingAddonSearchDirectoryInfoMessage, "kellerkompanie-sync");
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
                            {
                                downloadDirectory = inputDialog.ChosenDirectory;
                            }
                            else
                            {
                                MainWindow.Instance.PlayUpdateButton.IsEnabled = true;
                                foreach (AddonGroup addonGrp in FileIndexer.Instance.AddonGroups)
                                {
                                    addonGrp.ButtonIsEnabled = true;
                                }
                                return;
                            }
                            break;
                    }

                    addonGroup.StatusText = Properties.Resources.ProgressDownloading;
                    addonGroup.StatusVisibility = Visibility.Visible;
                    addonGroup.ButtonText = Properties.Resources.Pause;

                    BackgroundWorker worker = new BackgroundWorker();
                    worker.WorkerReportsProgress = true;
                    worker.DoWork += DownloadWorker_DoWork;
                    worker.ProgressChanged += DownloadWorker_ProgressChanged;
                    worker.RunWorkerCompleted += DownloadWorker_RunWorkerCompleted;
                    DownloadArguments args = new DownloadArguments
                    {
                        AddonGroup = addonGroup,
                        DownloadDirectory = downloadDirectory
                    };
                    worker.RunWorkerAsync(args);
                }
            }
            else if (downloadManager?.State == DownloadState.Paused)
            {
                // previous download is about to be resumed
                downloadManager.StartDownloads();
            }
            else if (downloadManager?.State == DownloadState.Downloading)
            {
                // download is currently running, pause it
                downloadManager.PauseDownloads();
            }
        }

        class DownloadArguments
        {
            public AddonGroup AddonGroup { get; set; }
            public string DownloadDirectory { get; set; }
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

            downloadManager = new DownloadManager(args.AddonGroup);
            downloadManager.StateChanged += DownloadManager_StateChanged;
            downloadManager.ProgressChanged += DownloadManager_ProgressChanged;

            foreach ((string url, string destinationFile, long filesize) in downloads)
            {
                downloadManager.AddDownload(url, destinationFile, filesize);
            }

            downloadManager.StartDownloads();
        }

        void DownloadManager_StateChanged(object sender, DownloadState state)
        {
            DownloadManager downloadManager = sender as DownloadManager;

            switch (state)
            {
                case DownloadState.Completed:
                    ValidateAddonGroup(downloadManager.AddonGroup);

                    Application.Current.Dispatcher.Invoke(new Action(() =>
                    {
                        MainWindow mainWindow = (MainWindow)Window.GetWindow(this);
                        mainWindow.ProgressBar.Value = 0;
                        mainWindow.ProgressBarText.Text = Properties.Resources.EverythingUpToDate;
                        downloadManager.AddonGroup.StatusText = "";
                    }));
                    break;

                case DownloadState.Paused:
                    Application.Current.Dispatcher.Invoke(new Action(() =>
                    {
                        downloadManager.AddonGroup.ButtonText = Properties.Resources.Continue;
                        downloadManager.AddonGroup.StatusText = Properties.Resources.ProgressDownloadPaused;
                    }));
                    break;

                case DownloadState.Downloading:
                    Application.Current.Dispatcher.Invoke(new Action(() =>
                    {
                        downloadManager.AddonGroup.ButtonText = Properties.Resources.Pause;
                        downloadManager.AddonGroup.StatusText = Properties.Resources.ProgressDownloading;
                    }));
                    break;
            }
        }

        void DownloadManager_ProgressChanged(object sender, DownloadProgress downloadProgress)
        {
            Debug.WriteLine("progress: {0}", downloadProgress.Progress);

            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                MainWindow mainWindow = (MainWindow)Window.GetWindow(this);
                mainWindow.ProgressBar.Value = downloadProgress.Progress;

                string downloadSize = string.Format("{0:n1}/{1:n1} MB", downloadProgress.CurrentDownloadSize / 1024 / 1024, downloadProgress.TotalDownloadSize / 1024 / 1024);
                string downloadSpeed = string.Format("{0:n1} MB/s", downloadProgress.DownloadSpeed / 1024);
                TimeSpan t = TimeSpan.FromSeconds(downloadProgress.RemainingTime);
                string remainingTime;
                if (t.Hours > 0)
                {
                    remainingTime = string.Format("{0}h:{1}m:{2}s {3}", t.Hours, t.Minutes, t.Seconds, Properties.Resources.Left);
                }
                else if (t.Hours == 0 && t.Minutes > 0)
                {
                    remainingTime = string.Format("{0}m:{1}s {2}", t.Minutes, t.Seconds, Properties.Resources.Left);
                }
                else
                {
                    remainingTime = string.Format("{0}s {1}", t.Seconds, Properties.Resources.Left);
                }

                mainWindow.ProgressBarText.Text = string.Format("{0} ({1} @ {2}, {3})", Properties.Resources.DownloadingModsProgress, downloadSize, downloadSpeed, remainingTime);
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
                downloadManager = null;
                addonGroup.StatusText = "";
                addonGroup.StatusVisibility = Visibility.Hidden;
                addonGroup.State = AddonGroupState.Ready;
                MainWindow.Instance.PlayUpdateButton.IsEnabled = true;
                foreach (AddonGroup addonGrp in FileIndexer.Instance.AddonGroups)
                {
                    addonGrp.ButtonIsEnabled = true;
                }
            }));
        }
    }
}

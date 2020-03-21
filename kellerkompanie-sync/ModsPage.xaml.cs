﻿using Serilog;
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

                // disable all buttons that should not be pressed while download is running
                foreach (AddonGroup addonGrp in FileIndexer.Instance.AddonGroups)
                {
                    if (addonGroup != addonGrp)
                    {
                        addonGrp.ButtonIsEnabled = false;
                    }
                }
                MainWindow.Instance.PlayUpdateButton.IsEnabled = false;

                // in case of missing addons decide where to download them to, if all addons already exist locally skip this step
                string downloadDirectoryForMissingAddons = null;
                if (addonGroup.State != AddonGroupState.CompleteButNotSubscribed)
                {
                    // decide where to download missing addons to                    
                    switch (Settings.Instance.GetAddonSearchDirectories().Count)
                    {
                        case 0:
                            // there is no addon search directory set, tell user to choose at least one
                            MessageBox.Show(Properties.Resources.MissingAddonSearchDirectoryInfoMessage, "kellerkompanie-sync");
                            return;

                        case 1:
                            // there is exactly one folder set as search directory, so this one will be download destination for all
                            downloadDirectoryForMissingAddons = Settings.Instance.GetAddonSearchDirectories()[0];
                            break;

                        default:
                            // there is more than one addon search directory, make user choose to which one he wants to download missing files
                            ChooseDirectoryWindow inputDialog = new ChooseDirectoryWindow();
                            if (inputDialog.ShowDialog() == true)
                            {
                                downloadDirectoryForMissingAddons = inputDialog.ChosenDirectory;
                            }
                            else
                            {
                                MainWindow.Instance.EnablePlayButton();
                                foreach (AddonGroup addonGrp in FileIndexer.Instance.AddonGroups)
                                {
                                    addonGrp.ButtonIsEnabled = true;
                                }
                                return;
                            }
                            break;
                    }
                }

                Dictionary<WebAddon, string> webAddonToDownloadDirectoryDict = new Dictionary<WebAddon, string>();
                WebAddonGroup webAddonGroup = addonGroup.WebAddonGroup;
                foreach (WebAddon webAddon in webAddonGroup.Addons)
                {
                    if (addonGroup.WebAddonToLocalAddonDict.ContainsKey(webAddon))
                    {
                        // some addons might already exist, for these download to existing folder
                        LocalAddon existingLocalAddon = addonGroup.WebAddonToLocalAddonDict[webAddon];
                        string parentFolder = Directory.GetParent(existingLocalAddon.AbsoluteFilepath).FullName;
                        webAddonToDownloadDirectoryDict.Add(webAddon, parentFolder);
                    }
                    else
                    {
                        // for all others choose the previously selected folder
                        Debug.Assert(addonGroup.State != AddonGroupState.CompleteButNotSubscribed);
                        webAddonToDownloadDirectoryDict.Add(webAddon, downloadDirectoryForMissingAddons);
                    }
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
                    DownloadDirectoryDict = webAddonToDownloadDirectoryDict
                };
                worker.RunWorkerAsync(args);                
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
            public Dictionary<WebAddon, string> DownloadDirectoryDict { get; set; }
        }

        void DownloadWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            DownloadArguments args = (DownloadArguments)e.Argument;

            RemoteIndex remoteIndex = WebAPI.GetIndex();

            // determine files to delete            
            WebAddonGroup webAddonGroup = args.AddonGroup.WebAddonGroup;
            foreach (WebAddon webAddon in webAddonGroup.Addons)
            {
                string webAddonUuid = webAddon.Uuid;
                if (!FileIndexer.Instance.addonUuidToLocalAddonMap.ContainsKey(webAddonUuid))
                    continue;

                RemoteAddon remAddon = remoteIndex.FilesIndex[webAddon.Foldername];

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
                RemoteAddon remoteAddon = FileIndexer.Instance.RemoteIndex.FilesIndex[webAddon.Foldername];
                string uuid = webAddon.Uuid;
                string name = webAddon.Name;

                if (!remoteAddon.Uuid.Equals(uuid))
                {
                    throw new InvalidOperationException("uuid " + uuid + " of local addon " + name + " does not match remote uuid " + remoteAddon.Uuid + " of addon " + remoteAddon.Name);
                }

                string destinationFolder = args.DownloadDirectoryDict[webAddon];

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
                    Dictionary<string, LocalFileIndex> relativeFilePathToFileIndexMap = new Dictionary<string, LocalFileIndex>();
                    List<LocalAddon> localAddons = FileIndexer.Instance.addonUuidToLocalAddonMap[uuid];
                    foreach (LocalAddon localAddon in localAddons)
                    {
                        foreach (LocalFileIndex fileIndex in localAddon.Files.Values)
                        {
                            relativeFilePathToFileIndexMap.Add(fileIndex.Relative_filepath, fileIndex);
                        }
                    }

                    // parts of the addon already exist, update existing and download missing                   
                    foreach (RemoteAddonFile remoteAddonFile in remoteAddon.Files.Values)
                    {
                        string remoteFilePath = remoteAddonFile.Path.Replace("/", "\\");
                        string remoteHash = remoteAddonFile.Hash;

                        if (relativeFilePathToFileIndexMap.ContainsKey(remoteFilePath))
                        {
                            LocalFileIndex localFileIndex = relativeFilePathToFileIndexMap[remoteFilePath];
                            if (!remoteHash.Equals(localFileIndex.Hash))
                            {
                                string destinationFilepath = Path.Combine(destinationFolder, remoteFilePath);
                                downloads.Add((WebAPI.RepoUrl + "/" + remoteFilePath, destinationFilepath, remoteAddonFile.Size));
                            }                            
                        }
                        else
                        {
                            string destinationFilepath = Path.Combine(destinationFolder, remoteFilePath);
                            downloads.Add((WebAPI.RepoUrl + "/" + remoteFilePath, destinationFilepath, remoteAddonFile.Size));
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
            if (downloadProgress.DownloadSpeed == 0)
            {
                return;
            }

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
                MainWindow.Instance.EnablePlayButton();
                foreach (AddonGroup addonGrp in FileIndexer.Instance.AddonGroups)
                {
                    addonGrp.ButtonIsEnabled = true;
                }
            }));
        }
    }
}

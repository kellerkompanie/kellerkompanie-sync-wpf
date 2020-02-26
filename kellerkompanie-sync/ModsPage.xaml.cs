﻿using kellerkompanie_sync;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Path = System.IO.Path;

namespace kellerkompanie_sync_wpf
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
        public bool CheckBoxIsEnabled { get; set; }
        public bool CheckBoxIsSelected { get; set; }


        private AddonGroupState state;
        public AddonGroupState State
        {
            get { return this.state; }
            set
            {
                if (this.state != value)
                {
                    this.state = value;
                    this.NotifyPropertyChanged("State");
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void NotifyPropertyChanged(string propName)
        {
            if (this.PropertyChanged != null)
                this.PropertyChanged(this, new PropertyChangedEventArgs(propName));
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

                    CheckBoxIsEnabled = false;

                    ButtonVisibility = "Hidden";
                    ButtonText = "";
                    ButtonIsEnabled = false;
                    break;
                case AddonGroupState.NonExistent:
                    Icon = "/Images/link.png";
                    IconTooltip = "All mods missing";
                    IconColor = "Red";

                    CheckBoxIsEnabled = false;

                    ButtonVisibility = "Visible";
                    ButtonText = "Subscribe";
                    ButtonIsEnabled = true;
                    break;
                case AddonGroupState.CompleteButNotSubscribed:
                    Icon = "/Images/link.png";
                    IconTooltip = "All mods downloaded, but not subscribed";
                    IconColor = "Green";

                    CheckBoxIsEnabled = false;

                    ButtonVisibility = "Visible";
                    ButtonText = "Subscribe";
                    ButtonIsEnabled = true;
                    break;
                case AddonGroupState.Partial:
                    Icon = "/Images/link.png";
                    IconTooltip = "Some mods already downloaded";
                    IconColor = "Yellow";

                    CheckBoxIsEnabled = false;

                    ButtonVisibility = "Visible";
                    ButtonText = "Subscribe";
                    ButtonIsEnabled = true;
                    break;
                case AddonGroupState.NeedsUpdate:
                    Icon = "/Images/download.png";
                    IconTooltip = "Needs update";
                    IconColor = "Yellow";

                    CheckBoxIsEnabled = false;

                    ButtonVisibility = "Visible";
                    ButtonText = "Update";
                    ButtonIsEnabled = true;
                    break;
                case AddonGroupState.Ready:
                    Icon = "/Images/checkmark.png";
                    IconTooltip = "Ready";
                    IconColor = "Green";

                    CheckBoxIsEnabled = true;

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
        
        private void DownloadFile(string sourceUrl, string destinationFile, long filesize)
        {
            Debug.WriteLine($"downloading: {sourceUrl} --> {destinationFile}");
            var directory = Path.GetDirectoryName(destinationFile);
            Directory.CreateDirectory(directory);

            using var client = new WebClient();
            client.DownloadFile(sourceUrl, destinationFile);
            long destinationFilesize = new FileInfo(destinationFile).Length;
            if (filesize != destinationFilesize)
            {
                throw new InvalidDataException($"downloaded file has different size than expected, expected {filesize} bytes but got {destinationFilesize} bytes");
            }
        }

        void wc_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            MainWindow wnd = (MainWindow)Window.GetWindow(this);
            wnd.ProgressBar.Value = e.ProgressPercentage;
            Debug.WriteLine(e.ProgressPercentage);
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

                RemoteAddon remAddon = remoteIndex.map[webAddon.Foldername];

                List<LocalAddon> localAddons = FileIndexer.Instance.addonUuidToLocalAddonMap[webAddonUuid];
                foreach (LocalAddon localAddon in localAddons)
                {
                    List<string> removals = new List<string>();
                    foreach (LocalFileIndex fileIndex in localAddon.Files.Values)
                    {
                        string relativeFilepath = fileIndex.Relative_filepath;
                        if (!remAddon.addon_files.ContainsKey(relativeFilepath.Replace("\\", "/")))
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
                RemoteAddon remoteAddon = remoteIndex.map[webAddon.Foldername];
                string uuid = webAddon.Uuid;
                string name = webAddon.Name;

                if (!remoteAddon.addon_uuid.Equals(uuid))
                {
                    throw new InvalidOperationException("uuid " + uuid + " of local addon " + name + " does not match remote uuid " + remoteAddon.addon_uuid + " of addon " + remoteAddon.addon_name);
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
                    foreach (RemoteAddonFile remoteAddonFile in remoteAddon.addon_files.Values)
                    {
                        string remoteFilePath = remoteAddonFile.file_path;
                        string destinationFilePath = Path.Combine(destinationFolder, remoteFilePath.Replace("/", "\\"));
                        downloads.Add((WebAPI.RepoUrl + "/" + remoteFilePath, destinationFilePath, remoteAddonFile.file_size));
                    }
                }
                else
                {
                    List<LocalAddon> localAddons = FileIndexer.Instance.addonUuidToLocalAddonMap[uuid];
                    foreach (RemoteAddonFile remoteAddonFile in remoteAddon.addon_files.Values)
                    {
                        string remoteFilePath = remoteAddonFile.file_path;
                        string remoteHash = remoteAddonFile.file_hash;

                        foreach (LocalAddon localAddon in localAddons)
                        {
                            foreach (LocalFileIndex fileIndex in localAddon.Files.Values)
                            {
                                if (remoteFilePath.Equals(fileIndex.Relative_filepath))
                                {
                                    if (!fileIndex.Hash.Equals(remoteHash))
                                    {
                                        string destinationFilepath = Path.Combine(destinationFolder, remoteFilePath.Replace("/", "\\"));
                                        downloads.Add((WebAPI.RepoUrl + "/" + remoteFilePath, destinationFilepath, remoteAddonFile.file_size));
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            // update existing information, i.e., versions, hashes etc.
            double i = 0;
            foreach ((string url, string destinationFile, long filesize) in downloads)
            {
                DownloadFile(url, destinationFile, filesize);

                i += 1;
                int percentage = (int)Math.Floor(i / downloads.Count * 100);
                (sender as BackgroundWorker).ReportProgress(percentage);
            }

            Application.Current.Dispatcher.Invoke(new Action(() => { FileIndexer.Instance.SetAddonGroupState(addonGroup, AddonGroupState.Ready); }));            
        }

        void DownloadWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            MainWindow wnd = (MainWindow)Window.GetWindow(this);
            wnd.ProgressBar.Value = e.ProgressPercentage;
            wnd.ProgressBarText.Text = "Downloading mods... (" + e.ProgressPercentage + "%)";
        }

        void DownloadWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            MainWindow wnd = (MainWindow)Window.GetWindow(this);
            wnd.ProgressBar.Value = 0;
            wnd.ProgressBarText.Text = "Everything up-to-date";
        }        
    }
}

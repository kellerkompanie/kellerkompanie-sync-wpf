using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace kellerkompanie_sync
{
    public class DownloadProgress
    {
        public double DownloadSpeed { get; set; }
        public double CurrentDownloadSize { get; set; }
        public double TotalDownloadSize { get; set; }
        public double RemainingTime { get; set; }
        public double ActiveDownloads { get; set; }
        public double Progress { get; set; }
    }

    public class DownloadManager
    {
        private bool isDownloading = false;
        private readonly List<Download> queuedDownloads = new List<Download>();
        private readonly List<Download> runningDownloads = new List<Download>();
        private readonly List<Download> finishedDownloads = new List<Download>();

        private static readonly ReaderWriterLock ReaderWriterLock = new ReaderWriterLock();
        private const int TIMEOUT = 100;

        private long totalDownloadSize = 0;

        public EventHandler<bool> DownloadsFinished;
        public EventHandler<DownloadProgress> ProgressChanged;
        public AddonGroup AddonGroup { get; }

        /// a timer used to update downloading information every x millisecond
        private readonly System.Timers.Timer timer = new System.Timers.Timer(500);

        public DownloadManager(AddonGroup addonGroup)
        {
            Log.Debug(string.Format("DownloadManager: creating instance for AddonGroup: {0}", addonGroup));
            AddonGroup = addonGroup;
        }

        public void StartDownloads()
        {
            isDownloading = true;
            timer.Elapsed += Timer_Elapsed;
            timer.Start();

            int n = Math.Min(Settings.Instance.SimultaneousDownloads, queuedDownloads.Count);
            Log.Debug(string.Format("DownloadManager: starting with {0} simultaneous downloads", n));
            for (int i = 0; i < n; i++)
            {
                DownloadNext();
            }
        }

        private void DownloadNext()
        {
            Download download = queuedDownloads[0];
            download.StateChanged += Download_StateChanged;
            queuedDownloads.RemoveAt(0);
            download.StartDownload();
        }

        public void PauseDownloads()
        {
            isDownloading = false;
            timer.Stop();

            ReaderWriterLock.AcquireWriterLock(TIMEOUT);
            try
            {
                foreach (Download download in runningDownloads)
                    download.PauseDownload();

                runningDownloads.Clear();
            }
            finally
            {
                ReaderWriterLock.ReleaseWriterLock();
            }
        }

        public void AddDownload(string sourceUrl, string filepath, long expectedFileSize)
        {
            if (isDownloading)
                throw new InvalidOperationException("can only add new downloads while downloader is not running");

            Download download = new Download(sourceUrl, filepath);
            totalDownloadSize += expectedFileSize;
            queuedDownloads.Add(download);
        }

        private void Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            double downloadSpeed = 0.0;
            int numTotalDownloads = 0;
            int numActiveDownloads = 0;
            int numFinishedDownloads = 0;
            long currentDownloadSize = 0;

            ReaderWriterLock.AcquireReaderLock(TIMEOUT);
            try
            {
                numTotalDownloads = queuedDownloads.Count + runningDownloads.Count + finishedDownloads.Count;
                numActiveDownloads = runningDownloads.Count;
                numFinishedDownloads = finishedDownloads.Count;

                foreach (Download download in runningDownloads)
                {
                    downloadSpeed += download.DownloadSpeed;
                    currentDownloadSize += download.DownloadedSize;
                }                    

                foreach (Download download in finishedDownloads)
                {
                    currentDownloadSize += download.FileSize;
                }
            }
            finally
            {
                ReaderWriterLock.ReleaseReaderLock();
            }

            DownloadProgress progress = new DownloadProgress();
            progress.DownloadSpeed = downloadSpeed;
            progress.ActiveDownloads = numActiveDownloads;
            progress.CurrentDownloadSize = currentDownloadSize;
            progress.TotalDownloadSize = totalDownloadSize;
            progress.RemainingTime = (totalDownloadSize - currentDownloadSize) / 1024 / downloadSpeed;
            progress.Progress = Math.Floor(((double) numFinishedDownloads / (double) numTotalDownloads) * 100.0);
            ProgressChanged.Invoke(this, progress);
        }

        private void Download_StateChanged(object sender, DownloadState e)
        {
            Download download = sender as Download;
            Debug.WriteLine("state changed: " + download);

            switch (download.DownloadState)
            {
                case DownloadState.Completed:
                    ReaderWriterLock.AcquireWriterLock(TIMEOUT);
                    try
                    {
                        runningDownloads.Remove(download);
                        finishedDownloads.Add(download);
                        
                        if (queuedDownloads.Count > 0)
                        {
                            DownloadNext();
                        }
                        else if (queuedDownloads.Count == 0 && runningDownloads.Count == 0)
                        {
                            isDownloading = false;
                            timer.Stop();
                            Log.Debug("DownloadManager: all downloads finished");
                            DownloadsFinished?.Invoke(this, true);
                        }
                    }
                    finally
                    {
                        ReaderWriterLock.ReleaseWriterLock();
                    }
                    break;

                case DownloadState.Downloading:
                    ReaderWriterLock.AcquireWriterLock(TIMEOUT);
                    try
                    {
                        runningDownloads.Add(download);
                        Debug.WriteLine("running downloads: " + runningDownloads.Count);
                    }
                    finally
                    {
                        ReaderWriterLock.ReleaseWriterLock();
                    }
                    break;

                case DownloadState.Paused:
                    ReaderWriterLock.AcquireWriterLock(TIMEOUT);
                    try
                    {
                        runningDownloads.Remove(download);
                        queuedDownloads.Add(download);
                    }
                    finally
                    {
                        ReaderWriterLock.ReleaseWriterLock();
                    }
                    break;

                default:
                    break;
            }
        }
    }
}

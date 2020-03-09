using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace kellerkompanie_sync
{
    public class DownloadManager
    {
        private bool isDownloading = false;
        private readonly List<Download> queuedDownloads = new List<Download>();
        private readonly List<Download> runningDownloads = new List<Download>();
        private readonly List<Download> finishedDownloads = new List<Download>();

        private static readonly ReaderWriterLock ReaderWriterLock = new ReaderWriterLock();
        private const int TIMEOUT = 100;

        public EventHandler<bool> DownloadsFinished;
        public EventHandler<double> ProgressChanged;
        public EventHandler<double> SpeedChanged;
        public AddonGroup AddonGroup { get; }

        public DownloadManager(AddonGroup addonGroup)
        {
            AddonGroup = addonGroup;
        }

        public void StartDownloads()
        {
            isDownloading = true;
            int n = Math.Min(Settings.Instance.SimultaneousDownloads, queuedDownloads.Count);
            for (int i = 0; i < n; i++)
            {
                DownloadNext();
            }
        }

        private void DownloadNext()
        {
            Download download = queuedDownloads[0];
            queuedDownloads.RemoveAt(0);
            download.StateChanged += Download_StateChanged;
            download.SpeedChanged += Download_SpeedChanged;
            download.StartDownload();
        }

        public void PauseDownloads()
        {
            isDownloading = false;

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

        public void AddDownload(string sourceUrl, string filepath)
        {
            if (isDownloading)
                throw new InvalidOperationException("can only add new downloads while downloader is not running");

            ReaderWriterLock.AcquireWriterLock(TIMEOUT);
            try
            {
                Download download = new Download(sourceUrl, filepath);
                queuedDownloads.Add(download);
            }
            finally
            {
                ReaderWriterLock.ReleaseWriterLock();
            }
        }

        private double GetProgressUI()
        {
            ReaderWriterLock.AcquireReaderLock(TIMEOUT);
            try
            {
                double total = queuedDownloads.Count + runningDownloads.Count + finishedDownloads.Count;
                double finished = finishedDownloads.Count;
                return Math.Floor((finished / total) * 100);
            }
            finally
            {
                ReaderWriterLock.ReleaseReaderLock();
            }
        }

        private void Download_SpeedChanged(object sender, double e)
        {
            double speed = 0.0;
            ReaderWriterLock.AcquireReaderLock(TIMEOUT);
            try
            {
                foreach (Download download in runningDownloads)
                    speed += download.DownloadSpeed;
            }
            finally
            {
                ReaderWriterLock.ReleaseReaderLock();
            }

            SpeedChanged?.Invoke(this, speed);
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

                        ProgressChanged?.Invoke(this, GetProgressUI());

                        if (queuedDownloads.Count > 0)
                        {
                            DownloadNext();
                        }
                        else if (queuedDownloads.Count == 0 && runningDownloads.Count == 0)
                        {
                            isDownloading = false;
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
                        ProgressChanged?.Invoke(this, GetProgressUI());
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

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
        private readonly List<Download> queuedDownloads = new();
        private readonly List<Download> runningDownloads = new();
        private readonly List<Download> finishedDownloads = new();

        private static readonly ReaderWriterLock ReaderWriterLock = new();
        private const int TIMEOUT = 100;
        private readonly System.Timers.Timer timer = new(500);
        private long totalDownloadSize = 0;

        public DownloadState State { get; private set; } = DownloadState.Created;
        public AddonGroup AddonGroup { get; }
        public EventHandler<DownloadState> StateChanged;
        public EventHandler<DownloadProgress> ProgressChanged;                

        public DownloadManager(AddonGroup addonGroup)
        {
            Log.Debug(string.Format("DownloadManager: creating instance for AddonGroup: {0}", addonGroup));
            AddonGroup = addonGroup;
        }

        public void StartDownloads()
        {
            if (queuedDownloads.Count == 0)
            {
                Log.Debug("DownloadManager StartDownloads(): no downloads in queue, finishing");
                State = DownloadState.Completed;
                StateChanged?.Invoke(this, State);
                return;
            }

            Log.Debug(string.Format("DownloadManager starting with queued={0} running={1} finished={2} total={3}", queuedDownloads.Count, runningDownloads.Count, finishedDownloads.Count, queuedDownloads.Count + runningDownloads.Count + finishedDownloads.Count));

            State = DownloadState.Downloading;
            timer.Elapsed += Timer_Elapsed;           
            timer.Start();

            int n = Math.Min(Settings.Instance.SimultaneousDownloads, queuedDownloads.Count);
            Log.Debug(string.Format("DownloadManager: starting with {0} simultaneous downloads", n));
            for (int i = 0; i < n; i++)
            {
                DownloadNext();
            }

            StateChanged?.Invoke(this, State);
        }

        private void DownloadNext()
        {
            ReaderWriterLock.AcquireWriterLock(TIMEOUT);
            try
            {
                Download download = queuedDownloads[0];
                download.StateChanged += Download_StateChanged;
                queuedDownloads.RemoveAt(0);
                download.StartDownload();
            }
            finally
            {
                ReaderWriterLock.ReleaseWriterLock();
            }            
        }

        public void PauseDownloads()
        {
            State = DownloadState.Paused;
            timer.Elapsed -= Timer_Elapsed;
            timer.Stop();            

            ReaderWriterLock.AcquireWriterLock(TIMEOUT);
            try
            {
                foreach (Download download in runningDownloads)
                {
                    download.PauseDownload();
                }
            }
            finally
            {
                ReaderWriterLock.ReleaseWriterLock();
            }

            StateChanged?.Invoke(this, State);
        }

        public void AddDownload(string sourceUrl, FilePath filepath, long expectedFileSize)
        {
            if (State != DownloadState.Created)
            {
                throw new InvalidOperationException("can only add new downloads before downloader started");
            }

            Download download = new(sourceUrl, filepath);
            totalDownloadSize += expectedFileSize;
            queuedDownloads.Add(download);
        }

        private void Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            double downloadSpeed = 0.0;
            int numActiveDownloads = 0;
            long currentDownloadSize = 0;

            ReaderWriterLock.AcquireReaderLock(TIMEOUT);
            try
            {
                numActiveDownloads = runningDownloads.Count;

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

            DownloadProgress progress = new()
            {
                DownloadSpeed = downloadSpeed,
                ActiveDownloads = numActiveDownloads,
                CurrentDownloadSize = currentDownloadSize,
                TotalDownloadSize = totalDownloadSize,
                RemainingTime = (totalDownloadSize - currentDownloadSize) / 1024 / downloadSpeed,
                Progress = Math.Floor(((double)currentDownloadSize / (double)totalDownloadSize) * 100.0)
            };
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
                        
                        if (queuedDownloads.Count > 0 && State != DownloadState.Paused && runningDownloads.Count < Settings.Instance.SimultaneousDownloads)
                        {
                            DownloadNext();
                        }
                        else if (queuedDownloads.Count == 0 && runningDownloads.Count == 0)
                        {
                            State = DownloadState.Completed;
                            timer.Elapsed -= Timer_Elapsed;
                            timer.Stop();                            
                            Log.Debug("DownloadManager: all downloads finished");
                            StateChanged?.Invoke(this, State);
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
                        download.StateChanged -= Download_StateChanged;
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

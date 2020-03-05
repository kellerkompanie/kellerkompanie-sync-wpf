using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace kellerkompanie_sync_wpf
{
    public class Download
    {
        /// Represent url of the file on the internet.
        Uri uri;

        /// Make request to the uri.
        System.Net.HttpWebRequest req;

        /// Get response from 'req'.
        System.Net.HttpWebResponse res;

        /// Read bytes from 'res'.
        Stream stream;

        /// Write bytes from 'stream' to the file on harddisk.
        FileStream fStream;

        /// Represent the start time of download.
        DateTime dt;

        /// Array of bytes to store downloaded bytes from 'stream'.
        byte[] buffer = new byte[1024];

        /// Store the total numbers of bytes from the 'buffer'.
        int bufferReader = 0;

        /// Represent the total length of the requested file.
        long fLength = 0;

        /// Represent the downloaded length of the requested file.
        long dLength = 0;

        /// Represent the downloaded length of the requested file, from the point of started.
        long cLength = 0;

        /// Represent the download speed.
        double dSpeed = 0;

        /// Use to check if the class is currently downloading the file.
        bool isDownloading = false;

        /// Use to stop the downloading.
        bool cancelDownload = false;

        /// Use to startfresh and overwrite a file on harddisk if exists ( ignore resuming ).
        bool overwrite = false;

        /// A timer use to update downloading-speed every x millisecond.
        System.Timers.Timer dsTimer = new System.Timers.Timer(1000);

        /// A timer use to update downloaded-length every x millisecond.
        System.Timers.Timer dlTimer = new System.Timers.Timer(1000);

        string filePath;

        public long FileSize
        {
            get { return fLength; }
        }

        public long DownloadedLength
        {
            get { return dLength; }
        }

        public string DownloadingSpeed
        {
            // {x:n1} Mean Math.round(x,2)
            get { return string.Format("{0:n1} Kb/s", dSpeed); }
        }

        public double DownloadingSpeedNumeric
        {
            get { return dSpeed; }
        }

        public double DSpeedUI
        {
            get { return dsTimer.Interval; }
            set { dsTimer.Interval = value; }
        }

        public double DLengthUI
        {
            get { return dlTimer.Interval; }
            set { dlTimer.Interval = value; }
        }

        public int BufferSize
        {
            get
            {
                return BufferSize;
            }
            set
            {
                // buffer cannot change during downloading.
                if (!isDownloading && value > 32 && value < int.MaxValue)
                {
                    buffer = new byte[value];
                    BufferSize = value;
                }
            }
        }

        public string DownloadState
        {
            get
            {
                if (isDownloading)
                    return "Downloading";
                else if (dLength < fLength)
                    return "Paused";
                else
                    return "Completed";
            }
        }

        public EventHandler<long> eSize;
        public EventHandler<long> eDownloadedSize;
        public EventHandler<string> eSpeed;
        public EventHandler<double> eSpeedNumeric;
        public EventHandler<string> eDownloadState;

        public async void StartDownload()
        {
            // Make sure folders exist
            var directory = Path.GetDirectoryName(filePath);
            Directory.CreateDirectory(directory);

            // Check if file exists and not overwrite (overwrite used to start download from 0 and overwrite the file if exists).
            if (File.Exists(filePath) && !overwrite)
            {
                ResumeDownload();
            }
            else
            {
                fStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);

                dLength = fStream.Length;   // if file not exists fStream.Length will return 0.
                
                dlTimer.Elapsed += DlTimer_Elapsed;     // assign method to dlTimer.
                dsTimer.Elapsed += DsTimer_Elapsed;     // assign method to dsTimer.
            }

            dlTimer.Start();
            dsTimer.Start();
            // Set 'cancelDownload' to false, so that method can stop again.
            cancelDownload = false;
            req = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(uri);
            // check if downloaded-length!=0 and !overwrite so the user want to resume.
            if (dLength > 0 && !overwrite)
                req.AddRange(dLength);      // add range to the 'req' to change the point of start download.
            isDownloading = true;
            using (res = (System.Net.HttpWebResponse)await req.GetResponseAsync())
            {
                fLength = res.ContentLength + dLength; // get the total-size of the file.
                if (eSize != null)
                    eSize.Invoke(this, FileSize);       // update the total-size.
                if (eDownloadedSize != null)
                    eDownloadedSize.Invoke(this, DownloadedLength); // update the downloaded-length.
                dt = DateTime.Now;       // get the current time ( point of start downloading ).
                using (stream = res.GetResponseStream())
                {
                    await System.Threading.Tasks.Task.Run(() =>     // await task so the winform don't freezing.
                    {
                        // update the download-state.
                        eDownloadState.Invoke(this, "Downloading");
                        // while not 'cancelDownload' and file doesn't end do:
                        while (!cancelDownload && ((bufferReader = stream.Read(buffer, 0, buffer.Length)) > 0))
                        {
                            fStream.Write(buffer, 0, bufferReader); // write byte to the file on harddisk.
                            dLength += bufferReader;    // update downloaded-length value.
                            cLength += bufferReader;    // update current-downloaded-length value.
                        }
                    });
                }
            }
            dlTimer.Stop();
            dsTimer.Stop();
            isDownloading = false;
            if (eSpeed != null)
                eSpeed.Invoke(this, "0.0 Kb/s");    // update downloading-speed to 0.0 kb/s.
            if (eSpeedNumeric != null)
                eSpeedNumeric.Invoke(this, 0.0);
            if (eDownloadedSize != null)
                eDownloadedSize.Invoke(this, DownloadedLength); // update downloaded-size.
            if (eDownloadState != null)
                eDownloadState.Invoke(this, DownloadState);     // update download-state.
            fStream.Dispose();      // free file on harddisk by dispose 'fStream'.
        }

        private void DlTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            // Call the event-handler.
            if (eDownloadedSize != null)
                eDownloadedSize.Invoke(this, DownloadedLength);
        }

        private void DsTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            // Download Speed In Kb/s = ( Downloaded-Length In KB ) / ( Total Seconds From Point Of Start ).
            dSpeed = (cLength / 1024) / ((DateTime.Now - dt).TotalSeconds);
            if (eSpeed != null) 
                eSpeed.Invoke(this, DownloadingSpeed); // Call the event-handler.
            if (eSpeedNumeric != null)
                eSpeedNumeric.Invoke(this, DownloadingSpeedNumeric);
        }

        public void CancelDownload()
        {
            cancelDownload = true;
        }

        public void ResumeDownload()
        {
            if (DownloadState == "Paused")
            {
                if (File.Exists(filePath) && !overwrite)
                {
                    fStream = new FileStream(filePath, FileMode.Append, FileAccess.Write);
                    if (dLength == fStream.Length)
                    {
                        StartDownload();
                        return;
                    }
                }
            }
            throw new ArgumentException("Cannot Resume Download");
        }

        public Download(string url, string filePath, bool overwrite = false)
        {
            // Convert string to uri using Uri.TryCreate.
            bool validUri = Uri.TryCreate(url, UriKind.Absolute, out uri);

            if (!validUri)      // Check if not valid uri then throw new error.
                throw new ArgumentException("Invalid url");
            this.filePath = filePath;

            this.overwrite = overwrite;
        }

        public void Dispose()
        {
            dlTimer.Stop();
            dsTimer.Stop();
            if (fStream != null) fStream.Dispose();
            if (res != null) res.Dispose();
            if (stream != null) stream.Dispose();
            GC.SuppressFinalize(this);
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder("{");
            sb.Append(DownloadState);
            sb.Append(",");
            sb.Append(uri);
            sb.Append("}");
            return sb.ToString();
        }
    }

    public class DownloadManager 
    {
        bool isDownloading = false;
        List<Download> queuedDownloads = new List<Download>();
        List<Download> runningDownloads = new List<Download>();
        List<Download> finishedDownloads = new List<Download>();

        private static ReaderWriterLock rwl = new ReaderWriterLock();
        private const int TIMEOUT = 100;

        public EventHandler<bool> eDownloadsFinished;
        public EventHandler<double> eProgressChanged;
        public EventHandler<double> eSpeedChanged;
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
            download.eDownloadState += Download_StateChanged;
            download.eSpeedNumeric += Download_SpeedChanged;
            download.StartDownload();
        }
        
        public void PauseDownloads()
        {
            isDownloading = false;

            rwl.AcquireWriterLock(TIMEOUT);
            try
            {
                foreach (Download download in runningDownloads)
                {
                    download.CancelDownload();
                    queuedDownloads.Add(download);
                }
            
                runningDownloads.Clear();
            }
            finally
            {
                rwl.ReleaseWriterLock();
            }
        }

        public void AddDownload(string sourceUrl, string filepath)
        {
            if (isDownloading)
            {
                throw new InvalidOperationException("can only add new downloads while downloader is not running");
            }

            rwl.AcquireWriterLock(TIMEOUT);
            try
            {
                Download download = new Download(sourceUrl, filepath);
                queuedDownloads.Add(download);
            }
            finally
            {
                rwl.ReleaseWriterLock();
            }
        }

        private double GetProgressUI()
        {
            rwl.AcquireReaderLock(TIMEOUT);
            try
            {
                double total = queuedDownloads.Count + runningDownloads.Count + finishedDownloads.Count;
                double finished = finishedDownloads.Count;
                return Math.Floor((finished / total) * 100);
            }
            finally
            {
                rwl.ReleaseReaderLock();
            }
        }

        private void Download_SpeedChanged(object sender, double e)
        {
            double speed = 0.0;
            rwl.AcquireReaderLock(TIMEOUT);
            try
            {                
                foreach (Download download in runningDownloads)
                {
                    speed += download.DownloadingSpeedNumeric;
                }
            }
            finally
            {
                rwl.ReleaseReaderLock();
            }

            if (eSpeedChanged != null)
                eSpeedChanged.Invoke(this, speed);
        }

        private void Download_StateChanged(object sender, string e)
        {
            Download download = sender as Download;
            Debug.WriteLine("state changed: " + download);

            switch (download.DownloadState)
            {
                case "Completed":
                    rwl.AcquireWriterLock(TIMEOUT);
                    try
                    {
                        runningDownloads.Remove(download);
                        finishedDownloads.Add(download);

                        if (eProgressChanged != null)
                            eProgressChanged.Invoke(this, GetProgressUI());

                        if (queuedDownloads.Count > 0)
                        {
                            DownloadNext();
                        }
                        else if (queuedDownloads.Count == 0 && runningDownloads.Count == 0)
                        {
                            isDownloading = false;
                            if (eDownloadsFinished != null)
                            {
                                eDownloadsFinished.Invoke(this, true);
                            }
                        }
                    }
                    finally
                    {
                        rwl.ReleaseWriterLock();
                    }
                    break;
                case "Downloading":
                    rwl.AcquireWriterLock(TIMEOUT);
                    try
                    {
                        runningDownloads.Add(download);
                        if (eProgressChanged != null)
                            eProgressChanged.Invoke(this, GetProgressUI());
                        Debug.WriteLine("running downloads: " + runningDownloads.Count);
                    }
                    finally
                    {
                        rwl.ReleaseWriterLock();
                    }
                    break;
                case "Paused":
                    rwl.AcquireWriterLock(TIMEOUT);
                    try
                    {
                        runningDownloads.Remove(download);
                        queuedDownloads.Add(download);
                    }
                    finally
                    {
                        rwl.ReleaseWriterLock();
                    }
                    break;
                default:
                    break;
            }            
        }
    }
}

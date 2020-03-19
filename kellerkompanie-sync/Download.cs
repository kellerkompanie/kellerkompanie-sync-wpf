using Serilog;
using System;
using System.IO;
using System.Text;

namespace kellerkompanie_sync
{
    public enum DownloadState
    {
        Created,
        Downloading,
        Paused,
        Completed
    }

    public class Download
    {
        /// Represent url of the file on the internet
        private Uri uri;
        private System.Net.HttpWebRequest request;
        private System.Net.HttpWebResponse response;
        private Stream stream;
        private FileStream fileStream;

        /// Start time of download.
        private DateTime downloadStart;

        private readonly byte[] buffer = new byte[4096];
        private int bufferBytesRead = 0;

        /// Represent the total length of the requested file.
        public long FileSize { get; private set; } = 0;

        /// Represent the downloaded length of the requested file.
        public long DownloadedSize { get; private set; } = 0;

        /// Represent the downloaded length of the requested file, from the point of started.
        private long CurrentSize = 0;

        public double DownloadSpeed { get; private set; } = 0;  

        private readonly string filePath;

        public DownloadState DownloadState { get; private set; } = DownloadState.Created;

        public EventHandler<long> eSize;
        public EventHandler<long> SizeChanged;
        public EventHandler<DownloadState> StateChanged;

        public Download(string url, string filePath)
        {
            // convert string to uri using Uri.TryCreate
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri))
                throw new ArgumentException("invalid url");

            this.filePath = filePath;
        }

        public async void StartDownload()
        {
            Log.Debug("starting download: " + this);

            var directory = Path.GetDirectoryName(filePath);
            Directory.CreateDirectory(directory);                        
            fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);

            // if file not exists fileStream.Length will return 0
            DownloadedSize = fileStream.Length;   
                        
            request = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(uri);
            // check if downloaded-length != 0, which equals to download resume
            if (DownloadedSize > 0)
            {
                // continue download at previous point
                request.AddRange(DownloadedSize);      
            }
            
            using (response = (System.Net.HttpWebResponse)await request.GetResponseAsync())
            {
                using (stream = response.GetResponseStream())
                {
                    await System.Threading.Tasks.Task.Run(() =>
                    {
                        FileSize = response.ContentLength + DownloadedSize;
                        eSize?.Invoke(this, FileSize);
                        SizeChanged?.Invoke(this, DownloadedSize);
                        downloadStart = DateTime.Now;

                        // update the download-state.
                        DownloadState = DownloadState.Downloading;
                        StateChanged.Invoke(this, DownloadState.Downloading);

                        while (DownloadState == DownloadState.Downloading && ((bufferBytesRead = stream.Read(buffer, 0, buffer.Length)) > 0))
                        {
                            fileStream.Write(buffer, 0, bufferBytesRead);
                            DownloadedSize += bufferBytesRead; 
                            CurrentSize += bufferBytesRead; 
                            DownloadSpeed = (CurrentSize / 1024) / ((DateTime.Now - downloadStart).TotalSeconds);
                        }
                    });
                }
            }
            
            fileStream.Dispose();
                        
            if (DownloadedSize == FileSize)
            {
                Log.Debug("finished download: " + this);
                DownloadState = DownloadState.Completed;
            }

            DownloadSpeed = 0;
            StateChanged?.Invoke(this, DownloadState);            
            SizeChanged?.Invoke(this, DownloadedSize);
        }
        

        public void PauseDownload()
        {
            DownloadState = DownloadState.Paused;
        }

        public void ResumeDownload()
        {
            if (DownloadState != DownloadState.Paused)
                throw new InvalidOperationException("can only resume paused downloads");
            
            StartDownload();            
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder("{");
            sb.Append(DownloadState);
            sb.Append(", ");
            sb.Append(uri);
            sb.Append("}");
            return sb.ToString();
        }     
    }    
}

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

        private readonly FilePath filePath;

        public DownloadState DownloadState { get; private set; } = DownloadState.Created;

        public EventHandler<DownloadState> StateChanged;

        public Download(string url, FilePath filePath)
        {
            // convert string to uri using Uri.TryCreate
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri))
            {
                throw new ArgumentException("invalid url");
            }                

            this.filePath = filePath;
        }

        public async void StartDownload()
        {
            Log.Debug("starting download: " + this);

            string directory = Path.GetDirectoryName(filePath.Value);
            Directory.CreateDirectory(directory);                        
            fileStream = new FileStream(filePath.Value, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);                       
                        
            request = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(uri);                        
            using (response = (System.Net.HttpWebResponse)await request.GetResponseAsync())
            {
                using (stream = response.GetResponseStream())
                {
                    await System.Threading.Tasks.Task.Run(() =>
                    {
                        DownloadedSize = 0;
                        FileSize = response.ContentLength;                        
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

            fileStream.Close();
            fileStream.Dispose();
                        
            if (DownloadedSize == FileSize)
            {
                Log.Debug("finished download: " + this);
                DownloadState = DownloadState.Completed;
            }

            DownloadSpeed = 0;
            StateChanged?.Invoke(this, DownloadState);
        }
        
        public void PauseDownload()
        {
            DownloadState = DownloadState.Paused;
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

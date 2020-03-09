using System;
using System.IO;
using System.Text;

namespace kellerkompanie_sync_wpf
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

        private readonly byte[] buffer = new byte[1024];
        private int bufferBytesRead = 0;

        /// Represent the total length of the requested file.
        public long FileSize { get; private set; } = 0;

        /// Represent the downloaded length of the requested file.
        public long DownloadedSize { get; private set; } = 0;

        /// Represent the downloaded length of the requested file, from the point of started.
        private long CurrentSize = 0;

        public double DownloadSpeed { get; set; } = 0; 

        /// a timer used to update downloading information every x millisecond
        private readonly System.Timers.Timer timer = new System.Timers.Timer(1000);

        private readonly string filePath;

        public DownloadState DownloadState { get; private set; } = DownloadState.Created;

        public EventHandler<long> eSize;
        public EventHandler<long> SizeChanged;
        public EventHandler<double> SpeedChanged;
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
            var directory = Path.GetDirectoryName(filePath);
            Directory.CreateDirectory(directory);                        
            fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);

            DownloadedSize = fileStream.Length;   // if file not exists fileStream.Length will return 0
            timer.Elapsed += OnTimerElapsed;     // assign method to dlTimer
            timer.Start();
            
            request = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(uri);
            // check if downloaded-length != 0, which equals to download resume
            if (DownloadedSize > 0)
                request.AddRange(DownloadedSize);      // add range to the 'req' to change the point of start download.
            
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
                            fileStream.Write(buffer, 0, bufferBytesRead); // write byte to the file on harddisk.
                            DownloadedSize += bufferBytesRead; // update downloaded-length value.
                            CurrentSize += bufferBytesRead; // update current-downloaded-length value.
                        }
                    });
                }
            }

            timer.Stop();
            fileStream.Dispose();
                        
            if (DownloadedSize == FileSize)
            {
                DownloadState = DownloadState.Completed;
            }
                
            StateChanged?.Invoke(this, DownloadState);
            SpeedChanged?.Invoke(this, 0.0);
            SizeChanged?.Invoke(this, DownloadedSize);
        }

        private void OnTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            SizeChanged?.Invoke(this, DownloadedSize);

            DownloadSpeed = (CurrentSize / 1024) / ((DateTime.Now - downloadStart).TotalSeconds);
            SpeedChanged?.Invoke(this, DownloadSpeed);
        }

        public void PauseDownload()
        {
            DownloadState = DownloadState.Paused;
        }

        public void ResumeDownload()
        {
            if (DownloadState == DownloadState.Paused)
            {
                StartDownload();
            }
            throw new InvalidOperationException("can only resume paused downloads");
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
}

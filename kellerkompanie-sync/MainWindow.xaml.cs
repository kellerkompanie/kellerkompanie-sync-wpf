using Serilog;
using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace kellerkompanie_sync
{
    public partial class MainWindow : Window
    {
        public static MainWindow Instance { get; private set; }

        public MainWindow()
        {
            Instance = this;

            Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File(Settings.LogFile, rollingInterval: RollingInterval.Day)
            .CreateLogger();
            Log.Information("Logger initialized");

            InitializeComponent();

            ProgressBar.Value = 0;
            ProgressBarText.Text = Properties.Resources.EverythingUpToDate;

            NavigateToPage(Page.News);

            Settings.Instance.LoadSettings();
            RestoreWindowPositionAndSize();

            FileIndexer.Setup(ProgressBar, ProgressBarText);
            FileIndexer.Instance.UpdateLocalIndex();

            EnablePlayButton();
        }

        public void EnablePlayButton()
        {
            if (File.Exists(Settings.Instance.ExecutableLocation))
            {
                PlayUpdateButton.IsEnabled = true;
                PlayUpdateButton.ToolTip = null;
            }
            else
            {
                PlayUpdateButton.IsEnabled = false;
                PlayUpdateButton.ToolTip = Properties.Resources.ExecutableNotFoundTooltip;
            }
        }

        enum Page
        {
            News,
            Mods,
            Settings
        }

        private void ButtonTeamspeak_Click(object sender, RoutedEventArgs e)
        {
            LaunchUri("ts3server://ts.kellerkompanie.com?port=9987");
        }

        private void ButtonForum_Click(object sender, RoutedEventArgs e)
        {
            LaunchUri("https://forum.kellerkompanie.com");
        }

        private void ButtonServer_Click(object sender, RoutedEventArgs e)
        {
            LaunchUri("http://server.kellerkompanie.com");
        }

        private void ButtonWiki_Click(object sender, RoutedEventArgs e)
        {
            LaunchUri("https://wiki.kellerkompanie.com");
        }

        private void buttonTFAR_Click(object sender, RoutedEventArgs e)
        {
            if (IsTeamspeakRunning())
            {
                MessageBox.Show(Properties.Resources.TeamspeakNotRunningInfoMessage, "kellerkompanie-sync");
                return;
            }

            LaunchUri("http://server.kellerkompanie.com/repository/task_force_radio.ts3_plugin");
        }

        public static void LaunchUri(string uri)
        {
            Process.Start(new ProcessStartInfo(uri) { CreateNoWindow = true, UseShellExecute = true });
        }

        private void ButtonNews_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage(Page.News);
        }

        private void ButtonMods_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage(Page.Mods);
        }

        private void ButtonSettings_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage(Page.Settings);
        }

        private void ButtonWebsite_Click(object sender, RoutedEventArgs e)
        {
            LaunchUri("https://kellerkompanie.com");
        }

        private readonly Brush orange = (SolidColorBrush)(new BrushConverter().ConvertFrom("#ee4d2e"));
        private readonly Brush white = (SolidColorBrush)(new BrushConverter().ConvertFrom("#f2f5f4"));

        private void NavigateToPage(Page page)
        {
            string destination = null;

            switch (page)
            {
                case Page.News:
                    destination = "NewsPage.xaml";
                    buttonNews.Foreground = orange;
                    buttonMods.Foreground = white;
                    buttonSettings.Foreground = white;
                    break;

                case Page.Mods:
                    destination = "ModsPage.xaml";
                    buttonNews.Foreground = white;
                    buttonMods.Foreground = orange;
                    buttonSettings.Foreground = white;
                    break;

                case Page.Settings:
                    destination = "SettingsPage.xaml";
                    buttonNews.Foreground = white;
                    buttonMods.Foreground = white;
                    buttonSettings.Foreground = orange;
                    break;
            }

            MainFrame.Navigate(new Uri(destination, UriKind.Relative));
        }

        private bool IsProcessRunning(string processName)
        {
            Process[] pname = Process.GetProcessesByName(processName);
            if (pname.Length == 0)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        private bool IsSteamRunning()
        {
            return IsProcessRunning("Steam");
        }

        private bool IsArmaRunning()
        {
            return IsProcessRunning(Path.GetFileNameWithoutExtension(Settings.Instance.ExecutableLocation));
        }

        private bool IsTeamspeakRunning()
        {
            return IsProcessRunning("ts3client_win64");
        }

        private readonly System.Timers.Timer timer = new System.Timers.Timer(5000);

        private void PlayUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            if (!IsSteamRunning())
            {
                Log.Information("PlayUpdateButton_Click: steam is not running");
                MessageBox.Show(Properties.Resources.SteamNotRunningInfoMessage, "kellerkompanie-sync");
                return;
            }

            StringBuilder sb = new StringBuilder();

            if (Settings.Instance.ParamDefaultWorldEmpty)
            {
                sb.Append(" -world=empty");
            }

            if (Settings.Instance.ParamNoLogs)
            {
                sb.Append(" -nologs");
            }

            if (Settings.Instance.ParamNoPause)
            {
                sb.Append(" -noPause");
            }

            if (Settings.Instance.ParamNoSplashScreen)
            {
                sb.Append(" -nosplash");
            }

            if (Settings.Instance.ParamShowScriptErrors)
            {
                sb.Append(" -showScriptErrors");
            }

            if (Settings.Instance.ParamWindowMode)
            {
                sb.Append(" -window");
            }

            foreach (AddonGroup addonGroup in FileIndexer.Instance.AddonGroups)
            {
                if (!addonGroup.CheckBoxIsChecked)
                {
                    continue;
                }

                foreach (RemoteAddon webAddon in addonGroup.RemoteAddons)
                {
                    Uuid uuid = webAddon.Uuid;
                    LocalAddon localAddon = FileIndexer.Instance.addonUuidToLocalAddonMap[uuid][0];
                    sb.Append(" -mod=");
                    sb.Append(localAddon.AbsoluteFilepath);
                    sb.Append(";");
                }
            }

            if (!string.IsNullOrEmpty(Settings.Instance.ParamAdditional))
            {
                sb.Append(" ");
                sb.Append(Settings.Instance.ParamAdditional);
            }

            string args = sb.ToString();
            Log.Information(string.Format("PlayUpdateButton_Click: starting arma from executable: {0}\nwith args:\n{1}", Settings.Instance.ExecutableLocation, args));
            PlayUpdateButton.IsEnabled = false;
            Process.Start(new ProcessStartInfo(Settings.Instance.ExecutableLocation, args) { CreateNoWindow = true });
            timer.Elapsed += Timer_Elapsed;
            timer.Start();
        }

        private void Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (!IsArmaRunning())
            {
                timer.Stop();
                Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    PlayUpdateButton.IsEnabled = true;
                }));
            }
        }

        private void RestoreWindowPositionAndSize()
        {
            Left = Settings.Instance.WindowX;
            Top = Settings.Instance.WindowY;
            Width = Settings.Instance.WindowWidth;
            Height = Settings.Instance.WindowHeight;
        }

        private void MainWindow_LocationChanged(object sender, EventArgs e)
        {
            Settings.Instance.WindowX = Left;
            Settings.Instance.WindowY = Top;
        }

        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Settings.Instance.WindowWidth = Width;
            Settings.Instance.WindowHeight = Height;
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            Settings.Instance.SaveSettings();
        }
    }
}

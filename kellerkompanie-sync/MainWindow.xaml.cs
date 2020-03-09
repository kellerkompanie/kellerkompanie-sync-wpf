﻿using kellerkompanie_sync;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace kellerkompanie_sync_wpf
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            ProgressBar.Value = 0;
            ProgressBarText.Text = "Everything up-to-date";

            NavigateToPage(Page.News);

            Settings.Instance.LoadSettings();
            RestoreWindowPositionAndSize();

            FileIndexer.Setup(ProgressBar, ProgressBarText);
            FileIndexer.Instance.UpdateLocalIndex();

            if (File.Exists(Settings.Instance.ExecutableLocation))
            {
                PlayUpdateButton.IsEnabled = true;
                PlayUpdateButton.ToolTip = null;
            }
            else
            {
                PlayUpdateButton.IsEnabled = false;
                PlayUpdateButton.ToolTip = "Executable not found, check settings";
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

        private bool IsTeamspeakRunning()
        {
            Process[] pname = Process.GetProcessesByName("ts3client_win64");
            if (pname.Length == 0)
                return false;
            else
                return true;
        }

        private void buttonTFAR_Click(object sender, RoutedEventArgs e)
        {
            if (IsTeamspeakRunning())
            {
                MessageBox.Show("Teamspeak is running, please close Teamspeak before installing the TFAR plugin!", "kellerkompanie-sync");
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
        
        private readonly Brush orange = (SolidColorBrush) (new BrushConverter().ConvertFrom("#ee4d2e"));
        private readonly Brush white = (SolidColorBrush) (new BrushConverter().ConvertFrom("#f2f5f4"));

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

        private bool IsSteamRunning()
        {
            Process[] pname = Process.GetProcessesByName("Steam");
            if (pname.Length == 0)
                return false;
            else
                return true;
        }

        private void PlayUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            if (!IsSteamRunning())
            {
                MessageBox.Show("Steam is not running, please start Steam first!", "kellerkompanie-sync");
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
                if (!addonGroup.CheckBoxIsSelected)
                    continue;

                WebAddonGroup webAddonGroup = WebAPI.GetAddonGroup(addonGroup.WebAddonGroupBase);
                foreach (WebAddon webAddon in webAddonGroup.Addons)
                {                    
                    string uuid = webAddon.Uuid;
                    LocalAddon localAddon = FileIndexer.Instance.addonUuidToLocalAddonMap[uuid][0];
                    sb.Append(" -mod=");
                    sb.Append(localAddon.AbsoluteFilepath);
                    sb.Append(";");
                }
            }

            if (!String.IsNullOrEmpty(Settings.Instance.ParamAdditional))
            {
                sb.Append(" ");
                sb.Append(Settings.Instance.ParamAdditional);
            }

            string args = sb.ToString();            
            Process.Start(new ProcessStartInfo(Settings.Instance.ExecutableLocation, args) { CreateNoWindow = true });
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

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace kellerkompanie_sync_wpf
{
    public class AddonSearchDirectory
    {
        public AddonSearchDirectory(string Directory)
        {
            this.Directory = Directory;
        }

        public string Directory { get; set; }

        public override bool Equals(Object obj)
        {
            //Check for null and compare run-time types.
            if ((obj == null) || !this.GetType().Equals(obj.GetType()))
            {
                return false;
            }
            else
            {
                AddonSearchDirectory other = (AddonSearchDirectory)obj;
                return (Directory.Equals(other.Directory));
            }
        }

        public override int GetHashCode()
        {
            return this.Directory.GetHashCode();
        }
    }

    public partial class SettingsPage : Page
    {
        public ObservableCollection<AddonSearchDirectory> AddonSearchDirectories { get; } = new ObservableCollection<AddonSearchDirectory>();

        public SettingsPage()
        {
            InitializeComponent();
            Settings settings = Settings.Instance;
            CheckBoxDefaultWorldEmpty.IsChecked = settings.ParamDefaultWorldEmpty;
            CheckBoxNoLogs.IsChecked = settings.ParamNoLogs;
            CheckBoxNoPause.IsChecked = settings.ParamNoPause;
            CheckBoxNoSplashScreen.IsChecked = settings.ParamNoSplashScreen;
            CheckBoxShowScriptErrors.IsChecked = settings.ParamShowScriptErrors;
            CheckBoxWindowMode.IsChecked = settings.ParamWindowMode;

            TextBoxExecutableLocation.Text = settings.ExecutableLocation;
            TextBoxAdditionalParameters.Text = settings.ParamAdditional;

            ListViewAddonSearchDirectories.ItemsSource = AddonSearchDirectories;
            foreach (string addonSearchDirectory in settings.AddonSearchDirectories)
            {
                AddonSearchDirectories.Add(new AddonSearchDirectory(addonSearchDirectory));
            }
        }

        private void ButtonAddAddonSearchDirectory_Click(object sender, RoutedEventArgs e)
        {
            var fileDialog = new System.Windows.Forms.FolderBrowserDialog();

            if (fileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var folder = fileDialog.SelectedPath;
                AddonSearchDirectory addonSearchDirectory = new AddonSearchDirectory(folder);
                if (!AddonSearchDirectories.Contains(addonSearchDirectory))
                {
                    AddonSearchDirectories.Add(addonSearchDirectory);

                    Settings.Instance.AddAddonSearchDirectory(addonSearchDirectory.Directory);
                    Settings.Instance.SaveSettings();

                    FileIndexer.Instance.UpdateLocalIndex();
                }
            }
        }

        private void SettingsExecutableLocationPicker_Click(object sender, RoutedEventArgs e)
        {
            var fileDialog = new System.Windows.Forms.OpenFileDialog();
            fileDialog.Filter = "Arma3 executable (*arma3_x64.exe)|*arma3_x64.exe";
            fileDialog.InitialDirectory = "C:\\Program Files(x86)\\Steam\\steamapps\\common\\Arma 3";
            fileDialog.RestoreDirectory = true;

            if (fileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var file = fileDialog.FileName;
                Settings.Instance.ExecutableLocation = file;
                Settings.Instance.SaveSettings();
                TextBoxExecutableLocation.Text = file;

                MainWindow wnd = (MainWindow)Window.GetWindow(this);
                wnd.PlayUpdateButton.IsEnabled = true;
                wnd.PlayUpdateButton.ToolTip = null;                
            }
        }

        private void CheckBoxShowScriptErrors_Click(object sender, RoutedEventArgs e)
        {
            bool isChecked = CheckBoxShowScriptErrors.IsChecked == true;
            Settings.Instance.ParamShowScriptErrors = isChecked;
            Settings.Instance.SaveSettings();
        }

        private void CheckBoxNoPause_Click(object sender, RoutedEventArgs e)
        {
            bool isChecked = CheckBoxNoPause.IsChecked == true;
            Settings.Instance.ParamNoPause = isChecked;
            Settings.Instance.SaveSettings();
        }

        private void CheckBoxWindowMode_Click(object sender, RoutedEventArgs e)
        {
            bool isChecked = CheckBoxWindowMode.IsChecked == true;
            Settings.Instance.ParamWindowMode = isChecked;
            Settings.Instance.SaveSettings();
        }

        private void CheckBoxNoSplashScreen_Click(object sender, RoutedEventArgs e)
        {
            bool isChecked = CheckBoxNoSplashScreen.IsChecked == true;
            Settings.Instance.ParamNoSplashScreen = isChecked;
            Settings.Instance.SaveSettings();
        }

        private void CheckBoxDefaultWorldEmpty_Click(object sender, RoutedEventArgs e)
        {
            bool isChecked = CheckBoxDefaultWorldEmpty.IsChecked == true;
            Settings.Instance.ParamDefaultWorldEmpty = isChecked;
            Settings.Instance.SaveSettings();
        }

        private void CheckBoxNoLogs_Click(object sender, RoutedEventArgs e)
        {
            bool isChecked = CheckBoxNoLogs.IsChecked == true;
            Settings.Instance.ParamNoLogs = isChecked;
            Settings.Instance.SaveSettings();
        }

        private void TextBoxAdditionalParameters_TextChanged(object sender, TextChangedEventArgs e)
        {
            Settings.Instance.ParamAdditional = TextBoxAdditionalParameters.Text;
            Settings.Instance.SaveSettings();
        }

        private void ButtonOpen_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var addonSearchDirectory = ((AddonSearchDirectory)button.DataContext).Directory;
            MainWindow.LaunchUri(addonSearchDirectory);
        }

        private void ButtonRemove_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var addonSearchDirectory = (AddonSearchDirectory)button.DataContext;

            AddonSearchDirectories.Remove(addonSearchDirectory);

            string directory = addonSearchDirectory.Directory;
            Settings.Instance.AddonSearchDirectories.Remove(directory);
            Settings.Instance.SaveSettings();
        }       
    }
}

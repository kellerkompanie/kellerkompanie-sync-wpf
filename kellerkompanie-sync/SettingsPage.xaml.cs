using System;
using System.Windows;
using System.Windows.Controls;

namespace kellerkompanie_sync
{
    public partial class SettingsPage : Page
    {
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

            SliderDownloads.Value = settings.SimultaneousDownloads;

            ListViewAddonSearchDirectories.ItemsSource = Settings.Instance.GetAddonSearchDirectories();
        }

        private void ButtonAddAddonSearchDirectory_Click(object sender, RoutedEventArgs e)
        {
            var fileDialog = new System.Windows.Forms.FolderBrowserDialog();

            if (fileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                FilePath folder = new FilePath(fileDialog.SelectedPath);
                if (!Settings.Instance.GetAddonSearchDirectories().Contains(folder))
                {
                    Settings.Instance.AddAddonSearchDirectory(folder);
                    Settings.SaveSettings();

                    FileIndexer.Instance.UpdateLocalIndex();
                }
            }
        }

        private void SettingsExecutableLocationPicker_Click(object sender, RoutedEventArgs e)
        {
            var fileDialog = new System.Windows.Forms.OpenFileDialog
            {
                Filter = "Arma3 executable (*arma3_x64.exe)|*arma3_x64.exe",
                InitialDirectory = "C:\\Program Files(x86)\\Steam\\steamapps\\common\\Arma 3",
                RestoreDirectory = true
            };

            if (fileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var file = fileDialog.FileName;
                Settings.Instance.ExecutableLocation = file;
                Settings.SaveSettings();
                TextBoxExecutableLocation.Text = file;

                MainWindow.Instance.EnablePlayButton();          
            }
        }

        private void CheckBoxShowScriptErrors_Click(object sender, RoutedEventArgs e)
        {
            bool isChecked = CheckBoxShowScriptErrors.IsChecked == true;
            Settings.Instance.ParamShowScriptErrors = isChecked;
            Settings.SaveSettings();
        }

        private void CheckBoxNoPause_Click(object sender, RoutedEventArgs e)
        {
            bool isChecked = CheckBoxNoPause.IsChecked == true;
            Settings.Instance.ParamNoPause = isChecked;
            Settings.SaveSettings();
        }

        private void CheckBoxWindowMode_Click(object sender, RoutedEventArgs e)
        {
            bool isChecked = CheckBoxWindowMode.IsChecked == true;
            Settings.Instance.ParamWindowMode = isChecked;
            Settings.SaveSettings();
        }

        private void CheckBoxNoSplashScreen_Click(object sender, RoutedEventArgs e)
        {
            bool isChecked = CheckBoxNoSplashScreen.IsChecked == true;
            Settings.Instance.ParamNoSplashScreen = isChecked;
            Settings.SaveSettings();
        }

        private void CheckBoxDefaultWorldEmpty_Click(object sender, RoutedEventArgs e)
        {
            bool isChecked = CheckBoxDefaultWorldEmpty.IsChecked == true;
            Settings.Instance.ParamDefaultWorldEmpty = isChecked;
            Settings.SaveSettings();
        }

        private void CheckBoxNoLogs_Click(object sender, RoutedEventArgs e)
        {
            bool isChecked = CheckBoxNoLogs.IsChecked == true;
            Settings.Instance.ParamNoLogs = isChecked;
            Settings.SaveSettings();
        }

        private void TextBoxAdditionalParameters_TextChanged(object sender, TextChangedEventArgs e)
        {
            Settings.Instance.ParamAdditional = TextBoxAdditionalParameters.Text;
            Settings.SaveSettings();
        }

        private void ButtonOpen_Click(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            string addonSearchDirectory = button.DataContext.ToString();
            MainWindow.LaunchUri(addonSearchDirectory);
        }

        private void ButtonRemove_Click(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            FilePath addonSearchDirectory = (FilePath)button.DataContext;
            Settings.Instance.GetAddonSearchDirectories().Remove(addonSearchDirectory);
            Settings.SaveSettings();
        }

        private void SliderDownloads_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Settings.Instance.SimultaneousDownloads = (int)Math.Floor(e.NewValue);
            Settings.SaveSettings();
        }
    }
}

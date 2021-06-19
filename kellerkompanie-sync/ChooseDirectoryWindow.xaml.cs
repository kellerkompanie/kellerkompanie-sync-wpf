using System.Windows;

namespace kellerkompanie_sync
{
    public partial class ChooseDirectoryWindow : Window
    {
        public ChooseDirectoryWindow()
        {
            InitializeComponent();

            ComboBox.ItemsSource = Settings.Instance.GetAddonSearchDirectories();
            ComboBox.SelectedIndex = 0;
        }

        private void ButtonOk_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        public FilePath ChosenDirectory => (FilePath)ComboBox.DataContext;
    }
}

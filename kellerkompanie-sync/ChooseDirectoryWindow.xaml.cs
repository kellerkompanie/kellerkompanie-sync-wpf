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

<<<<<<< HEAD
		public FilePath ChosenDirectory
		{
			get { return (FilePath)ComboBox.SelectedItem; }
		}
	}
=======
        public FilePath ChosenDirectory => (FilePath)ComboBox.DataContext;
    }
>>>>>>> 99367182fb9322683b0659f541210d9730e4f7ef
}

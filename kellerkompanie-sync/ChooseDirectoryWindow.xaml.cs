using System.Collections.ObjectModel;
using System.Windows;

namespace kellerkompanie_sync
{
    public partial class ChooseDirectoryWindow : Window
    {
		private ObservableCollection<string> list = new ObservableCollection<string>();

		public ChooseDirectoryWindow()
        {
            InitializeComponent();

			foreach (string addonSearchDirectory in Settings.Instance.GetAddonSearchDirectories())
			{
				list.Add(addonSearchDirectory);
			}
			ComboBox.ItemsSource = list;
			ComboBox.SelectedIndex = 0;
        }

		private void ButtonOk_Click(object sender, RoutedEventArgs e)
		{
			DialogResult = true;
		}

		public string ChosenDirectory
		{
			get { return ComboBox.Text; }
		}
	}
}

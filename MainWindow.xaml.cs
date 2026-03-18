using System.Windows;

namespace MidiAutoPlayer
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Button_Settings_Click(object sender, RoutedEventArgs e)
        {
            if (MainView.Visibility == Visibility.Visible)
            {
                MainView.Visibility = Visibility.Collapsed;
                SettingsView.Visibility = Visibility.Visible;
                Button_Settings.Content = "返回";
            }
            else
            {
                if (MainView.ViewModel != null)
                {
                    MainView.ViewModel.LoadSettings();
                    MainView.ViewModel.RefreshMidiFileList();
                    MainView.ViewModel.ApplyStoredNoteLevel();
                }
                MainView.Visibility = Visibility.Visible;
                SettingsView.Visibility = Visibility.Collapsed;
                Button_Settings.Content = "设置";
            }
        }
    }
}

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MidiAutoPlayer.Core.Midi;
using MidiAutoPlayer.ViewModel;

namespace MidiAutoPlayer.View
{
    public partial class MidiView : UserControl
    {
        public MidiView()
        {
            InitializeComponent();
        }

        public MidiViewModel ViewModel { get; set; }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ViewModel == null)
                {
                    ViewModel = new MidiViewModel();
                    DataContext = ViewModel;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ListBox_MidiFileInfo_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var item = ListBox_MidiFileInfo.SelectedItem as MidiFileInfo;
            ViewModel?.ChangePlayFile(item);
        }

        private void Button_Back_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.PlayLast();
        }

        private void Button_Play_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel?.IsPlaying == true)
            {
                ViewModel.IsPlaying = false;
            }
            else
            {
                ViewModel.IsPlaying = true;
            }
        }

        private void Button_Forward_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.PlayNext();
        }

        private void Button_DecreaseNoteLevel_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.DecreaseNoteLevel();
        }

        private void Button_IncreaseNoteLevel_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.IncreaseNoteLevel();
        }

        private void Button_AutoAdjust_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.AutoAdjustNoteLevel();
        }

        private void ToggleButton_CheckTrack_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.ChangeMidiTrack();
        }

        private void Button_RefreshWindow_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.RestartOrRefresh();
        }
    }
}

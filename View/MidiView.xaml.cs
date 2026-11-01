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

            // 解密按钮仅在非公开构建（本地）时挂接事件并显示
#if !GITHUB_BUILD
            if (Button_DecryptDms != null)
            {
                Button_DecryptDms.Click += Button_DecryptDms_Click;
            }
#endif
        }

        public MidiViewModel? ViewModel { get; set; }

        // IsDmsMode：true 时只显示 DMS/TXT 列表，false 时只显示 MIDI 列表
        public static readonly DependencyProperty IsDmsModeProperty =
            DependencyProperty.Register(
                nameof(IsDmsMode),
                typeof(bool),
                typeof(MidiView),
                new PropertyMetadata(false, OnIsDmsModeChanged));

        public bool IsDmsMode
        {
            get { return (bool)GetValue(IsDmsModeProperty); }
            set { SetValue(IsDmsModeProperty, value); }
        }

        private static void OnIsDmsModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var view = (MidiView)d;
            view.ApplyMode();
        }

        private void ApplyMode()
        {
            if (Tab_Midi == null || Tab_Dms == null || Tab_All == null)
            {
                return;
            }

            if (IsDmsMode)
            {
                // DMS 模式：只显示 DoMiSo 标签，并选中它
                Tab_All.Visibility = Visibility.Collapsed;
                Tab_Midi.Visibility = Visibility.Collapsed;
                Tab_Dms.Visibility = Visibility.Visible;
                TabControl_Files.SelectedItem = Tab_Dms;
                // 解密DMS按钮仅在DMS播放器页显示
#if !GITHUB_BUILD
                if (Button_DecryptDms != null)
                {
                    Button_DecryptDms.Visibility = Visibility.Visible;
                }
#endif
            }
            else
            {
                // MIDI 模式：只显示 MIDI 标签，并选中它
                Tab_All.Visibility = Visibility.Collapsed;
                Tab_Dms.Visibility = Visibility.Collapsed;
                Tab_Midi.Visibility = Visibility.Visible;
                TabControl_Files.SelectedItem = Tab_Midi;
                // 解密DMS按钮在MIDI播放器页隐藏
#if !GITHUB_BUILD
                if (Button_DecryptDms != null)
                {
                    Button_DecryptDms.Visibility = Visibility.Collapsed;
                }
#endif
            }
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ViewModel == null)
                {
                    // 若外部（主窗口）已通过 DataContext 注入共享 ViewModel，则直接复用
                    if (DataContext is MidiViewModel shared)
                    {
                        ViewModel = shared;
                    }
                    else
                    {
                        ViewModel = new MidiViewModel();
                        DataContext = ViewModel;
                    }
                }
                ApplyMode();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ListBox_MidiFileInfo_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var listBox = sender as ListBox;
            var item = listBox?.SelectedItem as IScoreInfo;
            if (item != null)
            {
                ViewModel?.ChangePlayFile(item);
            }
        }

        private void Button_Back_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.PlayLast();
        }

        private void Button_Play_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null)
            {
                return;
            }
            ViewModel.IsPlaying = !ViewModel.IsPlaying;
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

        private void Button_Sort_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.SortByHitRate = !ViewModel.SortByHitRate;
            }
        }

        private void ToggleButton_CheckTrack_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.ChangeMidiTrack();
        }

        private void Button_RefreshWindow_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.RestartOrRefresh();
        }

        private void Slider_Progress_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (ViewModel != null && ViewModel._isDraggingProgress)
            {
                ViewModel.ProgressValue = e.NewValue;
            }
        }

        private void Slider_Progress_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            ViewModel?.OnProgressDragStarted();
        }

        private void Slider_Progress_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            ViewModel?.OnProgressDragCompleted();
        }

#if !GITHUB_BUILD
        private void Button_DecryptDms_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Open file dialog to select encrypted dms file
                var openDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "选择加密的DMS文件",
                    Filter = "DMS文件 (*.dms)|*.dms|所有文件 (*.*)|*.*",
                    DefaultExt = ".dms"
                };

                if (openDialog.ShowDialog() == true)
                {
                    string inputFile = openDialog.FileName;

                    // Use DoMiSoFileInfo to decrypt
                    var decryptedContent = DoMiSoFileInfo.DecryptDmsFilePublic(inputFile);

                    if (string.IsNullOrEmpty(decryptedContent))
                    {
                        MessageBox.Show("解密失败或文件不是加密的DMS文件", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // Open save dialog
                    var saveDialog = new Microsoft.Win32.SaveFileDialog
                    {
                        Title = "保存解密后的文件",
                        Filter = "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
                        DefaultExt = ".txt",
                        FileName = System.IO.Path.GetFileNameWithoutExtension(inputFile) + "_decrypted.txt"
                    };

                    if (saveDialog.ShowDialog() == true)
                    {
                        System.IO.File.WriteAllText(saveDialog.FileName, decryptedContent, System.Text.Encoding.UTF8);
                        MessageBox.Show($"解密成功！\n文件已保存到：\n{saveDialog.FileName}", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"解密失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
#endif
    }
}

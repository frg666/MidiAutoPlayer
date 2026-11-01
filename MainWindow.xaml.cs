using System.Windows;
using MidiAutoPlayer.ViewModel;
using Wpf.Ui.Controls;

namespace MidiAutoPlayer
{
    public partial class MainWindow : FluentWindow
    {
        private MidiViewModel _sharedViewModel;

        public MainWindow()
        {
            InitializeComponent();

            // WPF-UI 的 Acrylic 背景要求在窗口初始化前就设置好
            // ExtendsContentIntoTitleBar，否则会抛异常。这里显式兜底。
            ExtendsContentIntoTitleBar = true;
            WindowBackdropType = WindowBackdropType.Acrylic;

            // 创建共享 ViewModel，注入两个播放器视图与设置视图，
            // 保证 MIDI / DMS / 设置的播放状态与数据完全一致。
            _sharedViewModel = new MidiViewModel();
            MidiView.ViewModel = _sharedViewModel;
            MidiView.DataContext = _sharedViewModel;
            DmsView.ViewModel = _sharedViewModel;
            DmsView.DataContext = _sharedViewModel;
            SettingsView.DataContext = _sharedViewModel;

            // 响应窗口透明度设置
            _sharedViewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MidiViewModel.WindowOpacity))
                {
                    Opacity = _sharedViewModel.WindowOpacity;
                }
            };

            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 应用已保存的窗口透明度
            Opacity = _sharedViewModel.WindowOpacity;
        }

        private void NavItem_Click(object sender, RoutedEventArgs e)
        {
            var item = sender as NavigationViewItem;
            if (item == null)
            {
                return;
            }

            SwitchPage(item.TargetPageTag);
        }

        private void SwitchPage(string tag)
        {
            MidiView.Visibility = tag == "midi" ? Visibility.Visible : Visibility.Collapsed;
            DmsView.Visibility = tag == "dms" ? Visibility.Visible : Visibility.Collapsed;
            SettingsView.Visibility = tag == "settings" ? Visibility.Visible : Visibility.Collapsed;

            // 更新导航项高亮
            foreach (var menuItem in NavView.MenuItems)
            {
                if (menuItem is NavigationViewItem navItem)
                {
                    navItem.IsActive = navItem.TargetPageTag == tag;
                }
            }
        }

        private void TitleBar_MinimizeClicked(TitleBar sender, RoutedEventArgs args)
        {
            WindowState = WindowState.Minimized;
        }

        private void TitleBar_MaximizeClicked(TitleBar sender, RoutedEventArgs args)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void TitleBar_CloseClicked(TitleBar sender, RoutedEventArgs args)
        {
            Close();
        }
    }
}

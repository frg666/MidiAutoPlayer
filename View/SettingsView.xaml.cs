using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using MidiAutoPlayer.Core.Midi;
using MidiAutoPlayer.ViewModel;

namespace MidiAutoPlayer.View
{
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
            Loaded += SettingsView_Loaded;
        }

        // 操作完成后异步刷新主界面文件列表（在 UI 线程执行，避免跨线程访问集合）
        private void RefreshMainListAsync()
        {
            if (DataContext is MidiViewModel vm)
            {
                // 延迟到 UI 空闲时执行，避免阻塞当前设置页响应
                Dispatcher.BeginInvoke(
                    new Action(() => vm.RefreshMidiFileList()),
                    System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        private void SettingsView_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateFileCount();
        }

        private void UpdateFileCount()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var midiDir = System.IO.Path.Combine(baseDir, "Resource", "Midi");
            if (System.IO.Directory.Exists(midiDir))
            {
                var count = System.IO.Directory.GetFiles(midiDir, "*.mid").Length + System.IO.Directory.GetFiles(midiDir, "*.midi").Length + System.IO.Directory.GetFiles(midiDir, "*.txt").Length;
                TextBlock_FileCount.Text = $"共有 {count} 个乐谱文件";
            }
        }

        private async void Button_AutoAdjustAll_Click(object sender, RoutedEventArgs e)
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var midiDir = System.IO.Path.Combine(baseDir, "Resource", "Midi");
            
            if (!System.IO.Directory.Exists(midiDir))
            {
                MessageBox.Show("乐谱文件夹不存在", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var files = System.IO.Directory.GetFiles(midiDir, "*.mid").ToList();
            files.AddRange(System.IO.Directory.GetFiles(midiDir, "*.midi"));
            files.AddRange(System.IO.Directory.GetFiles(midiDir, "*.txt"));
            
            if (files.Count == 0)
            {
                MessageBox.Show("没有找到乐谱文件", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Button_AutoAdjustAll.IsEnabled = false;
            TextBlock_Progress.Text = "正在处理...";

            var settings = new System.Collections.Generic.Dictionary<string, int>();
            var processed = 0;
            var total = files.Count;

            await Task.Run(() =>
            {
                foreach (var file in files)
                {
                    try
                    {
                        var ext = System.IO.Path.GetExtension(file).ToLower();
                        IScoreInfo scoreFile;
                        if (ext == ".txt")
                        {
                            scoreFile = new DoMiSoFileInfo(file);
                        }
                        else
                        {
                            scoreFile = new MidiFileInfo(file);
                        }
                        scoreFile.CalculateBestNoteLevel();
                        settings[scoreFile.Name] = scoreFile.BestNoteLevel;
                    }
                    catch { }
                    
                    processed++;
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        TextBlock_Progress.Text = $"已处理 {processed}/{total}";
                    });
                }
            });

            var settingPath = System.IO.Path.Combine(baseDir, "settings.txt");
            var lines = settings.Select(kv => $"{kv.Key}={kv.Value}");
            System.IO.File.WriteAllLines(settingPath, lines);

            Button_AutoAdjustAll.IsEnabled = true;
            TextBlock_Progress.Text = "完成！";
            MessageBox.Show($"已完成对 {total} 个文件的自动调整", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
            UpdateFileCount();
            RefreshMainListAsync();
        }

        private void Button_DeleteBelowThreshold_Click(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(TextBox_DeleteThreshold.Value.ToString(), out double threshold))
            {
                if (threshold < 0 || threshold > 100)
                {
                    MessageBox.Show("请输入0-100之间的数值", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var midiDir = System.IO.Path.Combine(baseDir, "Resource", "Midi");
                var settingPath = System.IO.Path.Combine(baseDir, "settings.txt");

                if (!System.IO.Directory.Exists(midiDir))
                {
                    return;
                }

                if (!System.IO.File.Exists(settingPath))
                {
                    MessageBox.Show("请先运行一键自动调整功能", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var filesToDelete = new System.Collections.Generic.List<string>();
                var lines = System.IO.File.ReadAllLines(settingPath);
                
                foreach (var line in lines)
                {
                    var parts = line.Split('=');
                    if (parts.Length == 2)
                    {
                        var fileName = parts[0];
                        if (int.TryParse(parts[1], out int level))
                        {
                            var fullPath = System.IO.Path.Combine(midiDir, fileName + ".mid");
                            if (!System.IO.File.Exists(fullPath))
                            {
                                fullPath = System.IO.Path.Combine(midiDir, fileName + ".midi");
                            }
                            if (!System.IO.File.Exists(fullPath))
                            {
                                fullPath = System.IO.Path.Combine(midiDir, fileName + ".txt");
                            }
                            
                            if (System.IO.File.Exists(fullPath))
                            {
                                try
                                {
                                    var ext = System.IO.Path.GetExtension(fullPath).ToLower();
                                    IScoreInfo scoreFile;
                                    if (ext == ".txt")
                                    {
                                        scoreFile = new DoMiSoFileInfo(fullPath);
                                    }
                                    else
                                    {
                                        scoreFile = new MidiFileInfo(fullPath);
                                    }
                                    scoreFile.RefreshTracksByNoteLevel(level);
                                    var radio = scoreFile.CanPlayNoteRadio;
                                    if (radio < threshold / 100.0)
                                    {
                                        filesToDelete.Add(fullPath);
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                }

                if (filesToDelete.Count == 0)
                {
                    MessageBox.Show("没有符合条件的文件需要删除", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var result = MessageBox.Show($"确定要删除自动调整后命中率低于 {threshold}% 的 {filesToDelete.Count} 个文件吗？", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    foreach (var file in filesToDelete)
                    {
                        try
                        {
                            System.IO.File.Delete(file);
                        }
                        catch { }
                    }
                    MessageBox.Show($"已删除 {filesToDelete.Count} 个文件", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
                    UpdateFileCount();
                    RefreshMainListAsync();
                }
            }
            else
            {
                MessageBox.Show("请输入有效的数值", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}

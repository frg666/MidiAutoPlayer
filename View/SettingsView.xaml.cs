using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using MidiAutoPlayer.Core.Midi;

namespace MidiAutoPlayer.View
{
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
            Loaded += SettingsView_Loaded;
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
                var count = System.IO.Directory.GetFiles(midiDir, "*.mid").Length + System.IO.Directory.GetFiles(midiDir, "*.midi").Length;
                TextBlock_FileCount.Text = $"共有 {count} 个MIDI文件";
            }
        }

        private async void Button_AutoAdjustAll_Click(object sender, RoutedEventArgs e)
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var midiDir = System.IO.Path.Combine(baseDir, "Resource", "Midi");
            
            if (!System.IO.Directory.Exists(midiDir))
            {
                MessageBox.Show("MIDI文件夹不存在", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var files = System.IO.Directory.GetFiles(midiDir, "*.mid").ToList();
            files.AddRange(System.IO.Directory.GetFiles(midiDir, "*.midi"));
            
            if (files.Count == 0)
            {
                MessageBox.Show("没有找到MIDI文件", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
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
                        var midiFile = new MidiFileInfo(file);
                        midiFile.CalculateBestNoteLevel();
                        settings[midiFile.Name] = midiFile.BestNoteLevel;
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
        }

        private void Button_DeleteBelowThreshold_Click(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(TextBox_DeleteThreshold.Text, out double threshold))
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
                            
                            if (System.IO.File.Exists(fullPath))
                            {
                                try
                                {
                                    var midiFile = new MidiFileInfo(fullPath);
                                    midiFile.RefreshTracksByNoteLevel(level);
                                    var radio = midiFile.CanPlayNoteRadio;
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
                }
            }
            else
            {
                MessageBox.Show("请输入有效的数值", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Interop;
using MidiAutoPlayer.Core.Midi;
using MidiAutoPlayer.Core.MusicGame;

namespace MidiAutoPlayer.ViewModel
{
    public class MidiViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }


        private ObservableCollection<MidiFileInfo> _MidiFileInfoList;
        public ObservableCollection<MidiFileInfo> MidiFileInfoList
        {
            get { return _MidiFileInfoList; }
            set
            {
                _MidiFileInfoList = value;
                OnPropertyChanged();
            }
        }


        private MidiFileInfo _SelectedMidiFile;
        public MidiFileInfo SelectedMidiFile
        {
            get { return _SelectedMidiFile; }
            set
            {
                _SelectedMidiFile = value;
                if (value != null && MidiPlayer != null)
                {
                    var key = $"{value.Name}_{(int)InstrumentType}";
                    if (_noteLevelSettings.TryGetValue(key, out int savedLevel))
                    {
                        value.RefreshTracksByNoteLevel(savedLevel, InstrumentType);
                    }
                    else
                    {
                        value.RefreshTracksByNoteLevel(0, InstrumentType);
                    }
                    OnPropertyChanged("NoteLevel");
                }
                OnPropertyChanged();
            }
        }


        private bool _IsAdmin;
        public bool IsAdmin
        {
            get { return _IsAdmin; }
            set
            {
                _IsAdmin = value;
                OnPropertyChanged();
            }
        }


        private bool _CanPlay;
        public bool CanPlay
        {
            get { return _CanPlay; }
            set
            {
                _CanPlay = value;
                OnPropertyChanged();
            }
        }

        private string _StateText;
        public string StateText
        {
            get { return _StateText; }
            set
            {
                _StateText = value;
                OnPropertyChanged();
            }
        }

        private string _Button_Restart_Content;
        public string Button_Restart_Content
        {
            get { return _Button_Restart_Content; }
            set
            {
                _Button_Restart_Content = value;
                OnPropertyChanged();
            }
        }

        private string _TextBlock_Color;
        public string TextBlock_Color
        {
            get { return _TextBlock_Color; }
            set
            {
                _TextBlock_Color = value;
                OnPropertyChanged();
            }
        }

        private InstrumentType _InstrumentType = InstrumentType.Piano;
        public InstrumentType InstrumentType
        {
            get { return _InstrumentType; }
            set
            {
                _InstrumentType = value;
                OnPropertyChanged();
                if (MidiPlayer != null)
                {
                    MidiPlayer.InstrumentType = value;
                }
                RefreshCurrentFileNoteLevel();
                SaveSettings();
            }
        }

        public string Name => MidiPlayer?.Name;


        public bool IsPlaying
        {
            get { return MidiPlayer?.IsPlaying ?? false; }
            set
            {
                if (MidiPlayer != null)
                {
                    MidiPlayer.IsPlaying = value;
                    OnPropertyChanged();
                }
            }
        }


        public bool AutoSwitchToGenshinWindow
        {
            get { return MidiPlayer?.AutoSwitchToGenshinWindow ?? true; }
            set
            {
                if (MidiPlayer != null)
                {
                    MidiPlayer.AutoSwitchToGenshinWindow = value;
                    OnPropertyChanged();
                }
            }
        }


        public bool PlayBackground
        {
            get { return MidiPlayer?.PlayBackground ?? false; }
            set
            {
                if (MidiPlayer != null)
                {
                    MidiPlayer.PlayBackground = value;
                    OnPropertyChanged();
                }
            }
        }

        public double Speed
        {
            get { return MidiPlayer?.Speed ?? 1.0; }
            set
            {
                if (MidiPlayer != null)
                {
                    MidiPlayer.Speed = value;
                    if (timer != null)
                    {
                        timer.Interval = 1000 / value;
                    }
                    OnPropertyChanged();
                }
            }
        }

        public int NoteLevel
        {
            get { return MidiPlayer?.NoteLevel ?? 0; }
            set
            {
                if (MidiPlayer != null)
                {
                    MidiPlayer.NoteLevel = value;
                    RefreshMidiFileInfoByNoteLevel(value);
                    OnPropertyChanged();
                    OnPropertyChanged("CanAutoAdjust");
                }
            }
        }

        public void RefreshCurrentFileNoteLevel()
        {
            if (SelectedMidiFile != null)
            {
                var key = $"{SelectedMidiFile.Name}_{(int)InstrumentType}";
                if (_noteLevelSettings.TryGetValue(key, out int savedLevel))
                {
                    NoteLevel = savedLevel;
                }
                else
                {
                    NoteLevel = 0;
                }
            }
        }

        public bool CanAutoAdjust { get; private set; }

        private Dictionary<string, int> _noteLevelSettings = new Dictionary<string, int>();

        public async void AutoAdjustNoteLevel()
        {
            if (SelectedMidiFile == null || MidiPlayer == null) return;

            var originalLevel = MidiPlayer.NoteLevel;
            var bestLevel = originalLevel;
            var bestRadio = 0.0;

            await Task.Run(() =>
            {
                for (int level = -24; level <= 24; level++)
                {
                    SelectedMidiFile.RefreshTracksByNoteLevel(level, InstrumentType);
                    var radio = SelectedMidiFile.CanPlayNoteRadio;
                    if (radio > bestRadio)
                    {
                        bestRadio = radio;
                        bestLevel = level;
                    }
                }
            });

            SelectedMidiFile.RefreshTracksByNoteLevel(originalLevel, InstrumentType);
            
            MidiPlayer.NoteLevel = bestLevel;
            var key = $"{SelectedMidiFile.Name}_{(int)InstrumentType}";
            _noteLevelSettings[key] = bestLevel;
            SaveSettings();
            
            RefreshMidiFileInfoByNoteLevel(MidiPlayer.NoteLevel);
            OnPropertyChanged("NoteLevel");
            OnPropertyChanged("CanAutoAdjust");
        }

        public void LoadSettings()
        {
            try
            {
                var settingPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.txt");
                if (File.Exists(settingPath))
                {
                    var lines = File.ReadAllLines(settingPath);
                    foreach (var line in lines)
                    {
                        if (line.StartsWith("_InstrumentType="))
                        {
                            if (int.TryParse(line.Split('=')[1], out int type) && Enum.IsDefined(typeof(InstrumentType), type))
                            {
                                _InstrumentType = (InstrumentType)type;
                            }
                        }
                        else
                        {
                            var parts = line.Split('=');
                            if (parts.Length == 2 && int.TryParse(parts[1], out int level))
                            {
                                _noteLevelSettings[parts[0]] = level;
                            }
                        }
                    }
                }
            }
            catch { }
        }

        public void SaveSettings()
        {
            try
            {
                var settingPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.txt");
                var lines = _noteLevelSettings.Select(kv => $"{kv.Key}={kv.Value}").ToList();
                lines.Insert(0, $"_InstrumentType={(int)InstrumentType}");
                File.WriteAllLines(settingPath, lines);
            }
            catch { }
        }

        public void ApplyStoredNoteLevel()
        {
            if (SelectedMidiFile != null && MidiPlayer != null)
            {
                var key = $"{SelectedMidiFile.Name}_{(int)InstrumentType}";
                if (_noteLevelSettings.TryGetValue(key, out int savedLevel))
                {
                    MidiPlayer.NoteLevel = savedLevel;
                    RefreshMidiFileInfoByNoteLevel(savedLevel);
                }
            }
        }

        public void IncreaseNoteLevel()
        {
            NoteLevel = Math.Min(NoteLevel + 1, 24);
        }

        public void DecreaseNoteLevel()
        {
            NoteLevel = Math.Max(NoteLevel - 1, -24);
        }

        public void DeleteSelectedFiles(IList<MidiFileInfo> selectedFiles)
        {
            if (selectedFiles == null || selectedFiles.Count == 0) return;

            foreach (var file in selectedFiles)
            {
                try
                {
                    if (File.Exists(file.FilePath))
                    {
                        File.Delete(file.FilePath);
                    }
                    MidiFileInfoList.Remove(file);
                    if (_noteLevelSettings.ContainsKey(file.Name))
                    {
                        _noteLevelSettings.Remove(file.Name);
                    }
                }
                catch { }
            }
            SaveSettings();

            if (MidiFileInfoList.Count > 0 && SelectedMidiFile == null)
            {
                SelectedMidiFile = MidiFileInfoList.First();
            }
        }

        public void RefreshMidiFileList()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var midiDir = Path.Combine(baseDir, "Resource", "Midi");

            if (!Directory.Exists(midiDir))
            {
                Directory.CreateDirectory(midiDir);
            }

            var files = Directory.GetFiles(midiDir).ToList();
            if (files.Count > 0)
            {
                var infos = files.ConvertAll(x => new MidiFileInfo(x)).OrderBy(x => x.Name);
                MidiFileInfoList = new ObservableCollection<MidiFileInfo>(infos);
                SelectedMidiFile = MidiFileInfoList.First();
            }
            else
            {
                MidiFileInfoList = new ObservableCollection<MidiFileInfo>();
                SelectedMidiFile = null;
            }
            OnPropertyChanged("MidiFileInfoList");
        }

        public void DeleteFilesBelowThreshold(double threshold)
        {
            var filesToDelete = new List<MidiFileInfo>();
            foreach (var file in MidiFileInfoList)
            {
                if (file.BestNoteRadio < threshold / 100.0)
                {
                    filesToDelete.Add(file);
                }
            }

            if (filesToDelete.Count > 0)
            {
                DeleteSelectedFiles(filesToDelete);
            }
        }

        public TimeSpan TotalTime => MidiPlayer?.TotalTime ?? TimeSpan.Zero;


        public TimeSpan CurrentTime
        {
            get { return MidiPlayer?.CurrentTime ?? TimeSpan.Zero; }
            set
            {
                if (MidiPlayer != null)
                {
                    MidiPlayer.CurrentTime = value;
                    OnPropertyChanged();
                }
            }
        }



        private bool hotkey;
        private IntPtr hWnd;
        private HwndSource hwndSource;
        private static MidiPlayer MidiPlayer;
        private System.Timers.Timer timer;

        public MidiViewModel()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var midiDir = Path.Combine(baseDir, "Resource", "Midi");

            if (!Directory.Exists(midiDir))
            {
                Directory.CreateDirectory(midiDir);
            }

            LoadSettings();

            var files = Directory.GetFiles(midiDir).ToList();
            if (files.Count > 0)
            {
                var infos = new List<MidiFileInfo>();
                foreach (var file in files)
                {
                    try
                    {
                        var info = new MidiFileInfo(file);
                        infos.Add(info);
                    }
                    catch
                    {
                    }
                }
                if (infos.Count > 0)
                {
                    var orderedInfos = infos.OrderBy(x => x.Name).ToList();
                    MidiFileInfoList = new ObservableCollection<MidiFileInfo>(orderedInfos);
                    MidiPlayer = new MidiPlayer();
                    MidiPlayer.Started += MidiPlayer_Started;
                    MidiPlayer.Stopped += MidiPlayer_Stopped;
                    MidiPlayer.Finished += MidiPlayer_Finished;
                    SelectedMidiFile = MidiFileInfoList.First();
                    ChangePlayFile(SelectedMidiFile, false);
                    ApplyStoredNoteLevel();
                }
                else
                {
                    MidiFileInfoList = new ObservableCollection<MidiFileInfo>();
                    MidiPlayer = new MidiPlayer();
                }
            }
            else
            {
                MidiFileInfoList = new ObservableCollection<MidiFileInfo>();
                MidiPlayer = new MidiPlayer();
            }

            hWnd = Process.GetCurrentProcess().MainWindowHandle;
            hotkey = Util.RegisterHotKey(hWnd);
            hwndSource = HwndSource.FromHwnd(hWnd);
            hwndSource.AddHook(HwndHook);
            RefreshState();

            if (files.Count > 0)
            {
                timer = new System.Timers.Timer(1000);
                timer.AutoReset = true;
                timer.Elapsed += Timer_Elapsed;
            }
        }


        public void RefreshMidiFileInfoByNoteLevel(int noteLevel)
        {
            foreach (var item in MidiFileInfoList)
            {
                item.RefreshTracksByNoteLevel(noteLevel, InstrumentType);
            }
            var info = SelectedMidiFile;
            SelectedMidiFile = null;
            SelectedMidiFile = info;
        }


        private void MidiPlayer_Started(object sender, EventArgs e)
        {
            timer.Start();
            OnPropertyChanged("IsPlaying");
            OnPropertyChanged("CurrentTime");
        }
        private void MidiPlayer_Stopped(object sender, EventArgs e)
        {
            timer.Stop();
            OnPropertyChanged("IsPlaying");
            OnPropertyChanged("CurrentTime");
        }

        private void MidiPlayer_Finished(object sender, EventArgs e)
        {
            timer.Stop();
            OnPropertyChanged("IsPlaying");
            OnPropertyChanged("CurrentTime");
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            OnPropertyChanged("IsPlaying");
            OnPropertyChanged("CurrentTime");
        }


        public void ChangePlayFile(MidiFileInfo info, bool autoPlay = true)
        {
            if (MidiPlayer != null && info != null)
            {
                MidiPlayer.ChangeFileInfo(info, autoPlay);
                ApplyStoredNoteLevel();
                OnPropertyChanged("Name");
                OnPropertyChanged("IsPlaying");
                OnPropertyChanged("AutoSwitchToGenshinWindow");
                OnPropertyChanged("PlayBackground");
                OnPropertyChanged("Speed");
                OnPropertyChanged("NoteLevel");
                OnPropertyChanged("TotalTime");
                OnPropertyChanged("CurrentTime");
            }
        }

        public void ChangeMidiTrack()
        {
            if (timer != null)
            {
                timer.Stop();
            }
            if (MidiPlayer != null)
            {
                MidiPlayer.Started -= MidiPlayer_Started;
                MidiPlayer.Stopped -= MidiPlayer_Stopped;
                MidiPlayer.Finished -= MidiPlayer_Finished;
                MidiPlayer?.ChangeFileInfo();
                MidiPlayer.Started += MidiPlayer_Started;
                MidiPlayer.Stopped += MidiPlayer_Stopped;
                MidiPlayer.Finished += MidiPlayer_Finished;
            }
            if (timer != null)
            {
                timer.Start();
            }
        }


        public void RefreshState()
        {
            IsAdmin = Util.IsAdmin();
            CanPlay = MidiPlayer?.CanPlay ?? false;
            StateText = "正常";
            TextBlock_Color = "Black";
            Button_Restart_Content = null;
            if (!hotkey)
            {
                StateText = "热键定义失败";
                TextBlock_Color = "Red";
                Button_Restart_Content = "重试";
            }
            if (!CanPlay)
            {
                StateText = "没有找到原神的窗口";
                TextBlock_Color = "Red";
                Button_Restart_Content = "刷新";
            }
            if (!IsAdmin)
            {
                StateText = "需要管理员权限";
                TextBlock_Color = "Red";
                Button_Restart_Content = "重启";
            }
        }


        public void RestartOrRefresh()
        {
            if (!IsAdmin)
            {
                try
                {
                    Util.RestartAsAdmin();
                }
                catch (Exception)
                {
                    System.Windows.MessageBox.Show("无法重启，请手动以管理员权限启动");
                }
            }
            if (!CanPlay)
            {
                MidiPlayer.RefreshGameWindow();
                RefreshState();
            }
            if (!hotkey)
            {
                Util.UnregisterHotKey(hWnd);
                hotkey = Util.RegisterHotKey(hWnd);
                RefreshState();
            }
        }


        private IntPtr HwndHook(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg == WM_HOTKEY)
            {
                switch (wParam.ToInt32())
                {
                    case 1000:
                        if (IsPlaying)
                        {
                            IsPlaying = false;
                        }
                        else
                        {
                            if (MidiPlayer?.MidiFileInfo == null && MidiFileInfoList.Count > 0)
                            {
                                ChangePlayFile(MidiFileInfoList.First());
                            }
                            else
                            {
                                IsPlaying = true;
                            }
                        }
                        handled = true;
                        break;
                    case 1001:
                        PlayLast();
                        handled = true;
                        break;
                    case 1002:
                        PlayNext();
                        handled = true;
                        break;
                }
            }
            return IntPtr.Zero;
        }


        public void PlayLast()
        {
            if (MidiFileInfoList.Count == 0)
            {
                return;
            }
            if (MidiPlayer.MidiFileInfo == null)
            {
                ChangePlayFile(MidiFileInfoList.Last());
            }
            else
            {
                var index = MidiFileInfoList.IndexOf(MidiPlayer.MidiFileInfo);
                if (index == 0)
                {
                    ChangePlayFile(MidiFileInfoList.Last());
                }
                else
                {
                    ChangePlayFile(MidiFileInfoList[index - 1]);
                }
            }
        }


        public void PlayNext()
        {
            if (MidiFileInfoList.Count == 0)
            {
                return;
            }
            if (MidiPlayer.MidiFileInfo == null)
            {
                ChangePlayFile(MidiFileInfoList.First());
            }
            else
            {
                var index = MidiFileInfoList.IndexOf(MidiPlayer.MidiFileInfo);
                if (index == MidiFileInfoList.Count - 1)
                {
                    ChangePlayFile(MidiFileInfoList.First());
                }
                else
                {
                    ChangePlayFile(MidiFileInfoList[index + 1]);
                }
            }
        }

    }
}

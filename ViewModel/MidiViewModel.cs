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


        private ObservableCollection<IScoreInfo> _MidiFileInfoList = new ObservableCollection<IScoreInfo>();
        public ObservableCollection<IScoreInfo> MidiFileInfoList
        {
            get { return _MidiFileInfoList; }
            set
            {
                _MidiFileInfoList = value;
                OnPropertyChanged();
            }
        }

        private ObservableCollection<IScoreInfo> _DoMiSoFileList = new ObservableCollection<IScoreInfo>();
        public ObservableCollection<IScoreInfo> DoMiSoFileList
        {
            get { return _DoMiSoFileList; }
            set
            {
                _DoMiSoFileList = value;
                OnPropertyChanged();
            }
        }

        private ObservableCollection<IScoreInfo> _MidiOnlyFileList = new ObservableCollection<IScoreInfo>();
        public ObservableCollection<IScoreInfo> MidiOnlyFileList
        {
            get { return _MidiOnlyFileList; }
            set
            {
                _MidiOnlyFileList = value;
                OnPropertyChanged();
            }
        }


        // 排序与搜索：保存三个列表的完整原始数据源，排序/搜索时据此重新填充。
        private List<IScoreInfo> _allFilesSource = new List<IScoreInfo>();
        private List<IScoreInfo> _dmsFilesSource = new List<IScoreInfo>();
        private List<IScoreInfo> _midiFilesSource = new List<IScoreInfo>();

        private bool _SortByHitRate;
        public bool SortByHitRate
        {
            get { return _SortByHitRate; }
            set
            {
                _SortByHitRate = value;
                OnPropertyChanged();
                OnPropertyChanged("SortButtonContent");
                ApplySortAndFilter();
            }
        }

        public string SortButtonContent => _SortByHitRate ? "排序：命中率" : "排序：名称";

        private string _SearchText = "";
        public string SearchText
        {
            get { return _SearchText; }
            set
            {
                _SearchText = value ?? "";
                OnPropertyChanged();
                ApplySortAndFilter();
            }
        }

        // 当前乐器对应的命中率（排序/搜索都基于它）
        private double GetHitRate(IScoreInfo info)
        {
            return InstrumentType switch
            {
                InstrumentType.FrenchHorn => info.FrenchHornCanPlayNoteRadio,
                InstrumentType.Guitar => info.GuitarCanPlayNoteRadio,
                _ => info.CanPlayNoteRadio,
            };
        }

        private IEnumerable<IScoreInfo> SortAndFilter(IEnumerable<IScoreInfo> source)
        {
            var query = source.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(_SearchText))
            {
                query = query.Where(x => x.Name != null && x.Name.Contains(_SearchText, StringComparison.OrdinalIgnoreCase));
            }
            if (_SortByHitRate)
            {
                query = query.OrderByDescending(x => GetHitRate(x));
            }
            else
            {
                query = query.OrderBy(x => x.Name);
            }
            return query.ToList();
        }

        private void ApplySortAndFilter()
        {
            var prevSelected = _SelectedMidiFile;
            MidiFileInfoList = new ObservableCollection<IScoreInfo>(SortAndFilter(_allFilesSource));
            DoMiSoFileList = new ObservableCollection<IScoreInfo>(SortAndFilter(_dmsFilesSource));
            MidiOnlyFileList = new ObservableCollection<IScoreInfo>(SortAndFilter(_midiFilesSource));

            // 保持之前选中的文件（若仍在过滤结果中）
            if (prevSelected != null && MidiFileInfoList.Contains(prevSelected))
            {
                _SelectedMidiFile = prevSelected;
                OnPropertyChanged("SelectedMidiFile");
            }
            else if (MidiFileInfoList.Count > 0)
            {
                SelectedMidiFile = MidiFileInfoList.First();
            }
        }


        private IScoreInfo? _SelectedMidiFile;
        public IScoreInfo? SelectedMidiFile
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

        private string _StateText = string.Empty;
        public string StateText
        {
            get { return _StateText; }
            set
            {
                _StateText = value;
                OnPropertyChanged();
            }
        }

        private string _Button_Restart_Content = string.Empty;
        public string Button_Restart_Content
        {
            get { return _Button_Restart_Content; }
            set
            {
                _Button_Restart_Content = value;
                OnPropertyChanged();
            }
        }

        private string _TextBlock_Color = string.Empty;
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
                // 切换乐器后，若处于按命中率排序，需按新乐器的命中率重排
                if (_SortByHitRate)
                {
                    ApplySortAndFilter();
                }
            }
        }

        public string Name => MidiPlayer?.Name ?? string.Empty;


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
            get
            {
                // 返回「选中乐曲 + 当前乐器」的偏移量（独立于正在播放的乐曲）。
                if (SelectedMidiFile != null)
                {
                    var key = $"{SelectedMidiFile.Name}_{(int)InstrumentType}";
                    if (_noteLevelSettings.TryGetValue(key, out int savedLevel))
                    {
                        return savedLevel;
                    }
                }
                return 0;
            }
            set
            {
                if (SelectedMidiFile == null) return;

                // 只更新「选中乐曲 + 当前乐器」的偏移设置，不直接改动播放器。
                var key = $"{SelectedMidiFile.Name}_{(int)InstrumentType}";
                _noteLevelSettings[key] = value;
                SaveSettings();

                // 仅当选中乐曲就是正在播放的乐曲时，才同步到播放器（实时生效）。
                if (MidiPlayer != null && MidiPlayer.MidiFileInfo == SelectedMidiFile)
                {
                    MidiPlayer.NoteLevel = value;
                }

                RefreshMidiFileInfoByNoteLevel(value);
                OnPropertyChanged();
                OnPropertyChanged("CanAutoAdjust");
            }
        }

        public bool _isDraggingProgress;
        private double _progressValue;
        public double ProgressValue
        {
            get
            {
                if (_isDraggingProgress) return _progressValue;
                return MidiPlayer?.CurrentTime.TotalSeconds ?? 0;
            }
            set
            {
                _progressValue = value;
                if (_isDraggingProgress)
                {
                    OnPropertyChanged();
                }
            }
        }

        public double ProgressMaximum
        {
            get { return MidiPlayer?.TotalTime.TotalSeconds ?? 1; }
        }

        public void OnProgressDragStarted()
        {
            _isDraggingProgress = true;
        }

        public void OnProgressDragCompleted()
        {
            if (MidiPlayer != null)
            {
                MidiPlayer.CurrentTime = TimeSpan.FromSeconds(_progressValue);
            }
            _isDraggingProgress = false;
            OnPropertyChanged("ProgressValue");
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

        // 窗口透明度（0.4~1.0，1.0 为完全不透明）
        private double _windowOpacity = 1.0;
        public double WindowOpacity
        {
            get { return _windowOpacity; }
            set
            {
                var v = Math.Clamp(value, 0.4, 1.0);
                if (Math.Abs(_windowOpacity - v) < 0.001)
                {
                    return;
                }
                _windowOpacity = v;
                OnPropertyChanged();
                SaveSettings();
            }
        }

        public async void AutoAdjustNoteLevel()
        {
            if (SelectedMidiFile == null || MidiPlayer == null) return;

            await Task.Run(() =>
            {
                // 同时计算钢琴(21键)、圆号(14键)与吉他(14单音)三套最佳移调及命中率
                SelectedMidiFile.CalculateBestNoteLevel();
            });

            // 三套最佳移调分别持久化：钢琴 key=Name_0，圆号 key=Name_1，吉他 key=Name_2
            var pianoKey = $"{SelectedMidiFile.Name}_{(int)InstrumentType.Piano}";
            var hornKey = $"{SelectedMidiFile.Name}_{(int)InstrumentType.FrenchHorn}";
            var guitarKey = $"{SelectedMidiFile.Name}_{(int)InstrumentType.Guitar}";
            _noteLevelSettings[pianoKey] = SelectedMidiFile.BestNoteLevel;
            _noteLevelSettings[hornKey] = SelectedMidiFile.FrenchHornBestNoteLevel;
            _noteLevelSettings[guitarKey] = SelectedMidiFile.GuitarBestNoteLevel;
            SaveSettings();

            // 当前乐器的移调值设为对应的最佳移调（通过 NoteLevel setter 同步到播放器）
            NoteLevel = InstrumentType switch
            {
                InstrumentType.FrenchHorn => SelectedMidiFile.FrenchHornBestNoteLevel,
                InstrumentType.Guitar => SelectedMidiFile.GuitarBestNoteLevel,
                _ => SelectedMidiFile.BestNoteLevel,
            };

            OnPropertyChanged("NoteLevel");
            OnPropertyChanged("CanAutoAdjust");
            OnPropertyChanged("BestNoteRadio");
            OnPropertyChanged("FrenchHornBestNoteRadio");
            OnPropertyChanged("GuitarBestNoteRadio");
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
                        else if (line.StartsWith("_WindowOpacity="))
                        {
                            if (double.TryParse(line.Split('=')[1], out double opacity))
                            {
                                _windowOpacity = Math.Clamp(opacity, 0.4, 1.0);
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
                lines.Insert(1, $"_WindowOpacity={_windowOpacity}");
                File.WriteAllLines(settingPath, lines);
            }
            catch { }
        }

        public void ApplyStoredNoteLevel(IScoreInfo? info = null)
        {
            var target = info ?? SelectedMidiFile;
            if (target != null && MidiPlayer != null)
            {
                var key = $"{target.Name}_{(int)InstrumentType}";
                if (_noteLevelSettings.TryGetValue(key, out int savedLevel))
                {
                    MidiPlayer.NoteLevel = savedLevel;
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

        public void DeleteSelectedFiles(IList<IScoreInfo> selectedFiles)
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

            var files = Directory.GetFiles(midiDir).Where(x =>
            {
                var ext = Path.GetExtension(x).ToLower();
                return ext == ".mid" || ext == ".midi" || ext == ".txt" || ext == ".dms";
            }).ToList();

            if (files.Count > 0)
            {
                var allInfos = new List<IScoreInfo>();
                var dmsInfos = new List<IScoreInfo>();
                var midiInfos = new List<IScoreInfo>();

                // Use parallel processing for faster loading
                var loadTasks = files.Select(file => Task.Run(() =>
                {
                    try
                    {
                        var ext = Path.GetExtension(file).ToLower();
                        if (ext == ".txt" || ext == ".dms")
                        {
                            return (info: (IScoreInfo)new DoMiSoFileInfo(file), isDms: true);
                        }
                        else
                        {
                            return (info: (IScoreInfo)new MidiFileInfo(file), isDms: false);
                        }
                    }
                    catch
                    {
                        return (info: (IScoreInfo?)null, isDms: false);
                    }
                })).ToArray();

                Task.WaitAll(loadTasks);

                foreach (var task in loadTasks)
                {
                    var result = task.Result;
                    if (result.info != null)
                    {
                        allInfos.Add(result.info);
                        if (result.isDms)
                            dmsInfos.Add(result.info);
                        else
                            midiInfos.Add(result.info);
                    }
                }

                var orderedAllInfos = allInfos.OrderBy(x => x.Name).ToList();
                var orderedDmsInfos = dmsInfos.OrderBy(x => x.Name).ToList();
                var orderedMidiInfos = midiInfos.OrderBy(x => x.Name).ToList();

                _allFilesSource = orderedAllInfos;
                _dmsFilesSource = orderedDmsInfos;
                _midiFilesSource = orderedMidiInfos;

                ApplySortAndFilter();
                SelectedMidiFile = MidiFileInfoList.FirstOrDefault();
            }
            else
            {
                _allFilesSource = new List<IScoreInfo>();
                _dmsFilesSource = new List<IScoreInfo>();
                _midiFilesSource = new List<IScoreInfo>();
                MidiFileInfoList = new ObservableCollection<IScoreInfo>();
                DoMiSoFileList = new ObservableCollection<IScoreInfo>();
                MidiOnlyFileList = new ObservableCollection<IScoreInfo>();
                SelectedMidiFile = null;
            }
            OnPropertyChanged("MidiFileInfoList");
            OnPropertyChanged("DoMiSoFileList");
            OnPropertyChanged("MidiOnlyFileList");
        }

        public void DeleteFilesBelowThreshold(double threshold)
        {
            var filesToDelete = new List<IScoreInfo>();
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
        private HwndSource? hwndSource;
        private static MidiPlayer? MidiPlayer;
        private System.Timers.Timer? timer;

        public MidiViewModel()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var midiDir = Path.Combine(baseDir, "Resource", "Midi");

            if (!Directory.Exists(midiDir))
            {
                Directory.CreateDirectory(midiDir);
            }

            LoadSettings();

            // Initialize empty collections first for immediate UI display
            MidiFileInfoList = new ObservableCollection<IScoreInfo>();
            DoMiSoFileList = new ObservableCollection<IScoreInfo>();
            MidiOnlyFileList = new ObservableCollection<IScoreInfo>();

            // Load files asynchronously in background
            Task.Run(() => LoadMidiFilesAsync(midiDir));
        }

        private async void LoadMidiFilesAsync(string midiDir)
        {
            var files = Directory.GetFiles(midiDir).Where(x =>
            {
                var ext = Path.GetExtension(x).ToLower();
                return ext == ".mid" || ext == ".midi" || ext == ".txt" || ext == ".dms";
            }).ToList();

            if (files.Count > 0)
            {
                var allInfos = new List<IScoreInfo>();
                var dmsInfos = new List<IScoreInfo>();
                var midiInfos = new List<IScoreInfo>();

                // Use parallel processing for faster loading
                var loadTasks = files.Select(file => Task.Run(() =>
                {
                    try
                    {
                        var ext = Path.GetExtension(file).ToLower();
                        if (ext == ".txt" || ext == ".dms")
                        {
                            return (info: (IScoreInfo)new DoMiSoFileInfo(file), isDms: true);
                        }
                        else
                        {
                            return (info: (IScoreInfo)new MidiFileInfo(file), isDms: false);
                        }
                    }
                    catch
                    {
                        return (info: (IScoreInfo?)null, isDms: false);
                    }
                })).ToArray();

                // Await without blocking - run in background
                await Task.WhenAll(loadTasks);

                foreach (var task in loadTasks)
                {
                    var result = task.Result;
                    if (result.info != null)
                    {
                        allInfos.Add(result.info);
                        if (result.isDms)
                            dmsInfos.Add(result.info);
                        else
                            midiInfos.Add(result.info);
                    }
                }

                if (allInfos.Count > 0)
                {
                    var orderedAllInfos = allInfos.OrderBy(x => x.Name).ToList();
                    var orderedDmsInfos = dmsInfos.OrderBy(x => x.Name).ToList();
                    var orderedMidiInfos = midiInfos.OrderBy(x => x.Name).ToList();

                    _allFilesSource = orderedAllInfos;
                    _dmsFilesSource = orderedDmsInfos;
                    _midiFilesSource = orderedMidiInfos;

                    // Update UI on the UI thread
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        ApplySortAndFilter();

                        if (MidiFileInfoList.Count > 0)
                        {
                            MidiPlayer = new MidiPlayer();
                            MidiPlayer.Started += MidiPlayer_Started;
                            MidiPlayer.Stopped += MidiPlayer_Stopped;
                            MidiPlayer.Finished += MidiPlayer_Finished;
                            SelectedMidiFile = MidiFileInfoList.First();
                            ChangePlayFile(SelectedMidiFile!, false);
                            ApplyStoredNoteLevel();
                        }
                    });
                }
                else
                {
                    _allFilesSource = new List<IScoreInfo>();
                    _dmsFilesSource = new List<IScoreInfo>();
                    _midiFilesSource = new List<IScoreInfo>();
                    MidiFileInfoList = new ObservableCollection<IScoreInfo>();
                    DoMiSoFileList = new ObservableCollection<IScoreInfo>();
                    MidiOnlyFileList = new ObservableCollection<IScoreInfo>();
                    MidiPlayer = new MidiPlayer();
                }
            }
            else
            {
                _allFilesSource = new List<IScoreInfo>();
                _dmsFilesSource = new List<IScoreInfo>();
                _midiFilesSource = new List<IScoreInfo>();
                MidiFileInfoList = new ObservableCollection<IScoreInfo>();
                MidiPlayer = new MidiPlayer();
            }

            InitHotKey();
            RefreshState();

            if (files.Count > 0)
            {
                timer = new System.Timers.Timer(1000);
                timer.AutoReset = true;
                timer.Elapsed += Timer_Elapsed;
            }
        }


        private void InitHotKey()
        {
            // 窗口句柄在窗口显示后才有效；若尚未就绪则延迟到 UI 线程再试
            hWnd = Process.GetCurrentProcess().MainWindowHandle;
            if (hWnd == IntPtr.Zero)
            {
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(
                    new Action(() => InitHotKey()),
                    System.Windows.Threading.DispatcherPriority.Loaded);
                return;
            }

            hotkey = Util.RegisterHotKey(hWnd);
            hwndSource = HwndSource.FromHwnd(hWnd);
            hwndSource?.AddHook(HwndHook);
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


        private void MidiPlayer_Started(object? sender, EventArgs e)
        {
            timer?.Start();
            OnPropertyChanged("IsPlaying");
            OnPropertyChanged("CurrentTime");
        }
        private void MidiPlayer_Stopped(object? sender, EventArgs e)
        {
            timer?.Stop();
            OnPropertyChanged("IsPlaying");
            OnPropertyChanged("CurrentTime");
        }

        private void MidiPlayer_Finished(object? sender, EventArgs e)
        {
            timer?.Stop();
            OnPropertyChanged("IsPlaying");
            OnPropertyChanged("CurrentTime");
        }

        private void Timer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            OnPropertyChanged("IsPlaying");
            OnPropertyChanged("CurrentTime");
            if (!_isDraggingProgress)
            {
                OnPropertyChanged("ProgressValue");
            }
        }


        public void ChangePlayFile(IScoreInfo info, bool autoPlay = true)
        {
            if (MidiPlayer != null && info != null)
            {
                MidiPlayer.ChangeFileInfo(info, autoPlay);
                ApplyStoredNoteLevel(info);
                OnPropertyChanged("Name");
                OnPropertyChanged("IsPlaying");
                OnPropertyChanged("AutoSwitchToGenshinWindow");
                OnPropertyChanged("PlayBackground");
                OnPropertyChanged("Speed");
                OnPropertyChanged("NoteLevel");
                OnPropertyChanged("TotalTime");
                OnPropertyChanged("CurrentTime");
                OnPropertyChanged("ProgressMaximum");
                OnPropertyChanged("ProgressValue");
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
                MidiPlayer!.Started -= MidiPlayer_Started;
                MidiPlayer.Stopped -= MidiPlayer_Stopped;
                MidiPlayer.Finished -= MidiPlayer_Finished;
                MidiPlayer.ChangeFileInfo();
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
            Button_Restart_Content = string.Empty;
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
                MidiPlayer?.RefreshGameWindow();
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
            if (MidiPlayer?.MidiFileInfo == null)
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
            if (MidiPlayer?.MidiFileInfo == null)
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

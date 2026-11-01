using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using MidiAutoPlayer.Core.Native;
using MidiAutoPlayer.Core.MusicGame;
using static MidiAutoPlayer.Core.Native.Msg;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Devices;
using Melanchall.DryWetMidi.Interaction;

namespace MidiAutoPlayer.Core.Midi
{
    public class MidiPlayer
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, [MarshalAs(UnmanagedType.LPArray), In] INPUT[] pInputs, int cbSize);

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public InputUnion u;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)]
            public MOUSEINPUT mi;
            [FieldOffset(0)]
            public KEYBDINPUT ki;
            [FieldOffset(0)]
            public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        private enum InputType : uint
        {
            INPUT_MOUSE = 0,
            INPUT_KEYBOARD = 1,
            INPUT_HARDWARE = 2
        }

        [Flags]
        private enum KeyEventF : uint
        {
            KEYEVENTF_EXTENDEDKEY = 0x0001,
            KEYEVENTF_KEYUP = 0x0002,
            KEYEVENTF_SCANCODE = 0x0008,
            KEYEVENTF_UNICODE = 0x0004
        }

        private static void Log(string msg)
        {
            try
            {
                var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "midi.log");
                File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] {msg}\n");
            }
            catch { }
        }

        public IScoreInfo? MidiFileInfo { get; private set; }

        public string Name { get; set; } = "";

        public bool CanPlay { get; private set; }

        public bool IsPlaying
        {
            get { return _playback?.IsRunning ?? false; }
            set
            {
                Log($"IsPlaying set to {value}, _playback is null: {_playback == null}");
                if (_playback == null)
                {
                    Log("  _playback is null, returning");
                    return;
                }
                if (IsPlaying == value)
                {
                    Log("  Already in this state, returning");
                    return;
                }
                if (value)
                {
                    Log("  Starting playback...");
                    if (AutoSwitchToGenshinWindow && _hWnd != IntPtr.Zero)
                    {
                        Log($"  Switching to window {_hWnd}");
                        User32.SwitchToThisWindow(_hWnd, true);
                        Thread.Sleep(100);
                    }
                    _playback.Start();
                    Log($"  Playback started, IsRunning: {_playback.IsRunning}");
                }
                else
                {
                    Log("  Stopping playback...");
                    foreach (var kvp in _activeKeys)
                    {
                        SendKeyUp(kvp.Key, false);
                    }
                    _activeKeys.Clear();
                    _playback.Stop();
                }
            }
        }

        public TimeSpan TotalTime { get; private set; }

        public TimeSpan CurrentTime
        {
            get { return _playback?.GetCurrentTime<MetricTimeSpan>() ?? TimeSpan.Zero; }
            set 
            { 
                if (_playback != null)
                {
                    _playback.MoveToTime(new MetricTimeSpan(value));
                }
            }
        }

        public bool PlayBackground { get; set; }

        public bool AutoSwitchToGenshinWindow { get; set; } = true;

        public double Speed
        {
            get { return _playback?.Speed ?? 1.0; }
            set
            {
                if (_playback == null || IsPlaying)
                {
                    return;
                }
                if (value > 0)
                {
                    _playback.Speed = value;
                }
            }
        }

        public int NoteLevel { get; set; }

        public InstrumentType InstrumentType { get; set; } = InstrumentType.Piano;

        public event EventHandler? Started;

        public event EventHandler? Stopped;

        public event EventHandler? Finished;

        private Dictionary<ushort, CancellationTokenSource> _activeKeys = new Dictionary<ushort, CancellationTokenSource>();

        // ============ 吉他和弦识别缓冲 ============
        // 吉他模式下，NoteOn 先进入缓冲，由定时器在「和弦窗口」结束后统一 flush，
        // 把窗口内快速出现的音归并成和弦键（匹配成功）或退回逐个单音键。
        private readonly List<(int noteNumber, long timeMs)> _guitarChordBuffer = new List<(int, long)>();
        private System.Threading.Timer? _guitarChordTimer;
        private readonly object _guitarBufferLock = new object();
        // 和弦识别窗口：毫秒。此窗口内到达的 NoteOn 视为「同时」参与和弦匹配。
        private const int GuitarChordWindowMs = 100;

        private void _playback_Started(object? sender, EventArgs e)
        {
            Log("_playback_Started fired");
            Started?.Invoke(this, e);
        }

        private void _playback_Stopped(object? sender, EventArgs e)
        {
            Log("_playback_Stopped fired");
            FlushAndClearGuitarBuffer();
            foreach (var kvp in _activeKeys)
            {
                SendKeyUp(kvp.Key, false);
            }
            _activeKeys.Clear();
            Stopped?.Invoke(this, e);
        }

        private void _playback_Finished(object? sender, EventArgs e)
        {
            Log("_playback_Finished fired");
            FlushAndClearGuitarBuffer();
            foreach (var kvp in _activeKeys)
            {
                SendKeyUp(kvp.Key, false);
            }
            _activeKeys.Clear();
            Finished?.Invoke(this, e);
        }

        // 清空吉他和弦缓冲与定时器（播放停止/结束/切歌时调用）。
        private void FlushAndClearGuitarBuffer()
        {
            lock (_guitarBufferLock)
            {
                _guitarChordBuffer.Clear();
            }
            _guitarChordTimer?.Dispose();
            _guitarChordTimer = null;
        }

        private Playback? _playback;

        private IntPtr _hWnd;

        public MidiPlayer(string processName)
        {
            Log($"MidiPlayer created with processName: {processName}");
            var pros = Process.GetProcessesByName(processName);
            if (pros.Any())
            {
                _hWnd = pros[0].MainWindowHandle;
                CanPlay = true;
                Log($"Found process {processName}, hWnd: {_hWnd}");
            }
            else
            {
                _hWnd = IntPtr.Zero;
                Log($"Process {processName} not found");
            }
        }

        public MidiPlayer()
        {
            Log("MidiPlayer() created");
            RefreshGameWindow();
        }

        public void RefreshGameWindow()
        {
            Log("Refreshing game window...");
            var pros = Process.GetProcessesByName("YuanShen").ToList();
            pros.AddRange(Process.GetProcessesByName("GenshinImpact"));
            pros.AddRange(Process.GetProcessesByName("NarakaBladepointMobile"));
            if (pros.Any())
            {
                _hWnd = pros[0].MainWindowHandle;
                CanPlay = true;
                Log($"Found game process, hWnd: {_hWnd}");
            }
            else
            {
                _hWnd = IntPtr.Zero;
                CanPlay = false;
                Log("No game process found");
            }
        }

        ~MidiPlayer()
        {
            _playback?.Dispose();
        }

        public void ChangeFileInfo(IScoreInfo info, bool autoPlay = true)
        {
            Log($"ChangeFileInfo called with autoPlay: {autoPlay}");
            if (info == null) return;
            FlushAndClearGuitarBuffer();
            foreach (var kvp in _activeKeys)
            {
                SendKeyUp(kvp.Key, false);
            }
            _activeKeys.Clear();
            Name = info.Name;
            MidiFileInfo = info;
            ChangeFileInfoInternal();
            if (autoPlay)
            {
                IsPlaying = true;
            }
        }

        public void ChangeFileInfo(bool autoPlay = true)
        {
            Log($"ChangeFileInfo(bool) called with autoPlay: {autoPlay}");
            if (MidiFileInfo == null)
            {
                Log("MidiFileInfo is null, cannot change");
                return;
            }
            FlushAndClearGuitarBuffer();
            foreach (var kvp in _activeKeys)
            {
                SendKeyUp(kvp.Key, false);
            }
            _activeKeys.Clear();
            var time = CurrentTime;
            ChangeFileInfoInternal();
            if (autoPlay)
            {
                IsPlaying = true;
            }
            CurrentTime = time;
        }

        private void ChangeFileInfoInternal()
        {
            if (MidiFileInfo == null)
            {
                Log("ChangeFileInfoInternal: MidiFileInfo is null");
                return;
            }
            Log("ChangeFileInfoInternal: Setting up playback");
            var speed = _playback?.Speed;
            _playback?.Dispose();

            if (MidiFileInfo is MidiFileInfo midiFileInfo)
            {
                midiFileInfo.MidiFile.Chunks.Clear();
                midiFileInfo.MidiFile.Chunks.AddRange(midiFileInfo.MidiTracks.Where(x => x.IsCheck).Select(x => x.Track));
                _playback = midiFileInfo.MidiFile.GetPlayback();
                TotalTime = midiFileInfo.MidiFile.GetDuration<MetricTimeSpan>();
            }
            else
            {
                _playback = MidiFileInfo.MidiFile.GetPlayback();
                TotalTime = MidiFileInfo.MidiFile.GetDuration<MetricTimeSpan>();
            }

            _playback.Speed = speed ?? 1;
            _playback.InterruptNotesOnStop = true;
            _playback.EventPlayed += NoteEventPlayed;
            _playback.Started += _playback_Started;
            _playback.Stopped += _playback_Stopped;
            _playback.Finished += _playback_Finished;
            Log($"ChangeFileInfoInternal complete, Duration: {TotalTime}");
        }

        private void NoteEventPlayed(object? sender, MidiEventPlayedEventArgs e)
        {
            var dictionary = InstrumentType == InstrumentType.FrenchHorn 
                ? Const.FrenchHornNoteToVisualKeyDictionary 
                : InstrumentType == InstrumentType.Guitar
                    ? Const.GuitarNoteToVisualKeyDictionary
                    : Const.NoteToVisualKeyDictionary;

            if (e.Event.EventType == MidiEventType.NoteOn)
            {
                var note = e.Event as NoteOnEvent;
                if (note == null) return;

                // velocity == 0 的 NoteOn 在 MIDI 规范中等价于 NoteOff，跳过，不发声。
                if (note.Velocity == 0) return;

                // 不做任何八度折返：整体移调 note + NoteLevel，
                // 超出乐器音域的音直接丢弃（与命中率统计逻辑一致）。
                var num = note.NoteNumber + NoteLevel;

                // 吉他：先进入和弦缓冲，交由定时器窗口归并。
                if (InstrumentType == InstrumentType.Guitar)
                {
                    lock (_guitarBufferLock)
                    {
                        _guitarChordBuffer.Add((num, Environment.TickCount64));
                    }
                    ResetGuitarChordTimer();
                    return;
                }

                if (dictionary.ContainsKey(num))
                {
                    var keyCode = (ushort)dictionary[num];
                    Log($"NoteOn: note={note.NoteNumber}, adjusted={num}, key={keyCode}, instrument={InstrumentType}");

                    if (InstrumentType == InstrumentType.Piano)
                    {
                        SendKey(keyCode, PlayBackground);
                    }
                    else
                    {
                        if (_activeKeys.ContainsKey(keyCode))
                        {
                            _activeKeys[keyCode].Cancel();
                            _activeKeys[keyCode].Dispose();
                            _activeKeys.Remove(keyCode);
                            SendKeyUp(keyCode, PlayBackground);
                        }

                        SendKeyDown(keyCode, PlayBackground);

                        var cts = new CancellationTokenSource();
                        _activeKeys[keyCode] = cts;
                    }
                }
                else
                {
                    Log($"NoteEventPlayed: note={note.NoteNumber}, adjusted={num} - key not found");
                }
            }
            else if (e.Event.EventType == MidiEventType.NoteOff)
            {
                var note = e.Event as NoteOffEvent;
                if (note == null) return;

                // 与 NoteOn 一致：整体移调，不做八度折返。
                var num = note.NoteNumber + NoteLevel;

                // 吉他：NoteOff 交由和弦缓冲 flush 后的单音松开逻辑处理，
                // 此处不单独处理（避免与和弦归并冲突）。
                if (InstrumentType == InstrumentType.Guitar)
                {
                    return;
                }

                if (dictionary.ContainsKey(num))
                {
                    var keyCode = (ushort)dictionary[num];
                    Log($"NoteOff: note={note.NoteNumber}, adjusted={num}, key={keyCode}");

                    if (_activeKeys.ContainsKey(keyCode))
                    {
                        _activeKeys[keyCode].Cancel();
                        _activeKeys[keyCode].Dispose();
                        _activeKeys.Remove(keyCode);
                    }

                    SendKeyUp(keyCode, PlayBackground);
                }
            }
        }

        // 重置吉他和弦窗口定时器：每次新音符进入缓冲后，重新计时一个窗口期。
        private void ResetGuitarChordTimer()
        {
            _guitarChordTimer?.Dispose();
            _guitarChordTimer = new System.Threading.Timer(_ => FlushGuitarChordBuffer(), null, GuitarChordWindowMs, System.Threading.Timeout.Infinite);
        }

        // 将缓冲窗口内的音符归并成和弦或单音，并触发按键。
        private void FlushGuitarChordBuffer()
        {
            List<(int noteNumber, long timeMs)> buffered;
            lock (_guitarBufferLock)
            {
                if (_guitarChordBuffer.Count == 0) return;
                buffered = new List<(int, long)>(_guitarChordBuffer);
                _guitarChordBuffer.Clear();
            }

            // 提取去重后的音高集合。
            var noteSet = buffered.Select(x => x.noteNumber).Distinct().ToList();

            // 尝试匹配和弦：音级集合（模 12）完全等于某个和弦的组成音级集合。
            var pitchClasses = noteSet.Select(n => ((n % 12) + 12) % 12).OrderBy(x => x).ToList();
            Const.GuitarChord? matched = Const.GuitarChords.FirstOrDefault(c =>
            {
                var cp = c.PitchClasses.Select(p => ((p % 12) + 12) % 12).OrderBy(x => x).ToList();
                return cp.SequenceEqual(pitchClasses);
            });

            if (matched != null)
            {
                // 匹配到和弦：按对应和弦键（瞬时按下并松开，类似 SendKey）。
                var chordKey = (ushort)matched.Key;
                Log($"Guitar chord match: {matched.Name} -> key={chordKey}");
                SendKey(chordKey, PlayBackground);
            }
            else
            {
                // 未匹配到和弦：退回逐个按单音键（若在下两排单音音域内）。
                foreach (var n in noteSet)
                {
                    if (Const.GuitarNoteToVisualKeyDictionary.ContainsKey(n))
                    {
                        var keyCode = (ushort)Const.GuitarNoteToVisualKeyDictionary[n];
                        Log($"Guitar single note fallback: note={n}, key={keyCode}");
                        SendKey(keyCode, PlayBackground);
                    }
                    else
                    {
                        Log($"Guitar note not playable: note={n}");
                    }
                }
            }
        }

        private void SendKey(ushort keyCode, bool background)
        {
            Log($"SendKey: keyCode={keyCode}, background={background}");
            
            if (_hWnd == IntPtr.Zero)
            {
                Log("  _hWnd is zero, cannot send key");
                return;
            }

            if (background)
            {
                User32.PostMessage(_hWnd, Msg.WM_ACTIVATE, 1, 0);
            }

            User32.PostMessage(_hWnd, Msg.WM_KEYDOWN, (uint)keyCode, 0x001E0001);
            User32.PostMessage(_hWnd, Msg.WM_CHAR, (uint)keyCode, 0x001E0001);
            User32.PostMessage(_hWnd, Msg.WM_IME_KEYUP, (uint)keyCode, 0xC01E0001);
            
            Log("  PostMessage sent successfully");
        }

        private void SendKeyDown(ushort keyCode, bool background)
        {
            if (_hWnd == IntPtr.Zero) return;

            if (background)
            {
                User32.PostMessage(_hWnd, Msg.WM_ACTIVATE, 1, 0);
            }

            User32.PostMessage(_hWnd, Msg.WM_KEYDOWN, (uint)keyCode, 0x001E0001);
            User32.PostMessage(_hWnd, Msg.WM_CHAR, (uint)keyCode, 0x001E0001);
        }

        private void SendKeyUp(ushort keyCode, bool background)
        {
            if (_hWnd == IntPtr.Zero) return;

            User32.PostMessage(_hWnd, Msg.WM_IME_KEYUP, (uint)keyCode, 0xC01E0001);
        }
    }
}

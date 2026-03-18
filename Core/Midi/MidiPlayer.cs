using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using MidiAutoPlayer.Core.Native;
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

        public MidiFileInfo? MidiFileInfo { get; private set; }

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

        public event EventHandler? Started;

        public event EventHandler? Stopped;

        public event EventHandler? Finished;

        private void _playback_Started(object? sender, EventArgs e)
        {
            Log("_playback_Started fired");
            Started?.Invoke(this, e);
        }

        private void _playback_Stopped(object? sender, EventArgs e)
        {
            Log("_playback_Stopped fired");
            Stopped?.Invoke(this, e);
        }

        private void _playback_Finished(object? sender, EventArgs e)
        {
            Log("_playback_Finished fired");
            Finished?.Invoke(this, e);
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

        public void ChangeFileInfo(MidiFileInfo info, bool autoPlay = true)
        {
            Log($"ChangeFileInfo called with autoPlay: {autoPlay}");
            if (info == null) return;
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
            MidiFileInfo.MidiFile.Chunks.Clear();
            MidiFileInfo.MidiFile.Chunks.AddRange(MidiFileInfo.MidiTracks.Where(x => x.IsCheck).Select(x => x.Track));
            _playback = MidiFileInfo.MidiFile.GetPlayback();
            TotalTime = MidiFileInfo.MidiFile.GetDuration<MetricTimeSpan>();
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
            if (e.Event.EventType == MidiEventType.NoteOn)
            {
                var note = e.Event as NoteOnEvent;
                if (note == null) return;
                
                var num = note.NoteNumber + NoteLevel;
                while (true)
                {
                    if (num < 48)
                    {
                        num += 12;
                    }
                    if (num > 83)
                    {
                        num -= 12;
                    }
                    if (num >= 48 && num <= 83)
                    {
                        break;
                    }
                }
                if (Const.NoteToVisualKeyDictionary.ContainsKey(num))
                {
                    var keyCode = (ushort)Const.NoteToVisualKeyDictionary[num];
                    Log($"NoteEventPlayed: note={note.NoteNumber}, adjusted={num}, key={keyCode}");
                    SendKey(keyCode, PlayBackground);
                }
                else
                {
                    Log($"NoteEventPlayed: note={note.NoteNumber}, adjusted={num} - key not found");
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
    }
}

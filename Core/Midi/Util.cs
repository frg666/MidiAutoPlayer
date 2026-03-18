using System;
using System.Diagnostics;
using System.Linq;
using System.Security.Principal;
using System.Text;
using MidiAutoPlayer.Core.Native;
using static MidiAutoPlayer.Core.Native.FsModifier;
using static MidiAutoPlayer.Core.Native.Msg;
using static MidiAutoPlayer.Core.Native.User32;
using static MidiAutoPlayer.Core.Native.VirtualKey;

namespace MidiAutoPlayer.Core.Midi
{
    public static class Util
    {
        internal static byte GetCharAsciiCode(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentNullException("Cannot get ascii value of null");
            }
            return Encoding.ASCII.GetBytes(key).First();
        }

        public static bool IsAdmin()
        {
            WindowsIdentity id = WindowsIdentity.GetCurrent();
            WindowsPrincipal p = new WindowsPrincipal(id);
            return p.IsInRole(WindowsBuiltInRole.Administrator);
        }


        public static void RestartAsAdmin()
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                UseShellExecute = true,
                WorkingDirectory = Environment.CurrentDirectory,
                FileName = Process.GetCurrentProcess().ProcessName,
                Verb = "runas"
            };
            Process.Start(startInfo);
            Environment.Exit(0);
        }

        public static bool RegisterHotKey(IntPtr hWnd)
        {
            if (!User32.RegisterHotKey(hWnd, 1000, (uint)MOD_NOREPEAT, (uint)VK_NUMPAD8))
            {
                return false;
            }
            if (!User32.RegisterHotKey(hWnd, 1001, (uint)MOD_NOREPEAT, (uint)VK_NUMPAD7))
            {
                return false;
            }
            if (!User32.RegisterHotKey(hWnd, 1002, (uint)MOD_NOREPEAT, (uint)VK_NUMPAD9))
            {
                return false;
            }
            return true;
        }


        public static bool UnregisterHotKey(IntPtr hWnd)
        {
            if (!User32.UnregisterHotKey(hWnd, 1000))
            {
                return false;
            }
            if (!User32.UnregisterHotKey(hWnd, 1001))
            {
                return false;
            }
            if (!User32.UnregisterHotKey(hWnd, 1002))
            {
                return false;
            }
            return true;
        }



        internal static VirtualKey NoteNumberToVisualKey(int noteNumber)
        {
            return Const.NoteToVisualKeyDictionary[noteNumber];
        }

        internal static void Postkey(IntPtr hWnd, int noteNumber, bool allowBackground = false)
        {
            if (allowBackground)
            {
                PostMessage(hWnd, WM_ACTIVATE, 1, 0);
            }
            PostMessage(hWnd, WM_KEYDOWN, (uint)NoteNumberToVisualKey(noteNumber), 0x1e0001);
            PostMessage(hWnd, WM_CHAR, (uint)NoteNumberToVisualKey(noteNumber), 0x1e0001);
            PostMessage(hWnd, WM_KEYUP, (uint)NoteNumberToVisualKey(noteNumber), 0xc01e0001);
        }

    }
}

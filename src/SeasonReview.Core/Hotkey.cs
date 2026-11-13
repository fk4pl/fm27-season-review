using System;
using System.Runtime.InteropServices;

namespace SeasonReview
{
    /// <summary>
    /// A keyboard shortcut such as "Ctrl+F8", polled through the Win32 API.
    /// FM27 is a Windows game on both supported platforms (natively, or under Proton/Wine),
    /// so user32 is always available and we do not depend on the game's input system.
    /// </summary>
    internal sealed class Hotkey
    {
        private const int VkShift = 0x10, VkControl = 0x11, VkAlt = 0x12;

        public int VirtualKey { get; private set; }
        public bool Ctrl { get; private set; }
        public bool Shift { get; private set; }
        public bool Alt { get; private set; }

        private bool _wasDown;

        public static bool TryParse(string text, out Hotkey hotkey)
        {
            hotkey = new Hotkey();
            if (string.IsNullOrWhiteSpace(text)) return false;

            foreach (var raw in text.Split('+'))
            {
                var part = raw.Trim().ToUpperInvariant();
                if (part == "CTRL" || part == "CONTROL") hotkey.Ctrl = true;
                else if (part == "SHIFT") hotkey.Shift = true;
                else if (part == "ALT") hotkey.Alt = true;
                else if (hotkey.VirtualKey != 0) return false; // two main keys
                else if (part.Length >= 2 && part[0] == 'F' && int.TryParse(part.Substring(1), out var f) && f >= 1 && f <= 24)
                    hotkey.VirtualKey = 0x70 + f - 1;
                else if (part.Length == 1 && (char.IsLetter(part[0]) || char.IsDigit(part[0])))
                    hotkey.VirtualKey = part[0];
                else return false;
            }
            return hotkey.VirtualKey != 0;
        }

        /// <summary>True once per key press, and only while the game window has focus.</summary>
        public bool PressedThisFrame()
        {
            var down = IsDown(VirtualKey)
                       && IsDown(VkControl) == Ctrl
                       && IsDown(VkShift) == Shift
                       && IsDown(VkAlt) == Alt;
            var pressed = down && !_wasDown && GameHasFocus();
            _wasDown = down;
            return pressed;
        }

        public override string ToString() =>
            (Ctrl ? "Ctrl+" : "") + (Shift ? "Shift+" : "") + (Alt ? "Alt+" : "") +
            (VirtualKey >= 0x70 && VirtualKey <= 0x87 ? "F" + (VirtualKey - 0x70 + 1) : ((char)VirtualKey).ToString());

        private static bool IsDown(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

        private static bool GameHasFocus()
        {
            var window = GetForegroundWindow();
            if (window == IntPtr.Zero) return false;
            GetWindowThreadProcessId(window, out var pid);
            return pid == (uint)Environment.ProcessId;
        }

        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vKey);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    }
}

using System;
using System.Runtime.InteropServices;

namespace HidWizards.UCR.Core.Utilities
{
    /// <summary>
    /// Thin adapter over the Win32 SendInput API. Injects synthetic keyboard events into the same
    /// input stream a real keyboard driver feeds -- no kernel driver, no reading of real keyboard
    /// input, output-only. Contains no business logic beyond the P/Invoke marshaling itself. Lives in
    /// UCR.Core (not UCR.Plugins, where it originated) because DeviceBinding's own auxiliary-keyboard-
    /// key feature needs it too, and UCR.Plugins already depends on UCR.Core, not the other way round.
    /// </summary>
    public static class NativeKeyboardInput
    {
        private const int InputKeyboard = 1;
        private const uint KeyEventFKeyUp = 0x0002;

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
        private struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public InputUnion u;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        /// <summary>
        /// Presses or releases a single virtual key. Uses SendInput rather than the older keybd_event
        /// (which SendInput's own documentation says to prefer) for more reliable delivery, including
        /// to elevated windows when this process is also elevated.
        /// </summary>
        public static void SendKey(ushort virtualKeyCode, bool keyDown)
        {
            var input = new INPUT
            {
                type = InputKeyboard,
                u = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = virtualKeyCode,
                        wScan = 0,
                        dwFlags = keyDown ? 0 : KeyEventFKeyUp,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };

            SendInput(1, new[] { input }, Marshal.SizeOf(typeof(INPUT)));
        }
    }
}

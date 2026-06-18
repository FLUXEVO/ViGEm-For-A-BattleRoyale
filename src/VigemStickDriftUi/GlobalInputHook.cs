using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace VigemStickDriftUi
{
    internal sealed class KeyStateChangedEventArgs : EventArgs
    {
        public KeyStateChangedEventArgs(Keys key, bool isDown)
        {
            Key = key;
            IsDown = isDown;
        }

        public Keys Key { get; }
        public bool IsDown { get; }
    }

    internal sealed class GlobalInputHook : IDisposable
    {
        private const int WH_MOUSE_LL = 14;
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_RBUTTONUP = 0x0205;
        private const int WM_MOUSEWHEEL = 0x020A;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        private readonly NativeMethods.LowLevelMouseProc _mouseProc;
        private readonly NativeMethods.LowLevelKeyboardProc _keyboardProc;
        private IntPtr _mouseHookHandle = IntPtr.Zero;
        private IntPtr _keyboardHookHandle = IntPtr.Zero;
        private bool _disposed;

        public event EventHandler<bool> LeftButtonStateChanged;
        public event EventHandler<bool> RightButtonStateChanged;
        public event EventHandler WheelScrolled;
        public event EventHandler<KeyStateChangedEventArgs> KeyStateChanged;

        public GlobalInputHook()
        {
            _mouseProc = MouseHookCallback;
            _keyboardProc = KeyboardHookCallback;
        }

        public void Start()
        {
            if (_mouseHookHandle != IntPtr.Zero || _keyboardHookHandle != IntPtr.Zero)
            {
                return;
            }

            using Process process = Process.GetCurrentProcess();
            using ProcessModule module = process.MainModule;
            IntPtr moduleHandle = NativeMethods.GetModuleHandle(module?.ModuleName);

            _mouseHookHandle = NativeMethods.SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, moduleHandle, 0);
            _keyboardHookHandle = NativeMethods.SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, moduleHandle, 0);

            if (_mouseHookHandle == IntPtr.Zero || _keyboardHookHandle == IntPtr.Zero)
            {
                Stop();
                throw new InvalidOperationException("Failed to install global input hooks.");
            }
        }

        public void Stop()
        {
            if (_mouseHookHandle != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_mouseHookHandle);
                _mouseHookHandle = IntPtr.Zero;
            }

            if (_keyboardHookHandle != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_keyboardHookHandle);
                _keyboardHookHandle = IntPtr.Zero;
            }
        }

        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int message = wParam.ToInt32();
                if (message == WM_LBUTTONDOWN)
                {
                    LeftButtonStateChanged?.Invoke(this, true);
                }
                else if (message == WM_LBUTTONUP)
                {
                    LeftButtonStateChanged?.Invoke(this, false);
                }
                else if (message == WM_RBUTTONDOWN)
                {
                    RightButtonStateChanged?.Invoke(this, true);
                }
                else if (message == WM_RBUTTONUP)
                {
                    RightButtonStateChanged?.Invoke(this, false);
                }
                else if (message == WM_MOUSEWHEEL)
                {
                    WheelScrolled?.Invoke(this, EventArgs.Empty);
                }
            }

            return NativeMethods.CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
        }

        private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int message = wParam.ToInt32();
                int vkCode = Marshal.ReadInt32(lParam);
                Keys key = (Keys)vkCode;

                if (message == WM_KEYDOWN || message == WM_SYSKEYDOWN)
                {
                    KeyStateChanged?.Invoke(this, new KeyStateChangedEventArgs(key, true));
                }
                else if (message == WM_KEYUP || message == WM_SYSKEYUP)
                {
                    KeyStateChanged?.Invoke(this, new KeyStateChangedEventArgs(key, false));
                }
            }

            return NativeMethods.CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Stop();
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        private static class NativeMethods
        {
            internal delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
            internal delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

            [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            internal static extern IntPtr SetWindowsHookEx(int idHook, Delegate lpfn, IntPtr hMod, uint dwThreadId);

            [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool UnhookWindowsHookEx(IntPtr hhk);

            [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            internal static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

            [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            internal static extern IntPtr GetModuleHandle(string lpModuleName);
        }
    }
}

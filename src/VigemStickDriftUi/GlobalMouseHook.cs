using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace VigemStickDriftUi
{
    internal sealed class GlobalMouseHook : IDisposable
    {
        private const int WH_MOUSE_LL = 14;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;

        private readonly NativeMethods.LowLevelMouseProc _proc;
        private IntPtr _hookHandle = IntPtr.Zero;
        private bool _disposed;

        public event EventHandler<bool> LeftButtonStateChanged;

        public GlobalMouseHook()
        {
            _proc = HookCallback;
        }

        public void Start()
        {
            if (_hookHandle != IntPtr.Zero)
                return;

            using Process process = Process.GetCurrentProcess();
            using ProcessModule module = process.MainModule;
            IntPtr moduleHandle = NativeMethods.GetModuleHandle(module?.ModuleName);
            _hookHandle = NativeMethods.SetWindowsHookEx(WH_MOUSE_LL, _proc, moduleHandle, 0);

            if (_hookHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to install global mouse hook.");
            }
        }

        public void Stop()
        {
            if (_hookHandle == IntPtr.Zero)
                return;

            NativeMethods.UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
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
            }

            return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            Stop();
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        private static class NativeMethods
        {
            internal delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

            [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            internal static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

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

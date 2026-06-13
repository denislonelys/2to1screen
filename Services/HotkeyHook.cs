using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using static TwoTo1Screen.Services.Native;

namespace TwoTo1Screen.Services
{
    /// <summary>
    /// Installs a global low-level keyboard hook that intercepts the
    /// PrintScreen key (incl. Win+PrintScreen) and, instead of letting
    /// Windows capture every monitor, captures only the configured monitor
    /// at full quality.
    /// </summary>
    internal sealed class HotkeyHook : IDisposable
    {
        private IntPtr _hook = IntPtr.Zero;
        private LowLevelKeyboardProc? _proc; // kept alive so it is not collected
        private readonly Func<AppSettings> _settings;

        public bool IsActive => _hook != IntPtr.Zero;

        public HotkeyHook(Func<AppSettings> settings)
        {
            _settings = settings;
        }

        public void Start()
        {
            if (_hook != IntPtr.Zero)
                return;

            _proc = HookCallback;
            IntPtr hModule = GetModuleHandle(null);
            _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, hModule, 0);
        }

        public void Stop()
        {
            if (_hook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hook);
                _hook = IntPtr.Zero;
            }
            _proc = null;
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int message = wParam.ToInt32();
                if (message == WM_KEYDOWN || message == WM_SYSKEYDOWN)
                {
                    var info = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                    if (info.vkCode == VK_SNAPSHOT)
                    {
                        AppSettings s = _settings();
                        bool winDown =
                            (GetAsyncKeyState(VK_LWIN) & 0x8000) != 0 ||
                            (GetAsyncKeyState(VK_RWIN) & 0x8000) != 0;

                        // Plain PrintScreen is always handled. Win+PrintScreen
                        // is only intercepted when the user enabled it.
                        if (!winDown || s.InterceptWinPrintScreen)
                        {
                            Task.Run(() => CaptureController.DoCapture(s));
                            return (IntPtr)1; // swallow -> prevents the all-monitor capture
                        }
                    }
                }
            }
            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        public void Dispose() => Stop();
    }
}

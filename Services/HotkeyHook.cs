using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using static TwoTo1Screen.Services.Native;

namespace TwoTo1Screen.Services
{
    /// <summary>
    /// Installs a global low-level keyboard hook that intercepts the configured
    /// capture hot-key (Print Screen by default, incl. Win+PrintScreen) and,
    /// instead of letting Windows capture every monitor, captures only the
    /// configured monitor at full quality.
    ///
    /// Also listens for a global toggle hot-key that pauses / resumes
    /// interception while the app sits minimized in the tray.
    /// </summary>
    internal sealed class HotkeyHook : IDisposable
    {
        private IntPtr _hook = IntPtr.Zero;
        private LowLevelKeyboardProc? _proc; // kept alive so it is not collected
        private readonly Func<AppSettings> _settings;
        private volatile bool _paused;

        public bool IsActive => _hook != IntPtr.Zero;
        public bool IsPaused => _paused;

        /// <summary>Raised on the UI thread when the global toggle flips the paused state.</summary>
        public event Action<bool>? PausedChanged;

        public HotkeyHook(Func<AppSettings> settings)
        {
            _settings = settings;
        }

        public void Start()
        {
            if (_hook != IntPtr.Zero)
                return;

            _paused = false;
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
            _paused = false;
        }

        public void SetPaused(bool paused)
        {
            if (_paused == paused) return;
            _paused = paused;
            PausedChanged?.Invoke(_paused);
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int message = wParam.ToInt32();
                if (message == WM_KEYDOWN || message == WM_SYSKEYDOWN)
                {
                    var info = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                    uint vk = info.vkCode;
                    AppSettings s = _settings();

                    // 1) Global toggle works even while paused.
                    if (Matches(s.ToggleHotkey, vk))
                    {
                        SetPaused(!_paused);
                        return (IntPtr)1; // swallow the toggle combo
                    }

                    if (!_paused)
                    {
                        bool winDown =
                            Down(VK_LWIN) || Down(VK_RWIN);

                        // 2) Win + PrintScreen (the native dual-monitor combo) — always
                        //    grabbed when enabled, regardless of the custom capture key.
                        if (vk == VK_SNAPSHOT && winDown && s.InterceptWinPrintScreen)
                        {
                            Task.Run(() => CaptureController.DoCapture(s));
                            return (IntPtr)1;
                        }

                        // 3) The user-configured capture hot-key.
                        if (Matches(s.CaptureHotkey, vk))
                        {
                            Task.Run(() => CaptureController.DoCapture(s));
                            return (IntPtr)1; // swallow -> prevents the all-monitor capture
                        }
                    }
                }
            }
            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        private static bool Matches(Hotkey? hk, uint vk)
        {
            if (hk == null || !hk.IsSet) return false;
            if (vk != hk.Vk) return false;

            bool ctrl = Down(VK_CONTROL);
            bool alt = Down(VK_MENU);
            bool shift = Down(VK_SHIFT);
            bool win = Down(VK_LWIN) || Down(VK_RWIN);

            return ctrl == hk.Ctrl && alt == hk.Alt && shift == hk.Shift && win == hk.Win;
        }

        private static bool Down(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

        public void Dispose() => Stop();
    }
}

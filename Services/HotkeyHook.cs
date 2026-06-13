using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using static TwoTo1Screen.Services.Native;

namespace TwoTo1Screen.Services
{
    /// <summary>
    /// Global low-level keyboard hook. Intercepts PrintScreen (single-monitor
    /// capture), optionally Win+D (single-monitor minimize) and any user-defined
    /// action hotkeys.
    /// </summary>
    internal sealed class HotkeyHook : IDisposable
    {
        private IntPtr _hook = IntPtr.Zero;
        private LowLevelKeyboardProc? _proc;
        private readonly Func<AppSettings> _settings;
        private readonly Dictionary<string, DateTime> _lastFire = new();

        public bool IsActive => _hook != IntPtr.Zero;

        /// <summary>Invoked (on the UI dispatcher) when an action hotkey fires.</summary>
        public Action<string>? OnAction;

        public HotkeyHook(Func<AppSettings> settings)
        {
            _settings = settings;
        }

        public void Start()
        {
            if (_hook != IntPtr.Zero) return;
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

        private bool Debounce(string id, int ms = 500)
        {
            var now = DateTime.UtcNow;
            if (_lastFire.TryGetValue(id, out var t) && (now - t).TotalMilliseconds < ms)
                return false;
            _lastFire[id] = now;
            return true;
        }

        private static bool Down(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

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

                    bool ctrl = Down(VK_CONTROL), alt = Down(VK_MENU), shift = Down(VK_SHIFT);
                    bool win = Down(VK_LWIN) || Down(VK_RWIN);

                    // 1) PrintScreen -> single-monitor capture
                    if (vk == VK_SNAPSHOT)
                    {
                        if (!win || s.InterceptWinPrintScreen)
                        {
                            Task.Run(() => CaptureController.DoCapture(s));
                            return (IntPtr)1;
                        }
                    }

                    // 2) Win+D -> minimize only the monitor under the cursor
                    if (s.WinDSingleMonitor && vk == VK_D && win && !ctrl && !alt && !shift)
                    {
                        if (Debounce("windd"))
                            Task.Run(() => { try { WindowManager.MinimizeMonitorUnderCursor(); } catch { } });
                        return (IntPtr)1; // swallow the default show-desktop
                    }

                    // 3) user-defined action hotkeys
                    if (vk != VK_CONTROL && vk != VK_MENU && vk != VK_SHIFT && vk != VK_LWIN && vk != VK_RWIN)
                    {
                        string? action = ActionRegistry.MatchAction(s, vk, ctrl, alt, shift, win);
                        if (action != null)
                        {
                            if (Debounce("act:" + action))
                                OnAction?.Invoke(action);
                            return (IntPtr)1;
                        }
                    }
                }
            }
            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        public void Dispose() => Stop();
    }
}

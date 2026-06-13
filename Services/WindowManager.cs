using System;
using System.Collections.Generic;
using static TwoTo1Screen.Services.Native;

namespace TwoTo1Screen.Services
{
    /// <summary>
    /// Minimizes only the windows located on a single monitor (the one under the
    /// cursor) — used to make Win+D affect just one screen instead of all.
    /// </summary>
    internal static class WindowManager
    {
        public static void MinimizeMonitorUnderCursor()
        {
            IntPtr targetMon;
            if (GetCursorPos(out POINT pt))
                targetMon = MonitorFromPoint(pt, MONITOR_DEFAULTTONEAREST);
            else
                return;

            IntPtr shell = GetShellWindow();
            var toMinimize = new List<IntPtr>();

            EnumWindows((hWnd, _) =>
            {
                if (hWnd == shell) return true;
                if (!IsWindowVisible(hWnd)) return true;
                if (IsIconic(hWnd)) return true;

                // top-level only (skip child windows)
                long style = GetWindowLongPtr(hWnd, GWL_STYLE).ToInt64();
                if ((style & WS_CHILD) != 0) return true;

                // skip tool windows with no title
                if (GetWindowTextLength(hWnd) == 0) return true;

                // owned-root: ensure we operate on the real top-level window
                IntPtr root = GetAncestor(hWnd, GA_ROOTOWNER);
                if (root != IntPtr.Zero && root != hWnd) return true;

                IntPtr mon = MonitorFromWindow(hWnd, MONITOR_DEFAULTTONEAREST);
                if (mon == targetMon)
                    toMinimize.Add(hWnd);
                return true;
            }, IntPtr.Zero);

            foreach (var h in toMinimize)
            {
                try { ShowWindowAsync(h, SW_MINIMIZE); } catch { }
            }
        }
    }
}

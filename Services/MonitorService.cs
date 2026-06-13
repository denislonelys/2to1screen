using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using static TwoTo1Screen.Services.Native;

namespace TwoTo1Screen.Services
{
    /// <summary>A single physical monitor with its pixel bounds.</summary>
    public class MonitorEntry
    {
        public string Device { get; set; } = "";
        public int Left { get; set; }
        public int Top { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool Primary { get; set; }
        public int Index { get; set; }

        public string Title => $"Монитор {Index + 1}{(Primary ? "  •  основной" : "")}";
        public string Sub => $"{Width} × {Height}";
        public string DisplayName => $"{Title}   ({Width}×{Height})";

        public override string ToString() => DisplayName;
    }

    internal static class MonitorService
    {
        public static List<MonitorEntry> GetMonitors()
        {
            var list = new List<MonitorEntry>();
            int index = 0;

            MonitorEnumProc callback = (IntPtr hMonitor, IntPtr hdc, ref RECT rect, IntPtr data) =>
            {
                var mi = new MONITORINFOEX
                {
                    cbSize = Marshal.SizeOf<MONITORINFOEX>(),
                    szDevice = string.Empty
                };

                if (GetMonitorInfo(hMonitor, ref mi))
                {
                    list.Add(new MonitorEntry
                    {
                        Device = mi.szDevice ?? string.Empty,
                        Left = mi.rcMonitor.left,
                        Top = mi.rcMonitor.top,
                        Width = mi.rcMonitor.right - mi.rcMonitor.left,
                        Height = mi.rcMonitor.bottom - mi.rcMonitor.top,
                        Primary = (mi.dwFlags & MONITORINFOF_PRIMARY) != 0,
                        Index = index
                    });
                    index++;
                }
                return true;
            };

            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
            GC.KeepAlive(callback);
            return list;
        }

        /// <summary>Resolve the monitor to capture based on the saved settings, falling back to primary.</summary>
        public static MonitorEntry Resolve(AppSettings s, List<MonitorEntry> monitors)
        {
            if (monitors.Count == 0)
                throw new InvalidOperationException("Мониторы не найдены.");

            foreach (var m in monitors)
                if (!string.IsNullOrEmpty(s.MonitorDevice) && m.Device == s.MonitorDevice)
                    return m;

            if (s.MonitorIndex >= 0 && s.MonitorIndex < monitors.Count)
                return monitors[s.MonitorIndex];

            foreach (var m in monitors)
                if (m.Primary)
                    return m;

            return monitors[0];
        }
    }
}

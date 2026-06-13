using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using static TwoTo1Screen.Services.Native;

namespace TwoTo1Screen.Services
{
    /// <summary>Applies / removes the silver acrylic "Liquid Glass" backdrop and rounded corners.</summary>
    internal static class Glass
    {
        // Silver tint in 0xAABBGGRR. A ~0x8C alpha gives a translucent silver wash
        // that lets the wallpaper show through while still reading as glass.
        public const uint SilverTint = 0x8CD8D2C8;

        public static void Enable(Window window) => Enable(window, SilverTint);

        public static void Enable(Window window, uint tint)
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;
            Enable(hwnd, tint);
            RoundCorners(hwnd);
        }

        public static void Enable(IntPtr hwnd) => Enable(hwnd, SilverTint);

        public static void Enable(IntPtr hwnd, uint tint)
        {
            ApplyAccent(hwnd, AccentStateEnum.ACCENT_ENABLE_ACRYLICBLURBEHIND, tint);
        }

        public static void Disable(Window window)
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;
            ApplyAccent(hwnd, AccentStateEnum.ACCENT_DISABLED, 0);
            RoundCorners(hwnd);
        }

        public static void RoundCorners(IntPtr hwnd)
        {
            try
            {
                int pref = DWMWCP_ROUND;
                DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int));
            }
            catch { }
        }

        private static void ApplyAccent(IntPtr hwnd, AccentStateEnum state, uint tint)
        {
            var accent = new AccentPolicy
            {
                AccentState = (int)state,
                AccentFlags = state == AccentStateEnum.ACCENT_ENABLE_ACRYLICBLURBEHIND ? 2 : 0,
                GradientColor = unchecked((int)tint),
                AnimationId = 0
            };

            int size = Marshal.SizeOf(accent);
            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(accent, ptr, false);
                var data = new WindowCompositionAttributeData
                {
                    Attribute = WCA_ACCENT_POLICY,
                    Data = ptr,
                    SizeOfData = size
                };
                SetWindowCompositionAttribute(hwnd, ref data);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }
    }
}

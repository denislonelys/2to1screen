using System;
using static TwoTo1Screen.Services.Native;

namespace TwoTo1Screen.Services
{
    /// <summary>Applies / removes the acrylic "Liquid Glass" backdrop and rounded corners.</summary>
    internal static class Glass
    {
        /// <summary>Enable acrylic blur-behind with the given tint (0xAABBGGRR).</summary>
        public static void EnableAcrylic(IntPtr hwnd, uint tint)
        {
            if (hwnd == IntPtr.Zero) return;
            ApplyAccent(hwnd, AccentStateEnum.ACCENT_ENABLE_ACRYLICBLURBEHIND, tint);
        }

        public static void Disable(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;
            ApplyAccent(hwnd, AccentStateEnum.ACCENT_DISABLED, 0);
        }

        public static void RoundCorners(IntPtr hwnd)
        {
            try
            {
                if (hwnd == IntPtr.Zero) return;
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

            int size = System.Runtime.InteropServices.Marshal.SizeOf(accent);
            IntPtr ptr = System.Runtime.InteropServices.Marshal.AllocHGlobal(size);
            try
            {
                System.Runtime.InteropServices.Marshal.StructureToPtr(accent, ptr, false);
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
                System.Runtime.InteropServices.Marshal.FreeHGlobal(ptr);
            }
        }
    }
}

using System;
using Microsoft.Win32;

namespace TwoTo1Screen.Services
{
    /// <summary>Detects whether the host OS is Windows 11 (build >= 22000).</summary>
    internal static class WindowsCheck
    {
        public static bool IsWindows11()
        {
            // Registry build number is the most reliable signal and is not
            // affected by the application compatibility manifest.
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
                if (key != null)
                {
                    var raw = key.GetValue("CurrentBuildNumber")?.ToString();
                    if (int.TryParse(raw, out int build) && build >= 22000)
                        return true;

                    // Some builds also expose CurrentBuild
                    var raw2 = key.GetValue("CurrentBuild")?.ToString();
                    if (int.TryParse(raw2, out int build2) && build2 >= 22000)
                        return true;
                }
            }
            catch
            {
                // ignore and fall back to Environment.OSVersion
            }

            var v = Environment.OSVersion.Version;
            return v.Major > 10 || (v.Major == 10 && v.Build >= 22000);
        }
    }
}

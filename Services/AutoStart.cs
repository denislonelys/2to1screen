using System;
using System.Diagnostics;

namespace TwoTo1Screen.Services
{
    /// <summary>
    /// Manages a Task Scheduler entry that launches the app in background
    /// mode at logon with the highest privileges (administrator). Creating
    /// and deleting the task requires elevation, which is requested via a UAC
    /// prompt (ShellExecute "runas").
    /// </summary>
    internal static class AutoStart
    {
        public const string TaskName = "2to1screen_autostart";

        public static string ExePath
        {
            get
            {
                try { return Process.GetCurrentProcess().MainModule?.FileName ?? ""; }
                catch { return Environment.ProcessPath ?? ""; }
            }
        }

        public static bool Enable()
        {
            string exe = ExePath;
            if (string.IsNullOrEmpty(exe))
                return false;

            // /RL HIGHEST -> run with highest privileges, /SC ONLOGON -> at logon.
            string args =
                $"/Create /TN \"{TaskName}\" /TR \"\\\"{exe}\\\" --background\" /SC ONLOGON /RL HIGHEST /F";
            return RunElevated("schtasks.exe", args);
        }

        public static bool Disable()
        {
            string args = $"/Delete /TN \"{TaskName}\" /F";
            return RunElevated("schtasks.exe", args);
        }

        public static bool Exists()
        {
            try
            {
                var psi = new ProcessStartInfo("schtasks.exe", $"/Query /TN \"{TaskName}\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using var p = Process.Start(psi);
                if (p == null) return false;
                p.WaitForExit(4000);
                return p.HasExited && p.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool RunElevated(string file, string args)
        {
            try
            {
                var psi = new ProcessStartInfo(file, args)
                {
                    UseShellExecute = true,
                    Verb = "runas", // triggers the UAC elevation prompt
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using var p = Process.Start(psi);
                if (p == null) return false;
                p.WaitForExit(20000);
                return p.HasExited && p.ExitCode == 0;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // user cancelled the UAC prompt
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}

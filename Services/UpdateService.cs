using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace TwoTo1Screen.Services
{
    /// <summary>
    /// Forced auto-update: compares the local version with the server's latest,
    /// downloads the new self-contained .exe from orproject.ru and swaps it in
    /// via a tiny helper batch that waits for this process to exit.
    /// </summary>
    internal static class UpdateService
    {
        public static bool UpdateAvailable(LicenseInfo info)
        {
            if (info == null || string.IsNullOrWhiteSpace(info.LatestVersion)) return false;
            return AppInfo.Compare(info.LatestVersion, AppInfo.Version) > 0;
        }

        /// <summary>Downloads the new exe (reporting 0..1 progress) and launches the swap helper.</summary>
        public static async Task DownloadAndApplyAsync(string downloadUrl, Action<double>? onProgress, CancellationToken ct)
        {
            string tmp = Path.Combine(Path.GetTempPath(), "2to1screen_update.exe");
            using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) })
            using (var resp = await http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                resp.EnsureSuccessStatusCode();
                long? total = resp.Content.Headers.ContentLength;
                using var src = await resp.Content.ReadAsStreamAsync(ct);
                using var dst = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None);
                var buf = new byte[81920];
                long read = 0; int n;
                while ((n = await src.ReadAsync(buf, 0, buf.Length, ct)) > 0)
                {
                    await dst.WriteAsync(buf, 0, n, ct);
                    read += n;
                    if (total.HasValue && total.Value > 0)
                        onProgress?.Invoke(Math.Min(1.0, (double)read / total.Value));
                }
            }
            ApplyAndRestart(tmp);
        }

        private static void ApplyAndRestart(string newExe)
        {
            string current = CurrentExePath();
            if (string.IsNullOrEmpty(current)) throw new Exception("cannot resolve current exe path");

            string bat = Path.Combine(Path.GetTempPath(), "2to1screen_update.cmd");
            string script =
                "@echo off\r\n" +
                "setlocal\r\n" +
                $"set \"SRC={newExe}\"\r\n" +
                $"set \"DST={current}\"\r\n" +
                "set /a TRIES=0\r\n" +
                ":copyloop\r\n" +
                "set /a TRIES+=1\r\n" +
                "copy /Y \"%SRC%\" \"%DST%\" >nul 2>&1\r\n" +
                "if errorlevel 1 (\r\n" +
                "  if %TRIES% GEQ 60 goto giveup\r\n" +
                "  ping -n 2 127.0.0.1 >nul\r\n" +
                "  goto copyloop\r\n" +
                ")\r\n" +
                "del /Q \"%SRC%\" >nul 2>&1\r\n" +
                "start \"\" \"%DST%\"\r\n" +
                ":giveup\r\n" +
                "del /Q \"%~f0\" >nul 2>&1\r\n";
            File.WriteAllText(bat, script);

            var psi = new ProcessStartInfo("cmd.exe", $"/c \"{bat}\"")
            {
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            Process.Start(psi);
        }

        private static string CurrentExePath()
        {
            try { return Process.GetCurrentProcess().MainModule?.FileName ?? Environment.ProcessPath ?? ""; }
            catch { return Environment.ProcessPath ?? ""; }
        }
    }
}

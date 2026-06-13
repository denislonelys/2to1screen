using System;
using System.Management;
using System.Security.Cryptography;
using System.Text;

namespace TwoTo1Screen.Services
{
    /// <summary>
    /// Computes a stable hardware id. It is based on hardware serial numbers
    /// (CPU, motherboard, disk, BIOS) so it survives both an app reinstall and
    /// a Windows reinstall — preventing trial/licence abuse. The result is a
    /// short opaque hex string suitable for the licensing server.
    /// </summary>
    internal static class Hwid
    {
        private static string? _cached;

        public static string Get()
        {
            if (_cached != null) return _cached;

            var parts = new[]
            {
                Wmi("Win32_Processor", "ProcessorId"),
                Wmi("Win32_BaseBoard", "SerialNumber"),
                Wmi("Win32_BIOS", "SerialNumber"),
                Wmi("Win32_DiskDrive", "SerialNumber"),
                MachineGuid(),
            };

            var raw = string.Join("|", parts);
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes("2to1screen::" + raw));

            var sb = new StringBuilder();
            // 24 hex chars = 96 bits, plenty and within the server's [8..128] limit.
            for (int i = 0; i < 12; i++) sb.Append(hash[i].ToString("x2"));
            _cached = sb.ToString();
            return _cached;
        }

        private static string Wmi(string cls, string prop)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher($"SELECT {prop} FROM {cls}");
                foreach (ManagementObject mo in searcher.Get())
                {
                    var v = mo[prop]?.ToString();
                    if (!string.IsNullOrWhiteSpace(v) &&
                        !v.Equals("To be filled by O.E.M.", StringComparison.OrdinalIgnoreCase) &&
                        !v.Equals("Default string", StringComparison.OrdinalIgnoreCase) &&
                        !v.Equals("None", StringComparison.OrdinalIgnoreCase))
                    {
                        return v.Trim();
                    }
                }
            }
            catch { }
            return "";
        }

        private static string MachineGuid()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Cryptography");
                return key?.GetValue("MachineGuid")?.ToString() ?? "";
            }
            catch { return ""; }
        }
    }
}

using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TwoTo1Screen.Services
{
    public enum LicenseStatus
    {
        Unknown = 0,    // не удалось определить (нет сети и кэша)
        None,           // нет ключа, триал не использован
        Trial,          // активен пробный период
        TrialExpired,   // пробный период закончился
        Licensed,       // активирован ключ
        Expired,        // ключ истёк
        Blocked,        // HWID заблокирован
    }

    public sealed class LicenseInfo
    {
        public LicenseStatus Status { get; set; } = LicenseStatus.Unknown;
        public long ExpiresAt { get; set; }        // ms epoch; 0 = lifetime; -1 = n/a
        public long TrialExpiresAt { get; set; }   // ms epoch; -1 = n/a
        public string LatestVersion { get; set; } = "";
        public string MinVersion { get; set; } = "";
        public string DownloadUrl { get; set; } = "https://orproject.ru/2to1screen.exe";
        public bool Mandatory { get; set; } = true;
        public bool Offline { get; set; }
        public string? Error { get; set; }          // reason for activate/trial failure

        public bool FullAccess => Status == LicenseStatus.Licensed;
        public bool BasicAccess => Status == LicenseStatus.Licensed || Status == LicenseStatus.Trial;
        public bool Lifetime => Status == LicenseStatus.Licensed && ExpiresAt == 0;

        public DateTimeOffset? ExpiresOn =>
            ExpiresAt > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(ExpiresAt) : (DateTimeOffset?)null;
        public DateTimeOffset? TrialExpiresOn =>
            TrialExpiresAt > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(TrialExpiresAt) : (DateTimeOffset?)null;
    }

    /// <summary>
    /// Talks to the licensing server at orproject.ru, verifies HMAC-signed
    /// responses, gates features and caches the last good state for offline use.
    /// </summary>
    public static class LicenseService
    {
        // Must match the server's SECRET (orproject backend .env).
        private const string Secret = "d2d8804813ed7bba0255c92d0d3ebeeae9615117619b0189fb5b51de45ea7fdf";
        private const string BaseUrl = "https://orproject.ru/2to1";
        private const long OfflineGraceMs = 14L * 24 * 60 * 60 * 1000;

        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };

        public static LicenseInfo Current { get; private set; } = new LicenseInfo();

        private static string CachePath =>
            Path.Combine(AppSettings.Dir, "license.dat");

        public static string Hardware => Hwid.Get();

        public static LicenseInfo Check()
        {
            try
            {
                var doc = Post("/check", null);
                var info = Parse(doc);
                SaveCache(_lastEnvelope!);
                Current = info;
                return info;
            }
            catch
            {
                var cached = LoadFromCache();
                Current = cached;
                return cached;
            }
        }

        public static LicenseInfo Activate(string key)
        {
            try
            {
                var doc = Post("/activate", $"\"key\":{JsonSerializer.Serialize(key)}");
                var info = Parse(doc);
                if (info.Status != LicenseStatus.Unknown && info.Error == null)
                    SaveCache(_lastEnvelope!);
                Current = info.Error == null ? info : Current;
                return info;
            }
            catch
            {
                return new LicenseInfo { Status = LicenseStatus.Unknown, Offline = true, Error = "network" };
            }
        }

        public static LicenseInfo StartTrial()
        {
            try
            {
                var doc = Post("/trial", null);
                var info = Parse(doc);
                if (info.Error == null) { SaveCache(_lastEnvelope!); Current = info; }
                return info;
            }
            catch
            {
                return new LicenseInfo { Status = LicenseStatus.Unknown, Offline = true, Error = "network" };
            }
        }

        // ---- internal -------------------------------------------------------------
        private static string? _lastEnvelope;

        private static JsonElement Post(string path, string? extraField)
        {
            string hwid = Hardware;
            string nonce = Guid.NewGuid().ToString("N");
            string os = SafeOs();
            var sb = new StringBuilder();
            sb.Append('{');
            sb.Append($"\"hwid\":{JsonSerializer.Serialize(hwid)},");
            sb.Append($"\"nonce\":{JsonSerializer.Serialize(nonce)},");
            sb.Append($"\"version\":{JsonSerializer.Serialize(AppInfo.Version)},");
            sb.Append($"\"os\":{JsonSerializer.Serialize(os)}");
            if (extraField != null) sb.Append("," + extraField);
            sb.Append('}');

            using var content = new StringContent(sb.ToString(), Encoding.UTF8, "application/json");
            using var resp = Http.PostAsync(BaseUrl + path, content).GetAwaiter().GetResult();
            resp.EnsureSuccessStatusCode();
            string body = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            using var outer = JsonDocument.Parse(body);
            string payload = outer.RootElement.GetProperty("payload").GetString() ?? throw new Exception("no payload");
            string sig = outer.RootElement.GetProperty("sig").GetString() ?? "";
            if (!VerifySig(payload, sig)) throw new Exception("bad signature");

            // confirm nonce echo to defeat replay
            using var inner = JsonDocument.Parse(payload);
            var root = inner.RootElement;
            if (root.TryGetProperty("nonce", out var n) && n.ValueKind == JsonValueKind.String && n.GetString() != nonce)
                throw new Exception("nonce mismatch");

            _lastEnvelope = body;
            // return a detached clone of the inner element
            return JsonDocument.Parse(payload).RootElement.Clone();
        }

        private static bool VerifySig(string payload, string sig)
        {
            using var h = new HMACSHA256(Encoding.UTF8.GetBytes(Secret));
            var mac = h.ComputeHash(Encoding.UTF8.GetBytes(payload));
            var sb = new StringBuilder();
            foreach (var b in mac) sb.Append(b.ToString("x2"));
            return CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(sb.ToString()), Encoding.ASCII.GetBytes(sig ?? ""));
        }

        private static LicenseInfo Parse(JsonElement root)
        {
            var info = new LicenseInfo();
            string status = GetStr(root, "status");

            // version info is always present
            info.LatestVersion = GetStr(root, "latestVersion");
            info.MinVersion = GetStr(root, "minVersion");
            info.DownloadUrl = string.IsNullOrEmpty(GetStr(root, "downloadUrl")) ? info.DownloadUrl : GetStr(root, "downloadUrl");
            if (root.TryGetProperty("mandatory", out var m)) info.Mandatory = m.ValueKind == JsonValueKind.True;

            info.ExpiresAt = GetLong(root, "expiresAt", -1);
            info.TrialExpiresAt = GetLong(root, "trialExpiresAt", -1);

            switch (status)
            {
                case "blocked": info.Status = LicenseStatus.Blocked; break;
                case "licensed": info.Status = LicenseStatus.Licensed; break;
                case "expired": info.Status = LicenseStatus.Expired; break;
                case "trial": info.Status = LicenseStatus.Trial; break;
                case "trial_expired": info.Status = LicenseStatus.TrialExpired; break;
                case "none": info.Status = LicenseStatus.None; break;
                case "activate_failed":
                case "trial_failed":
                    info.Status = Current.Status; // unchanged
                    info.Error = GetStr(root, "reason");
                    break;
                default: info.Status = LicenseStatus.None; break;
            }
            return info;
        }

        private static LicenseInfo LoadFromCache()
        {
            try
            {
                if (!File.Exists(CachePath)) return new LicenseInfo { Status = LicenseStatus.Unknown, Offline = true };
                var lines = File.ReadAllText(CachePath).Split('\n');
                long fetchedAt = long.Parse(lines[0]);
                string body = string.Join("\n", lines, 1, lines.Length - 1);
                using var outer = JsonDocument.Parse(body);
                string payload = outer.RootElement.GetProperty("payload").GetString()!;
                string sig = outer.RootElement.GetProperty("sig").GetString()!;
                if (!VerifySig(payload, sig)) return new LicenseInfo { Status = LicenseStatus.Unknown, Offline = true };

                var info = Parse(JsonDocument.Parse(payload).RootElement.Clone());
                info.Offline = true;

                long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                if (now - fetchedAt > OfflineGraceMs)
                    return new LicenseInfo { Status = LicenseStatus.Unknown, Offline = true, LatestVersion = info.LatestVersion, DownloadUrl = info.DownloadUrl };

                // re-evaluate expiry locally
                if (info.Status == LicenseStatus.Licensed && info.ExpiresAt > 0 && info.ExpiresAt < now)
                    info.Status = LicenseStatus.Expired;
                if (info.Status == LicenseStatus.Trial && info.TrialExpiresAt > 0 && info.TrialExpiresAt < now)
                    info.Status = LicenseStatus.TrialExpired;
                return info;
            }
            catch
            {
                return new LicenseInfo { Status = LicenseStatus.Unknown, Offline = true };
            }
        }

        private static void SaveCache(string envelope)
        {
            try
            {
                Directory.CreateDirectory(AppSettings.Dir);
                long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                File.WriteAllText(CachePath, now + "\n" + envelope);
            }
            catch { }
        }

        private static string GetStr(JsonElement e, string p) =>
            e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? (v.GetString() ?? "") : "";

        private static long GetLong(JsonElement e, string p, long def)
        {
            if (e.TryGetProperty(p, out var v))
            {
                if (v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var l)) return l;
                if (v.ValueKind == JsonValueKind.Null) return def;
            }
            return def;
        }

        private static string SafeOs()
        {
            try { return Environment.OSVersion.VersionString; } catch { return "Windows"; }
        }
    }
}

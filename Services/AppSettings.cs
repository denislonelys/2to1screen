using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TwoTo1Screen.Services
{
    public enum QualityPreset
    {
        VeryHigh = 0,
        High = 1,
        Medium = 2,
        Low = 3,
        Custom = 4
    }

    public enum SaveFormat
    {
        Png = 0,
        Jpeg = 1
    }

    /// <summary>A user-defined theme: "easy" (background image + accent) or "advanced" (full palette code).</summary>
    public class CustomTheme
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N").Substring(0, 12);
        public string Name { get; set; } = "Моя тема";
        public string Type { get; set; } = "easy"; // easy | advanced
        public string Mode { get; set; } = "dark";
        public string Bg { get; set; } = "#14161B";
        public string Bg2 { get; set; } = "#0F1116";
        public string Surface { get; set; } = "#1C1F26";
        public string Accent { get; set; } = "#CFE0F2";
        public string Accent2 { get; set; } = "#9FB6D6";
        public string Text { get; set; } = "#F2F5FA";
        public string TextMuted { get; set; } = "#8893A2";
        public string BgImageUrl { get; set; } = "";
    }

    /// <summary>Persisted user settings (stored as JSON in %AppData%/2to1screen).</summary>
    public class AppSettings
    {
        public string MonitorDevice { get; set; } = "";
        public int MonitorIndex { get; set; } = 0;

        public QualityPreset Quality { get; set; } = QualityPreset.VeryHigh;
        public int CustomWidth { get; set; } = 0;
        public int CustomHeight { get; set; } = 0;
        public SaveFormat CustomFormat { get; set; } = SaveFormat.Png;
        public int CustomJpegQuality { get; set; } = 92;

        public bool SaveToClipboard { get; set; } = true;
        public bool SaveToFile { get; set; } = true;
        public string SaveFolder { get; set; } = "";

        public bool InterceptWinPrintScreen { get; set; } = true;
        public bool ShutterSound { get; set; } = true;
        public bool ShowNotification { get; set; } = true;

        // ---- Theme / Liquid Glass -------------------------------------------------
        /// <summary>By default Liquid Glass is OFF and the standard black theme is used.</summary>
        public bool LiquidGlassEnabled { get; set; } = false;

        /// <summary>0 = непрозрачно, 100 = максимально прозрачно (видно обои).</summary>
        public int GlassTransparency { get; set; } = 45;

        /// <summary>Active theme id from the catalog/custom list. "builtin-black" is the default.</summary>
        public string ActiveThemeId { get; set; } = "builtin-black";

        public List<CustomTheme> CustomThemes { get; set; } = new List<CustomTheme>();

        public bool AutoStart { get; set; } = false;

        // ---- Window behaviour -----------------------------------------------------
        /// <summary>Win+D сворачивает окна только на одном мониторе (под курсором).</summary>
        public bool WinDSingleMonitor { get; set; } = false;

        // ---- Hotkeys (actionId -> combo string, e.g. "Ctrl+Alt+S") ----------------
        public Dictionary<string, string> Hotkeys { get; set; } = new Dictionary<string, string>();

        [JsonIgnore]
        public static string Dir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "2to1screen");

        [JsonIgnore]
        public static string FilePath => Path.Combine(Dir, "settings.json");

        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var json = File.ReadAllText(FilePath);
                    var s = JsonSerializer.Deserialize<AppSettings>(json, Options);
                    if (s != null)
                    {
                        s.EnsureDefaults();
                        return s;
                    }
                }
            }
            catch
            {
                // corrupt file -> start fresh
            }

            var def = new AppSettings();
            def.EnsureDefaults();
            return def;
        }

        public void EnsureDefaults()
        {
            if (string.IsNullOrWhiteSpace(SaveFolder))
            {
                SaveFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "2to1screen");
            }
            if (CustomJpegQuality < 1 || CustomJpegQuality > 100)
                CustomJpegQuality = 92;
            if (GlassTransparency < 0 || GlassTransparency > 100)
                GlassTransparency = 45;
            Hotkeys ??= new Dictionary<string, string>();
            CustomThemes ??= new List<CustomTheme>();
            if (string.IsNullOrWhiteSpace(ActiveThemeId))
                ActiveThemeId = "builtin-black";
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                var json = JsonSerializer.Serialize(this, Options);
                File.WriteAllText(FilePath, json);
            }
            catch
            {
                // non-fatal
            }
        }
    }
}

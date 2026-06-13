using System;
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

    /// <summary>Persisted user settings (stored as JSON in %AppData%/2to1screen).</summary>
    public class AppSettings
    {
        public string MonitorDevice { get; set; } = "";
        public int MonitorIndex { get; set; } = 0;

        public QualityPreset Quality { get; set; } = QualityPreset.VeryHigh;
        public int CustomWidth { get; set; } = 0;   // 0 => native width
        public int CustomHeight { get; set; } = 0;  // 0 => native height
        public SaveFormat CustomFormat { get; set; } = SaveFormat.Png;
        public int CustomJpegQuality { get; set; } = 92;

        public bool SaveToClipboard { get; set; } = true;
        public bool SaveToFile { get; set; } = true;
        public string SaveFolder { get; set; } = "";

        public bool InterceptWinPrintScreen { get; set; } = true;
        public bool ShutterSound { get; set; } = true;
        public bool ShowNotification { get; set; } = true;

        public bool LiquidGlassDisabled { get; set; } = false;
        public bool AutoStart { get; set; } = false;

        // ---- Appearance (v1.1) ----
        /// <summary>Primary accent color of the Liquid Glass theme (hex #RRGGBB).</summary>
        public string LiquidGlassAccent { get; set; } = "#CFE0F2";
        /// <summary>Selected store theme id, used when Liquid Glass is disabled.</summary>
        public string ThemeId { get; set; } = "midnight";

        // ---- Hotkeys (v1.1) ----
        /// <summary>Key combination that triggers a single-monitor capture.</summary>
        public Hotkey CaptureHotkey { get; set; } = Hotkey.PrintScreen();
        /// <summary>Global key combination that pauses / resumes interception (works from tray).</summary>
        public Hotkey ToggleHotkey { get; set; } = new Hotkey(0x2C, ctrl: true, alt: true); // Ctrl+Alt+PrtSc

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

            if (CaptureHotkey == null || !CaptureHotkey.IsSet)
                CaptureHotkey = Hotkey.PrintScreen();
            if (ToggleHotkey == null)
                ToggleHotkey = new Hotkey();
            if (string.IsNullOrWhiteSpace(LiquidGlassAccent))
                LiquidGlassAccent = "#CFE0F2";
            if (string.IsNullOrWhiteSpace(ThemeId))
                ThemeId = "midnight";
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

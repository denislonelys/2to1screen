using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;

namespace TwoTo1Screen.Services
{
    /// <summary>A resolved theme palette (built-in, catalog or custom).</summary>
    public sealed class ThemePalette
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Author { get; set; } = "";
        public string Source { get; set; } = "builtin";
        public string Mode { get; set; } = "dark";
        public string Bg { get; set; } = "#15181D";
        public string Bg2 { get; set; } = "#0F1116";
        public string Surface { get; set; } = "#1C2026";
        public string Accent { get; set; } = "#CFE0F2";
        public string Accent2 { get; set; } = "#9FB6D6";
        public string Text { get; set; } = "#F2F5FA";
        public string TextMuted { get; set; } = "#8893A2";
        public string BgImageUrl { get; set; } = "";

        public bool IsDark => !string.Equals(Mode, "light", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Applies palettes to the live application resource dictionary, manages the
    /// Liquid Glass acrylic backdrop with a transparency level, and loads the
    /// 500+ theme catalog from orproject.ru (with on-disk cache).
    /// </summary>
    public static class ThemeService
    {
        private const string CatalogUrl = "https://orproject.ru/2to1/themes";
        private static List<ThemePalette> _catalog = new List<ThemePalette>();

        public static IReadOnlyList<ThemePalette> Catalog => _catalog;

        public static readonly ThemePalette BuiltinBlack = new ThemePalette
        {
            Id = "builtin-black", Name = "Чёрная (стандартная)", Source = "builtin", Mode = "dark",
            Bg = "#15181D", Bg2 = "#0E1014", Surface = "#1C2026",
            Accent = "#CFE0F2", Accent2 = "#9FB6D6", Text = "#F2F5FA", TextMuted = "#8893A2",
        };

        // ---- public API -----------------------------------------------------------
        public static ThemePalette Resolve(AppSettings s)
        {
            string id = s.ActiveThemeId ?? "builtin-black";
            if (id == "builtin-black") return BuiltinBlack;

            var custom = s.CustomThemes.FirstOrDefault(c => c.Id == id);
            if (custom != null) return FromCustom(custom);

            var cat = _catalog.FirstOrDefault(c => c.Id == id);
            if (cat != null) return cat;

            return BuiltinBlack;
        }

        /// <summary>Build all theme brushes and push them into Application.Resources (live).</summary>
        public static void ApplyResources(AppSettings s)
        {
            var p = Resolve(s);
            bool glass = s.LiquidGlassEnabled;
            int t = Math.Clamp(s.GlassTransparency, 0, 100);
            // bars/cards strength (only meaningful with Liquid Glass on)
            double fill = glass ? Math.Clamp(s.GlassFillStrength, 0, 100) / 100.0 : 1.0;

            var res = Application.Current.Resources;

            Color bg = EffectiveTint(s, p);
            Color bg2 = Hex(p.Bg2, Darken(bg, 0.04));
            Color surface = Hex(p.Surface, Lighten(bg, 0.06));
            Color accent = EffectiveAccent(s, p);
            Color accent2 = Hex(p.Accent2, Darken(accent, 0.10));
            // when the accent is overridden for glass, derive the secondary stop from it
            if (glass && !string.IsNullOrWhiteSpace(s.GlassAccentColor)) accent2 = Darken(accent, 0.12);
            Color text = Hex(p.Text, Colors.White);
            Color muted = Hex(p.TextMuted, Color.FromRgb(0x88, 0x93, 0xA2));
            bool dark = p.IsDark;

            // overlay used for cards/strokes (white on dark themes, black on light)
            Color ov = dark ? Colors.White : Color.FromRgb(0x10, 0x14, 0x1A);

            res["AccentColor"] = accent;
            res["TextPrimary"] = new SolidColorBrush(text);
            res["TextSecondary"] = new SolidColorBrush(Mix(text, bg, 0.22));
            res["TextMuted"] = new SolidColorBrush(muted);

            res["AccentSolid"] = new SolidColorBrush(accent);
            res["AccentGradient"] = VGrad(accent, accent2);
            res["AccentForeground"] = new SolidColorBrush(Luminance(accent) > 0.6 ? Color.FromRgb(0x16, 0x20, 0x2B) : Colors.White);
            res["RunningGradient"] = VGrad(Color.FromRgb(0x8B, 0xE6, 0xB4), Color.FromRgb(0x49, 0xB9, 0x87));

            res["GlassStroke"] = new SolidColorBrush(WithA(ov, FillA(0x55, fill)));
            res["GlassStrokeSoft"] = new SolidColorBrush(WithA(ov, FillA(0x2E, fill)));
            res["SwitchOff"] = new SolidColorBrush(WithA(ov, FillA(0x3A, fill)));

            res["SidebarFill"] = VGrad(WithA(ov, FillA(0x2A, fill)), WithA(ov, FillA(0x10, fill)));
            res["GlassFill"] = VGrad(WithA(ov, FillA(0x2E, fill)), WithA(ov, FillA(0x14, fill)));
            res["GlassFillHover"] = VGrad(WithA(ov, FillA(0x4C, fill)), WithA(ov, FillA(0x24, fill)));

            // ---- window backdrop -------------------------------------------------
            if (!string.IsNullOrWhiteSpace(p.BgImageUrl))
            {
                res["WindowFill"] = ImageBrushFrom(p.BgImageUrl, bg);
            }
            else if (glass)
            {
                byte a = (byte)(0xFF - t * (0xFF - 0x16) / 100); // 0 -> непрозрачно, 100 -> почти прозрачно
                res["WindowFill"] = DiagGrad(WithA(bg, a), WithA(bg2, a), WithA(surface, a));
            }
            else
            {
                res["WindowFill"] = DiagGrad(bg, bg2, Darken(bg2, 0.05));
            }
        }

        /// <summary>Enable/disable the acrylic backdrop for a window using the theme colour + transparency.</summary>
        public static void ApplyWindowGlass(Window window, AppSettings s)
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;

            var p = Resolve(s);
            if (s.LiquidGlassEnabled && string.IsNullOrWhiteSpace(p.BgImageUrl))
            {
                int t = Math.Clamp(s.GlassTransparency, 0, 100);
                Color bg = EffectiveTint(s, p);
                byte tintA = (byte)(0xFF - t * (0xFF - 0x12) / 100);
                uint tint = (uint)((tintA << 24) | (bg.B << 16) | (bg.G << 8) | bg.R); // 0xAABBGGRR
                Glass.EnableAcrylic(hwnd, tint);
            }
            else
            {
                Glass.Disable(hwnd);
            }
            Glass.RoundCorners(hwnd);
        }

        // ---- effective (override-aware) colours for Liquid Glass ------------------
        /// <summary>Accent colour honouring the Liquid Glass accent override.</summary>
        private static Color EffectiveAccent(AppSettings s, ThemePalette p)
        {
            if (s.LiquidGlassEnabled && !string.IsNullOrWhiteSpace(s.GlassAccentColor))
                return Hex(s.GlassAccentColor, Hex(p.Accent, Color.FromRgb(0xCF, 0xE0, 0xF2)));
            return Hex(p.Accent, Color.FromRgb(0xCF, 0xE0, 0xF2));
        }

        /// <summary>Backdrop tint colour honouring the Liquid Glass tint override.</summary>
        private static Color EffectiveTint(AppSettings s, ThemePalette p)
        {
            if (s.LiquidGlassEnabled && !string.IsNullOrWhiteSpace(s.GlassTintColor))
                return Hex(s.GlassTintColor, Hex(p.Bg, Color.FromRgb(0x15, 0x18, 0x1D)));
            return Hex(p.Bg, Color.FromRgb(0x15, 0x18, 0x1D));
        }

        /// <summary>Scale an overlay alpha by the bars/cards strength factor (0..1).</summary>
        private static byte FillA(int baseAlpha, double factor) =>
            (byte)Math.Clamp((int)Math.Round(baseAlpha * factor), 0, 255);

        // ---- catalog --------------------------------------------------------------
        private static string CatalogCachePath => Path.Combine(AppSettings.Dir, "themes.cache.json");

        public static void LoadCatalogCacheSync()
        {
            try
            {
                if (File.Exists(CatalogCachePath))
                    _catalog = ParseCatalog(File.ReadAllText(CatalogCachePath));
            }
            catch { }
        }

        public static async System.Threading.Tasks.Task RefreshCatalogAsync()
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                string json = await http.GetStringAsync(CatalogUrl);
                var list = ParseCatalog(json);
                if (list.Count > 0)
                {
                    _catalog = list;
                    try { Directory.CreateDirectory(AppSettings.Dir); File.WriteAllText(CatalogCachePath, json); } catch { }
                }
            }
            catch { }
        }

        private static List<ThemePalette> ParseCatalog(string json)
        {
            var list = new List<ThemePalette>();
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("themes", out var arr)) return list;
            foreach (var e in arr.EnumerateArray())
            {
                list.Add(new ThemePalette
                {
                    Id = Str(e, "id"), Name = Str(e, "name"), Author = Str(e, "author"),
                    Source = Str(e, "source", "hydra"), Mode = Str(e, "mode", "dark"),
                    Bg = Str(e, "bg", "#15181D"), Bg2 = Str(e, "bg2", "#0E1014"),
                    Surface = Str(e, "surface", "#1C2026"),
                    Accent = Str(e, "accent", "#CFE0F2"), Accent2 = Str(e, "accent2", "#9FB6D6"),
                    Text = Str(e, "text", "#F2F5FA"), TextMuted = Str(e, "textMuted", "#8893A2"),
                });
            }
            return list;
        }

        public static ThemePalette FromCustom(CustomTheme c) => new ThemePalette
        {
            Id = c.Id, Name = c.Name, Source = "custom", Mode = c.Mode,
            Bg = c.Bg, Bg2 = c.Bg2, Surface = c.Surface, Accent = c.Accent, Accent2 = c.Accent2,
            Text = c.Text, TextMuted = c.TextMuted, BgImageUrl = c.BgImageUrl,
        };

        // ---- colour helpers -------------------------------------------------------
        public static Color Hex(string s, Color fallback)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(s)) return fallback;
                s = s.Trim();
                if (!s.StartsWith("#")) s = "#" + s;
                if (s.Length == 4) // #RGB
                    s = "#" + s[1] + s[1] + s[2] + s[2] + s[3] + s[3];
                var c = (Color)ColorConverter.ConvertFromString(s);
                return c;
            }
            catch { return fallback; }
        }

        private static Color WithA(Color c, byte a) => Color.FromArgb(a, c.R, c.G, c.B);
        private static double Luminance(Color c) => (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255.0;
        private static Color Lighten(Color c, double f) => Mix(c, Colors.White, f);
        private static Color Darken(Color c, double f) => Mix(c, Colors.Black, f);
        private static Color Mix(Color a, Color b, double f)
        {
            f = Math.Clamp(f, 0, 1);
            return Color.FromRgb(
                (byte)(a.R + (b.R - a.R) * f),
                (byte)(a.G + (b.G - a.G) * f),
                (byte)(a.B + (b.B - a.B) * f));
        }

        private static LinearGradientBrush VGrad(Color a, Color b)
        {
            var g = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            g.GradientStops.Add(new GradientStop(a, 0));
            g.GradientStops.Add(new GradientStop(b, 1));
            g.Freeze();
            return g;
        }

        private static LinearGradientBrush DiagGrad(Color a, Color b, Color c)
        {
            var g = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0.6, 1) };
            g.GradientStops.Add(new GradientStop(a, 0));
            g.GradientStops.Add(new GradientStop(b, 0.5));
            g.GradientStops.Add(new GradientStop(c, 1));
            g.Freeze();
            return g;
        }

        private static Brush ImageBrushFrom(string url, Color fallback)
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                bmp.UriSource = new Uri(url, UriKind.Absolute);
                bmp.EndInit();
                var br = new ImageBrush(bmp) { Stretch = Stretch.UniformToFill };
                return br;
            }
            catch { return new SolidColorBrush(fallback); }
        }

        private static string Str(JsonElement e, string p, string def = "") =>
            e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? (v.GetString() ?? def) : def;
    }
}

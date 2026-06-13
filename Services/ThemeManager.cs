using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace TwoTo1Screen.Services
{
    /// <summary>A fully-resolved set of colors that drives every themed brush.</summary>
    public sealed class Palette
    {
        public Color TextPrimary, TextSecondary, TextMuted;
        public Color GlassA, GlassB;          // card / surface fill gradient
        public Color GlassHoverA, GlassHoverB;
        public Color Stroke, StrokeSoft;
        public Color SidebarA, SidebarB;
        public Color AccentA, AccentB;        // accent gradient
        public Color Accent;                  // accent solid
        public Color AccentText;              // readable text on accent surfaces
        public Color SwitchOff;
        public Color WinTop, WinMid, WinBot;  // window backdrop gradient
        public bool Acrylic;                  // apply the live blur-behind
        public uint AcrylicTint;              // 0xAABBGGRR
    }

    /// <summary>A store theme (used when Liquid Glass is disabled).</summary>
    public sealed class ThemeDefinition
    {
        public string Id = "";
        public string Name = "";
        public string Tagline = "";
        public bool Animated;
        public Palette Palette = new Palette();
        public Color[] Blobs = Array.Empty<Color>(); // animated backdrop accents
        public Color PreviewA, PreviewB;              // store-card thumbnail gradient
    }

    /// <summary>
    /// Owns the active appearance. Either the Liquid Glass mode (acrylic backdrop
    /// tinted by a user-chosen accent) or one of the store themes. Applies the
    /// result to the application resource dictionary so every <c>DynamicResource</c>
    /// brush updates live.
    /// </summary>
    internal static class ThemeManager
    {
        public static event Action? Changed;

        private static readonly List<ThemeDefinition> _themes = BuildThemes();
        public static IReadOnlyList<ThemeDefinition> Themes => _themes;

        public static Palette CurrentPalette { get; private set; } = new Palette();
        public static bool IsGlass { get; private set; } = true;

        public static ThemeDefinition GetTheme(string id)
        {
            foreach (var t in _themes)
                if (t.Id == id) return t;
            return _themes[0];
        }

        // ---------------------------------------------------------------- apply

        /// <summary>Recompute and apply the active palette from current settings.</summary>
        public static void ApplyCurrent()
        {
            var s = App.Settings;
            Palette p;
            if (s.LiquidGlassDisabled)
            {
                IsGlass = false;
                p = GetTheme(s.ThemeId).Palette;
            }
            else
            {
                IsGlass = true;
                p = BuildGlassPalette(ParseColor(s.LiquidGlassAccent, Color.FromRgb(0xCF, 0xE0, 0xF2)));
            }

            CurrentPalette = p;
            PushToResources(p);
            Changed?.Invoke();
        }

        private static void PushToResources(Palette p)
        {
            var r = System.Windows.Application.Current?.Resources;
            if (r == null) return;
            ApplyToDictionary(r, p);
        }

        /// <summary>Write the palette into a resource dictionary (app-wide or a single window).</summary>
        public static void ApplyToDictionary(ResourceDictionary r, Palette p)
        {
            r["TextPrimary"] = Solid(p.TextPrimary);
            r["TextSecondary"] = Solid(p.TextSecondary);
            r["TextMuted"] = Solid(p.TextMuted);

            r["GlassFill"] = VGrad(p.GlassA, p.GlassB);
            r["GlassFillHover"] = VGrad(p.GlassHoverA, p.GlassHoverB);
            r["GlassStroke"] = Solid(p.Stroke);
            r["GlassStrokeSoft"] = Solid(p.StrokeSoft);
            r["SidebarFill"] = VGrad(p.SidebarA, p.SidebarB);

            r["AccentGradient"] = VGrad(p.AccentA, p.AccentB);
            r["AccentSolid"] = Solid(p.Accent);
            r["AccentColor"] = p.Accent;
            r["AccentTextBrush"] = Solid(p.AccentText);
            r["SwitchOff"] = Solid(p.SwitchOff);

            r["WindowBackdrop"] = WindowBackdropBrush(p);
        }

        public static Brush WindowBackdropBrush(Palette p)
        {
            var g = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0.6, 1) };
            g.GradientStops.Add(new GradientStop(p.WinTop, 0));
            g.GradientStops.Add(new GradientStop(p.WinMid, 0.55));
            g.GradientStops.Add(new GradientStop(p.WinBot, 1));
            g.Freeze();
            return g;
        }

        /// <summary>Apply the backdrop + acrylic + animated layer to a window.</summary>
        public static void ApplyWindowChrome(Window window, Border root, Panel? animatedHost)
        {
            var p = CurrentPalette;
            root.SetResourceReference(Border.BackgroundProperty, "WindowBackdrop");

            if (p.Acrylic)
                Glass.Enable(window, p.AcrylicTint);
            else
                Glass.Disable(window);

            if (animatedHost != null)
                BuildAnimatedBackdrop(animatedHost, IsGlass ? null : GetTheme(App.Settings.ThemeId));
        }

        // ------------------------------------------------------- animated layer

        public static void BuildAnimatedBackdrop(Panel host, ThemeDefinition? theme)
        {
            host.Children.Clear();
            if (theme == null || !theme.Animated || theme.Blobs.Length == 0)
            {
                host.Visibility = Visibility.Collapsed;
                return;
            }

            host.Visibility = Visibility.Visible;
            var canvas = new Grid { ClipToBounds = true };

            var rnd = new Random(theme.Id.GetHashCode());
            for (int i = 0; i < theme.Blobs.Length; i++)
            {
                Color c = theme.Blobs[i];
                double size = 360 + rnd.NextDouble() * 320;
                double x0 = (rnd.NextDouble() - 0.5) * 520;
                double y0 = (rnd.NextDouble() - 0.5) * 300;
                double dx = 60 + rnd.NextDouble() * 120;
                double dy = 50 + rnd.NextDouble() * 110;
                double durX = 7 + rnd.NextDouble() * 6;
                double durY = 9 + rnd.NextDouble() * 6;

                var fill = new RadialGradientBrush();
                fill.GradientStops.Add(new GradientStop(Color.FromArgb(0xC8, c.R, c.G, c.B), 0));
                fill.GradientStops.Add(new GradientStop(Color.FromArgb(0x55, c.R, c.G, c.B), 0.55));
                fill.GradientStops.Add(new GradientStop(Color.FromArgb(0x00, c.R, c.G, c.B), 1));

                var blob = new Ellipse
                {
                    Width = size,
                    Height = size,
                    Fill = fill,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    IsHitTestVisible = false
                };
                var tt = new TranslateTransform(x0, y0);
                blob.RenderTransform = tt;

                Animate(tt, TranslateTransform.XProperty, x0 - dx, x0 + dx, durX);
                Animate(tt, TranslateTransform.YProperty, y0 - dy, y0 + dy, durY);

                canvas.Children.Add(blob);
            }

            // A soft vignette keeps text readable over the moving color.
            var scrim = new Rectangle
            {
                IsHitTestVisible = false,
                Fill = new SolidColorBrush(Color.FromArgb(0x33, 0, 0, 0))
            };
            canvas.Children.Add(scrim);

            host.Children.Add(canvas);
        }

        private static void Animate(TranslateTransform t, System.Windows.DependencyProperty prop,
                                    double from, double to, double seconds)
        {
            var a = new DoubleAnimation(from, to, TimeSpan.FromSeconds(seconds))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            t.BeginAnimation(prop, a);
        }

        // -------------------------------------------------------- glass palette

        public static Palette BuildGlassPalette(Color accent)
        {
            // Liquid Glass: bright, very translucent silver-glass tinted by the
            // accent. Surfaces stay near-white so the wallpaper reads through.
            Color tintWhite = Mix(Colors.White, accent, 0.18);

            return new Palette
            {
                TextPrimary = Color.FromRgb(0xF7, 0xFA, 0xFE),
                TextSecondary = Color.FromRgb(0xDD, 0xE6, 0xF2),  // brighter than v1.0 for readability
                TextMuted = Color.FromRgb(0xB7, 0xC4, 0xD6),

                GlassA = Argb(0x2E, tintWhite),
                GlassB = Argb(0x10, tintWhite),
                GlassHoverA = Argb(0x4C, tintWhite),
                GlassHoverB = Argb(0x1E, tintWhite),
                Stroke = Argb(0x4A, Colors.White),
                StrokeSoft = Argb(0x24, Colors.White),
                SidebarA = Argb(0x28, tintWhite),
                SidebarB = Argb(0x0C, tintWhite),

                AccentA = Lighten(accent, 0.18),
                AccentB = Darken(accent, 0.10),
                Accent = accent,
                AccentText = OnColor(accent),
                SwitchOff = Argb(0x30, Colors.White),

                // markedly more transparent backdrop than v1.0.0
                WinTop = Argb(0x3A, Mix(Color.FromRgb(0xFA, 0xFB, 0xFD), accent, 0.10)),
                WinMid = Argb(0x26, Mix(Color.FromRgb(0xC9, 0xD2, 0xDE), accent, 0.14)),
                WinBot = Argb(0x34, Mix(Color.FromRgb(0x8E, 0x97, 0xA6), accent, 0.18)),

                Acrylic = true,
                AcrylicTint = Bgra(Mix(Color.FromRgb(0xD8, 0xD2, 0xC8), accent, 0.35), 0x4C)
            };
        }

        // ------------------------------------------------------------- registry

        private static List<ThemeDefinition> BuildThemes()
        {
            var list = new List<ThemeDefinition>
            {
                SolidTheme("midnight", "Midnight", "Чистая тёмная классика",
                    accent: Hex("#6EA8FE"),
                    top: Hex("#1B1F27"), mid: Hex("#13161D"), bot: Hex("#0C0E13")),

                SolidTheme("graphite", "Graphite", "Серебристый графит",
                    accent: Hex("#C9D2DE"),
                    top: Hex("#2B3038"), mid: Hex("#20242B"), bot: Hex("#15181D")),

                AnimatedTheme("aurora", "Aurora", "Северное сияние",
                    accent: Hex("#38E1B0"),
                    top: Hex("#06141C"), mid: Hex("#08161E"), bot: Hex("#040D13"),
                    blobs: new[] { Hex("#1FE0A8"), Hex("#54E38A"), Hex("#6C7CFF"), Hex("#13B89A") }),

                AnimatedTheme("synthwave", "Synthwave", "Неоновый закат 80-х",
                    accent: Hex("#FF4FD8"),
                    top: Hex("#1B0B2E"), mid: Hex("#220C3A"), bot: Hex("#120726"),
                    blobs: new[] { Hex("#FF4FD8"), Hex("#7A4DFF"), Hex("#00E0FF"), Hex("#FF7849") }),

                AnimatedTheme("ocean", "Ocean Deep", "Глубокий океан",
                    accent: Hex("#29C2F2"),
                    top: Hex("#04263B"), mid: Hex("#03192B"), bot: Hex("#020F1C"),
                    blobs: new[] { Hex("#25C2F2"), Hex("#2D6CF6"), Hex("#19E0C8"), Hex("#3A8DFF") }),

                AnimatedTheme("ember", "Ember", "Тлеющие угли",
                    accent: Hex("#FF7A3C"),
                    top: Hex("#1C0E0A"), mid: Hex("#160906"), bot: Hex("#0D0504"),
                    blobs: new[] { Hex("#FF7A3C"), Hex("#FF3B5C"), Hex("#FFB23C"), Hex("#E0452A") }),

                AnimatedTheme("forest", "Evergreen", "Вечный лес",
                    accent: Hex("#44D98A"),
                    top: Hex("#07140E"), mid: Hex("#05110B"), bot: Hex("#030C08"),
                    blobs: new[] { Hex("#3FD98A"), Hex("#9BE356"), Hex("#1FB5A0"), Hex("#2FA86A") }),

                LightTheme("pearl", "Pearl", "Светлый перламутр",
                    accent: Hex("#4C6FFF"),
                    top: Hex("#F6F8FD"), mid: Hex("#EAEFF8"), bot: Hex("#DCE4F1")),
            };
            return list;
        }

        private static ThemeDefinition SolidTheme(string id, string name, string tag,
            Color accent, Color top, Color mid, Color bot)
        {
            return new ThemeDefinition
            {
                Id = id, Name = name, Tagline = tag, Animated = false,
                Blobs = Array.Empty<Color>(),
                PreviewA = top, PreviewB = bot,
                Palette = DarkSurfacePalette(accent, top, mid, bot)
            };
        }

        private static ThemeDefinition AnimatedTheme(string id, string name, string tag,
            Color accent, Color top, Color mid, Color bot, Color[] blobs)
        {
            return new ThemeDefinition
            {
                Id = id, Name = name, Tagline = tag, Animated = true,
                Blobs = blobs,
                PreviewA = blobs.Length > 0 ? blobs[0] : top,
                PreviewB = blobs.Length > 1 ? Darken(blobs[1], 0.25) : bot,
                Palette = DarkSurfacePalette(accent, top, mid, bot)
            };
        }

        private static Palette DarkSurfacePalette(Color accent, Color top, Color mid, Color bot)
        {
            return new Palette
            {
                TextPrimary = Color.FromRgb(0xF4, 0xF7, 0xFB),
                TextSecondary = Color.FromRgb(0xCB, 0xD5, 0xE3),
                TextMuted = Color.FromRgb(0x93, 0x9F, 0xB0),

                GlassA = Argb(0x24, Colors.White),
                GlassB = Argb(0x0D, Colors.White),
                GlassHoverA = Argb(0x3A, Colors.White),
                GlassHoverB = Argb(0x18, Colors.White),
                Stroke = Argb(0x33, Colors.White),
                StrokeSoft = Argb(0x1C, Colors.White),
                SidebarA = Argb(0x22, Colors.White),
                SidebarB = Argb(0x0A, Colors.White),

                AccentA = Lighten(accent, 0.16),
                AccentB = Darken(accent, 0.12),
                Accent = accent,
                AccentText = OnColor(accent),
                SwitchOff = Argb(0x2E, Colors.White),

                WinTop = top, WinMid = mid, WinBot = bot,
                Acrylic = false,
                AcrylicTint = 0
            };
        }

        private static ThemeDefinition LightTheme(string id, string name, string tag,
            Color accent, Color top, Color mid, Color bot)
        {
            var pal = new Palette
            {
                TextPrimary = Color.FromRgb(0x1B, 0x22, 0x30),
                TextSecondary = Color.FromRgb(0x44, 0x50, 0x63),
                TextMuted = Color.FromRgb(0x6C, 0x77, 0x89),

                GlassA = Argb(0xE6, Colors.White),
                GlassB = Argb(0xC2, Colors.White),
                GlassHoverA = Argb(0xFF, Colors.White),
                GlassHoverB = Argb(0xDC, Colors.White),
                Stroke = Argb(0x33, Color.FromRgb(0x2B, 0x3A, 0x5B)),
                StrokeSoft = Argb(0x1C, Color.FromRgb(0x2B, 0x3A, 0x5B)),
                SidebarA = Argb(0xCC, Colors.White),
                SidebarB = Argb(0x99, Colors.White),

                AccentA = Lighten(accent, 0.12),
                AccentB = Darken(accent, 0.10),
                Accent = accent,
                AccentText = OnColor(accent),
                SwitchOff = Argb(0x33, Color.FromRgb(0x2B, 0x3A, 0x5B)),

                WinTop = top, WinMid = mid, WinBot = bot,
                Acrylic = false,
                AcrylicTint = 0
            };
            return new ThemeDefinition
            {
                Id = id, Name = name, Tagline = tag, Animated = false,
                Blobs = Array.Empty<Color>(),
                PreviewA = top, PreviewB = bot,
                Palette = pal
            };
        }

        // ----------------------------------------------------------- color util

        private static SolidColorBrush Solid(Color c)
        {
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        private static LinearGradientBrush VGrad(Color a, Color b)
        {
            var g = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            g.GradientStops.Add(new GradientStop(a, 0));
            g.GradientStops.Add(new GradientStop(b, 1));
            g.Freeze();
            return g;
        }

        public static Color Hex(string hex) => ParseColor(hex, Colors.Gray);

        public static Color ParseColor(string? hex, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(hex)) return fallback;
            try
            {
                var o = ColorConverter.ConvertFromString(hex);
                if (o is Color c) return c;
            }
            catch { }
            // manual #RRGGBB
            try
            {
                string h = hex.TrimStart('#');
                if (h.Length == 6)
                {
                    byte r = byte.Parse(h.Substring(0, 2), NumberStyles.HexNumber);
                    byte g = byte.Parse(h.Substring(2, 2), NumberStyles.HexNumber);
                    byte b = byte.Parse(h.Substring(4, 2), NumberStyles.HexNumber);
                    return Color.FromRgb(r, g, b);
                }
            }
            catch { }
            return fallback;
        }

        public static string ToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

        private static Color Argb(byte a, Color c) => Color.FromArgb(a, c.R, c.G, c.B);

        private static uint Bgra(Color c, byte a) =>
            (uint)((a << 24) | (c.B << 16) | (c.G << 8) | c.R);

        private static Color Mix(Color a, Color b, double t)
        {
            t = Math.Clamp(t, 0, 1);
            return Color.FromRgb(
                (byte)(a.R + (b.R - a.R) * t),
                (byte)(a.G + (b.G - a.G) * t),
                (byte)(a.B + (b.B - a.B) * t));
        }

        public static Color Lighten(Color c, double t) => Mix(c, Colors.White, t);
        public static Color Darken(Color c, double t) => Mix(c, Colors.Black, t);

        private static Color OnColor(Color c)
        {
            double lum = (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;
            return lum > 0.6 ? Color.FromRgb(0x15, 0x20, 0x2B) : Color.FromRgb(0xF6, 0xFA, 0xFF);
        }
    }
}

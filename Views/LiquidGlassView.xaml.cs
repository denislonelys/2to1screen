using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TwoTo1Screen.Services;
using WinForms = System.Windows.Forms;

namespace TwoTo1Screen.Views
{
    public partial class LiquidGlassView : UserControl
    {
        private MainWindow? _host;
        private bool _loading;

        // preset palettes
        private static readonly string[] AccentPresets =
        {
            "#CFE0F2", "#7C5CFF", "#38BDF8", "#2DD4BF", "#49B987",
            "#E8C15A", "#FF9F43", "#FF5C5C", "#FF6FB5", "#F2F5FA",
        };
        private static readonly string[] TintPresets =
        {
            "#15181D", "#0B0D12", "#101826", "#13202B", "#0E1A17",
            "#1B1020", "#241018", "#1E1412", "#101418", "#1A1D24",
        };

        public LiquidGlassView()
        {
            InitializeComponent();
            BuildSwatches();
        }

        public void Bind(MainWindow host)
        {
            _host = host;
            ReloadFromSettings();
        }

        public void ReloadFromSettings()
        {
            _loading = true;
            try
            {
                SwGlass.IsChecked = App.Settings.LiquidGlassEnabled;
                TransSlider.Value = App.Settings.GlassTransparency;
                TransVal.Text = App.Settings.GlassTransparency + "%";
                FillSlider.Value = App.Settings.GlassFillStrength;
                FillVal.Text = App.Settings.GlassFillStrength + "%";

                AccentBox.Text = App.Settings.GlassAccentColor;
                TintBox.Text = App.Settings.GlassTintColor;
                UpdatePreviews();
                HighlightSwatches();
                GlassOffNote.Visibility = App.Settings.LiquidGlassEnabled ? Visibility.Collapsed : Visibility.Visible;
            }
            finally { _loading = false; }
        }

        // ---- swatch grids ---------------------------------------------------------
        private void BuildSwatches()
        {
            AccentSwatches.Children.Clear();
            foreach (var hex in AccentPresets)
                AccentSwatches.Children.Add(MakeSwatch(hex, isAccent: true));

            TintSwatches.Children.Clear();
            foreach (var hex in TintPresets)
                TintSwatches.Children.Add(MakeSwatch(hex, isAccent: false));
        }

        private Border MakeSwatch(string hex, bool isAccent)
        {
            var b = new Border
            {
                Width = 30,
                Height = 30,
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 0, 8, 8),
                Cursor = Cursors.Hand,
                Background = new SolidColorBrush(ThemeService.Hex(hex, Colors.Gray)),
                BorderBrush = (Brush)FindResource("GlassStroke"),
                BorderThickness = new Thickness(1),
                Tag = hex,
                ToolTip = hex,
            };
            b.MouseLeftButtonUp += (_, __) =>
            {
                if (isAccent) { AccentBox.Text = hex; CommitAccent(hex); }
                else { TintBox.Text = hex; CommitTint(hex); }
            };
            return b;
        }

        private void HighlightSwatches()
        {
            foreach (var c in AccentSwatches.Children)
                if (c is Border b) MarkSwatch(b, SameHex((string)b.Tag, App.Settings.GlassAccentColor));
            foreach (var c in TintSwatches.Children)
                if (c is Border b) MarkSwatch(b, SameHex((string)b.Tag, App.Settings.GlassTintColor));
        }

        private void MarkSwatch(Border b, bool active)
        {
            b.BorderBrush = active ? (Brush)FindResource("AccentSolid") : (Brush)FindResource("GlassStroke");
            b.BorderThickness = new Thickness(active ? 2.5 : 1);
        }

        private static bool SameHex(string a, string b)
        {
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
            return string.Equals(Norm(a), Norm(b), StringComparison.OrdinalIgnoreCase);
        }

        private static string Norm(string s)
        {
            var c = ThemeService.Hex(s, Colors.Transparent);
            return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        }

        private void UpdatePreviews()
        {
            AccentPreview.Background = string.IsNullOrWhiteSpace(App.Settings.GlassAccentColor)
                ? (Brush)FindResource("AccentSolid")
                : new SolidColorBrush(ThemeService.Hex(App.Settings.GlassAccentColor, Colors.SteelBlue));
            TintPreview.Background = string.IsNullOrWhiteSpace(App.Settings.GlassTintColor)
                ? new SolidColorBrush(ThemeService.Hex(ThemeService.Resolve(App.Settings).Bg, Colors.Black))
                : new SolidColorBrush(ThemeService.Hex(App.Settings.GlassTintColor, Colors.Black));
        }

        // ---- toggles / sliders ----------------------------------------------------
        private void SwGlass_Click(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            App.Settings.LiquidGlassEnabled = SwGlass.IsChecked == true;
            App.Settings.Save();
            GlassOffNote.Visibility = App.Settings.LiquidGlassEnabled ? Visibility.Collapsed : Visibility.Visible;
            ApplyEverywhere();
        }

        private void TransSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_loading) return;
            App.Settings.GlassTransparency = (int)Math.Round(e.NewValue);
            TransVal.Text = App.Settings.GlassTransparency + "%";
            App.Settings.Save();
            ApplyEverywhere();
        }

        private void FillSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_loading) return;
            App.Settings.GlassFillStrength = (int)Math.Round(e.NewValue);
            FillVal.Text = App.Settings.GlassFillStrength + "%";
            App.Settings.Save();
            ApplyEverywhere();
        }

        // ---- accent ---------------------------------------------------------------
        private void AccentBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) CommitAccent(AccentBox.Text);
        }

        private void PickAccent_Click(object sender, RoutedEventArgs e)
        {
            string? picked = PickColor(string.IsNullOrWhiteSpace(App.Settings.GlassAccentColor)
                ? ThemeService.Resolve(App.Settings).Accent : App.Settings.GlassAccentColor);
            if (picked != null) { AccentBox.Text = picked; CommitAccent(picked); }
        }

        private void ResetAccent_Click(object sender, RoutedEventArgs e)
        {
            AccentBox.Text = "";
            CommitAccent("");
        }

        private void CommitAccent(string hex)
        {
            App.Settings.GlassAccentColor = NormalizeOrEmpty(hex);
            App.Settings.Save();
            UpdatePreviews();
            HighlightSwatches();
            ApplyEverywhere();
        }

        // ---- tint -----------------------------------------------------------------
        private void TintBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) CommitTint(TintBox.Text);
        }

        private void PickTint_Click(object sender, RoutedEventArgs e)
        {
            string? picked = PickColor(string.IsNullOrWhiteSpace(App.Settings.GlassTintColor)
                ? ThemeService.Resolve(App.Settings).Bg : App.Settings.GlassTintColor);
            if (picked != null) { TintBox.Text = picked; CommitTint(picked); }
        }

        private void ResetTint_Click(object sender, RoutedEventArgs e)
        {
            TintBox.Text = "";
            CommitTint("");
        }

        private void CommitTint(string hex)
        {
            App.Settings.GlassTintColor = NormalizeOrEmpty(hex);
            App.Settings.Save();
            UpdatePreviews();
            HighlightSwatches();
            ApplyEverywhere();
        }

        // ---- helpers --------------------------------------------------------------
        private static string NormalizeOrEmpty(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            var c = ThemeService.Hex(s, Colors.Transparent);
            return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        }

        private static string? PickColor(string current)
        {
            using var dlg = new WinForms.ColorDialog { FullOpen = true };
            var c = ThemeService.Hex(current, Colors.SteelBlue);
            dlg.Color = System.Drawing.Color.FromArgb(c.R, c.G, c.B);
            if (dlg.ShowDialog() == WinForms.DialogResult.OK)
                return $"#{dlg.Color.R:X2}{dlg.Color.G:X2}{dlg.Color.B:X2}";
            return null;
        }

        private void ApplyEverywhere()
        {
            App.Current.ApplyThemeEverywhere();
            _host?.RefreshRunningState();
        }
    }
}

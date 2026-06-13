using System;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using TwoTo1Screen.Services;
using WinForms = System.Windows.Forms;

namespace TwoTo1Screen.Views
{
    public partial class CustomThemeWindow : Window
    {
        private readonly string _type;
        public CustomTheme? Result { get; private set; }

        public CustomThemeWindow(string type)
        {
            InitializeComponent();
            _type = type;

            if (type == "advanced")
            {
                TitleText.Text = "Своя тема — код";
                EasyPanel.Visibility = Visibility.Collapsed;
                AdvancedPanel.Visibility = Visibility.Visible;
                NameBox.Text = "Моя тема";
                CodeBox.Text =
                    "{\n  \"mode\": \"dark\",\n  \"bg\": \"#101218\",\n  \"bg2\": \"#0B0D12\",\n  \"surface\": \"#1A1D24\",\n" +
                    "  \"accent\": \"#7C5CFF\",\n  \"accent2\": \"#5E44E0\",\n  \"text\": \"#F2F5FA\",\n  \"textMuted\": \"#8B93A2\"\n}";
            }
            else
            {
                TitleText.Text = "Своя тема — фото + цвет";
                NameBox.Text = "Моя тема";
                AccentBox.Text = "#7C5CFF";
            }

            SourceInitialized += (_, __) => ThemeService.ApplyWindowGlass(this, App.Settings);
            UpdateSwatch();
        }

        private void Header_Drag(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) { try { DragMove(); } catch { } }
        }

        private void AccentBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => UpdateSwatch();

        private void UpdateSwatch()
        {
            try { Swatch.Background = new SolidColorBrush(ThemeService.Hex(AccentBox.Text, Colors.MediumPurple)); }
            catch { }
        }

        private void PickColor_Click(object sender, RoutedEventArgs e)
        {
            using var dlg = new WinForms.ColorDialog { FullOpen = true };
            var c = ThemeService.Hex(AccentBox.Text, Colors.MediumPurple);
            dlg.Color = System.Drawing.Color.FromArgb(c.R, c.G, c.B);
            if (dlg.ShowDialog() == WinForms.DialogResult.OK)
            {
                AccentBox.Text = $"#{dlg.Color.R:X2}{dlg.Color.G:X2}{dlg.Color.B:X2}";
                UpdateSwatch();
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            string name = string.IsNullOrWhiteSpace(NameBox.Text) ? "Моя тема" : NameBox.Text.Trim();
            try
            {
                if (_type == "advanced")
                {
                    using var doc = JsonDocument.Parse(CodeBox.Text);
                    var r = doc.RootElement;
                    string mode = Get(r, "mode", "dark");
                    var ct = new CustomTheme
                    {
                        Name = name, Type = "advanced", Mode = mode == "light" ? "light" : "dark",
                        Bg = Hex(Get(r, "bg", "#101218")),
                        Bg2 = Hex(Get(r, "bg2", "")),
                        Surface = Hex(Get(r, "surface", "")),
                        Accent = Hex(Get(r, "accent", "#7C5CFF")),
                        Accent2 = Hex(Get(r, "accent2", "")),
                        Text = Hex(Get(r, "text", mode == "light" ? "#181C22" : "#F2F5FA")),
                        TextMuted = Hex(Get(r, "textMuted", "#8B93A2")),
                    };
                    if (string.IsNullOrEmpty(Get(r, "bg2", ""))) ct.Bg2 = ct.Bg;
                    if (string.IsNullOrEmpty(Get(r, "surface", ""))) ct.Surface = ct.Bg;
                    if (string.IsNullOrEmpty(Get(r, "accent2", ""))) ct.Accent2 = ct.Accent;
                    Result = ct;
                }
                else
                {
                    bool dark = ModeDark.IsChecked == true;
                    string accent = Hex(AccentBox.Text);
                    var ct = new CustomTheme
                    {
                        Name = name, Type = "easy", Mode = dark ? "dark" : "light",
                        BgImageUrl = (ImgBox.Text ?? "").Trim(),
                        Bg = dark ? "#12141A" : "#EEF1F6",
                        Bg2 = dark ? "#0C0E13" : "#E2E7EF",
                        Surface = dark ? "#1B1E26" : "#FFFFFF",
                        Accent = accent,
                        Accent2 = accent,
                        Text = dark ? "#F2F5FA" : "#181C22",
                        TextMuted = dark ? "#8B93A2" : "#5A6472",
                    };
                    Result = ct;
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                ErrText.Text = "Ошибка в коде темы: " + ex.Message;
                ErrText.Visibility = Visibility.Visible;
            }
        }

        private static string Get(JsonElement e, string p, string def) =>
            e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? (v.GetString() ?? def) : def;

        private static string Hex(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return s;
            var c = ThemeService.Hex(s, Colors.Transparent);
            return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
    }
}

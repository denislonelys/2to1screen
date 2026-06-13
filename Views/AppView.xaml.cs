using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TwoTo1Screen.Services;
using WinForms = System.Windows.Forms;

namespace TwoTo1Screen.Views
{
    public partial class AppView : UserControl
    {
        private MainWindow? _host;
        private bool _loading;

        // Curated Liquid Glass accent presets.
        private static readonly string[] AccentPresets =
        {
            "#CFE0F2", "#6EA8FE", "#34D5E0", "#4FE3B0", "#9BE356",
            "#FFC24B", "#FF8A5C", "#FF6B9D", "#B07CFF", "#7A86FF"
        };

        public AppView()
        {
            InitializeComponent();
        }

        public void Bind(MainWindow host)
        {
            _host = host;
            _loading = true;
            SwAutoStart.IsChecked = App.Settings.AutoStart;
            SwGlass.IsChecked = App.Settings.LiquidGlassDisabled;
            BuildAccentPalette();
            UpdateAccentPanelVisibility();
            _loading = false;
            RefreshRunningState();
        }

        private void BuildAccentPalette()
        {
            AccentPalette.Children.Clear();
            string current = (App.Settings.LiquidGlassAccent ?? "").Trim();

            foreach (var hex in AccentPresets)
            {
                var color = ThemeManager.ParseColor(hex, Colors.Gray);
                var swatch = new RadioButton
                {
                    Style = (Style)FindResource("ColorSwatch"),
                    GroupName = "GlassAccent",
                    Tag = hex,
                    Background = new SolidColorBrush(color),
                    ToolTip = hex,
                    IsChecked = string.Equals(hex, current, StringComparison.OrdinalIgnoreCase)
                };
                swatch.Checked += AccentSwatch_Checked;
                AccentPalette.Children.Add(swatch);
            }
        }

        private void AccentSwatch_Checked(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            if (sender is RadioButton rb && rb.Tag is string hex)
                ApplyAccent(hex);
        }

        private void ApplyAccent(string hex)
        {
            App.Settings.LiquidGlassAccent = hex;
            App.Settings.Save();
            if (!App.Settings.LiquidGlassDisabled)
            {
                _host?.ApplyTheme();
            }
        }

        private void BtnCustomColor_Click(object sender, RoutedEventArgs e)
        {
            var start = ThemeManager.ParseColor(App.Settings.LiquidGlassAccent, Colors.SkyBlue);
            using var dlg = new WinForms.ColorDialog
            {
                FullOpen = true,
                Color = System.Drawing.Color.FromArgb(start.R, start.G, start.B)
            };
            if (dlg.ShowDialog() == WinForms.DialogResult.OK)
            {
                string hex = $"#{dlg.Color.R:X2}{dlg.Color.G:X2}{dlg.Color.B:X2}";
                // Clear preset selection if the custom color is not one of them.
                _loading = true;
                foreach (var child in AccentPalette.Children)
                {
                    if (child is RadioButton rb)
                        rb.IsChecked = string.Equals(rb.Tag as string, hex, StringComparison.OrdinalIgnoreCase);
                }
                _loading = false;
                ApplyAccent(hex);
            }
        }

        private void UpdateAccentPanelVisibility()
        {
            GlassAccentPanel.Visibility = App.Settings.LiquidGlassDisabled
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        /// <summary>Re-read the glass on/off state (e.g. after the theme store disabled it).</summary>
        public void SyncFromSettings()
        {
            _loading = true;
            SwGlass.IsChecked = App.Settings.LiquidGlassDisabled;
            string current = (App.Settings.LiquidGlassAccent ?? "").Trim();
            foreach (var child in AccentPalette.Children)
            {
                if (child is RadioButton rb)
                    rb.IsChecked = string.Equals(rb.Tag as string, current, StringComparison.OrdinalIgnoreCase);
            }
            _loading = false;
            UpdateAccentPanelVisibility();
        }

        public void RefreshRunningState()
        {
            bool running = App.Current.IsRunning;
            bool paused = App.Current.IsPaused;
            BtnLaunch.Content = running ? "Остановить" : "Запустить";
            BtnLaunch.Background = running
                ? (Brush)App.Current.Resources["RunningGradient"]
                : (Brush)App.Current.Resources["AccentGradient"];
            RunDot.Fill = running && !paused
                ? new SolidColorBrush(Color.FromRgb(0x5C, 0xCB, 0x8E))
                : (running && paused
                    ? new SolidColorBrush(Color.FromRgb(0xE0, 0xB3, 0x4A))
                    : new SolidColorBrush(Color.FromRgb(0x88, 0x93, 0xA2)));
            RunLabel.Text = running
                ? (paused ? "Сервис на паузе — перехват приостановлен" : "Сервис работает — Print Screen перехвачен")
                : "Сервис остановлен";
        }

        private void BtnLaunch_Click(object sender, RoutedEventArgs e)
        {
            if (App.Current.IsRunning)
                App.Current.StopService();
            else
                App.Current.StartService();

            _host?.RefreshRunningState();
        }

        private void SwAutoStart_Click(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            bool wanted = SwAutoStart.IsChecked == true;

            bool ok = wanted ? AutoStart.Enable() : AutoStart.Disable();
            if (!ok)
            {
                // Revert if the elevation prompt was cancelled or failed.
                _loading = true;
                SwAutoStart.IsChecked = !wanted;
                _loading = false;
                System.Windows.MessageBox.Show(
                    "Не удалось изменить автозапуск. Для этого нужны права администратора — " +
                    "подтвердите запрос UAC.",
                    "2to1screen — автозапуск",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            App.Settings.AutoStart = wanted;
            App.Settings.Save();

            if (wanted && !App.Current.IsRunning)
            {
                // Makes sense to also activate the service now.
                App.Current.StartService();
                _host?.RefreshRunningState();
            }
        }

        private void SwGlass_Click(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            App.Settings.LiquidGlassDisabled = SwGlass.IsChecked == true;
            App.Settings.Save();
            UpdateAccentPanelVisibility();
            _host?.ApplyTheme();
        }
    }
}

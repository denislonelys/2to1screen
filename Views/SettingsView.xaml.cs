using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TwoTo1Screen.Services;
using WinForms = System.Windows.Forms;

namespace TwoTo1Screen.Views
{
    public partial class SettingsView : UserControl
    {
        private MainWindow? _host;
        private bool _loading;
        private List<MonitorEntry> _monitors = new();

        public SettingsView()
        {
            InitializeComponent();
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
                BuildMonitorList();
                SelectQualityChip(App.Settings.Quality);
                UpdateCustomInfo();

                SwClipboard.IsChecked = App.Settings.SaveToClipboard;
                SwFile.IsChecked = App.Settings.SaveToFile;
                SwWinPs.IsChecked = App.Settings.InterceptWinPrintScreen;
                SwSound.IsChecked = App.Settings.ShutterSound;
                SwNotify.IsChecked = App.Settings.ShowNotification;
                SwAnim.IsChecked = App.Settings.Animations;
                SwWinD.IsChecked = App.Settings.WinDSingleMonitor;

                FolderText.Text = App.Settings.SaveFolder;
                RefreshHotkeyHints();
            }
            finally
            {
                _loading = false;
            }
        }

        private void BuildMonitorList()
        {
            MonitorList.Children.Clear();
            _monitors = MonitorService.GetMonitors();

            if (_monitors.Count == 0)
            {
                MonitorList.Children.Add(new TextBlock
                {
                    Text = "Мониторы не обнаружены.",
                    Style = (Style)FindResource("Caption")
                });
                return;
            }

            MonitorEntry selected;
            try { selected = MonitorService.Resolve(App.Settings, _monitors); }
            catch { selected = _monitors[0]; }

            foreach (var m in _monitors)
            {
                var panel = new StackPanel();
                panel.Children.Add(new TextBlock
                {
                    Text = m.Title,
                    Foreground = (Brush)FindResource("TextPrimary"),
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 13.5
                });
                panel.Children.Add(new TextBlock
                {
                    Text = $"Разрешение {m.Sub}",
                    Style = (Style)FindResource("Caption"),
                    Margin = new Thickness(0, 2, 0, 0)
                });

                var rb = new RadioButton
                {
                    Style = (Style)FindResource("SelectRow"),
                    GroupName = "Mon",
                    Tag = m,
                    Content = panel,
                    IsChecked = ReferenceEquals(m, selected)
                };
                rb.Checked += Monitor_Checked;
                MonitorList.Children.Add(rb);
            }
        }

        private void Monitor_Checked(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            if (sender is RadioButton rb && rb.Tag is MonitorEntry m)
            {
                App.Settings.MonitorDevice = m.Device;
                App.Settings.MonitorIndex = m.Index;
                App.Settings.Save();
                UpdateCustomInfo();
            }
        }

        private void SelectQualityChip(QualityPreset preset)
        {
            ChipVeryHigh.IsChecked = preset == QualityPreset.VeryHigh;
            ChipHigh.IsChecked = preset == QualityPreset.High;
            ChipMedium.IsChecked = preset == QualityPreset.Medium;
            ChipLow.IsChecked = preset == QualityPreset.Low;
            ChipCustom.IsChecked = preset == QualityPreset.Custom;
        }

        private void Quality_Checked(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            if (sender is not RadioButton rb || rb.Tag is not string tag)
                return;

            if (!Enum.TryParse<QualityPreset>(tag, out var preset))
                return;

            App.Settings.Quality = preset;
            App.Settings.Save();

            if (preset == QualityPreset.Custom)
                OpenCustomDialog();

            UpdateCustomInfo();
        }

        private void OpenCustomDialog()
        {
            MonitorEntry mon;
            try { mon = MonitorService.Resolve(App.Settings, _monitors.Count > 0 ? _monitors : MonitorService.GetMonitors()); }
            catch
            {
                mon = new MonitorEntry { Width = 1920, Height = 1080 };
            }

            var dlg = new CustomQualityWindow(mon.Width, mon.Height, App.Settings)
            {
                Owner = _host ?? Window.GetWindow(this)
            };

            if (dlg.ShowDialog() == true)
            {
                App.Settings.CustomWidth = dlg.ResultWidth;
                App.Settings.CustomHeight = dlg.ResultHeight;
                App.Settings.CustomFormat = dlg.ResultFormat;
                App.Settings.CustomJpegQuality = dlg.ResultJpegQuality;
                App.Settings.Save();
            }
            UpdateCustomInfo();
        }

        private void UpdateCustomInfo()
        {
            if (App.Settings.Quality == QualityPreset.Custom)
            {
                int w = App.Settings.CustomWidth;
                int h = App.Settings.CustomHeight;
                string res = (w > 0 && h > 0) ? $"{w}×{h}" : "родное разрешение";
                string fmt = App.Settings.CustomFormat == SaveFormat.Png
                    ? "PNG"
                    : $"JPEG {App.Settings.CustomJpegQuality}%";
                CustomInfo.Text = $"Текущие настройки: {res}, формат {fmt}.  Нажмите «Настроить самому» ещё раз, чтобы изменить.";
                CustomInfo.Visibility = Visibility.Visible;
            }
            else
            {
                CustomInfo.Visibility = Visibility.Collapsed;
            }
        }

        private void Toggle_Click(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            App.Settings.SaveToClipboard = SwClipboard.IsChecked == true;
            App.Settings.SaveToFile = SwFile.IsChecked == true;
            App.Settings.InterceptWinPrintScreen = SwWinPs.IsChecked == true;
            App.Settings.ShutterSound = SwSound.IsChecked == true;
            App.Settings.ShowNotification = SwNotify.IsChecked == true;
            App.Settings.Animations = SwAnim.IsChecked == true;
            App.Settings.Save();
        }

        private void WinD_Click(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            App.Settings.WinDSingleMonitor = SwWinD.IsChecked == true;
            App.Settings.Save();
        }

        public void RefreshHotkeyHints()
        {
            if (WinDHotkey == null) return;
            App.Settings.Hotkeys.TryGetValue("minimize_monitor", out var combo);
            WinDHotkey.Text = string.IsNullOrEmpty(combo)
                ? "Клик колёсиком по строке — назначить свою горячую клавишу."
                : $"Горячая клавиша: {combo}. Клик колёсиком по строке — изменить.";
        }

        private void BtnFolder_Click(object sender, RoutedEventArgs e)
        {
            using var dlg = new WinForms.FolderBrowserDialog
            {
                Description = "Выберите папку для сохранения скриншотов",
                UseDescriptionForTitle = true,
                SelectedPath = App.Settings.SaveFolder
            };

            if (dlg.ShowDialog() == WinForms.DialogResult.OK && !string.IsNullOrWhiteSpace(dlg.SelectedPath))
            {
                App.Settings.SaveFolder = dlg.SelectedPath;
                App.Settings.Save();
                FolderText.Text = dlg.SelectedPath;
            }
        }
    }
}

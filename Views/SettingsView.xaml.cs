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
                SwWinD.IsChecked = App.Settings.WinDSingleMonitor;

                FolderText.Text = App.Settings.SaveFolder;
                BuildHotkeyList();
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
            App.Settings.Save();
        }

        private void WinD_Click(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            App.Settings.WinDSingleMonitor = SwWinD.IsChecked == true;
            App.Settings.Save();
        }

        private static readonly (string Id, string Label)[] BindableActions = new[]
        {
            ("toggle_service", "Запустить / остановить перехват"),
            ("capture_now", "Сделать скриншот сейчас"),
            ("open_app", "Открыть окно 2to1screen"),
            ("toggle_glass", "Вкл/выкл Liquid Glass"),
        };

        public void RefreshHotkeyHints() => BuildHotkeyList();

        private void BuildHotkeyList()
        {
            if (HotkeyList == null) return;
            HotkeyList.Children.Clear();
            foreach (var (id, label) in BindableActions)
            {
                App.Settings.Hotkeys.TryGetValue(id, out var combo);

                var card = new Border
                {
                    Style = (Style)FindResource("Card"),
                    Padding = new Thickness(14, 12, 14, 12),
                    Margin = new Thickness(0, 0, 0, 8),
                };
                BindHelper.SetAction(card, id);

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                left.Children.Add(new TextBlock { Text = label, Foreground = (Brush)FindResource("TextPrimary"), FontWeight = FontWeights.SemiBold, FontSize = 13.5 });
                var hint = new TextBlock { Style = (Style)FindResource("Caption"), Margin = new Thickness(0, 3, 0, 0) };
                hint.Text = string.IsNullOrEmpty(combo) ? "Не назначено" : "Клавиша: " + combo;
                left.Children.Add(hint);
                Grid.SetColumn(left, 0);

                var change = new Button { Content = "Изменить", Style = (Style)FindResource("GlassButton"), VerticalAlignment = VerticalAlignment.Center };
                change.Click += (_, __) => BindHelper.OpenBind(id, Window.GetWindow(this));
                Grid.SetColumn(change, 1);

                grid.Children.Add(left);
                grid.Children.Add(change);

                if (!string.IsNullOrEmpty(combo))
                {
                    var clear = new Button { Content = "Сброс", Style = (Style)FindResource("GlassButton"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
                    clear.Click += (_, __) => { App.Settings.Hotkeys.Remove(id); App.Settings.Save(); BuildHotkeyList(); };
                    Grid.SetColumn(clear, 2);
                    grid.Children.Add(clear);
                }

                card.Child = grid;
                HotkeyList.Children.Add(card);
            }
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

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using TwoTo1Screen.Services;

namespace TwoTo1Screen.Views
{
    public partial class ThemeStoreView : UserControl
    {
        private MainWindow? _host;

        public ThemeStoreView()
        {
            InitializeComponent();
        }

        public void Bind(MainWindow host)
        {
            _host = host;
            Reload();
        }

        public void Reload()
        {
            GlassNotice.Visibility = App.Settings.LiquidGlassDisabled
                ? Visibility.Collapsed
                : Visibility.Visible;

            CardsHost.Children.Clear();
            bool glassOff = App.Settings.LiquidGlassDisabled;

            foreach (var theme in ThemeManager.Themes)
            {
                bool selected = glassOff &&
                    string.Equals(theme.Id, App.Settings.ThemeId, StringComparison.OrdinalIgnoreCase);
                CardsHost.Children.Add(BuildCard(theme, selected));
            }
        }

        // ----------------------------------------------------------- card build

        private Border BuildCard(ThemeDefinition theme, bool selected)
        {
            var card = new Border
            {
                Width = 240,
                Margin = new Thickness(0, 0, 14, 14),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(12),
                Cursor = Cursors.Hand,
                Background = (Brush)FindResource("GlassFill"),
                BorderThickness = new Thickness(selected ? 2 : 1),
                BorderBrush = selected
                    ? (Brush)FindResource("AccentSolid")
                    : (Brush)FindResource("GlassStrokeSoft")
            };

            var stack = new StackPanel();
            stack.Children.Add(BuildThumbnail(theme));

            var titleRow = new Grid { Margin = new Thickness(2, 12, 2, 0) };
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var nameBlock = new TextBlock
            {
                Text = theme.Name,
                Foreground = (Brush)FindResource("TextPrimary"),
                FontWeight = FontWeights.SemiBold,
                FontSize = 15
            };
            Grid.SetColumn(nameBlock, 0);
            titleRow.Children.Add(nameBlock);

            if (selected)
            {
                var sel = new TextBlock
                {
                    Text = "✓ Выбрана",
                    Foreground = (Brush)FindResource("AccentSolid"),
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(sel, 1);
                titleRow.Children.Add(sel);
            }
            stack.Children.Add(titleRow);

            stack.Children.Add(new TextBlock
            {
                Text = theme.Tagline,
                Style = (Style)FindResource("Caption"),
                Margin = new Thickness(2, 3, 2, 0)
            });

            card.Child = stack;

            card.MouseLeftButtonUp += (_, __) => SelectTheme(theme);
            card.MouseDown += (_, e) =>
            {
                if (e.ChangedButton == MouseButton.Middle)
                {
                    e.Handled = true;
                    OpenPreview(theme);
                }
            };
            return card;
        }

        private FrameworkElement BuildThumbnail(ThemeDefinition theme)
        {
            const double w = 214, h = 118;
            var p = theme.Palette;

            var grid = new Grid
            {
                Width = w,
                Height = h,
                Background = ThemeManager.WindowBackdropBrush(p),
                Clip = new RectangleGeometry(new Rect(0, 0, w, h), 12, 12)
            };

            // Animated themes: hint with a couple of soft color blobs.
            if (theme.Animated)
            {
                double[] xs = { 0.22, 0.7, 0.5 };
                double[] ys = { 0.3, 0.65, 0.2 };
                for (int i = 0; i < theme.Blobs.Length && i < 3; i++)
                {
                    Color c = theme.Blobs[i];
                    var fill = new RadialGradientBrush();
                    fill.GradientStops.Add(new GradientStop(Color.FromArgb(0xD0, c.R, c.G, c.B), 0));
                    fill.GradientStops.Add(new GradientStop(Color.FromArgb(0x00, c.R, c.G, c.B), 1));
                    var blob = new Ellipse
                    {
                        Width = 150,
                        Height = 150,
                        Fill = fill,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Top,
                        Margin = new Thickness(xs[i] * w - 75, ys[i] * h - 75, 0, 0),
                        IsHitTestVisible = false
                    };
                    grid.Children.Add(blob);
                }
            }

            // A mini "card" + accent pill to suggest the UI surfaces.
            var miniCard = new Border
            {
                Width = 132,
                Height = 56,
                CornerRadius = new CornerRadius(9),
                Background = new SolidColorBrush(p.GlassHoverA),
                BorderBrush = new SolidColorBrush(p.Stroke),
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(14, 0, 0, 16),
                IsHitTestVisible = false
            };
            var miniStack = new StackPanel { Margin = new Thickness(10, 9, 10, 9) };
            miniStack.Children.Add(new Border
            {
                Height = 7,
                Width = 70,
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(p.TextPrimary)
            });
            miniStack.Children.Add(new Border
            {
                Height = 6,
                Width = 96,
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(p.TextMuted),
                Margin = new Thickness(0, 7, 0, 0)
            });
            miniCard.Child = miniStack;
            grid.Children.Add(miniCard);

            var pill = new Border
            {
                Width = 50,
                Height = 24,
                CornerRadius = new CornerRadius(7),
                Background = new SolidColorBrush(p.Accent),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 16, 18),
                IsHitTestVisible = false
            };
            grid.Children.Add(pill);

            if (theme.Animated)
            {
                var badge = new Border
                {
                    CornerRadius = new CornerRadius(7),
                    Background = new SolidColorBrush(Color.FromArgb(0x66, 0, 0, 0)),
                    Padding = new Thickness(7, 2, 7, 3),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, 10, 10, 0),
                    IsHitTestVisible = false,
                    Child = new TextBlock
                    {
                        Text = "✨ Анимация",
                        Foreground = Brushes.White,
                        FontSize = 10.5
                    }
                };
                grid.Children.Add(badge);
            }

            return grid;
        }

        // -------------------------------------------------------------- actions

        private void SelectTheme(ThemeDefinition theme)
        {
            if (!App.Settings.LiquidGlassDisabled)
            {
                // Liquid Glass is on — warn before switching to a store theme.
                var dlg = new GlassWarningWindow { Owner = Window.GetWindow(this) };
                bool? ok = dlg.ShowDialog();
                if (ok != true)
                    return;

                App.Settings.LiquidGlassDisabled = true;
                App.Settings.ThemeId = theme.Id;
                App.Settings.Save();
                _host?.ApplyTheme();
                _host?.SyncAppView();
                Reload();
                return;
            }

            App.Settings.ThemeId = theme.Id;
            App.Settings.Save();
            _host?.ApplyTheme();
            Reload();
        }

        private void OpenPreview(ThemeDefinition theme)
        {
            var win = new ThemePreviewWindow(theme) { Owner = Window.GetWindow(this) };
            win.ShowDialog();
        }
    }
}

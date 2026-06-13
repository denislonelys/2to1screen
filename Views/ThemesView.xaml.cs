using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TwoTo1Screen.Services;

namespace TwoTo1Screen.Views
{
    public partial class ThemesView : UserControl
    {
        private MainWindow? _host;
        private bool _loading;
        private List<ThemeVm> _all = new();

        public ThemesView()
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
                SwGlass.IsChecked = App.Settings.LiquidGlassEnabled;
                TransSlider.Value = App.Settings.GlassTransparency;
                TransVal.Text = App.Settings.GlassTransparency + "%";
                GlassOffNote.Visibility = App.Settings.LiquidGlassEnabled ? Visibility.Collapsed : Visibility.Visible;

                BuildCatalog();
                BuildCustom();
            }
            finally { _loading = false; }
        }

        private void BuildCatalog()
        {
            _all = new List<ThemeVm> { new ThemeVm(ThemeService.BuiltinBlack) };
            foreach (var p in ThemeService.Catalog)
                _all.Add(new ThemeVm(p));

            StoreTitle.Text = $"Магазин тем ({_all.Count})";
            ApplyFilter(SearchBox.Text);
        }

        private void ApplyFilter(string? q)
        {
            IEnumerable<ThemeVm> items = _all;
            if (!string.IsNullOrWhiteSpace(q))
            {
                string s = q.Trim().ToLowerInvariant();
                items = _all.Where(t => t.Name.ToLowerInvariant().Contains(s) ||
                                        (t.Author ?? "").ToLowerInvariant().Contains(s));
            }
            var list = items.ToList();
            _loading = true;
            ThemeList.ItemsSource = list;
            ThemeList.SelectedItem = list.FirstOrDefault(t => t.Id == App.Settings.ActiveThemeId);
            _loading = false;
        }

        private void BuildCustom()
        {
            CustomList.Items.Clear();
            var themes = App.Settings.CustomThemes;
            NoCustom.Visibility = themes.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            foreach (var c in themes)
                CustomList.Items.Add(BuildCustomTile(c));
        }

        private FrameworkElement BuildCustomTile(CustomTheme c)
        {
            var vm = new ThemeVm(ThemeService.FromCustom(c), isCustom: true);
            bool active = App.Settings.ActiveThemeId == c.Id;

            var preview = new Border
            {
                Width = 150, Height = 52, CornerRadius = new CornerRadius(8),
                Background = vm.BgBrush, Margin = new Thickness(0, 0, 0, 6),
            };
            var pg = new Grid();
            pg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
            pg.ColumnDefinitions.Add(new ColumnDefinition());
            pg.Children.Add(new Border { Background = vm.SurfaceBrush, CornerRadius = new CornerRadius(8, 0, 0, 8) });
            var pill = new Border { Height = 7, Width = 50, CornerRadius = new CornerRadius(4), Background = vm.AccentBrush, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(10, 12, 0, 0) };
            Grid.SetColumn(pill, 1); pg.Children.Add(pill);
            preview.Child = pg;

            var name = new TextBlock { Text = c.Name, Foreground = (Brush)FindResource("TextPrimary"), FontWeight = FontWeights.SemiBold, FontSize = 12.5, TextTrimming = TextTrimming.CharacterEllipsis };
            var del = new Button { Content = "Удалить", Style = (Style)FindResource("GlassButton"), Padding = new Thickness(8, 3, 8, 3), FontSize = 11, Margin = new Thickness(0, 6, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
            del.Click += (_, __) => { App.Settings.CustomThemes.RemoveAll(x => x.Id == c.Id); if (App.Settings.ActiveThemeId == c.Id) App.Settings.ActiveThemeId = "builtin-black"; App.Settings.Save(); App.Current.ApplyThemeEverywhere(); ReloadFromSettings(); };

            var sp = new StackPanel { Width = 150 };
            sp.Children.Add(preview);
            sp.Children.Add(name);
            sp.Children.Add(del);

            var card = new Border
            {
                CornerRadius = new CornerRadius(12), Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 10, 10),
                Background = (Brush)FindResource("GlassFill"),
                BorderBrush = active ? (Brush)FindResource("AccentSolid") : (Brush)FindResource("GlassStrokeSoft"),
                BorderThickness = new Thickness(active ? 2 : 1),
                Cursor = System.Windows.Input.Cursors.Hand,
                Child = sp,
            };
            card.MouseLeftButtonUp += (_, __) => ApplyTheme(c.Id);
            return card;
        }

        private void ApplyTheme(string id)
        {
            App.Settings.ActiveThemeId = id;
            App.Settings.Save();
            App.Current.ApplyThemeEverywhere();
            // refresh highlight states without rebuilding catalog scroll position
            BuildCustom();
        }

        private void ThemeList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading) return;
            if (ThemeList.SelectedItem is ThemeVm vm)
                ApplyTheme(vm.Id);
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loading) return;
            ApplyFilter(SearchBox.Text);
        }

        private void SwGlass_Click(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            App.Settings.LiquidGlassEnabled = SwGlass.IsChecked == true;
            App.Settings.Save();
            GlassOffNote.Visibility = App.Settings.LiquidGlassEnabled ? Visibility.Collapsed : Visibility.Visible;
            App.Current.ApplyThemeEverywhere();
        }

        private void TransSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_loading) return;
            App.Settings.GlassTransparency = (int)Math.Round(e.NewValue);
            TransVal.Text = App.Settings.GlassTransparency + "%";
            App.Settings.Save();
            App.Current.ApplyThemeEverywhere();
        }

        private void NewEasy_Click(object sender, RoutedEventArgs e) => OpenEditor("easy");
        private void NewAdvanced_Click(object sender, RoutedEventArgs e) => OpenEditor("advanced");

        private void OpenEditor(string type)
        {
            var dlg = new CustomThemeWindow(type) { Owner = _host ?? Window.GetWindow(this) };
            if (dlg.ShowDialog() == true && dlg.Result != null)
            {
                App.Settings.CustomThemes.Add(dlg.Result);
                App.Settings.ActiveThemeId = dlg.Result.Id;
                App.Settings.Save();
                App.Current.ApplyThemeEverywhere();
                ReloadFromSettings();
            }
        }
    }
}

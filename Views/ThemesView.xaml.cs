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
        private List<ThemeVm> _all = new();
        private const int Columns = 4;

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
            BuildCatalog();
            BuildCustom();
        }

        private void BuildCatalog()
        {
            _all = new List<ThemeVm> { new ThemeVm(ThemeService.BuiltinBlack) };
            foreach (var p in ThemeService.Catalog)
                _all.Add(new ThemeVm(p));

            string active = App.Settings.ActiveThemeId ?? "builtin-black";
            foreach (var vm in _all) vm.IsActive = vm.Id == active;

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
            ThemeList.ItemsSource = Chunk(items.ToList(), Columns);
        }

        private static List<List<ThemeVm>> Chunk(List<ThemeVm> src, int size)
        {
            var rows = new List<List<ThemeVm>>();
            for (int i = 0; i < src.Count; i += size)
                rows.Add(src.GetRange(i, Math.Min(size, src.Count - i)));
            return rows;
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
                Width = 150, Height = 60, CornerRadius = new CornerRadius(8),
                Background = vm.BgBrush, Margin = new Thickness(0, 0, 0, 8),
                ClipToBounds = true,
            };
            var pg = new Grid();
            pg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            pg.ColumnDefinitions.Add(new ColumnDefinition());
            pg.Children.Add(new Border { Background = vm.SurfaceBrush, CornerRadius = new CornerRadius(8, 0, 0, 8) });
            var pill = new Border { Height = 8, Width = 52, CornerRadius = new CornerRadius(4), Background = vm.AccentBrush, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(10, 14, 0, 0) };
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
                CornerRadius = new CornerRadius(14), Padding = new Thickness(10),
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
            // flip active highlight without rebuilding (preserves scroll position)
            foreach (var vm in _all) vm.IsActive = vm.Id == id;
            BuildCustom();
        }

        private void Tile_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is ThemeVm vm)
                ApplyTheme(vm.Id);
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SearchHint.Visibility = string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
            ApplyFilter(SearchBox.Text);
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

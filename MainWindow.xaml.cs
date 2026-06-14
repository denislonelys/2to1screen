using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using TwoTo1Screen.Services;
using TwoTo1Screen.Views;

namespace TwoTo1Screen
{
    public partial class MainWindow : Window
    {
        private bool _exiting;

        public MainWindow()
        {
            InitializeComponent();
            PreviewMouseDown += BindHelper.HandleMiddleClick;
            BindHelper.HotkeysChanged += OnHotkeysChanged;

            SourceInitialized += (_, __) => ApplyTheme();
            Loaded += (_, __) =>
            {
                PageApp.Bind(this);
                PageSettings.Bind(this);
                PageThemes.Bind(this);
                PageGlass.Bind(this);
                RefreshRunningState();
                RefreshLicenseUi();
            };
            Closed += (_, __) => BindHelper.HotkeysChanged -= OnHotkeysChanged;
        }

        private void OnHotkeysChanged()
        {
            PageApp.RefreshHotkeyHints();
            PageSettings.RefreshHotkeyHints();
        }

        /// <summary>Apply the active theme (resources + window acrylic).</summary>
        public void ApplyTheme()
        {
            App.Current.ApplyThemeResources();
            ThemeService.ApplyWindowGlass(this, App.Settings);
        }

        public void SyncThemeControls()
        {
            PageThemes.ReloadFromSettings();
            PageGlass.ReloadFromSettings();
            PageApp.RefreshRunningState();
        }

        public void RefreshLicenseUi()
        {
            var info = LicenseService.Current;
            string text; Color color;
            switch (info.Status)
            {
                case LicenseStatus.Licensed:
                    text = info.Lifetime ? "Активировано · бессрочно" : "Активировано";
                    color = Color.FromRgb(0x49, 0xB9, 0x87); break;
                case LicenseStatus.Trial:
                    text = "Пробная версия"; color = Color.FromRgb(0xD9, 0xA5, 0x3B); break;
                default:
                    text = "Не активировано"; color = Color.FromRgb(0xC0, 0x39, 0x2B); break;
            }
            LicenseBadgeText.Text = text;
            LicenseBadge.Background = new SolidColorBrush(Color.FromArgb(0x33, color.R, color.G, color.B));

            bool full = App.Current.LicenseFull;
            // gate Settings, Themes & Liquid Glass behind a valid licence
            NavSettings.Opacity = full ? 1.0 : 0.55;
            NavThemes.Opacity = full ? 1.0 : 0.55;
            NavGlass.Opacity = full ? 1.0 : 0.55;
        }

        public void RefreshRunningState()
        {
            bool running = App.Current.IsRunning;
            StatusDot.Fill = running
                ? new SolidColorBrush(Color.FromRgb(0x5C, 0xCB, 0x8E))
                : new SolidColorBrush(Color.FromRgb(0x88, 0x93, 0xA2));
            StatusText.Text = running ? "Работает" : "Выкл.";
            PageApp.RefreshRunningState();
        }

        private void Nav_Checked(object sender, RoutedEventArgs e)
        {
            if (PageApp == null || PageSettings == null || PageThemes == null || PageGlass == null || PageDev == null)
                return;

            // gating: Settings, Themes & Liquid Glass require a full licence
            if ((NavSettings.IsChecked == true || NavThemes.IsChecked == true || NavGlass.IsChecked == true) && !App.Current.LicenseFull)
            {
                System.Windows.MessageBox.Show(
                    "Этот раздел доступен только после активации программы по ключу.\n\n" +
                    "Без активации доступны только запуск и фоновый режим.",
                    "2to1screen — нужна активация", MessageBoxButton.OK, MessageBoxImage.Information);
                NavApp.IsChecked = true;
                return;
            }

            PageApp.Visibility = NavApp.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PageSettings.Visibility = NavSettings.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PageThemes.Visibility = NavThemes.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PageGlass.Visibility = NavGlass.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PageDev.Visibility = NavDev.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

            if (NavSettings.IsChecked == true) PageSettings.ReloadFromSettings();
            if (NavThemes.IsChecked == true) PageThemes.ReloadFromSettings();
            if (NavGlass.IsChecked == true) PageGlass.ReloadFromSettings();

            FrameworkElement? active =
                NavApp.IsChecked == true ? PageApp :
                NavSettings.IsChecked == true ? (FrameworkElement)PageSettings :
                NavThemes.IsChecked == true ? PageThemes :
                NavGlass.IsChecked == true ? PageGlass :
                NavDev.IsChecked == true ? PageDev : null;
            AnimatePage(active);
        }

        /// <summary>Subtle, GPU-friendly fade + slide-in when switching tabs.</summary>
        private void AnimatePage(FrameworkElement? page)
        {
            if (page == null) return;
            if (!App.Settings.Animations) { page.Opacity = 1; return; }

            var tt = page.RenderTransform as TranslateTransform;
            if (tt == null)
            {
                tt = new TranslateTransform();
                page.RenderTransform = tt;
            }

            var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            var slide = new DoubleAnimation(10, 0, TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            page.BeginAnimation(OpacityProperty, fade);
            tt.BeginAnimation(TranslateTransform.YProperty, slide);
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) { try { DragMove(); } catch { } }
        }

        private void BtnMin_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        protected override void OnClosing(CancelEventArgs e)
        {
            if (_exiting || App.Current.IsForceExiting) { base.OnClosing(e); return; }

            if (App.Current.IsRunning)
            {
                e.Cancel = true;
                Hide();
                App.Current.NotifyHidden();
                return;
            }

            _exiting = true;
            base.OnClosing(e);
            App.Current.ExitApp();
        }
    }
}

using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TwoTo1Screen.Services;

namespace TwoTo1Screen
{
    public partial class MainWindow : Window
    {
        private bool _exiting;

        public MainWindow()
        {
            InitializeComponent();

            SourceInitialized += (_, __) => ApplyTheme();
            ThemeManager.Changed += OnThemeChanged;

            BackdropHost.SizeChanged += (_, __) => ClipBackdrop();

            Loaded += (_, __) =>
            {
                PageApp.Bind(this);
                PageSettings.Bind(this);
                PageThemes.Bind(this);
                RefreshRunningState();
            };

            Closed += (_, __) => ThemeManager.Changed -= OnThemeChanged;
        }

        private void OnThemeChanged()
        {
            ThemeManager.ApplyWindowChrome(this, RootBorder, BackdropHost);
            ClipBackdrop();
        }

        private void ClipBackdrop()
        {
            if (BackdropHost.ActualWidth > 0 && BackdropHost.ActualHeight > 0)
            {
                BackdropHost.Clip = new RectangleGeometry(
                    new Rect(0, 0, BackdropHost.ActualWidth, BackdropHost.ActualHeight), 20, 20);
            }
        }

        /// <summary>Apply the active appearance (Liquid Glass or a store theme).</summary>
        public void ApplyTheme()
        {
            ThemeManager.ApplyCurrent();
            ThemeManager.ApplyWindowChrome(this, RootBorder, BackdropHost);
            ClipBackdrop();
        }

        /// <summary>Re-sync the Application tab toggles after a settings change elsewhere.</summary>
        public void SyncAppView()
        {
            PageApp.SyncFromSettings();
        }

        public void RefreshRunningState()
        {
            bool running = App.Current.IsRunning;
            bool paused = App.Current.IsPaused;

            StatusDot.Fill = running && !paused
                ? new SolidColorBrush(Color.FromRgb(0x5C, 0xCB, 0x8E))
                : (running && paused
                    ? new SolidColorBrush(Color.FromRgb(0xE0, 0xB3, 0x4A))
                    : new SolidColorBrush(Color.FromRgb(0x88, 0x93, 0xA2)));
            StatusText.Text = running ? (paused ? "Пауза" : "Работает") : "Выкл.";
            PageApp.RefreshRunningState();
        }

        private void Nav_Checked(object sender, RoutedEventArgs e)
        {
            if (PageApp == null || PageSettings == null || PageThemes == null || PageDev == null)
                return;

            PageApp.Visibility = NavApp.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PageSettings.Visibility = NavSettings.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PageThemes.Visibility = NavThemes.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PageDev.Visibility = NavDev.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

            if (NavSettings.IsChecked == true)
                PageSettings.ReloadFromSettings();
            if (NavThemes.IsChecked == true)
                PageThemes.Reload();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                try { DragMove(); } catch { }
            }
        }

        private void BtnMin_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        protected override void OnClosing(CancelEventArgs e)
        {
            if (_exiting)
            {
                base.OnClosing(e);
                return;
            }

            if (App.Current.IsRunning)
            {
                // Keep the capture service running in the tray.
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

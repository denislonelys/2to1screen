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
            Loaded += (_, __) =>
            {
                PageApp.Bind(this);
                PageSettings.Bind(this);
                RefreshRunningState();
            };
        }

        /// <summary>Apply or remove the Liquid Glass backdrop based on settings.</summary>
        public void ApplyTheme()
        {
            if (App.Settings.LiquidGlassDisabled)
            {
                RootBorder.SetResourceReference(Border.BackgroundProperty, "WindowFillSolid");
                Glass.Disable(this);
            }
            else
            {
                RootBorder.SetResourceReference(Border.BackgroundProperty, "WindowFillGlass");
                Glass.Enable(this);
            }
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
            if (PageApp == null || PageSettings == null || PageDev == null)
                return;

            PageApp.Visibility = NavApp.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PageSettings.Visibility = NavSettings.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PageDev.Visibility = NavDev.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

            if (NavSettings.IsChecked == true)
                PageSettings.ReloadFromSettings();
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

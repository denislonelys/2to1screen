using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TwoTo1Screen.Services;

namespace TwoTo1Screen.Views
{
    public partial class AppView : UserControl
    {
        private MainWindow? _host;
        private bool _loading;

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
            _loading = false;
            RefreshRunningState();
        }

        public void RefreshRunningState()
        {
            bool running = App.Current.IsRunning;
            BtnLaunch.Content = running ? "Остановить" : "Запустить";
            BtnLaunch.Background = running
                ? (Brush)App.Current.Resources["RunningGradient"]
                : (Brush)App.Current.Resources["AccentGradient"];
            RunDot.Fill = running
                ? new SolidColorBrush(Color.FromRgb(0x5C, 0xCB, 0x8E))
                : new SolidColorBrush(Color.FromRgb(0x88, 0x93, 0xA2));
            RunLabel.Text = running ? "Сервис работает — Print Screen перехвачен" : "Сервис остановлен";
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
            _host?.ApplyTheme();
        }
    }
}

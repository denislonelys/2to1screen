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
            _loading = false;
            RefreshRunningState();
            RefreshHotkeyHints();
        }

        public void RefreshHotkeyHints()
        {
            if (App.Settings.Hotkeys.TryGetValue("toggle_service", out var combo) && !string.IsNullOrEmpty(combo))
                HotkeyHint.Text = $"Горячая клавиша запуска: {combo}. Клик колёсиком по кнопке — изменить.";
            else
                HotkeyHint.Text = "Подсказка: нажмите на кнопку «Запустить» колёсиком мыши, чтобы назначить горячую клавишу.";
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
            if (!App.Current.LicenseBasic)
            {
                System.Windows.MessageBox.Show("Активируйте программу, чтобы пользоваться ею.",
                    "2to1screen", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (App.Current.IsRunning) App.Current.StopService();
            else App.Current.StartService();
            _host?.RefreshRunningState();
        }

        private void SwAutoStart_Click(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            bool wanted = SwAutoStart.IsChecked == true;

            bool ok = wanted ? AutoStart.Enable() : AutoStart.Disable();
            if (!ok)
            {
                _loading = true;
                SwAutoStart.IsChecked = !wanted;
                _loading = false;
                System.Windows.MessageBox.Show(
                    "Не удалось изменить автозапуск. Нужны права администратора — подтвердите запрос UAC.",
                    "2to1screen — автозапуск", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            App.Settings.AutoStart = wanted;
            App.Settings.Save();

            if (wanted && !App.Current.IsRunning && App.Current.LicenseBasic)
            {
                App.Current.StartService();
                _host?.RefreshRunningState();
            }
        }
    }
}

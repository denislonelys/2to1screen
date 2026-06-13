using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using TwoTo1Screen.Services;

namespace TwoTo1Screen.Views
{
    /// <summary>Mandatory update dialog — downloads the new exe and restarts. No cancel.</summary>
    public partial class UpdateWindow : Window
    {
        private readonly LicenseInfo _info;

        public UpdateWindow(LicenseInfo info)
        {
            InitializeComponent();
            _info = info;
            TitleText.Text = $"Доступно обновление {info.LatestVersion}";
            Loaded += async (_, __) => await StartAsync();
        }

        private async Task StartAsync()
        {
            BtnRetry.Visibility = Visibility.Collapsed;
            SubText.Text = "Обновление обязательно. Загрузка новой версии…";
            PercentText.Text = "0%";
            try
            {
                await UpdateService.DownloadAndApplyAsync(_info.DownloadUrl, p =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        Bar.Value = p * 100;
                        PercentText.Text = $"{(int)(p * 100)}%";
                    });
                }, CancellationToken.None);

                SubText.Text = "Загрузка завершена. Перезапуск…";
                PercentText.Text = "100%";
                // The helper batch waits for this process to exit, then swaps the exe and relaunches.
                await Task.Delay(400);
                App.Current.ForceExit();
            }
            catch
            {
                SubText.Text = "Не удалось загрузить обновление. Проверьте интернет.";
                BtnRetry.Visibility = Visibility.Visible;
            }
        }

        private async void Retry_Click(object sender, RoutedEventArgs e) => await StartAsync();
    }
}

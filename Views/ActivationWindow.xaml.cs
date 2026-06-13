using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using TwoTo1Screen.Services;

namespace TwoTo1Screen.Views
{
    public enum ActivationResult { Cancelled, Activated, Trial }

    public partial class ActivationWindow : Window
    {
        public ActivationResult Result { get; private set; } = ActivationResult.Cancelled;

        public ActivationWindow(LicenseInfo info)
        {
            InitializeComponent();
            HwidBox.Text = LicenseService.Hardware;

            SourceInitialized += (_, __) => ThemeService.ApplyWindowGlass(this, App.Settings);

            // The free trial is only offered if it has never been used on this device.
            bool trialAllowed = info.Status == LicenseStatus.None;
            BtnTrial.IsEnabled = trialAllowed;
            BtnTrial.Opacity = trialAllowed ? 1.0 : 0.5;
            if (!trialAllowed)
                TrialNote.Text = info.Status == LicenseStatus.Expired
                    ? "Срок действия ключа истёк. Введите новый ключ."
                    : "Пробный период уже был использован на этом устройстве.";
        }

        private void Header_Drag(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) { try { DragMove(); } catch { } }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Result = ActivationResult.Cancelled;
            DialogResult = false;
            Close();
        }

        private async void Activate_Click(object sender, RoutedEventArgs e)
        {
            string key = (KeyBox.Text ?? "").Trim();
            if (key.Length < 6) { ShowStatus("Введите ключ активации."); return; }

            SetBusy(true);
            var info = await Task.Run(() => LicenseService.Activate(key));
            SetBusy(false);

            if (info.Status == LicenseStatus.Licensed && info.Error == null)
            {
                Result = ActivationResult.Activated;
                DialogResult = true;
                Close();
                return;
            }

            ShowStatus(info.Error switch
            {
                "invalid" => "Ключ не найден. Проверьте правильность ввода.",
                "revoked" => "Этот ключ заблокирован.",
                "used" => "Ключ уже активирован на другом устройстве.",
                "blocked" => "Это устройство заблокировано.",
                "network" => "Нет связи с сервером. Проверьте интернет и попробуйте снова.",
                _ => "Не удалось активировать ключ. Попробуйте ещё раз.",
            });
        }

        private async void Trial_Click(object sender, RoutedEventArgs e)
        {
            SetBusy(true);
            var info = await Task.Run(() => LicenseService.StartTrial());
            SetBusy(false);

            if (info.Status == LicenseStatus.Trial && info.Error == null)
            {
                Result = ActivationResult.Trial;
                DialogResult = true;
                Close();
                return;
            }

            ShowStatus(info.Error switch
            {
                "trial_used" => "Пробный период уже использован на этом устройстве.",
                "blocked" => "Это устройство заблокировано.",
                "network" => "Нет связи с сервером. Проверьте интернет и попробуйте снова.",
                _ => "Не удалось запустить пробный период.",
            });
        }

        private void SetBusy(bool busy)
        {
            BtnActivate.IsEnabled = !busy;
            BtnTrial.IsEnabled = !busy && BtnTrial.Opacity > 0.6;
            KeyBox.IsEnabled = !busy;
            Cursor = busy ? Cursors.Wait : Cursors.Arrow;
        }

        private void ShowStatus(string msg)
        {
            StatusText.Text = msg;
            StatusText.Visibility = Visibility.Visible;
        }
    }
}

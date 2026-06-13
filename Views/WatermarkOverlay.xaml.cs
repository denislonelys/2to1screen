using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using static TwoTo1Screen.Services.Native;

namespace TwoTo1Screen.Views
{
    /// <summary>
    /// A click-through, always-on-top watermark in the bottom-right corner of the
    /// primary screen — like the Windows "activate" watermark. Shows a live
    /// countdown of the remaining trial time.
    /// </summary>
    public partial class WatermarkOverlay : Window
    {
        private readonly DispatcherTimer _timer;
        private long _trialExpiresAt; // ms epoch, 0 = no countdown

        /// <summary>Raised on the UI thread when the trial countdown reaches zero.</summary>
        public event Action? TrialExpired;

        public WatermarkOverlay()
        {
            InitializeComponent();
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (_, __) => Tick();

            SourceInitialized += (_, __) =>
            {
                MakeClickThrough();
                Reposition();
            };
            Loaded += (_, __) => { Reposition(); Tick(); };
        }

        public void SetTrial(long trialExpiresAt)
        {
            _trialExpiresAt = trialExpiresAt;
            Tick();
            _timer.Start();
        }

        private void Tick()
        {
            if (_trialExpiresAt <= 0)
            {
                Line2.Text = "Активируйте программу 2to1screen";
                return;
            }
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            long left = _trialExpiresAt - now;
            if (left <= 0)
            {
                _timer.Stop();
                Line1.Text = "2to1screen — пробный период истёк";
                Line2.Text = "Активируйте программу, чтобы продолжить";
                TrialExpired?.Invoke();
                return;
            }
            var ts = TimeSpan.FromMilliseconds(left);
            string rem = ts.TotalDays >= 1
                ? $"{(int)ts.TotalDays}д {ts.Hours}ч {ts.Minutes}м"
                : (ts.TotalHours >= 1 ? $"{ts.Hours}ч {ts.Minutes}м {ts.Seconds}с" : $"{ts.Minutes}м {ts.Seconds}с");
            Line1.Text = "2to1screen — пробная версия";
            Line2.Text = $"Не активировано. Осталось: {rem}";
        }

        private void Reposition()
        {
            try
            {
                var wa = SystemParameters.WorkArea; // primary screen work area (DIP)
                Left = wa.Right - ActualWidth - 28;
                Top = wa.Bottom - ActualHeight - 24;
            }
            catch { }
        }

        private void MakeClickThrough()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;
            long ex = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
            ex |= WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(ex));
        }

        public void CloseOverlay()
        {
            _timer.Stop();
            try { Close(); } catch { }
        }
    }
}

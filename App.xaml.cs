using System;
using System.Linq;
using System.Threading;
using System.Windows;
using TwoTo1Screen.Services;

namespace TwoTo1Screen
{
    public partial class App : System.Windows.Application
    {
        public static AppSettings Settings { get; private set; } = new AppSettings();

        private HotkeyHook? _hook;
        private TrayIcon? _tray;
        private MainWindow? _main;
        private bool _backgroundMode;
        private Mutex? _singleton;

        public static new App Current => (App)System.Windows.Application.Current;

        public bool IsRunning => _hook?.IsActive == true;
        public bool IsPaused => _hook?.IsPaused == true;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            if (!WindowsCheck.IsWindows11())
            {
                System.Windows.MessageBox.Show(
                    "Приложение «2to1screen» поддерживает только Windows 11.\n\n" +
                    "Текущая система не определена как Windows 11 (сборка ниже 22000), " +
                    "поэтому запуск невозможен.",
                    "2to1screen — несовместимая система",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                Shutdown();
                return;
            }

            _backgroundMode = e.Args.Any(a =>
                string.Equals(a, "--background", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(a, "/background", StringComparison.OrdinalIgnoreCase));

            // Allow only one running instance (avoids two hooks double-capturing).
            if (!TryAcquireSingleInstance())
            {
                if (!_backgroundMode)
                {
                    System.Windows.MessageBox.Show(
                        "«2to1screen» уже запущено. Откройте его из системного трея (значок рядом с часами).",
                        "2to1screen", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                Shutdown();
                return;
            }

            Settings = AppSettings.Load();
            ThemeManager.ApplyCurrent();
            _hook = new HotkeyHook(() => Settings);
            _hook.PausedChanged += OnPausedChanged;
            CaptureController.Captured += OnCaptured;

            // We manage shutdown ourselves so the background service can keep
            // running after the main window is closed.
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            SetupTray();

            if (_backgroundMode)
            {
                StartService();
                _tray?.SetRunning(IsRunning);
            }
            else
            {
                ShowMainWindow();
            }
        }

        private bool TryAcquireSingleInstance()
        {
            try
            {
                _singleton = new Mutex(true, @"Local\2to1screen_singleton_v1", out bool createdNew);
                return createdNew;
            }
            catch
            {
                // best-effort: never block startup because of the guard
                return true;
            }
        }

        private void SetupTray()
        {
            if (_tray != null) return;
            _tray = new TrayIcon();
            _tray.OnOpen += ShowMainWindow;
            _tray.OnToggle += () =>
            {
                if (IsRunning) StopService(); else StartService();
                _tray?.SetRunning(IsRunning);
                _main?.RefreshRunningState();
            };
            _tray.OnExit += ExitApp;
            _tray.Show(IsRunning);
        }

        public void ShowMainWindow()
        {
            Dispatcher.Invoke(() =>
            {
                if (_main == null)
                {
                    _main = new MainWindow();
                    _main.Closed += (_, __) => _main = null;
                }
                _main.Show();
                if (_main.WindowState == WindowState.Minimized)
                    _main.WindowState = WindowState.Normal;
                _main.Activate();
                _main.Topmost = true;
                _main.Topmost = false;
            });
        }

        public void StartService()
        {
            _hook?.Start();
            _tray?.SetRunning(IsRunning);
        }

        public void StopService()
        {
            _hook?.Stop();
            _tray?.SetRunning(IsRunning);
        }

        private void OnPausedChanged(bool paused)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _tray?.Notify("2to1screen", paused
                    ? "Перехват приостановлен (горячая клавиша)."
                    : "Перехват возобновлён.");
                _tray?.SetRunning(IsRunning);
                _main?.RefreshRunningState();
            }));
        }

        private void OnCaptured(string? path)
        {
            if (Settings.ShowNotification && _tray != null)
            {
                string msg = path != null
                    ? $"Скриншот сохранён:\n{System.IO.Path.GetFileName(path)}"
                    : "Скриншот скопирован в буфер обмена";
                Dispatcher.BeginInvoke(new Action(() => _tray?.Notify("2to1screen", msg)));
            }
        }

        public void NotifyHidden()
        {
            _tray?.Notify("2to1screen", "Приложение свёрнуто в трей и продолжает работать в фоне.");
        }

        public void ExitApp()
        {
            StopService();
            _tray?.Dispose();
            _tray = null;
            Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            CaptureController.Captured -= OnCaptured;
            _hook?.Dispose();
            _tray?.Dispose();
            try { _singleton?.ReleaseMutex(); } catch { }
            _singleton?.Dispose();
            base.OnExit(e);
        }
    }
}

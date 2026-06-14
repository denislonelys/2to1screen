using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using TwoTo1Screen.Services;
using TwoTo1Screen.Views;

namespace TwoTo1Screen
{
    public partial class App : System.Windows.Application
    {
        public static AppSettings Settings { get; private set; } = new AppSettings();

        private HotkeyHook? _hook;
        private TrayIcon? _tray;
        private MainWindow? _main;
        private WatermarkOverlay? _watermark;
        private DispatcherTimer? _licenseTimer;
        private bool _backgroundMode;
        public bool IsForceExiting { get; private set; }
        private Mutex? _singleton;

        public static new App Current => (App)System.Windows.Application.Current;

        public bool IsRunning => _hook?.IsActive == true;

        public bool LicenseFull => LicenseService.Current.FullAccess;
        public bool LicenseBasic => LicenseService.Current.BasicAccess;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            if (!WindowsCheck.IsWindows11())
            {
                System.Windows.MessageBox.Show(
                    "Приложение «2to1screen» поддерживает только Windows 11.\n\n" +
                    "Текущая система не определена как Windows 11 (сборка ниже 22000), поэтому запуск невозможен.",
                    "2to1screen — несовместимая система",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                Shutdown(); return;
            }

            _backgroundMode = e.Args.Any(a =>
                string.Equals(a, "--background", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(a, "/background", StringComparison.OrdinalIgnoreCase));

            if (!TryAcquireSingleInstance())
            {
                if (!_backgroundMode)
                    System.Windows.MessageBox.Show(
                        "«2to1screen» уже запущено. Откройте его из системного трея.",
                        "2to1screen", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown(); return;
            }

            Settings = AppSettings.Load();
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // theme first so every window is styled
            ThemeService.LoadCatalogCacheSync();
            ApplyThemeResources();

            _hook = new HotkeyHook(() => Settings);
            _hook.OnAction = a => Dispatcher.BeginInvoke(new Action(() => ActionRegistry.Invoke(a)));
            RegisterActions();
            CaptureController.Captured += OnCaptured;

            // ----- licensing + update gate -----
            var info = LicenseService.Check();

            if (info.Status == LicenseStatus.Blocked)
            {
                System.Windows.MessageBox.Show(
                    "Это устройство (HWID) заблокировано администратором.\nЗапуск программы невозможен.\n\nHWID: " + LicenseService.Hardware,
                    "2to1screen — доступ заблокирован", MessageBoxButton.OK, MessageBoxImage.Error);
                ForceExit(); return;
            }

            if (UpdateService.UpdateAvailable(info) && info.Mandatory)
            {
                var uw = new UpdateWindow(info);
                uw.Show();
                return; // update window restarts the app
            }

            if (!info.BasicAccess)
            {
                bool ok = ShowActivationGate(info);
                if (!ok) { ForceExit(); return; }
                info = LicenseService.Current;
            }

            SetupTray();
            StartCatalogRefresh();
            StartLicenseWatcher();

            if (info.Status == LicenseStatus.Trial)
                ShowWatermark(info.TrialExpiresAt);

            if (_backgroundMode)
            {
                if (LicenseBasic) StartService();
                _tray?.SetRunning(IsRunning);
            }
            else
            {
                ShowMainWindow();
            }
        }

        // ---- actions ----
        private void RegisterActions()
        {
            ActionRegistry.Register("toggle_service", "Запустить / остановить перехват", () =>
            {
                if (!LicenseBasic) return;
                if (IsRunning) StopService(); else StartService();
                _tray?.SetRunning(IsRunning);
                _main?.RefreshRunningState();
            });
            ActionRegistry.Register("capture_now", "Сделать скриншот", () =>
            {
                if (LicenseBasic) Task.Run(() => CaptureController.DoCapture(Settings));
            });
            ActionRegistry.Register("open_app", "Открыть окно 2to1screen", ShowMainWindow);
            ActionRegistry.Register("toggle_glass", "Включить / выключить Liquid Glass", () =>
            {
                if (!LicenseFull) return;
                Settings.LiquidGlassEnabled = !Settings.LiquidGlassEnabled;
                Settings.Save();
                ApplyThemeEverywhere();
                _main?.SyncThemeControls();
            });
            ActionRegistry.Register("minimize_monitor", "Свернуть окна одного монитора", () =>
            {
                if (!LicenseBasic) return;
                Task.Run(() => { try { WindowManager.MinimizeMonitorUnderCursor(); } catch { } });
            });
        }

        // ---- theme ----
        public void ApplyThemeResources() => ThemeService.ApplyResources(Settings);

        public void ApplyThemeEverywhere()
        {
            ThemeService.ApplyResources(Settings);
            if (_main != null) ThemeService.ApplyWindowGlass(_main, Settings);
            foreach (Window w in Windows)
            {
                if (w is MainWindow) continue;
                try { ThemeService.ApplyWindowGlass(w, Settings); } catch { }
            }
        }

        // ---- license gate ----
        private bool ShowActivationGate(LicenseInfo info)
        {
            var dlg = new ActivationWindow(info);
            dlg.ShowDialog();
            return LicenseService.Current.BasicAccess;
        }

        private void ShowWatermark(long trialExpiresAt)
        {
            if (_watermark != null) return;
            _watermark = new WatermarkOverlay();
            _watermark.TrialExpired += OnTrialExpired;
            _watermark.Show();
            _watermark.SetTrial(trialExpiresAt);
        }

        private void HideWatermark()
        {
            if (_watermark != null) { _watermark.CloseOverlay(); _watermark = null; }
        }

        private void OnTrialExpired()
        {
            StopService();
            HideWatermark();
            var info = LicenseService.Check();
            if (info.Status == LicenseStatus.Blocked) { ForceExit(); return; }
            if (!info.BasicAccess)
            {
                if (!ShowActivationGate(info)) { ForceExit(); return; }
                info = LicenseService.Current;
                if (info.Status == LicenseStatus.Trial) ShowWatermark(info.TrialExpiresAt);
            }
            _main?.RefreshLicenseUi();
        }

        private void StartLicenseWatcher()
        {
            _licenseTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(30) };
            _licenseTimer.Tick += async (_, __) =>
            {
                var info = await Task.Run(() => LicenseService.Check());
                if (info.Status == LicenseStatus.Blocked)
                {
                    System.Windows.MessageBox.Show("Это устройство заблокировано администратором.",
                        "2to1screen", MessageBoxButton.OK, MessageBoxImage.Error);
                    ForceExit(); return;
                }
                if (!info.BasicAccess)
                {
                    StopService();
                    if (!ShowActivationGate(info)) { ForceExit(); return; }
                }
                _main?.RefreshLicenseUi();
            };
            _licenseTimer.Start();
        }

        private void StartCatalogRefresh()
        {
            _ = Task.Run(async () =>
            {
                await ThemeService.RefreshCatalogAsync();
            });
        }

        // ---- infra (unchanged behaviour) ----
        private bool TryAcquireSingleInstance()
        {
            try
            {
                _singleton = new Mutex(true, @"Local\2to1screen_singleton_v1", out bool createdNew);
                return createdNew;
            }
            catch { return true; }
        }

        private void SetupTray()
        {
            if (_tray != null) return;
            _tray = new TrayIcon();
            _tray.OnOpen += ShowMainWindow;
            _tray.OnToggle += () =>
            {
                if (!LicenseBasic) return;
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
            if (!LicenseBasic) return;
            _hook?.Start();
            _tray?.SetRunning(IsRunning);
        }

        public void StopService()
        {
            _hook?.Stop();
            _tray?.SetRunning(IsRunning);
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

        public void NotifyHidden() => _tray?.Notify("2to1screen", "Приложение свёрнуто в трей и работает в фоне.");

        public void ExitApp()
        {
            StopService();
            HideWatermark();
            _tray?.Dispose();
            _tray = null;
            Shutdown();
        }

        /// <summary>Exit immediately (used by the updater and the block/gate flows).</summary>
        public void ForceExit()
        {
            IsForceExiting = true;
            try { StopService(); } catch { }
            HideWatermark();
            _tray?.Dispose(); _tray = null;
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

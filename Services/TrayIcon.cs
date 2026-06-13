using System;
using System.Drawing;
using WinForms = System.Windows.Forms;

namespace TwoTo1Screen.Services
{
    /// <summary>System-tray icon used in background mode (and when the main window is hidden).</summary>
    internal sealed class TrayIcon : IDisposable
    {
        private WinForms.NotifyIcon? _icon;
        private WinForms.ToolStripMenuItem? _toggleItem;

        public event Action? OnOpen;
        public event Action? OnToggle;
        public event Action? OnExit;

        public void Show(bool running)
        {
            if (_icon != null)
                return;

            _icon = new WinForms.NotifyIcon
            {
                Icon = LoadIcon(),
                Text = "2to1screen",
                Visible = true
            };

            var menu = new WinForms.ContextMenuStrip();

            var open = new WinForms.ToolStripMenuItem("Открыть");
            open.Click += (_, __) => OnOpen?.Invoke();

            _toggleItem = new WinForms.ToolStripMenuItem(running ? "Остановить" : "Запустить");
            _toggleItem.Click += (_, __) => OnToggle?.Invoke();

            var exit = new WinForms.ToolStripMenuItem("Выход");
            exit.Click += (_, __) => OnExit?.Invoke();

            menu.Items.Add(open);
            menu.Items.Add(_toggleItem);
            menu.Items.Add(new WinForms.ToolStripSeparator());
            menu.Items.Add(exit);

            _icon.ContextMenuStrip = menu;
            _icon.DoubleClick += (_, __) => OnOpen?.Invoke();
        }

        public void SetRunning(bool running)
        {
            if (_toggleItem != null)
                _toggleItem.Text = running ? "Остановить" : "Запустить";
        }

        public void Notify(string title, string text)
        {
            try
            {
                if (_icon != null)
                {
                    _icon.BalloonTipTitle = title;
                    _icon.BalloonTipText = text;
                    _icon.ShowBalloonTip(2500);
                }
            }
            catch { }
        }

        private static Icon LoadIcon()
        {
            try
            {
                var uri = new Uri("pack://application:,,,/assets/app.ico");
                var info = System.Windows.Application.GetResourceStream(uri);
                if (info != null)
                {
                    using var stream = info.Stream;
                    return new Icon(stream);
                }
            }
            catch { }
            return SystemIcons.Application;
        }

        public void Dispose()
        {
            if (_icon != null)
            {
                _icon.Visible = false;
                _icon.Dispose();
                _icon = null;
            }
        }
    }
}

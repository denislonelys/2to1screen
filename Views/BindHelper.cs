using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using TwoTo1Screen.Services;

namespace TwoTo1Screen.Views
{
    /// <summary>
    /// Lets the user middle-click any control marked with an attached action id
    /// to open the key-binding popup. Attach a single PreviewMouseDown handler at
    /// window level via <see cref="HandleMiddleClick"/>.
    /// </summary>
    public static class BindHelper
    {
        public static readonly DependencyProperty ActionProperty =
            DependencyProperty.RegisterAttached("Action", typeof(string), typeof(BindHelper),
                new PropertyMetadata(null));

        public static void SetAction(DependencyObject o, string v) => o.SetValue(ActionProperty, v);
        public static string? GetAction(DependencyObject o) => (string?)o.GetValue(ActionProperty);

        public static void HandleMiddleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Middle) return;

            DependencyObject? src = e.OriginalSource as DependencyObject;
            string? actionId = null;
            while (src != null)
            {
                var a = src.GetValue(ActionProperty) as string;
                if (!string.IsNullOrEmpty(a)) { actionId = a; break; }
                src = VisualTreeHelper.GetParent(src) ?? (src as FrameworkElement)?.Parent as DependencyObject;
            }
            if (string.IsNullOrEmpty(actionId)) return;

            e.Handled = true;

            if (!LicenseService.Current.FullAccess)
            {
                System.Windows.MessageBox.Show(
                    "Горячие клавиши доступны после активации программы.",
                    "2to1screen", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            OpenBind(actionId!, Window.GetWindow(sender as DependencyObject ?? src));
        }

        public static void OpenBind(string actionId, Window? owner)
        {
            App.Settings.Hotkeys.TryGetValue(actionId, out var current);
            var dlg = new KeyBindWindow(actionId, current) { Owner = owner };
            if (dlg.ShowDialog() == true && dlg.Saved)
            {
                if (string.IsNullOrEmpty(dlg.ResultCombo))
                    App.Settings.Hotkeys.Remove(actionId);
                else
                {
                    // a combo can only be bound to one action
                    foreach (var k in new System.Collections.Generic.List<string>(App.Settings.Hotkeys.Keys))
                        if (App.Settings.Hotkeys[k] == dlg.ResultCombo) App.Settings.Hotkeys.Remove(k);
                    App.Settings.Hotkeys[actionId] = dlg.ResultCombo!;
                }
                App.Settings.Save();
                HotkeysChanged?.Invoke();
            }
        }

        public static event Action? HotkeysChanged;
    }
}

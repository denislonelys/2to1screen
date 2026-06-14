using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using TwoTo1Screen.Services;

namespace TwoTo1Screen.Views
{
    public partial class KeyBindWindow : Window
    {
        /// <summary>Resulting combo string. null = cancelled. "" = cleared (remove binding).</summary>
        public string? ResultCombo { get; private set; }
        public bool Saved { get; private set; }

        private Hotkey? _captured;

        public KeyBindWindow(string actionId, string? current)
        {
            InitializeComponent();
            ActionLabel.Text = ActionRegistry.LabelOf(actionId);
            if (!string.IsNullOrWhiteSpace(current))
            {
                ComboText.Text = current!;
                _captured = Hotkey.Parse(current);
            }
            SourceInitialized += (_, __) => ThemeService.ApplyWindowGlass(this, App.Settings);
            Loaded += KeyBindWindow_Loaded;
        }

        private void KeyBindWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // anchor the mini-menu near the top-centre of the owner window
            var owner = Owner;
            if (owner != null && owner.WindowState != WindowState.Minimized)
            {
                double ow = owner.ActualWidth > 0 ? owner.ActualWidth : owner.Width;
                Left = owner.Left + (ow - ActualWidth) / 2;
                Top = owner.Top + 62;
            }
            else
            {
                Left = (SystemParameters.PrimaryScreenWidth - ActualWidth) / 2;
                Top = 80;
            }

            // gentle slide-down + fade entrance
            if (App.Settings.Animations)
            {
                BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140)));
                EnterTransform.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty,
                    new DoubleAnimation(-14, 0, TimeSpan.FromMilliseconds(180))
                    {
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    });
            }
            else
            {
                EnterTransform.Y = 0;
            }
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);
            e.Handled = true;

            Key key = e.Key == Key.System ? e.SystemKey : e.Key;

            // ignore lone modifier presses
            if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
                or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin or Key.System)
                return;

            if (key == Key.Escape) { Cancel_Click(this, new RoutedEventArgs()); return; }

            var hk = Hotkey.FromKey(key, Keyboard.Modifiers);
            _captured = hk;
            ComboText.Text = hk.ToString();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            ResultCombo = _captured?.ToString() ?? "";
            Saved = true;
            DialogResult = true;
            Close();
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            ResultCombo = "";
            Saved = true;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            ResultCombo = null;
            Saved = false;
            DialogResult = false;
            Close();
        }
    }
}

using System.Windows;
using System.Windows.Input;
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

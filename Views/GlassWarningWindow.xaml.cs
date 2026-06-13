using System.Windows;
using System.Windows.Input;
using TwoTo1Screen.Services;

namespace TwoTo1Screen.Views
{
    public partial class GlassWarningWindow : Window
    {
        public GlassWarningWindow()
        {
            InitializeComponent();
            SourceInitialized += (_, __) => ThemeManager.ApplyWindowChrome(this, RootBorder, null);
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                try { DragMove(); } catch { }
            }
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

using System.Windows;
using System.Windows.Input;
using TwoTo1Screen.Services;

namespace TwoTo1Screen.Views
{
    /// <summary>Full-screen, non-destructive preview of a store theme.</summary>
    public partial class ThemePreviewWindow : Window
    {
        private readonly ThemeDefinition _theme;

        public ThemePreviewWindow(ThemeDefinition theme)
        {
            _theme = theme;
            InitializeComponent();

            // Scope the theme to this window only — global app resources are untouched.
            ThemeManager.ApplyToDictionary(Resources, theme.Palette);

            ThemeTitle.Text = theme.Name;
            ThemeTagline.Text = theme.Tagline + (theme.Animated ? "  ·  анимированная тема" : "");
            PreviewBadge.Text = $"Предпросмотр темы «{theme.Name}»";

            Loaded += (_, __) => ThemeManager.BuildAnimatedBackdrop(BackdropHost, theme.Animated ? theme : null);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
                return;
            }
            base.OnKeyDown(e);
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}

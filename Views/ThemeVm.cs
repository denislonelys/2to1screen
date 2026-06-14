using System.ComponentModel;
using System.Windows.Media;
using TwoTo1Screen.Services;

namespace TwoTo1Screen.Views
{
    /// <summary>View-model for a theme tile in the store (precomputed preview brushes).</summary>
    public sealed class ThemeVm : INotifyPropertyChanged
    {
        public string Id { get; }
        public string Name { get; }
        public string Author { get; }
        public string Mode { get; }
        public bool IsCustom { get; }

        public Brush BgBrush { get; }
        public Brush SurfaceBrush { get; }
        public Brush AccentBrush { get; }
        public Brush TextBrush { get; }
        public Brush MutedBrush { get; }

        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            set { if (_isActive != value) { _isActive = value; PropertyChanged?.Invoke(this, ActiveArgs); } }
        }
        private static readonly PropertyChangedEventArgs ActiveArgs = new PropertyChangedEventArgs(nameof(IsActive));

        public string AuthorLine =>
            string.IsNullOrWhiteSpace(Author) ? (Mode == "light" ? "светлая" : "тёмная")
                                              : $"{Author}";

        public ThemeVm(ThemePalette p, bool isCustom = false)
        {
            Id = p.Id; Name = p.Name; Author = p.Author; Mode = p.Mode; IsCustom = isCustom;
            BgBrush = Freeze(new SolidColorBrush(ThemeService.Hex(p.Bg, Colors.Black)));
            SurfaceBrush = Freeze(new SolidColorBrush(ThemeService.Hex(p.Surface, Colors.DimGray)));
            AccentBrush = Freeze(new SolidColorBrush(ThemeService.Hex(p.Accent, Colors.SteelBlue)));
            TextBrush = Freeze(new SolidColorBrush(ThemeService.Hex(p.Text, Colors.White)));
            MutedBrush = Freeze(new SolidColorBrush(ThemeService.Hex(p.TextMuted, Colors.Gray)));
        }

        private static Brush Freeze(Brush b) { if (b.CanFreeze) b.Freeze(); return b; }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}

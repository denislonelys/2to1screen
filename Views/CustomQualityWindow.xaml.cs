using System;
using System.Windows;
using System.Windows.Input;
using TwoTo1Screen.Services;

namespace TwoTo1Screen.Views
{
    public partial class CustomQualityWindow : Window
    {
        private readonly int _maxW;
        private readonly int _maxH;
        private bool _loading = true;

        public int ResultWidth { get; private set; }
        public int ResultHeight { get; private set; }
        public SaveFormat ResultFormat { get; private set; }
        public int ResultJpegQuality { get; private set; }

        public CustomQualityWindow(int maxW, int maxH, AppSettings settings)
        {
            InitializeComponent();

            _maxW = Math.Max(1, maxW);
            _maxH = Math.Max(1, maxH);

            MaxInfo.Text = $"Максимум для выбранного монитора: {_maxW}×{_maxH}.";

            SourceInitialized += (_, __) =>
            {
                ThemeManager.ApplyWindowChrome(this, RootBorder, null);
            };

            // initial scale from saved width (relative to max)
            int initialScale = 100;
            if (settings.CustomWidth > 0)
                initialScale = (int)Math.Round(settings.CustomWidth * 100.0 / _maxW);
            initialScale = Math.Clamp(initialScale, 10, 100);

            ScaleSlider.Value = initialScale;
            JpegSlider.Value = Math.Clamp(settings.CustomJpegQuality, 40, 100);

            if (settings.CustomFormat == SaveFormat.Jpeg)
                FmtJpeg.IsChecked = true;
            else
                FmtPng.IsChecked = true;

            _loading = false;
            UpdateScaleLabels();
            UpdateJpegLabel();
            UpdateJpegPanel();
        }

        private void UpdateScaleLabels()
        {
            int scale = (int)ScaleSlider.Value;
            int w = Math.Max(1, (int)Math.Round(_maxW * scale / 100.0));
            int h = Math.Max(1, (int)Math.Round(_maxH * scale / 100.0));
            ScaleValue.Text = $"{scale}%";
            ResultRes.Text = $"Итоговое разрешение: {w}×{h} пикс.";
        }

        private void UpdateJpegLabel()
        {
            JpegValue.Text = $"{(int)JpegSlider.Value}%";
        }

        private void UpdateJpegPanel()
        {
            bool jpeg = FmtJpeg.IsChecked == true;
            JpegPanel.IsEnabled = jpeg;
            JpegPanel.Opacity = jpeg ? 1.0 : 0.4;
        }

        private void ScaleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_loading) return;
            UpdateScaleLabels();
        }

        private void JpegSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_loading) return;
            UpdateJpegLabel();
        }

        private void Fmt_Checked(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            UpdateJpegPanel();
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                try { DragMove(); } catch { }
            }
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            int scale = (int)ScaleSlider.Value;
            ResultWidth = Math.Max(1, (int)Math.Round(_maxW * scale / 100.0));
            ResultHeight = Math.Max(1, (int)Math.Round(_maxH * scale / 100.0));
            ResultFormat = FmtJpeg.IsChecked == true ? SaveFormat.Jpeg : SaveFormat.Png;
            ResultJpegQuality = (int)JpegSlider.Value;
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

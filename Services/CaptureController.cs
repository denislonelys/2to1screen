using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace TwoTo1Screen.Services
{
    /// <summary>
    /// Coordinates a single-monitor capture: grabs the configured monitor,
    /// copies to clipboard (on the UI thread), plays a shutter sound and
    /// raises the <see cref="Captured"/> event.
    /// </summary>
    internal static class CaptureController
    {
        /// <summary>Raised after a capture; argument is the saved file path (may be null).</summary>
        public static event Action<string?>? Captured;

        public static void DoCapture(AppSettings s)
        {
            try
            {
                var result = ScreenshotService.Capture(s);
                if (result.Bitmap == null)
                    return;

                if (s.SaveToClipboard)
                {
                    BitmapSource source = ToBitmapSource(result.Bitmap);
                    var app = System.Windows.Application.Current;
                    if (app != null)
                    {
                        app.Dispatcher.Invoke(() =>
                        {
                            try { System.Windows.Clipboard.SetImage(source); }
                            catch { /* clipboard may be locked by another app */ }
                        });
                    }
                }

                result.Bitmap.Dispose();

                if (s.ShutterSound)
                {
                    ShutterSound.Play();
                }

                Captured?.Invoke(result.FilePath);
            }
            catch
            {
                // never let a capture crash the hook thread
            }
        }

        public static BitmapSource ToBitmapSource(System.Drawing.Bitmap bmp)
        {
            IntPtr hBitmap = bmp.GetHbitmap();
            try
            {
                BitmapSource src = Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap, IntPtr.Zero, Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                src.Freeze();
                return src;
            }
            finally
            {
                Native.DeleteObject(hBitmap);
            }
        }
    }
}

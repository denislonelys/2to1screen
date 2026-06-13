using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using static TwoTo1Screen.Services.Native;

namespace TwoTo1Screen.Services
{
    internal static class ScreenshotService
    {
        public class CaptureResult
        {
            public Bitmap? Bitmap;
            public string? FilePath;
        }

        public static CaptureResult Capture(AppSettings s)
        {
            var monitors = MonitorService.GetMonitors();
            if (monitors.Count == 0)
                return new CaptureResult();

            var mon = MonitorService.Resolve(s, monitors);

            Bitmap raw = CaptureRegion(mon.Left, mon.Top, mon.Width, mon.Height);
            Bitmap final = ApplyQuality(raw, s, mon);
            if (!ReferenceEquals(final, raw))
                raw.Dispose();

            string? path = null;
            if (s.SaveToFile)
                path = SaveBitmap(final, s);

            return new CaptureResult { Bitmap = final, FilePath = path };
        }

        private static Bitmap CaptureRegion(int x, int y, int width, int height)
        {
            width = Math.Max(1, width);
            height = Math.Max(1, height);

            IntPtr screenDC = GetDC(IntPtr.Zero);
            IntPtr memDC = CreateCompatibleDC(screenDC);
            IntPtr hBmp = CreateCompatibleBitmap(screenDC, width, height);
            IntPtr old = SelectObject(memDC, hBmp);

            BitBlt(memDC, 0, 0, width, height, screenDC, x, y, SRCCOPY | CAPTUREBLT);

            SelectObject(memDC, old);
            Bitmap bmp = Image.FromHbitmap(hBmp);

            DeleteObject(hBmp);
            DeleteDC(memDC);
            ReleaseDC(IntPtr.Zero, screenDC);
            return bmp;
        }

        private static Bitmap ApplyQuality(Bitmap src, AppSettings s, MonitorEntry mon)
        {
            int targetW, targetH;

            if (s.Quality == QualityPreset.Custom)
            {
                targetW = s.CustomWidth > 0 ? Math.Min(s.CustomWidth, mon.Width) : mon.Width;
                targetH = s.CustomHeight > 0 ? Math.Min(s.CustomHeight, mon.Height) : mon.Height;
            }
            else
            {
                double scale = s.Quality switch
                {
                    QualityPreset.VeryHigh => 1.0,
                    QualityPreset.High => 1.0,
                    QualityPreset.Medium => 0.75,
                    QualityPreset.Low => 0.5,
                    _ => 1.0
                };
                targetW = Math.Max(1, (int)Math.Round(src.Width * scale));
                targetH = Math.Max(1, (int)Math.Round(src.Height * scale));
            }

            if (targetW == src.Width && targetH == src.Height)
                return src;

            var dst = new Bitmap(targetW, targetH, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(dst))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                g.DrawImage(src, 0, 0, targetW, targetH);
            }
            return dst;
        }

        private static SaveFormat FormatFor(AppSettings s) => s.Quality switch
        {
            QualityPreset.VeryHigh => SaveFormat.Png,
            QualityPreset.Custom => s.CustomFormat,
            _ => SaveFormat.Jpeg
        };

        private static long JpegQualityFor(AppSettings s) => s.Quality switch
        {
            QualityPreset.High => 92L,
            QualityPreset.Medium => 85L,
            QualityPreset.Low => 70L,
            QualityPreset.Custom => Math.Clamp(s.CustomJpegQuality, 1, 100),
            _ => 95L
        };

        public static string SaveBitmap(Bitmap bmp, AppSettings s)
        {
            Directory.CreateDirectory(s.SaveFolder);
            var fmt = FormatFor(s);
            string ext = fmt == SaveFormat.Png ? "png" : "jpg";
            string file = Path.Combine(s.SaveFolder, $"2to1_{DateTime.Now:yyyy-MM-dd_HH-mm-ss-fff}.{ext}");

            if (fmt == SaveFormat.Png)
            {
                bmp.Save(file, ImageFormat.Png);
            }
            else
            {
                ImageCodecInfo encoder = ImageCodecInfo.GetImageEncoders()
                    .First(c => c.FormatID == ImageFormat.Jpeg.Guid);
                using var ep = new EncoderParameters(1);
                ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, JpegQualityFor(s));
                bmp.Save(file, encoder, ep);
            }
            return file;
        }
    }
}

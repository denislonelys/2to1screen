using System;
using System.IO;
using System.Media;
using System.Windows;

namespace TwoTo1Screen.Services
{
    /// <summary>
    /// Plays the bundled camera-shutter sound. Uses an embedded WAV played
    /// through <see cref="SoundPlayer"/> so it is audible regardless of the
    /// user's Windows sound scheme (the old code relied on SystemSounds, which
    /// is silent when the scheme is set to "No Sounds").
    /// </summary>
    internal static class ShutterSound
    {
        private static SoundPlayer? _player;
        private static bool _tried;

        private static SoundPlayer? Player()
        {
            if (_player != null || _tried) return _player;
            _tried = true;
            try
            {
                var uri = new Uri("pack://application:,,,/assets/shutter.wav");
                var info = System.Windows.Application.GetResourceStream(uri);
                if (info != null)
                {
                    // Copy to a private memory stream so the player owns the data.
                    var ms = new MemoryStream();
                    using (var s = info.Stream) s.CopyTo(ms);
                    ms.Position = 0;
                    var p = new SoundPlayer(ms);
                    p.Load();          // decode once into memory
                    _player = p;
                }
            }
            catch
            {
                _player = null;
            }
            return _player;
        }

        public static void Play()
        {
            try
            {
                var p = Player();
                if (p != null)
                {
                    p.Play();          // async, replays from the in-memory buffer
                    return;
                }
            }
            catch { }

            // Fallback if the resource could not be loaded for any reason.
            try { SystemSounds.Asterisk.Play(); } catch { }
        }
    }
}

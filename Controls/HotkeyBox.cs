using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TwoTo1Screen.Services;

namespace TwoTo1Screen.Controls
{
    /// <summary>
    /// A button that records a global hot-key combination. Click it, then press
    /// the desired keys (e.g. Win+F12). Esc cancels, Backspace/Delete clears.
    /// </summary>
    public class HotkeyBox : Button
    {
        private bool _recording;
        private Hotkey _value = new Hotkey();

        public event Action<Hotkey>? ValueChanged;

        /// <summary>When set, the hot-key may be cleared (used for the optional toggle key).</summary>
        public bool AllowEmpty { get; set; } = true;

        public Hotkey Value
        {
            get => _value;
            set
            {
                _value = value ?? new Hotkey();
                UpdateText();
            }
        }

        public HotkeyBox()
        {
            Focusable = true;
            Cursor = Cursors.Hand;
            UpdateText();
            LostFocus += (_, __) => StopRecording(false);
        }

        protected override void OnClick()
        {
            base.OnClick();
            if (!_recording)
                StartRecording();
        }

        private void StartRecording()
        {
            _recording = true;
            Content = "Нажмите клавиши…";
            Keyboard.Focus(this);
        }

        private void StopRecording(bool _)
        {
            if (!_recording) return;
            _recording = false;
            UpdateText();
        }

        private void UpdateText()
        {
            Content = _value.IsSet ? _value.Display() : "— не назначено —";
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (_recording && TryCapture(e))
            {
                e.Handled = true;
                return;
            }
            base.OnPreviewKeyDown(e);
        }

        protected override void OnPreviewKeyUp(KeyEventArgs e)
        {
            // PrintScreen typically only surfaces on key-up in WPF.
            Key k = e.Key == Key.System ? e.SystemKey : e.Key;
            if (_recording && k == Key.Snapshot && TryCapture(e))
            {
                e.Handled = true;
                return;
            }
            base.OnPreviewKeyUp(e);
        }

        private bool TryCapture(KeyEventArgs e)
        {
            Key key = e.Key == Key.System ? e.SystemKey : e.Key;

            if (key == Key.Escape)
            {
                StopRecording(false);
                return true;
            }

            if ((key == Key.Back || key == Key.Delete) && AllowEmpty)
            {
                _value = new Hotkey();
                _recording = false;
                UpdateText();
                ValueChanged?.Invoke(_value);
                return true;
            }

            if (IsModifierKey(key))
                return false; // wait for the main key

            uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);
            if (vk == 0) return false;

            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
            bool alt = (Keyboard.Modifiers & ModifierKeys.Alt) != 0;
            bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
            bool win = Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin);

            _value = new Hotkey(vk, win, ctrl, alt, shift);
            _recording = false;
            UpdateText();
            ValueChanged?.Invoke(_value);
            return true;
        }

        private static bool IsModifierKey(Key k) =>
            k == Key.LeftCtrl || k == Key.RightCtrl ||
            k == Key.LeftAlt || k == Key.RightAlt ||
            k == Key.LeftShift || k == Key.RightShift ||
            k == Key.LWin || k == Key.RWin ||
            k == Key.System;
    }
}

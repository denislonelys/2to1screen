using System;
using System.Collections.Generic;
using System.Text;

namespace TwoTo1Screen.Services
{
    /// <summary>
    /// A serializable global hot-key: a main virtual key plus modifier flags
    /// (Win / Ctrl / Alt / Shift). Used both for the capture key and for the
    /// global enable/disable toggle.
    /// </summary>
    public sealed class Hotkey
    {
        public bool Win { get; set; }
        public bool Ctrl { get; set; }
        public bool Alt { get; set; }
        public bool Shift { get; set; }

        /// <summary>Main virtual-key code. 0 means "not assigned".</summary>
        public uint Vk { get; set; }

        public Hotkey() { }

        public Hotkey(uint vk, bool win = false, bool ctrl = false, bool alt = false, bool shift = false)
        {
            Vk = vk; Win = win; Ctrl = ctrl; Alt = alt; Shift = shift;
        }

        public bool IsSet => Vk != 0;

        public Hotkey Clone() => new Hotkey(Vk, Win, Ctrl, Alt, Shift);

        public static Hotkey PrintScreen() => new Hotkey(0x2C);

        public bool ModifiersOnly =>
            Vk == 0x10 || Vk == 0x11 || Vk == 0x12 ||           // shift / ctrl / alt
            Vk == 0xA0 || Vk == 0xA1 || Vk == 0xA2 || Vk == 0xA3 || // l/r shift, l/r ctrl
            Vk == 0xA4 || Vk == 0xA5 || Vk == 0x5B || Vk == 0x5C;   // l/r alt, l/r win

        /// <summary>Human readable form, e.g. "Win + F12", "Print Screen", "—".</summary>
        public string Display()
        {
            if (!IsSet) return "—";
            var parts = new List<string>(5);
            if (Win) parts.Add("Win");
            if (Ctrl) parts.Add("Ctrl");
            if (Alt) parts.Add("Alt");
            if (Shift) parts.Add("Shift");
            parts.Add(KeyName(Vk));
            return string.Join(" + ", parts);
        }

        public override string ToString() => Display();

        public static string KeyName(uint vk)
        {
            switch (vk)
            {
                case 0x2C: return "Print Screen";
                case 0x20: return "Space";
                case 0x0D: return "Enter";
                case 0x09: return "Tab";
                case 0x08: return "Backspace";
                case 0x1B: return "Esc";
                case 0x2D: return "Insert";
                case 0x2E: return "Delete";
                case 0x24: return "Home";
                case 0x23: return "End";
                case 0x21: return "Page Up";
                case 0x22: return "Page Down";
                case 0x25: return "←";
                case 0x26: return "↑";
                case 0x27: return "→";
                case 0x28: return "↓";
                case 0x13: return "Pause";
                case 0x91: return "Scroll Lock";
                case 0xBC: return ",";
                case 0xBE: return ".";
                case 0xBF: return "/";
                case 0xBA: return ";";
                case 0xDE: return "'";
                case 0xDB: return "[";
                case 0xDD: return "]";
                case 0xDC: return "\\";
                case 0xC0: return "`";
                case 0xBD: return "-";
                case 0xBB: return "=";
            }

            if (vk >= 0x30 && vk <= 0x39) return ((char)vk).ToString();            // 0-9
            if (vk >= 0x41 && vk <= 0x5A) return ((char)vk).ToString();            // A-Z
            if (vk >= 0x70 && vk <= 0x87) return "F" + (vk - 0x70 + 1);            // F1-F24
            if (vk >= 0x60 && vk <= 0x69) return "Num " + (vk - 0x60);            // numpad 0-9

            return "0x" + vk.ToString("X2");
        }
    }
}

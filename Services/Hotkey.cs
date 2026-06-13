using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Input;

namespace TwoTo1Screen.Services
{
    /// <summary>A keyboard shortcut: modifiers + a main virtual-key.</summary>
    public sealed class Hotkey
    {
        public bool Ctrl, Alt, Shift, Win;
        public uint Vk;

        public bool IsEmpty => Vk == 0;

        public static Hotkey FromKey(Key key, ModifierKeys mods)
        {
            uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);
            return new Hotkey
            {
                Vk = vk,
                Ctrl = mods.HasFlag(ModifierKeys.Control),
                Alt = mods.HasFlag(ModifierKeys.Alt),
                Shift = mods.HasFlag(ModifierKeys.Shift),
                Win = mods.HasFlag(ModifierKeys.Windows),
            };
        }

        public override string ToString()
        {
            if (Vk == 0) return "";
            var sb = new StringBuilder();
            if (Ctrl) sb.Append("Ctrl+");
            if (Alt) sb.Append("Alt+");
            if (Shift) sb.Append("Shift+");
            if (Win) sb.Append("Win+");
            sb.Append(KeyName(Vk));
            return sb.ToString();
        }

        public static Hotkey? Parse(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            var hk = new Hotkey();
            var parts = s.Split('+');
            foreach (var raw in parts)
            {
                var p = raw.Trim();
                if (p.Equals("Ctrl", StringComparison.OrdinalIgnoreCase)) hk.Ctrl = true;
                else if (p.Equals("Alt", StringComparison.OrdinalIgnoreCase)) hk.Alt = true;
                else if (p.Equals("Shift", StringComparison.OrdinalIgnoreCase)) hk.Shift = true;
                else if (p.Equals("Win", StringComparison.OrdinalIgnoreCase)) hk.Win = true;
                else hk.Vk = VkFromName(p);
            }
            return hk.Vk == 0 ? null : hk;
        }

        public static string KeyName(uint vk)
        {
            try
            {
                var key = KeyInterop.KeyFromVirtualKey((int)vk);
                if (key != Key.None) return key.ToString();
            }
            catch { }
            return "0x" + vk.ToString("X2");
        }

        private static uint VkFromName(string name)
        {
            if (name.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
                uint.TryParse(name.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out var hv))
                return hv;
            if (Enum.TryParse<Key>(name, true, out var key))
                return (uint)KeyInterop.VirtualKeyFromKey(key);
            return 0;
        }
    }

    /// <summary>
    /// Registry of bindable actions. The UI lets the user middle-click any
    /// bindable control to assign a global shortcut; the keyboard hook then
    /// dispatches matching key presses to the action.
    /// </summary>
    public static class ActionRegistry
    {
        public sealed class Entry
        {
            public string Id = "";
            public string Label = "";
            public Action Run = () => { };
        }

        private static readonly Dictionary<string, Entry> _actions = new();

        public static void Register(string id, string label, Action run)
        {
            _actions[id] = new Entry { Id = id, Label = label, Run = run };
        }

        public static string LabelOf(string id) => _actions.TryGetValue(id, out var e) ? e.Label : id;

        public static void Invoke(string id)
        {
            if (_actions.TryGetValue(id, out var e))
            {
                try { e.Run(); } catch { }
            }
        }

        /// <summary>Find the action whose bound hotkey exactly matches the current key event.</summary>
        public static string? MatchAction(AppSettings s, uint vk, bool ctrl, bool alt, bool shift, bool win)
        {
            foreach (var kv in s.Hotkeys)
            {
                var hk = Hotkey.Parse(kv.Value);
                if (hk == null) continue;
                if (hk.Vk == vk && hk.Ctrl == ctrl && hk.Alt == alt && hk.Shift == shift && hk.Win == win)
                    return kv.Key;
            }
            return null;
        }
    }
}

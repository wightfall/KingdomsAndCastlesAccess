using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace KCAccess.Core
{
    /// <summary>A key with modifiers, stored as text like "Ctrl+Shift+O". Key names are Unity KeyCode names.</summary>
    public struct Chord : IEquatable<Chord>
    {
        public string Key;
        public bool Ctrl, Shift, Alt;

        public Chord(string key, bool ctrl = false, bool shift = false, bool alt = false)
        {
            Key = key;
            Ctrl = ctrl;
            Shift = shift;
            Alt = alt;
        }

        public bool IsEmpty => string.IsNullOrEmpty(Key);

        public static bool TryParse(string text, out Chord chord)
        {
            chord = default;
            if (string.IsNullOrWhiteSpace(text)) return false;
            var c = new Chord();
            foreach (var raw in text.Split('+'))
            {
                string p = raw.Trim();
                if (p.Length == 0) return false;
                switch (p.ToLowerInvariant())
                {
                    case "ctrl":
                    case "control": c.Ctrl = true; break;
                    case "shift": c.Shift = true; break;
                    case "alt": c.Alt = true; break;
                    default:
                        if (c.Key != null) return false; // two keys
                        c.Key = p;
                        break;
                }
            }
            if (c.Key == null) return false;
            chord = c;
            return true;
        }

        public override string ToString() => (Ctrl ? "Ctrl+" : "") + (Shift ? "Shift+" : "") + (Alt ? "Alt+" : "") + Key;

        /// <summary>How the chord is spoken: "Control Shift O", "Backslash", "Page Down".</summary>
        public string Spoken() => (Ctrl ? "Control " : "") + (Shift ? "Shift " : "") + (Alt ? "Alt " : "") + KeyName(Key);

        public static string KeyName(string key)
        {
            if (string.IsNullOrEmpty(key)) return "none";
            switch (key)
            {
                case "LeftBracket": return "open bracket";
                case "RightBracket": return "close bracket";
                case "Backslash": return "Backslash";
                case "Slash": return "Slash";
                case "Semicolon": return "Semicolon";
                case "Quote": return "Apostrophe";
                case "Comma": return "Comma";
                case "Period": return "Period";
                case "Minus": return "Minus";
                case "Equals": return "Equals";
                case "BackQuote": return "Grave accent";
                case "Return": return "Enter";
            }
            var m = Regex.Match(key, "^Alpha([0-9])$");
            if (m.Success) return m.Groups[1].Value;
            m = Regex.Match(key, "^Keypad([0-9])$");
            if (m.Success) return "Numpad " + m.Groups[1].Value;
            return TextUtil.Humanize(key);
        }

        public bool Equals(Chord o) => string.Equals(Key, o.Key, StringComparison.OrdinalIgnoreCase) && Ctrl == o.Ctrl && Shift == o.Shift && Alt == o.Alt;
        public override bool Equals(object obj) => obj is Chord c && Equals(c);
        public override int GetHashCode() => (Key ?? "").ToLowerInvariant().GetHashCode() ^ (Ctrl ? 1 : 0) ^ (Shift ? 2 : 0) ^ (Alt ? 4 : 0);
    }

    public enum BindingScope
    {
        /// <summary>Works everywhere (menus, dialogs and the map).</summary>
        Global,
        /// <summary>Works on the map only.</summary>
        Map
    }

    public sealed class BindingDef
    {
        public readonly string Id;
        public readonly string Name;
        public readonly Chord Default;
        public readonly BindingScope Scope;

        public BindingDef(string id, string name, string def, BindingScope scope)
        {
            Id = id;
            Name = name;
            Chord.TryParse(def, out Default);
            Scope = scope;
        }
    }

    /// <summary>The mod's rebindable keys, their current chords and conflict checks.</summary>
    public sealed class Bindings
    {
        public static readonly List<BindingDef> Defs = new List<BindingDef>
        {
            new BindingDef("Settings", "Mod settings", "Ctrl+Shift+O", BindingScope.Global),
            new BindingDef("KeyList", "Key list", "Shift+F1", BindingScope.Global),
            new BindingDef("Mute", "Sound cues on or off", "Ctrl+Shift+M", BindingScope.Global),
            new BindingDef("Redetect", "Search for the screen reader again", "Ctrl+Shift+F5", BindingScope.Global),
            new BindingDef("Reset", "Reset the mod", "Ctrl+Shift+F10", BindingScope.Global),
            new BindingDef("Report", "Keyboard report", "Ctrl+Shift+F11", BindingScope.Global),
            new BindingDef("TileInfo", "Describe the tile", "I", BindingScope.Map),
            new BindingDef("BuildingDetails", "Details of the selected building", "Shift+I", BindingScope.Map),
            new BindingDef("Survey", "Survey the area", "O", BindingScope.Map),
            new BindingDef("Coordinates", "Cursor coordinates", "G", BindingScope.Map),
            new BindingDef("Date", "Date, season and weather", "T", BindingScope.Map),
            new BindingDef("Status", "Kingdom status", "K", BindingScope.Map),
            new BindingDef("Log", "Notification history", "L", BindingScope.Map),
            new BindingDef("LastNotification", "Repeat the last notification", "Shift+L", BindingScope.Map),
            new BindingDef("BuildMenu", "Build menu", "B", BindingScope.Map),
            new BindingDef("Keep", "Jump to your keep", "Home", BindingScope.Map),
            new BindingDef("SelectedBuilding", "Jump to the selected building", "End", BindingScope.Map),
            new BindingDef("PrevCategory", "Previous scan category", "PageUp", BindingScope.Map),
            new BindingDef("NextCategory", "Next scan category", "PageDown", BindingScope.Map),
            new BindingDef("PrevItem", "Previous target in the category", "LeftBracket", BindingScope.Map),
            new BindingDef("NextItem", "Next target in the category", "RightBracket", BindingScope.Map),
            new BindingDef("JumpTarget", "Jump to the target", "Backslash", BindingScope.Map),
            new BindingDef("WhereTarget", "Where is the target", "Shift+Backslash", BindingScope.Map),
            new BindingDef("Walk", "Walk to the target", "N", BindingScope.Map),
            new BindingDef("WalkStraight", "Walk to the target in a straight line", "Ctrl+N", BindingScope.Map),
            new BindingDef("Beacon", "Target beacon on or off", "Shift+N", BindingScope.Map),
            new BindingDef("ChopMode", "Chop trees mode on or off", "Shift+C", BindingScope.Map),
            new BindingDef("Validity", "While placing: why the spot is valid or not", "V", BindingScope.Map),
            new BindingDef("StackMore", "Castle blocks: build one more level with each placement", "Shift+PageUp", BindingScope.Map),
            new BindingDef("StackLess", "Castle blocks: build one level fewer with each placement", "Shift+PageDown", BindingScope.Map),
            new BindingDef("MoveSoldiers", "Send selected soldiers to the cursor", "M", BindingScope.Map),
            new BindingDef("Alert", "Respond to the nearest alert (exclamation mark: advisor news, waiting envoy, stopped cart, ship)", "Ctrl+E", BindingScope.Map),
        };

        /// <summary>Keys the mod always uses for itself; they cannot be given to another action.</summary>
        public static readonly List<KeyValuePair<Chord, string>> Fixed = BuildFixed();

        private readonly Dictionary<string, Chord> current = new Dictionary<string, Chord>();

        public Bindings()
        {
            Reset();
        }

        public void Reset()
        {
            current.Clear();
            foreach (var d in Defs) current[d.Id] = d.Default;
        }

        public static BindingDef Def(string id) => Defs.Find(d => d.Id == id);

        public Chord Get(string id) => current.TryGetValue(id, out var c) ? c : default;

        public void Set(string id, Chord chord)
        {
            if (Def(id) == null) throw new ArgumentException("unknown binding " + id);
            current[id] = chord;
        }

        /// <summary>Spoken key for an action ("Control Shift O"), used in help texts.</summary>
        public string Spoken(string id) => Get(id).Spoken();

        /// <summary>
        /// Why <paramref name="chord"/> cannot be used for <paramref name="id"/>, or null when it is free.
        /// <paramref name="gameUse"/> names the game's own action for a chord (or null).
        /// </summary>
        public string Conflict(string id, Chord chord, Func<Chord, string> gameUse = null)
        {
            var def = Def(id);
            if (def == null) return "unknown action";
            if (chord.IsEmpty) return "no key";
            foreach (var f in Fixed)
                if (f.Key.Equals(chord)) return "it is used by the mod for " + f.Value;
            foreach (var other in Defs)
            {
                if (other.Id == id) continue;
                // Map keys only clash with map and global keys; global keys clash with everything.
                bool overlap = def.Scope == BindingScope.Global || other.Scope == BindingScope.Global || def.Scope == other.Scope;
                if (overlap && Get(other.Id).Equals(chord)) return "it is used by the mod for " + other.Name;
            }
            string game = gameUse != null ? gameUse(chord) : null;
            if (!string.IsNullOrEmpty(game)) return "it is the game's key for " + game;
            return null;
        }

        /// <summary>"Id=Ctrl+Shift+O" lines for the ones that differ from the defaults.</summary>
        public string Serialize()
        {
            var lines = new List<string>();
            foreach (var d in Defs)
            {
                var c = Get(d.Id);
                if (!c.Equals(d.Default)) lines.Add(d.Id + "=" + c);
            }
            return string.Join(";", lines.ToArray());
        }

        /// <summary>Applies saved changes; unknown or broken entries are ignored.</summary>
        public void Load(string text)
        {
            Reset();
            if (string.IsNullOrEmpty(text)) return;
            foreach (var part in text.Split(';'))
            {
                int eq = part.IndexOf('=');
                if (eq <= 0) continue;
                string id = part.Substring(0, eq).Trim();
                if (Def(id) == null) continue;
                if (Chord.TryParse(part.Substring(eq + 1), out var c)) current[id] = c;
            }
        }

        private static List<KeyValuePair<Chord, string>> BuildFixed()
        {
            var l = new List<KeyValuePair<Chord, string>>();
            void Add(string chord, string what)
            {
                Chord.TryParse(chord, out var c);
                l.Add(new KeyValuePair<Chord, string>(c, what));
            }
            foreach (var arrow in new[] { "UpArrow", "DownArrow", "LeftArrow", "RightArrow" })
            {
                Add(arrow, "moving");
                Add("Shift+" + arrow, "moving 5 tiles");
                Add("Ctrl+" + arrow, "jumping to the next change");
            }
            Add("Return", "selecting and activating");
            Add("Shift+Return", "selecting soldiers and marking areas");
            Add("Ctrl+Shift+Return", "adding soldiers to the selection");
            Add("KeypadEnter", "activating");
            Add("Escape", "going back");
            Add("Tab", "moving between controls");
            Add("Shift+Tab", "moving between controls");
            Add("F1", "help");
            Add("F5", "repeating");
            Add("F6", "moving between panels");
            Add("Shift+F6", "moving between panels");
            Add("Ctrl+R", "reading the whole screen");
            Add("Ctrl+M", "exploring the map from the map setup screen");
            Add("Backspace", "deleting while typing");
            for (int i = 1; i <= 9; i++)
            {
                Add("Ctrl+Alpha" + i, "jumping to bookmark " + i);
                Add("Ctrl+Shift+Alpha" + i, "storing bookmark " + i);
                Add("Alt+Alpha" + i, "making bookmark " + i + " the target");
            }
            return l;
        }
    }
}

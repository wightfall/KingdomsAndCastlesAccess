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
        public string Spoken() => (Ctrl ? Loc.T("Control") + " " : "") + (Shift ? Loc.T("Shift") + " " : "") + (Alt ? Loc.T("Alt") + " " : "") + KeyName(Key);

        public static string KeyName(string key)
        {
            if (string.IsNullOrEmpty(key)) return Loc.T("none");
            switch (key)
            {
                case "LeftBracket": return Loc.T("open bracket");
                case "RightBracket": return Loc.T("close bracket");
                case "Backslash": return Loc.T("Backslash");
                case "Slash": return Loc.T("Slash");
                case "Semicolon": return Loc.T("Semicolon");
                case "Quote": return Loc.T("Apostrophe");
                case "Comma": return Loc.T("Comma");
                case "Period": return Loc.T("Period");
                case "Minus": return Loc.T("Minus");
                case "Equals": return Loc.T("Equals");
                case "BackQuote": return Loc.T("Grave accent");
                case "Return": return Loc.T("Enter");
                case "KeypadEnter": return Loc.T("Numpad Enter");
                case "Space": return Loc.T("Space");
                case "Tab": return Loc.T("Tab");
                case "Escape": return Loc.T("Escape");
                case "Backspace": return Loc.T("Backspace");
                case "Delete": return Loc.T("Delete");
                case "Insert": return Loc.T("Insert");
                case "Home": return Loc.T("Home");
                case "End": return Loc.T("End");
                case "PageUp": return Loc.T("Page Up");
                case "PageDown": return Loc.T("Page Down");
                case "UpArrow": return Loc.T("Up Arrow");
                case "DownArrow": return Loc.T("Down Arrow");
                case "LeftArrow": return Loc.T("Left Arrow");
                case "RightArrow": return Loc.T("Right Arrow");
            }
            var m = Regex.Match(key, "^Alpha([0-9])$");
            if (m.Success) return m.Groups[1].Value;
            m = Regex.Match(key, "^Keypad([0-9])$");
            if (m.Success) return Loc.F("Numpad {0}", m.Groups[1].Value);
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

        /// <summary>The action's name in the player's language.</summary>
        public string SpokenName => Loc.T(Name);

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
            new BindingDef("Settings", Loc.N("Mod settings"), "Ctrl+Shift+O", BindingScope.Global),
            new BindingDef("KeyList", Loc.N("Key list"), "Shift+F1", BindingScope.Global),
            new BindingDef("Mute", Loc.N("Sound cues on or off"), "Ctrl+Shift+M", BindingScope.Global),
            new BindingDef("Redetect", Loc.N("Search for the screen reader again"), "Ctrl+Shift+F5", BindingScope.Global),
            new BindingDef("Reset", Loc.N("Reset the mod"), "Ctrl+Shift+F10", BindingScope.Global),
            new BindingDef("Report", Loc.N("Keyboard report"), "Ctrl+Shift+F11", BindingScope.Global),
            new BindingDef("TileInfo", Loc.N("Describe the tile"), "I", BindingScope.Map),
            new BindingDef("BuildingDetails", Loc.N("Details of the selected building"), "Shift+I", BindingScope.Map),
            new BindingDef("Survey", Loc.N("Survey the area"), "O", BindingScope.Map),
            new BindingDef("Coordinates", Loc.N("Cursor coordinates"), "G", BindingScope.Map),
            new BindingDef("Date", Loc.N("Date, season and weather"), "T", BindingScope.Map),
            new BindingDef("Status", Loc.N("Kingdom status"), "K", BindingScope.Map),
            new BindingDef("Log", Loc.N("Notification history"), "L", BindingScope.Map),
            new BindingDef("LastNotification", Loc.N("Repeat the last notification"), "Shift+L", BindingScope.Map),
            new BindingDef("BuildMenu", Loc.N("Build menu"), "B", BindingScope.Map),
            new BindingDef("Keep", Loc.N("Jump to your keep"), "Home", BindingScope.Map),
            new BindingDef("SelectedBuilding", Loc.N("Jump to the selected building"), "End", BindingScope.Map),
            new BindingDef("PrevCategory", Loc.N("Previous scan category"), "PageUp", BindingScope.Map),
            new BindingDef("NextCategory", Loc.N("Next scan category"), "PageDown", BindingScope.Map),
            new BindingDef("PrevItem", Loc.N("Previous target in the category"), "LeftBracket", BindingScope.Map),
            new BindingDef("NextItem", Loc.N("Next target in the category"), "RightBracket", BindingScope.Map),
            new BindingDef("JumpTarget", Loc.N("Jump to the target"), "Backslash", BindingScope.Map),
            new BindingDef("WhereTarget", Loc.N("Where is the target"), "Shift+Backslash", BindingScope.Map),
            new BindingDef("Walk", Loc.N("Walk to the target"), "N", BindingScope.Map),
            new BindingDef("WalkStraight", Loc.N("Walk to the target in a straight line"), "Ctrl+N", BindingScope.Map),
            new BindingDef("Beacon", Loc.N("Target beacon on or off"), "Shift+N", BindingScope.Map),
            new BindingDef("ChopMode", Loc.N("Chop trees mode on or off"), "Shift+C", BindingScope.Map),
            new BindingDef("Validity", Loc.N("While placing: why the spot is valid or not"), "V", BindingScope.Map),
            new BindingDef("StackMore", Loc.N("Castle blocks: build one more level with each placement"), "Shift+PageUp", BindingScope.Map),
            new BindingDef("StackLess", Loc.N("Castle blocks: build one level fewer with each placement"), "Shift+PageDown", BindingScope.Map),
            new BindingDef("MoveSoldiers", Loc.N("Send selected soldiers to the cursor"), "M", BindingScope.Map),
            new BindingDef("Alert", Loc.N("Respond to the nearest alert (exclamation mark: advisor news, waiting envoy, stopped cart, ship)"), "Ctrl+E", BindingScope.Map),
        };

        /// <summary>Keys the mod always uses for itself; they cannot be given to another action.</summary>
        public static readonly List<KeyValuePair<Chord, FixedUse>> Fixed = BuildFixed();

        /// <summary>What a fixed key does: an English text (translated when spoken) with an optional {0} number.</summary>
        public struct FixedUse
        {
            public string Text;
            public int Number;

            public override string ToString() => Loc.F(Text, Number);
        }

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
            if (def == null) return Loc.T("unknown action");
            if (chord.IsEmpty) return Loc.T("no key");
            foreach (var f in Fixed)
                if (f.Key.Equals(chord)) return Loc.F("it is used by the mod for {0}", f.Value.ToString());
            foreach (var other in Defs)
            {
                if (other.Id == id) continue;
                // Map keys only clash with map and global keys; global keys clash with everything.
                bool overlap = def.Scope == BindingScope.Global || other.Scope == BindingScope.Global || def.Scope == other.Scope;
                if (overlap && Get(other.Id).Equals(chord)) return Loc.F("it is used by the mod for {0}", other.SpokenName);
            }
            string game = gameUse != null ? gameUse(chord) : null;
            if (!string.IsNullOrEmpty(game)) return Loc.F("it is the game's key for {0}", game);
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

        private static List<KeyValuePair<Chord, FixedUse>> BuildFixed()
        {
            var l = new List<KeyValuePair<Chord, FixedUse>>();
            void Add(string chord, string what, int number = 0)
            {
                Chord.TryParse(chord, out var c);
                l.Add(new KeyValuePair<Chord, FixedUse>(c, new FixedUse { Text = what, Number = number }));
            }
            foreach (var arrow in new[] { "UpArrow", "DownArrow", "LeftArrow", "RightArrow" })
            {
                Add(arrow, Loc.N("moving"));
                Add("Shift+" + arrow, Loc.N("moving 5 tiles"));
                Add("Ctrl+" + arrow, Loc.N("jumping to the next change"));
            }
            Add("Return", Loc.N("selecting and activating"));
            Add("Shift+Return", Loc.N("selecting soldiers and marking areas"));
            Add("Ctrl+Shift+Return", Loc.N("adding soldiers to the selection"));
            Add("KeypadEnter", Loc.N("activating"));
            Add("Escape", Loc.N("going back"));
            Add("Tab", Loc.N("moving between controls"));
            Add("Shift+Tab", Loc.N("moving between controls"));
            Add("F1", Loc.N("help"));
            Add("F5", Loc.N("repeating"));
            Add("F6", Loc.N("moving between panels"));
            Add("Shift+F6", Loc.N("moving between panels"));
            Add("Ctrl+R", Loc.N("reading the whole screen"));
            Add("Ctrl+M", Loc.N("exploring the map from the map setup screen"));
            Add("Backspace", Loc.N("deleting while typing"));
            for (int i = 1; i <= 9; i++)
            {
                Add("Ctrl+Alpha" + i, Loc.N("jumping to bookmark {0}"), i);
                Add("Ctrl+Shift+Alpha" + i, Loc.N("storing bookmark {0}"), i);
                Add("Alt+Alpha" + i, Loc.N("making bookmark {0} the target"), i);
            }
            return l;
        }
    }
}

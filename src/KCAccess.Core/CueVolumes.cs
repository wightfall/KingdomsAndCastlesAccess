using System;
using System.Collections.Generic;

namespace KCAccess.Core
{
    /// <summary>
    /// Volume of each sound cue in percent of the overall cue volume (100 = as loud as the overall volume, 0 = silent),
    /// stored as "Navigate=50;Alert=150". Cues that are not listed are at 100.
    /// </summary>
    public sealed class CueVolumes
    {
        public const int Default = 100;
        public const int Max = 200;
        public const int Step = 10;

        private readonly Dictionary<Cue, int> percent = new Dictionary<Cue, int>();

        public int Get(Cue cue) => percent.TryGetValue(cue, out int p) ? p : Default;

        /// <summary>The cue's volume as a factor of the overall volume (1 = 100 percent).</summary>
        public float Scale(Cue cue) => Get(cue) / 100f;

        public bool IsDefault(Cue cue) => !percent.ContainsKey(cue);

        public bool AllDefault => percent.Count == 0;

        /// <summary>Sets the volume, kept between 0 and <see cref="Max"/>.</summary>
        public void Set(Cue cue, int value)
        {
            value = Math.Max(0, Math.Min(Max, value));
            if (value == Default) percent.Remove(cue);
            else percent[cue] = value;
        }

        /// <summary>Changes the volume by <paramref name="steps"/> of <see cref="Step"/> percent and returns the new value.</summary>
        public int Change(Cue cue, int steps)
        {
            Set(cue, Get(cue) + steps * Step);
            return Get(cue);
        }

        public void Reset() => percent.Clear();

        /// <summary>Reads "Navigate=50;Alert=150"; unknown cue names, broken numbers and empty parts are ignored.</summary>
        public void Load(string text)
        {
            percent.Clear();
            if (string.IsNullOrEmpty(text)) return;
            foreach (string part in text.Split(';'))
            {
                int eq = part.IndexOf('=');
                if (eq <= 0) continue;
                string name = part.Substring(0, eq).Trim();
                string number = part.Substring(eq + 1).Trim();
                if (!TryName(name, out Cue cue)) continue;
                if (int.TryParse(number, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int value)) Set(cue, value);
            }
        }

        /// <summary>A cue by its name, ignoring case (numbers are not names).</summary>
        private static bool TryName(string name, out Cue cue)
        {
            foreach (Cue c in Enum.GetValues(typeof(Cue)))
            {
                if (!string.Equals(c.ToString(), name, StringComparison.OrdinalIgnoreCase)) continue;
                cue = c;
                return true;
            }
            cue = default(Cue);
            return false;
        }

        /// <summary>The changed volumes in cue order ("Navigate=50;Alert=150"), empty when all are at 100.</summary>
        public string Serialize()
        {
            var parts = new List<string>();
            foreach (Cue cue in Enum.GetValues(typeof(Cue)))
                if (percent.TryGetValue(cue, out int p)) parts.Add(cue + "=" + p.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return string.Join(";", parts.ToArray());
        }
    }
}

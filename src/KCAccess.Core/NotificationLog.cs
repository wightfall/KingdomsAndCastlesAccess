using System.Collections.Generic;

namespace KCAccess.Core
{
    public enum Severity
    {
        Info,
        Warning,
        Danger
    }

    public sealed class Notification
    {
        public string Text;
        public Severity Severity;
        public int Year;
        /// <summary>Map tile the notification is about, if any.</summary>
        public GridPos? Where;

        public string Describe()
        {
            string prefix = Severity == Severity.Danger ? "Danger: " : (Severity == Severity.Warning ? "Warning: " : string.Empty);
            string year = Year > 0 ? ", year " + Year : string.Empty;
            return prefix + Text + year;
        }
    }

    /// <summary>History of kingdom notifications, newest first.</summary>
    public sealed class NotificationLog
    {
        private readonly List<Notification> items = new List<Notification>();

        public NotificationLog(int capacity = 200)
        {
            Capacity = capacity;
        }

        public int Capacity { get; }

        public IReadOnlyList<Notification> Items => items;

        public int Count => items.Count;

        public Notification Add(string text, Severity severity, int year, GridPos? where = null)
        {
            var n = new Notification { Text = TextUtil.Clean(text), Severity = severity, Year = year, Where = where };
            items.Insert(0, n);
            if (items.Count > Capacity) items.RemoveAt(items.Count - 1);
            return n;
        }

        public void Clear() => items.Clear();

        /// <summary>Classifies a game log id into a severity (used for sound cues).</summary>
        public static Severity Classify(string id, bool gameWarning, bool gameImportant)
        {
            if (id != null)
            {
                string l = id.ToLowerInvariant();
                string[] danger = { "fire", "plague", "kidnap", "dragonkill", "wolfkill", "buildingbreak", "failwarning", "starvedeath", "betray", "transportcartdestroyed" };
                foreach (var d in danger) if (l.Contains(d)) return Severity.Danger;
            }
            if (gameWarning) return Severity.Warning;
            return Severity.Info;
        }
    }
}

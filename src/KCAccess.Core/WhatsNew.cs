using System;
using System.Collections.Generic;

namespace KCAccess.Core
{
    /// <summary>
    /// What changed in this version of the mod, read in the game (the release notes are on GitHub only, in English).
    /// The lines are translated like every other text. Replace them for each release.
    /// </summary>
    public static class WhatsNew
    {
        /// <summary>The version these lines describe.</summary>
        public const string ForVersion = "1.29.1";

        public static readonly string[] Lines =
        {
            Loc.N("Guided game: a new kingdom gets spoken steps, from the keep to the buildings it needs before winter. {Status} shows the current step. Mod settings turns it off."),
            Loc.N("With a controller, help and hints name the controller buttons instead of keyboard keys."),
            Loc.N("The scanner's Foreign kingdoms category also lists the docks of other kingdoms, for merchant ships and envoys."),
            Loc.N("The pause menu's quit question starts on Back to Game, so a stray Enter does not quit."),
            Loc.N("F1 in the mod's lists names Control C and Control Shift C, which copy a line or the whole list."),
            Loc.N("Uninstall in the setup program asks whether to keep the language files you changed."),
            Loc.N("Control C, Control Shift C and Control V can no longer be given to another mod action by mistake."),
            Loc.N("{WhatsNew} reads this list again. Mod settings can turn off the announcement after an update."),
        };

        /// <summary>The title of the list.</summary>
        public static string Title(string version) => Loc.F("What's new in KCAccess {0}", version);

        /// <summary>The whole list as text for the reader, one line each (keys still as {BindingId}).</summary>
        public static string Text(string version)
        {
            var parts = new List<string> { Title(version) };
            foreach (var l in Lines) parts.Add(Loc.T(l));
            return string.Join("\n", parts.ToArray());
        }

        /// <summary>
        /// Whether to tell the player about the update: the mod ran before (a version was remembered) and that version
        /// differs. A first install says nothing.
        /// </summary>
        public static bool ShouldAnnounce(string remembered, string current) =>
            !string.IsNullOrWhiteSpace(remembered) && !string.Equals(remembered.Trim(), current, StringComparison.Ordinal);
    }
}

using KCAccess.Core;
using UnityEngine;

namespace KCAccess.Game
{
    /// <summary>
    /// Shift+F1: every mod key, one line at a time and grouped by where it works, followed by every sound cue;
    /// Enter on a sound plays it. Page Up / Page Down jump between groups.
    /// </summary>
    internal sealed class KeysMenu : ListMenu
    {
        private readonly bool fromMap;
        private readonly System.Collections.Generic.HashSet<Entry> headers = new System.Collections.Generic.HashSet<Entry>();

        internal KeysMenu(bool fromMap)
        {
            this.fromMap = fromMap;
        }

        public override string HelpId => "Keys";

        protected override string Title => Loc.T("Key list. Up and Down read one key at a time, Page Up and Page Down jump between groups, Escape closes. Sound previews are at the end");

        protected override void Build()
        {
            headers.Clear();
            foreach (var section in KeyHelp.Sections)
            {
                headers.Add(Add(Loc.T(section.Title) + ", " + Loc.P(section.Lines.Count, "{0} key", "{0} keys")));
                foreach (var line in section.Lines) Add(line.ToString());
            }
            headers.Add(Add(Loc.P(System.Linq.Enumerable.Count(CueLibrary.All), "Sound cues, {0} sound. Enter plays the sound under the cursor", "Sound cues, {0} sounds. Enter plays the sound under the cursor")));
            foreach (Cue cue in CueLibrary.All)
            {
                var c = cue;
                Add(Loc.F("Sound: {0}. {1}", CueLibrary.Name(c), CueLibrary.Meaning(c)), () => AudioCues.Preview(c));
            }
        }

        protected override bool HandleExtraKeys()
        {
            if (KInput.Plain(KeyCode.PageDown) || KInput.Plain(KeyCode.PageUp))
            {
                // Jump to the next group header (a key section or the sound section).
                int dir = KInput.Down(KeyCode.PageDown) ? 1 : -1;
                int i = List.Index;
                for (int n = 0; n < List.Count; n++)
                {
                    i += dir;
                    if (i < 0 || i >= List.Count) break;
                    if (headers.Contains(List.Items[i]))
                    {
                        List.SelectIndex(i);
                        Speak(NavResult.Moved);
                        return true;
                    }
                }
                A.Cue(Cue.Edge);
                A.Say(dir > 0 ? Loc.T("No more groups, this is the last") : Loc.T("No more groups, this is the first"));
                return true;
            }
            return false;
        }

        protected override void OnClosed()
        {
            if (fromMap) MapController.Inst.OnReturnToMap();
            else AccessController.Inst.Nav.SpeakCurrent();
        }
    }
}

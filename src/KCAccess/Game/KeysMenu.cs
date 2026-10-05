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

        internal KeysMenu(bool fromMap)
        {
            this.fromMap = fromMap;
        }

        public override string HelpId => "Keys";

        protected override string Title => "Key list. Up and Down read one key at a time, Page Up and Page Down jump between groups, Escape closes. Sound previews are at the end";

        protected override void Build()
        {
            foreach (var section in KeyHelp.Sections)
            {
                Add(section.Title + ", " + TextUtil.Plural(section.Lines.Count, "key"));
                foreach (var line in section.Lines) Add(line.ToString());
            }
            Add("Sound cues, " + TextUtil.Plural(System.Linq.Enumerable.Count(CueLibrary.All), "sound") + ". Enter plays the sound under the cursor");
            foreach (Cue cue in CueLibrary.All)
            {
                var c = cue;
                Add("Sound: " + CueLibrary.Name(c) + ". " + CueLibrary.Meaning(c), () => Preview(c));
            }
        }

        private static void Preview(Cue cue)
        {
            if (!Plugin.CfgCues.Value)
            {
                A.Say("Sound cues are off. Control Shift M turns them on.", force: true);
                return;
            }
            if (cue == Cue.Beacon)
            {
                // Show the stereo idea: a ping from the left (west) and from the right (east).
                AudioCues.PlayPanned(Cue.Beacon, -0.9f, 1f);
                Inst.Run(0.5f, () => AudioCues.PlayPanned(Cue.Beacon, 0.9f, 1f));
                return;
            }
            AudioCues.Play(cue);
        }

        protected override bool HandleExtraKeys()
        {
            if (KInput.Plain(KeyCode.PageDown) || KInput.Plain(KeyCode.PageUp))
            {
                // Group headers are the lines ending in "N keys" / the sound section header.
                int dir = KInput.Down(KeyCode.PageDown) ? 1 : -1;
                int i = List.Index;
                for (int n = 0; n < List.Count; n++)
                {
                    i += dir;
                    if (i < 0 || i >= List.Count) break;
                    string t = List.Items[i].Text;
                    if (!t.StartsWith("Sound: ") && !t.Contains(": "))
                    {
                        List.SelectIndex(i);
                        Speak(NavResult.Moved);
                        return true;
                    }
                }
                A.Cue(Cue.Edge);
                return true;
            }
            return false;
        }

        protected override void OnClosed()
        {
            if (fromMap) MapController.Inst.OnReturnToMap();
            else AccessController.Inst.Nav.SpeakCurrent();
        }

        /// <summary>Tiny scheduler for the delayed second beacon ping.</summary>
        private static Runner Inst => Runner.Get();

        private sealed class Runner : MonoBehaviour
        {
            private static Runner inst;

            internal static Runner Get()
            {
                if (inst == null)
                {
                    var go = new GameObject("KCAccessKeysMenuRunner");
                    Object.DontDestroyOnLoad(go);
                    inst = go.AddComponent<Runner>();
                }
                return inst;
            }

            internal void Run(float delay, System.Action action) => StartCoroutine(Later(delay, action));

            private System.Collections.IEnumerator Later(float delay, System.Action action)
            {
                yield return new WaitForSecondsRealtime(delay);
                action();
            }
        }
    }
}

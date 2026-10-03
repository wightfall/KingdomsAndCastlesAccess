using KCAccess.Core;

namespace KCAccess.Game
{
    /// <summary>L: browse notification history, Enter jumps the map cursor to where it happened.</summary>
    internal sealed class LogBrowser : ListMenu
    {
        public override string HelpId => "Log";

        protected override string Title => "Notifications, " + TextUtil.Plural(GameEvents.Log.Count, "message");

        protected override void Build()
        {
            foreach (var n in GameEvents.Log.Items)
            {
                var note = n;
                string text = note.Describe() + (note.Where.HasValue ? ", has location" : string.Empty);
                Add(text, note.Where.HasValue ? () =>
                {
                    Close(announce: false);
                    A.Cue(Cue.Close);
                    MapController.Inst.JumpTo(note.Where.Value);
                } : (System.Action)null);
            }
        }
    }
}

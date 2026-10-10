using KCAccess.Core;

namespace KCAccess.Game
{
    /// <summary>L: browse notification history, Enter jumps the map cursor to where it happened.</summary>
    internal sealed class LogBrowser : ListMenu
    {
        /// <summary>The notifications as listed: new ones are inserted at the top of the log while the list is open.</summary>
        private readonly System.Collections.Generic.List<Notification> shown = new System.Collections.Generic.List<Notification>();

        protected override bool HandleExtraKeys()
        {
            if (!KInput.WithShift(UnityEngine.KeyCode.Return) || !List.HasCurrent) return false;
            int idx = List.Index;
            if (idx < 0 || idx >= shown.Count) return true;
            var n = shown[idx];
            if (!n.Where.HasValue)
            {
                A.Cue(Cue.Error);
                A.Say(Loc.T("This notification has no location"));
                return true;
            }
            MapController.Inst.Nav.SetTarget(n.Where.Value, n.Text);
            Close(announce: false);
            A.Cue(Cue.Close);
            A.Say(Loc.F("Target set: {0}", MapController.Inst.Nav.Describe(MapController.Inst.CursorPos)) + ". " + KCAccess.Core.KeyHelp.Resolve(Loc.T("{Walk} walks there, {Beacon} turns on the beacon.")));
            return true;
        }

        public override string HelpId => "Log";

        protected override string Title => Loc.P(GameEvents.Log.Count, "Notifications, {0} message", "Notifications, {0} messages");

        protected override void Build()
        {
            shown.Clear();
            foreach (var n in GameEvents.Log.Items)
            {
                var note = n;
                shown.Add(note);
                string text = note.Describe() + (note.Where.HasValue ? ", " + Loc.T("has location") : string.Empty);
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

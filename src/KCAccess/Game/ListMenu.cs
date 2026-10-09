using System;
using KCAccess.Core;
using UnityEngine;

namespace KCAccess.Game
{
    /// <summary>A simple spoken list owned by the mod: Up/Down, Home/End, letters, Enter, Escape.</summary>
    internal abstract class ListMenu : IModalMenu
    {
        internal sealed class Entry
        {
            public Func<string> Label;
            public Action OnEnter;
            public string Static;

            public string Text => Label != null ? Label() : Static;
        }

        protected readonly NavList<Entry> List;
        private readonly TypeAhead typeAhead = new TypeAhead(() => Time.realtimeSinceStartup);
        private bool open = true;
        private bool announced;

        protected ListMenu()
        {
            List = new NavList<Entry>(e => e.Text, wrap: false);
        }

        public bool IsOpen => open;

        public abstract string HelpId { get; }

        protected abstract string Title { get; }

        protected abstract void Build();

        protected Entry Add(string text, Action onEnter = null)
        {
            var e = new Entry { Static = text, OnEnter = onEnter };
            AddEntry(e);
            return e;
        }

        protected Entry Add(Func<string> label, Action onEnter = null)
        {
            var e = new Entry { Label = label, OnEnter = onEnter };
            AddEntry(e);
            return e;
        }

        private readonly System.Collections.Generic.List<Entry> pending = new System.Collections.Generic.List<Entry>();

        private void AddEntry(Entry e) => pending.Add(e);

        protected void Rebuild()
        {
            pending.Clear();
            Build();
            List.SetItems(pending, keepFocus: false);
        }

        public void Close(bool announce = true)
        {
            open = false;
            if (announce)
            {
                A.Cue(Cue.Close);
                OnClosed();
            }
        }

        /// <summary>Spoken after closing; menus opened outside the map override it.</summary>
        protected virtual void OnClosed() => MapController.Inst.OnReturnToMap();

        public virtual void HandleInput()
        {
            if (!announced)
            {
                announced = true;
                Rebuild();
                A.Cue(Cue.Open);
                A.Say(TextUtil.Sentences(Title, List.Count == 0 ? Loc.T("empty") : List.CurrentLabel));
                return;
            }
            if (KInput.Plain(KeyCode.Escape))
            {
                KInput.Consume(KeyCode.Escape);
                Close();
                return;
            }
            if (KInput.Plain(KeyCode.DownArrow)) Speak(List.Next());
            else if (KInput.Plain(KeyCode.UpArrow)) Speak(List.Previous());
            else if (KInput.Plain(KeyCode.Home)) Speak(List.First());
            else if (KInput.Plain(KeyCode.End)) Speak(List.Last());
            else if (KInput.Plain(KeyCode.F5)) A.Say(List.CurrentLabel + ", " + List.PositionText, force: true);
            else if ((KInput.Plain(KeyCode.Return) || KInput.Plain(KeyCode.KeypadEnter)) && List.HasCurrent)
            {
                KInput.Consume(KeyCode.Return);
                var cur = List.Current;
                if (cur.OnEnter != null) cur.OnEnter();
                else A.Say(cur.Text, force: true);
            }
            else if (KInput.Down(KeyCode.R) && KInput.Ctrl)
            {
                var parts = new System.Collections.Generic.List<string> { Title };
                foreach (var e in List.Items) parts.Add(e.Text);
                A.Say(TextUtil.Sentences(parts.ToArray()), force: true);
            }
            else if (!HandleExtraKeys())
            {
                char? c = KInput.LetterDown();
                if (c.HasValue && !KInput.Shift)
                {
                    if (List.FindNext(typeAhead.Add(c.Value))) Speak(NavResult.Moved);
                    else A.Cue(Cue.Edge);
                }
            }
        }

        /// <summary>Override for menu specific keys. Return true when handled.</summary>
        protected virtual bool HandleExtraKeys() => false;

        protected void Speak(NavResult r)
        {
            if (r == NavResult.Empty)
            {
                A.Cue(Cue.Edge);
                A.Say(Loc.T("empty"));
                return;
            }
            A.Cue(r == NavResult.HitEdge ? Cue.Edge : Cue.Navigate);
            A.Say(List.CurrentLabel + (Plugin.CfgPositions.Value && List.Count > 1 ? ", " + List.PositionText : string.Empty));
        }
    }
}

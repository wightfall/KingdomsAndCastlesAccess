using System;
using System.Collections.Generic;
using KCAccess.Core;
using KCAccess.UI;
using TMPro;
using UnityEngine;

namespace KCAccess.Game
{
    /// <summary>
    /// A long text read one line at a time (LongText splits it): the main menu's news, a load error's details, any
    /// text of 300 characters or more where Enter opens it, and the credits, which the game rolls past and hides line
    /// by line (CreditsUI culls everything outside its window). Up and Down read lines, Page Up and Page Down jump
    /// between groups (paragraphs, credit titles), Escape returns.
    /// </summary>
    internal sealed class TextReader : ListMenu
    {
        private readonly string title;
        private readonly Func<List<LongText.Line>> source;
        private List<LongText.Line> lines = new List<LongText.Line>();
        private readonly Action onClosed;
        private readonly bool countGroups;
        private readonly HashSet<Entry> headers = new HashSet<Entry>();

        private TextReader(string title, Func<List<LongText.Line>> source, Action onClosed, bool countGroups)
        {
            this.title = title;
            this.source = source;
            this.onClosed = onClosed;
            this.countGroups = countGroups;
        }

        public override string HelpId => "TextReader";

        protected override string Title => TextUtil.Sentences(title,
            Loc.P(lines.Count, "{0} line", "{0} lines") + (headers.Count > 1 ? ", " + Loc.P(headers.Count, "{0} group", "{0} groups") : string.Empty),
            Loc.T("Up and Down read one line at a time, Page Up and Page Down jump between groups, Escape returns"));

        protected override void Build()
        {
            lines = source() ?? new List<LongText.Line>();
            headers.Clear();
            for (int i = 0; i < lines.Count; i++)
            {
                string text = lines[i].Text;
                // A credits title says how many names follow it ("The Brave Knights, 268 names").
                if (countGroups && lines[i].StartsGroup)
                {
                    int n = LongText.LinesInGroup(lines, i);
                    if (n > 0) text += ", " + Loc.P(n, "{0} name", "{0} names");
                }
                var e = Add(text);
                if (lines[i].StartsGroup) headers.Add(e);
            }
        }

        protected override bool HandleExtraKeys() => JumpBetweenGroups(headers);

        protected override void OnClosed() => onClosed?.Invoke();

        /// <summary>Opens a long text item of a screen or panel; the focus stays on it.</summary>
        internal static bool OpenText(string caption, string raw)
        {
            var lines = LongText.Lines(raw);
            if (lines.Count == 0) return false;
            AccessController.Inst.ActiveMenu = new TextReader(caption, () => lines, () => AccessController.Inst.Nav.SpeakCurrent(), countGroups: false);
            return true;
        }

        /// <summary>
        /// The credits as the game builds them under CreditsUI.CreditsContainer, hidden lines included: titles (the
        /// container's own texts, and in the translators' part the texts beside the name groups) start groups, every
        /// other text is a name, with its role when it has one ("Peter Angstadt, Design, Programming, Art").
        /// </summary>
        internal static List<LongText.Line> CreditLines(CreditsUI ui)
        {
            var lines = new List<LongText.Line>();
            if (ui == null || ui.CreditsContainer == null) return lines;
            foreach (Transform child in ui.CreditsContainer)
            {
                var tmp = child.GetComponent<TMP_Text>();
                if (tmp != null) AddLine(lines, tmp.text, null, title: true);
                else AddNames(lines, child);
            }
            return lines;
        }

        private static void AddNames(List<LongText.Line> lines, Transform group)
        {
            // Inside the translators' part titles stand between the name groups; elsewhere a group holds names only.
            bool hasGroups = false;
            foreach (Transform c in group)
                if (c.GetComponent<TMP_Text>() == null && c.childCount > 0) hasGroups = true;
            foreach (Transform c in group)
            {
                var tmp = c.GetComponent<TMP_Text>();
                if (tmp == null)
                {
                    if (c.childCount > 0) AddNames(lines, c);
                    continue;
                }
                if (hasGroups) AddLine(lines, tmp.text, null, title: true);
                else AddLine(lines, tmp.text, RoleOf(c), title: false);
            }
        }

        /// <summary>A team member's role: the text object under the name.</summary>
        private static string RoleOf(Transform name)
        {
            foreach (Transform c in name)
                if (c.GetComponent<TMP_Text>() is TMP_Text role && TextUtil.HasContent(role.text)) return role.text;
            return null;
        }

        private static void AddLine(List<LongText.Line> lines, string text, string role, bool title)
        {
            string clean = TextUtil.Clean(text);
            if (!TextUtil.HasContent(clean)) return;
            if (role != null) clean = TextUtil.Join(", ", clean, TextUtil.Clean(role));
            lines.Add(new LongText.Line { Text = clean, StartsGroup = title });
        }

        /// <summary>
        /// The Credits screen opened: read the whole credits; Escape returns to the main menu (its Back button). The lines
        /// are collected when the reader starts, a frame later, when the translators' part is surely built.
        /// </summary>
        internal static void OpenCredits(CreditsUI ui)
        {
            if (ui == null) return;
            AccessController.Inst.ActiveMenu = new TextReader(Loc.T("Credits"), () => CreditLines(ui), () =>
            {
                if (ui != null && ui.backButton != null && ui.backButton.gameObject.activeInHierarchy) UINavigator.Click(ui.backButton.gameObject);
            }, countGroups: true);
        }
    }
}

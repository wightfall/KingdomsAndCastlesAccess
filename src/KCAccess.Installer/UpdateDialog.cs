using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using KCAccess.Core;

namespace KCAccess.Installer
{
    /// <summary>
    /// "Update available": what is new in a read-only box (focused first, so a screen reader reads it line by line with
    /// the arrow keys) and Yes / No buttons right after it. Enter answers Yes, Escape No.
    /// </summary>
    public sealed class UpdateDialog : Form
    {
        private readonly Label question = new Label();
        private readonly Label notesLabel = new Label();
        private readonly TextBox notes = new TextBox();
        private readonly Button yes = new Button();
        private readonly Button no = new Button();

        /// <param name="headline">"KCAccess 1.25.0 is available (you have 1.24.1)."</param>
        /// <param name="releaseNotes">The release's notes from GitHub (Markdown), or null when there are none.</param>
        public UpdateDialog(string headline, string releaseNotes)
        {
            Font = new Font("Segoe UI", 10f);
            Text = Loc.T("Update available");
            ClientSize = new Size(620, 440);
            MinimumSize = new Size(480, 360);
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            MinimizeBox = false;
            MaximizeBox = true;

            question.Text = headline + " " + Loc.T("Download and install it now?");
            question.SetBounds(12, 12, 596, 44);
            question.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            notesLabel.Text = Loc.T("&What's new:");
            notesLabel.AutoSize = true;
            notesLabel.Location = new Point(12, 62);

            notes.Multiline = true;
            notes.ReadOnly = true;
            notes.WordWrap = true;
            notes.ScrollBars = ScrollBars.Vertical;
            notes.BackColor = SystemColors.Window;
            notes.SetBounds(12, 86, 596, 290);
            notes.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            notes.AccessibleName = Loc.T("What's new");
            var lines = ReleaseNotes.Lines(releaseNotes);
            notes.Text = lines.Count > 0 ? string.Join(Environment.NewLine, lines.ToArray()) : Loc.T("This release has no notes.");

            yes.Text = Loc.T("&Yes");
            yes.DialogResult = DialogResult.Yes;
            yes.SetBounds(12, 388, 140, 36);
            yes.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            no.Text = Loc.T("&No");
            no.DialogResult = DialogResult.No;
            no.SetBounds(162, 388, 140, 36);
            no.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;

            // Tab order: notes, Yes, No (the question is read as the window's text).
            question.TabIndex = 0;
            notesLabel.TabIndex = 1;
            notes.TabIndex = 2;
            yes.TabIndex = 3;
            no.TabIndex = 4;
            Controls.AddRange(new Control[] { question, notesLabel, notes, yes, no });
            AcceptButton = yes;
            CancelButton = no;
            AccessibleDescription = question.Text;
            Shown += (s, e) =>
            {
                notes.Focus();
                notes.SelectionStart = 0;
                notes.SelectionLength = 0;
            };
        }

        /// <summary>Shows the question; true when the player chose Yes.</summary>
        public static bool Ask(IWin32Window owner, string headline, string releaseNotes)
        {
            using (var d = new UpdateDialog(headline, releaseNotes)) return d.ShowDialog(owner) == DialogResult.Yes;
        }
    }

    /// <summary>GitHub release notes (Markdown) as plain lines for a text box a screen reader reads line by line.</summary>
    public static class ReleaseNotes
    {
        private static readonly Regex Heading = new Regex(@"^\s{0,3}#+\s*", RegexOptions.CultureInvariant);
        private static readonly Regex Link = new Regex(@"!?\[([^\]]*)\]\(([^)]*)\)", RegexOptions.CultureInvariant);
        private static readonly Regex Bullet = new Regex(@"^(\s*)[-*+]\s+", RegexOptions.CultureInvariant);
        private static readonly Regex Rule = new Regex(@"^\s*([-*_]\s*){3,}$", RegexOptions.CultureInvariant);
        private static readonly Regex Html = new Regex(@"<[^>]+>", RegexOptions.CultureInvariant);

        /// <summary>
        /// Every line of the notes without Markdown marks: headings and bold lose their # and **, links keep their text
        /// (a bare link keeps the address), bullets become "- ", rules and runs of empty lines are dropped. Nothing is cut.
        /// </summary>
        public static List<string> Lines(string markdown)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(markdown)) return result;
            bool lastEmpty = true;
            foreach (string raw in markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            {
                string line = raw.TrimEnd();
                if (Rule.IsMatch(line) && line.Trim().Length > 0) continue;
                line = Heading.Replace(line, string.Empty);
                line = Link.Replace(line, m => m.Groups[1].Value.Trim().Length > 0 ? m.Groups[1].Value : m.Groups[2].Value);
                line = Html.Replace(line, string.Empty);
                line = line.Replace("**", string.Empty).Replace("__", string.Empty).Replace("`", string.Empty);
                var b = Bullet.Match(line);
                if (b.Success) line = b.Groups[1].Value + "- " + line.Substring(b.Length);
                if (line.Trim().Length == 0)
                {
                    if (!lastEmpty) result.Add(string.Empty);
                    lastEmpty = true;
                    continue;
                }
                result.Add(line);
                lastEmpty = false;
            }
            while (result.Count > 0 && result[result.Count - 1].Length == 0) result.RemoveAt(result.Count - 1);
            return result;
        }
    }
}

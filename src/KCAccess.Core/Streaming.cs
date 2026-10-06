using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace KCAccess.Core
{
    /// <summary>One option of the current Twitch chat vote: viewers type "#Number" in chat to vote for it.</summary>
    public sealed class VoteOption
    {
        public int Number;
        public string Title;
        public int Votes;

        public VoteOption(int number, string title, int votes = 0)
        {
            Number = number;
            Title = title;
            Votes = votes;
        }
    }

    /// <summary>Spoken texts for the game's Twitch chat voting (StreamerUI).</summary>
    public static class TwitchText
    {
        /// <summary>"2 minutes 5 seconds", "1 minute", "45 seconds", "0 seconds".</summary>
        public static string Duration(int seconds)
        {
            if (seconds < 0) seconds = 0;
            int m = seconds / 60, s = seconds % 60;
            if (m == 0) return Loc.P(s, "{0} second", "{0} seconds");
            string minutes = Loc.P(m, "{0} minute", "{0} minutes");
            return s == 0 ? minutes : minutes + " " + Loc.P(s, "{0} second", "{0} seconds");
        }

        /// <summary>"New Twitch vote, ends in 2 minutes: 1, Bountiful harvest; 2, Plague".</summary>
        public static string NewVote(IList<VoteOption> options, int seconds)
        {
            if (options == null || options.Count == 0)
                return Loc.T("No Twitch vote could start: every vote option is turned off or not possible right now");
            var parts = new List<string>();
            foreach (var o in options) parts.Add(o.Number + ", " + o.Title);
            return Loc.F("New Twitch vote, ends in {0}: {1}", Duration(seconds), string.Join("; ", parts.ToArray()));
        }

        /// <summary>"Twitch vote: 1 Bountiful harvest 3 votes, 2 Plague 1 vote, 45 seconds left".</summary>
        public static string Status(IList<VoteOption> options, int seconds)
        {
            if (options == null || options.Count == 0) return Loc.T("No Twitch vote running");
            var parts = new List<string>();
            foreach (var o in options) parts.Add(o.Number + " " + o.Title + " " + Loc.P(o.Votes, "{0} vote", "{0} votes"));
            return Loc.F("Twitch vote: {0}, {1} left", string.Join(", ", parts.ToArray()), Duration(seconds));
        }

        /// <summary>"Leading: 2, Plague, 3 votes", "Tied at 2 votes: 1, Harvest and 3, Rain", "No votes yet".</summary>
        public static string Leading(IList<VoteOption> options)
        {
            if (options == null || options.Count == 0) return Loc.T("No votes yet");
            int max = 0;
            foreach (var o in options) if (o.Votes > max) max = o.Votes;
            if (max == 0) return Loc.T("No votes yet");
            var top = new List<string>();
            foreach (var o in options) if (o.Votes == max) top.Add(o.Number + ", " + o.Title);
            if (top.Count == 1) return Loc.F("Leading: {0}, {1}", top[0], Loc.P(max, "{0} vote", "{0} votes"));
            return Loc.F("Tied at {0}: {1}", Loc.P(max, "{0} vote", "{0} votes"), string.Join(" " + Loc.T("and") + " ", top.ToArray()));
        }

        /// <summary>Said once when the vote is about to end.</summary>
        public static string Countdown(IList<VoteOption> options, int seconds) =>
            Loc.F("{0} left in the Twitch vote.", Duration(seconds)) + " " + Leading(options);

        /// <summary>"Twitch viewers chose Plague, picked by someone".</summary>
        public static string Winner(string title, string voter)
        {
            string s = Loc.F("Twitch viewers chose {0}", string.IsNullOrEmpty(title) ? Loc.T("an effect") : title);
            return string.IsNullOrEmpty(voter) ? s : s + ", " + Loc.F("picked by {0}", voter);
        }
    }

    /// <summary>
    /// Decides which Twitch chat messages are read aloud: vote messages are skipped (the vote has its own status),
    /// long messages are cut, and when chat is busy only a few messages per time window are spoken; the rest are
    /// summarised later as "and 5 more chat messages".
    /// </summary>
    public sealed class ChatFilter
    {
        // Same pattern the game uses to recognise a vote (StreamerUI.voteStringMatcher): "#2", "# 2", "2", "#2 please".
        private static readonly Regex VotePattern = new Regex(@"^#*\s*[0-9]+", RegexOptions.CultureInvariant);

        private readonly Func<double> clock;
        private readonly Queue<double> recent = new Queue<double>();
        private int dropped;

        public ChatFilter(Func<double> clock, int maxMessages = 3, double windowSeconds = 10, int maxLength = 200)
        {
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            MaxMessages = Math.Max(1, maxMessages);
            WindowSeconds = windowSeconds;
            MaxLength = Math.Max(10, maxLength);
        }

        public int MaxMessages { get; }
        public double WindowSeconds { get; }
        public int MaxLength { get; }

        /// <summary>Messages skipped because chat was too busy, not yet summarised.</summary>
        public int Dropped => dropped;

        public static bool IsVote(string message) => message != null && VotePattern.IsMatch(message);

        /// <summary>Cuts a message to the maximum length, at a word boundary when possible.</summary>
        public string Shorten(string message)
        {
            string s = TextUtil.Clean(message);
            if (s.Length <= MaxLength) return s;
            int cut = s.LastIndexOf(' ', MaxLength);
            if (cut < MaxLength / 2) cut = MaxLength;
            return s.Substring(0, cut).TrimEnd();
        }

        /// <summary>Text to speak for a chat message ("name: message"), or null when it is skipped.</summary>
        public string Accept(string username, string message)
        {
            if (!TextUtil.HasContent(message) || IsVote(message)) return null;
            string text = Shorten(message);
            if (text.Length == 0) return null;
            double now = clock();
            Expire(now);
            if (recent.Count >= MaxMessages)
            {
                dropped++;
                return null;
            }
            recent.Enqueue(now);
            string user = string.IsNullOrEmpty(username) ? Loc.T("someone") : username.Trim();
            return user + ": " + text;
        }

        /// <summary>Call regularly: once chat calms down, returns "and 4 more chat messages" (once), otherwise null.</summary>
        public string Flush()
        {
            if (dropped == 0) return null;
            double now = clock();
            Expire(now);
            if (recent.Count >= MaxMessages) return null;
            string s = Loc.P(dropped, "and {0} more chat message", "and {0} more chat messages");
            dropped = 0;
            recent.Enqueue(now);
            return s;
        }

        public void Reset()
        {
            recent.Clear();
            dropped = 0;
        }

        private void Expire(double now)
        {
            while (recent.Count > 0 && now - recent.Peek() >= WindowSeconds) recent.Dequeue();
        }
    }

    /// <summary>A caption line ready to draw: its text and how opaque it is (1 = fully visible, fades to 0).</summary>
    public struct CaptionLine
    {
        public string Text;
        public float Alpha;
    }

    /// <summary>The last few spoken lines shown on screen for stream viewers; each fades out after a few seconds.</summary>
    public sealed class Captions
    {
        private sealed class Entry
        {
            public string Text;
            public double Time;
        }

        private readonly List<Entry> lines = new List<Entry>();

        public Captions(int maxLines = 3, double lifetime = 6, double fade = 1)
        {
            MaxLines = Math.Max(1, maxLines);
            Lifetime = lifetime;
            Fade = Math.Max(0.001, Math.Min(fade, lifetime));
        }

        public int MaxLines { get; }
        public double Lifetime { get; }
        public double Fade { get; }

        /// <summary>Very long lines (a whole screen read with Control R) are cut so they do not cover the game.</summary>
        public int MaxLength { get; set; } = 300;

        public void Add(string text, double now)
        {
            string s = TextUtil.Clean(text);
            if (s.Length == 0) return;
            if (s.Length > MaxLength)
            {
                int cut = s.LastIndexOf(' ', MaxLength);
                if (cut < MaxLength / 2) cut = MaxLength;
                s = s.Substring(0, cut).TrimEnd() + " ...";
            }
            lines.Add(new Entry { Text = s, Time = now });
            while (lines.Count > MaxLines) lines.RemoveAt(0);
        }

        /// <summary>The lines still on screen, oldest first, with their opacity.</summary>
        public List<CaptionLine> Visible(double now)
        {
            lines.RemoveAll(e => now - e.Time >= Lifetime);
            var result = new List<CaptionLine>(lines.Count);
            foreach (var e in lines)
            {
                double left = Lifetime - (now - e.Time);
                float alpha = left >= Fade ? 1f : (float)Math.Max(0, left / Fade);
                result.Add(new CaptionLine { Text = e.Text, Alpha = alpha });
            }
            return result;
        }

        public int Count => lines.Count;

        public void Clear() => lines.Clear();
    }

    /// <summary>The text of the status file a streamer can show in OBS (Text source, "Read from file").</summary>
    public static class StreamOverlay
    {
        public static string Build(string kingdom, string date, string population, string gold, string lastNotification, string extra = null)
        {
            var sb = new StringBuilder();
            Line(sb, string.IsNullOrEmpty(kingdom) ? null : Loc.F("Kingdom: {0}", kingdom));
            Line(sb, date);
            Line(sb, population);
            Line(sb, gold);
            Line(sb, string.IsNullOrEmpty(lastNotification) ? null : Loc.F("Last: {0}", lastNotification));
            Line(sb, extra);
            return sb.ToString();
        }

        private static void Line(StringBuilder sb, string text)
        {
            string s = TextUtil.Clean(text);
            if (s.Length == 0) return;
            sb.Append(s).Append("\r\n");
        }
    }
}

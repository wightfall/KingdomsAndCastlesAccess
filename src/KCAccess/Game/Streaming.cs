using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using KCAccess.Core;
using KCAccess.UI;
using UnityEngine;

namespace KCAccess.Game
{
    /// <summary>
    /// The game's Twitch chat voting (Settings: Enable Twitch Chat Voting, StreamerUI). Viewers type "#2" in chat to
    /// vote for an effect; the winner is shown in a banner. Everything here was visual only: the connection result,
    /// the options and their numbers, the running vote counts and the countdown.
    /// </summary>
    internal static class Twitch
    {
        private static readonly ChatFilter chat = new ChatFilter(() => Time.realtimeSinceStartup);
        private static TwitchIRC subscribedIrc;
        private static string pending;          // announcement waiting until the result banner has closed
        private static float pendingAt;
        private static bool countdownSaid;
        private static bool noVotesRound;
        private static string awaitingChannel;  // channel name typed, connection result not known yet
        private static string bannerWinner;     // "Twitch viewers chose ..." for the result banner's title

        /// <summary>Last result, for the K status list.</summary>
        internal static string LastWinner;

        /// <summary>The voting feature is switched on in the game's settings (its panel exists).</summary>
        internal static bool Enabled => StreamerUI.inst != null && StreamerUI.inst.gameObject.activeInHierarchy;

        internal static bool Connected => Enabled && StreamerUI.inst.TwitchMode;

        internal static bool VoteRunning => Connected && StreamerUI.inst.activelyVotableUI != null && StreamerUI.inst.activelyVotableUI.Count > 0;

        /// <summary>The settings part of the panel (channel name, interval, vote count, option check boxes) is shown.</summary>
        internal static bool SettingsVisible => Enabled && StreamerUI.inst.visible;

        internal static string Translate(string term)
        {
            if (string.IsNullOrEmpty(term)) return string.Empty;
            string t = null;
            try { t = I2.Loc.LocalizationManager.GetTranslation(term); } catch { }
            if (string.IsNullOrEmpty(t))
            {
                // "VotableBountifulHarvestTitle" -> "Bountiful Harvest"
                string id = term;
                if (id.StartsWith("Votable")) id = id.Substring(7);
                if (id.EndsWith("Title")) id = id.Substring(0, id.Length - 5);
                t = TextUtil.Humanize(id);
            }
            return TextUtil.Clean(t);
        }

        internal static string TitleOf(Votable v) => v == null ? string.Empty : Translate(v.GetTitleTerm());

        internal static List<VoteOption> Options()
        {
            var list = new List<VoteOption>();
            var s = StreamerUI.inst;
            if (s == null || s.activelyVotableUI == null) return list;
            foreach (var a in s.activelyVotableUI)
            {
                if (a == null) continue;
                list.Add(new VoteOption(a.GetIndex(), TitleOf(a.GetVotable()), a.Votes != null ? a.Votes.Count : 0));
            }
            return list;
        }

        internal static int SecondsLeft()
        {
            var t = StreamerUI.inst != null ? StreamerUI.inst.voteTimer : null;
            return t == null ? 0 : Mathf.Max(0, Mathf.CeilToInt(t.Duration - t.CurrTime));
        }

        /// <summary>The K status line (Enter opens or closes the Twitch voting settings).</summary>
        internal static string StatusLine()
        {
            if (!Enabled) return Loc.T("Twitch voting is off");
            string s;
            if (!Connected) s = Loc.T("Twitch voting: not connected. Press Enter to open the Twitch voting settings and type your channel name");
            else if (VoteRunning) s = TwitchText.Status(Options(), SecondsLeft());
            else s = Loc.T("Twitch voting connected, no vote running right now");
            if (LastWinner != null) s += ". " + Loc.F("Last result: {0}", LastWinner);
            return s;
        }

        /// <summary>F6 group: the whole panel while its settings are open, otherwise the small tab with the current vote.</summary>
        internal static List<Transform> PanelRoots()
        {
            var roots = new List<Transform>();
            if (!Enabled) return roots;
            var s = StreamerUI.inst;
            if (s.visible)
            {
                roots.Add(s.transform);
                return roots;
            }
            var small = s.transform.Find("Small");
            if (small != null && UIText.IsVisible(small.gameObject) && GameScreens.HasContent(small)) roots.Add(small);
            return roots;
        }

        /// <summary>Enter on the K line: show the settings and move the keyboard into them.</summary>
        internal static void OpenSettings()
        {
            if (!Enabled)
            {
                A.Say(Loc.T("Twitch voting is off. Turn on Enable Twitch Chat Voting in the game's settings first."), force: true);
                return;
            }
            var s = StreamerUI.inst;
            if (s.visible)
            {
                Hide();
                A.Cue(Cue.Close);
                A.Say(Loc.T("Twitch voting settings closed"), force: true);
                return;
            }
            s.Visible(true);
            A.Cue(Cue.Open);
            if (!AccessController.Inst.FocusPanelGroup("Twitch"))
                A.Say(Loc.T("Twitch voting settings opened. F6 from the map reaches them."), force: true);
        }

        internal static void Hide()
        {
            try
            {
                if (SettingsVisible) StreamerUI.inst.Visible(false);
            }
            catch
            {
                // the panel's animator may be missing while the feature switches off
            }
        }

        /// <summary>A new or loaded world: the last result and a pending announcement belong to the previous kingdom.</summary>
        internal static void Reset()
        {
            LastWinner = null;
            bannerWinner = null;
            pending = null;
            countdownSaid = false;
            noVotesRound = false;
        }

        // ------------------------------------------------------------------ game hooks (GamePatches.cs)

        internal static void OnUsernameSet(string username)
        {
            awaitingChannel = string.IsNullOrEmpty(username) ? null : username.ToLowerInvariant();
            if (awaitingChannel != null) A.SayQueued(Loc.F("Connecting to the Twitch channel {0}", awaitingChannel));
        }

        internal static void OnOnline(bool online)
        {
            if (online)
            {
                A.Cue(Cue.Placed);
                A.SayQueued((awaitingChannel != null ? Loc.F("Connected to the Twitch chat of {0}.", awaitingChannel) : Loc.T("Connected to the Twitch chat.")) + " " + Loc.T("Viewers vote by typing the number sign and an option number in chat, for example #2."));
                awaitingChannel = null;
            }
            else if (awaitingChannel != null)
            {
                A.Cue(Cue.Error);
                A.SayQueued(Loc.F("Could not join the Twitch channel {0}. Check the channel name and try again.", awaitingChannel));
                awaitingChannel = null;
            }
        }

        internal static void BeforeVotingEnd()
        {
            noVotesRound = false;
            try
            {
                if (!Connected) return;
                var opts = Options();
                noVotesRound = opts.Count > 0 && opts.TrueForAll(o => o.Votes == 0);
            }
            catch
            {
                // announcement only
            }
        }

        internal static void OnNewVoteSet()
        {
            countdownSaid = false;
            if (!Connected) return;
            string text = TwitchText.NewVote(Options(), SecondsLeft());
            if (noVotesRound) text = Loc.T("Nobody voted.") + " " + text;
            noVotesRound = false;
            // Said a moment later: the result banner closing (which starts the next vote) and the map announcement
            // that follows would cut it off otherwise.
            pending = text;
            pendingAt = Time.unscaledTime;
        }

        internal static void OnWinner(Votable v, string voter)
        {
            LastWinner = TwitchText.Winner(TitleOf(v), TextUtil.Clean(voter));
            bannerWinner = LastWinner;
            GameEvents.Log.Add(LastWinner, Severity.Info, Player.inst != null ? Player.inst.CurrYear : 0);
        }

        /// <summary>Title for the result banner (read in full when it opens), so the winner is the first thing heard.</summary>
        internal static string BannerTitle()
        {
            var b = EffectBanner.inst;
            return b != null && b.resetVotesOnDismiss ? bannerWinner : null;
        }

        private static void OnChat(TwitchChatMessage m)
        {
            try
            {
                if (m == null || !Plugin.CfgTwitchChat.Value) return;
                string text = chat.Accept(m.username, m.message);
                if (text != null) A.SayQueued(text);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Twitch chat reading failed: " + e.Message);
            }
        }

        /// <summary>Called every frame in play.</summary>
        internal static void Tick()
        {
            var s = StreamerUI.inst;
            if (s == null) return;
            var irc = s.twitchIrc != null ? s.twitchIrc : s.GetComponent<TwitchIRC>();
            if (irc != null && irc != subscribedIrc)
            {
                irc.messageRecievedEvent.AddListener(OnChat);
                subscribedIrc = irc;
            }
            if (Plugin.CfgTwitchChat.Value)
            {
                string more = chat.Flush();
                if (more != null) A.SayQueued(more);
            }
            else if (chat.Dropped > 0) chat.Reset();

            bool bannerUp = EffectBanner.inst != null && EffectBanner.inst.Showing();
            if (pending != null && !bannerUp && Time.unscaledTime - pendingAt > 0.6f)
            {
                A.Cue(Cue.Notify);
                A.SayQueued(pending);
                pending = null;
            }

            if (!VoteRunning || !s.voteTimer.Enabled || bannerUp) return;
            int left = SecondsLeft();
            if (!countdownSaid && left <= 10 && left > 0)
            {
                countdownSaid = true;
                if (Plugin.CfgTwitchCountdown.Value) A.SayQueued(TwitchText.Countdown(Options(), left));
            }
        }
    }

    /// <summary>Writes BepInEx/kcaccess_stream.txt every 2 seconds for an OBS text source (setting, off by default).</summary>
    internal static class StreamStatusFile
    {
        private static float next;
        private static string lastWritten;
        private static bool failed;

        internal static string FilePath => Path.Combine(BepInEx.Paths.BepInExRootPath, "kcaccess_stream.txt");

        internal static void Tick(bool playing)
        {
            if (!Plugin.CfgStreamFile.Value || Time.unscaledTime < next) return;
            next = Time.unscaledTime + 2f;
            try
            {
                string text = playing ? Build() : "Kingdoms and Castles\r\n" + Loc.T("In the menus") + "\r\n";
                if (text == lastWritten) return;
                File.WriteAllText(FilePath, text, new UTF8Encoding(false));
                lastWritten = text;
                failed = false;
            }
            catch (Exception e)
            {
                if (failed) return;
                failed = true;
                Plugin.Log.LogWarning("Could not write the stream status file " + FilePath + ": " + e.Message);
            }
        }

        private static string Build()
        {
            return StreamOverlay.Build(
                Safe(() => TownNameUI.inst != null ? TownNameUI.inst.townName : null),
                Safe(() => TextUtil.Join(", ", Loc.F("Year {0}", Player.inst.CurrYear), Status.SeasonName())),
                Safe(() => Loc.F("Population {0}", Status.Population())),
                Safe(Status.GoldLine),
                Safe(() => GameEvents.Log.Count > 0 ? GameEvents.Log.Items[0].Text : null),
                Safe(() => Twitch.VoteRunning ? TwitchText.Status(Twitch.Options(), Twitch.SecondsLeft()) : null));
        }

        private static string Safe(Func<string> get)
        {
            try
            {
                return get();
            }
            catch
            {
                return null; // the game may be loading
            }
        }
    }

    /// <summary>
    /// Draws the last spoken lines at the bottom of the screen for people watching a stream, who cannot hear the
    /// screen reader (setting, off by default). IMGUI, so no assets are needed.
    /// </summary>
    internal sealed class CaptionOverlay : MonoBehaviour
    {
        internal static readonly Captions Lines = new Captions(3, 6, 1);

        private GUIStyle style;
        private readonly GUIContent content = new GUIContent();

        internal static void OnSpoken(string text)
        {
            if (Plugin.CfgCaptions == null || !Plugin.CfgCaptions.Value) return;
            Lines.Add(text, Time.realtimeSinceStartup);
        }

        private void Awake()
        {
            useGUILayout = false;
        }

        private void OnGUI()
        {
            try
            {
                if (Plugin.CfgCaptions == null || !Plugin.CfgCaptions.Value)
                {
                    if (Lines.Count > 0) Lines.Clear();
                    return;
                }
                if (Lines.Count == 0 || Event.current.type != EventType.Repaint) return;
                var lines = Lines.Visible(Time.realtimeSinceStartup);
                if (lines.Count == 0) return;
                if (style == null)
                {
                    style = new GUIStyle(GUI.skin.label) { wordWrap = true, alignment = TextAnchor.MiddleCenter, richText = false, fontStyle = FontStyle.Bold };
                    style.normal.textColor = Color.white;
                }
                style.fontSize = Mathf.Clamp(Screen.height / 30, 16, 48);
                float pad = style.fontSize * 0.4f;
                float width = Mathf.Min(Screen.width - 32f, Screen.height * 1.6f);
                float x = (Screen.width - width) / 2f;
                float y = Screen.height - Screen.height * 0.08f; // above the game's bottom toolbar
                var old = GUI.color;
                for (int i = lines.Count - 1; i >= 0; i--)
                {
                    content.text = lines[i].Text;
                    float h = style.CalcHeight(content, width - 2f * pad) + 2f * pad;
                    y -= h;
                    var box = new Rect(x, y, width, h);
                    GUI.color = new Color(0f, 0f, 0f, 0.75f * lines[i].Alpha);
                    GUI.DrawTexture(box, Texture2D.whiteTexture);
                    GUI.color = new Color(1f, 1f, 1f, lines[i].Alpha);
                    GUI.Label(new Rect(x + pad, y + pad, width - 2f * pad, h - 2f * pad), content, style);
                    y -= pad * 0.5f;
                }
                GUI.color = old;
            }
            catch (Exception e)
            {
                Plugin.CfgCaptions.Value = false;
                Plugin.Log.LogError("Speech captions failed and were turned off: " + e.Message);
            }
        }
    }
}

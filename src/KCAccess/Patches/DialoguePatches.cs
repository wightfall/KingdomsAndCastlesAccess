using System;
using HarmonyLib;
using KCAccess.Core;
using PixelCrushers.DialogueSystem;
using UnityEngine;

namespace KCAccess
{
    /// <summary>
    /// Diplomacy and story conversations use the PixelCrushers Dialogue System. Speak every line as it is shown
    /// and the number of replies, then put the keyboard on the Continue button or the first reply, and say it.
    /// </summary>
    [HarmonyPatch(typeof(StandardUISubtitlePanel), nameof(StandardUISubtitlePanel.ShowSubtitle))]
    internal static class Patch_DialogueSubtitle
    {
        internal static string LastLine;
        private static float lastLineAt = -10f;

        /// <summary>
        /// A conversation is speaking. A window that opens with its first line (an envoy's message) announces itself and
        /// pauses the game a moment later, on a slow computer seconds later, and what they say must wait or the line is cut
        /// off unheard.
        /// </summary>
        internal static bool ConversationSpeaking => Time.unscaledTime - lastLineAt < 1.5f || (DialogueManager.isConversationActive && WindowOpen());

        /// <summary>
        /// A window that holds conversations is on screen. Escape in the middle of an envoy's message closes the window
        /// while the dialogue system stays at the closing line, waiting for a "Farewell" nobody can give any more: the
        /// conversation counted as speaking for the rest of the session, and every panel said only its name.
        /// </summary>
        internal static bool WindowOpen()
        {
            try
            {
                var ui = GameUI.inst;
                if (ui == null) return true;
                return (ui.diplomacyUI != null && ui.diplomacyUI.gameObject.activeInHierarchy)
                    || (AdvisorUI.inst != null && AdvisorUI.inst.IsVisible())
                    || (ui.witchUI != null && ui.witchUI.gameObject.activeSelf);
            }
            catch (Exception)
            {
                return true;
            }
        }

        private static void Postfix(Subtitle subtitle)
        {
            try
            {
                if (subtitle?.formattedText == null) return;
                string text = TextUtil.Clean(subtitle.formattedText.text);
                // The same line shown twice in a row (the panel refreshing) is said once; the same line in a later
                // conversation (an advisor's greeting) was never said again.
                if (!TextUtil.HasContent(text)) return;
                bool repeat = text == LastLine && Time.unscaledTime - lastLineAt < 2f;
                LastLine = text;
                lastLineAt = Time.unscaledTime;
                if (repeat) return;
                string speaker = subtitle.speakerInfo != null ? TextUtil.Clean(subtitle.speakerInfo.Name) : null;
                // Player lines are the reply just chosen; only name the other party.
                bool isPlayer = subtitle.speakerInfo != null && subtitle.speakerInfo.isPlayer;
                // The game's actors are mostly called "NPC" / "Player"; those names add nothing.
                bool generic = string.IsNullOrEmpty(speaker) || speaker == "NPC" || speaker == "Player";
                A.Say(isPlayer || generic ? text : TextUtil.Join(": ", speaker, text), KCAccess.Core.Priority.High, force: true);
                // Put the keyboard on the Continue button so Enter moves the conversation on.
                AccessController.Inst?.Nav.RequestFocus(go => go.name.IndexOf("Continue", StringComparison.OrdinalIgnoreCase) >= 0 && go.GetComponent<UnityEngine.UI.Button>() != null, speak: true);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Dialogue line hook failed: " + e.Message);
            }
        }
    }

    [HarmonyPatch(typeof(StandardUIMenuPanel), nameof(StandardUIMenuPanel.ShowResponses))]
    internal static class Patch_DialogueResponses
    {
        private static void Postfix(Response[] responses)
        {
            try
            {
                if (responses == null || responses.Length == 0) return;
                // Escape closed the window during the message: the replies that follow have nowhere to be chosen.
                if (!Patch_DialogueSubtitle.WindowOpen()) return;
                int enabled = 0;
                foreach (var r in responses) if (r != null && r.enabled) enabled++;
                A.SayQueued(Loc.P(enabled, "{0} reply", "{0} replies") + ". " + Loc.T("Up and Down choose, Enter answers."));
                AccessController.Inst?.Nav.RequestFocus(go => go.GetComponent<StandardUIResponseButton>() != null, speak: true);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Dialogue responses hook failed: " + e.Message);
            }
        }
    }
}

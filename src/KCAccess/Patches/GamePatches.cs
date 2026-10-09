using System;
using System.Collections.Generic;
using HarmonyLib;
using KCAccess.Game;
using UnityEngine;

namespace KCAccess
{
    /// <summary>While the keyboard drives the pointer, the mouse resting over a UI panel must not swallow clicks.</summary>
    [HarmonyPatch(typeof(GameUI), nameof(GameUI.PointerOverUI))]
    internal static class Patch_PointerOverUI
    {
        private static bool Prefix(ref bool __result)
        {
            var vp = VirtualPointer.Inst;
            if (vp != null && vp.KeyboardActive && !GamepadControl.isControllerActive)
            {
                __result = false;
                return false;
            }
            return true;
        }
    }

    /// <summary>Speaks and records every kingdom notification.</summary>
    [HarmonyPatch(typeof(KingdomLog), nameof(KingdomLog.TryLog))]
    internal static class Patch_KingdomLog
    {
        private static void Prefix(out int __state)
        {
            __state = KingdomLog.inst != null ? KingdomLog.inst.log.Count : -1;
        }

        private static void Postfix(string id, string message, KingdomLog.LogStatus status, object GameObjectOrVector3, int __state)
        {
            try
            {
                if (KingdomLog.inst == null || __state < 0 || KingdomLog.inst.log.Count <= __state) return; // suppressed duplicate
                Vector3? pos = null;
                if (GameObjectOrVector3 is ICamTrackable t) pos = t.GetDesiredTrackingPos();
                else if (GameObjectOrVector3 is Vector3 v) pos = v;
                else if (GameObjectOrVector3 is GameObject go && go != null) pos = go.transform.position;
                GameEvents.OnNotification(id, message, status, pos);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Notification hook failed: " + e.Message);
            }
        }
    }

    [HarmonyPatch(typeof(VikingNotification), nameof(VikingNotification.ShowBanner))]
    internal static class Patch_VikingBanner
    {
        private static void Postfix() => GameEvents.OnThreat(KCAccess.Core.Loc.T("Vikings are attacking!"));
    }

    [HarmonyPatch(typeof(DragonNotification), nameof(DragonNotification.ShowBanner))]
    internal static class Patch_DragonBanner
    {
        private static void Postfix() => GameEvents.OnThreat(KCAccess.Core.Loc.T("Dragons are attacking!"));
    }

    [HarmonyPatch(typeof(MerchantNotification), nameof(MerchantNotification.ShowBanner))]
    internal static class Patch_MerchantBanner
    {
        private static void Postfix() => GameEvents.OnInfo(KCAccess.Core.Loc.T("A merchant ship has arrived."));
    }

    /// <summary>Announces how many pieces were built (the game restages the next piece instantly, so polling misses it).</summary>
    [HarmonyPatch(typeof(PlacementMode), nameof(PlacementMode.AcceptPlacement))]
    internal static class Patch_AcceptPlacement
    {
        internal sealed class State
        {
            public string Name;
            public int Valid;
            public readonly Dictionary<string, int> Skipped = new Dictionary<string, int>();
        }

        private static void Prefix(PlacementMode __instance, out State __state)
        {
            var st = new State();
            try
            {
                foreach (var b in __instance.buildings)
                {
                    if (b == null) continue;
                    st.Name = b.FriendlyName;
                    var r = World.inst.CanPlace(b);
                    if (r == PlacementValidationResult.Valid) { st.Valid++; continue; }
                    string why = KCAccess.Core.PlacementText.Explain(r.ToString());
                    st.Skipped[why] = st.Skipped.TryGetValue(why, out int n) ? n + 1 : 1;
                }
            }
            catch
            {
                // Never break placement because of the announcement.
            }
            __state = st;
        }

        private static void Postfix(bool __result, State __state)
        {
            if (__state == null || __state.Name == null) return;
            string skipped = null;
            if (__state.Valid > 0 && __state.Skipped.Count > 0)
            {
                var parts = new List<string>();
                foreach (var kv in __state.Skipped) parts.Add(KCAccess.Core.Loc.F("{0} skipped, {1}", kv.Value, kv.Key));
                skipped = string.Join("; ", parts.ToArray());
            }
            MapController.Inst.OnPlacementAccepted(__result, __state.Name, __state.Valid, skipped);
        }
    }

    // No hook on SaveLoadUI.ClickSaveItem: it only opens the "overwrite?" confirmation, so "Game saved" was spoken
    // before the player answered (even after No). Patch_ManualSave confirms a save once it really happened.
}

namespace KCAccess
{
    /// <summary>The C key (or the tile panel's chop button) toggles chopping on the selected tile: say which way it went.</summary>
    [HarmonyPatch(typeof(TileInfoUI), nameof(TileInfoUI.ClickedChop))]
    internal static class Patch_ClickedChop
    {
        private static void Prefix(TileInfoUI __instance, out int __state)
        {
            var c = GameUI.inst != null ? GameUI.inst.GetCellSelected() : null;
            if (c == null || c.TreeAmount == 0) { __state = 0; return; }
            __instance.UpdateChopStatus();
            __state = __instance.clearCutter == null ? 1 : 2;
        }

        private static void Postfix(int __state)
        {
            if (__state == 0) return;
            A.Cue(__state == 1 ? KCAccess.Core.Cue.Activate : KCAccess.Core.Cue.Close);
            A.Say(__state == 1 ? KCAccess.Core.Loc.T("Trees marked for chopping. Idle villagers will cut them for wood.") : KCAccess.Core.Loc.T("Chopping cancelled on this tile."), force: true);
        }
    }
}

namespace KCAccess
{
    /// <summary>Saving from the save screen gives no visible confirmation either; say it (autosaves stay quiet).</summary>
    [HarmonyPatch(typeof(LoadSave), nameof(LoadSave.Save))]
    internal static class Patch_ManualSave
    {
        private static void Postfix()
        {
            try
            {
                if (GameState.inst != null && GameState.inst.IsMainMenuMode() && GameState.inst.mainMenuMode.GetState() == MainMenuMode.State.Save)
                {
                    A.Cue(KCAccess.Core.Cue.Placed);
                    A.SayQueued(KCAccess.Core.Loc.T("Game saved"));
                }
            }
            catch
            {
                // Announcement only.
            }
        }
    }
}

namespace KCAccess
{
    /// <summary>Villagers picked from a house or workplace list (the magnifying glass) are selected silently by the game.</summary>
    [HarmonyPatch(typeof(PersonListItemUI), "OnMagClick")]
    internal static class Patch_PersonListMagClick
    {
        private static void Postfix(PersonListItemUI __instance)
        {
            if (__instance == null || __instance.Villager == null) return;
            A.Cue(KCAccess.Core.Cue.Activate);
            A.Say(KCAccess.Core.Loc.F("Selected {0}", Game.CellInfo.VillagerSummary(__instance.Villager)) + ". " + KCAccess.Core.Loc.T("Villager details are added at the end of this panel."), force: true);
        }
    }
}

namespace KCAccess
{
    /// <summary>Deleting a save removes its slot silently; confirm it.</summary>
    [HarmonyPatch(typeof(LoadSave), nameof(LoadSave.Delete))]
    internal static class Patch_DeleteSave
    {
        private static void Postfix()
        {
            A.Cue(KCAccess.Core.Cue.Close);
            A.SayQueued(KCAccess.Core.Loc.T("Save deleted"));
        }
    }
}

namespace KCAccess
{
    /// <summary>Demolishing (Delete key, the panel's Demolish button, demolish mode) happened silently.</summary>
    [HarmonyPatch(typeof(World), nameof(World.DemolishBuildingByPlayer))]
    internal static class Patch_Demolish
    {
        private static readonly List<string> demolished = new List<string>();
        private static int frame = -1;
        internal static float LastDemolishTime = -10f;

        private static void Prefix(Building building, out string __state)
        {
            __state = null;
            if (building == null) return;
            try
            {
                if (!World.inst.CanDemoBuilding(building))
                {
                    A.Cue(KCAccess.Core.Cue.Error);
                    A.Say(KCAccess.Core.Loc.F("{0} cannot be demolished", building.FriendlyName), force: true);
                    return;
                }
                __state = building.FriendlyName;
            }
            catch
            {
                // announcement only
            }
        }

        private static void Postfix(string __state)
        {
            if (__state == null) return;
            demolished.Add(__state);
            frame = Time.frameCount;
            LastDemolishTime = Time.unscaledTime;
        }

        /// <summary>Called every frame: one announcement for everything demolished together (area demolish).</summary>
        internal static void Flush()
        {
            if (demolished.Count == 0 || Time.frameCount == frame) return;
            A.Cue(KCAccess.Core.Cue.Close);
            // "Demolished Road, 2 pieces, Farm"
            var counts = new Dictionary<string, int>();
            var order = new List<string>();
            foreach (var n in demolished)
            {
                if (!counts.ContainsKey(n)) { counts[n] = 0; order.Add(n); }
                counts[n]++;
            }
            var parts = new List<string>();
            foreach (var n in order) parts.Add(counts[n] > 1 ? KCAccess.Core.Loc.F("{0}, {1} pieces", n, counts[n]) : n);
            A.Say(KCAccess.Core.Loc.F("Demolished {0}", string.Join("; ", parts.ToArray())), force: true);
            demolished.Clear();
        }
    }
}

namespace KCAccess
{
    /// <summary>
    /// Foreign envoys travelling to the player's keep only showed a silent banner, and an envoy that arrived waits
    /// (up to two years) for the player to click it. Both are announced now, and the Alert key (Ctrl+E) talks to it (see Game.Alerts).
    /// </summary>
    internal static class EnvoyWatch
    {
        private static readonly HashSet<Envoy> enroute = new HashSet<Envoy>();
        private static readonly HashSet<Envoy> arrived = new HashSet<Envoy>();

        internal static string KingdomOf(Envoy e)
        {
            try
            {
                var owner = World.GetLandmassOwnerByTeamId(e.TeamID());
                if (owner != null && owner.ownedLandMasses != null && owner.ownedLandMasses.Count > 0)
                {
                    int lm = owner.ownedLandMasses.data[0];
                    if (lm >= 0 && lm < Player.inst.LandMassNames.Count && !string.IsNullOrEmpty(Player.inst.LandMassNames[lm])) return Player.inst.LandMassNames[lm];
                }
            }
            catch
            {
                // name is optional
            }
            return KCAccess.Core.Loc.T("a foreign kingdom");
        }

        internal static void OnEnroute(Envoy e)
        {
            if (e == null || !enroute.Add(e)) return;
            A.Cue(KCAccess.Core.Cue.Notify);
            A.SayQueued(KCAccess.Core.Loc.F("An envoy from {0} is on the way to your keep.", KingdomOf(e)));
        }

        internal static void OnArrived(Envoy e)
        {
            if (e == null || !arrived.Add(e)) return; // the game calls this every frame while the envoy waits
            // The alert watcher announces it ("an envoy waits to speak with you"); make it the navigation target too.
            var p = e.transform.position;
            Game.MapController.Inst.Nav.SetTarget(new KCAccess.Core.GridPos((int)p.x, (int)p.z), KCAccess.Core.Loc.F("envoy from {0}", KingdomOf(e)));
        }

        /// <summary>A new or loaded world: forget the envoys of the previous one.</summary>
        internal static void Reset()
        {
            enroute.Clear();
            arrived.Clear();
        }

    }

    [HarmonyPatch(typeof(DiplomacyNotificationUI), nameof(DiplomacyNotificationUI.NotifyEnroute))]
    internal static class Patch_EnvoyEnroute
    {
        private static void Postfix(Envoy envoy) => EnvoyWatch.OnEnroute(envoy);
    }

    [HarmonyPatch(typeof(DiplomacyNotificationUI), nameof(DiplomacyNotificationUI.NotifyArrived))]
    internal static class Patch_EnvoyArrived
    {
        private static void Postfix(Envoy envoy) => EnvoyWatch.OnArrived(envoy);
    }
}

namespace KCAccess
{
    /// <summary>
    /// Finished research at the great library only changed the research window (if it was open): say what was learned.
    /// </summary>
    [HarmonyPatch(typeof(GreatLibrary), nameof(GreatLibrary.Tick))]
    internal static class Patch_ResearchDone
    {
        private static void Prefix(GreatLibrary __instance, out Player.UpgradeType __state) => __state = __instance.research;

        private static void Postfix(GreatLibrary __instance, Player.UpgradeType __state)
        {
            // Cancelling happens elsewhere (CancelResearch), so here a research that ended was completed.
            if (__state == Player.UpgradeType.None || __instance.research != Player.UpgradeType.None) return;
            try
            {
                var b = __instance.GetComponent<Building>();
                if (b == null || b.TeamID() != 0) return;
                var cell = b.GetCell();
                GameEvents.OnDone(KCAccess.Core.Loc.F("Research complete: {0}", KCAccess.UI.Special.UpgradeName(__state)),
                    cell != null ? new KCAccess.Core.GridPos(cell.x, cell.z) : (KCAccess.Core.GridPos?)null);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Research announcement failed: " + e.Message);
            }
        }
    }

    /// <summary>A barracks, archery range, siege workshop or keep finished a unit with only a sound effect: say it.</summary>
    [HarmonyPatch(typeof(Barracks), nameof(Barracks.Tick))]
    internal static class Patch_TrainingDone
    {
        private static void Prefix(Barracks __instance, out bool __state) => __state = __instance.training;

        private static void Postfix(Barracks __instance, bool __state)
        {
            // Inside Tick, training only ends when the unit is complete (cancelling is CancelTraining).
            if (!__state || __instance.training) return;
            try
            {
                var b = __instance.GetComponent<Building>();
                if (b == null || b.TeamID() != 0) return;
                string unit;
                switch (__instance.typeToMake)
                {
                    case Barracks.TypeToMake.Archers: unit = KCAccess.Core.Loc.T("archers"); break;
                    case Barracks.TypeToMake.Envoy: unit = KCAccess.Core.Loc.T("envoy"); break;
                    case Barracks.TypeToMake.Settler: unit = KCAccess.Core.Loc.T("settlers"); break;
                    case Barracks.TypeToMake.Catapult: unit = KCAccess.Core.Loc.T("siege catapult"); break;
                    default: unit = KCAccess.Core.Loc.T("soldiers"); break;
                }
                var cell = b.GetCell();
                GameEvents.OnDone(KCAccess.Core.Loc.F("{0} ready at the {1}", KCAccess.Core.TextUtil.Capitalize(unit), b.FriendlyName),
                    cell != null ? new KCAccess.Core.GridPos(cell.x, cell.z) : (KCAccess.Core.GridPos?)null);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Training announcement failed: " + e.Message);
            }
        }
    }
}

namespace KCAccess
{
    /// <summary>Kingdom share (tourism) messages and share codes pop up over the screen without taking focus.</summary>
    [HarmonyPatch]
    internal static class Patch_TourismPopup
    {
        private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            foreach (var m in AccessTools.GetDeclaredMethods(typeof(TourismPopup)))
                if (m.Name == nameof(TourismPopup.Show)) yield return m;
        }

        private static void Postfix(TourismPopup __instance)
        {
            try
            {
                string msg = KCAccess.Core.TextUtil.Clean(__instance.msgText != null ? __instance.msgText.text : string.Empty);
                string code = __instance.codeObj != null && __instance.codeObj.activeSelf && __instance.codeText != null ? __instance.codeText.text : null;
                A.Cue(KCAccess.Core.Cue.Open);
                A.Say(msg + (string.IsNullOrEmpty(code) ? string.Empty : ". " + KCAccess.Core.Loc.F("Code: {0}", code)), force: true);
                var ok = __instance.okayButton;
                if (ok != null) AccessController.Inst.Nav.RequestFocus(g => g == ok.gameObject);
            }
            catch
            {
                // announcement only
            }
        }
    }
}

namespace KCAccess
{
    /// <summary>Steam shows unlocked achievements as a picture pop-up; say it when one is newly unlocked.</summary>
    [HarmonyPatch(typeof(Assets.Achievements), "Try")]
    internal static class Patch_Achievement
    {
        private static void Prefix(string achievement, out bool __state)
        {
            __state = false;
            try { __state = IntegrationManager.inst != null && IntegrationManager.inst.GetAchievement(achievement); } catch { }
        }

        private static void Postfix(string achievement, bool __state)
        {
            try
            {
                if (__state || IntegrationManager.inst == null || !IntegrationManager.inst.GetAchievement(achievement)) return;
                A.Cue(KCAccess.Core.Cue.Placed);
                A.SayQueued(KCAccess.Core.Loc.F("Achievement unlocked: {0}", KCAccess.Core.TextUtil.Humanize(achievement.Replace('_', ' '))));
            }
            catch
            {
                // announcement only
            }
        }
    }
}

namespace KCAccess
{
    /// <summary>
    /// Buttons that leave the game (Discord, Twitter, Twitch, the shop, news links, the feedback document, the save
    /// folder) switch to another window without a word. Say where focus is going and how to come back.
    /// </summary>
    internal static class ExternalLinks
    {
        internal static void Leaving(string what) =>
            A.Say(KCAccess.Core.Loc.F("Opening {0}. The game stays open: Alt Tab returns to it.", what), force: true);
    }

    [HarmonyPatch(typeof(MainMenuMode), nameof(MainMenuMode.OnClickedDiscord))]
    internal static class Patch_Discord { private static void Prefix() => ExternalLinks.Leaving(KCAccess.Core.Loc.T("the Kingdoms and Castles Discord in your web browser")); }

    [HarmonyPatch(typeof(MainMenuMode), nameof(MainMenuMode.OnClickedTwitter))]
    internal static class Patch_Twitter { private static void Prefix() => ExternalLinks.Leaving(KCAccess.Core.Loc.T("Lion Shield on Twitter in your web browser")); }

    [HarmonyPatch(typeof(MainMenuMode), nameof(MainMenuMode.OnClickedTwitch))]
    internal static class Patch_Twitch { private static void Prefix() => ExternalLinks.Leaving(KCAccess.Core.Loc.T("Lion Shield on Twitch in your web browser")); }

    [HarmonyPatch(typeof(MainMenuMode), nameof(MainMenuMode.OnClickedSendSaveToDev))]
    internal static class Patch_SendSave { private static void Prefix() => ExternalLinks.Leaving(KCAccess.Core.Loc.T("the instructions for sending a save to the developers in your web browser")); }

    [HarmonyPatch(typeof(GameState), nameof(GameState.OnClickedOpenWebpage))]
    internal static class Patch_OpenWebpage
    {
        private static void Prefix(string url)
        {
            string host = url;
            try { host = new System.Uri(url).Host.Replace("www.", ""); } catch { }
            ExternalLinks.Leaving(KCAccess.Core.Loc.F("a web page, {0}, in your web browser", host));
        }
    }

    [HarmonyPatch(typeof(StoreInfo), nameof(StoreInfo.OnClickedStore))]
    internal static class Patch_Store { private static void Prefix() => ExternalLinks.Leaving(KCAccess.Core.Loc.T("the Lion Shield shop in your web browser")); }

    [HarmonyPatch(typeof(OpenInFileBrowser), nameof(OpenInFileBrowser.Open))]
    internal static class Patch_OpenFolder { private static void Prefix() => ExternalLinks.Leaving(KCAccess.Core.Loc.T("the folder in File Explorer")); }
}

namespace KCAccess
{
    /// <summary>
    /// Twitch chat voting (StreamerUI): the connection result, every new vote with the numbers viewers type, and the
    /// winner were visual only. See Game.Twitch for the countdown, the K status line and reading chat aloud.
    /// </summary>
    [HarmonyPatch(typeof(StreamerUI), nameof(StreamerUI.NewVoteSet))]
    internal static class Patch_TwitchNewVote
    {
        private static void Postfix()
        {
            try { Game.Twitch.OnNewVoteSet(); } catch (Exception e) { Plugin.Log.LogWarning("Twitch vote announcement failed: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(StreamerUI), nameof(StreamerUI.OnVotingEnd))]
    internal static class Patch_TwitchVotingEnd
    {
        private static void Prefix() => Game.Twitch.BeforeVotingEnd();
    }

    [HarmonyPatch(typeof(StreamerUI), nameof(StreamerUI.SetUsername))]
    internal static class Patch_TwitchUsername
    {
        private static void Postfix(string username)
        {
            try { Game.Twitch.OnUsernameSet(username); } catch { /* announcement only */ }
        }
    }

    [HarmonyPatch(typeof(StreamerUI), "SetOnline")]
    internal static class Patch_TwitchOnline
    {
        private static void Postfix(bool show)
        {
            try { Game.Twitch.OnOnline(show); } catch { /* announcement only */ }
        }
    }

    [HarmonyPatch(typeof(EffectBanner), nameof(EffectBanner.ShowBannerVoteSystem))]
    internal static class Patch_TwitchWinner
    {
        private static void Postfix(Votable votable, string voterName)
        {
            try { if (votable != null) Game.Twitch.OnWinner(votable, voterName); } catch { /* announcement only */ }
        }
    }
}

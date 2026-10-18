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

    /// <summary>Another world was loaded or generated: entering play afterwards is a new kingdom, not a resume.</summary>
    [HarmonyPatch]
    internal static class Patch_WorldChanged
    {
        private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(LoadSave), nameof(LoadSave.LoadAtPath));
            yield return AccessTools.Method(typeof(World), nameof(World.Generate));
        }

        private static void Postfix()
        {
            GameEvents.WorldChanged = true;
            GameEvents.WorldChangedFrame = Time.frameCount;
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
    /// <summary>
    /// Saving from the save screen gives no visible confirmation either; say it once the save file is written: the game
    /// writes it on a thread of its own after Save returns. Autosaves stay quiet.
    /// </summary>
    [HarmonyPatch(typeof(LoadSave), nameof(LoadSave.Save))]
    internal static class Patch_ManualSave
    {
        /// <summary>Set (by the writing thread too) when the game logs a problem with the save.</summary>
        internal static volatile bool Failed;

        private static void Prefix() => Failed = false;

        private static void Postfix(System.Threading.Thread __result)
        {
            try
            {
                if (GameState.inst != null && GameState.inst.IsMainMenuMode() && GameState.inst.mainMenuMode.GetState() == MainMenuMode.State.Save && AccessController.Inst != null)
                    AccessController.Inst.StartCoroutine(Confirm(__result));
            }
            catch
            {
                // Announcement only.
            }
        }

        private static System.Collections.IEnumerator Confirm(System.Threading.Thread writer)
        {
            float waited = 0f;
            while (writer != null && writer.IsAlive && waited < 300f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            if (Failed)
            {
                A.Cue(KCAccess.Core.Cue.Error);
                A.Say(KCAccess.Core.Loc.T("Saving failed"));
            }
            else
            {
                A.Cue(KCAccess.Core.Cue.Placed);
                A.SayQueued(KCAccess.Core.Loc.T("Game saved"));
            }
        }
    }

    [HarmonyPatch(typeof(LoadSave), nameof(LoadSave.AppendToLocalErrorLog))]
    internal static class Patch_SaveProblem
    {
        private static void Prefix(string str)
        {
            if (str != null && str.StartsWith("Problem during save", StringComparison.Ordinal)) Patch_ManualSave.Failed = true;
        }
    }

    /// <summary>The creative menu's team colours (who spawned units belong to) change with a click sound only.</summary>
    [HarmonyPatch(typeof(CreativeModeOptions), nameof(CreativeModeOptions.UpdatePalette))]
    internal static class Patch_CreativeTeamChosen
    {
        private static void Prefix(CreativeModeOptions __instance, out int __state) => __state = __instance.currentPalette;

        private static void Postfix(CreativeModeOptions __instance, int idx, bool force, int __state)
        {
            if (force || __state == __instance.currentPalette || __instance.currentPalette != idx) return;
            A.Say(KCAccess.Core.Loc.F("Spawned units now belong to {0}", UI.Special.CreativeTeam(__instance, idx)), force: true);
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

    /// <summary>
    /// Rebuilding rubble (its panel button, R while it is selected, rebuild mode) silently swaps it for a construction
    /// site. One announcement for everything rebuilt together.
    /// </summary>
    [HarmonyPatch(typeof(Rubble), nameof(Rubble.Rebuild))]
    internal static class Patch_RubbleRebuild
    {
        private static readonly List<string> rebuilt = new List<string>();
        private static int frame = -1;

        private static void Prefix(Rubble __instance, out string __state)
        {
            __state = null;
            try
            {
                __state = CellInfo.RubbleOf(__instance.GetComponent<Building>());
            }
            catch
            {
                // announcement only
            }
        }

        private static void Postfix(string __state)
        {
            if (__state == null) return;
            if (!rebuilt.Contains(__state)) rebuilt.Add(__state);
            frame = Time.frameCount;
        }

        /// <summary>Called every frame.</summary>
        internal static void Flush()
        {
            if (rebuilt.Count == 0 || Time.frameCount == frame) return;
            A.Cue(KCAccess.Core.Cue.Activate);
            A.Say(KCAccess.Core.Loc.F("Rebuilding {0}: construction sites replace the rubble", string.Join("; ", rebuilt.ToArray())), force: true);
            rebuilt.Clear();
        }
    }

    /// <summary>
    /// A working building shows what it makes or loses as numbers floating up from it ("+5 stone" from a quarry, a
    /// farm's harvest, apples rotting in winter). Those of the selected building are spoken (AnnounceProduction setting),
    /// so you hear it work as a sighted player sees it.
    /// </summary>
    [HarmonyPatch(typeof(Assets.ResourceTextStackManager), nameof(Assets.ResourceTextStackManager.ShowResourceAmount))]
    internal static class Patch_ProductionNumbers
    {
        private static void Prefix(Vector3 start, FreeResourceType type, int amount)
        {
            if (amount == 0 || !Plugin.CfgProduction.Value) return;
            try
            {
                var b = GameUI.inst != null ? GameUI.inst.GetBuildingSelected() : null;
                if (b == null || b.TeamID() != 0) return;
                var cell = World.inst.GetCellData(start);
                if (cell == null || !cell.OccupyingStructure.Contains(b)) return; // shown over another building
                string res = KCAccess.Core.ResourceNames.Name(type.ToString());
                A.SayQueued(amount > 0 ? KCAccess.Core.Loc.F("+{0} {1}", amount, res) : KCAccess.Core.Loc.F("-{0} {1}", -amount, res));
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

    /// <summary>A construction site of the player finished: only a sound and the scaffolding going away showed it.</summary>
    [HarmonyPatch(typeof(Building), "UpdateConstruction")]
    internal static class Patch_ConstructionDone
    {
        private static void Prefix(Building __instance, out bool __state) => __state = __instance.IsBuilt();

        private static void Postfix(Building __instance, bool __state)
        {
            if (__state || !__instance.IsBuilt()) return;
            try
            {
                if (__instance.TeamID() == 0) GameEvents.OnBuilt(__instance);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Construction announcement failed: " + e.Message);
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
    /// <summary>Kingdom share, upload: "Take Picture" hides the window and shows only an Accept button over the map.</summary>
    [HarmonyPatch(typeof(TourismModalUI), nameof(TourismModalUI.TakeScreenshot))]
    internal static class Patch_TourismPicture
    {
        private static void Postfix()
        {
            A.Cue(KCAccess.Core.Cue.Open);
            A.Say(KCAccess.Core.Loc.T("Picture mode: the map as the camera shows it now becomes your kingdom's picture. Accept Picture takes it."), force: true);
        }
    }

    /// <summary>Kingdom share, visit: checking a code asks the server and answers seconds later, without a word.</summary>
    [HarmonyPatch(typeof(TourismModalUI), nameof(TourismModalUI.LCodeExistence))]
    internal static class Patch_TourismCodeChecked
    {
        private static void Postfix(bool exists)
        {
            // An unknown code is answered by the game's message pop-up (Patch_TourismPopup).
            if (exists) A.Say(KCAccess.Core.Loc.T("Share code found, downloading the kingdom ..."), force: true);
        }
    }

    [HarmonyPatch(typeof(TourismModalUI), nameof(TourismModalUI.LGetSaveData))]
    internal static class Patch_TourismKingdomDownloaded
    {
        private static void Postfix(TourismModalUI __instance, bool success)
        {
            if (!success || AccessController.Inst == null) return; // the game shows an error pop-up
            AccessController.Inst.StartCoroutine(Announce(__instance));
        }

        private static System.Collections.IEnumerator Announce(TourismModalUI ui)
        {
            // The kingdom's summary texts are set with TMP SetText, which shows in .text only after the next layout pass.
            yield return null;
            yield return null;
            try
            {
                var option = ui.L_saveLoadOption;
                string info = option != null ? UI.UIText.JoinTexts(UI.UIText.VisibleTexts(option.transform)) : null;
                A.Cue(KCAccess.Core.Cue.Open);
                A.Say(KCAccess.Core.TextUtil.Sentences(KCAccess.Core.Loc.T("Kingdom downloaded"), info, KCAccess.Core.Loc.T("Visit loads it.")), force: true);
                AccessController.Inst?.Nav.RequestFocus(g => g.name == "Visit Button");
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

        private static readonly Dictionary<string, string> Names = new Dictionary<string, string>
        {
            { "first_hamlet", KCAccess.Core.Loc.N("First hamlet") },
            { "first_village", KCAccess.Core.Loc.N("First village") },
            { "first_town", KCAccess.Core.Loc.N("First town") },
            { "first_city", KCAccess.Core.Loc.N("First city") },
            { "first_humblekingdom", KCAccess.Core.Loc.N("First humble kingdom") },
            { "first_thrivingkingdom", KCAccess.Core.Loc.N("First thriving kingdom") },
            { "first_dignifiedkingdom", KCAccess.Core.Loc.N("First dignified kingdom") },
            { "first_magnificentkingdom", KCAccess.Core.Loc.N("First magnificent kingdom") },
            { "first_kingdomofthegods", KCAccess.Core.Loc.N("First kingdom of the gods") },
            { "towerofbabel", KCAccess.Core.Loc.N("Tower of Babel") },
            { "first_dragonkill", KCAccess.Core.Loc.N("First dragon slain") },
            { "first_ogrekill", KCAccess.Core.Loc.N("First ogre slain") },
        };

        /// <summary>
        /// The achievement's name: the mod's translation, else Steam's name for it (in the Steam language), else the
        /// English name. The game's ids ("first_humblekingdom") were spoken as they are.
        /// </summary>
        private static string NameOf(string id)
        {
            Names.TryGetValue(id, out var english);
            var table = KCAccess.Core.Loc.Table;
            string translated = english != null && table != null ? table.Lookup(english) : null;
            if (translated != null) return translated;
            try
            {
                if (SteamManager.Initialized)
                {
                    string steam = Steamworks.SteamUserStats.GetAchievementDisplayAttribute(id, "name");
                    if (KCAccess.Core.TextUtil.HasContent(steam)) return steam;
                }
            }
            catch
            {
                // no Steam
            }
            return english ?? KCAccess.Core.TextUtil.Humanize(id.Replace('_', ' '));
        }

        private static void Postfix(string achievement, bool __state)
        {
            try
            {
                if (__state || IntegrationManager.inst == null || !IntegrationManager.inst.GetAchievement(achievement)) return;
                A.Cue(KCAccess.Core.Cue.Placed);
                A.SayQueued(KCAccess.Core.Loc.F("Achievement unlocked: {0}", NameOf(achievement)));
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

namespace KCAccess
{
    /// <summary>
    /// The unit panel's Settle and Disband turn the units back into villagers (one per soldier or settler); the panel
    /// only switches to its unit list. Say what happened and how many villagers that made.
    /// </summary>
    [HarmonyPatch]
    internal static class Patch_UnitsDisbanded
    {
        private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(UnitUI), nameof(UnitUI.OnSettle));
            yield return AccessTools.Method(typeof(UnitUI), nameof(UnitUI.OnDisband));
        }

        private static void Prefix(out int __state) => __state = Player.inst != null ? Player.inst.Workers.Count : -1;

        private static void Postfix(int __state, System.Reflection.MethodBase __originalMethod)
        {
            try
            {
                if (__state < 0 || Player.inst == null) return;
                // Settle on an exploration ship starts placing its outpost instead; that is announced as placement.
                if (GameUI.inst != null && GameUI.inst.CurrPlacementMode != null && GameUI.inst.CurrPlacementMode.IsPlacing()) return;
                int added = Player.inst.Workers.Count - __state;
                bool settle = __originalMethod != null && __originalMethod.Name == nameof(UnitUI.OnSettle);
                string text = added > 0
                    ? (settle ? KCAccess.Core.Loc.P(added, "Settled: {0} new villager", "Settled: {0} new villagers") : KCAccess.Core.Loc.P(added, "Disbanded: {0} new villager", "Disbanded: {0} new villagers"))
                    : (settle ? null : KCAccess.Core.Loc.T("Disbanded"));
                if (text != null) A.Say(text, force: true);
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
    /// <summary>An envoy picks a gift up from your Hall of Diplomacy when it walks close; only a parcel appears on it.</summary>
    [HarmonyPatch(typeof(HallOfDiplomacy), nameof(HallOfDiplomacy.TakeGift))]
    internal static class Patch_GiftTaken
    {
        private static void Postfix(HallOfDiplomacy __instance, Assets.Code.ResourceAmount __result)
        {
            try
            {
                var b = __instance != null ? __instance.GetComponent<Building>() : null;
                if (b == null || b.TeamID() != 0) return;
                var parts = new List<string>();
                for (int i = 0; i < 12; i++)
                {
                    int n = __result.Get((FreeResourceType)i);
                    if (n > 0) parts.Add(KCAccess.Core.ResourceNames.Name(((FreeResourceType)i).ToString()) + " " + n);
                }
                A.SayQueued(KCAccess.Core.Loc.F("Your envoy picked up the gift: {0}", string.Join(", ", parts.ToArray())));
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
    /// Make Gift in the Hall of Diplomacy swaps the button for a row of amount fields: start on the first field
    /// instead of where the pressed button was.
    /// </summary>
    [HarmonyPatch(typeof(ForeignMinistryUI), nameof(ForeignMinistryUI.OnClickedMakeCustomGift))]
    internal static class Patch_GiftEditorOpened
    {
        private static void Postfix(ForeignMinistryUI __instance)
        {
            try
            {
                var container = __instance != null && __instance.giftAmountContainer != null ? __instance.giftAmountContainer.transform : null;
                var first = container != null ? container.GetComponentInChildren<TMPro.TMP_InputField>() : null;
                if (first == null || AccessController.Inst == null) return;
                A.Say(KCAccess.Core.Loc.T("Gift: type an amount for each resource, then Make Gift."), force: true);
                AccessController.Inst.Nav.RequestFocus(g => g == first.gameObject);
            }
            catch
            {
                // focus only
            }
        }
    }
}

namespace KCAccess
{
    /// <summary>
    /// Building buttons that cost resources (church tithe, Great Hall feast, town square festival) only flash the
    /// resource counters red when the kingdom cannot pay: say what it costs and what is missing.
    /// </summary>
    [HarmonyPatch(typeof(ResourceUI), nameof(ResourceUI.FlashResourcesUIRed))]
    internal static class Patch_CannotAfford
    {
        private static void Postfix(Assets.Code.ResourceAmount R)
        {
            try
            {
                var p = Player.inst;
                if (p == null) return;
                var names = new List<string>();
                var need = new List<int>();
                var have = new List<int>();
                for (int i = 0; i < (int)FreeResourceType.NumTypes; i++)
                {
                    var t = (FreeResourceType)i;
                    int n = R.Get(t);
                    if (n <= 0) continue;
                    names.Add(t.ToString());
                    need.Add(n);
                    have.Add(t == FreeResourceType.Gold ? p.PlayerLandmassOwner.Gold : p.FocusedResources.Get(t));
                }
                if (names.Count == 0) return;
                A.Cue(KCAccess.Core.Cue.Error);
                A.Say(KCAccess.Core.Loc.F("Not enough resources: {0}", KCAccess.Core.ResourceNames.Cost(names.ToArray(), need.ToArray(), have.ToArray())), force: true);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Cost announcement failed: " + e.Message);
            }
        }
    }
}

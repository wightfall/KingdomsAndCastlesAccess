using Assets.Code;
using Assets;
using System;
using System.Collections;
using KCAccess.Core;
using UnityEngine;

namespace KCAccess.Game
{
    /// <summary>Numbers about the kingdom, read straight from the game state.</summary>
    internal static class Status
    {
        private static int Landmass => Player.inst.FocusedLandMass;

        internal static int Res(FreeResourceType t)
        {
            try
            {
                return Player.inst.FocusedResources.Get(t);
            }
            catch
            {
                return 0;
            }
        }

        internal static string SeasonName() => Weather.inst != null ? (Weather.inst.season == Weather.Season.Winter ? Loc.T("winter") : Loc.T("summer")) : string.Empty;

        /// <summary>Villagers on the player's island.</summary>
        internal static int Population() => Landmass < 0 ? 0 : World.inst.GetVillagersForLandMass(Landmass).Count;

        internal static string DateLine()
        {
            string season = SeasonName();
            string weather = string.Empty;
            try
            {
                switch (Weather.CurrentWeather)
                {
                    case Weather.WeatherType.NormalRain: weather = Loc.T("raining"); break;
                    case Weather.WeatherType.HeavyRain: weather = Loc.T("heavy rain"); break;
                    case Weather.WeatherType.Snow: weather = Loc.T("snowing"); break;
                    case Weather.WeatherType.LightningStorm: weather = Loc.T("thunderstorm"); break;
                }
            }
            catch
            {
                // Weather type names differ between versions; leave it out.
            }
            return TextUtil.Join(", ", Loc.F("Year {0}", Player.inst.CurrYear), season, weather, SpeedLine());
        }

        internal static string SpeedLine()
        {
            if (TimeManager.inst == null) return string.Empty;
            if (TimeManager.inst.IsPaused()) return Loc.T("paused");
            switch (TimeManager.inst.speedInUse)
            {
                case 1: return Loc.T("normal speed");
                case 2: return Loc.T("fast speed");
                case 3: return Loc.T("fastest speed");
                default: return Loc.F("speed {0}", TimeManager.inst.speedInUse);
            }
        }

        internal static string GoldLine() => Loc.F("Gold {0}", Player.inst.PlayerLandmassOwner.Gold);

        internal static string PopulationLine()
        {
            if (Landmass < 0) return Loc.T("No population yet");
            int pop = Population();
            int idle = World.inst.AvailableWorkersOnLandMass(Landmass);
            int beds = Player.inst.TotalResidentialSlotsOnLandMass(Landmass);
            int homeless = Player.inst.Homeless != null ? Player.inst.Homeless.Count : 0;
            string s = Loc.F("Population {0}, {1} idle", pop, idle) + ", " + Loc.P(beds, "{0} bed", "{0} beds");
            if (homeless > 0) s += ", " + Loc.F("{0} homeless", homeless);
            return s;
        }

        internal static string HappinessLine()
        {
            var h = Player.inst.HappinessForFocusedLandMass;
            if (h == null) return Loc.T("Happiness unknown");
            string s = Loc.F("Happiness {0}", h.currHappiness);
            if (h.targetHappiness != h.currHappiness) s += ", " + Loc.F("trending to {0}", h.targetHappiness);
            return s;
        }

        internal static string HealthLine()
        {
            var h = Player.inst.HealthForFocusedLandMass;
            return h == null ? Loc.T("Health unknown") : Loc.F("Health {0}", h.currHealth);
        }

        internal static string FoodLine()
        {
            int wheat = Res(FreeResourceType.Wheat), apples = Res(FreeResourceType.Apples), fish = Res(FreeResourceType.Fish), pork = Res(FreeResourceType.Pork);
            return Loc.F("Food {0}: wheat {1}, apples {2}, fish {3}, pork {4}", wheat + apples + fish + pork, wheat, apples, fish, pork);
        }

        internal static string MaterialsLine() =>
            Loc.F("Wood {0}, stone {1}, iron {2}, charcoal {3}, tools {4}, armaments {5}", Res(FreeResourceType.Tree), Res(FreeResourceType.Stone), Res(FreeResourceType.IronOre),
                Res(FreeResourceType.Charcoal), Res(FreeResourceType.Tools), Res(FreeResourceType.Armament));

        internal static string TaxLine()
        {
            if (Landmass < 0) return Loc.T("Tax rate unknown");
            int pct = (int)(Player.inst.GetTaxRate(Landmass) * 10f) * 2; // same formula as the game's tax display (TaxRateUI)
            return Loc.F("Tax rate {0} percent", pct);
        }

        internal static string ThreatLine()
        {
            var parts = new System.Collections.Generic.List<string>();
            var ui = GameUI.inst;
            try
            {
                if (ui.dragonNotification != null && ui.dragonNotification.Content != null && ui.dragonNotification.Content.activeInHierarchy)
                    parts.Add(Loc.T("Dragons active"));
            }
            catch
            {
                // Notification layout differs between versions.
            }
            try
            {
                // The game shows these as on-screen timers (Settings: Show Viking and Dragon Timers).
                if (Player.inst.difficulty != 0 && RaiderSystem.inst != null && RaiderSystem.inst.AllowSpawningVikings())
                {
                    if (RaiderSystem.inst.IsRaidInProgress())
                    {
                        int vikings = 0;
                        var units = RaiderSystem.inst.unitData;
                        for (int i = 0; i < units.Count; i++) if (units[i].unit != null && !units[i].unit.IsBeingCarried()) vikings++;
                        parts.Add(Loc.P(vikings, "Viking raid in progress, {0} viking left", "Viking raid in progress, {0} vikings left"));
                    }
                    else parts.Add(Loc.P(RaiderSystem.inst.yearsUntilNextAttack + 1, "Next viking raid in {0} year", "Next viking raid in {0} years"));
                }
                if (Player.inst.difficulty != 0 && DragonSpawn.inst != null && DragonSpawn.inst.AllowSpawning())
                    parts.Add(Loc.P(DragonSpawn.inst.yearsUntilNextAttack + 1, "Next dragon attack in {0} year", "Next dragon attack in {0} years"));
            }
            catch
            {
                // timers are optional
            }
            int enemies = 0;
            var armies = UnitSystem.inst.armies;
            for (int i = 0; i < armies.Count; i++)
            {
                var a = armies.data[i];
                if (a != null && a.teamId != 0 && World.inst.RelationBetween(0, a.teamId) == World.Relations.Enemy) enemies++;
            }
            if (enemies > 0) parts.Add(Loc.P(enemies, "{0} enemy army on the map", "{0} enemy armies on the map"));
            int catapults = 0;
            var sieges = SiegeCatapultSystem.siegeCatapults;
            for (int i = 0; sieges != null && i < sieges.Count; i++)
            {
                var sc = sieges.data[i];
                if (sc != null && sc.ValidToSelect() && sc.TeamID() != 0 && World.inst.RelationBetween(0, sc.TeamID()) == World.Relations.Enemy) catapults++;
            }
            if (catapults > 0) parts.Add(Loc.P(catapults, "{0} enemy siege catapult on the map", "{0} enemy siege catapults on the map"));
            return parts.Count == 0 ? Loc.T("No threats visible") : TextUtil.Join(". ", parts);
        }

        /// <summary>
        /// Timed effects shown only as icons with a countdown (StatusEffectsManager): Twitch vote results, witch
        /// blessings and curses, Chamber of War orders. Null when none is active.
        /// </summary>
        internal static string EffectsLine()
        {
            var m = StatusEffectsManager.inst;
            if (m == null || m.activeEffectContainer == null) return null;
            var parts = new System.Collections.Generic.List<string>();
            foreach (var e in m.activeEffectContainer.GetComponentsInChildren<StreamerEffect>())
            {
                if (e == null) continue;
                string term = e.NameText != null ? e.NameText.Term : e.pendingTerm;
                string name = Twitch.Translate(term);
                if (name.Length == 0) name = TextUtil.Humanize(e.GetType().Name.Replace("StreamerEffect_", "").Replace("ChamberOfWarEffect_", ""));
                float left = e.Timer != null && e.Timer.Duration > 0f ? e.TimeRemaining() : -1f;
                parts.Add(left > 0f ? name + ", " + Loc.F("{0} left", TwitchText.Duration(Mathf.CeilToInt(left))) : name);
            }
            return parts.Count == 0 ? null : Loc.F("Active effects: {0}", string.Join("; ", parts.ToArray()));
        }
    }

    /// <summary>K: kingdom status list. Enter on a resource line reads the game's detailed yearly report.</summary>
    internal sealed class StatusMenu : ListMenu
    {
        public override string HelpId => "Status";

        protected override string Title => Loc.T("Kingdom status");

        protected override void Build()
        {
            Add(Status.DateLine);
            Add(Status.GoldLine, () => Examine(GameUI.inst.islandInfoUI.GetComponentInChildren<GoldInfo>(true)));
            Add(Status.PopulationLine);
            Add(Status.HappinessLine, () => SpeakExplanation(() => HappinessUI.inst.GetHappinessExplanation()));
            Add(Status.HealthLine, () => SpeakExplanation(() => GameUI.inst.islandInfoUI.GetComponentInChildren<HealthUI>(true).GetHappinessExplanation()));
            Add(Status.FoodLine, () => Examine(GameUI.inst.islandInfoUI.GetComponentInChildren<FoodInfo>(true)));
            Add(() => Loc.F("Wood {0}", Status.Res(FreeResourceType.Tree)), () => Examine(GameUI.inst.islandInfoUI.GetComponentInChildren<WoodInfo>(true)));
            Add(() => Loc.F("Stone {0}", Status.Res(FreeResourceType.Stone)), () => Examine(GameUI.inst.islandInfoUI.GetComponentInChildren<StoneInfo>(true)));
            Add(() => Loc.F("Iron {0}", Status.Res(FreeResourceType.IronOre)), () => Examine(GameUI.inst.islandInfoUI.GetComponentInChildren<IronInfo>(true)));
            Add(() => Loc.F("Charcoal {0}", Status.Res(FreeResourceType.Charcoal)), () => Examine(GameUI.inst.islandInfoUI.GetComponentInChildren<CharcoalInfo>(true)));
            Add(() => Loc.F("Tools {0}", Status.Res(FreeResourceType.Tools)), () => Examine(GameUI.inst.islandInfoUI.GetComponentInChildren<ToolInfo>(true)));
            Add(() => Loc.F("Armaments {0}", Status.Res(FreeResourceType.Armament)), () => Examine(GameUI.inst.islandInfoUI.GetComponentInChildren<ArmamentInfo>(true)));
            Add(Status.TaxLine);
            Add(Status.ThreatLine);
            Add(Problems.SummaryLine);
            if (Status.EffectsLine() != null) Add(() => Status.EffectsLine() ?? Loc.T("No active effects"));
            if (Twitch.Enabled) Add(Twitch.StatusLine, () => { Close(announce: false); Twitch.OpenSettings(); });
            Add(() => Loc.T("Press Enter on gold, food, a material, happiness or health for the detailed report. F6 from the map reaches the kingdom overview panel with the tax buttons."));
        }

        private static void SpeakExplanation(Func<string> get)
        {
            try
            {
                A.Say(get(), force: true);
            }
            catch (Exception e)
            {
                A.Say(Loc.T("Report not available"));
                Plugin.Log.LogWarning("Explanation failed: " + e.Message);
            }
        }

        /// <summary>Opens the game's own resource report (it fills in on the next frame), reads it, then closes it.</summary>
        internal static void Examine(InfoBase info)
        {
            if (info == null)
            {
                A.Say(Loc.T("Report not available"));
                return;
            }
            AccessController.Inst.StartCoroutine(ExamineRoutine(info));
        }

        private static IEnumerator ExamineRoutine(InfoBase info)
        {
            info.OnClickedExamine();
            yield return null;
            yield return null;
            string text = null;
            var field = info.GetType().GetField("Text");
            if (field != null && field.GetValue(info) is TMPro.TMP_Text t && t.gameObject.activeInHierarchy) text = t.text;
            if (string.IsNullOrEmpty(text) && info.info != null) text = UI.UIText.JoinTexts(UI.UIText.VisibleTexts(info.info.transform));
            A.Say(string.IsNullOrEmpty(text) ? Loc.T("Report not available") : text, force: true);
            info.OnPointerExit();
        }
    }
}

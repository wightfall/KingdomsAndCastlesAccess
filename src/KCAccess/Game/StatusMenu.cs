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

        internal static string DateLine()
        {
            string season = Weather.inst != null ? (Weather.inst.season == Weather.Season.Winter ? "winter" : "summer") : string.Empty;
            string weather = string.Empty;
            try
            {
                switch (Weather.CurrentWeather)
                {
                    case Weather.WeatherType.NormalRain: weather = "raining"; break;
                    case Weather.WeatherType.HeavyRain: weather = "heavy rain"; break;
                    case Weather.WeatherType.Snow: weather = "snowing"; break;
                    case Weather.WeatherType.LightningStorm: weather = "thunderstorm"; break;
                }
            }
            catch
            {
                // Weather type names differ between versions; leave it out.
            }
            return TextUtil.Join(", ", "Year " + Player.inst.CurrYear, season, weather, SpeedLine());
        }

        internal static string SpeedLine()
        {
            if (TimeManager.inst == null) return string.Empty;
            if (TimeManager.inst.IsPaused()) return "paused";
            switch (TimeManager.inst.speedInUse)
            {
                case 1: return "normal speed";
                case 2: return "fast speed";
                case 3: return "fastest speed";
                default: return "speed " + TimeManager.inst.speedInUse;
            }
        }

        internal static string GoldLine() => "Gold " + Player.inst.PlayerLandmassOwner.Gold;

        internal static string PopulationLine()
        {
            if (Landmass < 0) return "No population yet";
            int pop = World.inst.GetVillagersForLandMass(Landmass).Count;
            int idle = World.inst.AvailableWorkersOnLandMass(Landmass);
            int beds = Player.inst.TotalResidentialSlotsOnLandMass(Landmass);
            int homeless = Player.inst.Homeless != null ? Player.inst.Homeless.Count : 0;
            string s = "Population " + pop + ", " + idle + " idle, " + TextUtil.Plural(beds, "bed");
            if (homeless > 0) s += ", " + homeless + " homeless";
            return s;
        }

        internal static string HappinessLine()
        {
            var h = Player.inst.HappinessForFocusedLandMass;
            if (h == null) return "Happiness unknown";
            string s = "Happiness " + h.currHappiness;
            if (h.targetHappiness != h.currHappiness) s += ", trending to " + h.targetHappiness;
            return s;
        }

        internal static string HealthLine()
        {
            var h = Player.inst.HealthForFocusedLandMass;
            return h == null ? "Health unknown" : "Health " + h.currHealth;
        }

        internal static string FoodLine()
        {
            int wheat = Res(FreeResourceType.Wheat), apples = Res(FreeResourceType.Apples), fish = Res(FreeResourceType.Fish), pork = Res(FreeResourceType.Pork);
            return "Food " + (wheat + apples + fish + pork) + ": wheat " + wheat + ", apples " + apples + ", fish " + fish + ", pork " + pork;
        }

        internal static string MaterialsLine() =>
            "Wood " + Res(FreeResourceType.Tree) + ", stone " + Res(FreeResourceType.Stone) + ", iron " + Res(FreeResourceType.IronOre)
            + ", charcoal " + Res(FreeResourceType.Charcoal) + ", tools " + Res(FreeResourceType.Tools) + ", armaments " + Res(FreeResourceType.Armament);

        internal static string TaxLine()
        {
            if (Landmass < 0) return "Tax rate unknown";
            int pct = (int)(Player.inst.GetTaxRate(Landmass) * 10f) * 2; // same formula as the game's tax display (TaxRateUI)
            return "Tax rate " + pct + " percent";
        }

        internal static string ThreatLine()
        {
            var parts = new System.Collections.Generic.List<string>();
            var ui = GameUI.inst;
            try
            {
                if (ui.vikingNotification != null && ui.vikingNotification.Content.activeInHierarchy)
                    parts.Add("Vikings: " + TextUtil.Join(", ", ui.vikingNotification.vikingCountText.text, ui.vikingNotification.vikingYearCountdownText.text));
                if (ui.dragonNotification != null && ui.dragonNotification.Content != null && ui.dragonNotification.Content.activeInHierarchy)
                    parts.Add("Dragons active");
            }
            catch
            {
                // Notification layout differs between versions.
            }
            int enemies = 0;
            var armies = UnitSystem.inst.armies;
            for (int i = 0; i < armies.Count; i++)
            {
                var a = armies.data[i];
                if (a != null && a.teamId != 0 && World.inst.RelationBetween(0, a.teamId) == World.Relations.Enemy) enemies++;
            }
            if (enemies > 0) parts.Add(TextUtil.Plural(enemies, "enemy army", "enemy armies") + " on the map");
            return parts.Count == 0 ? "No threats visible" : TextUtil.Join(". ", parts);
        }
    }

    /// <summary>K: kingdom status list. Enter on a resource line reads the game's detailed yearly report.</summary>
    internal sealed class StatusMenu : ListMenu
    {
        public override string HelpId => "Status";

        protected override string Title => "Kingdom status";

        protected override void Build()
        {
            Add(Status.DateLine);
            Add(Status.GoldLine, () => Examine(GameUI.inst.islandInfoUI.GetComponentInChildren<GoldInfo>(true)));
            Add(Status.PopulationLine);
            Add(Status.HappinessLine, () => SpeakExplanation(() => HappinessUI.inst.GetHappinessExplanation()));
            Add(Status.HealthLine, () => SpeakExplanation(() => GameUI.inst.islandInfoUI.GetComponentInChildren<HealthUI>(true).GetHappinessExplanation()));
            Add(Status.FoodLine, () => Examine(GameUI.inst.islandInfoUI.GetComponentInChildren<FoodInfo>(true)));
            Add(() => "Wood " + Status.Res(FreeResourceType.Tree), () => Examine(GameUI.inst.islandInfoUI.GetComponentInChildren<WoodInfo>(true)));
            Add(() => "Stone " + Status.Res(FreeResourceType.Stone), () => Examine(GameUI.inst.islandInfoUI.GetComponentInChildren<StoneInfo>(true)));
            Add(() => "Iron " + Status.Res(FreeResourceType.IronOre), () => Examine(GameUI.inst.islandInfoUI.GetComponentInChildren<IronInfo>(true)));
            Add(() => "Charcoal " + Status.Res(FreeResourceType.Charcoal), () => Examine(GameUI.inst.islandInfoUI.GetComponentInChildren<CharcoalInfo>(true)));
            Add(() => "Tools " + Status.Res(FreeResourceType.Tools), () => Examine(GameUI.inst.islandInfoUI.GetComponentInChildren<ToolInfo>(true)));
            Add(() => "Armaments " + Status.Res(FreeResourceType.Armament), () => Examine(GameUI.inst.islandInfoUI.GetComponentInChildren<ArmamentInfo>(true)));
            Add(Status.TaxLine);
            Add(Status.ThreatLine);
            Add("Press Enter on gold, food, a material, happiness or health for the detailed report. F6 from the map reaches the kingdom overview panel with the tax buttons.");
        }

        private static void SpeakExplanation(Func<string> get)
        {
            try
            {
                A.Say(get(), force: true);
            }
            catch (Exception e)
            {
                A.Say("Report not available");
                Plugin.Log.LogWarning("Explanation failed: " + e.Message);
            }
        }

        /// <summary>Opens the game's own resource report (it fills in on the next frame), reads it, then closes it.</summary>
        internal static void Examine(InfoBase info)
        {
            if (info == null)
            {
                A.Say("Report not available");
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
            A.Say(string.IsNullOrEmpty(text) ? "Report not available" : text, force: true);
            info.OnPointerExit();
        }
    }
}

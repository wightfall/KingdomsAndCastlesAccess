using Assets.Code;
using Assets;
using KCAccess.Core;
using UnityEngine;

namespace KCAccess.Game
{
    /// <summary>Speaks things that happen in the kingdom: notifications, threats, seasons.</summary>
    internal static class GameEvents
    {
        internal static readonly NotificationLog Log = new NotificationLog();

        private static Weather.Season? lastSeason;
        private static bool lastPaused;
        private static int lastSpeed = -1;

        internal static void OnEnterPlayMode()
        {
            lastSeason = null;
            lastSpeed = -1;
            // Coming back from the pause menu (or save / settings / banner screens) is just a resume.
            var st = GameState.inst.mainMenuMode.GetState();
            bool freshWorld = st == MainMenuMode.State.NewMap || st == MainMenuMode.State.Load || st == MainMenuMode.State.Menu
                || st == MainMenuMode.State.GameWorkshopUI || st == MainMenuMode.State.LoadError || st == MainMenuMode.State.KingdomShareFromMenu;
            if (freshWorld)
            {
                Log.Clear();
                MapController.Inst.OnEnterPlayMode();
            }
            else MapController.Inst.OnResume();
        }

        internal static void OnSaved() => A.SayQueued("Game saved");

        private static Weather.WeatherType? lastWeather;

        internal static void Tick()
        {
            if (Weather.inst != null)
            {
                var s = Weather.inst.season;
                if (lastSeason.HasValue && lastSeason.Value != s && Plugin.CfgAnnounceSeasons.Value)
                {
                    A.Cue(Cue.Notify);
                    A.SayQueued(s == Weather.Season.Winter ? "Winter has come" : "Summer has come");
                }
                lastSeason = s;
            }
            try
            {
                var wt = Weather.CurrentWeather;
                if (lastWeather.HasValue && lastWeather.Value != wt && Plugin.CfgAnnounceSeasons.Value)
                {
                    if (wt == Weather.WeatherType.HeavyRain) { A.Cue(Cue.Notify); A.SayQueued("Heavy rain. Farms near water may flood."); }
                    else if (wt == Weather.WeatherType.LightningStorm) { A.Cue(Cue.Notify); A.SayQueued("Thunderstorm. Lightning can start fires."); }
                }
                lastWeather = wt;
            }
            catch (System.Exception)
            {
                // weather is optional
            }
            if (TimeManager.inst != null)
            {
                bool paused = TimeManager.inst.IsPaused();
                int speed = TimeManager.inst.speedInUse;
                if (lastSpeed >= 0 && (paused != lastPaused || speed != lastSpeed) && GameState.inst.IsPlayMode())
                {
                    A.Say(Status.SpeedLine());
                }
                lastPaused = paused;
                lastSpeed = speed;
            }
        }

        internal static void OnNotification(string id, string message, KingdomLog.LogStatus status, Vector3? pos)
        {
            var sev = NotificationLog.Classify(id, status == KingdomLog.LogStatus.Warning, status == KingdomLog.LogStatus.Important);
            GridPos? where = null;
            if (pos.HasValue) where = new GridPos((int)pos.Value.x, (int)pos.Value.z);
            int year = Player.inst != null ? Player.inst.CurrYear : 0;
            var n = Log.Add(message, sev, year, where);
            if (!Plugin.CfgAnnounceLog.Value || GameState.inst == null || !GameState.inst.IsPlayMode()) return;
            if (!Plugin.CfgAnnounceSeasons.Value && System.Text.RegularExpressions.Regex.IsMatch(message ?? "", @"^\s*Year \d+\s*$")) return; // "Year 74" banners
            A.Cue(sev == Severity.Danger ? Cue.Alert : (sev == Severity.Warning ? Cue.Error : Cue.Notify));
            A.SayQueued(n.Text + (where.HasValue ? ", " + Directions.Relative(MapController.Inst.CursorPos, where.Value) : string.Empty));
        }

        internal static void OnThreat(string text)
        {
            Log.Add(text, Severity.Danger, Player.inst != null ? Player.inst.CurrYear : 0);
            A.Cue(Cue.Alert);
            A.Say(text + KCAccess.Core.KeyHelp.Resolve(" {NextCategory} to the Threats category, then {NextItem} picks the nearest, {JumpTarget} jumps there."), Priority.High);
        }

        internal static void OnInfo(string text)
        {
            Log.Add(text, Severity.Info, Player.inst != null ? Player.inst.CurrYear : 0);
            A.Cue(Cue.Notify);
            A.SayQueued(text);
        }
    }
}

using System.Collections.Generic;
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

        /// <summary>Set when a save is loaded or a map generated (Patch_WorldChanged); cleared on entering play.</summary>
        internal static bool WorldChanged;

        internal static void OnEnterPlayMode()
        {
            lastSeason = null;
            lastSpeed = -1;
            lastRaid = null;
            lastDragonAttack = null;
            lastVikingYears = -1;
            lastDragonYears = -1;
            // Coming back from the pause menu (or save / settings / banner screens) is just a resume. A world that was
            // loaded or generated meanwhile is not: visiting a shared kingdom from the pause menu (and coming back from
            // it) loads another world while the menu state stays "kingdom share from game".
            var st = GameState.inst.mainMenuMode.GetState();
            bool freshWorld = WorldChanged || st == MainMenuMode.State.NewMap || st == MainMenuMode.State.Load || st == MainMenuMode.State.Menu
                || st == MainMenuMode.State.GameWorkshopUI || st == MainMenuMode.State.LoadError || st == MainMenuMode.State.KingdomShareFromMenu;
            WorldChanged = false;
            if (freshWorld)
            {
                // Another world: nothing from the previous kingdom may leak into announcements.
                Log.Clear();
                lastWeather = null;
                built.Clear();
                EnvoyWatch.Reset();
                Twitch.Reset();
                MapController.Inst.OnEnterPlayMode();
            }
            else MapController.Inst.OnResume();
        }

        private static Weather.WeatherType? lastWeather;
        private static bool? lastRaid, lastDragonAttack;
        private static int lastVikingYears = -1, lastDragonYears = -1;

        internal static void Tick()
        {
            TrackAttacks();
            FlushBuilt();
            if (Weather.inst != null)
            {
                var s = Weather.inst.season;
                if (lastSeason.HasValue && lastSeason.Value != s && Plugin.CfgAnnounceSeasons.Value)
                {
                    A.Cue(Cue.Notify);
                    A.SayQueued(s == Weather.Season.Winter ? Loc.T("Winter has come") : Loc.T("Summer has come"));
                }
                lastSeason = s;
            }
            try
            {
                var wt = Weather.CurrentWeather;
                if (lastWeather.HasValue && lastWeather.Value != wt && Plugin.CfgAnnounceSeasons.Value)
                {
                    if (wt == Weather.WeatherType.HeavyRain) { A.Cue(Cue.Notify); A.SayQueued(Loc.T("Heavy rain. Farms near water may flood.")); }
                    else if (wt == Weather.WeatherType.LightningStorm) { A.Cue(Cue.Notify); A.SayQueued(Loc.T("Thunderstorm. Lightning can start fires.")); }
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

        /// <summary>
        /// The start of a viking raid or dragon attack has a banner (Patch_VikingBanner / Patch_DragonBanner), but its end
        /// was only the battle music stopping: say when the last raider or attacking dragon is gone.
        /// </summary>
        private static void TrackAttacks()
        {
            try
            {
                bool raid = RaiderSystem.inst != null && RaiderSystem.inst.IsRaidInProgress();
                if (lastRaid == true && !raid && Plugin.CfgAnnounceLog.Value) OnInfo(Loc.T("The viking raid is over."));
                lastRaid = raid;
                bool dragons = DragonSpawn.inst != null && DragonSpawn.inst.IsAttackInProgress();
                if (lastDragonAttack == true && !dragons && Plugin.CfgAnnounceLog.Value) OnInfo(Loc.T("The dragon attack is over."));
                lastDragonAttack = dragons;
                // The on-screen countdowns (setting "Show Viking and Dragon Timers") start to flash in the last year.
                bool timers = Assets.Settings.inst != null && Assets.Settings.inst.VikingDragonTimers && Player.inst != null && Player.inst.difficulty != Player.Difficulty.Peaceful;
                int vy = timers && !raid && RaiderSystem.inst != null && RaiderSystem.inst.AllowSpawningVikings() ? RaiderSystem.inst.yearsUntilNextAttack : -1;
                if (lastVikingYears > 0 && vy == 0 && Plugin.CfgAnnounceLog.Value) OnThreatWarning(Loc.T("Vikings will raid within a year."));
                lastVikingYears = vy;
                int dy = timers && !dragons && DragonSpawn.inst != null && DragonSpawn.inst.AllowSpawning() ? DragonSpawn.inst.yearsUntilNextAttack : -1;
                if (lastDragonYears > 0 && dy == 0 && Plugin.CfgAnnounceLog.Value) OnThreatWarning(Loc.T("Dragons will attack within a year."));
                lastDragonYears = dy;
            }
            catch (System.Exception)
            {
                // announcement only
            }
        }

        internal static void OnNotification(string id, string message, KingdomLog.LogStatus status, Vector3? pos)
        {
            if (id == "streamervote")
            {
                // Effect banners (Twitch vote results, witch spells) log their title. The banner itself is read in full
                // when it opens, and a Twitch result is logged with its voter by Twitch.OnWinner.
                if (EffectBanner.inst != null && EffectBanner.inst.resetVotesOnDismiss) return;
                Log.Add(message, Severity.Info, Player.inst != null ? Player.inst.CurrYear : 0);
                return;
            }
            var sev = NotificationLog.Classify(id, status == KingdomLog.LogStatus.Warning, status == KingdomLog.LogStatus.Important);
            GridPos? where = null;
            if (pos.HasValue) where = new GridPos((int)pos.Value.x, (int)pos.Value.z);
            int year = Player.inst != null ? Player.inst.CurrYear : 0;
            var n = Log.Add(message, sev, year, where);
            if (!Plugin.CfgAnnounceLog.Value || GameState.inst == null || !GameState.inst.IsPlayMode()) return;
            // The new year banner ("Year 74", in the game's language) follows the seasons setting.
            if (!Plugin.CfgAnnounceSeasons.Value && id == "year") return;
            A.Cue(sev == Severity.Danger ? Cue.Alert : (sev == Severity.Warning ? Cue.Error : Cue.Notify));
            A.SayQueued(n.Text + (where.HasValue ? ", " + Directions.Relative(MapController.Inst.CursorPos, where.Value) : string.Empty));
        }

        internal static void OnThreat(string text)
        {
            Log.Add(text, Severity.Danger, Player.inst != null ? Player.inst.CurrYear : 0);
            A.Cue(Cue.Alert);
            A.Say(text + " " + KCAccess.Core.KeyHelp.Resolve(Loc.T("{NextCategory} to the Threats category, then {NextItem} picks the nearest, {JumpTarget} jumps there.")), Priority.High);
        }

        /// <summary>Something the player started is finished (research, a trained unit); spoken with where it happened.</summary>
        internal static void OnDone(string text, GridPos? where)
        {
            Log.Add(text, Severity.Info, Player.inst != null ? Player.inst.CurrYear : 0, where);
            if (!Plugin.CfgAnnounceLog.Value) return;
            A.Cue(Cue.Notify);
            A.SayQueued(text + (where.HasValue ? ", " + Directions.Relative(MapController.Inst.CursorPos, where.Value) : string.Empty));
        }

        // Construction sites finishing: only a sound and the scaffolding disappearing. Collected and said together
        // ("Construction finished: Farm; Road, 6 pieces") once nothing else finished for a few seconds.
        private static readonly List<string> built = new List<string>();
        private static GridPos? builtAt;
        private static float builtQuietAt, builtLatestAt;

        /// <summary>One of the player's buildings finished construction (Patch_ConstructionDone).</summary>
        internal static void OnBuilt(Building b)
        {
            if (b == null || GameState.inst == null || !GameState.inst.IsPlayMode()) return;
            if (built.Count == 0)
            {
                var c = b.GetCell();
                builtAt = c != null ? new GridPos(c.x, c.z) : (GridPos?)null;
                builtLatestAt = Time.unscaledTime + 20f;
            }
            built.Add(b.FriendlyName);
            builtQuietAt = Time.unscaledTime + 3f;
        }

        private static void FlushBuilt()
        {
            if (built.Count == 0 || (Time.unscaledTime < builtQuietAt && Time.unscaledTime < builtLatestAt)) return;
            var counts = new Dictionary<string, int>();
            var order = new List<string>();
            foreach (var n in built)
            {
                if (!counts.ContainsKey(n)) { counts[n] = 0; order.Add(n); }
                counts[n]++;
            }
            var parts = new List<string>();
            foreach (var n in order) parts.Add(counts[n] > 1 ? Loc.F("{0}, {1} pieces", n, counts[n]) : n);
            GridPos? where = built.Count == 1 ? builtAt : null;
            built.Clear();
            OnDone(Loc.F("Construction finished: {0}", string.Join("; ", parts.ToArray())), where);
        }

        private static void OnThreatWarning(string text)
        {
            Log.Add(text, Severity.Warning, Player.inst != null ? Player.inst.CurrYear : 0);
            A.Cue(Cue.Error);
            A.SayQueued(text);
        }

        internal static void OnInfo(string text)
        {
            Log.Add(text, Severity.Info, Player.inst != null ? Player.inst.CurrYear : 0);
            A.Cue(Cue.Notify);
            A.SayQueued(text);
        }
    }
}

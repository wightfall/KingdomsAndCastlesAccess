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

        /// <summary>The frame of the last load or generation: some systems (ships) finish setting up a few frames later.</summary>
        internal static int WorldChangedFrame;

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
                raidedIslands.Clear();
                dragonIslands.Clear();
                WaterWatch.Reset();
                EnvoyWatch.Reset();
                ShipWatch.Reset();
                FireWatch.Reset();
                PileWatch.Reset();
                ThiefWatch.Reset();
                MissionWatch.Reset();
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
            TrackIslandAttacks();
            FlushBuilt();
            WaterWatch.Tick();
            ShipWatch.Tick();
            FireWatch.Tick();
            PileWatch.Tick();
            MissionWatch.Tick();
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

        // With several kingdoms (the player's islands and AI kingdoms) one banner does not say who is attacked: each
        // island's attack is announced on its own when the attackers reach it and when they are gone.
        private static readonly IslandAttacks raidedIslands = new IslandAttacks(5.0);
        private static readonly IslandAttacks dragonIslands = new IslandAttacks(20.0);
        private static readonly List<int> started = new List<int>(), ended = new List<int>();
        private static readonly Dictionary<int, GridPos> seenAt = new Dictionary<int, GridPos>();
        private static float nextIslandCheck;

        private static void TrackIslandAttacks()
        {
            if (Time.unscaledTime < nextIslandCheck) return;
            nextIslandCheck = Time.unscaledTime + 1f;
            try
            {
                if (World.inst == null || Player.inst == null || !GameState.inst.IsPlayMode()) return;
                double now = Time.time; // game time: a paused game does not end an attack
                seenAt.Clear();
                var raided = new List<int>();
                if (RaiderSystem.inst != null)
                    foreach (var d in RaiderSystem.inst.unitData)
                    {
                        var u = d != null ? d.unit : null;
                        if (u == null || u.IsBeingCarried()) continue;
                        if (u is UnitSystem.Army army && army.IsInvalid()) continue;
                        if (u is SiegeMonster ogre && ogre.IsInvalid()) continue;
                        Note(u.GetPos(), raided);
                    }
                var dragons = new List<int>();
                if (DragonSpawn.inst != null)
                {
                    var list = DragonSpawn.inst.currentDragons;
                    for (int i = 0; i < list.Count; i++)
                    {
                        var dragon = list.data[i];
                        if (dragon == null || dragon.currSlot != null || dragon.teamId == 0) continue;
                        if (dragon.dragonController != null && dragon.dragonController.GetState() == DragonController.DragonState.Dead) continue;
                        Note(dragon.transform.position, dragons);
                    }
                }
                bool announce = Plugin.CfgAnnounceLog.Value && KingdomCount() > 1;
                raidedIslands.Update(raided, now, started, ended);
                if (announce)
                {
                    foreach (int lm in started) OnIslandAttack(lm, Loc.F("Vikings landed on {0}.", Island(lm)));
                    foreach (int lm in ended) OnInfo(Loc.F("The vikings on {0} are gone.", Island(lm)));
                }
                dragonIslands.Update(dragons, now, started, ended);
                if (announce)
                {
                    foreach (int lm in started) OnIslandAttack(lm, Loc.F("Dragons are attacking {0}.", Island(lm)));
                    foreach (int lm in ended) OnInfo(Loc.F("The dragons left {0}.", Island(lm)));
                }
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning("Island attacks: " + e.Message);
            }
        }

        /// <summary>The island under a position (land only), remembered with the first position seen on it.</summary>
        private static void Note(Vector3 p, List<int> islands)
        {
            var c = World.inst.GetCellDataClamped(p);
            if (c == null || c.landMassIdx < 0 || CellInfo.IsWater(c)) return;
            islands.Add(c.landMassIdx);
            if (!seenAt.ContainsKey(c.landMassIdx)) seenAt[c.landMassIdx] = new GridPos(c.x, c.z);
        }

        /// <summary>Islands owned by any kingdom: the player's and the AI kingdoms'.</summary>
        private static int KingdomCount()
        {
            int n = 0;
            var owners = World.LandMassOwners;
            for (int i = 0; i < owners.Count; i++)
                if (owners.data[i] != null && owners.data[i].ownedLandMasses != null) n += owners.data[i].ownedLandMasses.Count;
            return n;
        }

        /// <summary>"Longvale, your island", "Stot kingdom", "unclaimed island".</summary>
        internal static string Island(int lm)
        {
            string name = lm >= 0 && lm < Player.inst.LandMassNames.Count ? Player.inst.LandMassNames[lm] : null;
            return IslandAttacks.Describe(name, Player.inst.LandMassIsAPlayerLandMass(lm), World.GetLandmassOwner(lm) != null);
        }

        /// <summary>An attack on the player's own island is a danger; on an AI kingdom's island it is news.</summary>
        private static void OnIslandAttack(int lm, string text)
        {
            bool mine = Player.inst.LandMassIsAPlayerLandMass(lm);
            GridPos? where = seenAt.TryGetValue(lm, out var p) ? p : (GridPos?)null;
            Log.Add(text, mine ? Severity.Danger : Severity.Info, Player.inst.CurrYear, where);
            A.Cue(mine ? Cue.Alert : Cue.Notify);
            A.SayQueued(TextUtil.WithPlace(text, where.HasValue ? Directions.Relative(MapController.Inst.CursorPos, where.Value) : null));
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
            // Messages about one island start with its name ("Longvale: ..."); an island without a name yet (a new
            // outpost) leaves a bare ": ..." in the game's text.
            string bare = TextUtil.Clean(message);
            int skip = 0;
            while (skip < bare.Length && (char.IsWhiteSpace(bare[skip]) || char.GetUnicodeCategory(bare[skip]) == System.Globalization.UnicodeCategory.Format)) skip++;
            if (skip < bare.Length && bare[skip] == ':') message = Loc.T("Unnamed island") + bare.Substring(skip);
            var sev = NotificationLog.Classify(id, status == KingdomLog.LogStatus.Warning, status == KingdomLog.LogStatus.Important);
            GridPos? where = null;
            if (pos.HasValue) where = new GridPos((int)pos.Value.x, (int)pos.Value.z);
            int year = Player.inst != null ? Player.inst.CurrYear : 0;
            var n = Log.Add(message, sev, year, where);
            if (!Plugin.CfgAnnounceLog.Value || GameState.inst == null || !GameState.inst.IsPlayMode()) return;
            // The new year banner ("Year 74", in the game's language) follows the seasons setting.
            if (!Plugin.CfgAnnounceSeasons.Value && id == "year") return;
            A.Cue(sev == Severity.Danger ? Cue.Alert : (sev == Severity.Warning ? Cue.Error : Cue.Notify));
            A.SayQueued(TextUtil.WithPlace(n.Text, where.HasValue ? Directions.Relative(MapController.Inst.CursorPos, where.Value) : null));
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
            A.SayQueued(TextUtil.WithPlace(text, where.HasValue ? Directions.Relative(MapController.Inst.CursorPos, where.Value) : null));
        }

        // Construction sites finishing: only a sound and the scaffolding disappearing. Collected and said together
        // ("Construction finished: Farm; Road, 6 pieces") once nothing else finished for a few seconds.
        private static readonly List<string> built = new List<string>();
        private static GridPos? builtAt;
        private static float builtQuietAt, builtLatestAt;
        private static readonly List<string> builtNotes = new List<string>();
        private static float builtNotesAt;

        /// <summary>
        /// What a finished building shows floating up from it (a granary's extra food storage). It comes a moment before
        /// the building counts as finished and is said after the construction announcement it belongs to.
        /// </summary>
        internal static void OnBuiltNote(string note)
        {
            if (builtNotes.Count == 0) builtNotesAt = Time.unscaledTime + 1f;
            builtNotes.Add(note);
        }

        private static void SayBuiltNotes()
        {
            if (Plugin.CfgAnnounceLog.Value) foreach (var n in builtNotes) A.SayQueued(n);
            builtNotes.Clear();
        }

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
            WaterWatch.OnBuilt(b);
        }

        private static void FlushBuilt()
        {
            if (built.Count == 0 && builtNotes.Count > 0 && Time.unscaledTime > builtNotesAt) SayBuiltNotes(); // no announcement came
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
            SayBuiltNotes();
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

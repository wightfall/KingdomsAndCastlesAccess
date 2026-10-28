using System.Collections.Generic;
using KCAccess.Core;
using Assets.Code;
using UnityEngine;

namespace KCAccess.Game
{
    /// <summary>
    /// What a sighted player sees happening in the AI kingdoms besides attacks (GameEvents announces those): fires
    /// breaking out on their islands (on explored land) and a kingdom claiming another island (its colour spreads over
    /// it). The game logs neither. Setting "Announce other events seen in other kingdoms"; the history (L) keeps them
    /// either way. Also wars, peace and alliances: the game changes them without a message (an aggressive kingdom whose
    /// envoys you ignore declares war; allying with one kingdom sets it against your enemies) and shows them only as the
    /// ruler's grumpy face and closed docks. Those with you are said whenever notifications are on, those between two
    /// AI kingdoms with the other events.
    /// </summary>
    internal static class OtherKingdoms
    {
        private static readonly List<Vector3> burning = new List<Vector3>();
        private static readonly Dictionary<int, HashSet<int>> islands = new Dictionary<int, HashSet<int>>();
        private static readonly Dictionary<long, KingdomNews.Stance> stances = new Dictionary<long, KingdomNews.Stance>();
        private static float nextCheck;

        internal static void Reset()
        {
            burning.Clear();
            islands.Clear();
            stances.Clear();
            nextCheck = 0f;
        }

        internal static void Tick()
        {
            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + 1f;
            if (World.inst == null || Player.inst == null || GameState.inst == null || !GameState.inst.IsPlayMode()) return;
            if (!Ready) return;
            try
            {
                Fires();
                Claims();
                Stances();
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning("Other kingdoms: " + e.Message);
            }
        }

        /// <summary>A fire farther than 3 tiles from every fire burning a second ago is a new outbreak (as FireWatch).</summary>
        private static void Fires()
        {
            var fm = FireManager.inst;
            if (fm == null || fm.fireContainer == null) return;
            var now = new List<Vector3>();
            foreach (var f in fm.fireContainer.GetComponentsInChildren<Fire>())
            {
                if (f == null) continue;
                var c = World.inst.GetCellDataClamped(f.transform.position);
                if (c == null || c.landMassIdx < 0 || Player.inst.LandMassIsAPlayerLandMass(c.landMassIdx) || World.GetLandmassOwner(c.landMassIdx) == null) continue;
                if (!CellInfo.Explored(c)) continue; // under the fog a sighted player does not see it either
                now.Add(f.transform.position);
            }
            var outbreaks = new List<Vector3>();
            foreach (var p in now)
            {
                bool near = false;
                foreach (var q in burning) if (Mathf.Abs(p.x - q.x) <= 3f && Mathf.Abs(p.z - q.z) <= 3f) { near = true; break; }
                foreach (var q in outbreaks) if (Mathf.Abs(p.x - q.x) <= 3f && Mathf.Abs(p.z - q.z) <= 3f) { near = true; break; }
                if (!near) outbreaks.Add(p);
            }
            burning.Clear();
            burning.AddRange(now);
            foreach (var p in outbreaks)
            {
                var c = World.inst.GetCellDataClamped(p);
                var b = c.TopMostStructure;
                Say(KingdomNews.FireIn(GameEvents.Island(c.landMassIdx), b != null ? b.FriendlyName : null), new GridPos(c.x, c.z));
            }
        }

        private static void Claims()
        {
            var now = new Dictionary<int, IEnumerable<int>>();
            var owners = World.LandMassOwners;
            for (int i = 0; i < owners.Count; i++)
            {
                var o = owners.data[i];
                if (o == null || o == Player.inst.PlayerLandmassOwner || o.ownedLandMasses == null) continue;
                var list = new List<int>();
                for (int j = 0; j < o.ownedLandMasses.Count; j++) list.Add(o.ownedLandMasses.data[j]);
                now[o.teamId] = list;
            }
            foreach (var kv in KingdomNews.NewIslands(islands, now))
            {
                string kingdom = GameEvents.KingdomName(World.GetLandmassOwnerByTeamId(kv.Key));
                Say(KingdomNews.Claimed(kingdom), Scanner.LandmassCentre(kv.Value));
            }
        }

        /// <summary>A loaded world restores kingdoms, islands and relations over its first frames: the first look comes after.</summary>
        private static bool Ready => Time.frameCount >= GameEvents.WorldChangedFrame + 60 && !LoadSave.Loading;

        /// <summary>Wars, peace and alliances changed since the last look (also called before GameEvents checks armies).</summary>
        internal static void CheckStances()
        {
            if (!Ready) return;
            try
            {
                Stances();
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning("Other kingdoms: " + e.Message);
            }
        }

        private static void Stances()
        {
            var brains = AIBrainsContainer.inst;
            if (brains == null || brains.kingdoms == null) return;
            var teams = new List<int> { 0 };
            foreach (var k in brains.kingdoms)
                if (k != null && k.LandmassOwner != null) teams.Add(k.LandmassOwner.teamId);
            var now = new Dictionary<long, KingdomNews.Stance>();
            for (int i = 0; i < teams.Count; i++)
                for (int j = i + 1; j < teams.Count; j++)
                {
                    var r = World.inst.RelationBetween(teams[i], teams[j]);
                    now[KingdomNews.PairKey(teams[i], teams[j])] = r == World.Relations.Enemy ? KingdomNews.Stance.War
                        : r == World.Relations.Allies ? KingdomNews.Stance.Alliance : KingdomNews.Stance.Peace;
                }
            foreach (var kv in KingdomNews.StanceChanges(stances, now))
            {
                KingdomNews.PairTeams(kv.Key, out int a, out int b);
                string first = GameEvents.KingdomName(World.GetLandmassOwnerByTeamId(a == 0 ? b : a));
                if (a == 0 || b == 0)
                {
                    // Your own kingdom: not one of the "other kingdoms" settings.
                    bool war = kv.Value == KingdomNews.Stance.War;
                    string text = TextUtil.Capitalize(KingdomNews.StanceWithYou(first, kv.Value));
                    GameEvents.Log.Add(text, war ? Severity.Danger : Severity.Info, Player.inst.CurrYear);
                    if (!Plugin.CfgAnnounceLog.Value) continue;
                    A.Cue(war ? Cue.Alert : Cue.Notify);
                    A.SayQueued(text);
                }
                else Say(KingdomNews.StanceBetween(first, GameEvents.KingdomName(World.GetLandmassOwnerByTeamId(b)), kv.Value), null);
            }
        }

        private static void Say(string text, GridPos? where)
        {
            text = TextUtil.Capitalize(text);
            GameEvents.Log.Add(text, Severity.Info, Player.inst.CurrYear, where);
            if (!Plugin.CfgAnnounceLog.Value || !Plugin.CfgOtherKingdomEvents.Value) return;
            A.Cue(Cue.Notify);
            A.SayQueued(TextUtil.WithPlace(text, where.HasValue ? Directions.Relative(MapController.Inst.CursorPos, where.Value) : null));
        }
    }
}

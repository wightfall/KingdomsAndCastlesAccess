using Assets.Code;
using Assets;
using System.Collections.Generic;
using KCAccess.Core;
using UnityEngine;

namespace KCAccess.Game
{
    /// <summary>Turns a map tile into speech.</summary>
    internal static class CellInfo
    {
        /// <summary>Is the tile explored (not covered by fog of war)?</summary>
        internal static bool Explored(Cell c)
        {
            if (c == null) return false;
            if (FogOfWar.inst == null) return true;
            try
            {
                return FogOfWar.inst.IsVisible(c.x, c.z);
            }
            catch
            {
                return true;
            }
        }

        internal static bool IsWater(Cell c) => c != null && c.Type == ResourceType.Water;

        /// <summary>Short terrain word: "grass", "forest", "sea" …</summary>
        internal static string Terrain(Cell c)
        {
            switch (c.Type)
            {
                case ResourceType.Water:
                    if (c.deepWater) return c.saltWater ? Loc.T("deep sea") : Loc.T("deep water");
                    return c.saltWater ? Loc.T("shallow sea") : Loc.T("shallow water");
                case ResourceType.Wood:
                    return c.TreeAmount > 0 ? Loc.T("forest") : Loc.T("grass");
                case ResourceType.Stone: return Loc.T("stone deposit");
                case ResourceType.UnusableStone: return Loc.T("rock");
                case ResourceType.IronDeposit: return Loc.T("iron deposit");
                case ResourceType.EmptyCave: return Loc.T("empty cave");
                case ResourceType.WolfDen: return Loc.T("wolf den");
                case ResourceType.WitchHut: return Loc.T("witch hut");
                default:
                    if (c.TreeAmount > 0) return Loc.T("forest");
                    if (TreeSystem.inst != null && TreeSystem.inst.AnimCount(c) > 0) return Loc.T("grass, tree growing or falling");
                    return Loc.T("grass");
            }
        }

        internal static string Fertility(Cell c)
        {
            if (IsWater(c)) return string.Empty;
            switch (c.GetEffectiveFertility())
            {
                case 0: return Loc.T("barren");
                case 1: return Loc.T("fertile");
                case 2: return Loc.T("very fertile");
                default: return Loc.T("irrigated very fertile");
            }
        }

        /// <summary>Signature used by Ctrl+arrows to find where the map changes.</summary>
        internal static string Signature(Cell c)
        {
            if (c == null) return "edge";
            if (!Explored(c)) return "fog";
            var b = c.TopMostStructure;
            if (b != null) return "b:" + b.UniqueName + Overlays.SignaturePart(c);
            return "t:" + Terrain(c) + Overlays.SignaturePart(c);
        }

        /// <summary>Building names on the tile, top to bottom, merging stacked castle blocks.</summary>
        internal static List<string> Structures(Cell c)
        {
            var result = new List<string>();
            var counts = new Dictionary<string, int>();
            var order = new List<string>();
            var all = new List<Building>();
            all.AddRange(c.OccupyingStructure);
            all.AddRange(c.SubStructure);
            for (int i = all.Count - 1; i >= 0; i--)
            {
                var b = all[i];
                if (b == null || b.Life <= 0f && !b.IsBuilt()) continue;
                string name = BuildingSummary(b, brief: true);
                if (!counts.ContainsKey(name))
                {
                    counts[name] = 0;
                    order.Add(name);
                }
                counts[name]++;
            }
            foreach (var n in order) result.Add(counts[n] > 1 ? n + ", " + Loc.F("stacked {0} high", counts[n]) : n);
            return result;
        }

        /// <summary>
        /// What rubble was before it fell ("Well"; each kind once for stacked castle blocks), or null for anything else.
        /// The game shows it only as the building's outline while the rubble is selected.
        /// </summary>
        internal static string RubbleOf(Building b)
        {
            var rubble = b != null && b.uniqueNameHash == World.rubbleHash ? b.GetComponent<Rubble>() : null;
            if (rubble == null || !rubble.HasReplacementData() || GameState.inst == null) return null;
            var names = new List<string>();
            foreach (var state in rubble.GetBuildingStates())
            {
                var prefab = GameState.inst.GetPlaceableByUniqueName(state.uniqueName);
                string n = prefab != null ? prefab.FriendlyName : TextUtil.Humanize(state.uniqueName);
                if (!names.Contains(n)) names.Add(n);
            }
            return names.Count > 0 ? string.Join(", ", names.ToArray()) : null;
        }

        /// <summary>What rebuilding the rubble costs, with what you have of anything short ("cost wood 10 (have 4)").</summary>
        internal static string RebuildCost(Building rubbleBuilding)
        {
            var rubble = rubbleBuilding.GetComponent<Rubble>();
            ResourceAmount cost = rubble.ReplacementCost();
            int lm = rubbleBuilding.LandMass();
            var all = Player.inst.resourcesPerLandmass;
            ResourceAmount have = all != null && lm >= 0 && lm < all.Length ? all[lm] : default(ResourceAmount);
            const int n = 12;
            var names = new string[n];
            var need = new int[n];
            var got = new int[n];
            for (int i = 0; i < n; i++)
            {
                names[i] = ((FreeResourceType)i).ToString();
                need[i] = cost.Get((FreeResourceType)i);
                got[i] = have.Get((FreeResourceType)i);
            }
            return Loc.F("cost {0}", ResourceNames.Cost(names, need, got));
        }

        /// <summary>Rubble in the area that rebuild mode could put up again (not the keep's).</summary>
        internal static int RebuildableRubble(int minX, int minZ, int maxX, int maxZ)
        {
            int count = 0;
            for (int z = minZ; z < maxZ; z++)
                for (int x = minX; x < maxX; x++)
                {
                    var c = World.inst.GetCellData(x, z);
                    var b = c != null ? c.StructureFind(World.rubbleHash) : null;
                    var r = b != null ? b.GetComponent<Rubble>() : null;
                    if (r != null && r.HasReplacementData() && r.CanRebuild) count++;
                }
            return count;
        }

        /// <summary>"Small House, under construction 40 percent, yours".</summary>
        internal static string BuildingSummary(Building b, bool brief)
        {
            var parts = new List<string> { RubbleOf(b) is string was ? Loc.F("Rubble of {0}", was) : b.FriendlyName };
            if (!b.IsBuilt()) parts.Add(Loc.F("under construction {0}", TextUtil.Percent(Mathf.Clamp01(b.constructionProgress))));
            else
            {
                float health = Overlays.Integrity(b); // stacked castle blocks can take more than one building's damage
                bool mapSaysIt = brief && b.decayChance != 0 && Overlays.Active == Overlays.Kind.Integrity; // the tile ends with "integrity N percent"
                if (health < 0.999f && b.Life > 0f && !mapSaysIt) parts.Add(Loc.F("damaged, {0} health", TextUtil.Percent(health)));
                if (!b.Open) parts.Add(Loc.T("closed"));
                if (!brief && b.WorkersForFullYield > 0 && b.CategoryName != "house")
                {
                    parts.Add(Loc.F("{0} of {1} workers", b.WorkersAllocated, b.WorkersForFullYield));
                }
            }
            if (!brief && b.IsBuilt())
            {
                string range = RangeText(b, placed: true);
                if (range != null) parts.Add(range);
            }
            int team = b.TeamID();
            if (team != 0)
            {
                var rel = World.inst.RelationBetween(0, team);
                parts.Add(rel == World.Relations.Enemy ? Loc.T("enemy") : Loc.T("foreign"));
            }
            return TextUtil.Join(", ", parts);
        }

        /// <summary>
        /// Shooting range of towers (the rings the game draws on the ground). <paramref name="placed"/>: the building
        /// stands on the map (built or being placed), so its castle height counts; otherwise (build menu) only the
        /// range on the ground and at full height.
        /// </summary>
        internal static string RangeText(Building b, bool placed)
        {
            var r = b != null ? b.GetComponent<IMaxRangeDisplay>() : null;
            if (r == null) return null;
            try
            {
                return UnitText.Range(r.GetInitRadius(), r.GetUnitsPerRing(), placed ? r.GetCurrHeight() : -1, r.GetNumRings());
            }
            catch (System.Exception)
            {
                return null; // not set up yet
            }
        }

        internal static string Owner(Cell c)
        {
            if (c.landMassIdx < 0) return string.Empty;
            if (Player.inst.LandMassIsAPlayerLandMass(c.landMassIdx)) return string.Empty;
            var owner = World.GetLandmassOwner(c.landMassIdx);
            if (owner == null) return IsWater(c) || Player.inst.keep == null ? string.Empty : Loc.T("unclaimed land");
            string name = c.landMassIdx < Player.inst.LandMassNames.Count ? Player.inst.LandMassNames[c.landMassIdx] : null;
            return string.IsNullOrEmpty(name) ? Loc.T("foreign kingdom") : Loc.F("{0} kingdom", name);
        }

        internal static bool OnFire(Cell c)
        {
            if (FireManager.inst == null) return false;
            try
            {
                return FireManager.inst.ClosestFireWithinRadius(c.Center, 0.75f, includeFullFires: true) != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Units standing on or right next to the tile.</summary>
        internal static List<string> Units(Cell c)
        {
            var result = new List<string>();
            if (UnitSystem.inst != null)
            {
                var armies = UnitSystem.inst.armies;
                for (int i = 0; i < armies.Count; i++)
                {
                    var a = armies.data[i];
                    if (a == null) continue;
                    Vector3 p = a.GetPos();
                    if ((int)p.x != c.x || (int)p.z != c.z) continue;
                    result.Add(Units_ArmyName(a));
                }
            }
            if (ShipSystem.inst != null && ShipSystem.inst.unitPosGrid != null) // the grid is rebuilt for a few frames after a load
            {
                var ships = ShipSystem.inst.GetShipsAt(c.x, c.z);
                for (int i = 0; ships != null && i < ships.Count; i++)
                {
                    var s = ships.data[i];
                    if (s == null) continue;
                    result.Add(ShipName(s));
                }
            }
            for (int i = 0; i < SiegeMonster.monsters.Count; i++)
            {
                var m = SiegeMonster.monsters.data[i];
                if (m == null) continue;
                Vector3 p = m.GetPos();
                if ((int)p.x == c.x && (int)p.z == c.z) result.Add(Loc.T("ogre"));
            }
            foreach (var puppet in RestingDragons(c)) result.Add(UnitName(puppet) + ", " + Loc.T("resting"));
            if (DragonSpawn.inst != null)
            {
                var dragons = DragonSpawn.inst.currentDragons;
                for (int i = 0; i < dragons.Count; i++)
                {
                    var d = dragons.data[i];
                    if (d == null) continue;
                    Vector3 p = d.transform.position;
                    if ((int)p.x == c.x && (int)p.z == c.z) result.Add(DragonName(d));
                }
            }
            // Siege catapults (yours from the siege workshop, and the vikings') were never mentioned.
            var catapults = SiegeCatapultSystem.siegeCatapults;
            for (int i = 0; catapults != null && i < catapults.Count; i++)
            {
                var sc = catapults.data[i];
                if (sc == null || !sc.ValidToSelect()) continue; // dead, or carried on a ship
                Vector3 p = sc.GetPos();
                if ((int)p.x == c.x && (int)p.z == c.z) result.Add(CatapultName(sc));
            }
            if (CartSystem.inst != null)
            {
                foreach (var cart in CartSystem.inst.carts)
                {
                    if (cart == null) continue;
                    Vector3 p = cart.GetPos();
                    if ((int)p.x == c.x && (int)p.z == c.z) result.Add(CartName(cart));
                }
            }
            int wolves = 0, hunting = 0;
            for (int i = 0; i < WolfDen.wolves.Count; i++)
            {
                var w = WolfDen.wolves.data[i];
                if (w == null || w.IsInvalid()) continue;
                if ((int)w.pos.x != c.x || (int)w.pos.z != c.z) continue;
                wolves++;
                if (w.status == WolfDen.Status.Chase || w.status == WolfDen.Status.Attack) hunting++;
            }
            if (wolves > 0) result.Add(Loc.P(wolves, "{0} wolf", "{0} wolves") + (hunting > 0 ? ", " + Loc.T("hunting") : string.Empty));
            return result;
        }

        private static bool Enemy(int team) => team != 0 && World.inst.RelationBetween(0, team) == World.Relations.Enemy;

        /// <summary>", selected" in the player's language, added to the names of selected units.</summary>
        internal static string SelectedSuffix => ", " + Loc.T("selected");

        /// <summary>A unit name without the ", selected" mark.</summary>
        internal static string WithoutSelected(string name) => name == null ? null : name.Replace(SelectedSuffix, string.Empty);

        private static string Selected(ISelectable s) => GameUI.inst != null && s != null && GameUI.inst.IsSelected(s) ? SelectedSuffix : string.Empty;

        /// <summary>
        /// Dragons of the player's roosts that are not flying: on their roost or at a dragon bath, doctor or obstacle course.
        /// The game selects them by their model (DragonPuppet). Those standing on this tile, or whose building covers it.
        /// </summary>
        internal static List<DragonPuppet> RestingDragons(Cell c)
        {
            var result = new List<DragonPuppet>();
            if (c == null || Player.inst == null) return result;
            try
            {
                var nests = Player.inst.GetBuildingList(World.dragonNestHash);
                for (int n = 0; n < nests.Count; n++)
                {
                    var nest = nests.data[n] != null ? nests.data[n].GetComponent<DragonNest>() : null;
                    if (nest == null || nest.slots == null) continue;
                    foreach (var slot in nest.slots)
                    {
                        if (slot == null || !slot.hasDragon || slot.dController != null) continue;
                        Building home = nest.b;
                        GameObject model = slot.CurrPuppet;
                        if (slot.currDockBuilding is DragonBuilding db)
                        {
                            home = db.b;
                            model = db.CurrPuppet;
                        }
                        var puppet = model != null && model.activeInHierarchy ? model.GetComponent<DragonPuppet>() : null;
                        if (puppet == null) continue;
                        Vector3 p = puppet.transform.position;
                        bool onTile = (int)p.x == c.x && (int)p.z == c.z;
                        if (onTile || home != null && c.OccupyingStructure.Contains(home)) result.Add(puppet);
                    }
                }
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning("Resting dragons failed: " + e.Message);
            }
            return result;
        }

        /// <summary>"your dragon" (from a dragon nest), "wild dragon" (an attacking one) or "enemy dragon".</summary>
        internal static string DragonName(Dragon d)
        {
            if (d.teamId == 0) return Loc.T("your dragon") + Selected(d);
            if (d.teamId == -1) return Loc.T("wild dragon");
            return UnitText.Owned(false, Enemy(d.teamId), Loc.T("dragon"));
        }

        internal static string CatapultName(SiegeCatapult sc) => UnitText.Owned(sc.TeamID() == 0, Enemy(sc.TeamID()), Loc.T("siege catapult")) + Selected(sc);

        internal static string CartName(TransportCart cart) => UnitText.Owned(cart.TeamID() == 0, Enemy(cart.TeamID()), Loc.T("transport cart")) + Selected(cart);

        /// <summary>Any unit the player can select and send somewhere.</summary>
        internal static string UnitName(IMoveableUnit u)
        {
            switch (u)
            {
                case UnitSystem.Army a: return Units_ArmyName(a);
                case SiegeCatapult sc: return CatapultName(sc);
                case Dragon d: return DragonName(d);
                case ShipBase s: return ShipName(s);
                case SiegeMonster _: return Loc.T("ogre");
                case DragonPuppet p: return (p.dSlot != null && TextUtil.HasContent(p.dSlot.name) ? Loc.F("your dragon {0}", p.dSlot.name) : Loc.T("your dragon")) + Selected(p);
                default: return Loc.T("unit");
            }
        }

        /// <summary>"your transport ship", "merchant ship", "enemy viking ship" …</summary>
        internal static string ShipName(ShipBase s)
        {
            string kind;
            switch (s.type)
            {
                case ShipBase.ShipType.Merchant:
                case ShipBase.ShipType.PlayerMerchant: kind = Loc.T("merchant ship"); break;
                case ShipBase.ShipType.Fishing: kind = Loc.T("fishing ship"); break;
                case ShipBase.ShipType.TroopTransport: kind = Loc.T("troop ship"); break;
                case ShipBase.ShipType.VikingTroopTransport: kind = Loc.T("viking ship"); break;
                case ShipBase.ShipType.OgreTroopTransport: kind = Loc.T("ogre ship"); break;
                case ShipBase.ShipType.SeedShip: kind = Loc.T("exploration ship"); break;
                default: kind = Loc.T("transport ship"); break;
            }
            if (s.type == ShipBase.ShipType.Merchant || s.type == ShipBase.ShipType.PlayerMerchant) return kind;
            bool selected = GameUI.inst != null && s is ISelectable sel && GameUI.inst.IsSelected(sel);
            return UnitText.Owned(s.teamID == 0, s.teamID != 0 && World.inst.RelationBetween(0, s.teamID) == World.Relations.Enemy, kind) + (selected ? SelectedSuffix : string.Empty);
        }

        internal static string Units_ArmyName(UnitSystem.Army a)
        {
            string type;
            switch (a.armyType)
            {
                case UnitSystem.ArmyType.Archer: type = Loc.T("archers"); break;
                case UnitSystem.ArmyType.Thief:
                    type = a.stolenResourceAmount > 0 && a.stolenResourceType == FreeResourceType.Gold ? Loc.F("thieves carrying {0} gold", a.stolenResourceAmount) : Loc.T("thieves");
                    break;
                case UnitSystem.ArmyType.Envoy: type = Loc.T("envoy"); break;
                case UnitSystem.ArmyType.Settler: type = Loc.T("settlers"); break;
                case UnitSystem.ArmyType.Elite: type = Loc.T("elite soldiers"); break;
                default: type = Loc.T("soldiers"); break;
            }
            bool selected = GameUI.inst != null && GameUI.inst.IsSelected(a);
            return UnitText.Owned(a.teamId == 0, a.teamId != 0 && World.inst.RelationBetween(0, a.teamId) == World.Relations.Enemy, type) + (selected ? SelectedSuffix : string.Empty);
        }

        /// <summary>Description spoken while moving the cursor.</summary>
        internal static string Brief(Cell c, bool verbose)
        {
            if (c == null) return Loc.T("outside the map");
            if (!Explored(c)) return Loc.T("unexplored");
            var parts = new List<string>();
            if (OnFire(c)) parts.Add(Loc.T("on fire"));
            parts.AddRange(Units(c));
            var structures = Structures(c);
            parts.AddRange(structures);
            string terrain = Terrain(c);
            if (c.Type == ResourceType.Wood || c.TreeAmount > 0)
            {
                if (c.TreeAmount > 0) terrain = Loc.T("forest") + ", " + Loc.P(c.TreeAmount, "{0} tree", "{0} trees");
            }
            if (structures.Count == 0 || c.Type != ResourceType.None) parts.Add(terrain);
            string map = Overlays.TileText(c);
            if (map != null) parts.Add(map);
            if (c.TreeAmount > 0 && GameUI.inst != null && GameUI.inst.GetClearCutterJob(c) != null) parts.Add(Loc.T("marked for chopping"));
            if (verbose && !IsWater(c) && c.Type == ResourceType.None) parts.Add(Fertility(c)); // soil only matters on open ground
            int villagers = VillagerCount(c);
            if (villagers > 0)
            {
                int sick = SickCount(c);
                parts.Add(Loc.P(villagers, "{0} villager", "{0} villagers") + (sick > 0 ? ", " + Loc.F("{0} sick", sick) : string.Empty));
            }
            if (IsWater(c))
            {
                int fish = FishAt(c);
                if (fish > 0) parts.Add(Loc.P(fish, "{0} fish", "{0} fish"));
            }
            if (verbose)
            {
                string problem = Problems.At(c.x, c.z);
                if (problem != null) parts.Add(problem);
            }
            parts.Add(Owner(c));
            if (Plugin.CfgCoordinates.Value) parts.Add(c.x + ", " + c.z);
            return TextUtil.Join(", ", parts);
        }

        /// <summary>Everything about a tile (I key).</summary>
        internal static string Full(Cell c)
        {
            if (c == null) return Loc.T("outside the map");
            var parts = new List<string> { Loc.F("Tile {0}, {1}", c.x, c.z) };
            if (!Explored(c))
            {
                parts.Add(Loc.T("unexplored"));
                return TextUtil.Sentences(parts.ToArray());
            }
            parts.Add(Brief(c, verbose: true));
            if (!IsWater(c))
            {
                parts.Add(c.RoadCoverage > 0 || c.RoadConnectCoverage > 0 ? Loc.T("inside road coverage") : Loc.T("outside road coverage"));
                if (c.IrrigationCoverage > 0) parts.Add(Loc.T("irrigated"));
            }
            string left = DepositLeft(c);
            if (left != null) parts.Add(left);
            if (c.wolfTerritory) parts.Add(Loc.T("wolf territory"));
            if (c.Danger > 0) parts.Add(Loc.T("dangerous area"));
            foreach (var b in c.OccupyingStructure)
            {
                if (b != null && b.IsBuilt() && b.WorkersForFullYield > 0 && b.CategoryName != "house") parts.Add(b.FriendlyName + ": " + Loc.F("{0} of {1} workers", b.WorkersAllocated, b.WorkersForFullYield));
            }
            return TextUtil.Sentences(parts.ToArray());
        }

        /// <summary>
        /// Survival mode: stone and iron deposits run out. The tile panel shows "remaining / total" after an icon; null
        /// for other tiles and other difficulties.
        /// </summary>
        internal static string DepositLeft(Cell c)
        {
            if (c == null || Player.inst == null || Player.inst.difficulty != Player.Difficulty.Survival) return null;
            FreeResourceType t;
            if (c.Type == ResourceType.Stone) t = FreeResourceType.Stone;
            else if (c.Type == ResourceType.IronDeposit) t = FreeResourceType.IronOre;
            else return null;
            try
            {
                var tracker = ResourceTracker.GetTracker(c.Position);
                if (tracker == null) return null;
                int max = tracker.maxResources.Get(t);
                return Loc.F("{0} of {1} left in this deposit", max - tracker.currentHarvestedResources.Get(t), max);
            }
            catch (System.Exception)
            {
                return null;
            }
        }

        /// <summary>Fish swimming in this water tile (fishing huts need them nearby).</summary>
        internal static int FishAt(Cell c)
        {
            try
            {
                var fc = FishSystem.inst != null ? FishSystem.inst.fishCells : null;
                if (c == null || fc == null || !IsWater(c)) return 0; // fish swim over the shoreline; only water tiles count
                int i = c.z * World.inst.GridWidth + c.x;
                return i >= 0 && i < fc.Length && fc[i] != null ? fc[i].fish.Count : 0;
            }
            catch (System.Exception)
            {
                return 0;
            }
        }

        internal static int SickCount(Cell c)
        {
            var v = World.inst.GetVillagersAt(c.x, c.z);
            int n = 0;
            if (v != null) for (int i = 0; i < v.Count; i++) if (v.data[i] != null && v.data[i].sick) n++;
            return n;
        }

        internal static int VillagerCount(Cell c)
        {
            try
            {
                var v = World.inst.GetVillagersAt(c.x, c.z);
                return v != null ? v.Count : 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>Counts what lies within <paramref name="radius"/> tiles of the cursor (O key).</summary>
        internal static AreaSurvey Survey(GridPos at, int radius)
        {
            var w = World.inst;
            var s = new AreaSurvey { Radius = radius };
            for (int z = at.Z - radius; z <= at.Z + radius; z++)
                for (int x = at.X - radius; x <= at.X + radius; x++)
                {
                    if (x < 0 || z < 0 || x >= w.GridWidth || z >= w.GridHeight) continue;
                    var c = w.GetCellData(x, z);
                    if (c == null) continue;
                    if (!Explored(c)) { s.Fog++; continue; }
                    if (IsWater(c))
                    {
                        if (c.deepWater) s.DeepWater++; else s.Water++;
                        if (FishAt(c) > 0) s.FishTiles++;
                        continue;
                    }
                    s.Land++;
                    if (c.Type == ResourceType.Stone) s.Stone++;
                    else if (c.Type == ResourceType.IronDeposit) s.Iron++;
                    int fert = c.GetEffectiveFertility();
                    if (fert >= 2) s.VeryFertile++; else if (fert == 1) s.Fertile++;
                    if (c.TreeAmount > 0) s.Forest++;
                    if (c.OccupyingStructure.Count > 0) s.Buildings++;
                    else if (c.TreeAmount == 0 && c.Type == ResourceType.None) s.Open++;
                }
            if (s.Stone == 0) s.NearestStone = Nearest(at, radius, c => c.Type == ResourceType.Stone);
            if (s.Iron == 0) s.NearestIron = Nearest(at, radius, c => c.Type == ResourceType.IronDeposit);
            if (s.Water == 0 && s.DeepWater == 0) s.NearestWater = Nearest(at, radius, IsWater);
            return s;
        }

        private static string Nearest(GridPos at, int skipRadius, System.Func<Cell, bool> match)
        {
            var w = World.inst;
            for (int r = skipRadius + 1; r <= 40; r++)
            {
                GridPos? best = null;
                double bestD = double.MaxValue;
                for (int dz = -r; dz <= r; dz++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dz)) != r) continue;
                        int x = at.X + dx, z = at.Z + dz;
                        if (x < 0 || z < 0 || x >= w.GridWidth || z >= w.GridHeight) continue;
                        var c = w.GetCellData(x, z);
                        if (c == null || !Explored(c) || !match(c)) continue;
                        var p = new GridPos(x, z);
                        double d = Directions.Euclid(at, p);
                        if (d < bestD) { bestD = d; best = p; }
                    }
                if (best.HasValue) return Directions.Relative(at, best.Value);
            }
            return null;
        }

        /// <summary>Name, age, job and current thought of a villager (what the villager panel shows).</summary>
        internal static string VillagerSummary(Villager v)
        {
            if (v == null) return Loc.T("villager");
            var parts = new List<string> { string.IsNullOrEmpty(v.name) ? Loc.T("villager") : v.name };
            try
            {
                parts.Add(string.Format(I2.Loc.ScriptLocalization.PersonUIYearsOld, Mathf.FloorToInt(v.timeAlive / Weather.inst.TimeInYear())));
            }
            catch (System.Exception)
            {
                // age is optional
            }
            string job = v.job != null ? TextUtil.Clean(v.job.GetDescription() ?? string.Empty) : string.Empty;
            parts.Add(job.Length > 0 ? job : Loc.T("no job"));
            if (v.Residence == null) parts.Add(Loc.T("homeless"));
            if (v.sick) parts.Add(Loc.T("sick with plague"));
            string thought = TextUtil.Clean(v.GetThought() ?? string.Empty);
            if (thought.Length > 0) parts.Add(Loc.F("thinks: {0}", thought));
            return string.Join(", ", parts.ToArray());
        }
    }
}

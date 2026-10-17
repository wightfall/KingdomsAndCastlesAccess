using System.Collections.Generic;
using KCAccess.Core;
using UnityEngine;

namespace KCAccess.Game
{
    /// <summary>
    /// What a building reaches. While a building is placed or selected the game colours its working area on the
    /// ground: the fire protection of wells, the trees of foresters, the fish of fishing huts, the irrigation of
    /// reservoirs, the farms next to windmills and the happiness that churches, taverns, gardens and the like give the
    /// houses around them. Nothing else tells it.
    /// </summary>
    internal static class Reach
    {
        /// <summary>"works within 15 tiles" for the build menu, or null for buildings without a working area.</summary>
        internal static string Radius(Building b)
        {
            if (b == null) return null;
            float r = 0f;
            foreach (var rb in b.GetComponents<RadiusBonus>()) r = Mathf.Max(r, rb.radiusMax);
            if (b.GetComponent<Well>() is Well well) r = Mathf.Max(r, well.maxProtectionRadius);
            if (b.GetComponent<Forester>() is Forester forester) r = Mathf.Max(r, forester.Radius);
            if (b.GetComponent<FishingHut>() is FishingHut hut) r = Mathf.Max(r, hut.Radius);
            if (b.GetComponent<Reservoir>() is Reservoir reservoir) r = Mathf.Max(r, reservoir.affectedRadius);
            int n = Mathf.RoundToInt(r);
            return n > 0 ? Loc.P(n, "works within {0} tile", "works within {0} tiles") : null;
        }

        /// <summary>What the building reaches where it stands (built, or held while placing it), or null.</summary>
        internal static string Text(Building b)
        {
            if (b == null || World.inst == null || Player.inst == null) return null;
            try
            {
                var parts = new List<string>();
                var bonuses = b.GetComponents<RadiusBonus>();
                if (bonuses.Length > 0) parts.Add(Happiness(b, bonuses));
                if (b.GetComponent<Well>() is Well well) parts.Add(FireProtection(b, well));
                if (b.GetComponent<Forester>() is Forester forester) parts.Add(Trees(b, forester));
                if (b.GetComponent<FishingHut>() is FishingHut hut) parts.Add(Fish(b, hut));
                if (b.GetComponent<Reservoir>() is Reservoir reservoir) parts.Add(Irrigation(b, reservoir));
                if (b.GetComponent<Windmill>() is Windmill mill) parts.Add(Farms(b, mill));
                parts.RemoveAll(string.IsNullOrEmpty);
                return parts.Count > 0 ? TextUtil.Join(", ", parts) : null;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning("Reach failed: " + e.Message);
                return null;
            }
        }

        /// <summary>Houses within the happiness radius (the game measures from the building's middle to a house's tile).</summary>
        private static string Happiness(Building b, RadiusBonus[] bonuses)
        {
            int lm = b.LandMass();
            var perLandmass = Player.inst.ResidentialsPerLandmass;
            if (lm < 0 || perLandmass == null || lm >= perLandmass.Length || perLandmass[lm] == null) return null;
            Vector3 center = b.Center();
            float r = 0f;
            int most = 0;
            foreach (var rb in bonuses)
            {
                r = Mathf.Max(r, rb.radiusMax);
                if (rb.happinessFunc != null) most = Mathf.Max(most, rb.GetMaximumHappiness());
            }
            int houses = 0;
            var homes = perLandmass[lm];
            for (int i = 0; i < homes.Count; i++)
            {
                var hb = homes.data[i] != null ? homes.data[i].GetComponent<Building>() : null;
                var c = hb != null && hb.IsBuilt() ? hb.GetCell() : null;
                if (c != null && Mathff.DistXZ(center, c.Center) <= r) houses++;
            }
            string s = Loc.P(houses, "{0} house in reach", "{0} houses in reach");
            return most > 0 ? s + ", " + Loc.F("happiness up to {0}", most) : s;
        }

        /// <summary>Buildings that can burn within the well's reach: fully protected close by, partly farther out.</summary>
        private static string FireProtection(Building b, Well well)
        {
            int lm = b.LandMass();
            var cell = b.GetCell();
            var list = lm >= 0 ? Player.inst.GetBuildingListForLandMass(lm) : null;
            if (cell == null || list == null) return null;
            int full = 0, partly = 0;
            for (int i = 0; i < list.Count; i++)
            {
                var o = list.data[i];
                if (o == null || o == b || !o.IsBuilt() || World.IsImmuneToFire(o)) continue;
                var oc = o.GetCell();
                if (oc == null) continue;
                float d = Vector3.Distance(cell.Position, oc.Position);
                if (d < well.minProtectionRadius) full++;
                else if (d <= well.maxProtectionRadius) partly++;
            }
            string s = Loc.P(full, "protects {0} building from fire", "protects {0} buildings from fire");
            return partly > 0 ? s + ", " + Loc.P(partly, "{0} more partly", "{0} more partly") : s;
        }

        /// <summary>Tiles with trees to cut and open tiles to plant within the forester's reach.</summary>
        private static string Trees(Building b, Forester forester)
        {
            int trees = 0, open = 0;
            World.inst.ForEachTileTouchingRadius(b.Center(), forester.Radius, (x, z, c) =>
            {
                if (c == null) return;
                if (c.TreeAmount > 0) trees++;
                else if (TreeGrowth.IsValidForTreePlanting(c)) open++;
            });
            return Loc.P(trees, "{0} tile with trees in reach", "{0} tiles with trees in reach") + ", " + Loc.P(open, "{0} open tile to plant", "{0} open tiles to plant");
        }

        /// <summary>Fish in the open water the fishing boat sails to (the game's own circle around the hut).</summary>
        private static string Fish(Building b, FishingHut hut)
        {
            Vector3 v = b.Center();
            int r = hut.Radius;
            int fish = 0;
            int x0 = Mathf.Max(0, (int)(v.x - r)), x1 = Mathf.Min(World.inst.GridWidth - 1, (int)(v.x + r));
            int z0 = Mathf.Max(0, (int)(v.z - r)), z1 = Mathf.Min(World.inst.GridHeight - 1, (int)(v.z + r));
            for (int z = z0; z <= z1; z++)
                for (int x = x0; x <= x1; x++)
                {
                    if ((v.x - x) * (v.x - x) + (v.z - z) * (v.z - z) > r * r) continue;
                    var c = World.inst.GetCellData(x, z);
                    if (c == null || c.SubStructure.Count > 0 || c.OccupyingStructure.Count > 0) continue;
                    fish += CellInfo.FishAt(c);
                }
            return Loc.P(fish, "{0} fish in reach", "{0} fish in reach");
        }

        /// <summary>Land the reservoir irrigates, and the farms, orchards and gardens whose own tile is in it.</summary>
        private static string Irrigation(Building b, Reservoir reservoir)
        {
            var cell = b.GetCell();
            if (cell == null) return null;
            int tiles = 0;
            var crops = new HashSet<Building>();
            World.inst.ForEachTileTouchingRadius(cell.Center, reservoir.affectedRadius, (x, z, c) =>
            {
                if (c == null || c.Type == ResourceType.Water) return;
                tiles++;
                foreach (var o in c.OccupyingStructure)
                {
                    if (o == null || o == b || o.GetCell() != c || !Player.inst.LandMassIsAPlayerLandMass(o.LandMass())) continue;
                    if (o.GetComponent<Field>() != null || o.GetComponent<Orchard>() != null || o.GetComponent<Garden>() != null || o.GetComponent<Park>() != null || o.GetComponent<Fountain>() != null)
                        crops.Add(o);
                }
            });
            string s = Loc.P(tiles, "irrigates {0} tile", "irrigates {0} tiles");
            return crops.Count > 0 ? s + ", " + Loc.P(crops.Count, "{0} farm, orchard or garden among them", "{0} farms, orchards or gardens among them") : s;
        }

        /// <summary>Farms close enough to get the windmill's bonus (a staffed windmill).</summary>
        private static string Farms(Building b, Windmill mill)
        {
            int lm = b.LandMass();
            var farms = lm >= 0 ? Player.inst.GetBuildingListForLandMass(lm, World.farmHash) : null;
            int n = 0;
            if (farms != null)
                for (int i = 0; i < farms.Count; i++)
                {
                    var f = farms.data[i];
                    if (f != null && f.GetComponent<Field>() != null && mill.InZone(f.GetCell())) n++;
                }
            return Loc.P(n, "{0} farm close enough for its bonus", "{0} farms close enough for its bonus");
        }
    }
}

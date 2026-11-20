using System;
using System.Collections.Generic;
using Engine;
using Game;
using SCIEPort.Features.Plants;
using SCIEPort.Features.SpecialTrees;
using static SCIEPort.Features.Landmarks.LandmarkTerrain;

namespace SCIEPort.Features.Landmarks
{
    internal static class SakuraCreek
    {
        internal static LandmarkPlan Generate(LandmarkTerrain terrain)
        {
            TreeSpecies species = TreeSpecies.Sakura;
            if (!species.Registered) return null;

            int reaches = 0;
            foreach (Point2 water in terrain.Candidates())
            {
                int level = terrain.WaterSurface(water);
                if (level <= 0 || terrain.Shore(water) < 14f || terrain.Humidity(water) < 6 || terrain.Temperature(water, level) < 4) continue;
                foreach (Point2 along in terrain.Bearings())
                {
                    if (!CreekFits(terrain, water, along, level)) continue;
                    LandmarkPlan plan = PlantBanks(terrain, water, along, level, species);
                    if (plan != null) return plan;
                    if (++reaches >= 12) return null;
                }
            }
            return null;
        }

        private static bool CreekFits(LandmarkTerrain terrain, Point2 center, Point2 along, int level)
        {
            for (int v = -2; v <= 2; v++)
                if (terrain.WaterSurface(At(center, along, 0, v)) != level) return false;
            for (int side = -1; side <= 1; side += 2)
            {
                bool bank = false;
                for (int u = 1; u <= 5; u++)
                {
                    Point2 column = At(center, along, u * side, 0);
                    int surface = terrain.Surface(column);
                    if (surface < level || surface > level + 8 || !Ground(terrain.SurfaceBlock(column))) continue;
                    bank = true;
                    break;
                }
                if (!bank) return false;
            }
            return true;
        }

        private static LandmarkPlan PlantBanks(LandmarkTerrain terrain, Point2 center, Point2 along, int level, TreeSpecies species)
        {
            var plan = new LandmarkPlan(terrain);
            var planted = new List<Point2>(3);
            // Claim an available tree site on each bank first, then fill an open spot upstream or downstream.
            for (int side = -1; side <= 1; side += 2)
                PlantBank(plan, terrain, planted, center, along, level, species, side);
            if (planted.Count < 3) PlantBank(plan, terrain, planted, center, along, level, species, 0);
            if (planted.Count < 2) return null;

            TryStoneTerrace(plan, terrain, center, along, level);
            return plan;
        }

        private static void PlantBank(LandmarkPlan plan, LandmarkTerrain terrain, List<Point2> planted, Point2 center,
            Point2 along, int level, TreeSpecies species, int side)
        {
            foreach (Point2 candidate in terrain.Candidates())
            {
                int dx = candidate.X - center.X, dz = candidate.Y - center.Y;
                int across = -dx * along.Y + dz * along.X;
                int lengthwise = dx * along.X + dz * along.Y;
                if (Math.Abs(across) > 6 || across == 0 || (side != 0 && Math.Sign(across) != side) || Math.Abs(lengthwise) > 5) continue;
                int y = terrain.Surface(candidate) + 1;
                if (y < level + 1 || y > level + 9 || !species.WildSite(terrain.SurfaceBlock(candidate), terrain.Temperature(candidate, y),
                    terrain.Humidity(candidate), y - terrain.Sea, terrain.Shore(candidate))) continue;
                if (planted.Exists(tree => Math.Abs(tree.X - candidate.X) + Math.Abs(tree.Y - candidate.Y) < 5)) continue;

                int variant = species.VariantAt(terrain.Seed, terrain.Chunk.Origin.X + candidate.X, terrain.Chunk.Origin.Y + candidate.Y);
                if (!plan.TryAddTree(candidate, y, species.GetBrush(variant, true), PlantHabitat.IsNaturalSoil)) continue;
                planted.Add(candidate);
                return;
            }
        }

        private static void TryStoneTerrace(LandmarkPlan plan, LandmarkTerrain terrain, Point2 center, Point2 along, int level)
        {
            for (int side = -1; side <= 1; side += 2)
            for (int u = 1; u <= 3; u++)
            {
                Point2 corner = At(center, along, side * u, 1);
                int height = terrain.Surface(corner);
                if (height < level || height > level + 2) continue;
                Point2[] tiles =
                {
                    corner, At(corner, along, side, 0), At(corner, along, 0, 1), At(corner, along, side, 1)
                };
                if (!Array.TrueForAll(tiles, tile => terrain.Surface(tile) == height && Ground(plan.Read(tile, height)) &&
                    Ground(plan.Read(tile, height - 1)) && Open(plan.Read(tile, height + 1)) && plan.Read(tile, height + 2) == 0)) continue;
                foreach (Point2 tile in tiles)
                {
                    plan.Set(tile, height, Terrain.MakeBlockValue(LimestoneBlock.Index), Ground);
                    plan.Set(tile, height + 1, 0, Open);
                }
                return;
            }
        }
    }
}

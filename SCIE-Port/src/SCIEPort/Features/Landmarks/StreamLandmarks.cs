using System;
using Engine;
using Game;
using static SCIEPort.Features.Landmarks.LandmarkTerrain;

namespace SCIEPort.Features.Landmarks
{
    internal static class StreamLandmarks
    {
        private static readonly int[] PoolWidths = { 1, 2, 3, 2, 1 };
        private static readonly int Source = Terrain.MakeBlockValue(WaterBlock.Index);
        private static readonly int Falling = Terrain.MakeBlockValue(WaterBlock.Index, 0, FluidBlock.SetLevel(0, 1));

        internal static LandmarkPlan Waterfall(LandmarkTerrain terrain, bool cave)
        {
            foreach (Point2 lip in terrain.Candidates())
            {
                int high = terrain.Surface(lip);
                if (high < terrain.Sea + 4 || terrain.Shore(lip) < 12f || terrain.Humidity(lip) < 6 || terrain.Temperature(lip, high) < 2) continue;
                int rock = terrain.SurfaceBlock(lip);
                if (!Rock(rock))
                {
                    if (!Ground(rock) || !Rock(terrain.Read(lip, high - 1))) continue;
                    rock = terrain.Read(lip, high - 1);
                }
                foreach (Point2 front in terrain.Bearings())
                {
                    Point2 fall = At(lip, front, 0, 1);
                    Point2 basin = At(lip, front, 0, 3);
                    if (!Inside(basin) || terrain.Surface(fall) >= high - 3) continue;
                    int level = terrain.WaterSurface(basin);
                    if (level == 0 && Ground(terrain.SurfaceBlock(basin))) level = terrain.Surface(basin) - 1;
                    if (level < terrain.Sea - 1 || high - level < 4 || high - level > 14) continue;
                    if (!SpringFits(terrain, lip, front, high)) continue;

                    var plan = new LandmarkPlan(terrain);
                    if (!DigPool(plan, terrain, lip, front, level, rock) || !FeedSpring(plan, lip, front, high, level)) continue;
                    if (cave && high - level >= 6) TryCave(plan, lip, front, level);
                    return plan;
                }
            }
            return null;
        }

        private static bool SpringFits(LandmarkTerrain terrain, Point2 lip, Point2 front, int high)
        {
            // A short level spring channel enters the air column over the cliff. It cannot leak
            // across the top of the hill when the native fluid simulator wakes this section.
            if (!Ground(terrain.Read(At(lip, front, 0, -2), high))) return false;
            for (int v = -1; v <= 0; v++)
            {
                Point2 channel = At(lip, front, 0, v);
                if (!Ground(terrain.Read(channel, high - 1)) || !Ground(terrain.Read(channel, high)) ||
                    !Open(terrain.Read(channel, high + 1))) return false;
                if (!Ground(terrain.Read(At(lip, front, -1, v), high)) || !Ground(terrain.Read(At(lip, front, 1, v), high))) return false;
            }
            return terrain.Read(At(lip, front, 0, 1), high) == 0;
        }

        private static bool DigPool(LandmarkPlan plan, LandmarkTerrain terrain, Point2 lip, Point2 front, int level, int rock)
        {
            for (int v = 1; v <= PoolWidths.Length; v++)
            for (int u = -PoolWidths[v - 1]; u <= PoolWidths[v - 1]; u++)
            {
                Point2 column = At(lip, front, u, v);
                int top = terrain.Surface(column);
                if (top < level - 2 || top > level + 2 || !PoolBed(terrain.SurfaceBlock(column))) return false;
                int depth = v >= 2 && v <= 4 && Math.Abs(u) <= 1 ? 3 : 2;
                int bed = level - depth;
                if (!Ground(terrain.Read(column, bed - 1)) || !plan.Set(column, bed, Terrain.MakeBlockValue(rock), PoolBed)) return false;
                for (int y = bed + 1; y <= level; y++)
                    if (!plan.Set(column, y, Source, PoolBed)) return false;
                for (int y = level + 1; y <= top + 1; y++)
                    if (!plan.Set(column, y, 0, block => Ground(block) || Open(block))) return false;
            }

            // Seal the excavated footprint into existing rock/earth or same-level water.
            // A shallow one-block dip gets a low stone edge, while deep cavities reject the site.
            for (int v = 1; v <= PoolWidths.Length; v++)
            for (int u = -PoolWidths[v - 1]; u <= PoolWidths[v - 1]; u++)
            {
                Point2 column = At(lip, front, u, v);
                foreach (Point2 side in terrain.Bearings())
                {
                    Point2 neighbor = column + side;
                    int atSurface = plan.Read(neighbor, level);
                    if (!Ground(atSurface) && atSurface != WaterBlock.Index)
                    {
                        if (!Ground(plan.Read(neighbor, level - 1)) || !Open(plan.Read(neighbor, level + 1)) ||
                            !plan.Set(neighbor, level, Terrain.MakeBlockValue(rock), Open)) return false;
                    }
                    for (int y = level - 2; y < level; y++)
                    {
                        if (plan.Read(column, y) != WaterBlock.Index) continue;
                        int wall = plan.Read(neighbor, y);
                        if (!Ground(wall) && wall != WaterBlock.Index) return false;
                    }
                }
            }
            return true;
        }

        private static bool FeedSpring(LandmarkPlan plan, Point2 lip, Point2 front, int high, int level)
        {
            for (int v = -1; v <= 0; v++)
            {
                Point2 channel = At(lip, front, 0, v);
                if (!plan.Set(channel, high, Source, Ground) || !plan.Set(channel, high + 1, 0, Open)) return false;
            }
            Point2 fall = At(lip, front, 0, 1);
            for (int y = level + 1; y <= high; y++)
                if (!plan.Set(fall, y, Falling, block => block == 0)) return false;
            return true;
        }

        private static void TryCave(LandmarkPlan plan, Point2 lip, Point2 front, int level)
        {
            for (int v = -2; v <= 0; v++)
            for (int u = v == 0 ? 0 : -1; u <= (v == 0 ? 0 : 1); u++)
            {
                Point2 cell = At(lip, front, u, v);
                if (!Rock(plan.Read(cell, level)) || !Rock(plan.Read(cell, level + 4))) return;
                for (int y = level + 1; y <= level + 3; y++)
                    if (!Rock(plan.Read(cell, y))) return;
            }
            for (int u = -1; u <= 1; u++)
            for (int y = level + 1; y <= level + 3; y++)
                if (!Rock(plan.Read(At(lip, front, u, -3), y))) return;
            for (int side = -2; side <= 2; side += 4)
            for (int v = -2; v <= -1; v++)
            for (int y = level + 1; y <= level + 3; y++)
                if (!Rock(plan.Read(At(lip, front, side, v), y))) return;

            for (int v = -2; v <= 0; v++)
            for (int u = v == 0 ? 0 : -1; u <= (v == 0 ? 0 : 1); u++)
            for (int y = level + 1; y <= level + 3; y++)
                plan.Set(At(lip, front, u, v), y, 0, Rock);
        }

        internal static LandmarkPlan Ford(LandmarkTerrain terrain, bool remnant)
        {
            foreach (Point2 bank in terrain.Candidates())
            {
                int top = terrain.Surface(bank);
                if (!Ground(terrain.SurfaceBlock(bank)) || terrain.Shore(bank) < 12f || Math.Abs(top - terrain.Sea) > 3 ||
                    terrain.Temperature(bank, top) <= 1) continue;
                foreach (Point2 front in terrain.Bearings())
                {
                    Point2 first = At(bank, front, 0, 1);
                    int level = terrain.WaterSurface(first);
                    if (level < top - 1 || level > top || !WalkableBank(terrain, bank, level)) continue;
                    int far = 1;
                    while (far <= 9 && terrain.WaterSurface(At(bank, front, 0, far)) == level) far++;
                    if (far < 4 || far > 9 || !WalkableBank(terrain, At(bank, front, 0, far), level)) continue;
                    if (!MakeSteppingStones(terrain, bank, front, far, level, out LandmarkPlan plan)) continue;
                    if (remnant) TryBridgeRemnant(plan, terrain, bank, front, level);
                    return plan;
                }
            }
            return null;
        }

        private static bool WalkableBank(LandmarkTerrain terrain, Point2 bank, int level)
        {
            int y = terrain.Surface(bank);
            return level > 0 && y >= level && y <= level + 1 && Ground(terrain.SurfaceBlock(bank)) &&
                Ground(terrain.Read(bank, y - 1)) && Open(terrain.Read(bank, y + 1)) && terrain.Read(bank, y + 2) == 0;
        }

        private static bool MakeSteppingStones(LandmarkTerrain terrain, Point2 bank, Point2 front, int far, int level, out LandmarkPlan plan)
        {
            plan = new LandmarkPlan(terrain);
            int masonry = Terrain.MakeBlockValue(CobblestoneBlock.Index);
            for (int v = 1; v < far; v += 2)
            {
                Point2 stone = At(bank, front, 0, v);
                if (terrain.WaterSurface(At(bank, front, -1, v)) != level || terrain.WaterSurface(At(bank, front, 1, v)) != level) return false;
                int bed = terrain.WaterBed(stone, level, 4);
                if (bed == 0 || terrain.Read(stone, level + 1) != 0 || terrain.Read(stone, level + 2) != 0) return false;
                for (int y = bed + 1; y <= level; y++)
                    if (!plan.Set(stone, y, masonry, block => block == WaterBlock.Index)) return false;
            }
            Point2 finish = At(bank, front, 0, far);
            foreach (Point2 landing in new[] { bank, finish })
            {
                int top = terrain.Surface(landing);
                if (!plan.Set(landing, top, Terrain.MakeBlockValue(GravelBlock.Index), Ground) ||
                    !plan.Set(landing, top + 1, 0, Open)) return false;
            }
            return true;
        }

        private static void TryBridgeRemnant(LandmarkPlan plan, LandmarkTerrain terrain, Point2 bank, Point2 front, int level)
        {
            for (int side = -2; side <= 2; side += 4)
            {
                Point2 landing = At(bank, front, side, 0);
                Point2 near = At(bank, front, side, 1);
                Point2 pier = At(bank, front, side, 2);
                if (!WalkableBank(terrain, landing, level) || terrain.WaterSurface(near) != level || terrain.WaterSurface(pier) != level ||
                    terrain.WaterSurface(At(bank, front, side, 3)) != level) continue;
                if (plan.Read(near, level + 1) != 0 || plan.Read(pier, level + 1) != 0) continue;
                int bed = terrain.WaterBed(pier, level, 4);
                if (bed == 0) continue;

                int plank = Terrain.MakeBlockValue(WoodenSlabBlock.Index);
                int timber = Terrain.MakeBlockValue(OakWoodBlock.Index);
                for (int y = bed + 1; y <= level; y++)
                    plan.Set(pier, y, timber, block => block == WaterBlock.Index);
                plan.Set(near, level + 1, plank, block => block == 0);
                plan.Set(pier, level + 1, plank, block => block == 0);
                return;
            }
        }
    }
}

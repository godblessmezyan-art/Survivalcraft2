using System;
using Engine;
using Game;
using static SCIEPort.Features.Landmarks.LandmarkTerrain;

namespace SCIEPort.Features.Landmarks
{
    internal static class StreamLandmarks
    {
        private static readonly int Source = Terrain.MakeBlockValue(WaterBlock.Index);
        private static readonly int Falling = Terrain.MakeBlockValue(WaterBlock.Index, 0, FluidBlock.SetLevel(0, 1));

        internal static LandmarkPlan Waterfall(LandmarkTerrain terrain, bool cave)
        {
            foreach (Point2 lip in terrain.Candidates())
            {
                int high = terrain.Surface(lip);
                if (high < terrain.Sea + 3 || !Ground(terrain.SurfaceBlock(lip)) || terrain.Humidity(lip) < 3 ||
                    terrain.Temperature(lip, high + 1) < 2 || terrain.Shore(lip) < 8f) continue;

                foreach (Point2 front in terrain.Bearings())
                {
                    if (!Inside(At(lip, front, -2, 5)) || !Inside(At(lip, front, 2, 5)) ||
                        !Inside(At(lip, front, 0, -1))) continue;
                    int toe = terrain.Surface(At(lip, front, 0, 3));
                    int level = toe - 1;
                    if (level < terrain.Sea || high <= toe || high - level > 13 ||
                        terrain.Surface(At(lip, front, 0, 1)) >= high) continue;

                    int spring = Math.Max(high, level + 4);
                    int rock = terrain.SurfaceBlock(lip);
                    if (!Rock(rock)) rock = terrain.Read(lip, high - 1);
                    if (!Rock(rock)) rock = LimestoneBlock.Index;
                    int stone = Terrain.MakeBlockValue(rock);
                    var plan = new LandmarkPlan(terrain);
                    if (!SculptPool(plan, terrain, lip, front, level, stone) ||
                        !SculptSpring(plan, terrain, lip, front, spring, level, stone)) continue;
                    if (cave && spring - level >= 6) TryCave(plan, lip, front, level);
                    return plan;
                }
            }
            return null;
        }

        private static bool SculptPool(LandmarkPlan plan, LandmarkTerrain terrain, Point2 lip, Point2 front, int level, int stone)
        {
            for (int v = 0; v <= 5; v++)
            for (int side = -2; side <= 2; side++)
            {
                Point2 column = At(lip, front, side, v);
                int top = terrain.Surface(column);
                if (!Ground(terrain.SurfaceBlock(column))) return false;
                bool rim = v == 0 || v == 5 || Math.Abs(side) == 2 || v == 4 && side != 0;
                if (rim)
                {
                    if (top < level - 2 || !FillTo(plan, column, top, level, stone)) return false;
                    for (int y = level - 1; y <= level; y++)
                        if (!Ground(plan.Read(column, y))) return false;
                    continue;
                }

                if (top < level - 1 || top > level + 4 || !Ground(plan.Read(column, level - 3))) return false;
                if (!plan.Set(column, level - 2, stone, Ground)) return false;
                for (int y = level - 1; y <= level; y++)
                    if (!plan.Set(column, y, Source, PoolBed)) return false;
                for (int y = level + 1; y <= top + 1; y++)
                    if (!plan.Set(column, y, 0, block => Ground(block) || Open(block))) return false;
            }
            return true;
        }

        private static bool SculptSpring(LandmarkPlan plan, LandmarkTerrain terrain, Point2 lip, Point2 front, int high, int level, int stone)
        {
            for (int side = -2; side <= 2; side++)
            {
                Point2 edge = At(lip, front, side, 0);
                int surface = terrain.Surface(edge);
                if (surface < high - 4 || !Ground(terrain.SurfaceBlock(edge))) return false;
                if (Math.Abs(side) == 2)
                {
                    if (!BuildSpringWall(plan, terrain, edge, high, stone)) return false;
                    continue;
                }
                if (!BuildSpringWall(plan, terrain, At(lip, front, side, -1), high, stone) ||
                    !FillTo(plan, edge, surface, high - 1, stone) || !Ground(plan.Read(edge, high - 1)) ||
                    !plan.Set(edge, high, Source, block => Ground(block) || Open(block)) ||
                    !plan.Set(edge, high + 1, 0, Open)) return false;

                Point2 fall = At(lip, front, side, 1);
                for (int y = level + 1; y <= high; y++)
                    if (!plan.Set(fall, y, Falling, Open)) return false;
            }
            return true;
        }

        private static bool BuildSpringWall(LandmarkPlan plan, LandmarkTerrain terrain, Point2 wall, int high, int stone)
        {
            int surface = terrain.Surface(wall);
            return surface >= high - 4 && Ground(terrain.SurfaceBlock(wall)) &&
                FillTo(plan, wall, surface, high, stone) && Ground(plan.Read(wall, high));
        }

        private static bool FillTo(LandmarkPlan plan, Point2 column, int top, int y, int stone)
        {
            for (int height = top + 1; height <= y; height++)
                if (!plan.Set(column, height, stone, Open)) return false;
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

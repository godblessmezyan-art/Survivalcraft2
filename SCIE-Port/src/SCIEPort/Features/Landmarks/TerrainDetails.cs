using System;
using Engine;
using Game;
using SCIEPort.Features.Geology;
using static SCIEPort.Features.Landmarks.LandmarkTerrain;

namespace SCIEPort.Features.Landmarks
{
    internal static class TerrainDetails
    {
        internal static LandmarkPlan TidePools(LandmarkTerrain terrain)
        {
            foreach (Point2 center in terrain.Candidates())
            {
                int level = terrain.Surface(center);
                float shore = terrain.Shore(center);
                if (shore < -4 || shore > 15 || level < terrain.Sea + 1 || level > terrain.Sea + 5 ||
                    terrain.Temperature(center, level) < 4 || !terrain.HasOpenGround(center, level, 0)) continue;
                var plan = new LandmarkPlan(terrain);
                if (CarvePools(terrain, plan, center, level)) return plan;
            }
            return null;
        }

        private static bool CarvePools(LandmarkTerrain terrain, LandmarkPlan plan, Point2 center, int level)
        {
            for (int z = -2; z <= 3; z++)
            for (int x = -4; x <= 4; x++)
            {
                int distance = Math.Min(Math.Abs(x + 2) + Math.Abs(z), Math.Abs(x - 2) + Math.Abs(z - 1));
                if (distance > 2) continue;
                var point = center + new Point2(x, z);
                if (!terrain.HasOpenGround(point, level, 1)) return false;
                if (distance <= 1)
                {
                    if (!plan.Set(point, level, 0, GroundOrCover) ||
                        !plan.Set(point, level - 1, Terrain.MakeBlockValue(WaterBlock.Index), Ground) ||
                        !plan.Set(point, level - 2, Terrain.MakeBlockValue(GravelBlock.Index), Ground)) return false;
                }
                else if (!plan.Set(point, level - 1, Terrain.MakeBlockValue(LimestoneBlock.Index), Ground)) return false;
            }
            return true;
        }

        internal static LandmarkPlan MarshIslets(LandmarkTerrain terrain)
        {
            foreach (Point2 center in terrain.Candidates())
            {
                int level = terrain.WaterSurface(center);
                if (level == 0 || terrain.Shore(center) < 8 || terrain.Humidity(center) < 7 ||
                    terrain.Temperature(center, level) < 4) continue;
                foreach (Point2 front in terrain.Bearings())
                {
                    var plan = new LandmarkPlan(terrain);
                    if (BuildIslets(terrain, plan, center, front, level)) return plan;
                }
            }
            return null;
        }

        private static bool BuildIslets(LandmarkTerrain terrain, LandmarkPlan plan, Point2 center, Point2 front, int level)
        {
            for (int step = -4; step <= 4; step++)
            for (int side = -2; side <= 2; side++)
            {
                Point2 point = At(center, front, side, step);
                int bed = terrain.WaterBed(point, level, 4);
                if (terrain.WaterSurface(point) != level || bed == 0) return false;
                bool island = step < -1 && Math.Abs(side + step + 3) < 2 ||
                    step > 1 && Math.Abs(side - step + 3) < 2;
                if (!island) continue;
                for (int y = bed + 1; y <= level; y++)
                    if (!plan.Set(point, y, Terrain.MakeBlockValue(DirtBlock.Index), block => block == WaterBlock.Index)) return false;
                if (!plan.Set(point, level + 1, Terrain.MakeBlockValue(GrassBlock.Index), Open)) return false;
            }
            return true;
        }

        internal static LandmarkPlan SnowCornice(LandmarkTerrain terrain)
        {
            foreach (Point2 center in terrain.Candidates())
            {
                int level = terrain.Surface(center);
                if (level < terrain.Sea + 12 || terrain.Temperature(center, level) > 0 ||
                    !terrain.HasPlateau(center, level, 4, 3)) continue;
                foreach (Point2 front in terrain.Bearings())
                {
                    var plan = new LandmarkPlan(terrain);
                    if (ShapeSnowRidge(terrain, plan, center, front)) return plan;
                }
            }
            return null;
        }

        private static bool ShapeSnowRidge(LandmarkTerrain terrain, LandmarkPlan plan, Point2 center, Point2 front)
        {
            int bias = GeologySites.Grain(terrain.Chunk.Origin.X + center.X, terrain.Chunk.Origin.Y + center.Y, terrain.Seed, 2);
            for (int step = -4; step <= 4; step++)
            for (int side = -2; side <= 2; side++)
            {
                int distance = Math.Abs(side - (Math.Abs(step) / 3 - bias));
                int lift = 3 - distance - Math.Abs(step) / 2;
                if (lift <= 0) continue;
                Point2 point = At(center, front, side, step);
                int ground = terrain.Surface(point);
                for (int y = 1; y <= lift; y++)
                    if (!plan.Set(point, ground + y, Terrain.MakeBlockValue(GraniteBlock.Index), Open)) return false;
                if (!plan.Set(point, ground + lift + 1, Terrain.MakeBlockValue(SnowBlock.Index), Open)) return false;
            }
            return true;
        }

        internal static LandmarkPlan SandstoneSlot(LandmarkTerrain terrain)
        {
            foreach (Point2 center in terrain.Candidates())
            {
                int level = terrain.Surface(center);
                if (level < terrain.Sea + 6 || terrain.Shore(center) < 20 || terrain.Humidity(center) > 6 ||
                    terrain.Temperature(center, level) < 6 || !HasSand(terrain.SurfaceBlock(center))) continue;
                foreach (Point2 front in terrain.Bearings())
                {
                    var plan = new LandmarkPlan(terrain);
                    if (CutSlot(terrain, plan, center, front, level)) return plan;
                }
            }
            return null;
        }

        private static bool CutSlot(LandmarkTerrain terrain, LandmarkPlan plan, Point2 center, Point2 front, int level)
        {
            for (int step = -5; step <= 5; step++)
            for (int side = -2; side <= 2; side++)
            {
                Point2 point = At(center, front, side + (step >= 1 ? 1 : 0), step);
                if (!terrain.HasOpenGround(point, level, 1) || !HasSand(terrain.SurfaceBlock(point))) return false;
                int surface = terrain.Surface(point);
                if (Math.Abs(side) == 2)
                {
                    if (Math.Abs(step) <= 3 && !plan.Set(point, surface, Terrain.MakeBlockValue(SandstoneBlock.Index), Ground))
                        return false;
                    continue;
                }
                int depth = Math.Min(3, 5 - Math.Abs(step));
                for (int y = surface; y > surface - depth; y--)
                    if (!plan.Set(point, y, 0, Ground)) return false;
                if (!plan.Set(point, surface - depth, Terrain.MakeBlockValue(GravelBlock.Index), Ground)) return false;
            }
            return true;
        }

        private static bool GroundOrCover(int block) => Ground(block) || Open(block);
        private static bool HasSand(int block) => block == SandBlock.Index || block == SandstoneBlock.Index;
    }
}

using System;
using Engine;
using Game;
using SCIEPort.Features.Geology;
using static SCIEPort.Features.Landmarks.LandmarkTerrain;

namespace SCIEPort.Features.Landmarks
{
    internal static class SurfaceLandmarks
    {
        internal static LandmarkPlan ScreeFan(LandmarkTerrain terrain)
        {
            foreach (Point2 apex in terrain.Candidates())
            {
                int level = terrain.Surface(apex);
                if (level < terrain.Sea + 8 || terrain.Shore(apex) < 20 || !Rock(terrain.SurfaceBlock(apex))) continue;
                foreach (Point2 down in terrain.Bearings())
                {
                    int drop = level - terrain.Surface(At(apex, down, 0, 6));
                    if (drop < 3 || drop > 10) continue;
                    var plan = new LandmarkPlan(terrain);
                    if (LayScree(terrain, plan, apex, down, level, drop)) return plan;
                }
            }
            return null;
        }

        private static bool LayScree(LandmarkTerrain terrain, LandmarkPlan plan, Point2 apex, Point2 down, int level, int drop)
        {
            for (int step = 0; step <= 6; step++)
            {
                int width = (step + 1) / 2;
                for (int side = -width; side <= width; side++)
                {
                    Point2 column = At(apex, down, side, step);
                    if (!terrain.HasOpenGround(column, level - drop * step / 6, 2)) return false;
                    int y = terrain.Surface(column);
                    int grain = GeologySites.Grain(terrain.Chunk.Origin.X + column.X, terrain.Chunk.Origin.Y + column.Y, terrain.Seed, 7);
                    if (Math.Abs(side) == width && step >= 3 && grain < 2) continue;
                    if (!plan.Set(column, y, Terrain.MakeBlockValue(GravelBlock.Index), Ground)) return false;
                    if (grain == 6 && Math.Abs(side) < width &&
                        !plan.Set(column, y + 1, Terrain.MakeBlockValue(terrain.SurfaceBlock(apex)), Open)) return false;
                }
            }
            return true;
        }

        internal static LandmarkPlan MeadowHummocks(LandmarkTerrain terrain)
        {
            foreach (Point2 center in terrain.Candidates())
            {
                int level = terrain.Surface(center);
                if (level < terrain.Sea + 3 || terrain.Shore(center) < 18 || terrain.Humidity(center) < 8 ||
                    terrain.Temperature(center, level) < 4 || terrain.SurfaceBlock(center) != GrassBlock.Index ||
                    !terrain.HasPlateau(center, level, 4, 2)) continue;
                var plan = new LandmarkPlan(terrain);
                if (BuildHummocks(terrain, plan, center)) return plan;
            }
            return null;
        }

        private static bool BuildHummocks(LandmarkTerrain terrain, LandmarkPlan plan, Point2 center)
        {
            for (int z = -3; z <= 3; z++)
            for (int x = -3; x <= 3; x++)
            {
                int west = (x + 2) * (x + 2) + (z - 1) * (z - 1);
                int east = (x - 2) * (x - 2) + (z + 1) * (z + 1);
                int north = x * x + (z + 3) * (z + 3);
                int distance = Math.Min(west, Math.Min(east, north));
                if (distance > 4) continue;
                Point2 column = center + new Point2(x, z);
                if (terrain.SurfaceBlock(column) != GrassBlock.Index) return false;
                int y = terrain.Surface(column);
                if (!plan.Set(column, y + 1, Terrain.MakeBlockValue(distance <= 1 ? GraniteBlock.Index : GrassBlock.Index), Open)) return false;
                if (distance <= 1 && !plan.Set(column, y + 2, Terrain.MakeBlockValue(GrassBlock.Index), Open)) return false;
            }
            return true;
        }

        internal static LandmarkPlan FrostPolygons(LandmarkTerrain terrain)
        {
            foreach (Point2 center in terrain.Candidates())
            {
                int level = terrain.Surface(center);
                if (level < terrain.Sea + 10 || terrain.Shore(center) < 16 ||
                    terrain.Temperature(center, level) > 0 || !terrain.HasOpenGround(center, level, 0)) continue;
                var plan = new LandmarkPlan(terrain);
                if (TracePolygons(terrain, plan, center)) return plan;
            }
            return null;
        }

        private static bool TracePolygons(LandmarkTerrain terrain, LandmarkPlan plan, Point2 center)
        {
            int level = terrain.Surface(center);
            int stones = 0, snow = 0;
            int phase = GeologySites.Grain(terrain.Chunk.Origin.X + center.X, terrain.Chunk.Origin.Y + center.Y, terrain.Seed, 3);
            for (int z = -4; z <= 4; z++)
            for (int x = -4; x <= 4; x++)
            {
                if (Math.Abs(x) + Math.Abs(z) > 7) continue;
                Point2 column = center + new Point2(x, z);
                if (!terrain.HasOpenGround(column, level, 2)) continue;
                int row = (z + 4 + phase) / 3;
                bool seam = (z + 4 + phase) % 3 == 0 || (x + 5 + (row & 1) * 2) % 4 == 0;
                int y = terrain.Surface(column);
                if (!plan.Set(column, y, Terrain.MakeBlockValue(seam ? GravelBlock.Index : GraniteBlock.Index), Ground)) return false;
                if (!plan.Set(column, y + 1, seam ? 0 : Terrain.MakeBlockValue(SnowBlock.Index), Open)) return false;
                if (seam) stones++;
                else snow++;
            }
            return stones >= 12 && snow >= 14;
        }
    }
}

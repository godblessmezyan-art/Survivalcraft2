using System;
using Engine;
using Game;
using SCIEPort.Features.Geology;
using static SCIEPort.Features.Landmarks.LandmarkTerrain;

namespace SCIEPort.Features.Landmarks
{
    internal static class RockLandmarks
    {
        internal static LandmarkPlan SeaArch(LandmarkTerrain terrain)
        {
            int level = terrain.Sea;
            foreach (Point2 opening in terrain.Candidates())
            {
                if (terrain.WaterSurface(opening) != level || terrain.Shore(opening) < -24f || terrain.Shore(opening) > 10f) continue;
                foreach (Point2 span in terrain.Bearings())
                {
                    Point2 headland = At(opening, span, 0, -4);
                    int top = terrain.Surface(headland);
                    if (top < level || top > level + 6 || !Ground(terrain.SurfaceBlock(headland))) continue;
                    int rock = HeadlandRock(terrain, headland, top);
                    if (rock == 0 || !BoatChannelFits(terrain, opening, span, level)) continue;
                    var plan = new LandmarkPlan(terrain);
                    if (RaiseArch(plan, terrain, opening, span, level, rock)) return plan;
                }
            }
            return null;
        }

        internal static LandmarkPlan SeaStacks(LandmarkTerrain terrain)
        {
            int level = terrain.Sea;
            foreach (Point2 center in terrain.Candidates())
            {
                if (terrain.WaterSurface(center) != level || terrain.Shore(center) < -38f || terrain.Shore(center) > 6f) continue;
                foreach (Point2 span in terrain.Bearings())
                {
                    var plan = new LandmarkPlan(terrain);
                    if (RaiseStacks(plan, terrain, center, span, level)) return plan;
                }
            }
            return null;
        }

        private static bool RaiseStacks(LandmarkPlan plan, LandmarkTerrain terrain, Point2 center, Point2 span, int level)
        {
            // Leave the middle three columns in each gap open at the waterline.
            for (int distance = -3; distance <= 3; distance++)
            {
                if (distance == 0) continue;
                Point2 gap = At(center, span, 0, distance);
                if (terrain.WaterSurface(gap) != level || terrain.Read(gap, level - 2) != WaterBlock.Index) return false;
            }
            for (int distance = -4; distance <= 4; distance += 4)
            {
                Point2 column = At(center, span, 0, distance);
                if (terrain.WaterSurface(column) != level) return false;
                int bed = terrain.WaterBed(column, level, 13);
                if (bed == 0 || level - bed < 3) return false;
                int rock = HeadlandRock(terrain, column, bed);
                if (rock == 0) rock = BasaltBlock.Index;
                int height = 2 + GeologySites.Grain(terrain.Chunk.Origin.X + column.X,
                    terrain.Chunk.Origin.Y + column.Y, terrain.Seed, 4);
                for (int y = bed + 1; y <= level + height; y++)
                    if (!plan.Set(column, y, Terrain.MakeBlockValue(rock), block => block == WaterBlock.Index || block == 0)) return false;

                Point2 shoulder = At(center, span, distance == 0 ? -1 : 1, distance);
                if (terrain.WaterSurface(shoulder) != level) return false;
                int shoulderBed = terrain.WaterBed(shoulder, level, 13);
                if (shoulderBed == 0 || level - shoulderBed < 3) return false;
                for (int y = shoulderBed + 1; y <= level + height - 2; y++)
                    if (!plan.Set(shoulder, y, Terrain.MakeBlockValue(rock), block => block == WaterBlock.Index || block == 0)) return false;
            }
            return true;
        }

        private static int HeadlandRock(LandmarkTerrain terrain, Point2 bank, int top)
        {
            for (int depth = 0; depth <= 4; depth++)
            {
                int rock = terrain.Read(bank, top - depth);
                if (Rock(rock)) return rock;
                if (!Ground(rock)) return 0;
            }
            return terrain.SurfaceBlock(bank) == SandBlock.Index ? SandstoneBlock.Index : 0;
        }

        private static bool BoatChannelFits(LandmarkTerrain terrain, Point2 opening, Point2 span, int level)
        {
            for (int v = -1; v <= 1; v++)
            for (int u = -3; u <= 3; u++)
            {
                Point2 channel = At(opening, span, u, v);
                if (terrain.WaterSurface(channel) != level || terrain.Read(channel, level - 1) != WaterBlock.Index) return false;
                for (int y = level + 1; y <= level + 3; y++)
                    if (terrain.Read(channel, y) != 0) return false;
            }
            return terrain.WaterSurface(At(opening, span, 0, 3)) == level;
        }

        private static bool RaiseArch(LandmarkPlan plan, LandmarkTerrain terrain, Point2 opening, Point2 span, int level, int rock)
        {
            int stone = Terrain.MakeBlockValue(rock);
            for (int v = -4; v <= 4; v++)
            for (int u = -1; u <= 1; u++)
            {
                // Shorter flanks and rounded shoulders break up the square silhouette.
                if (Math.Abs(v) == 4 && u != 0) continue;
                Point2 column = At(opening, span, u, v);
                int distance = Math.Abs(v);
                int bottom = level + (distance <= 1 ? 4 : 3);
                int top = level + (distance <= 1 ? 7 : distance == 2 ? 6 : 5) - Math.Abs(u);
                if (distance >= 3)
                {
                    int bed = terrain.WaterSurface(column) == level ? terrain.WaterBed(column, level, 8) : terrain.Surface(column);
                    if (bed == 0 || !Ground(terrain.Read(column, bed)) || bed > top) return false;
                    bottom = bed + 1;
                }
                for (int y = bottom; y <= top; y++)
                {
                    if (Ground(plan.Read(column, y))) continue;
                    if (!plan.Set(column, y, stone, block => Open(block) || distance >= 3 && block == WaterBlock.Index)) return false;
                }
            }
            return true;
        }

        internal static LandmarkPlan SnowShelter(LandmarkTerrain terrain)
        {
            foreach (Point2 approach in terrain.Candidates())
            {
                int floor = terrain.Surface(approach);
                if (floor < terrain.Sea + 14 || !Ground(terrain.SurfaceBlock(approach)) || terrain.Temperature(approach, floor) > 0) continue;
                foreach (Point2 inward in terrain.Bearings())
                {
                    Point2 mouth = At(approach, inward, 0, 1);
                    if (terrain.Surface(mouth) < floor + 4 || !Rock(terrain.Read(mouth, floor + 1))) continue;
                    if (!HasLedge(terrain, approach, inward, floor)) continue;
                    var plan = new LandmarkPlan(terrain);
                    if (CarveShelter(plan, mouth, inward, floor)) return plan;
                }
            }
            return null;
        }

        private static bool HasLedge(LandmarkTerrain terrain, Point2 approach, Point2 inward, int floor)
        {
            int previous = 0;
            for (int u = -2; u <= 2; u++)
            {
                Point2 step = At(approach, inward, u, 0);
                int height = terrain.Surface(step);
                if (Math.Abs(floor - height) > 1 || (previous != 0 && Math.Abs(previous - height) > 1) ||
                    !Ground(terrain.SurfaceBlock(step)) || !Open(terrain.Read(step, height + 1)) || terrain.Read(step, height + 2) != 0) return false;
                previous = height;
            }
            return true;
        }

        private static bool CarveShelter(LandmarkPlan plan, Point2 mouth, Point2 inward, int floor)
        {
            for (int v = 0; v <= 2; v++)
            {
                int width = v == 2 ? 1 : 2;
                for (int u = -width; u <= width; u++)
                {
                    Point2 cell = At(mouth, inward, u, v);
                    int clearance = Math.Abs(u) <= 1 && v < 2 ? 3 : 2;
                    if (!Rock(plan.Read(cell, floor)) || !Rock(plan.Read(cell, floor + clearance + 1))) return false;
                    for (int y = floor + 1; y <= floor + clearance; y++)
                        if (!plan.Set(cell, y, 0, Rock)) return false;
                }
            }

            for (int u = -1; u <= 1; u++)
            for (int y = floor + 1; y <= floor + 2; y++)
                if (!Rock(plan.Read(At(mouth, inward, u, 3), y))) return false;
            for (int direction = -1; direction <= 1; direction += 2)
            for (int v = 0; v <= 2; v++)
            for (int y = floor + 1; y <= floor + 2; y++)
                if (!Rock(plan.Read(At(mouth, inward, direction * (v == 2 ? 2 : 3), v), y))) return false;
            return true;
        }
    }
}

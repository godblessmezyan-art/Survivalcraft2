using System;
using Engine;
using Game;
using SCIEPort.Features.Geology;
using static SCIEPort.Features.Landmarks.LandmarkTerrain;

namespace SCIEPort.Features.Landmarks
{
    /// <summary>
    /// Landmarks built from stone: a megalith ring, hoodoo spires and a karst sinkhole.
    /// Every generator only proposes cells inside the inner 1..14 columns of one chunk and
    /// commits through <see cref="LandmarkPlan"/>, so a rejected site writes nothing.
    /// </summary>
    internal static class StoneLandmarks
    {
        private static readonly Point2[] RingOffsets =
        {
            new Point2(3, 0), new Point2(2, 2), new Point2(0, 3), new Point2(-2, 2),
            new Point2(-3, 0), new Point2(-2, -2), new Point2(0, -3), new Point2(2, -2)
        };

        private static readonly Point2[] SpireOffsets =
        {
            new Point2(-2, -1), new Point2(2, 1), new Point2(1, -3), new Point2(-1, 2)
        };

        internal static LandmarkPlan StoneCircle(LandmarkTerrain terrain)
        {
            foreach (Point2 center in terrain.Candidates())
            {
                if (!Fits(center, 4)) continue;
                int level = terrain.Surface(center);
                if (level < terrain.Sea + 2 || terrain.SurfaceBlock(center) == SandBlock.Index) continue;
                if (terrain.Shore(center) < 24f || terrain.Humidity(center) < 3) continue;
                if (terrain.Temperature(center, level) < 1) continue;
                if (!terrain.HasPlateau(center, level, 3, 1)) continue;

                var plan = new LandmarkPlan(terrain);
                if (Ring(plan, terrain, center, level)) return plan;
            }
            return null;
        }

        private static bool Ring(LandmarkPlan plan, LandmarkTerrain terrain, Point2 center, int level)
        {
            for (int index = 0; index < RingOffsets.Length; index++)
            {
                Point2 column = center + RingOffsets[index];
                int baseY = terrain.Surface(column);
                int stone = Masonry(terrain, column, baseY);
                if (stone < 0) return false;
                int height = 2 + GeologySites.Grain(center.X + index, center.Y, terrain.Seed, 3);
                for (int y = baseY + 1; y <= baseY + height; y++)
                    if (!plan.Set(column, y, Terrain.MakeBlockValue(stone), Open)) return false;
                if ((index & 1) == 0 &&
                    !plan.Set(column, baseY + height + 1, Terrain.MakeBlockValue(SandstoneBlock.Index), Open)) return false;
            }
            return plan.Set(center, level + 1, Terrain.MakeBlockValue(LimestoneBlock.Index), Open);
        }

        internal static LandmarkPlan HoodooField(LandmarkTerrain terrain)
        {
            foreach (Point2 center in terrain.Candidates())
            {
                // Caps reach one column past the stalk, so the footprint needs one extra ring.
                if (!Fits(center, 5)) continue;
                int level = terrain.Surface(center);
                if (level < terrain.Sea + 2 || terrain.Shore(center) < 18f) continue;
                if (terrain.Temperature(center, level) < 6 || terrain.Humidity(center) > 6) continue;
                if (!terrain.HasPlateau(center, level, 3, 2)) continue;

                var plan = new LandmarkPlan(terrain);
                if (Spires(plan, terrain, center)) return plan;
            }
            return null;
        }

        private static bool Spires(LandmarkPlan plan, LandmarkTerrain terrain, Point2 center)
        {
            for (int index = 0; index < SpireOffsets.Length; index++)
            {
                Point2 column = center + SpireOffsets[index];
                int baseY = terrain.Surface(column);
                int height = 3 + GeologySites.Grain(column.X, column.Y, terrain.Seed + index, 4);
                int surface = terrain.SurfaceBlock(column);
                int cap = Rock(surface) ? surface : GraniteBlock.Index;

                for (int y = baseY + 1; y <= baseY + height; y++)
                    if (!plan.Set(column, y, Terrain.MakeBlockValue(SandstoneBlock.Index), Open)) return false;

                // A cap one block wider than the stalk is what makes the silhouette a mushroom.
                for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                    if (!plan.Set(new Point2(column.X + dx, column.Y + dz), baseY + height + 1,
                        Terrain.MakeBlockValue(cap), Open)) return false;
            }
            return true;
        }

        internal static LandmarkPlan Sinkhole(LandmarkTerrain terrain)
        {
            foreach (Point2 center in terrain.Candidates())
            {
                if (!Fits(center, 5)) continue;
                int level = terrain.Surface(center);
                if (level < terrain.Sea + 8 || terrain.Shore(center) < 18f) continue;
                if (terrain.Humidity(center) < 7 || terrain.Temperature(center, level) < 2) continue;
                if (!terrain.HasPlateau(center, level, 4, 2)) continue;

                foreach (Point2 slope in terrain.Bearings())
                {
                    var plan = new LandmarkPlan(terrain);
                    if (Carve(plan, terrain, center, slope, level)) return plan;
                }
            }
            return null;
        }

        private static bool Carve(LandmarkPlan plan, LandmarkTerrain terrain, Point2 center, Point2 slope, int level)
        {
            int depth = 5 + GeologySites.Grain(center.X, center.Y, terrain.Seed, 2);
            int floor = level - depth;
            int exitHeight = terrain.Surface(At(center, slope, 0, 4));
            if (exitHeight > floor + 6 || !terrain.HasOpenGround(At(center, slope, 0, 5), exitHeight, 1)) return false;
            int rimmed = 0;
            for (int dz = -4; dz <= 4; dz++)
            for (int dx = -4; dx <= 4; dx++)
            {
                Point2 column = At(center, slope, dx, dz);
                int distance = dx * dx + dz * dz;
                int surface = terrain.Surface(column);
                bool exit = (dx == 0 || dx == 1) && dz >= -2;
                if (distance <= 12 || exit)
                {
                    int bottom = exit ? Math.Max(floor, exitHeight - (4 - dz)) : floor + (distance > 6 ? 1 : 0);
                    if (bottom > surface || !Ground(terrain.Read(column, bottom - 1))) return false;
                    int top = surface + (IsCover(terrain.Read(column, surface + 1)) ? 1 : 0);
                    for (int y = bottom + 1; y <= top; y++)
                        if (!plan.Set(column, y, 0, block => Ground(block) || Open(block))) return false;
                    int lining = distance <= 2 && !exit ? GravelBlock.Index : LimestoneBlock.Index;
                    if (!plan.Set(column, bottom, Terrain.MakeBlockValue(lining), Ground)) return false;
                }
                else if (distance <= 20 && surface == level)
                {
                    // Lay a limestone lip on the intact rim so the hole reads as cut into rock.
                    if (plan.Set(column, surface, Terrain.MakeBlockValue(LimestoneBlock.Index), Ground)) rimmed++;
                }
            }
            return rimmed >= 6;
        }

        /// <summary>Keeps a footprint of the given radius inside the 1..14 columns of the chunk.</summary>
        private static bool Fits(Point2 center, int radius)
            => center.X >= radius && center.X <= 15 - radius && center.Y >= radius && center.Y <= 15 - radius;

        /// <summary>Prefers the rock already under the site so the stones match local geology.</summary>
        private static int Masonry(LandmarkTerrain terrain, Point2 column, int surface)
        {
            for (int depth = 0; depth <= 4; depth++)
            {
                int block = terrain.Read(column, surface - depth);
                if (Rock(block)) return block;
                if (!Ground(block)) return -1;
            }
            return GraniteBlock.Index;
        }
    }
}

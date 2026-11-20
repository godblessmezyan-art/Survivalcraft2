using System;
using Engine;
using Game;
using SCIEPort.Features.Geology;
using static SCIEPort.Features.Landmarks.LandmarkTerrain;

namespace SCIEPort.Features.Landmarks
{
    /// <summary>
    /// Climate-bound landmarks: blue-ice seracs on frozen ridges and a wind-built dune ridge
    /// in hot deserts. Both only raise material, so nothing has to be dug out of the terrain.
    /// </summary>
    internal static class WildLandmarks
    {
        private static readonly Point2[] TowerCorners =
        {
            new Point2(-2, -2), new Point2(1, -2), new Point2(-2, 1), new Point2(1, 1)
        };

        internal static LandmarkPlan Seracs(LandmarkTerrain terrain)
        {
            if (!(BlocksManager.Blocks[BlueIceBlock.Index] is BlueIceBlock)) return null;
            foreach (Point2 center in terrain.Candidates())
            {
                if (center.X < 4 || center.X > 11 || center.Y < 4 || center.Y > 11) continue;
                int level = terrain.Surface(center);
                if (level < terrain.Sea + 8) continue;
                if (terrain.Temperature(center, level) > 0) continue;
                if (terrain.Humidity(center) < 4) continue;
                if (!terrain.HasPlateau(center, level, 2, 2)) continue;

                var plan = new LandmarkPlan(terrain);
                if (Towers(plan, terrain, center)) return plan;
            }
            return null;
        }

        private static bool Towers(LandmarkPlan plan, LandmarkTerrain terrain, Point2 center)
        {
            for (int index = 0; index < TowerCorners.Length; index++)
            {
                Point2 corner = center + TowerCorners[index];
                int height = 2 + GeologySites.Grain(corner.X, corner.Y, terrain.Seed + index, 4);
                for (int dz = 0; dz <= 1; dz++)
                for (int dx = 0; dx <= 1; dx++)
                {
                    var column = new Point2(corner.X + dx, corner.Y + dz);
                    int baseY = terrain.Surface(column);
                    int top = baseY + height + ((dx + dz + index) & 1);
                    for (int y = baseY + 1; y <= top; y++)
                        if (!plan.Set(column, y, Terrain.MakeBlockValue(BlueIceBlock.Index), Open)) return false;
                    if (!plan.Set(column, top + 1, Terrain.MakeBlockValue(SnowBlock.Index), Open)) return false;
                }
            }
            return true;
        }

        internal static LandmarkPlan DuneRidge(LandmarkTerrain terrain)
        {
            foreach (Point2 center in terrain.Candidates())
            {
                if (center.X < 5 || center.X > 10 || center.Y < 6 || center.Y > 9) continue;
                int level = terrain.Surface(center);
                if (terrain.SurfaceBlock(center) != SandBlock.Index || level < terrain.Sea + 1) continue;
                if (terrain.Temperature(center, level) < 7 || terrain.Humidity(center) > 5) continue;
                if (terrain.Shore(center) < 22f) continue;
                var plan = new LandmarkPlan(terrain);
                if (Ridge(plan, terrain, center, level)) return plan;
            }
            return null;
        }

        private static bool Ridge(LandmarkPlan plan, LandmarkTerrain terrain, Point2 center, int level)
        {
            int crest = 2 + GeologySites.Grain(center.X, center.Y, terrain.Seed, 3);
            int raised = 0;
            for (int v = -5; v <= 5; v++)
            for (int u = -4; u <= 4; u++)
            {
                float across = GeologyProfiles.Edge((4.6f - Math.Abs(u - (float)Math.Sin(v * .6))) / 3.2f);
                float along = GeologyProfiles.Edge((5.6f - Math.Abs(v)) / 3f);
                int lift = (int)Math.Round(crest * across * along + .35f);
                if (lift == 0) continue;
                var column = new Point2(center.X + u, center.Y + v);
                if (!terrain.HasOpenGround(column, level, 3) || terrain.SurfaceBlock(column) != SandBlock.Index) return false;
                int surface = terrain.Surface(column);
                int top = surface + lift;
                int sand = Terrain.MakeBlockValue(SandBlock.Index);
                for (int y = surface + 1; y <= top; y++)
                    if (!plan.Set(column, y, sand, Open)) return false;
                raised++;
            }
            return raised >= 45;
        }
    }
}

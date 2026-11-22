using System;
using Engine;
using Game;

namespace SCIEPort.Features.Geology
{
    internal static class CloudfallProfiles
    {
        internal const int Radius = 48;
        internal const int CoreRadius = 6;
        internal const int HalfFallsWidth = 2;
        private static readonly Point2[] Directions = { new Point2(1, 0), new Point2(0, 1), new Point2(-1, 0), new Point2(0, -1) };
        private static readonly Point2[] Pillars =
        {
            new Point2(-33, -10), new Point2(-25, -25), new Point2(-9, -27), new Point2(11, -29),
            new Point2(27, -23), new Point2(35, -4), new Point2(-31, 15), new Point2(31, 16)
        };

        internal static GeologySite FindSite(ITerrainContentsGenerator generator, int rx, int rz, int seed)
        {
            int sea = generator.OceanLevel;
            int start = GeologySites.Grain(rx, rz, seed ^ 0x251ae, 36);
            for (int attempt = 0; attempt < 12; attempt++)
            {
                int slot = (start + 13 * attempt) % 36;
                int x = rx * GeologySites.RegionWidth + 56 + 16 * (slot % 6);
                int z = rz * GeologySites.RegionWidth + 56 + 16 * (slot / 6);
                if (x < -3999952 || x > 3999952 || z < -3999952 || z > 3999952) continue;
                if (generator.CalculateOceanShoreDistance(x, z) < 16f || generator.CalculateHumidity(x, z) < 3) continue;
                int center = Height(generator, x, z);
                if (center < sea + 2 || center > 176 || SubsystemWeather.IsPlaceFrozen(generator.CalculateTemperature(x, z), center))
                    continue;
                if (!SupportsCore(generator, x, z, center, sea)) continue;
                if (TryChooseSlope(generator, x, z, center, sea, seed, out Point2 facing, out int pool, out int spring))
                    return new GeologySite(GeologyKind.CloudfallPeaks, x, z, pool + 2, Radius, seed, sea, facing)
                    {
                        CloudfallSpringY = spring
                    };
            }
            return null;
        }

        private static bool SupportsCore(ITerrainContentsGenerator generator, int x, int z, int center, int sea)
        {
            for (int dz = -6; dz <= 6; dz += 3)
            for (int dx = -6; dx <= 6; dx += 3)
            {
                int height = Height(generator, x + dx, z + dz);
                if (height < sea + 1 || Math.Abs(height - center) > 13) return false;
            }
            return true;
        }

        private static bool TryChooseSlope(ITerrainContentsGenerator generator, int x, int z, int center, int sea, int seed,
            out Point2 facing, out int pool, out int spring)
        {
            facing = default;
            pool = spring = 0;
            int best = int.MaxValue;
            int start = GeologySites.Grain(x, z, seed, 4);
            for (int i = 0; i < Directions.Length; i++)
            {
                Point2 direction = Directions[(i + start) & 3];
                int lower = Sample(generator, x, z, direction, 0, 3);
                int back = Sample(generator, x, z, direction, 0, -5);
                int shoulder = Math.Min(back, Math.Min(Sample(generator, x, z, direction, -2, -4),
                    Sample(generator, x, z, direction, 2, -4)));
                int level = Math.Max(sea + 3, Math.Min(center, lower) - 2);
                int head = Math.Max(level + 5, Math.Min(level + 14, shoulder));
                // Use a real uphill shoulder. Dig a shallow basin and shape the lip locally.
                if (back <= lower || head - shoulder > 3 || lower < sea + 1 || back - level > 19) continue;
                int entry = Sample(generator, x, z, direction, PathCenter(34), 34);
                int score = Math.Abs(entry - (level + 9)) + Math.Max(0, head - shoulder);
                if (entry <= sea || score >= best || score > 18 ||
                    !SupportsApproach(generator, x, z, direction, sea) || !WaterRemainsThawed(generator, x, z, direction, level, head))
                    continue;
                best = score;
                facing = direction;
                pool = level;
                spring = head;
            }
            return best < int.MaxValue;
        }

        private static bool SupportsApproach(ITerrainContentsGenerator generator, int x, int z, Point2 direction, int sea)
        {
            for (int v = 7; v <= 35; v++)
            for (int u = -1; u <= 1; u++)
                if (Sample(generator, x, z, direction, PathCenter(v) + u, v) <= sea) return false;
            return true;
        }

        private static bool WaterRemainsThawed(ITerrainContentsGenerator generator, int x, int z, Point2 direction, int pool, int spring)
        {
            for (int v = -1; v <= 4; v++)
            for (int u = -5; u <= 5; u++)
                if (InPool(u, v) && FrozenAt(generator, x, z, direction, u, v, pool)) return false;
            for (int u = -HalfFallsWidth; u <= HalfFallsWidth; u++)
            for (int v = -2; v <= -1; v++)
                if (FrozenAt(generator, x, z, direction, u, v, spring)) return false;
            return true;
        }

        private static bool FrozenAt(ITerrainContentsGenerator generator, int x, int z, Point2 direction, int u, int v, int y) =>
            SubsystemWeather.IsPlaceFrozen(generator.CalculateTemperature(x + direction.X * v - direction.Y * u,
                z + direction.Y * v + direction.X * u), y);

        private static int Sample(ITerrainContentsGenerator generator, int x, int z, Point2 direction, int u, int v) =>
            Height(generator, x + direction.X * v - direction.Y * u, z + direction.Y * v + direction.X * u);

        internal static bool InCore(int x, int z) => Math.Abs(x) <= CoreRadius && Math.Abs(z) <= CoreRadius;

        internal static int PathCenter(int v) => (int)Math.Round(2.7 * Math.Sin((v - 5) * .19));

        internal static int PathHeight(int pool, int v) => pool + 1 + Math.Min((v - 5) / 2, 8);

        internal static bool IsTrail(int u, int v) => v >= 5 && v <= 30 &&
            Math.Abs(u - PathCenter(v)) <= (v >= 24 && v <= 29 ? 3 : 1);

        internal static bool InPool(int u, int v)
        {
            switch (v)
            {
                case -1: return Math.Abs(u) <= 3;
                case 0: return u >= -4 && u <= 4;
                case 1: return u >= -5 && u <= 4;
                case 2: return Math.Abs(u) <= 5;
                case 3: return u >= -4 && u <= 3;
                case 4: return u >= -2 && u <= 2;
                default: return false;
            }
        }

        internal static GeologyColumn At(GeologySite site, int dx, int dz)
        {
            int u = dz * site.Facing.X - dx * site.Facing.Y;
            int v = dx * site.Facing.X + dz * site.Facing.Y;
            if (InCore(u, v))
            {
                GeologyColumn core = Core(site, u, v);
                if (core.Weight > 0) return core;
            }

            int rock = PeakStone();
            int pathDistance = Math.Abs(u - PathCenter(v));
            if (v >= 5 && v <= 35 && pathDistance <= (v >= 24 && v <= 29 ? 6 : 4))
            {
                bool floor = pathDistance <= (v >= 24 && v <= 29 ? 3 : 1);
                float weight = floor ? 1f : GeologyProfiles.Edge((5 - pathDistance) / 3f);
                if (v > 30) weight *= GeologyProfiles.Edge((36 - v) / 6f);
                int grade = PathHeight(site.Base - 2, v);
                return new GeologyColumn(weight, floor ? grade : grade + 1, rock, rock);
            }

            float edge = GeologyProfiles.Edge(Math.Min(48 - Math.Abs(u), 40 - Math.Abs(v)) / 7f);
            if (edge == 0) return default;
            int rise = 0;
            for (int i = 0; i < Pillars.Length; i++)
            {
                Point2 p = Pillars[i];
                int variation = GeologySites.Grain(p.X, p.Y, site.Seed, 11);
                int width = 5 + variation % 4;
                float distance = Math.Abs(u - p.X) * .85f + Math.Abs(v - p.Y) * .9f;
                if (distance >= width + 4) continue;
                float shoulder = GeologyProfiles.Edge((width + 4 - distance) / (width + 1));
                rise = Math.Max(rise, (int)Math.Round((15 + variation + (i % 3) * 4) * shoulder));
            }
            if (rise <= 2) return default;
            bool moss = rise > 14 && GeologySites.Grain(u / 2, v / 2, site.Seed, 8) < 4;
            return new GeologyColumn(edge, rise, moss ? MossStone() : rock, rock) { RelativeSurface = true };
        }

        private static GeologyColumn Core(GeologySite site, int u, int v)
        {
            int rock = PeakStone();
            int pool = site.Base - 2, head = site.CloudfallSpringY;
            int side = Math.Abs(u);
            if (InPool(u, v))
            {
                int depth = v <= 2 && side <= 2 ? 3 : 2;
                var basin = new GeologyColumn(1f, pool - depth, rock, rock);
                int waterTop = v == -1 && side <= HalfFallsWidth ? head : pool;
                basin.Flood(Terrain.MakeBlockValue(WaterBlock.Index), waterTop, pool);
                return basin;
            }
            if (v == -2 && side <= HalfFallsWidth)
            {
                var source = new GeologyColumn(1f, head - 1, rock, rock);
                source.Flood(Terrain.MakeBlockValue(WaterBlock.Index), head);
                return source;
            }
            if (v == -2 && side == HalfFallsWidth + 1 || v == -3 && side <= HalfFallsWidth)
                return new GeologyColumn(1f, head + 1, side > 1 ? MossStone() : rock, rock);
            if (v >= -5 && v <= -3 && side <= 4)
            {
                float shoulder = GeologyProfiles.Edge((5 - side) / 3f) * GeologyProfiles.Edge((v + 6) / 3f);
                return new GeologyColumn(shoulder, head + (v <= -4 ? 1 : -1), MossStone(), rock);
            }
            if (InPool(u - 1, v) || InPool(u + 1, v) || InPool(u, v - 1) || InPool(u, v + 1))
                return new GeologyColumn(1f, pool + 1 + (v < 4 && side > 3 ? 1 : 0), rock, rock);
            return default;
        }

        private static int Height(ITerrainContentsGenerator generator, int x, int z) => (int)Math.Round(generator.CalculateHeight(x, z));
        private static int PeakStone() => BlocksManager.Blocks[PeakSandstoneBlock.Index] is PeakSandstoneBlock
            ? PeakSandstoneBlock.Index : SandstoneBlock.Index;
        private static int MossStone() => BlocksManager.Blocks[MossyPeakRockBlock.Index] is MossyPeakRockBlock
            ? MossyPeakRockBlock.Index : GrassBlock.Index;
    }
}

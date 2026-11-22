using System;
using Engine;
using Game;

namespace SCIEPort.Features.Geology
{
    internal static class CloudfallProfiles
    {
        internal const int Radius = 48;
        internal const int CoreRadius = 6;
        private static readonly int[] FallHeights = { 14, 22, 30 };
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
                int basin = Math.Max(sea + 5, center);
                if (center < sea + 2 || basin > 176 || SubsystemWeather.IsPlaceFrozen(generator.CalculateTemperature(x, z), basin + 28))
                    continue;
                if (!SupportsCore(generator, x, z, basin, sea)) continue;
                Point2 facing = ChooseApproach(generator, x, z, basin, sea, seed);
                if (facing.X == 0 && facing.Y == 0) continue;
                return new GeologySite(GeologyKind.CloudfallPeaks, x, z, basin, Radius, seed, sea, facing);
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

        private static Point2 ChooseApproach(ITerrainContentsGenerator generator, int x, int z, int center, int sea, int seed)
        {
            int best = int.MaxValue;
            Point2 facing = default;
            int start = GeologySites.Grain(x, z, seed, 4);
            for (int i = 0; i < Directions.Length; i++)
            {
                Point2 direction = Directions[(i + start) & 3];
                int entry = Height(generator, x + direction.X * 38, z + direction.Y * 38);
                int ridge = Height(generator, x - direction.X * 26, z - direction.Y * 26);
                if (entry < sea + 1 || ridge < sea - 1 || Math.Abs(ridge - center) > 30) continue;
                int score = Math.Abs(entry - (center + 8));
                if (score >= best || score > 18 || !SupportsApproach(generator, x, z, direction, sea) ||
                    !WaterRemainsThawed(generator, x, z, direction, center - 2)) continue;
                best = score;
                facing = direction;
            }
            return facing;
        }

        private static bool SupportsApproach(ITerrainContentsGenerator generator, int x, int z, Point2 direction, int sea)
        {
            for (int v = 7; v <= 35; v++)
            for (int u = -1; u <= 1; u++)
                if (Height(generator, x + direction.X * v - direction.Y * u, z + direction.Y * v + direction.X * u) <= sea)
                    return false;
            return true;
        }

        private static bool WaterRemainsThawed(ITerrainContentsGenerator generator, int x, int z, Point2 direction, int pool)
        {
            for (int v = -1; v <= 4; v++)
            for (int u = -5; u <= 5; u++)
                if (FrozenAt(generator, x, z, direction, u, v, pool)) return false;

            for (int i = 0; i < FallHeights.Length; i++)
            for (int v = -2; v <= -1; v++)
                if (FrozenAt(generator, x, z, direction, -4 + i * 4, v, pool + FallHeights[i])) return false;
            return true;
        }

        private static bool FrozenAt(ITerrainContentsGenerator generator, int x, int z, Point2 direction, int u, int v, int y) =>
            SubsystemWeather.IsPlaceFrozen(generator.CalculateTemperature(x + direction.X * v - direction.Y * u,
                z + direction.Y * v + direction.X * u), y);

        internal static bool InCore(int x, int z) => Math.Abs(x) <= CoreRadius && Math.Abs(z) <= CoreRadius;

        internal static bool IsTrail(int u, int v) =>
            v >= 7 && v <= 35 && Math.Abs(u) <= (v >= 24 && v <= 30 ? 4 : 1);

        internal static GeologyColumn At(GeologySite site, int dx, int dz)
        {
            int u = dz * site.Facing.X - dx * site.Facing.Y;
            int v = dx * site.Facing.X + dz * site.Facing.Y;
            if (InCore(u, v)) return Core(site, u, v);
            float edge = GeologyProfiles.Edge(Math.Min(48 - Math.Abs(u), 40 - Math.Abs(v)) / 7f);
            if (edge == 0) return default;

            if (IsTrail(u, v))
            {
                int grade = site.Base - 1 + Math.Min((v - 6) / 2, 9);
                return new GeologyColumn(v <= 30 ? 1f : GeologyProfiles.Edge((36 - v) / 6f), grade, PeakStone(), PeakStone());
            }

            int floor = site.Base - 1 + (int)Math.Round(2 * Math.Sin(u * .18) + 2 * Math.Cos(v * .16));
            int high = floor;
            for (int i = 0; i < Pillars.Length; i++)
            {
                Point2 p = Pillars[i];
                int variation = GeologySites.Grain(p.X, p.Y, site.Seed, 11);
                int width = 6 + variation % 3;
                float distance = Math.Abs(u - p.X) * .85f + Math.Abs(v - p.Y) * .9f;
                if (distance >= width + 6) continue;
                float shoulder = GeologyProfiles.Edge((width + 6 - distance) / (width + 3));
                int crown = site.Base + 27 + variation + (i % 3) * 4;
                high = Math.Max(high, GeologyProfiles.Mix(floor, crown, shoulder));
            }
            if (v >= -14 && v < -6 && Math.Abs(u) <= 8)
                high = Math.Max(high, GeologyProfiles.Mix(floor, site.Base + 21, GeologyProfiles.Edge((v + 14) / 7f)));
            bool peak = high > floor + 2;
            bool moss = peak && (high > site.Base + 26 || GeologySites.Grain(u / 2, v / 2, site.Seed, 9) < 2);
            return new GeologyColumn(edge, high, moss ? MossStone() : peak ? PeakStone() : GrassBlock.Index, PeakStone());
        }

        private static GeologyColumn Core(GeologySite site, int u, int v)
        {
            int rock = PeakStone();
            int pool = site.Base - 2;
            int fall = Math.Min(2, (u + 6) / 4);
            int source = pool + FallHeights[fall];
            bool onAxis = u == -4 + fall * 4;
            if (v >= -1 && v <= 4 && Math.Abs(u) <= 5)
            {
                var basin = new GeologyColumn(1f, pool - 3, rock, rock);
                basin.Flood(Terrain.MakeBlockValue(WaterBlock.Index), onAxis && v == -1 ? source : pool, pool);
                return basin;
            }
            if (v <= -2)
            {
                if (v == -2 && onAxis)
                {
                    var spring = new GeologyColumn(1f, source - 1, rock, rock);
                    spring.Flood(Terrain.MakeBlockValue(WaterBlock.Index), source);
                    return spring;
                }
                int height = source + 2 + (v < -3 ? 2 : 0);
                return new GeologyColumn(1f, height, v <= -4 ? MossStone() : rock, rock);
            }
            return new GeologyColumn(1f, pool + 1, rock, rock);
        }

        private static int Height(ITerrainContentsGenerator generator, int x, int z) => (int)Math.Round(generator.CalculateHeight(x, z));
        private static int PeakStone() => BlocksManager.Blocks[PeakSandstoneBlock.Index] is PeakSandstoneBlock
            ? PeakSandstoneBlock.Index : SandstoneBlock.Index;
        private static int MossStone() => BlocksManager.Blocks[MossyPeakRockBlock.Index] is MossyPeakRockBlock
            ? MossyPeakRockBlock.Index : GrassBlock.Index;
    }
}

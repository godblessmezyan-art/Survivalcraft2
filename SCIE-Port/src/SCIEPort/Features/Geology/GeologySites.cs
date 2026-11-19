using System;
using System.Collections.Generic;
using Engine;
using Game;
using SCIEPort.Features.KellyPlants;

namespace SCIEPort.Features.Geology
{
    internal enum GeologyKind { Crater, MagmaCrater, CraterLakes, Terraces, PaintedHills, BasaltCoast, Pinnacles, RiverCave, IceCave, SaltFlat }

    internal sealed class GeologySite
    {
        internal readonly GeologyKind Kind;
        internal readonly int X, Z, Base, Radius, Seed, Sea;
        internal readonly Point2 Facing;

        internal GeologySite(GeologyKind kind, int x, int z, int height, int radius, int seed, int sea, Point2 facing)
        {
            Kind = kind;
            X = x;
            Z = z;
            Base = height;
            Radius = radius;
            Seed = seed;
            Sea = sea;
            Facing = facing;
        }

        internal bool Touches(TerrainChunk chunk)
            => X + Radius >= chunk.Origin.X && X - Radius <= chunk.Origin.X + 15 &&
               Z + Radius >= chunk.Origin.Y && Z - Radius <= chunk.Origin.Y + 15;
    }

    internal static class GeologySites
    {
        private const int RegionWidth = 192;
        private static readonly Point2[] Directions = { new Point2(1, 0), new Point2(0, 1), new Point2(-1, 0), new Point2(0, -1) };

        internal static bool Enabled(TerrainGenerationMode mode) => mode == TerrainGenerationMode.Continent ||
            mode == TerrainGenerationMode.Island || mode == TerrainGenerationMode.Better;

        internal static GeologySite Choose(ITerrainContentsGenerator generator, int worldSeed, Point2 origin)
        {
            int rx = FloorDiv(origin.X, RegionWidth), rz = FloorDiv(origin.Y, RegionWidth);
            int seed = KellyPlantRules.GetChunkSeed(worldSeed ^ 0x625b7d3, rx, rz);
            var random = new Game.Random(seed);
            if (!random.Bool(.83f)) return null;
            int sea = generator.OceanLevel;
            // Chunk-aligned centers leave room for bounded single-chunk lava/lake basins. The
            // buffer at the region edge keeps every proposal within its seed's ownership region.
            for (int attempt = 0; attempt < 4; attempt++)
            {
                int x = rx * RegionWidth + random.Int(3, 8) * 16 + 8;
                int z = rz * RegionWidth + random.Int(3, 8) * 16 + 8;
                int variantSeed = random.Int(1, 1000000000);
                int height = (int)Math.Round(generator.CalculateHeight(x, z));
                float shore = generator.CalculateOceanShoreDistance(x, z);
                int heat = generator.CalculateTemperature(x, z) + SubsystemWeather.GetTemperatureAdjustmentAtHeight(height);
                int humidity = generator.CalculateHumidity(x, z);

                if (shore >= -18f && shore <= 8f && Math.Abs(height - sea) <= 6 && heat >= 2)
                    return new GeologySite(GeologyKind.BasaltCoast, x, z, sea, 22, variantSeed, sea, FindLandward(generator, x, z));
                if (shore < 26f || height < sea - 3 || height > 191) continue;
                Point2 facing = Directions[random.Int(0, 3)];
                if (!FindRelief(generator, x, z, height, out int low, out int relief)) continue;

                var eligible = new List<GeologyKind>(7);
                bool gentle = relief <= 17;
                if (heat <= 2 && height >= sea + 14 && relief <= 29) eligible.Add(GeologyKind.IceCave);
                if (heat >= 3)
                {
                    if (shore >= 46 && relief <= 24 && height >= sea + 2)
                        eligible.Add(ChooseVolcano(random));
                    if (humidity >= 6 && gentle && height <= sea + 24) eligible.Add(GeologyKind.Terraces);
                    if (humidity <= 10 && relief <= 29) eligible.Add(GeologyKind.PaintedHills);
                    if (humidity <= 11 && gentle) eligible.Add(GeologyKind.Pinnacles);
                    if (humidity >= 5 && relief <= 26) eligible.Add(GeologyKind.RiverCave);
                    if (humidity <= 6 && heat >= 5 && relief <= 11 && height <= sea + 47) eligible.Add(GeologyKind.SaltFlat);
                }
                if (eligible.Count == 0) continue;
                GeologyKind kind = eligible[random.Int(0, eligible.Count - 1)];
                int baseline = kind == GeologyKind.RiverCave || kind == GeologyKind.Terraces ? (height + low) / 2 : height;
                baseline = Math.Max(sea + 1, baseline);
                if (kind == GeologyKind.Crater || kind == GeologyKind.MagmaCrater) baseline = Math.Max(sea + 6, baseline);
                return new GeologySite(kind, x, z, baseline, Radius(kind), variantSeed, sea, facing);
            }
            return null;
        }

        private static GeologyKind ChooseVolcano(Game.Random random)
        {
            float type = random.Float(0, 1);
            return type < .12f ? GeologyKind.CraterLakes : type < .40f ? GeologyKind.MagmaCrater : GeologyKind.Crater;
        }

        private static bool FindRelief(ITerrainContentsGenerator generator, int x, int z, int center, out int low, out int relief)
        {
            low = center;
            int high = center;
            foreach (Point2 direction in Directions)
            {
                int y = (int)Math.Round(generator.CalculateHeight(x + 21 * direction.X, z + 21 * direction.Y));
                low = Math.Min(low, y);
                high = Math.Max(high, y);
            }
            relief = high - low;
            return relief <= 29;
        }

        private static Point2 FindLandward(ITerrainContentsGenerator generator, int x, int z)
        {
            Point2 best = Directions[0];
            float highest = float.NegativeInfinity;
            foreach (Point2 direction in Directions)
            {
                float distance = generator.CalculateOceanShoreDistance(x + 10 * direction.X, z + 10 * direction.Y);
                if (distance <= highest) continue;
                highest = distance;
                best = direction;
            }
            return best;
        }

        private static int Radius(GeologyKind kind)
        {
            switch (kind)
            {
                case GeologyKind.CraterLakes: return 43;
                case GeologyKind.Crater:
                case GeologyKind.MagmaCrater: return 34;
                case GeologyKind.Terraces: return 23;
                case GeologyKind.RiverCave: return 30;
                case GeologyKind.IceCave: return 24;
                case GeologyKind.Pinnacles: return 27;
                default: return 33;
            }
        }

        internal static int FloorDiv(int value, int divisor)
        {
            int quotient = value / divisor;
            return value % divisor < 0 ? quotient - 1 : quotient;
        }

        internal static int Grain(int x, int z, int seed, int range)
        {
            unchecked
            {
                uint hash = (uint)(x * 374761393 + z * 668265263 + seed);
                hash = (hash ^ (hash >> 13)) * 1274126177;
                return (int)((hash ^ (hash >> 16)) % (uint)range);
            }
        }
    }
}

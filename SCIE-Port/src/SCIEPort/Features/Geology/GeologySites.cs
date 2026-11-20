using System;
using System.Collections.Generic;
using Engine;
using Game;
using SCIEPort.Features.KellyPlants;

namespace SCIEPort.Features.Geology
{
    internal enum GeologyKind
    {
        Crater, MagmaCrater, CraterLakes, Terraces, PaintedHills, BasaltCoast, Pinnacles, RiverCave, IceCave, SaltFlat,
        ColumnarBasalt, DryCanyon, GraniteDomes, ErodedBadlands, GlacialCirque
    }

    internal sealed class GeologySite
    {
        internal readonly GeologyKind Kind;
        internal readonly int X, Z, Base, Radius, Seed, Sea, Outlet;
        internal readonly Point2 Facing;
        internal int Extent => Radius + (GeologySites.IsVolcano(Kind) ? GeologyProfiles.VolcanoApron : 0);

        internal GeologySite(GeologyKind kind, int x, int z, int height, int radius, int seed, int sea, Point2 facing, int outlet = 0)
        {
            Kind = kind;
            X = x;
            Z = z;
            Base = height;
            Radius = radius;
            Seed = seed;
            Sea = sea;
            Facing = facing;
            Outlet = outlet;
        }

        internal bool Touches(TerrainChunk chunk)
            => X + Extent >= chunk.Origin.X && X - Extent <= chunk.Origin.X + 15 &&
               Z + Extent >= chunk.Origin.Y && Z - Extent <= chunk.Origin.Y + 15;
    }

    internal static class GeologySites
    {
        internal const int RegionWidth = 192;
        private const int VolcanoAttempts = 12;
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
                if (IsVolcano(kind)) return FindVolcano(generator, kind, rx, rz, x, z, variantSeed);
                int baseline = kind == GeologyKind.RiverCave || kind == GeologyKind.Terraces ? (height + low) / 2 : height;
                baseline = Math.Max(sea + 1, baseline);
                int radius = Radius(kind);
                if (kind == GeologyKind.PaintedHills || kind == GeologyKind.Pinnacles)
                {
                    // Reuse the original site reservation. Volcano searches and pyramid
                    // exclusion still see the same positions and extents for this seed.
                    int roll = Grain(x, z, variantSeed ^ 0x15a2cb, 100);
                    bool basalt = low >= sea + 2 && height >= sea + 6 && relief <= 17;
                    bool canyon = low >= sea + 2 && height >= sea + 6 && heat >= 4 && humidity <= 9 && relief <= 21;
                    if (roll < 65)
                    {
                        if (basalt && (!canyon || (roll & 1) == 0)) kind = GeologyKind.ColumnarBasalt;
                        else if (canyon) kind = GeologyKind.DryCanyon;
                    }
                }
                int surfaceRoll = Grain(x, z, variantSeed ^ 0x6b912e, 100);
                if (kind == GeologyKind.IceCave && low >= sea + 6 && height >= sea + 18 && surfaceRoll < 38)
                    kind = GeologyKind.GlacialCirque;
                else if ((kind == GeologyKind.DryCanyon || kind == GeologyKind.PaintedHills) &&
                    low >= sea + 2 && height >= sea + 6 && shore >= 38 && heat >= 4 && humidity <= 9 && surfaceRoll < 38)
                    kind = GeologyKind.ErodedBadlands;
                else if (humidity >= 6 && low >= sea + 2 && height >= sea + 6 &&
                    (kind == GeologyKind.ColumnarBasalt && surfaceRoll < 48 || kind == GeologyKind.Terraces && surfaceRoll < 20))
                    kind = GeologyKind.GraniteDomes;
                return new GeologySite(kind, x, z, baseline, radius, variantSeed, sea, facing);
            }
            return null;
        }

        private static GeologyKind ChooseVolcano(Game.Random random)
        {
            float type = random.Float(0, 1);
            return type < .12f ? GeologyKind.CraterLakes : type < .62f ? GeologyKind.MagmaCrater : GeologyKind.Crater;
        }

        internal static bool IsVolcano(GeologyKind kind) =>
            kind == GeologyKind.Crater || kind == GeologyKind.MagmaCrater || kind == GeologyKind.CraterLakes;

        private static GeologySite FindVolcano(ITerrainContentsGenerator generator, GeologyKind kind,
            int rx, int rz, int preferredX, int preferredZ, int seed)
        {
            int sea = generator.OceanLevel, radius = Radius(kind);
            int startX = rx * RegionWidth + 56, startZ = rz * RegionWidth + 56;
            int slot = (preferredX - startX) / 16 + 6 * ((preferredZ - startZ) / 16);
            // Keep volcano reservations inside the original region. Visit distinct chunk centers
            // to search nearby uplands without moving any other geology site's selection.
            for (int attempt = 0; attempt < VolcanoAttempts; attempt++, slot = (slot + 13) % 36)
            {
                int x = startX + 16 * (slot % 6), z = startZ + 16 * (slot / 6);
                if (generator.CalculateOceanShoreDistance(x, z) < radius + 12) continue;
                int height = SampleHeight(generator, x, z);
                if (height < sea + 14 || height > 191 ||
                    !FindRelief(generator, x, z, height, out int low, out int relief) || low < sea + 8 || relief > 27)
                    continue;

                int baseline = Math.Max(height, sea + 25);
                int floor = baseline + (kind == GeologyKind.CraterLakes ? 4 : -4);
                if (TryFindOutlet(generator, kind, x, z, radius, floor, seed, out Point2 facing, out int outlet))
                    return new GeologySite(kind, x, z, baseline, radius, seed, sea, facing, outlet);
            }
            return null;
        }

        private static bool TryFindOutlet(ITerrainContentsGenerator generator, GeologyKind kind,
            int x, int z, int radius, int floor, int seed, out Point2 facing, out int outlet)
        {
            facing = default;
            outlet = 0;
            int bestScore = int.MaxValue;
            int first = Grain(x, z, seed, Directions.Length);
            for (int index = 0; index < Directions.Length; index++)
            {
                Point2 direction = Directions[(first + index) % Directions.Length];
                int edge = SampleHeight(generator, x - (radius - 1) * direction.X, z - (radius - 1) * direction.Y);
                int outside = SampleHeight(generator, x - (radius + 4) * direction.X, z - (radius + 4) * direction.Y);
                int apron = SampleHeight(generator, x - (radius - 7) * direction.X, z - (radius - 7) * direction.Y);
                int approach = Math.Abs(outside - edge);
                int grade = Math.Abs(edge - floor);
                if (outside < generator.OceanLevel + 5 || approach > 5 || Math.Abs(edge - apron) > 8 ||
                    grade > radius - 17 || (kind == GeologyKind.MagmaCrater && edge < floor - 4))
                    continue;
                int score = approach * 4 + grade + Math.Abs(edge - apron) * 2;
                if (score >= bestScore) continue;
                bestScore = score;
                facing = direction;
                outlet = edge;
            }
            return bestScore < int.MaxValue;
        }

        private static int SampleHeight(ITerrainContentsGenerator generator, int x, int z) =>
            (int)Math.Round(generator.CalculateHeight(x, z));

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

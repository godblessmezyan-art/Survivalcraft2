using System;
using Engine;
using Game;
using SCIEPort.Features.Geology;
using SCIEPort.Features.KellyPlants;

namespace SCIEPort.Features.TidePalace
{
    internal sealed class PalaceSite
    {
        internal const int RegionSize = 384;
        internal const int Width = 64;
        internal const int Length = 48;
        internal const int Complete = (1 << 12) - 1;
        internal const int CoordinateLimit = 4000000;
        internal static readonly Point3 West = new Point3(11, -5, 25);
        internal static readonly Point3 Display = new Point3(29, -4, 43);
        internal static readonly Point3 EastLight = new Point3(32, 7, 30);
        internal static readonly Point3 WestLight = new Point3(26, 7, 30);
        internal static readonly Point3[] Dials =
        {
            new Point3(42, 3, 24), new Point3(47, 3, 24), new Point3(52, 3, 24)
        };

        internal readonly Point2 Origin;
        internal readonly int BaseY;
        internal readonly int Solution;

        internal PalaceSite(Point2 origin, int baseY, int solution)
        {
            Origin = origin;
            BaseY = baseY;
            Solution = solution;
        }

        internal Point3 World(Point3 local) => new Point3(Origin.X + local.X, BaseY + local.Y, Origin.Y + local.Z);
        internal static Point2 Region(Point2 position) => new Point2(
            GeologySites.FloorDiv(position.X, RegionSize), GeologySites.FloorDiv(position.Y, RegionSize));
        internal static bool InBounds(int x, int z) => x >= -CoordinateLimit && x <= CoordinateLimit &&
            z >= -CoordinateLimit && z <= CoordinateLimit;
        internal int ChunkBit(Point2 origin)
        {
            int x = origin.X - Origin.X, z = origin.Y - Origin.Y;
            return x >= 0 && x < Width && z >= 0 && z < Length ? 1 << ((z / 16) * 4 + x / 16) : 0;
        }
        internal static int Digit(int value, int dial) => value / Place(dial) % 3;
        internal static int Advance(int value, int dial) => value + (Digit(value, dial) == 2 ? -2 : 1) * Place(dial);
        private static int Place(int dial) => dial == 0 ? 1 : dial == 1 ? 3 : 9;

        internal static PalaceSite Choose(ITerrainContentsGenerator generator, int worldSeed, Point2 region)
        {
            var random = new Game.Random(KellyPlantRules.GetChunkSeed(worldSeed ^ 0x1bbc7e, region.X, region.Y));
            if (!random.Bool(.36f)) return null;
            int sea = generator.OceanLevel;
            for (int attempt = 0; attempt < 4; attempt++)
            {
                int x = region.X * RegionSize + random.Int(3, 17) * 16;
                int z = region.Y * RegionSize + random.Int(3, 17) * 16;
                if (!InBounds(x, z) || !InBounds(x + Width - 1, z + Length - 1)) continue;
                if (TryDeck(generator, sea, x, z, out int deck))
                    return new PalaceSite(new Point2(x, z), deck, random.Int(1, 26));
            }
            return null;
        }

        private static bool TryDeck(ITerrainContentsGenerator generator, int sea, int x, int z, out int deck)
        {
            deck = 0;
            int low = int.MaxValue, high = int.MinValue;
            for (int dz = -4; dz <= Length + 4; dz += 8)
            for (int dx = -4; dx <= Width + 4; dx += 8)
            {
                if (generator.CalculateOceanShoreDistance(x + dx, z + dz) > -48f) return false;
                int floor = (int)Math.Ceiling(generator.CalculateHeight(x + dx, z + dz));
                low = Math.Min(low, floor);
                high = Math.Max(high, floor);
            }
            deck = Math.Max(high + 2, sea - 34);
            return high - low <= 10 && deck - low <= 20 && deck >= 24 && deck + 18 <= sea - 6 && sea <= 248;
        }
    }
}

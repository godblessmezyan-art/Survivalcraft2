using System;
using Engine;
using Game;
using SCIEPort.Features.Geology;
using SCIEPort.Features.KellyPlants;

namespace SCIEPort.Features.Pyramid
{
    internal sealed class PyramidSite
    {
        internal const int Side = 80;
        internal const int ChunkSide = Side / 16;
        internal const int Complete = (1 << (ChunkSide * ChunkSide)) - 1;
        internal const int RegionSize = 384;
        private const int CoordinateLimit = 4000000;
        internal const int GateZ = 45;
        internal static readonly Point3 Display = new Point3(40, -9, 65);
        internal static readonly Point3[] Seals =
        {
            new Point3(11, 2, 25), new Point3(67, 2, 37), new Point3(39, 19, 25)
        };

        private static readonly int[,] Orders = { { 0, 1, 2 }, { 0, 2, 1 }, { 1, 0, 2 }, { 1, 2, 0 }, { 2, 0, 1 }, { 2, 1, 0 } };
        internal readonly Point2 Origin;
        internal readonly int BaseY, Order, RewardId, ApproachFloor;
        internal readonly PyramidMaze Maze;
        internal readonly bool Tomb, Pointed;

        internal PyramidSite(Point2 origin, int baseY, int order, int rewardId)
        {
            Origin = origin;
            BaseY = baseY;
            Order = order;
            RewardId = rewardId;
        }

        internal PyramidSite(Point2 origin, int baseY, int order, int rewardId, int mazeSeed, int approachFloor, bool tomb)
            : this(origin, baseY, order, rewardId, mazeSeed, approachFloor, tomb, false)
        {
        }

        internal PyramidSite(Point2 origin, int baseY, int order, int rewardId, int mazeSeed, int approachFloor, bool tomb, bool pointed)
            : this(origin, baseY, order, rewardId)
        {
            Maze = new PyramidMaze(mazeSeed);
            ApproachFloor = Math.Max(tomb ? -9 : -10, Math.Min(0, approachFloor));
            Tomb = tomb;
            Pointed = tomb && pointed;
        }

        internal Point3 World(Point3 local) => new Point3(Origin.X + local.X, BaseY + local.Y, Origin.Y + local.Z);
        internal int SealAt(int step) => Orders[Order, step];
        internal static bool InBounds(int x, int z) => x >= -CoordinateLimit && x <= CoordinateLimit &&
            z >= -CoordinateLimit && z <= CoordinateLimit;
        internal static Point2 Region(Point2 point) => new Point2(
            GeologySites.FloorDiv(point.X, RegionSize), GeologySites.FloorDiv(point.Y, RegionSize));

        internal static bool ValidOrigin(Point2 origin)
        {
            var end = new Point2(origin.X + Side - 1, origin.Y + Side - 1);
            return InBounds(origin.X, origin.Y) && InBounds(end.X, end.Y) &&
                (origin.X & 15) == 0 && (origin.Y & 15) == 0 && Region(origin) == Region(end);
        }

        internal int ChunkBit(Point2 point)
        {
            int x = point.X - Origin.X, z = point.Y - Origin.Y;
            return x >= 0 && x < Side && z >= 0 && z < Side ? 1 << ((z >> 4) * ChunkSide + (x >> 4)) : 0;
        }

        internal static PyramidSite Choose(ITerrainContentsGenerator generator, int worldSeed, Point2 region)
        {
            var random = new Game.Random(KellyPlantRules.GetChunkSeed(worldSeed ^ 0x5e88b13, region.X, region.Y));
            if (!random.Bool(.62f)) return null;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                int x = region.X * RegionSize + random.Int(2, 17) * 16;
                int z = region.Y * RegionSize + random.Int(2, 17) * 16;
                var origin = new Point2(x, z);
                if (!ValidOrigin(origin) || !TryBase(generator, x, z, out _) ||
                    OverlapsGeology(generator, worldSeed, origin)) continue;
                int frontY = Terrain.ToCell(generator.CalculateHeight(
                    x + PyramidMaze.TombEntrance.X, z + PyramidMaze.TombEntrance.Z - 1));
                // Leave one block of clearance for the native surface's rounding near the rim.
                // The recessed door only needs a short cut into the natural sand in front.
                int baseY = frontY - 2;
                if (baseY < 32 || baseY > RimHeight(generator, origin)) continue;
                return new PyramidSite(origin, baseY, random.Int(0, 5), PyramidLoot.Draw(random),
                    KellyPlantRules.GetChunkSeed(worldSeed ^ 0x2e12ba7, x, z), 0, true, true);
            }
            return null;
        }

        private static int RimHeight(ITerrainContentsGenerator generator, Point2 origin)
        {
            // Sample the visible rim, including its corners, so a downhill face cannot
            // expose several layers of the vertical foundation.
            const int left = PyramidLayout.CenterX - PyramidLayout.BaseHalfWidth;
            const int right = PyramidLayout.CenterX + PyramidLayout.BaseHalfWidth;
            const int front = PyramidLayout.CenterZ - PyramidLayout.BaseHalfDepth;
            const int back = PyramidLayout.CenterZ + PyramidLayout.BaseHalfDepth;
            float minimum = float.MaxValue;
            for (int x = left; x <= right; x++)
                minimum = Math.Min(minimum, Math.Min(generator.CalculateHeight(origin.X + x, origin.Y + front),
                    generator.CalculateHeight(origin.X + x, origin.Y + back)));
            for (int z = front + 1; z < back; z++)
                minimum = Math.Min(minimum, Math.Min(generator.CalculateHeight(origin.X + left, origin.Y + z),
                    generator.CalculateHeight(origin.X + right, origin.Y + z)));
            return Terrain.ToCell(minimum);
        }

        private static bool TryBase(ITerrainContentsGenerator generator, int x, int z, out int baseY)
        {
            baseY = 0;
            int minimum = int.MaxValue, maximum = int.MinValue, arid = 0;
            if (generator.CalculateTemperature(x + 40, z + 40) < 8 ||
                generator.CalculateHumidity(x + 40, z + 40) > 7) return false;
            for (int dz = 0; dz < Side; dz += 13)
            for (int dx = 0; dx < Side; dx += 13)
            {
                if (generator.CalculateOceanShoreDistance(x + dx, z + dz) < 32f) return false;
                int y = (int)Math.Ceiling(generator.CalculateHeight(x + dx, z + dz));
                minimum = Math.Min(minimum, y);
                maximum = Math.Max(maximum, y);
                if (generator.CalculateHumidity(x + dx, z + dz) <= 8) arid++;
            }
            baseY = maximum;
            return arid >= 28 && minimum >= generator.OceanLevel && maximum - minimum <= 12 &&
                baseY >= 32 && baseY + PyramidLayout.HighY < 248;
        }

        private static bool OverlapsGeology(ITerrainContentsGenerator generator, int seed, Point2 origin)
        {
            const int size = GeologySites.RegionWidth;
            for (int rz = GeologySites.FloorDiv(origin.Y, size); rz <= GeologySites.FloorDiv(origin.Y + Side - 1, size); rz++)
            for (int rx = GeologySites.FloorDiv(origin.X, size); rx <= GeologySites.FloorDiv(origin.X + Side - 1, size); rx++)
            {
                GeologySite feature = GeologySites.Choose(generator, seed, new Point2(rx * size, rz * size));
                if (feature != null && feature.X + feature.Extent >= origin.X && feature.X - feature.Extent < origin.X + Side &&
                    feature.Z + feature.Extent >= origin.Y && feature.Z - feature.Extent < origin.Y + Side) return true;
            }
            return false;
        }
    }
}

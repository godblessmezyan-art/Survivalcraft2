using Engine;
using SCIEPort.Features.Geology;

namespace SCIEPort.Features.Landmarks
{
    internal enum LandmarkKind
    {
        Waterfall = 0,
        Ford,
        Blossom,
        Arch,
        Shelter,
        StoneCircle,
        Hoodoo,
        Serac,
        DuneRidge,
        SeaStacks,
        Sinkhole,
        ScreeFan,
        MeadowHummocks,
        FrostPolygons
    }

    internal static class LandmarkDeck
    {
        private static readonly int[] Weights = { 150, 150, 130, 130, 120, 110, 100, 90, 100, 100, 100, 115, 130, 110 };
        private static readonly int Total = SumWeights();
        internal const int RepeatRadius = 2;

        internal static bool Choose(int seed, Point2 chunk, out LandmarkKind? first, out LandmarkKind? second)
        {
            first = second = null;
            if (!Participates(seed, chunk)) return false;
            DrawPair(seed, chunk, out LandmarkKind primary, out LandmarkKind fallback);
            first = primary;
            second = fallback;

            // Compare raw draws so either chunk can generate first. A higher-ranked neighbor
            // reserves both of its candidates, even if its terrain later rejects them.
            int rank = Rank(seed, chunk);
            for (int dz = -RepeatRadius; dz <= RepeatRadius; dz++)
            for (int dx = -RepeatRadius; dx <= RepeatRadius; dx++)
            {
                if (dx == 0 && dz == 0) continue;
                Point2 neighbor = chunk + new Point2(dx, dz);
                if (!Participates(seed, neighbor)) continue;
                int neighborRank = Rank(seed, neighbor);
                if (neighborRank < rank || neighborRank == rank && (dz > 0 || dz == 0 && dx > 0)) continue;
                DrawPair(seed, neighbor, out LandmarkKind a, out LandmarkKind b);
                if (first == a || first == b) first = null;
                if (second == a || second == b) second = null;
                if (!first.HasValue && !second.HasValue) return false;
            }
            return first.HasValue || second.HasValue;
        }

        private static bool Participates(int seed, Point2 chunk) =>
            GeologySites.Grain(chunk.X, chunk.Y, seed ^ 0x42d5961, 100) < 62;

        private static int Rank(int seed, Point2 chunk) =>
            GeologySites.Grain(chunk.X, chunk.Y, seed ^ 0x6c0178b, int.MaxValue);

        private static int SumWeights()
        {
            int total = 0;
            foreach (int weight in Weights) total += weight;
            return total;
        }

        private static void DrawPair(int seed, Point2 chunk, out LandmarkKind first, out LandmarkKind second)
        {
            first = Draw(GeologySites.Grain(chunk.X, chunk.Y, seed ^ 0x2013ffa, Total), -1);
            second = Draw(GeologySites.Grain(chunk.X, chunk.Y, seed ^ 0x5f3a1d, Total - Weights[(int)first]), (int)first);
        }

        private static LandmarkKind Draw(int roll, int skip)
        {
            for (int kind = 0; kind < Weights.Length; kind++)
            {
                if (kind == skip) continue;
                roll -= Weights[kind];
                if (roll < 0) return (LandmarkKind)kind;
            }
            throw new System.InvalidOperationException("Invalid landmark draw.");
        }
    }
}

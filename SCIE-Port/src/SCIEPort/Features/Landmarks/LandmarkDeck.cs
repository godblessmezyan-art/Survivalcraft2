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
        Sinkhole
    }

    internal static class LandmarkDeck
    {
        private static readonly int[] Weights = { 150, 150, 130, 130, 120, 110, 100, 90, 100, 100, 100 };
        private const int Total = 1280;
        private static readonly Point2[] Neighbors =
        {
            new Point2(-1, 0), new Point2(0, -1), new Point2(1, 0), new Point2(0, 1)
        };

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
            foreach (Point2 offset in Neighbors)
            {
                Point2 neighbor = chunk + offset;
                if (!Participates(seed, neighbor)) continue;
                int neighborRank = Rank(seed, neighbor);
                if (neighborRank < rank || neighborRank == rank && offset.X + offset.Y > 0) continue;
                DrawPair(seed, neighbor, out LandmarkKind a, out LandmarkKind b);
                if (first == a || first == b) first = null;
                if (second == a || second == b) second = null;
            }
            return first.HasValue || second.HasValue;
        }

        private static bool Participates(int seed, Point2 chunk) =>
            GeologySites.Grain(chunk.X, chunk.Y, seed ^ 0x42d5961, 100) < 62;

        private static int Rank(int seed, Point2 chunk) =>
            GeologySites.Grain(chunk.X, chunk.Y, seed ^ 0x6c0178b, int.MaxValue);

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

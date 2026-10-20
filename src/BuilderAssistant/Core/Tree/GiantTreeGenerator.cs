using BuilderAssistant.Core.Blueprints;
using BuilderAssistant.Core.Building;

namespace BuilderAssistant.Core.Tree
{
    /// <summary>
    /// Paints the giant central tree: a buttressed root flare, a tall organic tapered
    /// trunk of vertical oak logs, and a crown where the trunk thins into the canopy.
    /// Deterministic for a given <see cref="TreehouseSpec.Seed"/>. Only VERTICAL logs are
    /// authored (data 0), so nothing here depends on the un-verified horizontal-log mirror
    /// axis. The trunk core columns are always solid so the whole tree is one connected
    /// mass; only the outer rim is thinned by the seeded "bark wobble" for a natural,
    /// irregular silhouette (AGENTS.md: no simple rectangular stacking).
    /// </summary>
    internal sealed class GiantTreeGenerator
    {
        public void Generate(TreeCanvas c, TreehouseSpec spec)
        {
            var rng = new DeterministicRandom(spec.Seed ^ 0x51E3A7);
            int cx = spec.CenterX, cz = spec.CenterZ;
            int trunkTop = spec.TrunkTopY;

            for (int y = 0; y <= trunkTop; y++)
            {
                int r = TrunkRadius(y, trunkTop);

                // Root flare: a wide buttress at the very bottom, plus splayed root legs.
                if (y <= 1) r += 1;

                for (int dz = -r - 1; dz <= r + 1; dz++)
                {
                    for (int dx = -r - 1; dx <= r + 1; dx++)
                    {
                        int x = cx + dx, z = cz + dz;
                        if (x < 0 || z < 0 || x >= spec.Width || z >= spec.Depth) continue;

                        // Euclidean-ish round cross-section with a little margin so it reads
                        // as a trunk, not a diamond.
                        int dist2 = dx * dx + dz * dz;
                        int rr = r * r + r;
                        if (dist2 > rr) continue;

                        // Organic rim wobble: thin the outermost ring sometimes, but NEVER
                        // the inner core (|dx|,|dz| <= r-1) so the trunk stays solid/connected.
                        bool rim = dist2 > (r - 1) * (r - 1) + (r - 1);
                        if (rim && y > 2 && !rng.Chance(72)) continue;

                        c.Set(x, y, z, TreeCanvas.Log, BlockFamily.Log, 0, TrunkPhase(y, spec));
                    }
                }
            }

            // Crown knob: a short spread of logs just above the trunk top so the canopy has
            // a solid anchor to grow from.
            int cy = trunkTop;
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                    c.Set(cx + dx, cy, cz + dz, TreeCanvas.Log, BlockFamily.Log, 0, TreehousePhaseNames.TrunkUpper);
        }

        private static string TrunkPhase(int y, TreehouseSpec spec)
        {
            if (y <= 2) return TreehousePhaseNames.Roots;
            return y <= 13 ? TreehousePhaseNames.TrunkLower : TreehousePhaseNames.TrunkUpper;
        }

        /// <summary>
        /// A natural trunk radius profile: fat at the base, tapering to a slim crown. It is
        /// monotonic-ish so the tree looks like it is carrying the floors, with the mid
        /// section (where the floors bolt on) kept thick enough to look load-bearing.
        /// </summary>
        private static int TrunkRadius(int y, int trunkTop)
        {
            if (y <= 2) return 3;
            if (y <= 13) return 2;
            if (y <= 22) return 2;
            if (y <= trunkTop) return 1;
            return 1;
        }
    }
}

using System.Collections.Generic;
using BuilderAssistant.Core.Blueprints;
using BuilderAssistant.Core.Building;

namespace BuilderAssistant.Core.Tree
{
    /// <summary>
    /// Paints the evergreen foliage canopy that gives the giant tree its silhouette and,
    /// in the live game, its sense of being ALIVE. Every leaf puff is centred on a proven
    /// log anchor (a branch tip, a branch spine cell, or the trunk crown), so each painted
    /// leaf is within Chebyshev distance 3 of a WoodBlock - the exact survival condition
    /// <c>SubsystemWoodBlockBehavior</c> uses in x25.01.17. A leaf that would not satisfy
    /// it is skipped, never faked, so the canopy is stable across save/reload.
    ///
    /// Aesthetic goals (AGENTS.md / task §4): the crown is lumpy and irregular rather than
    /// a clean ball, taller over the top floor to frame the dwelling, thinner at the edges,
    /// with a few bare gaps so the trunk and branches read through the leaves. We deliberately
    /// avoid Math.Cos/Sin and use the integer DeterministicRandom + squared-distance tests so
    /// the exact same seed yields the exact same canopy on Windows, in the net8 harness, and
    /// on the Android client.
    /// </summary>
    internal sealed class TreeCanopyGenerator
    {
        // Hard ceiling on leaf cells for the whole canopy, purely so the offline preview and
        // the phase slicer never have to reason about an absurd blob. The slicer still
        // auto-sub-divides by Y-band if a phase would top the 1024 per-transaction cap.
        private const int MaxLeafCells = 1600;

        public void Generate(TreeCanvas c, TreehouseSpec spec, IList<CellPos> branchAnchors)
        {
            var rng = new DeterministicRandom(spec.Seed ^ 0x1C0FFEE);
            int cx = spec.CenterX, cz = spec.CenterZ;
            int radius = spec.CanopyRadius < 1 ? 1 : (spec.CanopyRadius > 3 ? 3 : spec.CanopyRadius);

            // 1) A solid crown mass at the trunk top, so the tree reads as topped-out even
            //    before the lateral branches are leafed. This is the visual anchor of the
            //    whole canopy and the reason the trunk must reach TrunkTopY.
            int crownY = spec.TrunkTopY;
            PaintPuff(c, rng, cx, crownY + 1, cz, radius + 1, spec, c.Count < MaxLeafCells);

            // 2) Lateral foliage: every branch tip gets a generous puff (they form the outer
            //    crown that overhangs the balconies); intermediate spine cells get one only
            //    on a seeded chance, which is what makes the canopy irregular instead of a
            //    mechanical ring of identical balls.
            for (int i = 0; i < branchAnchors.Count; i++)
            {
                if (c.Count >= MaxLeafCells) break;
                CellPos a = branchAnchors[i];
                bool isOuterTip = (i % 3 == 2); // tips registered last per branch, spread out
                int chance = isOuterTip ? 100 : 45;
                if (!rng.Chance(chance)) continue;
                int puffR = isOuterTip ? radius : radius - 1;
                if (puffR < 1) puffR = 1;
                // Leaves sit a touch above and beyond the limb so the bough disappears into foliage.
                PaintPuff(c, rng, a.X, a.Y + 1, a.Z, puffR, spec, true);
            }
        }

        /// <summary>
        /// Paint one roughly-spherical leaf cluster centered on a log anchor. Uses
        /// <see cref="TreeCanvas.SetIfEmpty"/> so foliage never erases the trunk/branches it
        /// hugs, skips the exact anchor cell so the wood stays visible through the leaves, and
        /// refuses any leaf that is not provably within Chebyshev 3 of some log.
        /// </summary>
        private static void PaintPuff(TreeCanvas c, DeterministicRandom rng,
            int ax, int ay, int az, int radius, TreehouseSpec spec, bool enabled)
        {
            if (!enabled || radius < 1)
            {
                return;
            }

            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dz = -radius; dz <= radius; dz++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        int x = ax + dx, y = ay + dy, z = az + dz;
                        if (x < 0 || z < 0 || y < 0) continue;
                        if (x >= spec.Width || z >= spec.Depth || y >= spec.Height) continue;

                        // Squared distance keeps it round-ish; a little slack so a radius-1
                        // puff is still a visible blob and not a plus-sign.
                        int d2 = dx * dx + dy * dy + dz * dz;
                        if (d2 > radius * radius + radius) continue;

                        // Never overwrite the log we are hugging (leave wood showing).
                        if (c.IsLog(x, y, z)) continue;
                        if (c.HasCell(x, y, z)) continue; // air only; house paints later anyway

                        // Organic lumps: thin the outermost shell sometimes so the silhouette
                        // is bumpy, but keep the inner core of the puff solid.
                        bool shell = d2 > (radius - 1) * (radius - 1) + (radius - 1);
                        if (shell && !rng.Chance(78)) continue;

                        // Decay-safety guard: a leaf must hug a log (Chebyshev <= 3).
                        if (!c.IsNearLog(x, y, z, 3)) continue;

                        c.SetIfEmpty(x, y, z, TreeCanvas.Leaf, BlockFamily.Cube, 0, TreehousePhaseNames.Canopy);
                    }
                }
            }
        }
    }
}

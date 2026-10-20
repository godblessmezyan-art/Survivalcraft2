using System.Collections.Generic;
using BuilderAssistant.Core.Blueprints;
using BuilderAssistant.Core.Building;

namespace BuilderAssistant.Core.Tree
{
    /// <summary>
    /// Paints the large lateral branches that spread the tree's silhouette and, critically,
    /// provide the log anchors the canopy must hug. Each branch climbs outward and upward
    /// from the trunk as a stepped spine of VERTICAL logs (no horizontal-log axis is used),
    /// so the limb reads as a rising bough while every one of its cells is a valid, save-safe
    /// vanilla log. The generator records every branch spine cell in <see cref="Anchors"/> so
    /// <see cref="TreeCanopyGenerator"/> can attach a foliage cluster at a proven log position
    /// (a leaf puff centered on a log is within Chebyshev 3 of it and therefore never decays).
    /// </summary>
    internal sealed class TreeBranchGenerator
    {
        /// <summary>Log cells the canopy is allowed to cluster around (branch spines + tips).</summary>
        public readonly List<CellPos> Anchors = new List<CellPos>();

        public void Generate(TreeCanvas c, TreehouseSpec spec)
        {
            var rng = new DeterministicRandom(spec.Seed ^ 0x2B7E12);
            int cx = spec.CenterX, cz = spec.CenterZ;

            // Pure-integer 8-compass table. Using a lookup (instead of Math.Cos/Sin) keeps the
            // branch layout bit-identical between .NET Framework 4.7.2 and .NET 8, which is the
            // whole point of the seeded deterministic generator.
            int[] compX = { 1, 1, 0, -1, -1, -1, 0, 1 };
            int[] compZ = { 0, 1, 1, 1, 0, -1, -1, -1 };

            const int branchCount = 6;
            for (int b = 0; b < branchCount; b++)
            {
                // Fan the branches evenly around the ring, with a small seeded slot jitter so
                // the fanning is not a mechanical pinwheel.
                int slot = (b * 8 / branchCount) + (rng.NextInt(3) - 1);
                slot = ((slot % 8) + 8) % 8;
                int dirX = compX[slot];
                int dirZ = compZ[slot];

                int startR = 2;                                   // leave the trunk surface
                int length = rng.Range(3, 5);
                int baseY = rng.Range(12, spec.TrunkTopY - 3);

                for (int step = 0; step < length; step++)
                {
                    int rad = startR + step;
                    int x = cx + dirX * rad;
                    int z = cz + dirZ * rad;
                    int y = baseY + step;                          // climbs as it goes out
                    if (x < 1 || z < 1 || x >= spec.Width - 1 || z >= spec.Depth - 1) break;
                    if (y >= spec.Height - 3) break;

                    // A 2-wide stub reads as a thick limb; the outer cell is the spine anchor.
                    c.SetIfEmpty(x, y, z, TreeCanvas.Branch, BlockFamily.Log, 0, TreehousePhaseNames.Branches);
                    Anchors.Add(new CellPos(x, y, z));

                    // Thicken: a neighbouring log half-steps up, giving the bough mass.
                    int px = x + dirZ, pz = z - dirX;              // perpendicular offset
                    if (px >= 1 && pz >= 1 && px < spec.Width - 1 && pz < spec.Depth - 1 && rng.Chance(60))
                    {
                        c.SetIfEmpty(px, y, pz, TreeCanvas.Branch, BlockFamily.Log, 0, TreehousePhaseNames.Branches);
                    }

                    // Branch tip sits a little higher: a final anchor for a crown-of-the-limb puff.
                    if (step == length - 1)
                    {
                        c.SetIfEmpty(x, y + 1, z, TreeCanvas.Branch, BlockFamily.Log, 0, TreehousePhaseNames.Branches);
                        Anchors.Add(new CellPos(x, y + 1, z));
                    }
                }
            }
        }
    }
}

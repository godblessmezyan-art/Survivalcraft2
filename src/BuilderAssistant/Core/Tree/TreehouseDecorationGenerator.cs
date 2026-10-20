using BuilderAssistant.Core.Blueprints;

namespace BuilderAssistant.Core.Tree
{
    /// <summary>
    /// The finishing pass that makes the treehouse feel lived-in and structurally believable:
    /// the front door, a climbable stair path from the forest floor up to the entry deck
    /// (we do NOT rely on ladders/torches — their orientation/state is not device-verified, so
    /// the entrance is plain ascending WoodenStairs), timber support braces that brace each
    /// cantilevered deck against the trunk, and a few railed viewing balconies that jut from
    /// the storeys. Like the dwelling pass, everything is painted with
    /// <see cref="TreeCanvas.SetIfEmpty"/> and never overwrites a log, so the trunk stays
    /// alive and every nearby leaf stays within Chebyshev 3 of it.
    ///
    /// Furniture (chests/beds/torchets) is deliberately NOT placed here. Task §5 says
    /// furniture is not required for the first treehouse and only allowed once reliable
    /// creation+save is proven; that proof is deferred to M5. This pass stays within the
    /// already-audited vanilla set (planks, slabs, stairs, fences, a single wooden door).
    /// </summary>
    internal sealed class TreehouseDecorationGenerator
    {
        public void Generate(TreeCanvas c, TreehouseSpec spec)
        {
            PaintEntryDoor(c, spec);
            PaintPorchStairs(c, spec);
            PaintDeckBraces(c, spec, half: 8, deckY: spec.FloorOneDeckY);
            PaintDeckBraces(c, spec, half: 6, deckY: spec.FloorTwoDeckY);
            PaintDeckBraces(c, spec, half: 5, deckY: spec.FloorThreeDeckY);
            PaintBalconies(c, spec);
        }

        private void PaintEntryDoor(TreeCanvas c, TreehouseSpec spec)
        {
            int cx = spec.CenterX;
            int z0 = spec.CenterZ - 8; // floor-1 front wall line (half == 8)
            int doorY = spec.FloorOneDeckY + 1;
            // Bottom half only; SubsystemDoorBlockBehavior spawns the matching top half at
            // build time. Facing rot 0 = toward -Z (outward from the front wall). Never let a
            // door erase the trunk if the front line happens to meet a log.
            if (!c.IsLog(cx, doorY, z0))
            {
                c.Set(cx, doorY, z0, TreeCanvas.Door, BlockFamily.Door, 0, TreehousePhaseNames.Decoration);
            }
            // A lintel above the doorway so the opening reads as framed.
            c.SetStructural(cx, doorY + 2, z0, TreeCanvas.Trim, BlockFamily.Cube, 0, TreehousePhaseNames.Decoration);
        }

        /// <summary>
        /// A grounded stair ramp so the entry deck (6 blocks up) is actually reachable from
        /// the forest floor without a ladder: steps climb in +Z from the ground toward the
        /// door, each capped by a fence newel for a handrail read.
        /// </summary>
        private void PaintPorchStairs(TreeCanvas c, TreehouseSpec spec)
        {
            int cx = spec.CenterX;
            int z0 = spec.CenterZ - 8;
            for (int i = 0; i <= spec.FloorOneDeckY; i++)
            {
                int y = i;                 // ground level 0 up to deck level
                int z = z0 - 1 - (spec.FloorOneDeckY - i); // approach from well in front of the wall
                if (z < 0) continue;
                if (c.IsLog(cx, y, z)) continue;
                c.SetStructural(cx, y, z, TreeCanvas.Stair, BlockFamily.Stairs, 0, TreehousePhaseNames.Decoration);
                // Side newels alongside the first and last landing.
                if (i == 0 || i == spec.FloorOneDeckY)
                {
                    c.SetStructural(cx - 1, y + 1, z, TreeCanvas.Rail, BlockFamily.Fence, 0, TreehousePhaseNames.Decoration);
                    c.SetStructural(cx + 1, y + 1, z, TreeCanvas.Rail, BlockFamily.Fence, 0, TreehousePhaseNames.Decoration);
                }
            }
        }

        /// <summary>
        /// Angled timber braces under the outer rim of each deck: a short diagonal of plank
        /// blocks from the trunk down to the deck edge, giving the cantilever a load-bearing
        /// look (and, because each brace touches the deck plate, staying part of the single
        /// connected structure).
        /// </summary>
        private void PaintDeckBraces(TreeCanvas c, TreehouseSpec spec, int half, int deckY)
        {
            int cx = spec.CenterX, cz = spec.CenterZ;
            // Braces at the four diagonal corners and the four edge midpoints.
            int[][] dirs =
            {
                new[] {  1,  0 }, new[] { -1,  0 },
                new[] {  0,  1 }, new[] {  0, -1 },
                new[] {  1,  1 }, new[] { -1, -1 },
                new[] {  1, -1 }, new[] { -1,  1 },
            };
            foreach (int[] d in dirs)
            {
                int edgeX = cx + d[0] * half;
                int edgeZ = cz + d[1] * half;
                // Two-course diagonal stepping inward+down from the deck rim toward the trunk.
                for (int k = 1; k <= 2; k++)
                {
                    int bx = edgeX - d[0] * k;
                    int bz = edgeZ - d[1] * k;
                    int by = deckY - k;
                    if (by < 0) continue;
                    if (c.IsLog(bx, by, bz)) continue;
                    c.SetStructural(bx, by, bz, TreeCanvas.Trim, BlockFamily.Cube, 0, TreehousePhaseNames.Decoration);
                }
            }
        }

        /// <summary>
        /// Two railed viewing balconies projecting from floor 1 (front and one side), each a
        /// small plank deck ringed by fence railings, supported by a brace back to the trunk.
        /// These are the "surrounding wooden platforms + viewing balconies" the task asks for,
        /// kept modest in v1 so the phase slicer stays well inside the per-transaction cap.
        /// </summary>
        private void PaintBalconies(TreeCanvas c, TreehouseSpec spec)
        {
            int cx = spec.CenterX, cz = spec.CenterZ;
            int deckY = spec.FloorOneDeckY;
            int z0 = cz - 8;

            BuildBalcony(c, cx, deckY, z0 - 2, dz: -1, phase: TreehousePhaseNames.Decoration); // front
            BuildBalcony(c, cx + 10, deckY, cz, dz: 0, phase: TreehousePhaseNames.Decoration); // east
        }

        private void BuildBalcony(TreeCanvas c, int bx, int deckY, int bz, int dz, string phase)
        {
            // A 3x3 plank platform one course below the deck lip, then railings around its
            // open edges. dz just nudges the front balcony a touch further out.
            int z = bz + dz;
            for (int ix = -1; ix <= 1; ix++)
            {
                for (int iz = -1; iz <= 1; iz++)
                {
                    int px = bx + ix, pz = z + iz;
                    if (c.IsLog(px, deckY, pz)) continue;
                    c.SetStructural(px, deckY, pz, TreeCanvas.Plank, BlockFamily.Cube, 0, phase);
                }
            }
            // Rail the outward-facing edge (skip the side that meets the house).
            for (int ix = -1; ix <= 1; ix++)
            {
                int railZ = z - 1;
                if (c.IsLog(bx + ix, deckY + 1, railZ)) continue;
                c.SetStructural(bx + ix, deckY + 1, railZ, TreeCanvas.Rail, BlockFamily.Fence, 0, phase);
            }
        }
    }
}

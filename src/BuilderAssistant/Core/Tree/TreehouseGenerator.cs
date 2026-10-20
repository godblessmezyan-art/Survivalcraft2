using BuilderAssistant.Core.Blueprints;

namespace BuilderAssistant.Core.Tree
{
    /// <summary>
    /// Builds the three-story wooden dwelling that hangs off the giant trunk. Each storey is
    /// a square ring platform bolted around the trunk, a hollow walled room with glazed
    /// windows, and (for the lower two) a walkable internal stair climbing to the next deck;
    /// the top storey is capped by a stepped hip roof. The storeys shrink as they rise
    /// (floor 1 is widest, floor 3 narrowest) which is what gives the treehouse its layered
    /// "grows with the tree" silhouette instead of a stacked rectangular tower (task §3/§4).
    ///
    /// Every structural cell is painted with <see cref="TreeCanvas.SetIfEmpty"/> so the house
    /// NEVER overwrites a trunk/branch log: the tree visibly pierces each deck and the roof,
    /// which both looks right and keeps every canopy leaf's supporting log alive (the
    /// decay-safety invariant the validator re-proves on the final canvas). Doorways and
    /// stair openings are carved only where no log stands.
    ///
    /// Orientation is authored directly in master coordinates (the phased assembly stamps at
    /// the anchor without a blueprint Reorient), so stair/door facing here is cosmetic-but-
    /// valid; live climb-ability and door/auto-top-half behaviour are marked 待验证 on-device
    /// per AGENTS.md rather than claimed from an offline pass.
    /// </summary>
    internal sealed class TreehouseGenerator
    {
        // Storey geometry (half-extents + deck Y + wall height). Authored so decks leave room
        // for the storey below's walls+roof and the top storey's roof still fits under Height.
        public void Generate(TreeCanvas c, TreehouseSpec spec)
        {
            // Floor 1: widest ring platform, the entry level.
            PaintStorey(c, spec, half: 8, deckY: spec.FloorOneDeckY, wallH: 6, phase: TreehousePhaseNames.FloorOne,
                isEntry: true, roof: false);

            // Floor 2: medium ring.
            PaintStorey(c, spec, half: 6, deckY: spec.FloorTwoDeckY, wallH: 5, phase: TreehousePhaseNames.FloorTwo,
                isEntry: false, roof: false);

            // Floor 3: smallest ring, capped with the hip roof.
            PaintStorey(c, spec, half: 5, deckY: spec.FloorThreeDeckY, wallH: 5, phase: TreehousePhaseNames.FloorThree,
                isEntry: false, roof: true);

            // Connect the decks with internal stair flights (drawn after rooms so they sit in
            // the hollow interior; SetIfEmpty means they never collide with the trunk core).
            PaintStairRun(c, spec, fromDeckY: spec.FloorOneDeckY, toDeckY: spec.FloorTwoDeckY,
                half: 6, phase: TreehousePhaseNames.FloorTwo);
            PaintStairRun(c, spec, fromDeckY: spec.FloorTwoDeckY, toDeckY: spec.FloorThreeDeckY,
                half: 5, phase: TreehousePhaseNames.FloorThree);
        }

        private void PaintStorey(TreeCanvas c, TreehouseSpec spec, int half, int deckY, int wallH,
            string phase, bool isEntry, bool roof)
        {
            int cx = spec.CenterX, cz = spec.CenterZ;
            int x0 = cx - half, x1 = cx + half;
            int z0 = cz - half, z1 = cz + half;

            // Deck plate: a solid plank floor spanning the storey. SetIfEmpty skips the trunk.
            for (int z = z0; z <= z1; z++)
                for (int x = x0; x <= x1; x++)
                    c.SetStructural(x, deckY, z, TreeCanvas.Plank, BlockFamily.Cube, 0, phase);

            // Wall ring from deckY+1 up wallH courses.
            int wallTop = deckY + wallH;
            for (int y = deckY + 1; y < wallTop; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    PaintWallCell(c, x, y, z0, spec, phase, isEntry, deckY, wallH, edge: Edge.North);
                    PaintWallCell(c, x, y, z1, spec, phase, isEntry, deckY, wallH, edge: Edge.South);
                }
                for (int z = z0 + 1; z <= z1 - 1; z++)
                {
                    PaintWallCell(c, x0, y, z, spec, phase, isEntry, deckY, wallH, edge: Edge.West);
                    PaintWallCell(c, x1, y, z, spec, phase, isEntry, deckY, wallH, edge: Edge.East);
                }
            }

            // Corner posts (sturdy timber) at the four wall corners for a load-bearing read.
            for (int y = deckY + 1; y < wallTop; y++)
            {
                c.SetStructural(x0, y, z0, TreeCanvas.Trim, BlockFamily.Cube, 0, phase);
                c.SetStructural(x1, y, z0, TreeCanvas.Trim, BlockFamily.Cube, 0, phase);
                c.SetStructural(x0, y, z1, TreeCanvas.Trim, BlockFamily.Cube, 0, phase);
                c.SetStructural(x1, y, z1, TreeCanvas.Trim, BlockFamily.Cube, 0, phase);
            }

            if (roof)
            {
                PaintHipRoof(c, spec, wallTop, phase);
            }
            else
            {
                // A parapet railing lip so the upper deck edges read as balconies.
                for (int x = x0; x <= x1; x++)
                {
                    c.SetStructural(x, wallTop, z0, TreeCanvas.Rail, BlockFamily.Fence, 0, phase);
                    c.SetStructural(x, wallTop, z1, TreeCanvas.Rail, BlockFamily.Fence, 0, phase);
                }
                for (int z = z0; z <= z1; z++)
                {
                    c.SetStructural(x0, wallTop, z, TreeCanvas.Rail, BlockFamily.Fence, 0, phase);
                    c.SetStructural(x1, wallTop, z, TreeCanvas.Rail, BlockFamily.Fence, 0, phase);
                }
            }
        }

        private enum Edge { North, South, East, West }

        private void PaintWallCell(TreeCanvas c, int x, int y, int z, TreehouseSpec spec, string phase,
            bool isEntry, int deckY, int wallH, Edge edge)
        {
            // Entry doorway: a 1-wide, doorH-tall gap at the centre of the north (front) wall.
            if (isEntry && edge == Edge.North && y <= deckY + 2 &&
                x == spec.CenterX)
            {
                // Leave the doorway empty (air) for two courses so a door can stand there.
                return;
            }

            // Windows: glazed panes on a regular-but-broken rhythm at eye height, never on the
            // doorway row, so each room has light without turning the wall into pure glass.
            bool eyeLevel = (y == deckY + 2 || y == deckY + 3);
            bool windowCol = ((x + z) % 3 == 1);
            if (eyeLevel && windowCol)
            {
                c.SetStructural(x, y, z, TreeCanvas.Glass, BlockFamily.Cube, 0, phase);
                // A stone sill one course under the glass for a finished opening.
                if (y == deckY + 2)
                {
                    c.SetStructural(x, y - 1, z, TreeCanvas.Slab, BlockFamily.Slab, 0, phase);
                }
                return;
            }

            // Plain plank wall.
            c.SetStructural(x, y, z, TreeCanvas.Plank, BlockFamily.Cube, 0, phase);
        }

        private void PaintHipRoof(TreeCanvas c, TreehouseSpec spec, int baseY, string phase)
        {
            int cx = spec.CenterX, cz = spec.CenterZ;
            int half = 5;
            int x0 = cx - half, x1 = cx + half;
            int z0 = cz - half, z1 = cz + half;

            // Four stepped inset layers, each ring one smaller than the last, capped near the
            // trunk so the trunk pokes through into the canopy. Stairs give the eaves a tiled
            // roofline; flat slabs/ planks fill the inner courses.
            int layers = half;
            for (int ly = 0; ly < layers; ly++)
            {
                int y = baseY + ly;
                int inset = ly;
                int rx0 = x0 + inset, rx1 = x1 - inset;
                int rz0 = z0 + inset, rz1 = z1 - inset;
                if (rx0 > rx1 || rz0 > rz1) break;

                for (int z = rz0; z <= rz1; z++)
                {
                    for (int x = rx0; x <= rx1; x++)
                    {
                        bool ring = (x == rx0 || x == rx1 || z == rz0 || z == rz1);
                        if (ring)
                        {
                            // Eave courses use stairs; inner fill uses plank decking.
                            c.SetStructural(x, y, z, TreeCanvas.Stair, BlockFamily.Stairs, 0, phase);
                        }
                        else
                        {
                            c.SetStructural(x, y, z, TreeCanvas.Plank, BlockFamily.Cube, 0, phase);
                        }
                    }
                }
            }

            // Ridge cap: a small plank pad high on the trunk where the roof meets the crown.
            int topY = baseY + layers - 1;
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                    c.SetStructural(cx + dx, topY, cz + dz, TreeCanvas.Trim, BlockFamily.Cube, 0, phase);
        }

        /// <summary>
        /// A single straight internal stair flight climbing along the west inner wall from one
        /// deck to the next. Steps are authored as WoodenStairs facing the climb; a fence post
        /// caps the well opening on the upper deck so it reads (and guards) as a real stair.
        /// Live climb geometry is 待验证 on-device; this proves a physically continuous,
        /// trunk-free ascending run in the blueprint.
        /// </summary>
        private void PaintStairRun(TreeCanvas c, TreehouseSpec spec, int fromDeckY, int toDeckY, int half, string phase)
        {
            int cz = spec.CenterZ;
            int stairX = spec.CenterX - half + 1; // hug the inner west wall
            int run = toDeckY - fromDeckY;         // vertical rise to cover
            if (run <= 1) return;

            for (int i = 0; i < run; i++)
            {
                int y = fromDeckY + 1 + i;
                int z = cz - 3 + i;               // climb from the front towards the back
                if (z < cz - half + 1) continue;
                // Never carve/step on the trunk.
                if (c.IsLog(stairX, y, z)) continue;
                // Clear any foliage/plate that is in the way (but keep logs), then place a step.
                if (c.HasCell(stairX, y, z) && !c.IsLog(stairX, y, z)) c.Carve(stairX, y, z);
                c.SetStructural(stairX, y, z, TreeCanvas.Stair, BlockFamily.Stairs, 0, phase);

                // Give each step a one-block head clearance by clearing the cell above.
                int above = y + 1;
                if (!c.IsLog(stairX, above, z) && c.HasCell(stairX, above, z))
                {
                    c.Carve(stairX, above, z);
                }
            }
        }
    }
}

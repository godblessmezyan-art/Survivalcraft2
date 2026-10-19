namespace BuilderAssistant.Core.Building
{
    /// <summary>
    /// Floor-to-floor connection. A genuine two-storey house needs a WALKABLE stair, not
    /// just a decorative one, so this stage:
    ///   - picks a two-column switchback stairwell against the left interior wall,
    ///   - cuts a real void through the destination floor slab along the stair run (so the
    ///     two storeys' interior air volumes are actually connected),
    ///   - lays WoodenStairs blocks whose facing follows the climb direction,
    ///   - records the stairwell facts on the context so <c>BuildingValidator</c> can prove
    ///     the vertical link exists (and so <c>OpeningGenerator</c> keeps it off the entry).
    ///
    /// Single-storey specs (the cottage) have no floors to connect and return unchanged.
    /// All loops are bounded by the storey/step counts derived from the (validated) spec, so
    /// there is no possibility of an unbounded scan.
    /// </summary>
    internal sealed class StairGenerator : IBuildingComponent
    {
        public void Generate(BuildingContext ctx)
        {
            int floors = ctx.Spec.Floors;
            if (floors < 2)
            {
                return; // nothing to connect
            }

            int colA = ctx.X0 + 1;
            int colB = ctx.X0 + 2; // Width >= 7 was validated, so colB stays interior.
            ctx.HasStairwell = true;
            ctx.StairwellX = colA;

            string stairMat = ctx.Spec.Materials.InteriorStair;
            bool asc = true;

            for (int story = 0; story < floors - 1; story++)
            {
                int col = (story % 2 == 0) ? colA : colB;
                int yBottom = ctx.Spec.FloorSlabY(story) + 1;
                int yTop = ctx.Spec.FloorSlabY(story + 1); // destination floor slab level
                int steps = yTop - yBottom + 1;            // WallHeight + 1
                int rot = asc ? 2 : 0;
                int zDir = asc ? 1 : -1;
                int z0 = asc ? (ctx.Z0 + 1) : (ctx.Z1 - 1);

                int zRunStart = z0;
                int zRunEnd = z0;

                for (int i = 0; i < steps; i++)
                {
                    int y = yBottom + i;
                    int z = z0 + zDir * i;
                    if (z <= ctx.Z0 || z >= ctx.Z1)
                    {
                        break; // never grow the flight through an exterior wall
                    }
                    zRunEnd = z;
                    ctx.SetStairs(col, y, z, stairMat, rot);
                }

                // Cut the stairwell void through the destination slab along the flight's
                // footprint so the storeys connect. The head cell is (re)stamped as a stair
                // afterwards so the top step reads correctly rather than as a bare hole.
                int lo = System.Math.Min(zRunStart, zRunEnd);
                int hi = System.Math.Max(zRunStart, zRunEnd);
                for (int z = lo; z <= hi; z++)
                {
                    ctx.Builder.Carve(col, yTop, z, col, yTop, z);
                }
                ctx.SetStairs(col, yTop, zRunEnd, stairMat, rot);

                // A short railing at the well head on the upper floor, set BESIDE the void
                // (never across it) so the passage stays open but the edge is guarded.
                int railCol = (col == colA) ? colB : colA;
                if (railCol <= ctx.X1 - 1 && railCol >= ctx.X0 + 1)
                {
                    ctx.SetFence(railCol, yTop + 1, zRunEnd, ctx.Spec.Materials.Railing, 12);
                }

                asc = !asc;
            }
        }
    }
}

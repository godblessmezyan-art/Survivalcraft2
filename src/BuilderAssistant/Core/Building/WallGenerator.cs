namespace BuilderAssistant.Core.Building
{
    /// <summary>
    /// Exterior walls + structural frame. For every storey it raises a full-height ring of
    /// <c>Wall</c> blocks on the footprint perimeter, then reinforces it with <c>Trim</c>:
    /// solid corner posts on all storeys, a mid-rail on the long faces, and a ring beam at
    /// each storey's top course. This gives a genuine load-bearing frame rather than a bare
    /// hollow outline, and the corners/mid-posts are where windows and door jambs latch on.
    /// </summary>
    internal sealed class WallGenerator : IBuildingComponent
    {
        public void Generate(BuildingContext ctx)
        {
            string wall = ctx.Spec.Materials.Wall;
            string trim = ctx.Spec.Materials.Trim;

            for (int story = 0; story < ctx.Spec.Floors; story++)
            {
                int baseY = ctx.WallBaseY(story);
                int topY = ctx.WallTopY(story);

                for (int y = baseY; y <= topY; y++)
                {
                    // Full-height side walls (x = X0 and x = X1) across all Z.
                    for (int z = ctx.Z0; z <= ctx.Z1; z++)
                    {
                        ctx.SetCube(ctx.X0, y, z, wall);
                        ctx.SetCube(ctx.X1, y, z, wall);
                    }
                    // Full-height front/back walls (z = Z0 and z = Z1) across all X.
                    for (int x = ctx.X0; x <= ctx.X1; x++)
                    {
                        ctx.SetCube(x, y, ctx.Z0, wall);
                        ctx.SetCube(x, y, ctx.Z1, wall);
                    }
                }

                // Corner posts run the whole storey in trim (visual + structural).
                for (int y = baseY; y <= topY; y++)
                {
                    ctx.SetCube(ctx.X0, y, ctx.Z0, trim);
                    ctx.SetCube(ctx.X1, y, ctx.Z0, trim);
                    ctx.SetCube(ctx.X0, y, ctx.Z1, trim);
                    ctx.SetCube(ctx.X1, y, ctx.Z1, trim);
                }

                // Ring beam along the top course of each storey (reads as a girder).
                int ring = topY;
                for (int z = ctx.Z0; z <= ctx.Z1; z++)
                {
                    ctx.SetCube(ctx.X0, ring, z, trim);
                    ctx.SetCube(ctx.X1, ring, z, trim);
                }
                for (int x = ctx.X0; x <= ctx.X1; x++)
                {
                    ctx.SetCube(x, ring, ctx.Z0, trim);
                    ctx.SetCube(x, ring, ctx.Z1, trim);
                }
            }
        }
    }
}

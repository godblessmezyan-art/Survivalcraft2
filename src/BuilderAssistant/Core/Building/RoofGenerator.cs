namespace BuilderAssistant.Core.Building
{
    /// <summary>
    /// Roof, in three parameterized shapes (all bounded loops, all relative coords). The
    /// ceiling plate already exists at <c>Spec.CeilingY</c> (from Foundation); this stage
    /// sits the roof above it and re-skins the eave edge with oriented stairs so the roof
    /// reads as a slope and exercises the stair facing rules:
    ///   - Hip:    stepped inset plates converging to a ridge/point (the cottage roof).
    ///   - Gabled: two slopes meeting at a single ridge along the wider axis, with gable-end
    ///             triangles on the two shorter faces.
    ///   - Flat:   an open deck ringed by a railing (a usable terrace).
    /// The number of stepping courses is capped by the storey height so a roof can never
    /// push the structure past the axis/budget already checked by the spec validator.
    /// </summary>
    internal sealed class RoofGenerator : IBuildingComponent
    {
        public void Generate(BuildingContext ctx)
        {
            int baseY = ctx.Spec.CeilingY;
            switch (ctx.Spec.Roof)
            {
                case RoofType.Gabled:
                    BuildGabled(ctx, baseY);
                    break;
                case RoofType.Flat:
                    BuildFlat(ctx, baseY);
                    break;
                case RoofType.Hip:
                default:
                    BuildHip(ctx, baseY);
                    break;
            }
        }

        private static void BuildHip(BuildingContext ctx, int baseY)
        {
            string roof = ctx.Spec.Materials.Roof;
            string stair = ctx.Spec.Materials.Stair;

            // Eave ring on the ceiling plate border, re-skin as inward-facing stairs.
            for (int x = ctx.X0; x <= ctx.X1; x++)
            {
                ctx.SetStairs(x, baseY, ctx.Z0, stair, 2);
                ctx.SetStairs(x, baseY, ctx.Z1, stair, 0);
            }
            for (int z = ctx.Z0 + 1; z <= ctx.Z1 - 1; z++)
            {
                ctx.SetStairs(ctx.X0, baseY, z, stair, 3);
                ctx.SetStairs(ctx.X1, baseY, z, stair, 1);
            }

            int limit = ctx.Spec.WallHeight + 2; // keep the stack bounded
            for (int i = 1; i <= limit; i++)
            {
                int zx0 = ctx.X0 + i, zx1 = ctx.X1 - i;
                int zz0 = ctx.Z0 + i, zz1 = ctx.Z1 - i;
                if (zx0 > zx1 || zz0 > zz1) break;
                int y = baseY + i;
                for (int z = zz0; z <= zz1; z++)
                {
                    for (int x = zx0; x <= zx1; x++)
                    {
                        ctx.SetCube(x, y, z, roof);
                    }
                }
            }
        }

        private static void BuildGabled(BuildingContext ctx, int baseY)
        {
            string roof = ctx.Spec.Materials.Roof;
            string stair = ctx.Spec.Materials.Stair;

            // Eave stairs along the two long eave edges (front/back).
            for (int x = ctx.X0; x <= ctx.X1; x++)
            {
                ctx.SetStairs(x, baseY, ctx.Z0, stair, 2);
                ctx.SetStairs(x, baseY, ctx.Z1, stair, 0);
            }

            int midZ = (ctx.Z0 + ctx.Z1) / 2;
            int rise = midZ - ctx.Z0;
            int limit = System.Math.Min(rise, ctx.Spec.WallHeight + 2);

            for (int i = 1; i <= limit; i++)
            {
                int y = baseY + i;
                int fz = ctx.Z0 + i;
                int bz = ctx.Z1 - i;

                // The two slope rafters (full width along X).
                for (int x = ctx.X0; x <= ctx.X1; x++)
                {
                    ctx.SetCube(x, y, fz, roof);
                    if (bz != fz) ctx.SetCube(x, y, bz, roof);
                }
                // Gable-end infill on the two short faces (x = X0 and x = X1).
                int lo = System.Math.Min(fz, bz);
                int hi = System.Math.Max(fz, bz);
                for (int z = lo; z <= hi; z++)
                {
                    ctx.SetCube(ctx.X0, y, z, roof);
                    ctx.SetCube(ctx.X1, y, z, roof);
                }
            }
        }

        private static void BuildFlat(BuildingContext ctx, int baseY)
        {
            string rail = ctx.Spec.Materials.Railing;
            int y = baseY + 1;

            // Parapet railing around the flat deck (fence arms point along the run).
            for (int x = ctx.X0; x <= ctx.X1; x++)
            {
                ctx.SetFence(x, y, ctx.Z0, rail, 3);
                ctx.SetFence(x, y, ctx.Z1, rail, 3);
            }
            for (int z = ctx.Z0; z <= ctx.Z1; z++)
            {
                ctx.SetFence(ctx.X0, y, z, rail, 12);
                ctx.SetFence(ctx.X1, y, z, rail, 12);
            }
        }
    }
}

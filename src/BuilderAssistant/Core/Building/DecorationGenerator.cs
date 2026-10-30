using BuilderAssistant.Core.Blueprints;

namespace BuilderAssistant.Core.Building
{
    /// <summary>
    /// Finishing detail. Adds the small, low-cost pieces that make a building feel built
    /// rather than boxy: a stone chimney stack that pierces the roof (optional by spec), a
    /// couple of interior furnishings (a plank table with slab seats), and - for hip roofs -
    /// two ridge fence finials. Everything is bounded and interior/edge-safe (never on the
    /// door column or the stairwell void), and uses only the material profile's logical keys.
    /// </summary>
    internal sealed class DecorationGenerator : IBuildingComponent
    {
        public void Generate(BuildingContext ctx)
        {
            if (ctx.Spec.HasChimney)
            {
                BuildChimney(ctx);
            }
            if (ctx.Spec.Roof == RoofType.Hip)
            {
                AddRidgeFinials(ctx);
            }
            if(ctx.Spec.Type != BuildingType.FurnishedHome) BuildFurniture(ctx);
        }

        private static void BuildChimney(BuildingContext ctx)
        {
            // A stone flue in the back-right, clear of the left-side stairwell, running from
            // the ground up through the roof so it reads as a real chimney.
            int cx = ctx.X1 - 2;
            int cz = ctx.Z1 - 2;
            if (cx <= ctx.X0 || cz <= ctx.Z0)
            {
                return;
            }
            if (ctx.HasStairwell && (cx == ctx.StairwellX || cx == ctx.StairwellX + 1))
            {
                return;
            }
            int topY = ctx.Spec.CeilingY + 1;
            for (int y = ctx.WallBaseY(0); y <= topY; y++)
            {
                ctx.Builder.Set(cx, y, cz, BuildingMaterials.StoneWall);
            }
        }

        private static void AddRidgeFinials(BuildingContext ctx)
        {
            int centerX = (ctx.X0 + ctx.X1) / 2;
            int y = ctx.Spec.CeilingY + 2;
            int fz = ctx.Z0 + 2;
            int bz = ctx.Z1 - 2;
            if (fz < bz)
            {
                ctx.SetFence(centerX, y, fz, ctx.Spec.Materials.Railing, 0);
                ctx.SetFence(centerX, y, bz, ctx.Spec.Materials.Railing, 0);
            }
        }

        private static void BuildFurniture(BuildingContext ctx)
        {
            if (ctx.Spec.Width < 7 || ctx.Spec.Length < 9)
            {
                return; // keep tiny footprints clean rather than jamming props into them
            }
            int doorX = (ctx.X0 + ctx.X1) / 2;
            int y = ctx.WallBaseY(0);

            // A plank table with two slab seats, biased to the right half (off the entry
            // column and off the left stairwell).
            int tx = doorX + 1 <= ctx.X1 - 1 ? doorX + 1 : ctx.X1 - 1;
            int tz = ctx.Z0 + 3;
            if (tz < ctx.Z1 && ctx.IsInteriorColumn(tx, tz))
            {
                ctx.SetCube(tx, y, tz, ctx.Spec.Materials.Trim);           // table
                ctx.SetSlabBottom(tx - 1, y, tz, ctx.Spec.Materials.Slab);  // seat
                ctx.SetSlabBottom(tx + 1, y, tz, ctx.Spec.Materials.Slab);  // seat
            }
        }
    }
}

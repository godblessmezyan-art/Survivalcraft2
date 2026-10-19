namespace BuilderAssistant.Core.Building
{
    /// <summary>
    /// Foundation + floor plates. Places a full horizontal slab at every storey level:
    /// the ground plinth (Foundation material) and one Floor plate per upper storey, plus
    /// the top ceiling plate the roof will sit on. Everything above the ground plate is
    /// left hollow by design - this is the building's floor structure, not solid fill.
    ///
    /// Runs FIRST so later stages (stairs, openings) can carve through these plates and
    /// rely on last-write-wins to punch the stairwell openings and doorways.
    /// </summary>
    internal sealed class FoundationGenerator : IBuildingComponent
    {
        public void Generate(BuildingContext ctx)
        {
            int floors = ctx.Spec.Floors;
            for (int story = 0; story <= floors; story++) // floors+1 plates: ground .. ceiling
            {
                int y = ctx.Spec.FloorSlabY(story);
                string material = (story == 0) ? ctx.Spec.Materials.Foundation : ctx.Spec.Materials.Floor;
                for (int z = ctx.Z0; z <= ctx.Z1; z++)
                {
                    for (int x = ctx.X0; x <= ctx.X1; x++)
                    {
                        ctx.SetCube(x, y, z, material);
                    }
                }
            }
        }
    }
}

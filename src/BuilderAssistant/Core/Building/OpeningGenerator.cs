namespace BuilderAssistant.Core.Building
{
    /// <summary>
    /// Doors and windows. Places ONE front entry door (authoring only its bottom cell and
    /// leaving a clear two-tall doorway above it, so the engine's auto-top has headroom and
    /// the entrance is guaranteed passable), then glazes windows across all four faces using
    /// the seeded RNG and the spec's <c>WindowDensity</c>.
    ///
    /// The door column is fixed at the footprint centre - the entry is a structural promise
    /// the validator must always be able to verify, so it is deliberately NOT randomized.
    /// What DOES vary with the seed is the window set and its sills ("门窗组合"), which is
    /// cosmetic and cannot break circulation. Runs after walls so it can overwrite wall
    /// courses with glass and sills, and after stairs so the stairwell column is known.
    /// </summary>
    internal sealed class OpeningGenerator : IBuildingComponent
    {
        public void Generate(BuildingContext ctx)
        {
            PlaceEntryDoor(ctx);
            PlaceWindows(ctx);
        }

        private static void PlaceEntryDoor(BuildingContext ctx)
        {
            int doorX = (ctx.X0 + ctx.X1) / 2;
            int doorY = ctx.WallBaseY(0); // ground storey base = 1
            int doorZ = ctx.Z0;            // front wall line

            // Punch a clean two-tall doorway, then stamp only the bottom door cell. The
            // cell above stays air so SubsystemDoorBlockBehavior can spawn the top half,
            // and the foundation plate below (y=0) gives the door its required support.
            ctx.Builder.Carve(doorX, doorY, doorZ, doorX, doorY + 1, doorZ);
            ctx.SetDoor(doorX, doorY, doorZ, ctx.Spec.Materials.Door, 2); // rot2 = faces +Z (in)

            ctx.HasDoor = true;
            ctx.DoorX = doorX;
            ctx.DoorY = doorY;
            ctx.DoorZ = doorZ;
            ctx.DoorFacingRot = 2;
        }

        private static void PlaceWindows(BuildingContext ctx)
        {
            string glass = ctx.Spec.Materials.Glass;
            string sill = ctx.Spec.Materials.Slab;
            int doorX = (ctx.X0 + ctx.X1) / 2;

            for (int story = 0; story < ctx.Spec.Floors; story++)
            {
                int baseY = ctx.WallBaseY(story);
                int topY = ctx.WallTopY(story);
                int midY = baseY + (ctx.Spec.WallHeight / 2);
                if (midY < baseY) midY = baseY;
                if (midY > topY) midY = topY;

                // Front and back walls (vary X along the wall line).
                int[] facesZ = { ctx.Z0, ctx.Z1 };
                for (int f = 0; f < facesZ.Length; f++)
                {
                    int z = facesZ[f];
                    for (int x = ctx.X0 + 1; x <= ctx.X1 - 1; x += 2)
                    {
                        if (z == ctx.Z0 && x == doorX) continue; // keep the entry clear
                        if (ctx.Rng.Chance(ctx.Spec.WindowDensity))
                        {
                            ctx.SetCube(x, midY, z, glass);
                            if (midY - 1 >= baseY) ctx.SetSlabBottom(x, midY - 1, z, sill);
                        }
                    }
                }

                // Left and right walls (vary Z along the wall line).
                int[] facesX = { ctx.X0, ctx.X1 };
                for (int f = 0; f < facesX.Length; f++)
                {
                    int x = facesX[f];
                    for (int z = ctx.Z0 + 1; z <= ctx.Z1 - 1; z += 2)
                    {
                        if (ctx.Rng.Chance(ctx.Spec.WindowDensity))
                        {
                            ctx.SetCube(x, midY, z, glass);
                            if (midY - 1 >= baseY) ctx.SetSlabBottom(x, midY - 1, z, sill);
                        }
                    }
                }
            }
        }
    }
}

namespace BuilderAssistant.Core.Building
{
    /// <summary>
    /// Interior layout: room partitions and support beams. Runs after walls/openings so it
    /// only ever writes INTERIOR columns (never on the exterior ring). It adds one cross
    /// partition per storey whose doorway gap is pinned to the entry column, which keeps
    /// the space divided into rooms while guaranteeing the front door still opens into a
    /// connected, walkable interior (the validator floods this). The partition's Z is chosen
    /// by the seeded RNG, so different seeds give different room layouts within the same
    /// footprint.
    /// </summary>
    internal sealed class FloorPlanGenerator : IBuildingComponent
    {
        public void Generate(BuildingContext ctx)
        {
            if (ctx.Spec.Type == BuildingType.FurnishedHome)
            {
                ctx.HomeLayout=FurnishedHomeLayout.Create(ctx.Spec);
                for(int x=1;x<ctx.Spec.Width-1;x++)
                for(int y=1;y<=ctx.Spec.WallHeight;y++)
                    if(x!=ctx.HomeLayout.Entrance.X || y>2)
                        ctx.SetCube(x,y,ctx.HomeLayout.PartitionZ,ctx.Spec.Materials.InteriorWall);
                return;
            }
            int doorX = (ctx.X0 + ctx.X1) / 2;
            string wall = ctx.Spec.Materials.InteriorWall;
            string beam = ctx.Spec.Materials.Trim;

            bool twoCourse = ctx.Spec.WallHeight >= 3;

            for (int story = 0; story < ctx.Spec.Floors; story++)
            {
                int baseY = ctx.WallBaseY(story);

                if (ctx.Spec.Width >= 5 && ctx.Spec.Length >= 7)
                {
                    int zMin = ctx.Z0 + 2;
                    int zMax = ctx.Z1 - 2;
                    int span = zMax - zMin + 1;
                    if (span >= 1)
                    {
                        int pz = zMin + ctx.Rng.NextInt(span); // seeded room split

                        for (int x = ctx.X0 + 1; x <= ctx.X1 - 1; x++)
                        {
                            if (x == doorX) continue; // keep the entry hallway open
                            // Do not run the partition straight through the stairwell void.
                            if (ctx.HasStairwell && (x == ctx.StairwellX || x == ctx.StairwellX + 1))
                            {
                                continue;
                            }
                            ctx.SetCube(x, baseY, pz, wall);
                            if (twoCourse) ctx.SetCube(x, baseY + 1, pz, wall);
                        }
                    }
                }

                // Two structural posts near the back corners (trim), clear of the well.
                int bx = ctx.X1 - 1;
                if (bx > ctx.X0 && (!ctx.HasStairwell || bx != ctx.StairwellX))
                {
                    for (int y = baseY; y <= ctx.WallTopY(story); y++)
                    {
                        ctx.SetCube(bx, y, ctx.Z1 - 1, beam);
                    }
                }
            }
        }
    }
}

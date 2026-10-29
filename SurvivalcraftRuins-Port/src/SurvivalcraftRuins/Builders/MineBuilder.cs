using SurvivalcraftRuins.Core;

namespace SurvivalcraftRuins.Builders;

/// <summary>
/// Abandoned mine: a surface headframe visible from afar, a ladder shaft, and
/// timbered galleries seeded with ore pockets, chests, and one cave-in.
/// Origin (0,0,0) is ground level at the shaft center; the workings extend to
/// negative Y.
/// </summary>
public static class MineBuilder
{
    private static readonly string[] s_ores =
    {
        "CoalOreBlock", "CoalOreBlock", "IronOreBlock", "IronOreBlock",
        "SulphurOreBlock", "SaltpeterOreBlock", "CopperOreBlock", "GermaniumOreBlock",
    };

    public static StructureData Build(int seed)
    {
        var random = new Random(seed);
        var b = new StructureBuilder("abandoned_mine");

        int depth = 12 + random.Next(6);           // shaft depth
        int galleryY = -depth;

        // Headframe: four log legs, plank platform and pitched cap.
        for (int y = 1; y <= 6; y++)
        {
            b.Set(-2, y, -2, "OakWoodBlock");
            b.Set(-2, y, 2, "OakWoodBlock");
            b.Set(2, y, -2, "OakWoodBlock");
            b.Set(2, y, 2, "OakWoodBlock");
        }
        b.Fill(-2, 7, -2, 2, 7, 2, "PlanksBlock");
        b.Fill(-1, 8, -1, 1, 8, 1, "PlanksBlock");
        // Cross bracing so it reads as a derrick from a distance.
        for (int y = 2; y <= 5; y++)
        {
            b.Set(0, y, -2, "WoodenFenceBlock");
            b.Set(0, y, 2, "WoodenFenceBlock");
        }
        b.Set(0, 9, 0, "TorchBlock", 4);
        b.Ambush(0, 1, 4);   // hyenas prowling the pit head

        // Shaft: 2x2, carved from the surface down, ladder on the north wall.
        for (int y = 0; y >= galleryY; y--)
        {
            b.Fill(0, y, 0, 1, y, 1, "AirBlock");
            b.Set(0, y, -1, "PlanksBlock");        // shaft lining on ladder side
            b.Set(0, y, 0, "WoodenLadderBlock", 2);
        }
        b.Fill(-1, galleryY - 1, -1, 2, galleryY - 1, 2, "PlanksBlock"); // shaft floor

        // Two galleries leaving the shaft bottom along +X and +Z.
        CarveGallery(b, random, galleryY, alongX: true, length: 9 + random.Next(4));
        CarveGallery(b, random, galleryY, alongX: false, length: 9 + random.Next(4));

        return b.Build();
    }

    private static void CarveGallery(StructureBuilder b, Random random, int galleryY, bool alongX, int length)
    {
        int caveInAt = length - 3 - random.Next(3);
        for (int i = 2; i < 2 + length; i++)
        {
            for (int side = -1; side <= 1; side++)
            {
                for (int y = 0; y < 3; y++)
                {
                    int x = alongX ? i : side;
                    int z = alongX ? side : i;
                    if (i - 2 == caveInAt)
                    {
                        b.Set(x, galleryY + y, z, "GravelBlock");     // cave-in
                    }
                    else
                    {
                        b.Set(x, galleryY + y, z, "AirBlock");
                    }
                }
            }
            // Timber set every third block: two posts and a cap beam.
            if ((i - 2) % 3 == 0 && i - 2 != caveInAt)
            {
                int px0 = alongX ? i : -1;
                int pz0 = alongX ? -1 : i;
                int px1 = alongX ? i : 1;
                int pz1 = alongX ? 1 : i;
                b.Set(px0, galleryY, pz0, "OakWoodBlock");
                b.Set(px0, galleryY + 1, pz0, "OakWoodBlock");
                b.Set(px1, galleryY, pz1, "OakWoodBlock");
                b.Set(px1, galleryY + 1, pz1, "OakWoodBlock");
                for (int side = -1; side <= 1; side++)
                {
                    b.Set(alongX ? i : side, galleryY + 2, alongX ? side : i, "PlanksBlock");
                }
                if (random.Next(3) == 0)
                {
                    b.Set(px0, galleryY + 1, pz0 + (alongX ? 0 : 0), "TorchBlock", 4);
                }
            }
            // Ore pockets in the floor and outer walls.
            if (random.Next(3) == 0)
            {
                string ore = s_ores[random.Next(s_ores.Length)];
                int wallSide = random.Next(2) == 0 ? -2 : 2;
                b.Set(alongX ? i : wallSide, galleryY + random.Next(2), alongX ? wallSide : i, ore);
            }
            if (random.Next(4) == 0)
            {
                string ore = s_ores[random.Next(s_ores.Length)];
                b.Set(alongX ? i : 0, galleryY - 1, alongX ? 0 : i, ore);
            }
        }
        // Chest at the gallery end (before the collapse when applicable).
        int chestAt = Math.Max(3, Math.Min(2 + caveInAt - 1, 1 + length));
        b.Chest(alongX ? chestAt : -1, galleryY, alongX ? -1 : chestAt);
    }
}

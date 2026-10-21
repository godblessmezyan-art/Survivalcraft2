using SurvivalcraftRuins.Core;

namespace SurvivalcraftRuins.Builders;

/// <summary>
/// Painted-block helpers. Painted cube blocks (planks, marble, bricks, ...)
/// encode color as data = 1 | (colorIndex &lt;&lt; 1); world palette:
/// 0 white, 2 pink, 4 yellow, 10 purple, 11 blue, 13 green, 14 red.
/// </summary>
internal static class Paint
{
    public static int Data(int colorIndex)
    {
        return 1 | (colorIndex << 1);
    }
}

/// <summary>
/// Roadside merchant stall: red/white striped awning over a counter, colored
/// carpets, two well-stocked chests. Bright, common, friendly.
/// </summary>
public static class MarketStallBuilder
{
    public static StructureData Build(int seed)
    {
        var random = new Random(seed);
        var b = new StructureBuilder("market_stall");
        int accent = new[] { 14, 11, 13, 10 }[random.Next(4)];   // red/blue/green/purple

        // Corner posts and awning: accent/white striped painted planks.
        for (int y = 1; y <= 3; y++)
        {
            b.Set(-3, y, -2, "OakWoodBlock");
            b.Set(-3, y, 2, "OakWoodBlock");
            b.Set(3, y, -2, "OakWoodBlock");
            b.Set(3, y, 2, "OakWoodBlock");
        }
        for (int x = -3; x <= 3; x++)
        {
            for (int z = -2; z <= 2; z++)
            {
                int color = (x + 10) % 2 == 0 ? accent : 0;
                b.Set(x, 4, z, "PlanksBlock", Paint.Data(color));
            }
        }

        // Counter along the front, goods shelf at the back.
        for (int x = -2; x <= 2; x++)
        {
            b.Set(x, 1, 2, "PlanksBlock");
        }
        b.Set(-2, 1, -2, "PlanksBlock");
        b.Set(2, 1, -2, "PlanksBlock");
        b.Chest(-2, 2, -2);
        b.Chest(2, 2, -2);

        // Plank floor with clear headroom, colored carpets in the middle.
        for (int x = -2; x <= 2; x++)
        {
            for (int z = -1; z <= 1; z++)
            {
                b.Set(x, 0, z, "PlanksBlock");
                b.Fill(x, 1, z, x, 3, z, "AirBlock");
            }
        }
        b.Set(0, 1, 0, "CarpetBlock", accent);
        b.Set(-1, 1, 0, "CarpetBlock", 4);
        b.Set(1, 1, 0, "CarpetBlock", accent);

        b.Set(3, 4, 3, "TorchBlock", 4);
        b.Set(-3, 4, 3, "TorchBlock", 4);
        return b.Build();
    }
}

/// <summary>
/// Watchtower: tall cobblestone tower with a red-trimmed platform and a
/// blue/white banner strip — a landmark meant to be seen across the plains.
/// </summary>
public static class WatchtowerBuilder
{
    public static StructureData Build(int seed)
    {
        var random = new Random(seed);
        var b = new StructureBuilder("watchtower");
        int height = 10 + random.Next(3);

        // Shaft: 5x5 cobblestone with a hollow core and interior ladder.
        for (int y = 0; y < height; y++)
        {
            for (int x = -2; x <= 2; x++)
            {
                for (int z = -2; z <= 2; z++)
                {
                    bool wall = Math.Abs(x) == 2 || Math.Abs(z) == 2;
                    b.Set(x, y, z, wall ? "CobblestoneBlock" : "AirBlock");
                }
            }
            b.Set(0, y, 1, "WoodenLadderBlock", 0);
        }
        // Ground-floor doorway.
        b.Set(0, 1, -2, "AirBlock");
        b.Set(0, 2, -2, "AirBlock");
        b.Chest(1, 1, 1);                                      // baseline chest downstairs

        // Banner strip: blue/white painted planks down one face.
        for (int y = 3; y < height - 1; y++)
        {
            b.Set(2, y, 0, "PlanksBlock", Paint.Data(y % 2 == 0 ? 11 : 0));
        }

        // Platform with red-trimmed crenellation.
        b.Fill(-3, height, -3, 3, height, 3, "PlanksBlock");
        b.Set(0, height, 1, "AirBlock");                        // ladder hatch
        for (int x = -3; x <= 3; x++)
        {
            for (int z = -3; z <= 3; z++)
            {
                bool edge = Math.Abs(x) == 3 || Math.Abs(z) == 3;
                if (edge && (x + z + 20) % 2 == 0)
                {
                    b.Set(x, height + 1, z, "PlanksBlock", Paint.Data(14));
                }
                else if (edge)
                {
                    b.Set(x, height + 1, z, "WoodenFenceBlock");
                }
            }
        }
        b.Chest(2, height + 1, 2);                              // watchman chest on top
        b.Set(-2, height + 1, -2, "TorchBlock", 4);
        b.Set(2, height + 1, -2, "TorchBlock", 4);
        b.Set(0, height + 1, 0, "CampfireBlock");               // signal fire
        return b.Build();
    }
}

/// <summary>
/// Wayside shrine: purple-and-white marble altar with flower beds and a
/// treasure generator hidden under the altar slab — mine the altar, get paid.
/// </summary>
public static class ShrineBuilder
{
    public static StructureData Build(int seed)
    {
        var random = new Random(seed);
        var b = new StructureBuilder("shrine");
        int accent = random.Next(2) == 0 ? 10 : 11;             // purple or blue

        // Platform: 7x7 checkerboard of white/accent marble.
        for (int x = -3; x <= 3; x++)
        {
            for (int z = -3; z <= 3; z++)
            {
                int color = (x + z + 20) % 2 == 0 ? 0 : accent;
                b.Set(x, 0, z, "MarbleBlock", Paint.Data(color));
            }
        }

        // Four corner columns with torches.
        foreach (var (cx, cz) in new[] { (-3, -3), (-3, 3), (3, -3), (3, 3) })
        {
            b.Set(cx, 1, cz, "MarbleBlock");
            b.Set(cx, 2, cz, "MarbleBlock");
            b.Set(cx, 3, cz, "MarbleBlock", Paint.Data(accent));
            b.Set(cx, 4, cz, "TorchBlock", 4);
        }

        // Altar: treasure generator sealed inside the pedestal.
        b.Set(0, 1, 0, "MarbleBlock", Paint.Data(accent));
        b.Set(0, 2, 0, "TreasureGeneratorBlock");
        b.Set(0, 3, 0, "MarbleBlock");

        // Flower beds and offering chest.
        b.Set(-2, 1, 0, "RedFlowerBlock");
        b.Set(2, 1, 0, "PurpleFlowerBlock");
        b.Set(0, 1, -2, "WhiteFlowerBlock");
        b.Set(0, 1, 2, "RedFlowerBlock");
        b.Chest(2, 1, 2);
        return b.Build();
    }
}

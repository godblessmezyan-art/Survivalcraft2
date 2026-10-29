using SurvivalcraftRuins.Core;

namespace SurvivalcraftRuins.Builders;

/// <summary>
/// Coastal lighthouse: banded stone tower with an interior ladder, keeper's
/// room at the base, and a glazed lamp room ringed with wicker lamps. Origin
/// (0,0,0) is ground level at the tower center.
/// </summary>
public static class LighthouseBuilder
{
    public static StructureData Build(int seed)
    {
        var random = new Random(seed);
        var b = new StructureBuilder("lighthouse");

        int height = 15 + random.Next(4);

        // Keeper's room: squat cobblestone annex on the landward side.
        b.Box(2, 0, -3, 8, 4, 3, "CobblestoneBlock");
        b.Fill(3, 1, 0, 3, 2, 0, "AirBlock");     // doorway toward the tower
        b.Set(8, 1, 0, "AirBlock");               // outer door
        b.Set(8, 2, 0, "AirBlock");
        b.Set(5, 3, -3, "WindowBlock");
        b.Set(5, 3, 3, "WindowBlock");
        b.Chest(4, 1, -2);                        // keeper supplies
        b.Chest(7, 1, 2);                         // baseline oddments
        b.Set(6, 1, -2, "CraftingTableBlock");
        b.Set(4, 1, 2, "TorchBlock", 4);

        // Tower shaft: radius-3 cylinder, marble band every fourth course.
        for (int y = 0; y < height; y++)
        {
            string wall = y % 4 == 3 ? "MarbleBlock" : "StoneBrickBlock";
            for (int x = -3; x <= 3; x++)
            {
                for (int z = -3; z <= 3; z++)
                {
                    int rr = x * x + z * z;
                    if (rr > 10)
                    {
                        continue;
                    }
                    if (rr >= 6)
                    {
                        b.Set(x, y, z, wall);
                    }
                    else
                    {
                        b.Set(x, y, z, "AirBlock");
                    }
                }
            }
            b.Set(0, y, 1, "WoodenLadderBlock", 0);
        }
        // Tower door facing the keeper's room.
        b.Fill(2, 1, 0, 3, 2, 0, "AirBlock");
        // A few slit windows.
        for (int y = 5; y < height - 2; y += 4)
        {
            b.Set(-3, y, 0, "WindowBlock");
            b.Set(3, y, 0, "GlassBlock");
        }

        // Gallery deck with railing.
        b.Fill(-4, height, -4, 4, height, 4, "PlanksBlock");
        for (int x = -4; x <= 4; x++)
        {
            for (int z = -4; z <= 4; z++)
            {
                bool edge = Math.Abs(x) == 4 || Math.Abs(z) == 4;
                if (edge && x * x + z * z <= 32)
                {
                    b.Set(x, height + 1, z, "IronFenceBlock");
                }
            }
        }
        b.Set(0, height, 1, "AirBlock");           // ladder exit through the deck

        // Lamp room: glass cage with a wicker-lamp cluster.
        b.Box(-2, height + 1, -2, 2, height + 4, 2, "GlassBlock");
        b.Set(0, height + 1, 0, "MarbleBlock");
        b.Set(0, height + 2, 0, "WickerLampBlock");
        b.Set(1, height + 2, 0, "WickerLampBlock");
        b.Set(-1, height + 2, 0, "WickerLampBlock");
        b.Set(0, height + 2, 1, "WickerLampBlock");
        b.Set(0, height + 2, -1, "WickerLampBlock");
        b.Fill(-2, height + 5, -2, 2, height + 5, 2, "MarbleBlock");   // cap

        return b.Build();
    }
}

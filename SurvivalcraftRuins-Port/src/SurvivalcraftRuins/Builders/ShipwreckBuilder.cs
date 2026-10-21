using SurvivalcraftRuins.Core;

namespace SurvivalcraftRuins.Builders;

/// <summary>
/// Beached/sunken sailing ship, WDA undead-pirate-ship layout reduced to SC's
/// palette: plank hull with air-pocket hold, stern cabin, broken mast, and a
/// breach in one side. Origin (0,0,0) is the keel center on the seabed.
/// </summary>
public static class ShipwreckBuilder
{
    public static StructureData Build(int seed)
    {
        var random = new Random(seed);
        var b = new StructureBuilder("shipwreck");

        int length = 15 + random.Next(3) * 2;       // 15..19 along X
        int halfBeam = 3;                           // beam 7
        int hullTop = 4;                            // deck height above keel
        int half = length / 2;

        for (int x = 0; x < length; x++)
        {
            int taper = Math.Min(x, length - 1 - x);
            int hw = Math.Min(halfBeam, taper == 0 ? 1 : (taper == 1 ? 2 : halfBeam));
            for (int z = -hw; z <= hw; z++)
            {
                for (int y = 0; y <= hullTop; y++)
                {
                    bool shell = y == 0 || Math.Abs(z) == hw || x == 0 || x == length - 1;
                    if (y == hullTop)
                    {
                        b.Set(x - half, y, z, "PlanksBlock");   // deck
                    }
                    else if (shell)
                    {
                        b.Set(x - half, y, z, "PlanksBlock");
                    }
                    else
                    {
                        b.Set(x - half, y, z, "AirBlock");      // hold stays dry-ish
                    }
                }
                // Bulwark railing along deck edges.
                if (Math.Abs(z) == hw && x > 0 && x < length - 1)
                {
                    b.Set(x - half, hullTop + 1, z, "WoodenFenceBlock");
                }
            }
        }

        // Keel line for silhouette.
        for (int x = 1; x < length - 1; x++)
        {
            b.Set(x - half, 0, 0, "OakWoodBlock", 1);
        }

        // Hull breach on a random side amidships — the way in for divers.
        int breachX = half - 3 + random.Next(5);
        int breachSide = random.Next(2) == 0 ? -halfBeam : halfBeam;
        for (int dx = 0; dx < 2; dx++)
        {
            for (int y = 1; y <= 2; y++)
            {
                b.Set(breachX - half + dx, y, breachSide, "AirBlock");
            }
        }

        // Deck hatch into the hold.
        b.Set(1, hullTop, 0, "AirBlock");
        b.Set(1, hullTop - 1, 0, "WoodenLadderBlock", 0);

        // Broken mast: a stump plus a fallen section across the deck.
        int mastX = -half + length / 3;
        int stumpHeight = 2 + random.Next(2);
        for (int y = 1; y <= stumpHeight; y++)
        {
            b.Set(mastX, hullTop + y, 0, "OakWoodBlock");
        }
        int fallenLength = 4 + random.Next(2);
        for (int i = 1; i <= fallenLength; i++)
        {
            b.Set(mastX + i, hullTop + 1, Math.Min(halfBeam - 1, i / 2), "OakWoodBlock", 1);
        }

        // Stern cabin with the captain's goods.
        int cabinStart = length - 7 - half;
        int cabinEnd = length - 2 - half;
        b.Box(cabinStart, hullTop, -2, cabinEnd, hullTop + 3, 2, "PlanksBlock");
        b.Set(cabinStart, hullTop + 1, 0, "AirBlock");                  // doorway
        b.Chest(cabinEnd - 1, hullTop + 1, -1);                         // ship_treasure
        b.Set(cabinEnd - 1, hullTop + 1, 1, "TreasureGeneratorBlock");  // vanilla treasure trickle

        // Hold cargo: two supply chests, powder kegs, treasure generator.
        b.Chest(-half + 3, 1, -1);
        b.Chest(-half + 5, 1, 1);
        b.Set(-half + 7, 1, 0, "SmallGunpowderKegBlock");
        b.Set(-half + 8, 1, -1, "SmallGunpowderKegBlock");
        b.Set(-half + 9, 1, 1, "TreasureGeneratorBlock");

        return b.Build();
    }
}

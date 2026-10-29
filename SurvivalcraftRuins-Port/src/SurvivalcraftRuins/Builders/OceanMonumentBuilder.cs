using SurvivalcraftRuins.Core;

namespace SurvivalcraftRuins.Builders;

/// <summary>
/// Ocean monument, laid out from Minecraft 1.16.5's OceanMonumentPieces: a
/// 58x58 hall standing on a 7x7 field of pillars, two side wings, a roof
/// penthouse, and a 5x3x5 lattice of 8x4x8 rooms. The vault at its heart is the
/// original's gold room.
///
/// Two deliberate departures. SC has no prismarine, so the palette is
/// marble/granite/basalt. And room interiors are left unwritten rather than
/// filled: the sea has already flooded those cells, which both matches the
/// original's water-filled halls and saves tens of thousands of cells. Air is
/// written only where a diver is meant to find a breath.
/// Origin (0,0,0) is the north-west corner of the base slab.
/// </summary>
public static class OceanMonumentBuilder
{
    public const int Span = 58;
    public const int RoofY = 16;

    /// <summary>Top of the penthouse, the highest block in the structure.</summary>
    public const int PenthouseTopY = 17;

    /// <summary>How far the pillars reach below the base before the bed swallows them.</summary>
    public const int PillarDepth = 26;

    private const string Light = "MarbleBlock";      // prismarine bricks
    private const string Gray = "GraniteBlock";      // prismarine
    private const string Dark = "BasaltBlock";       // dark prismarine
    private const string Lamp = "WickerLampBlock";   // sea lantern
    private const string Water = "WaterBlock";
    private const string Air = "AirBlock";

    private const int RoomsX = 5;
    private const int RoomsY = 3;
    private const int RoomsZ = 5;

    /// <summary>
    /// Exact extent, declared so placement can test a site before paying for a
    /// build. CoreTests keeps this honest against what Build actually produces.
    /// </summary>
    public static readonly StructureBounds Bounds =
        new(0, -PillarDepth, 0, Span - 1, PenthouseTopY, Span - 1);

    public static StructureData Build(int seed)
    {
        var random = new Random(seed);
        var b = new StructureBuilder("ocean_monument");

        BuildPillars(b);
        BuildShell(b);
        BuildWing(b, 0);
        BuildWing(b, 33);
        BuildPenthouse(b);
        BuildRooms(b, random);

        return b.Build();
    }

    /// <summary>
    /// The 7x7 field of 4x4 pillars on a 9 block pitch, each driven far enough
    /// down to meet any sea bed the placement rules allow. This is what lets the
    /// monument stand over uneven ground with no foundation pass.
    /// </summary>
    private static void BuildPillars(StructureBuilder b)
    {
        for (int gx = 0; gx < 7; gx++)
        {
            for (int gz = 0; gz < 7; gz++)
            {
                bool onEdge = gx is 0 or 6 || gz is 0 or 6;
                if (!onEdge || (gz == 0 && gx == 3))
                {
                    continue;   // the original leaves the entrance bay clear
                }
                b.Fill(gx * 9, -PillarDepth, gz * 9, gx * 9 + 3, 0, gz * 9 + 3, Light);
            }
        }
    }

    /// <summary>Base slab, curtain wall, stepped skirt and roof.</summary>
    private static void BuildShell(StructureBuilder b)
    {
        b.Fill(0, 0, 0, Span - 1, 0, Span - 1, Gray);

        for (int i = 0; i < 4; i++)
        {
            b.Fill(i, i + 1, i, Span - 1 - i, i + 1, i, Light);
            b.Fill(i, i + 1, Span - 1 - i, Span - 1 - i, i + 1, Span - 1 - i, Light);
            b.Fill(i, i + 1, i, i, i + 1, Span - 1 - i, Light);
            b.Fill(Span - 1 - i, i + 1, i, Span - 1 - i, i + 1, Span - 1 - i, Light);
        }

        b.Fill(0, 5, 0, Span - 1, RoofY - 1, 0, Gray);
        b.Fill(0, 5, Span - 1, Span - 1, RoofY - 1, Span - 1, Gray);
        b.Fill(0, 5, 0, 0, RoofY - 1, Span - 1, Gray);
        b.Fill(Span - 1, 5, 0, Span - 1, RoofY - 1, Span - 1, Gray);
        b.Fill(0, RoofY, 0, Span - 1, RoofY, Span - 1, Light);

        // Grand entrance on the north face: flooded, as the whole hall is.
        b.Fill(26, 1, 0, 31, 6, 2, Water);
        b.Set(25, 6, 0, Lamp);
        b.Set(32, 6, 0, Lamp);
    }

    /// <summary>One of the two side wings, at x offset 0 or 33.</summary>
    private static void BuildWing(StructureBuilder b, int x0)
    {
        b.Fill(x0 + 1, 1, 1, x0 + 23, 1, 21, Gray);
        b.Fill(x0 + 1, 8, 1, x0 + 23, 8, 21, Light);
        for (int i = 0; i < 4; i++)
        {
            b.Fill(x0 + 1 + i, i + 2, 1 + i, x0 + 1 + i, i + 2, 21 - i, Light);
            b.Fill(x0 + 23 - i, i + 2, 1 + i, x0 + 23 - i, i + 2, 21 - i, Light);
        }
        for (int x = x0 + 5; x <= x0 + 19; x += 7)
        {
            for (int z = 5; z <= 17; z += 6)
            {
                b.Fill(x, 2, z, x, 7, z, Dark);
                b.Set(x, 5, z, Lamp);
            }
        }
        // A pocket of trapped air under the wing roof: the one place in the
        // wings a diver can refill, which is what makes the swim survivable.
        b.Fill(x0 + 10, 7, 10, x0 + 14, 7, 12, Air);

        // Wing cache, kept clear of the room lattice (which starts at z=9 and
        // is written afterwards) and of the colonnade ranks.
        b.Chest(x0 + 12, 2, 4);
        b.Ambush(x0 + 12, 2, 6);
    }

    /// <summary>The roof penthouse, the top approach in the original.</summary>
    private static void BuildPenthouse(StructureBuilder b)
    {
        b.Fill(22, 13, 22, 35, 13, 35, Gray);
        b.Fill(22, 17, 22, 35, 17, 35, Light);
        b.Fill(22, 14, 22, 35, 16, 22, Light);
        b.Fill(22, 14, 35, 35, 16, 35, Light);
        b.Fill(22, 14, 22, 22, 16, 35, Light);
        b.Fill(35, 14, 22, 35, 16, 35, Light);
        foreach (var (x, z) in new[] { (24, 24), (33, 24), (24, 33), (33, 33) })
        {
            b.Set(x, 15, z, Lamp);
        }
    }

    /// <summary>
    /// The room lattice. Levels 0 and 1 hold a 5x4 field of rooms, level 2 a
    /// 3x2 field, exactly as the original's grid does. The vault claims a 2x2x2
    /// block of slots on the ground floor; the rooms it swallows are skipped.
    /// </summary>
    private static void BuildRooms(StructureBuilder b, Random random)
    {
        var present = new bool[RoomsX * RoomsY * RoomsZ];
        for (int y = 0; y < RoomsY; y++)
        {
            int fromX = y == 2 ? 1 : 0;
            int toX = y == 2 ? 3 : RoomsX - 1;
            int depth = y == 2 ? 2 : 4;
            for (int x = fromX; x <= toX; x++)
            {
                for (int z = 0; z < depth; z++)
                {
                    present[RoomIndex(x, y, z)] = true;
                }
            }
        }

        // Vault: ground floor, third rank in, mirroring the original's roll.
        int coreX = random.Next(4);
        const int coreY = 0;
        const int coreZ = 2;
        var claimed = new bool[present.Length];
        for (int dx = 0; dx <= 1; dx++)
        {
            for (int dy = 0; dy <= 1; dy++)
            {
                for (int dz = 0; dz <= 1; dz++)
                {
                    claimed[RoomIndex(coreX + dx, coreY + dy, coreZ + dz)] = true;
                }
            }
        }

        for (int y = 0; y < RoomsY; y++)
        {
            for (int z = 0; z < RoomsZ; z++)
            {
                for (int x = 0; x < RoomsX; x++)
                {
                    int index = RoomIndex(x, y, z);
                    if (!present[index] || claimed[index])
                    {
                        continue;
                    }
                    BuildRoom(b, random, x, y, z, present, claimed);
                }
            }
        }
        BuildVault(b, 9 + coreX * 8, 1 + coreY * 4, 9 + coreZ * 8);
    }

    private static int RoomIndex(int x, int y, int z) => y * 25 + z * 5 + x;

    private static bool Neighbour(bool[] present, bool[] claimed, int x, int y, int z)
    {
        if (x < 0 || x >= RoomsX || y < 0 || y >= RoomsY || z < 0 || z >= RoomsZ)
        {
            return false;
        }
        int index = RoomIndex(x, y, z);
        return present[index] || claimed[index];
    }

    /// <summary>
    /// One 8x4x8 cell: six walls and the openings on to its neighbours. Every
    /// room opens toward the entrance side, so no room can end up sealed however
    /// the extra doorways roll.
    /// </summary>
    private static void BuildRoom(StructureBuilder b, Random random, int x, int y, int z,
        bool[] present, bool[] claimed)
    {
        int rx = 9 + x * 8;
        int ry = 1 + y * 4;
        int rz = 9 + z * 8;
        Shell(b, rx, ry, rz, rx + 7, ry + 3, rz + 7, Light);
        b.Set(rx + 3, ry + 3, rz + 3, random.Next(4) == 0 ? Lamp : Dark);

        Doorway(b, rx + 3, ry + 1, rz, 2, 0);
        if (Neighbour(present, claimed, x + 1, y, z))
        {
            Doorway(b, rx + 7, ry + 1, rz + 3, 0, 2);
        }
        if (Neighbour(present, claimed, x, y, z + 1) && random.Next(3) != 0)
        {
            Doorway(b, rx + 3, ry + 1, rz + 7, 2, 0);
        }
        if (Neighbour(present, claimed, x, y + 1, z) && random.Next(2) == 0)
        {
            b.Fill(rx + 3, ry + 3, rz + 3, rx + 4, ry + 3, rz + 4, Water);
        }
    }

    /// <summary>
    /// Writes the six faces of a box and leaves the inside untouched, so the
    /// sea stays where it already is.
    /// </summary>
    private static void Shell(StructureBuilder b, int x0, int y0, int z0, int x1, int y1, int z1, string block)
    {
        b.Fill(x0, y0, z0, x1, y0, z1, block);
        b.Fill(x0, y1, z0, x1, y1, z1, block);
        b.Fill(x0, y0, z0, x0, y1, z1, block);
        b.Fill(x1, y0, z0, x1, y1, z1, block);
        b.Fill(x0, y0, z0, x1, y1, z0, block);
        b.Fill(x0, y0, z1, x1, y1, z1, block);
    }

    /// <summary>
    /// Cuts a flooded opening two blocks high, running <paramref name="spanX"/>
    /// or <paramref name="spanZ"/> blocks wide.
    /// </summary>
    private static void Doorway(StructureBuilder b, int x, int y, int z, int spanX, int spanZ)
    {
        b.Fill(x, y, z, x + Math.Max(spanX - 1, 0), y + 1, z + Math.Max(spanZ - 1, 0), Water);
    }

    /// <summary>
    /// The vault: a dark shell around the prize, lit at the corners. The
    /// original stacks eight gold blocks here; SC has no gold block, so the
    /// centre is a treasure generator flanked by two chests.
    /// </summary>
    private static void BuildVault(StructureBuilder b, int rx, int ry, int rz)
    {
        Shell(b, rx, ry, rz, rx + 15, ry + 7, rz + 15, Light);
        Shell(b, rx + 6, ry + 3, rz + 6, rx + 9, ry + 6, rz + 9, Dark);
        b.Fill(rx + 7, ry + 4, rz + 7, rx + 8, ry + 5, rz + 8, "TreasureGeneratorBlock");
        b.Chest(rx + 6, ry + 4, rz + 7);
        b.Chest(rx + 9, ry + 4, rz + 8);
        foreach (var (dx, dz) in new[] { (5, 5), (10, 5), (5, 10), (10, 10) })
        {
            b.Fill(rx + dx, ry + 2, rz + dz, rx + dx, ry + 6, rz + dz, Light);
            b.Set(rx + dx, ry + 4, rz + dz, Lamp);
        }
        // Way in from the hall, and the guards that meet you at it.
        Doorway(b, rx + 7, ry + 1, rz, 2, 0);
        Doorway(b, rx + 7, ry + 4, rz + 6, 2, 0);
        b.Ambush(rx + 7, ry + 2, rz + 3);
        b.Ambush(rx + 8, ry + 2, rz + 12);
    }
}

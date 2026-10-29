using SurvivalcraftRuins.Core;

namespace SurvivalcraftRuins.Builders;

/// <summary>
/// Jungle temple, laid out from Minecraft 1.16.5's JunglePyramidPiece: a
/// 12x15 three-tier ziggurat over a basement holding two dart traps, a main
/// chest, and a three-lever puzzle that opens the hidden one. Walls are mixed
/// cobble and mossy cobble at the original's 40/60 ratio.
/// SC has no tripwire, so the traps trigger from pressure plates in the same
/// corridors, wired to dispensers standing where the originals do.
/// Origin (0,0,0) is the north-west corner at ground level.
/// </summary>
public static class JungleTempleBuilder
{
    public const int Width = 12;
    public const int Depth = 15;

    private const string Cobble = "CobblestoneBlock";
    private const string Stairs = "CobblestoneStairsBlock";
    private const string Ivy = "IvyBlock";
    private const string Air = "AirBlock";
    private const string Wire = "WireBlock";
    private const string Plate = "PressurePlateBlock";
    private const string Switch = "SwitchBlock";
    private const string Dispenser = "DispenserBlock";
    private const string Piston = "PistonBlock";
    private const string Brick = "StoneBrickBlock";

    private const int MossPaint = 1 | (13 << 1);   // green tint stands in for moss
    private const int WireOnFloor = 1 << 4;        // wire clings to the block below
    private const int PlateOnFloor = 4 << 1;       // pressure plate mounted face up

    // Face indices follow CellFace: 0=+Z, 1=+X, 2=-Z, 3=-X, 4=+Y, 5=-Y.
    /// <summary>Dispenser data: aimed at <paramref name="face"/> and set to shoot.</summary>
    private static int Muzzle(int face) => (face & 7) | 8;

    /// <summary>Wire clinging to the neighbouring block in the given direction.</summary>
    private static int WireOn(params int[] faces)
    {
        int mask = 0;
        foreach (int face in faces)
        {
            mask |= 1 << face;
        }
        return mask;
    }

    /// <summary>
    /// Sticky (pulling) piston aimed at <paramref name="face"/>. The extension
    /// limit matters: the door piston must push its plug exactly one cell, into
    /// the pocket behind it, and no further.
    /// </summary>
    private static int StickyPiston(int face, int maxExtension) =>
        (1 << 1) | ((face & 7) << 3) | ((maxExtension & 7) << 6);

    public static StructureData Build(int seed)
    {
        var random = new Random(seed);
        var b = new StructureBuilder("jungle_temple");

        // Plinth and the three stacked tiers.
        MossFill(b, random, 0, -4, 0, Width - 1, 0, Depth - 1);
        MossFill(b, random, 2, 1, 2, 9, 2, 2);
        MossFill(b, random, 2, 1, 12, 9, 2, 12);
        MossFill(b, random, 2, 1, 3, 2, 2, 11);
        MossFill(b, random, 9, 1, 3, 9, 2, 11);
        MossFill(b, random, 1, 3, 1, 10, 6, 1);
        MossFill(b, random, 1, 3, 13, 10, 6, 13);
        MossFill(b, random, 1, 3, 2, 1, 6, 12);
        MossFill(b, random, 10, 3, 2, 10, 6, 12);
        MossFill(b, random, 2, 3, 2, 9, 3, 12);
        MossFill(b, random, 2, 6, 2, 9, 6, 12);
        MossFill(b, random, 3, 7, 3, 8, 7, 11);
        MossFill(b, random, 4, 8, 4, 7, 8, 10);

        // Rooms and doorways carved back out.
        b.Fill(3, 1, 3, 8, 2, 11, Air);
        b.Fill(4, 3, 6, 7, 3, 9, Air);
        b.Fill(2, 4, 2, 9, 5, 12, Air);
        b.Fill(4, 6, 5, 7, 6, 9, Air);
        b.Fill(5, 7, 6, 6, 7, 8, Air);
        b.Fill(5, 1, 2, 6, 2, 2, Air);
        b.Fill(5, 2, 12, 6, 2, 12, Air);
        b.Fill(5, 5, 1, 6, 5, 1, Air);
        b.Fill(5, 5, 13, 6, 5, 13, Air);
        b.Set(1, 5, 5, Air);
        b.Set(10, 5, 5, Air);
        b.Set(1, 5, 9, Air);
        b.Set(10, 5, 9, Air);

        // Merlons along the upper walls.
        for (int z = 0; z <= 14; z += 14)
        {
            foreach (int x in new[] { 2, 4, 7, 9 })
            {
                MossFill(b, random, x, 4, z, x, 5, z);
            }
        }
        MossFill(b, random, 5, 6, 0, 6, 6, 0);
        for (int x = 0; x <= 11; x += 11)
        {
            for (int z = 2; z <= 12; z += 2)
            {
                MossFill(b, random, x, 4, z, x, 5, z);
            }
            MossFill(b, random, x, 6, 5, x, 6, 5);
            MossFill(b, random, x, 6, 9, x, 6, 9);
        }
        foreach (var (x, z) in new[] { (2, 2), (9, 2), (2, 12), (9, 12) })
        {
            MossFill(b, random, x, 7, z, x, 9, z);
        }
        foreach (var (x, z) in new[] { (4, 4), (7, 4), (4, 10), (7, 10) })
        {
            MossFill(b, random, x, 9, z, x, 9, z);
        }
        MossFill(b, random, 5, 9, 7, 6, 9, 7);
        b.Set(5, 9, 6, Stairs, 2);
        b.Set(6, 9, 6, Stairs, 2);
        b.Set(5, 9, 8, Stairs, 0);
        b.Set(6, 9, 8, Stairs, 0);
        for (int x = 4; x <= 7; x++)
        {
            b.Set(x, 0, 0, Stairs, 2);
        }
        foreach (int x in new[] { 4, 7 })
        {
            b.Set(x, 1, 8, Stairs, 2);
            b.Set(x, 2, 9, Stairs, 2);
            b.Set(x, 3, 10, Stairs, 2);
            MossFill(b, random, x, 1, 9, x, 1, 9);
        }
        MossFill(b, random, 4, 1, 10, 7, 2, 10);
        MossFill(b, random, 5, 4, 5, 6, 4, 5);
        b.Set(4, 4, 5, Stairs, 1);
        b.Set(7, 4, 5, Stairs, 3);

        // The staircase down into the basement.
        for (int i = 0; i < 4; i++)
        {
            b.Set(5, -i, 6 + i, Stairs, 0);
            b.Set(6, -i, 6 + i, Stairs, 0);
            b.Fill(5, -i, 7 + i, 6, -i, 9 + i, Air);
        }

        // Basement corridors.
        b.Fill(1, -3, 12, 10, -1, 13, Air);
        b.Fill(1, -3, 1, 3, -1, 13, Air);
        b.Fill(1, -3, 1, 9, -1, 5, Air);
        for (int z = 1; z <= 13; z += 2)
        {
            MossFill(b, random, 1, -3, z, 1, -2, z);
        }
        for (int z = 2; z <= 12; z += 2)
        {
            MossFill(b, random, 1, -1, z, 3, -1, z);
        }
        MossFill(b, random, 2, -2, 1, 5, -2, 1);
        MossFill(b, random, 7, -2, 1, 9, -2, 1);
        MossFill(b, random, 6, -3, 1, 6, -3, 1);
        MossFill(b, random, 6, -1, 1, 6, -1, 1);

        BuildWestTrap(b);
        BuildEastTrap(b);
        BuildLeverPuzzle(b, random);

        // Guard posts live in the upper hall, clear of both trap corridors: a
        // jaguar pacing over a pressure plate spends the trap before the player
        // ever reaches it.
        b.Ambush(4, 1, 4);
        b.Ambush(6, 1, 6);
        b.Ambush(7, 1, 10);
        return b.Build();
    }

    /// <summary>
    /// West corridor trap. The original runs a tripwire across the passage into
    /// a redstone line feeding a dispenser; here two floor plates do the
    /// triggering and the wire runs back along the corridor floor to a
    /// dispenser set into the end wall at knee height.
    /// Every cell in this circuit is checked by TrapTests against a port of the
    /// game's wiring rules — the 0.5 version laid the wire two cells clear of
    /// the plates and nothing was ever connected.
    /// </summary>
    private static void BuildWestTrap(StructureBuilder b)
    {
        // Plates sit where the original strings its tripwire.
        b.Set(2, -3, 8, Plate, PlateOnFloor);
        b.Set(3, -3, 8, Plate, PlateOnFloor);
        // Floor wire from under the plates down to the dispenser.
        for (int z = 1; z <= 7; z++)
        {
            b.Set(2, -3, z, Wire, WireOnFloor);
        }
        b.Set(3, -3, 7, Wire, WireOnFloor);   // picks up the second plate
        // Set into the south wall, firing back up the corridor.
        b.Set(2, -3, 0, Dispenser, Muzzle(0));
        b.Set(3, -2, 2, Ivy);
    }

    /// <summary>East corridor trap, mirroring the west one onto the main chest.</summary>
    private static void BuildEastTrap(StructureBuilder b)
    {
        for (int z = 3; z <= 5; z++)
        {
            b.Set(7, -3, z, Plate, PlateOnFloor);
            b.Set(8, -3, z, Wire, WireOnFloor);
        }
        b.Set(9, -3, 4, Wire, WireOnFloor);
        // Set into the east wall, firing across the corridor at the plates.
        b.Set(10, -3, 4, Dispenser, Muzzle(3));
        b.Set(8, -1, 3, Ivy);
        b.Set(8, -2, 3, Ivy);
        b.Chest(8, -3, 2);
    }

    /// <summary>
    /// The lever puzzle, at the end of the back corridor. Three levers face a
    /// stone-brick door panel across the passage:
    ///   - one is live and drives the sticky pistons that slide the panel
    ///     sideways into the wall, opening the niche with the second chest;
    ///   - one is trapped and fires a bolt from the wall behind it;
    ///   - one does nothing.
    ///
    /// The panel is framed in stone brick against the cobble wall so it reads
    /// as a door rather than as scenery, and the door reveals its own mechanism:
    /// the piston pockets either side of the panel are visible from the corridor.
    /// The niche is one cell wide on purpose — the panel needs a pocket to be
    /// pushed into, and a wider pocket would open a way around the door.
    /// The panel is ordinary stone brick, so a player who cannot make the
    /// pistons cooperate can always dig it out.
    /// </summary>
    private static void BuildLeverPuzzle(StructureBuilder b, Random random)
    {
        // The niche and its chest.
        b.Fill(9, -3, 8, 9, -1, 10, Air);
        b.Chest(9, -3, 9);

        // Door: piston, arm space, panel, and the pocket the panel slides into,
        // all buried in the z=11 wall.
        for (int y = -3; y <= -2; y++)
        {
            b.Set(7, y, 11, Piston, StickyPiston(1, 1));
            b.Set(8, y, 11, Air);
            b.Set(9, y, 11, Brick);
            b.Set(10, y, 11, Air);
        }
        // Lintel over the doorway, so the panel reads as a door from across
        // the corridor instead of as another patch of wall.
        for (int x = 8; x <= 10; x++)
        {
            b.Set(x, -1, 11, Brick);
        }

        // Levers on the far wall of the back corridor, mounted on z=14.
        int live = 8 + random.Next(3);
        int trapped = 8 + ((live - 8 + 1 + random.Next(2)) % 3);
        for (int x = 8; x <= 10; x++)
        {
            b.Set(x, -2, 13, Switch, 2 << 1);
        }

        // Live lever -> wall wire -> floor wire -> up the piston faces.
        b.Set(live, -3, 13, Wire, WireOn(2, 4));
        for (int x = 7; x <= live; x++)
        {
            b.Set(x, -3, 12, Wire, x == 7 ? WireOn(0, 4) : WireOnFloor);
        }
        b.Set(7, -2, 12, Wire, WireOn(0));

        // Trapped lever -> wire up the same wall -> dispenser set into it.
        // Routed along the top of the wall so it can never touch the floor run
        // that drives the door, however the two levers land next to each other.
        b.Set(trapped, -1, 13, Wire, WireOn(2));
        b.Set(trapped, -1, 14, Dispenser, Muzzle(2));
    }

    /// <summary>
    /// Fills a box with cobble, tinting roughly six cells in ten green: the
    /// original picks mossy cobblestone with probability 0.6 per block.
    /// </summary>
    private static void MossFill(StructureBuilder b, Random random, int x0, int y0, int z0, int x1, int y1, int z1)
    {
        for (int x = Math.Min(x0, x1); x <= Math.Max(x0, x1); x++)
        for (int y = Math.Min(y0, y1); y <= Math.Max(y0, y1); y++)
        for (int z = Math.Min(z0, z1); z <= Math.Max(z0, z1); z++)
        {
            b.Set(x, y, z, Cobble, random.NextDouble() < 0.4 ? 0 : MossPaint);
        }
    }
}

using SurvivalcraftRuins.Core;

namespace SurvivalcraftRuins.Builders;

/// <summary>
/// Desert pyramid, laid out from Minecraft 1.16.5's DesertPyramidPiece: a
/// 21x21 stepped pyramid with twin corner towers, a decorated entrance, and the
/// blue floor tile that marks the sealed chamber ten blocks below. Down there,
/// four chests ring a pressure plate wired to a bed of gunpowder kegs.
/// Origin (0,0,0) is the north-west corner at ground level.
/// </summary>
public static class DesertTempleBuilder
{
    public const int Width = 21;
    public const int Depth = 21;

    private const string Sand = "SandstoneBlock";
    private const string Stairs = "SandstoneStairsBlock";
    private const string Slab = "SandstoneSlabBlock";
    private const string Air = "AirBlock";

    // SC sandstone is paintable, which stands in for MC's cut/chiseled variants
    // and the terracotta mosaic. Palette indices come from WorldPalette.
    private const int ColorPale = 0;    // chiselled accent
    private const int ColorOrange = 6;  // orange terracotta
    private const int ColorBlue = 11;   // blue terracotta

    private static int Painted(int color) => 1 | (color << 1);

    public static StructureData Build(int seed)
    {
        var b = new StructureBuilder("desert_temple");
        int paleData = Painted(ColorPale);
        int orangeData = Painted(ColorOrange);

        // Solid plinth, then nine stepped courses hollowed as they rise.
        b.Fill(0, -4, 0, Width - 1, 0, Depth - 1, Sand);
        for (int i = 1; i <= 9; i++)
        {
            b.Fill(i, i, i, Width - 1 - i, i, Depth - 1 - i, Sand);
            b.Fill(i + 1, i, i + 1, Width - 2 - i, i, Depth - 2 - i, Air);
        }

        // Twin corner towers with a capped roof and four stair eaves.
        BuildTower(b, 0, paleData);
        BuildTower(b, Width - 5, paleData);

        // Entrance hall and its frame.
        b.Box(8, 0, 0, 12, 4, 4, Sand);
        b.Fill(9, 1, 0, 11, 3, 4, Air);
        b.Set(9, 1, 1, Sand, paleData);
        b.Set(9, 2, 1, Sand, paleData);
        b.Set(9, 3, 1, Sand, paleData);
        b.Set(10, 3, 1, Sand, paleData);
        b.Set(11, 3, 1, Sand, paleData);
        b.Set(11, 2, 1, Sand, paleData);
        b.Set(11, 1, 1, Sand, paleData);

        // Side corridors off the hall.
        b.Box(4, 1, 1, 8, 3, 3, Sand);
        b.Fill(4, 1, 2, 8, 2, 2, Air);
        b.Box(12, 1, 1, 16, 3, 3, Sand);
        b.Fill(12, 1, 2, 16, 2, 2, Air);

        // Inner chamber ceiling with its light well, and the four pillars.
        b.Fill(5, 4, 5, Width - 6, 4, Depth - 6, Sand);
        b.Fill(9, 4, 9, 11, 4, 11, Air);
        foreach (int px in new[] { 8, 12 })
        {
            foreach (int pz in new[] { 8, 12 })
            {
                b.Fill(px, 1, pz, px, 3, pz, Sand, paleData);
            }
        }

        // Buttressed side wings with the two upper alcoves.
        b.Fill(1, 1, 5, 4, 4, 11, Sand);
        b.Fill(Width - 5, 1, 5, Width - 2, 4, 11, Sand);
        b.Fill(6, 7, 9, 6, 7, 11, Sand);
        b.Fill(Width - 7, 7, 9, Width - 7, 7, 11, Sand);
        b.Fill(5, 5, 9, 5, 7, 11, Sand, paleData);
        b.Fill(Width - 6, 5, 9, Width - 6, 7, 11, Sand, paleData);
        b.Set(5, 5, 10, Air);
        b.Set(5, 6, 10, Air);
        b.Set(6, 6, 10, Air);
        b.Set(Width - 6, 5, 10, Air);
        b.Set(Width - 6, 6, 10, Air);
        b.Set(Width - 7, 6, 10, Air);

        // Stair niches flanking the hall.
        b.Fill(2, 4, 4, 2, 6, 4, Air);
        b.Fill(Width - 3, 4, 4, Width - 3, 6, 4, Air);
        b.Set(2, 4, 5, Stairs, 0);
        b.Set(2, 3, 4, Stairs, 0);
        b.Set(Width - 3, 4, 5, Stairs, 0);
        b.Set(Width - 3, 3, 4, Stairs, 0);

        b.Fill(1, 1, 3, 2, 2, 3, Sand);
        b.Fill(Width - 3, 1, 3, Width - 2, 2, 3, Sand);
        b.Set(1, 1, 2, Sand);
        b.Set(Width - 2, 1, 2, Sand);
        b.Set(1, 2, 2, Slab);
        b.Set(Width - 2, 2, 2, Slab);
        b.Set(2, 1, 2, Stairs, 3);
        b.Set(Width - 3, 1, 2, Stairs, 1);

        // Long colonnaded galleries down both flanks.
        b.Fill(4, 3, 5, 4, 3, 17, Sand);
        b.Fill(Width - 5, 3, 5, Width - 5, 3, 17, Sand);
        b.Fill(3, 1, 5, 4, 2, 16, Air);
        b.Fill(Width - 6, 1, 5, Width - 5, 2, 16, Air);
        for (int z = 5; z <= 17; z += 2)
        {
            b.Set(4, 1, z, Sand, paleData);
            b.Set(4, 2, z, Sand, orangeData);
            b.Set(Width - 5, 1, z, Sand, paleData);
            b.Set(Width - 5, 2, z, Sand, orangeData);
        }

        BuildFloorSigil(b, orangeData);
        BuildFacades(b, paleData, orangeData);
        BuildHiddenChamber(b, paleData);
        return b.Build();
    }

    private static void BuildTower(StructureBuilder b, int x0, int paleData)
    {
        b.Box(x0, 0, 0, x0 + 4, 9, 4, Sand);
        b.Fill(x0 + 1, 10, 1, x0 + 3, 10, 3, Sand, paleData);
        b.Set(x0 + 2, 10, 0, Stairs, 2);
        b.Set(x0 + 2, 10, 4, Stairs, 0);
        b.Set(x0, 10, 2, Stairs, 3);
        b.Set(x0 + 4, 10, 2, Stairs, 1);
    }

    /// <summary>
    /// The floor mosaic: a ring of orange tiles around a single blue one. In
    /// the original it is the only hint that anything lies underneath, so it is
    /// reproduced tile for tile.
    /// </summary>
    private static void BuildFloorSigil(StructureBuilder b, int orangeData)
    {
        int[,] ring =
        {
            { 10, 7 }, { 10, 8 }, { 9, 9 }, { 11, 9 }, { 8, 10 }, { 12, 10 },
            { 7, 10 }, { 13, 10 }, { 9, 11 }, { 11, 11 }, { 10, 12 }, { 10, 13 },
        };
        for (int i = 0; i < ring.GetLength(0); i++)
        {
            b.Set(ring[i, 0], 0, ring[i, 1], Sand, orangeData);
        }
        b.Set(10, 0, 10, Sand, Painted(ColorBlue));
    }

    /// <summary>
    /// Banded facades on the two side walls and the entrance front. The band
    /// pattern is the original's, with SC's paintable sandstone standing in for
    /// cut/chiselled sandstone (0) and orange terracotta (1).
    /// </summary>
    private static void BuildFacades(StructureBuilder b, int paleData, int orangeData)
    {
        int[,] bands =
        {
            { 0, 1, 0 },   // y = 2
            { 0, 1, 0 },   // y = 3
            { 1, 0, 1 },   // y = 4
            { 0, 1, 0 },   // y = 5
            { 1, 0, 1 },   // y = 6
            { 1, 1, 1 },   // y = 7
            { 0, 0, 0 },   // y = 8
        };
        int Tone(int flag) => flag == 0 ? paleData : orangeData;

        for (int x = 0; x <= Width - 1; x += Width - 1)
        {
            for (int y = 2; y <= 8; y++)
            {
                for (int z = 1; z <= 3; z++)
                {
                    b.Set(x, y, z, Sand, Tone(bands[y - 2, z - 1]));
                }
            }
        }
        for (int x = 2; x <= Width - 3; x += Width - 5)
        {
            for (int y = 2; y <= 8; y++)
            {
                for (int i = -1; i <= 1; i++)
                {
                    b.Set(x + i, y, 0, Sand, Tone(bands[y - 2, i + 1]));
                }
            }
        }
        b.Fill(8, 4, 0, 12, 6, 0, Sand, paleData);
        b.Set(8, 6, 0, Air);
        b.Set(12, 6, 0, Air);
        b.Set(9, 5, 0, Sand, orangeData);
        b.Set(10, 5, 0, Sand, paleData);
        b.Set(11, 5, 0, Sand, orangeData);
    }

    /// <summary>
    /// The sealed chamber. A shaft drops from the floor sigil to a landing ten
    /// blocks down, where four chests ring a pressure plate. The plate feeds a
    /// detonator buried in a bed of gunpowder kegs, so cracking the floor the
    /// impatient way costs the loot.
    /// </summary>
    private static void BuildHiddenChamber(StructureBuilder b, int paleData)
    {
        b.Fill(8, -14, 8, 12, -11, 12, Sand, paleData);
        b.Fill(8, -10, 8, 12, -10, 12, Sand, paleData);
        b.Fill(8, -9, 8, 12, -9, 12, Sand, paleData);
        b.Fill(8, -8, 8, 12, -1, 12, Sand);
        b.Fill(9, -11, 9, 11, -1, 11, Air);

        // Keg bed with the detonator at its centre, one course below the floor.
        b.Fill(9, -13, 9, 11, -13, 11, "MediumGunpowderKegBlock");
        b.Set(10, -13, 10, "DetonatorBlock", 5);
        // Signal path from the plate down through the floor slab. The
        // wire-through block's low bits pick the axis it conducts along; 2 is
        // vertical (see WireThroughBlock.SetWiredFace).
        b.Set(10, -12, 10, "WireThroughStoneBlock", 2);
        b.Set(10, -11, 10, "PressurePlateBlock", 4 << 1);

        // Alcoves at the four compass points, each holding a chest.
        foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
        {
            b.Set(10 + dx * 2, -11, 10 + dz * 2, Air);
            b.Set(10 + dx * 2, -10, 10 + dz * 2, Air);
            b.Set(10 + dx * 3, -10, 10 + dz * 3, Sand, paleData);
            b.Set(10 + dx * 3, -11, 10 + dz * 3, Sand, paleData);
            b.Chest(10 + dx * 2, -11, 10 + dz * 2);
        }
        // Guards wait in the entrance hall rather than the sealed chamber.
        b.Ambush(10, 1, 6);
        b.Ambush(6, 1, 2);
        b.Ambush(14, 1, 2);
    }
}

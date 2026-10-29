namespace SurvivalcraftRuins.Core;

/// <summary>A block cell in structure-relative coordinates. Y may be negative (below the anchor).</summary>
public readonly record struct Cell(short X, short Y, short Z, ushort PaletteIndex);

/// <summary>Palette entry: SC block class name plus data bits (Terrain data, not full value).</summary>
public readonly record struct PaletteEntry(string BlockName, int Data);

public readonly record struct ChestMarker(short X, short Y, short Z);

public readonly record struct AmbushMarker(short X, short Y, short Z);

/// <summary>
/// A realized structure: palette-compressed cells plus special markers.
/// Instances of this are immutable and shared; weathering is applied per world
/// position at write time so it never mutates the shared data.
/// </summary>
public sealed class StructureData
{
    public required string Name { get; init; }

    /// <summary>Inclusive bounds in relative coords. MinY may be negative (mines dig below the anchor).</summary>
    public required int MinX { get; init; }
    public required int MinY { get; init; }
    public required int MinZ { get; init; }
    public required int MaxX { get; init; }
    public required int MaxY { get; init; }
    public required int MaxZ { get; init; }

    public required IReadOnlyList<PaletteEntry> Palette { get; init; }
    public required IReadOnlyList<Cell> Cells { get; init; }
    public required IReadOnlyList<ChestMarker> Chests { get; init; }
    public required IReadOnlyList<AmbushMarker> Ambushes { get; init; }

    public int SizeX => MaxX - MinX + 1;
    public int SizeY => MaxY - MinY + 1;
    public int SizeZ => MaxZ - MinZ + 1;

    public static StructureData FromCells(
        string name,
        IReadOnlyList<PaletteEntry> palette,
        IReadOnlyList<Cell> cells,
        IReadOnlyList<ChestMarker> chests,
        IReadOnlyList<AmbushMarker> ambushes)
    {
        if (cells.Count == 0)
        {
            throw new InvalidOperationException($"Structure '{name}' has no cells.");
        }
        int minX = int.MaxValue, minY = int.MaxValue, minZ = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue, maxZ = int.MinValue;
        foreach (var c in cells)
        {
            if (c.X < minX) minX = c.X;
            if (c.Y < minY) minY = c.Y;
            if (c.Z < minZ) minZ = c.Z;
            if (c.X > maxX) maxX = c.X;
            if (c.Y > maxY) maxY = c.Y;
            if (c.Z > maxZ) maxZ = c.Z;
        }
        return new StructureData
        {
            Name = name,
            MinX = minX, MinY = minY, MinZ = minZ,
            MaxX = maxX, MaxY = maxY, MaxZ = maxZ,
            Palette = palette,
            Cells = cells,
            Chests = chests,
            Ambushes = ambushes,
        };
    }
}

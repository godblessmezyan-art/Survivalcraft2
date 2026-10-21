namespace SurvivalcraftRuins.Core;

/// <summary>
/// Mutable accumulator used by procedural builders. Later writes to the same
/// cell override earlier ones, so builders can carve rooms after massing walls.
/// </summary>
public sealed class StructureBuilder
{
    private readonly string m_name;
    private readonly List<PaletteEntry> m_palette = new();
    private readonly Dictionary<PaletteEntry, ushort> m_paletteLookup = new();
    private readonly Dictionary<(short, short, short), ushort> m_cells = new();
    private readonly List<ChestMarker> m_chests = new();
    private readonly List<AmbushMarker> m_ambushes = new();

    public StructureBuilder(string name)
    {
        m_name = name;
    }

    public ushort PaletteIndex(string blockName, int data = 0)
    {
        var entry = new PaletteEntry(blockName, data);
        if (!m_paletteLookup.TryGetValue(entry, out ushort index))
        {
            index = (ushort)m_palette.Count;
            m_palette.Add(entry);
            m_paletteLookup.Add(entry, index);
        }
        return index;
    }

    public void Set(int x, int y, int z, string blockName, int data = 0)
    {
        m_cells[((short)x, (short)y, (short)z)] = PaletteIndex(blockName, data);
    }

    public void Fill(int x0, int y0, int z0, int x1, int y1, int z1, string blockName, int data = 0)
    {
        ushort index = PaletteIndex(blockName, data);
        for (int x = Math.Min(x0, x1); x <= Math.Max(x0, x1); x++)
        for (int y = Math.Min(y0, y1); y <= Math.Max(y0, y1); y++)
        for (int z = Math.Min(z0, z1); z <= Math.Max(z0, z1); z++)
        {
            m_cells[((short)x, (short)y, (short)z)] = index;
        }
    }

    public void Box(int x0, int y0, int z0, int x1, int y1, int z1, string wallBlock, string interiorBlock = "AirBlock")
    {
        Fill(x0, y0, z0, x1, y1, z1, wallBlock);
        if (x1 - x0 > 1 && y1 - y0 > 1 && z1 - z0 > 1)
        {
            Fill(x0 + 1, y0 + 1, z0 + 1, x1 - 1, y1 - 1, z1 - 1, interiorBlock);
        }
    }

    public void Chest(int x, int y, int z)
    {
        Set(x, y, z, "ChestBlock");
        m_chests.Add(new ChestMarker((short)x, (short)y, (short)z));
    }

    public void Ambush(int x, int y, int z)
    {
        m_ambushes.Add(new AmbushMarker((short)x, (short)y, (short)z));
    }

    public StructureData Build()
    {
        var cells = new List<Cell>(m_cells.Count);
        foreach (var kv in m_cells)
        {
            cells.Add(new Cell(kv.Key.Item1, kv.Key.Item2, kv.Key.Item3, kv.Value));
        }
        // Deterministic ordering independent of dictionary iteration order.
        cells.Sort((a, b) =>
        {
            int c = a.Y.CompareTo(b.Y);
            if (c != 0) return c;
            c = a.Z.CompareTo(b.Z);
            if (c != 0) return c;
            return a.X.CompareTo(b.X);
        });
        return StructureData.FromCells(m_name, m_palette.ToArray(), cells, m_chests.ToArray(), m_ambushes.ToArray());
    }
}

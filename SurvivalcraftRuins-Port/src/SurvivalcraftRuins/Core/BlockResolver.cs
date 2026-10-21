using Game;

namespace SurvivalcraftRuins.Core;

/// <summary>
/// Maps SC block class names (e.g. "CobblestoneBlock") to terrain values.
/// Names are resolved against BlocksManager at first use so templates stay
/// engine-agnostic and unit-testable.
/// </summary>
public static class BlockResolver
{
    private static Dictionary<string, int>? s_indexByName;
    private static readonly object s_lock = new();

    public static void EnsureInitialized()
    {
        if (s_indexByName != null)
        {
            return;
        }
        lock (s_lock)
        {
            if (s_indexByName != null)
            {
                return;
            }
            var map = new Dictionary<string, int>(StringComparer.Ordinal);
            var blocks = BlocksManager.Blocks;
            for (int i = 0; i < blocks.Length; i++)
            {
                if (blocks[i] == null)
                {
                    continue;
                }
                string name = blocks[i].GetType().Name;
                if (!map.ContainsKey(name))
                {
                    map.Add(name, blocks[i].BlockIndex);
                }
            }
            s_indexByName = map;
        }
    }

    public static bool TryGetIndex(string blockName, out int index)
    {
        EnsureInitialized();
        return s_indexByName!.TryGetValue(blockName, out index);
    }

    /// <summary>Terrain value for a validated palette entry.</summary>
    public static int MakeValue(string blockName, int data)
    {
        if (blockName == "AirBlock")
        {
            return 0;
        }
        if (!TryGetIndex(blockName, out int index))
        {
            throw new InvalidDataException($"Unknown SCNET block name '{blockName}'. Refusing terrain write.");
        }
        if (data < 0 || data > 0x3FFFF)
        {
            throw new InvalidDataException($"Invalid data value {data} for '{blockName}'. Refusing terrain write.");
        }
        if (!BlockDataPolicy.IsSupported(blockName, data, out string reason))
        {
            throw new InvalidDataException($"Unsafe data value {data} for '{blockName}': {reason}. Refusing terrain write.");
        }
        return Terrain.MakeBlockValue(index, 0, data);
    }
}

using SimpleJson;

namespace SurvivalcraftRuins.Core;

public readonly record struct LootEntry(string BlockName, int Data, int Weight, int MinCount, int MaxCount);

public readonly record struct LootItem(string BlockName, int Data, int Count);

/// <summary>
/// Weighted loot pool. An entry with an empty block name is a deliberate miss,
/// used to make rare pools frequently come up empty.
/// </summary>
public sealed class LootPool
{
    public required string Name { get; init; }
    public required int MinRolls { get; init; }
    public required int MaxRolls { get; init; }
    public required IReadOnlyList<LootEntry> Entries { get; init; }

    public List<LootItem> Roll(Random random)
    {
        var result = new List<LootItem>();
        if (Entries.Count == 0)
        {
            return result;
        }
        int totalWeight = 0;
        foreach (var e in Entries)
        {
            totalWeight += e.Weight;
        }
        int rolls = random.Next(MinRolls, MaxRolls + 1);
        for (int i = 0; i < rolls; i++)
        {
            int pick = random.Next(totalWeight);
            foreach (var e in Entries)
            {
                pick -= e.Weight;
                if (pick < 0)
                {
                    if (!string.IsNullOrEmpty(e.BlockName))
                    {
                        int count = random.Next(e.MinCount, e.MaxCount + 1);
                        if (count > 0)
                        {
                            result.Add(new LootItem(e.BlockName, e.Data, count));
                        }
                    }
                    break;
                }
            }
        }
        return result;
    }
}

/// <summary>
/// Parses Assets/Loot/pools.json:
/// { "pools": [ { "name": "...", "rolls": [min,max],
///                "entries": [ { "block": "IronIngotBlock", "data": 0, "w": 6, "min": 1, "max": 3 },
///                             { "block": "", "w": 4 } ] } ] }
/// </summary>
public static class LootConfig
{
    public static Dictionary<string, LootPool> Parse(string json)
    {
        var pools = new Dictionary<string, LootPool>(StringComparer.OrdinalIgnoreCase);
        JsonObject root = JsonHelpers.ParseObject(json, "loot pools");
        foreach (object poolValue in JsonHelpers.Array(root, "pools"))
        {
            if (poolValue is not JsonObject p)
            {
                throw new InvalidDataException("Loot pool entry must be an object.");
            }
            JsonArray rolls = JsonHelpers.Array(p, "rolls");
            var entries = new List<LootEntry>();
            foreach (object entryValue in JsonHelpers.Array(p, "entries"))
            {
                if (entryValue is not JsonObject e)
                {
                    throw new InvalidDataException("Loot entry must be an object.");
                }
                string block = JsonHelpers.String(e, "block");
                entries.Add(new LootEntry(
                    block,
                    JsonHelpers.Int(e, "data", 0),
                    JsonHelpers.Int(e, "w", 1),
                    JsonHelpers.Int(e, "min", 1),
                    JsonHelpers.Int(e, "max", 1)));
            }
            string name = JsonHelpers.String(p, "name");
            pools[name] = new LootPool
            {
                Name = name,
                MinRolls = JsonHelpers.Int(rolls, 0),
                MaxRolls = JsonHelpers.Int(rolls, 1),
                Entries = entries,
            };
        }
        return pools;
    }
}

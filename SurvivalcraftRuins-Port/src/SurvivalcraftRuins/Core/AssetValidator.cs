using Game;

namespace SurvivalcraftRuins.Core;

public static class AssetValidator
{
    public const int ExpectedVariantCount = 23;

    public static void Validate(
        IReadOnlyList<StructureDef> definitions,
        IReadOnlyDictionary<string, LootPool> lootPools)
    {
        int variants = 0;
        foreach (StructureDef definition in definitions)
        {
            foreach (StructureVariant variant in definition.Variants)
            {
                variants++;
                StructureData data = variant.Provider(0x25_01_17);
                ValidateStructure(variant, data, lootPools);
            }
        }
        if (variants != ExpectedVariantCount)
        {
            throw new InvalidDataException($"Expected {ExpectedVariantCount} structure variants, found {variants}.");
        }

        foreach (LootPool pool in lootPools.Values)
        {
            if (pool.MinRolls < 0 || pool.MaxRolls < pool.MinRolls)
            {
                throw new InvalidDataException($"Loot pool '{pool.Name}' has invalid roll bounds.");
            }
            foreach (LootEntry entry in pool.Entries)
            {
                if (entry.Weight <= 0 || entry.MinCount < 0 || entry.MaxCount < entry.MinCount)
                {
                    throw new InvalidDataException($"Loot pool '{pool.Name}' has invalid weights/counts.");
                }
                if (!string.IsNullOrEmpty(entry.BlockName))
                {
                    ValidateBlock(entry.BlockName, entry.Data, "loot pool " + pool.Name);
                }
            }
        }
    }

    private static void ValidateStructure(
        StructureVariant variant,
        StructureData data,
        IReadOnlyDictionary<string, LootPool> lootPools)
    {
        if (data.Cells.Count <= 0 || data.Cells.Count > WorldGenerationLimits.MaxCellsPerStructure)
        {
            throw new InvalidDataException(
                $"Structure '{variant.Name}' has unsafe cell count {data.Cells.Count}.");
        }
        if (variant.Bounds.HasValue && variant.Bounds.Value != StructureBounds.From(data))
        {
            throw new InvalidDataException($"Structure '{variant.Name}' declared bounds do not match its cells.");
        }
        for (int i = 0; i < data.Palette.Count; i++)
        {
            PaletteEntry entry = data.Palette[i];
            ValidateBlock(entry.BlockName, entry.Data, "structure " + variant.Name);
        }
        foreach (Cell cell in data.Cells)
        {
            if (cell.PaletteIndex >= data.Palette.Count)
            {
                throw new InvalidDataException(
                    $"Structure '{variant.Name}' cell has palette index {cell.PaletteIndex} outside {data.Palette.Count}.");
            }
        }
        foreach (string pool in variant.ChestPools)
        {
            if (!lootPools.ContainsKey(pool))
            {
                throw new InvalidDataException($"Structure '{variant.Name}' references missing loot pool '{pool}'.");
            }
        }
        if (variant.DispenserPool != null && !lootPools.ContainsKey(variant.DispenserPool))
        {
            throw new InvalidDataException(
                $"Structure '{variant.Name}' references missing dispenser pool '{variant.DispenserPool}'.");
        }
    }

    private static void ValidateBlock(string blockName, int data, string source)
    {
        if (string.IsNullOrWhiteSpace(blockName))
        {
            throw new InvalidDataException($"{source} contains an empty block name.");
        }
        if (blockName != "AirBlock" && !BlockResolver.TryGetIndex(blockName, out _))
        {
            throw new InvalidDataException($"{source} references unknown SCNET block '{blockName}'.");
        }
        if (!BlockDataPolicy.IsSupported(blockName, data, out string reason))
        {
            throw new InvalidDataException($"{source} has unsafe data {data} for '{blockName}': {reason}.");
        }
    }
}

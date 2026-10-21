using System.Collections.Concurrent;
using Engine;
using Game;
using GameEntitySystem;
using SimpleJson;

namespace SurvivalcraftRuins.Core;

/// <summary>
/// A container written by worldgen that still needs its contents. Older saves
/// predate <see cref="Container"/>, whose empty value means "Chest".
/// </summary>
public sealed class PendingChest
{
    public int X, Y, Z;
    public string Pool = "";
    public int Seed;
    /// <summary>Entity template of the block entity: "Chest" or "Dispenser".</summary>
    public string Container = "";

    public string ContainerTemplate => string.IsNullOrEmpty(Container) ? "Chest" : Container;
}

public sealed class AmbushState
{
    public string Id = "";
    public int X, Y, Z;
    public string Creature = "";
    public int Min, Max;
    public float Radius;
    public bool Triggered;
    public int Seed;
    /// <summary>Guards spawn in water rather than in an air pocket.</summary>
    public bool Aquatic;
    /// <summary>Stand-in used when supernatural creatures are disabled; empty means skip.</summary>
    public string Substitute = "";
}

public sealed class RuinsSaveData
{
    public List<AmbushState> Ambushes = new();
    public List<PendingChest> Chests = new();

    /// <summary>
    /// Density this world was generated with, frozen on first visit so that
    /// changing the setting later cannot re-roll a world mid-exploration.
    /// </summary>
    public float DensityScale;

    /// <summary>
    /// Placement table this world uses. 0 marks a save written before the
    /// rebalance; such worlds are pinned to layout 1 (the pre-0.5 grid) so the
    /// structures already half-written into their explored edge stay complete.
    /// </summary>
    public int LayoutVersion;
}

/// <summary>Adapts the game's terrain generator to the testable sampler interface.</summary>
public sealed class GeneratorSampler : IWorldSampler
{
    private readonly ITerrainContentsGenerator m_generator;

    public GeneratorSampler(ITerrainContentsGenerator generator)
    {
        m_generator = generator;
    }

    public int OceanLevel => m_generator.OceanLevel;

    public float Height(float x, float z)
    {
        return m_generator.CalculateHeight(x, z);
    }

    public int Temperature(float x, float z)
    {
        return m_generator.CalculateTemperature(x, z);
    }

    public int Humidity(float x, float z)
    {
        return m_generator.CalculateHumidity(x, z);
    }
}

/// <summary>
/// Per-world runtime state. Chunk writing happens on the terrain thread; chest
/// entity creation and ambush spawning happen on the main thread via Update.
/// </summary>
public sealed class WorldState
{
    public required Project Project { get; init; }
    public required SubsystemTerrain Terrain { get; init; }
    public required SubsystemGameInfo GameInfo { get; init; }
    public required SubsystemBlockEntities BlockEntities { get; init; }
    public required SubsystemPlayers Players { get; init; }
    public required SubsystemCreatureSpawn CreatureSpawn { get; init; }
    public required ITerrainContentsGenerator Generator { get; init; }
    public required PlacementEngine Placement { get; init; }
    public required IReadOnlyDictionary<string, LootPool> LootPools { get; init; }

    // Markers discovered on the terrain thread, drained on the main thread.
    public readonly ConcurrentQueue<PendingChest> IncomingChests = new();
    public readonly ConcurrentQueue<AmbushState> IncomingAmbushes = new();

    // Main-thread state, persisted to the world directory.
    public readonly List<PendingChest> PendingChests = new();
    public readonly Dictionary<string, AmbushState> Ambushes = new();
    public bool Dirty;
    public double LastSaveTime;
    public int AmbushScanOffset;

    private static readonly ConcurrentDictionary<StructureData, (short X, short Z)[]> s_baseColumns = new();
    private static readonly ConcurrentDictionary<StructureData, ChestMarker[]> s_sortedChests = new();

    /// <summary>Guard post budget per variant; per-world so it dies with the world.</summary>
    private readonly ConcurrentDictionary<StructureVariant, GuardPost[]> m_guardPosts = new();

    public string SavePath => Storage.CombinePaths(GameInfo.DirectoryName, "SurvivalcraftRuins.json");

    /// <summary>Density frozen into this world; the placement engine is built with it.</summary>
    public float DensityScale { get; init; } = 1f;

    /// <summary>Placement table generation this world was created with.</summary>
    public int LayoutVersion { get; init; } = StructureRegistry.CurrentLayoutVersion;

    /// <summary>Live multiplier on guard counts; unlike density this may change any time.</summary>
    public float GuardScale => RuinsSettings.Guards;

    /// <summary>Reads a world's save file, or null when it has none yet.</summary>
    public static RuinsSaveData? ReadSaveData(string directoryName)
    {
        string path = Storage.CombinePaths(directoryName, "SurvivalcraftRuins.json");
        try
        {
            if (!Storage.FileExists(path))
            {
                return null;
            }
            string json = Storage.ReadAllText(path);
            return PersistenceJson.Parse(json);
        }
        catch (Exception exception)
        {
            Log.Warning($"[Ruins] Failed to load persistence: {exception.Message}");
            return null;
        }
    }

    public void LoadPersistence(RuinsSaveData? data)
    {
        if (data == null)
        {
            return;
        }
        foreach (var ambush in data.Ambushes)
        {
            Ambushes[ambush.Id] = ambush;
        }
        PendingChests.AddRange(data.Chests);
    }

    public void SavePersistence()
    {
        try
        {
            var data = new RuinsSaveData
            {
                Ambushes = new List<AmbushState>(Ambushes.Values),
                Chests = new List<PendingChest>(PendingChests),
                DensityScale = DensityScale,
                LayoutVersion = LayoutVersion,
            };
            string json = PersistenceJson.Serialize(data);
            string temporaryPath = SavePath + ".tmp";
            Storage.WriteAllText(temporaryPath, json);
            Storage.MoveFile(temporaryPath, SavePath);
            Dirty = false;
        }
        catch (Exception exception)
        {
            Log.Warning($"[Ruins] Failed to save persistence: {exception.Message}");
        }
    }

    /// <summary>
    /// Writes every structure part intersecting the chunk. Runs on the terrain
    /// thread right after content pass 3, so vanilla pass 4 decoration
    /// (plants, trees) happens around the finished structure.
    /// </summary>
    public void WriteChunk(TerrainChunk chunk)
    {
        int baseX = chunk.Coords.X * 16;
        int baseZ = chunk.Coords.Y * 16;
        var instances = Placement.InstancesIntersecting(baseX, baseZ, baseX + 15, baseZ + 15);
        int plannedWrites = 0;
        foreach (var instance in instances)
        {
            plannedWrites += CountInstanceWrites(chunk, instance, baseX, baseZ);
            if (plannedWrites > WorldGenerationLimits.MaxCellWritesPerChunk)
            {
                Log.Warning(
                    $"[Ruins] Refusing chunk {chunk.Coords}: planned writes {plannedWrites} exceed "
                    + $"limit {WorldGenerationLimits.MaxCellWritesPerChunk}.");
                return;
            }
        }
        foreach (var instance in instances)
        {
            WriteInstancePart(chunk, instance, baseX, baseZ);
        }
    }

    private static int CountInstanceWrites(TerrainChunk chunk, StructureInstance instance, int baseX, int baseZ)
    {
        int count = 0;
        foreach (Cell cell in instance.Data.Cells)
        {
            int wx = instance.OriginX + cell.X;
            int wy = instance.OriginY + cell.Y;
            int wz = instance.OriginZ + cell.Z;
            if (wx >= baseX && wx <= baseX + 15 && wz >= baseZ && wz <= baseZ + 15 && wy >= 1 && wy <= 254)
            {
                count++;
            }
        }
        if (instance.Def.Anchor is AnchorKind.Land or AnchorKind.Coast)
        {
            var seen = new HashSet<(short X, short Z)>();
            int foundationTop = instance.OriginY + instance.Data.MinY - 1;
            foreach (Cell cell in instance.Data.Cells)
            {
                if (cell.Y != instance.Data.MinY
                    || instance.Data.Palette[cell.PaletteIndex].BlockName == "AirBlock"
                    || !seen.Add((cell.X, cell.Z)))
                {
                    continue;
                }
                int wx = instance.OriginX + cell.X;
                int wz = instance.OriginZ + cell.Z;
                if (wx < baseX || wx > baseX + 15 || wz < baseZ || wz > baseZ + 15)
                {
                    continue;
                }
                int top = chunk.CalculateTopmostCellHeight(wx - baseX, wz - baseZ);
                count += Math.Max(0, Math.Min(254, foundationTop) - Math.Max(1, top + 1) + 1);
            }
        }
        return count;
    }

    private void WriteInstancePart(TerrainChunk chunk, StructureInstance instance, int baseX, int baseZ)
    {
        var data = instance.Data;
        var def = instance.Def;
        var variant = instance.Variant;
        var weather = variant.Weather;
        int ox = instance.OriginX, oy = instance.OriginY, oz = instance.OriginZ;

        if (def.Anchor is AnchorKind.Land or AnchorKind.Coast)
        {
            WriteFoundation(chunk, instance, baseX, baseZ);
        }

        var sortedChests = s_sortedChests.GetOrAdd(data, static d =>
        {
            var array = new ChestMarker[d.Chests.Count];
            for (int i = 0; i < d.Chests.Count; i++)
            {
                array[i] = d.Chests[i];
            }
            Array.Sort(array, (a, b) =>
            {
                int c = a.Y.CompareTo(b.Y);
                if (c != 0) return c;
                c = a.Z.CompareTo(b.Z);
                if (c != 0) return c;
                return a.X.CompareTo(b.X);
            });
            return array;
        });

        foreach (var cell in data.Cells)
        {
            int wx = ox + cell.X;
            int wz = oz + cell.Z;
            if (wx < baseX || wx > baseX + 15 || wz < baseZ || wz > baseZ + 15)
            {
                continue;
            }
            int wy = oy + cell.Y;
            if (wy < 1 || wy > 254)
            {
                continue;
            }
            var entry = data.Palette[cell.PaletteIndex];
            string blockName = entry.BlockName;

            bool weatherImmune = blockName is "AirBlock" or "ChestBlock" or "TreasureGeneratorBlock" or "WickerLampBlock";
            if (!weatherImmune && weather.DecayChance > 0f)
            {
                uint cellHash = DeterministicHash.Mix(
                    (uint)instance.InstanceSeed,
                    DeterministicHash.Mix((uint)cell.X, DeterministicHash.Mix((uint)cell.Y, (uint)cell.Z)));
                if (DeterministicHash.ToUnitFloat(cellHash) < weather.DecayChance)
                {
                    continue;
                }
                if (weather.SwapChance > 0f
                    && weather.Swaps.TryGetValue(blockName, out string? swapped)
                    && DeterministicHash.ToUnitFloat(DeterministicHash.Mix(cellHash, 7u)) < weather.SwapChance)
                {
                    blockName = swapped;
                }
            }

            chunk.SetCellValueFast(wx - baseX, wy, wz - baseZ, BlockResolver.MakeValue(blockName, entry.Data));

            if (blockName == "ChestBlock")
            {
                int chestIndex = Array.FindIndex(sortedChests, m => m.X == cell.X && m.Y == cell.Y && m.Z == cell.Z);
                string pool = ResolveChestPool(variant, chestIndex);
                EnqueueContainer(instance, wx, wy, wz, pool, "Chest");
            }
            else if (blockName == "DispenserBlock" && variant.DispenserPool != null)
            {
                EnqueueContainer(instance, wx, wy, wz, variant.DispenserPool, "Dispenser");
            }
        }

        if (variant.Guards != null)
        {
            foreach (var post in m_guardPosts.GetOrAdd(variant, static (v, d) => GuardPlanner.Plan(v, d), data))
            {
                int wx = ox + post.X;
                int wz = oz + post.Z;
                if (wx < baseX || wx > baseX + 15 || wz < baseZ || wz > baseZ + 15)
                {
                    continue;
                }
                int wy = CompatMath.Clamp(oy + post.Y, 1, 254);
                uint postHash = DeterministicHash.Mix((uint)instance.InstanceSeed, (uint)(wx * 31 ^ wz));
                var creatures = variant.Guards.Creatures;
                string creature = creatures[(int)(postHash % (uint)creatures.Count)];
                IncomingAmbushes.Enqueue(new AmbushState
                {
                    Id = $"{variant.Name}:{wx},{wy},{wz}",
                    X = wx,
                    Y = wy,
                    Z = wz,
                    Creature = creature,
                    Min = post.Count,
                    Max = post.Count,
                    Radius = variant.Guards.TriggerRadius,
                    Aquatic = variant.Guards.Aquatic,
                    Substitute = variant.Guards.MundaneSubstitute ?? "",
                    Seed = (int)DeterministicHash.Mix(postHash, 0x60ADu),
                });
            }
        }
    }

    private void EnqueueContainer(StructureInstance instance, int wx, int wy, int wz, string pool, string container)
    {
        IncomingChests.Enqueue(new PendingChest
        {
            X = wx,
            Y = wy,
            Z = wz,
            Pool = pool,
            Container = container,
            Seed = (int)DeterministicHash.Mix((uint)instance.InstanceSeed,
                DeterministicHash.Mix((uint)wx, DeterministicHash.Mix((uint)wy, (uint)wz))),
        });
    }

    private static string ResolveChestPool(StructureVariant variant, int chestIndex)
    {
        if (variant.ChestPools.Count == 0)
        {
            return "baseline";
        }
        if (chestIndex < 0 || chestIndex >= variant.ChestPools.Count)
        {
            return variant.ChestPools[variant.ChestPools.Count - 1];
        }
        return variant.ChestPools[chestIndex];
    }

    private void WriteFoundation(TerrainChunk chunk, StructureInstance instance, int baseX, int baseZ)
    {
        var data = instance.Data;
        var columns = s_baseColumns.GetOrAdd(data, static d =>
        {
            var set = new HashSet<(short, short)>();
            foreach (var cell in d.Cells)
            {
                if (cell.Y == d.MinY && d.Palette[cell.PaletteIndex].BlockName != "AirBlock")
                {
                    set.Add((cell.X, cell.Z));
                }
            }
            var array = new (short, short)[set.Count];
            int i = 0;
            foreach (var c in set)
            {
                array[i++] = c;
            }
            return array;
        });

        int foundationValue = BlockResolver.MakeValue(instance.Variant.FoundationBlock, 0);
        if (foundationValue == 0)
        {
            return;
        }
        int foundationTop = instance.OriginY + data.MinY - 1;
        foreach (var (dx, dz) in columns)
        {
            int wx = instance.OriginX + dx;
            int wz = instance.OriginZ + dz;
            if (wx < baseX || wx > baseX + 15 || wz < baseZ || wz > baseZ + 15)
            {
                continue;
            }
            int lx = wx - baseX;
            int lz = wz - baseZ;
            int top = chunk.CalculateTopmostCellHeight(lx, lz);
            for (int y = top + 1; y <= foundationTop && y <= 254; y++)
            {
                if (y >= 1)
                {
                    chunk.SetCellValueFast(lx, y, lz, foundationValue);
                }
            }
        }
    }
}

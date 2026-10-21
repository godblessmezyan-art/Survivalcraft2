using Engine;
using Game;
using GameEntitySystem;
using TemplatesDatabase;
using Random = System.Random;

namespace SurvivalcraftRuins.Core;

/// <summary>
/// Static orchestrator bridging ModLoader hooks to per-world state.
/// </summary>
public static class RuinsRuntime
{
    private const int ChestsPerFrame = 8;
    private const int ChestChecksPerFrame = 64;
    private const int IncomingMarkersPerFrame = 128;
    private const int AmbushChecksPerFrame = 64;
    private const double SaveIntervalSeconds = 5.0;

    private static readonly object s_bindLock = new();
    private static readonly string[] s_requiredTemplateNames =
    {
        "bathhouse",
        "coliseum",
        "fishing_hut",
        "galley",
        "mushroom_house",
        "prairie_ruin",
        "ruined_temple",
        "sky_blimp",
        "sky_fortress",
        "tree_house",
        "werewolf_den",
        "windmill",
        "wishing_well",
    };

    private static WorldState? s_state;
    private static Project? s_failedProject;
    private static double s_time;

    private static IReadOnlyDictionary<string, StructureData>? s_templates;
    private static IReadOnlyDictionary<string, LootPool>? s_lootPools;

    /// <summary>Loads templates and loot pools from the mod package. Idempotent.</summary>
    public static void LoadAssets(ModEntity entity)
    {
        if (s_templates != null)
        {
            return;
        }
        var templates = new Dictionary<string, StructureData>(StringComparer.OrdinalIgnoreCase);
        foreach (string expectedName in s_requiredTemplateNames)
        {
            string path = "Assets/Structures/" + expectedName + ".json";
            StructureData data = StructureTemplate.Parse(ReadRequiredAsset(entity, path));
            if (!string.Equals(data.Name, expectedName, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Structure asset '{path}' declares name '{data.Name}', expected '{expectedName}'.");
            }
            templates.Add(data.Name, data);
        }
        Dictionary<string, LootPool> pools = LootConfig.Parse(
            ReadRequiredAsset(entity, "Assets/Loot/pools.json"));
        if (pools.Count == 0)
        {
            throw new InvalidDataException("Loot asset 'Assets/Loot/pools.json' contains no pools.");
        }
        s_templates = templates;
        s_lootPools = pools;
        Log.Information($"[Ruins] Loaded {templates.Count} structure templates, {s_lootPools.Count} loot pools.");
    }

    private static string ReadRequiredAsset(ModEntity entity, string path)
    {
        string? contents = null;
        Exception? readError = null;
        entity.GetFile(path, stream =>
        {
            try
            {
                using var reader = new StreamReader(stream);
                contents = reader.ReadToEnd();
            }
            catch (Exception exception)
            {
                readError = exception;
            }
        });
        if (readError != null)
        {
            throw new InvalidDataException($"Cannot read required mod asset '{path}'.", readError);
        }
        if (contents == null)
        {
            throw new FileNotFoundException($"Required mod asset '{path}' is missing or has incorrect case.", path);
        }
        return contents;
    }

    public static void OnChunkGenerated(TerrainChunk chunk)
    {
        if (!RuntimeCompat.CanMutateWorld)
        {
            return;
        }
        var state = BindIfNeeded();
        state?.WriteChunk(chunk);
    }

    public static void Update(float dt)
    {
        if (!RuntimeCompat.CanMutateWorld)
        {
            return;
        }
        var state = BindIfNeeded();
        if (state == null)
        {
            return;
        }
        s_time += dt;

        DrainIncoming(state);
        ProcessPendingChests(state);
        ProcessAmbushes(state);

        if (state.Dirty && s_time - state.LastSaveTime > SaveIntervalSeconds)
        {
            state.LastSaveTime = s_time;
            state.SavePersistence();
        }
    }

    private static WorldState? BindIfNeeded()
    {
        var project = GameManager.Project;
        if (project != null && ReferenceEquals(s_failedProject, project))
        {
            return null;
        }
        var current = s_state;
        if (current != null && ReferenceEquals(current.Project, project))
        {
            return current;
        }
        lock (s_bindLock)
        {
            current = s_state;
            if (current != null && ReferenceEquals(current.Project, project))
            {
                return current;
            }
            if (current != null)
            {
                if (current.Dirty)
                {
                    current.SavePersistence();
                }
                s_state = null;
            }
            if (project == null || s_templates == null || s_lootPools == null)
            {
                return null;
            }
            try
            {
                var terrain = project.FindSubsystem<SubsystemTerrain>();
                var gameInfo = project.FindSubsystem<SubsystemGameInfo>();
                if (terrain?.TerrainContentsGenerator == null || gameInfo == null)
                {
                    return null;
                }
                BlockResolver.EnsureInitialized();

                // Every world, however old, gets the current table. Earlier
                // versions pinned a world to the layout it was created with so
                // that a structure straddling the explored frontier could never
                // end up half written. That cost more than it bought: players
                // adding the mod to a world they already play got the old,
                // near-empty layout and never saw the new structures. The
                // frontier artefact is one partially written building at the
                // edge of already generated land, once, and only where the two
                // layouts disagree.
                var saved = WorldState.ReadSaveData(gameInfo.DirectoryName);
                float density = RuinsSettings.Density;
                int layout = StructureRegistry.CurrentLayoutVersion;
                if (saved != null && saved.LayoutVersion != layout)
                {
                    Log.Information(
                        $"[Ruins] World '{gameInfo.DirectoryName}' was generated with layout "
                        + $"{saved.LayoutVersion}; newly generated chunks will use layout {layout}. "
                        + "Already generated land is untouched.");
                }

                var defs = StructureRegistry.CreateDefs(s_templates);
                AssetValidator.Validate(defs, s_lootPools);
                var sampler = new GeneratorSampler(terrain.TerrainContentsGenerator);
                var state = new WorldState
                {
                    Project = project,
                    Terrain = terrain,
                    GameInfo = gameInfo,
                    BlockEntities = project.FindSubsystem<SubsystemBlockEntities>(throwOnError: true),
                    Players = project.FindSubsystem<SubsystemPlayers>(throwOnError: true),
                    CreatureSpawn = project.FindSubsystem<SubsystemCreatureSpawn>(throwOnError: true),
                    Generator = terrain.TerrainContentsGenerator,
                    Placement = new PlacementEngine(gameInfo.WorldSeed, sampler, defs, density),
                    LootPools = s_lootPools,
                    DensityScale = density,
                    LayoutVersion = layout,
                };
                state.LoadPersistence(saved);
                state.Dirty = true;   // records density/layout for worlds that predate them
                s_state = state;
                s_failedProject = null;
                Log.Information(
                    $"[Ruins] Bound to world '{gameInfo.DirectoryName}', seed {gameInfo.WorldSeed}, "
                    + $"layout {layout}, density {density:0.##}, guards {RuinsSettings.Guards:0.##}.");
                return state;
            }
            catch (Exception exception)
            {
                s_failedProject = project;
                Log.Warning($"[Ruins] World bind failed: {exception.Message}");
                return null;
            }
        }
    }

    private static void DrainIncoming(WorldState state)
    {
        bool changed = false;
        int drained = 0;
        while (drained < IncomingMarkersPerFrame && state.IncomingChests.TryDequeue(out var chest))
        {
            drained++;
            bool exists = false;
            foreach (var existing in state.PendingChests)
            {
                if (existing.X == chest.X && existing.Y == chest.Y && existing.Z == chest.Z)
                {
                    exists = true;
                    break;
                }
            }
            if (!exists)
            {
                state.PendingChests.Add(chest);
                changed = true;
            }
        }
        while (drained < IncomingMarkersPerFrame && state.IncomingAmbushes.TryDequeue(out var ambush))
        {
            drained++;
            if (!state.Ambushes.ContainsKey(ambush.Id))
            {
                state.Ambushes.Add(ambush.Id, ambush);
                changed = true;
            }
        }
        if (changed)
        {
            state.Dirty = true;
        }
    }

    private static void ProcessPendingChests(WorldState state)
    {
        int processed = 0;
        int checkedCount = 0;
        for (int i = state.PendingChests.Count - 1;
             i >= 0 && processed < ChestsPerFrame && checkedCount < ChestChecksPerFrame;
             i--, checkedCount++)
        {
            var pending = state.PendingChests[i];
            var chunk = state.Terrain.Terrain.GetChunkAtCell(pending.X, pending.Z);
            if (chunk == null || chunk.State < TerrainChunkState.InvalidVertices1)
            {
                continue;
            }
            processed++;
            int contents = state.Terrain.Terrain.GetCellContents(pending.X, pending.Y, pending.Z);
            string blockName = pending.ContainerTemplate == "Dispenser" ? "DispenserBlock" : "ChestBlock";
            bool stillThere = BlockResolver.TryGetIndex(blockName, out int blockIndex) && contents == blockIndex;
            if (stillThere)
            {
                try
                {
                    FillChest(state, pending);
                }
                catch (Exception exception)
                {
                    Log.Warning($"[Ruins] Container fill at {pending.X},{pending.Y},{pending.Z} failed: {exception.Message}");
                }
            }
            state.PendingChests.RemoveAt(i);
            state.Dirty = true;
        }
    }

    private static void FillChest(WorldState state, PendingChest pending)
    {
        var point = new Point3(pending.X, pending.Y, pending.Z);
        var blockEntity = state.BlockEntities.GetBlockEntity(pending.X, pending.Y, pending.Z);
        if (blockEntity == null)
        {
            // Replicates SubsystemBlockEntities.CreateBlockEntityLogic without
            // the owner (which dereferences a null miner during worldgen).
            var databaseObject = state.Project.GameDatabase.Database.FindDatabaseObject(
                pending.ContainerTemplate, state.Project.GameDatabase.EntityTemplateType, throwIfNotFound: true);
            var valuesDictionary = new ValuesDictionary();
            valuesDictionary.PopulateFromDatabaseObject(databaseObject);
            valuesDictionary.GetValue<ValuesDictionary>("BlockEntity").SetValue("Coordinates", point);
            var entity = state.Project.CreateEntity(valuesDictionary);
            state.Project.AddEntity(entity);
            blockEntity = state.BlockEntities.GetBlockEntity(pending.X, pending.Y, pending.Z);
            if (blockEntity == null)
            {
                return;
            }
        }
        // ComponentChest and ComponentDispenser share ComponentInventoryBase, so
        // the same fill path serves chests and loaded arrow traps.
        var chest = blockEntity.Entity.FindComponent<ComponentInventoryBase>();
        if (chest == null)
        {
            return;
        }
        if (!state.LootPools.TryGetValue(pending.Pool, out var pool))
        {
            Log.Warning($"[Ruins] Unknown loot pool '{pending.Pool}'.");
            return;
        }
        var random = new Random(pending.Seed);
        foreach (var item in pool.Roll(random))
        {
            int value = BlockResolver.MakeValue(item.BlockName, item.Data);
            if (value != 0)
            {
                ComponentInventoryBase.AcquireItems(chest, value, item.Count);
            }
        }
    }

    private static void ProcessAmbushes(WorldState state)
    {
        if (state.Ambushes.Count == 0 || state.Players.ComponentPlayers.Count == 0)
        {
            return;
        }
        List<string>? triggeredIds = null;
        var ambushes = new AmbushState[state.Ambushes.Count];
        state.Ambushes.Values.CopyTo(ambushes, 0);
        if (ambushes.Length == 0)
        {
            return;
        }
        int checks = Math.Min(AmbushChecksPerFrame, ambushes.Length);
        for (int i = 0; i < checks; i++)
        {
            int index = (state.AmbushScanOffset + i) % ambushes.Length;
            AmbushState ambush = ambushes[index];
            if (ambush.Triggered)
            {
                continue;
            }
            bool playerNear = false;
            foreach (var player in state.Players.ComponentPlayers)
            {
                var position = player.ComponentBody.Position;
                float dx = position.X - ambush.X;
                float dy = position.Y - ambush.Y;
                float dz = position.Z - ambush.Z;
                if (dx * dx + dy * dy + dz * dz <= ambush.Radius * ambush.Radius)
                {
                    playerNear = true;
                    break;
                }
            }
            if (!playerNear)
            {
                continue;
            }
            var chunk = state.Terrain.Terrain.GetChunkAtCell(ambush.X, ambush.Z);
            if (chunk == null || chunk.State < TerrainChunkState.Valid)
            {
                continue;
            }
            SpawnAmbush(state, ambush);
            (triggeredIds ??= new List<string>()).Add(ambush.Id);
        }
        state.AmbushScanOffset = (state.AmbushScanOffset + checks) % ambushes.Length;
        if (triggeredIds != null)
        {
            foreach (var id in triggeredIds)
            {
                state.Ambushes[id].Triggered = true;
            }
            state.Dirty = true;
        }
    }

    private static void SpawnAmbush(WorldState state, AmbushState ambush)
    {
        string creature = ambush.Creature;
        if (!state.GameInfo.WorldSettings.AreSupernaturalCreaturesEnabled && IsSupernatural(creature))
        {
            if (string.IsNullOrEmpty(ambush.Substitute))
            {
                return;
            }
            creature = ambush.Substitute;
        }

        var random = new Random(ambush.Seed);
        int count = random.Next(ambush.Min, ambush.Max + 1);
        count = (int)CompatMath.Round(count * state.GuardScale);
        if (count <= 0)
        {
            return;
        }
        for (int i = 0; i < count; i++)
        {
            float angle = (float)(random.NextDouble() * Math.PI * 2.0);
            float distance = 1.5f + (float)random.NextDouble() * 3f;
            int x = ambush.X + (int)CompatMath.Round(CompatMath.Cos(angle) * distance);
            int z = ambush.Z + (int)CompatMath.Round(CompatMath.Sin(angle) * distance);
            // Prefer the marker height (inside the structure); creatures placed
            // at the terrain top would appear on the roof and wander off.
            int y = ambush.Aquatic
                ? FindWaterSpawnY(state, x, ambush.Y, z)
                : FindSpawnY(state, x, ambush.Y, z);
            try
            {
                // Portable across SCAPI and Net2.4 (CreateEntity is Net-only).
                var entity = DatabaseManager.CreateEntity(state.Project, creature, throwIfNotFound: true);
                var frame = entity.FindComponent<ComponentFrame>(throwOnError: true);
                frame.Position = new Vector3(x + 0.5f, y + 0.5f, z + 0.5f);
                frame.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY,
                    (float)(random.NextDouble() * Math.PI * 2.0));
                entity.FindComponent<ComponentSpawn>(throwOnError: true).SpawnDuration = 0f;
                state.Project.AddEntity(entity);
            }
            catch (Exception exception)
            {
                Log.Warning($"[Ruins] Guard spawn '{creature}' failed: {exception.Message}");
            }
        }
        Log.Information($"[Ruins] Guards roused at {ambush.X},{ambush.Y},{ambush.Z}: {count}x {creature}.");
    }

    private static bool IsSupernatural(string creature)
    {
        return creature.StartsWith("Werewolf", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Spawn height for swimmers: the first cell around the marker that holds
    /// water, searched upward then downward, so sharks never beach themselves.
    /// </summary>
    private static int FindWaterSpawnY(WorldState state, int x, int markerY, int z)
    {
        var terrain = state.Terrain.Terrain;
        if (terrain.GetChunkAtCell(x, z) == null)
        {
            return CompatMath.Clamp(markerY, 1, 254);
        }
        BlockResolver.TryGetIndex("WaterBlock", out int water);
        for (int offset = 0; offset <= 8; offset++)
        {
            int up = markerY + offset;
            if (up <= 253 && terrain.GetCellContents(x, up, z) == water)
            {
                return up;
            }
            int down = markerY - offset;
            if (down >= 1 && terrain.GetCellContents(x, down, z) == water)
            {
                return down;
            }
        }
        return CompatMath.Clamp(markerY, 1, 254);
    }

    /// <summary>
    /// Picks a spawn height: standing room as close to the post as possible,
    /// searched up and then down, and only as a last resort above the terrain.
    /// Guards are posted at chests now, so a cramped cellar must not push them
    /// up through the roof.
    /// </summary>
    private static int FindSpawnY(WorldState state, int x, int markerY, int z)
    {
        var terrain = state.Terrain.Terrain;
        if (terrain.GetChunkAtCell(x, z) == null)
        {
            return CompatMath.Clamp(markerY, 1, 254);
        }
        for (int offset = 0; offset <= 6; offset++)
        {
            int up = markerY + offset;
            if (up <= 253 && HasStandingRoom(terrain, x, up, z))
            {
                return up;
            }
            int down = markerY - offset;
            if (down >= 1 && HasStandingRoom(terrain, x, down, z))
            {
                return down;
            }
        }
        return Math.Min(terrain.CalculateTopmostCellHeight(x, z) + 1, 254);
    }

    private static bool HasStandingRoom(Terrain terrain, int x, int y, int z)
    {
        return terrain.GetCellContents(x, y, z) == 0 && terrain.GetCellContents(x, y + 1, z) == 0;
    }
}

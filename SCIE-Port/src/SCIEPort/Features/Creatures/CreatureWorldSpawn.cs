using Engine;
using Game;
using Game.NetWork;
using GameEntitySystem;

namespace SCIEPort.Features.Creatures
{
    /// <summary>
    /// World-facing natural creature spawner. It is driven from the already-proven
    /// <c>OnTerrainContentsGenerated</c> chunk hook (the same hook the Kelly plant
    /// scatterer uses) so that fox and rabbit placement is derived entirely from the
    /// world seed and a fresh chunk, never from a whole-map scan. Every decision that
    /// can be made purely (seed stability, spawn chance, per-chunk budget, population
    /// ceiling, habitat suitability and authority gating) is delegated to the offline
    /// tested <see cref="Ecology.EcologyRules"/>, and this class only translates those
    /// decisions into real chunk queries and a single guarded <see cref="SubsystemCreatureSpawn"/>
    /// call. Live entity generation during chunk generation is intentionally marked
    /// 待验证: the static ABI and the pure rules are 已确认, but the on-device behaviour
    /// (spawn timing inside generation, desync, despawn bookkeeping) has not yet been
    /// observed on Android/7723 and must not be claimed as working.
    /// </summary>
    internal static class CreatureWorldSpawn
    {
        private static SubsystemCreatureSpawn s_creatureSpawn;
        private static SubsystemGameInfo s_gameInfo;
        private static SubsystemTerrain s_terrain;

        // Tracked alive counts. These only ever increase on a successful spawn and
        // are clamped to the species ceiling, so the worst case is an over-conservative
        // population that stops spawning rather than an over-populated world. Wiring the
        // decrement on creature death is a known, honestly-declared gap (待验证).
        private static int s_trackedFoxes;
        private static int s_trackedRabbits;

        /// <summary>
        /// Caches the game subsystems needed to spawn creatures. Safe to call more than
        /// once (the fox and rabbit spawn subsystems both feed the same active Project).
        /// </summary>
        public static void SetProject(Project project)
        {
            if (project == null)
            {
                return;
            }

            s_creatureSpawn = project.FindSubsystem<SubsystemCreatureSpawn>(false);
            s_gameInfo = project.FindSubsystem<SubsystemGameInfo>(false);
            s_terrain = project.FindSubsystem<SubsystemTerrain>(false);
        }

        /// <summary>
        /// True only when every subsystem the spawner needs is present. The loader must
        /// check this before calling <see cref="TrySpawnNaturalCreatures"/> so a partially
        /// initialized world never triggers a null reference.
        /// </summary>
        public static bool IsReady
        {
            get { return s_creatureSpawn != null && s_gameInfo != null && s_terrain != null; }
        }

        /// <summary>
        /// Attempt to seed at most <c>MaxSpawnAttemptsPerChunk</c> creatures into a
        /// freshly generated chunk. Runs only on the authoritative side (single-player
        /// local or listen/host server); a normal client returns immediately so it can
        /// never manufacture an unsynchronized entity.
        /// </summary>
        public static void TrySpawnNaturalCreatures(TerrainChunk chunk)
        {
            if (chunk == null || !IsReady)
            {
                return;
            }

            // Authority gate: an ordinary client must not create entities.
            if (!Ecology.EcologyRules.CanAuthoritativeWorldMutation(CommonLib.WorkType == WorkType.Client))
            {
                return;
            }

            // Determinism: the roll sequence is derived from the world seed plus the chunk
            // coordinates, so the same world always seeds the same creatures and re-entering
            // a chunk never re-randomizes the population.
            Game.Random random = new Game.Random(
                Ecology.EcologyRules.GetChunkSeed(s_gameInfo.WorldSeed, chunk.Coords.X, chunk.Coords.Y));

            int attempts = 0;

            // Fox first (predator), then rabbit (prey). The budget caps total spawns per
            // chunk pass so one generation never bursts the frame on a low-end device.
            if (Ecology.EcologyRules.CanSpawnWithinBudget(attempts))
            {
                attempts += TrySpawnFox(chunk, random) ? 1 : 0;
            }

            if (Ecology.EcologyRules.CanSpawnWithinBudget(attempts))
            {
                attempts += TrySpawnRabbit(chunk, random) ? 1 : 0;
            }
        }

        private static bool TrySpawnFox(TerrainChunk chunk, Game.Random random)
        {
            int trackedFoxes = Ecology.EcologyRules.ClampTrackedCount(s_trackedFoxes, Ecology.EcologyRules.FoxCap);
            if (trackedFoxes >= Ecology.EcologyRules.FoxCap)
            {
                return false;
            }

            int cellX = random.Int(0, 16);
            int cellZ = random.Int(0, 16);
            int surfaceY = FindSurface(chunk, cellX, cellZ);
            if (surfaceY < 0 || !IsAir(chunk, cellX, surfaceY + 1, cellZ))
            {
                return false;
            }

            int ground = Terrain.ExtractContents(chunk.GetCellValueFast(cellX, surfaceY, cellZ));
            bool naturalGround = IsNaturalGround(ground);
            bool canopy = HasNearbyLeaves(chunk, cellX, surfaceY + 1, cellZ);
            bool habitat = Ecology.EcologyRules.IsFoxHabitat(
                chunk.GetTemperatureFast(cellX, cellZ),
                chunk.GetHumidityFast(cellX, cellZ),
                naturalGround,
                canopy);
            if (!habitat)
            {
                return false;
            }

            if (!Ecology.EcologyRules.ChanceMet(random.Int(0, 100), Ecology.EcologyRules.FoxSpawnChancePercent))
            {
                return false;
            }

            return SpawnCreature(chunk, "SCIEPortFox", cellX, surfaceY + 1, cellZ, ref s_trackedFoxes, Ecology.EcologyRules.FoxCap);
        }

        private static bool TrySpawnRabbit(TerrainChunk chunk, Game.Random random)
        {
            int trackedRabbits = Ecology.EcologyRules.ClampTrackedCount(s_trackedRabbits, Ecology.EcologyRules.RabbitCap);
            if (trackedRabbits >= Ecology.EcologyRules.RabbitCap)
            {
                return false;
            }

            int cellX = random.Int(0, 16);
            int cellZ = random.Int(0, 16);
            int surfaceY = FindSurface(chunk, cellX, cellZ);
            if (surfaceY < 0 || !IsAir(chunk, cellX, surfaceY + 1, cellZ))
            {
                return false;
            }

            int ground = Terrain.ExtractContents(chunk.GetCellValueFast(cellX, surfaceY, cellZ));
            bool naturalGround = IsNaturalGround(ground);
            bool canopy = HasNearbyLeaves(chunk, cellX, surfaceY + 1, cellZ);
            bool habitat = Ecology.EcologyRules.IsRabbitHabitat(
                chunk.GetTemperatureFast(cellX, cellZ),
                chunk.GetHumidityFast(cellX, cellZ),
                naturalGround,
                canopy);
            if (!habitat)
            {
                return false;
            }

            if (!Ecology.EcologyRules.ChanceMet(random.Int(0, 100), Ecology.EcologyRules.RabbitSpawnChancePercent))
            {
                return false;
            }

            return SpawnCreature(chunk, "SCIEPortRabbit", cellX, surfaceY + 1, cellZ, ref s_trackedRabbits, Ecology.EcologyRules.RabbitCap);
        }

        private static bool SpawnCreature(
            TerrainChunk chunk,
            string creatureName,
            int localX,
            int localY,
            int localZ,
            ref int trackedCounter,
            int ceiling)
        {
            // World-space position mirrors the manual creative spawn offset so a naturally
            // seeded creature stands centred on top of the surface cell, exactly like a
            // player-placed one. chunk.Origin.X is the world X of local 0 and Origin.Y the
            // world Z of local 0 (the same mapping the Kelly scatterer relies on).
            Vector3 position = new Vector3(
                chunk.Origin.X + localX + 0.5f,
                localY + 0.05f,
                chunk.Origin.Y + localZ + 0.5f);

            try
            {
                GameEntitySystem.Entity entity = s_creatureSpawn.SpawnCreature(creatureName, position, true);
                if (entity == null)
                {
                    return false;
                }

                trackedCounter = Ecology.EcologyRules.ClampTrackedCount(trackedCounter + 1, ceiling);
                return true;
            }
            catch (System.Exception exception)
            {
                // Live entity generation inside chunk generation is 待验证. If it throws we
                // surface the failure through the shared logger rather than swallowing it,
                // so the on-device test produces evidence instead of a silent no-op.
                Log.Error("[SCIEPort] Natural creature spawn failed for " + creatureName + ": " + exception);
                return false;
            }
        }

        private static int FindSurface(TerrainChunk chunk, int x, int z)
        {
            for (int y = MathUtils.Min(chunk.GetTopHeightFast(x, z), 254); y >= 1; y--)
            {
                if (Terrain.ExtractContents(chunk.GetCellValueFast(x, y, z)) != 0)
                {
                    return y;
                }
            }

            return -1;
        }

        private static bool IsAir(TerrainChunk chunk, int x, int y, int z)
        {
            return y >= 0 && y < 256 && Terrain.ExtractContents(chunk.GetCellValueFast(x, y, z)) == 0;
        }

        private static bool IsNaturalGround(int contents)
        {
            return contents > 0 &&
                (BlocksManager.Blocks[contents] is GrassBlock || BlocksManager.Blocks[contents] is DirtBlock);
        }

        // Canopy proxy reused from the Kelly scatterer: fox habitat wants real forest cover,
        // rabbit habitat wants its absence, so we only need a boolean "is there leaves nearby".
        private static bool HasNearbyLeaves(TerrainChunk chunk, int x, int y, int z)
        {
            int leaves = 0;
            for (int dx = -3; dx <= 3; dx++)
            {
                for (int dz = -3; dz <= 3; dz++)
                {
                    int localX = x + dx;
                    int localZ = z + dz;
                    if (localX < 0 || localX > 15 || localZ < 0 || localZ > 15)
                    {
                        continue;
                    }

                    for (int dy = 1; dy <= 9 && y + dy < 256; dy++)
                    {
                        int contents = Terrain.ExtractContents(chunk.GetCellValueFast(localX, y + dy, localZ));
                        if (contents > 0 && BlocksManager.Blocks[contents] is LeavesBlock && ++leaves >= 3)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}

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
    /// tested <see cref="Ecology.EcologyRules"/>.
    ///
    /// The chunk hook only <em>validates and queues</em>. The entity is created later from
    /// <see cref="Update"/> once the chunk has finished its contents stage, because creating
    /// entities from inside chunk generation is not a supported x25.01.17 path and was the
    /// reason a fresh world could stall on the loading screen.
    /// </summary>
    internal static class CreatureWorldSpawn
    {
        private struct PendingSpawn
        {
            internal string Creature;
            internal int CellX, CellY, CellZ, Ceiling;
            internal bool Rabbit;
        }

        private const int MaxPendingSpawns = 32;
        private const int MaxSpawnsPerUpdate = 2;
        private const int MaxFailuresBeforeDisable = 5;

        private static SubsystemCreatureSpawn s_creatureSpawn;
        private static SubsystemGameInfo s_gameInfo;
        private static SubsystemTerrain s_terrain;

        private static readonly System.Collections.Generic.List<PendingSpawn> s_pending =
            new System.Collections.Generic.List<PendingSpawn>(MaxPendingSpawns);

        // Tracked alive counts. These only ever increase on a successful spawn and
        // are clamped to the species ceiling, so the worst case is an over-conservative
        // population that stops spawning rather than an over-populated world. Wiring the
        // decrement on creature death is a known, honestly-declared gap (待验证).
        private static int s_trackedFoxes;
        private static int s_trackedRabbits;
        private static int s_probeFailuresLogged;
        private static int s_failures;
        private static bool s_disabled;

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
            s_pending.Clear();
            // A new project starts with a clean failure budget: a previous world's fault must
            // not disable spawning for the rest of the session.
            s_failures = 0;
            s_disabled = false;
            s_probeFailuresLogged = 0;
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
            if (s_disabled || chunk == null || !IsReady)
            {
                return;
            }

            // Authority gate: an ordinary client must not create entities.
            if (!Ecology.EcologyRules.CanAuthoritativeWorldMutation(CommonLib.WorkType == WorkType.Client))
            {
                return;
            }

            try
            {
                SpawnPass(chunk);
            }
            catch (System.Exception exception)
            {
                FailSafe(exception);
            }
        }

        private static void SpawnPass(TerrainChunk chunk)
        {
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

        /// <summary>
        /// A chunk-generation hook must never keep failing: after a handful of unexpected
        /// exceptions the spawner disables itself for the session instead of touching every
        /// remaining chunk in the spawn area, so a fault can no longer keep a fresh world
        /// from finishing its loading screen.
        /// </summary>
        private static void FailSafe(System.Exception exception)
        {
            s_failures++;
            Log.Error("[SCIEPort] Natural creature world spawn failed (" + s_failures + "/" + MaxFailuresBeforeDisable + "): " + exception);
            if (s_failures < MaxFailuresBeforeDisable)
            {
                return;
            }

            s_disabled = true;
            s_pending.Clear();
            Log.Error("[SCIEPort] Natural creature world spawn disabled for this session after repeated failures.");
        }

        /// <summary>
        /// Drains queued spawns on the main thread while the owning chunk is queryable.
        /// Bounded per frame so a wave of freshly generated chunks can never burst the frame.
        /// </summary>
        public static void Update()
        {
            if (s_disabled || s_pending.Count == 0 || !IsReady)
            {
                return;
            }

            if (!Ecology.EcologyRules.CanAuthoritativeWorldMutation(CommonLib.WorkType == WorkType.Client))
            {
                s_pending.Clear();
                return;
            }

            try
            {
                Drain();
            }
            catch (System.Exception exception)
            {
                FailSafe(exception);
            }
        }

        private static void Drain()
        {
            int spawned = 0;
            for (int i = 0; i < s_pending.Count && spawned < MaxSpawnsPerUpdate;)
            {
                PendingSpawn pending = s_pending[i];
                TerrainChunk chunk = s_terrain.Terrain.GetChunkAtCell(pending.CellX, pending.CellZ);
                // Contents must be complete before an entity may stand on them; anything
                // earlier (or a chunk that unloaded again) keeps the request queued.
                if (chunk == null || chunk.State < TerrainChunkState.InvalidLight)
                {
                    i++;
                    continue;
                }

                s_pending.RemoveAt(i);
                spawned++;

                int ceiling = pending.Ceiling;
                int tracked = pending.Rabbit ? s_trackedRabbits : s_trackedFoxes;
                if (Ecology.EcologyRules.ClampTrackedCount(tracked, ceiling) >= ceiling)
                {
                    continue;
                }

                int localX = pending.CellX - chunk.Origin.X;
                int localZ = pending.CellZ - chunk.Origin.Y;
                if (!Ecology.EcologyRules.IsInsideChunk(localX, localZ))
                {
                    continue;
                }
                if (!IsAir(chunk, localX, pending.CellY, localZ))
                {
                    continue;
                }

                if (pending.Rabbit)
                {
                    SpawnCreature(chunk, pending.Creature, localX, pending.CellY, localZ, ref s_trackedRabbits, ceiling);
                }
                else
                {
                    SpawnCreature(chunk, pending.Creature, localX, pending.CellY, localZ, ref s_trackedFoxes, ceiling);
                }
            }
        }

        private static bool TrySpawnFox(TerrainChunk chunk, Game.Random random)
        {
            int trackedFoxes = Ecology.EcologyRules.ClampTrackedCount(s_trackedFoxes, Ecology.EcologyRules.FoxCap);
            if (trackedFoxes >= Ecology.EcologyRules.FoxCap)
            {
                return false;
            }

            // Game.Random.Int is inclusive on both ends: LastChunkCellIndex (15), never 16.
            int cellX = random.Int(0, Ecology.EcologyRules.LastChunkCellIndex);
            int cellZ = random.Int(0, Ecology.EcologyRules.LastChunkCellIndex);
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

            if (!Ecology.EcologyRules.ChanceMet(random.Int(0, Ecology.EcologyRules.LastChanceRoll), Ecology.EcologyRules.FoxSpawnChancePercent))
            {
                return false;
            }

            return QueueSpawn(chunk, "SCIEPortFox", cellX, surfaceY + 1, cellZ, false, Ecology.EcologyRules.FoxCap);
        }

        private static bool TrySpawnRabbit(TerrainChunk chunk, Game.Random random)
        {
            int trackedRabbits = Ecology.EcologyRules.ClampTrackedCount(s_trackedRabbits, Ecology.EcologyRules.RabbitCap);
            if (trackedRabbits >= Ecology.EcologyRules.RabbitCap)
            {
                return false;
            }

            int cellX = random.Int(0, Ecology.EcologyRules.LastChunkCellIndex);
            int cellZ = random.Int(0, Ecology.EcologyRules.LastChunkCellIndex);
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

            if (!Ecology.EcologyRules.ChanceMet(random.Int(0, Ecology.EcologyRules.LastChanceRoll), Ecology.EcologyRules.RabbitSpawnChancePercent))
            {
                return false;
            }

            return QueueSpawn(chunk, "SCIEPortRabbit", cellX, surfaceY + 1, cellZ, true, Ecology.EcologyRules.RabbitCap);
        }

        private static bool QueueSpawn(TerrainChunk chunk, string creatureName, int localX, int localY, int localZ, bool rabbit, int ceiling)
        {
            if (!Ecology.EcologyRules.IsInsideChunk(localX, localZ) || localY < 1 || localY > 254)
            {
                return false;
            }

            // Bounded queue: a fresh-world generation wave must never grow without limit.
            if (s_pending.Count >= MaxPendingSpawns)
            {
                s_pending.RemoveAt(0);
            }

            s_pending.Add(new PendingSpawn
            {
                Creature = creatureName,
                CellX = chunk.Origin.X + localX,
                CellY = localY,
                CellZ = chunk.Origin.Y + localZ,
                Ceiling = ceiling,
                Rabbit = rabbit
            });
            return true;
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
                Log.Error("[SCIEPort] Natural creature spawn failed for " + creatureName + ": " + exception);
                return false;
            }
        }

        private static int FindSurface(TerrainChunk chunk, int x, int z)
        {
            if (chunk == null)
            {
                return -1;
            }

            // Never trust the caller: chunk arrays are 16x16 columns and 256 cells tall, and
            // an out-of-range read throws IndexOutOfRangeException straight out of the
            // terrain generator. Clamp first, then probe.
            x = Ecology.EcologyRules.ClampChunkCell(x);
            z = Ecology.EcologyRules.ClampChunkCell(z);
            try
            {
                int top = MathUtils.Min(MathUtils.Max(chunk.GetTopHeightFast(x, z), 1), 254);
                for (int y = top; y >= 1; y--)
                {
                    if (Terrain.ExtractContents(chunk.GetCellValueFast(x, y, z)) != 0)
                    {
                        return y;
                    }
                }
            }
            catch (System.Exception exception)
            {
                ReportProbeFailure(exception);
            }

            return -1;
        }

        private static bool IsAir(TerrainChunk chunk, int x, int y, int z)
        {
            if (chunk == null || !Ecology.EcologyRules.IsInsideChunk(x, z) || y < 0 || y > 255)
            {
                return false;
            }

            try
            {
                return Terrain.ExtractContents(chunk.GetCellValueFast(x, y, z)) == 0;
            }
            catch (System.Exception exception)
            {
                ReportProbeFailure(exception);
                return false;
            }
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
                    if (!Ecology.EcologyRules.IsInsideChunk(localX, localZ))
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

        // A broken chunk probe must not flood the log while a whole spawn area generates.
        private static void ReportProbeFailure(System.Exception exception)
        {
            if (s_probeFailuresLogged >= 3)
            {
                return;
            }

            s_probeFailuresLogged++;
            Log.Error("[SCIEPort] Creature chunk probe failed: " + exception);
        }
    }
}

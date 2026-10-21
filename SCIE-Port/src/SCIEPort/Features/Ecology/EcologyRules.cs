using System;

namespace SCIEPort.Features.Ecology
{
    /// <summary>
    /// Pure ecology rules shared by the SCIE-Port creature/plant runtime and the
    /// offline test suite. These rules deliberately have no dependency on the
    /// Survivalcraft assemblies so that determinism, population caps, per-chunk
    /// budgets, biome suitability and authority gating can be verified without a
    /// running game process.
    /// </summary>
    public static class EcologyRules
    {
        /// <summary>
        /// Population ceilings kept deliberately small so low-end Android devices
        /// never have to simulate a large animal population at once.
        /// </summary>
        public const int FoxCap = 3;

        /// <summary>Maximum concurrently tracked rabbit individuals.</summary>
        public const int RabbitCap = 6;

        /// <summary>
        /// A single chunk-generation pass may attempt at most this many creature
        /// spawns, and only inside the fresh chunk (never a whole-map scan).
        /// </summary>
        public const int MaxSpawnAttemptsPerChunk = 2;

        /// <summary>
        /// Ecological niche bound for foxes: fox prefers forested/plains cover.
        /// Temperature is the target seasonal temperature (already height
        /// adjusted); humidity is the seasonal humidity; the ground must be
        /// natural and there must be canopy cover.
        /// </summary>
        public static bool IsFoxHabitat(int temperature, int humidity, bool naturalGround, bool hasCanopyCover)
        {
            if (!naturalGround)
            {
                return false;
            }

            // Foxes are hardy but still need a survivable, not fully frozen
            // surface with some cover to feel correct on a fresh map.
            return temperature >= 2 && temperature <= 16 &&
                humidity >= 2 && humidity <= 12 &&
                hasCanopyCover;
        }

        /// <summary>
        /// Ecological niche bound for rabbits: rabbits favour milder, open
        /// grassland with at most light cover, so they require natural ground and
        /// the absence of dense canopy.
        /// </summary>
        public static bool IsRabbitHabitat(int temperature, int humidity, bool naturalGround, bool hasCanopyCover)
        {
            if (!naturalGround)
            {
                return false;
            }

            // Rabbits favour milder, open grassland with at most light cover.
            return temperature >= 4 && temperature <= 18 &&
                humidity >= 1 && humidity <= 10 &&
                !hasCanopyCover;
        }

        /// <summary>
        /// Only the authoritative side (single-player local or a listen/host
        /// server) may create entities or mutate the world. An ordinary client
        /// must never manufacture an unsynchronized creature.
        /// </summary>
        public static bool CanAuthoritativeWorldMutation(bool isClientWorkType)
        {
            return !isClientWorkType;
        }

        /// <summary>
        /// Returns how many individuals of one species should currently be
        /// present for the surrounding world, given how many of that species are
        /// alive and the ceiling. A negative or zero result requests no new
        /// spawns. The result never exceeds the species ceiling.
        /// </summary>
        public static int GetSpawnDemand(int aliveCount, int ceiling)
        {
            if (ceiling <= 0)
            {
                return 0;
            }

            int demand = ceiling - aliveCount;
            if (demand < 0)
            {
                return 0;
            }

            if (demand > MaxSpawnAttemptsPerChunk)
            {
                return MaxSpawnAttemptsPerChunk;
            }

            return demand;
        }

        /// <summary>
        /// Stable world/chunk derived spawn roll. Feeding the same inputs must
        /// always yield the same value so natural placement is reproducible from
        /// the world seed alone and never re-randomizes across re-entries.
        /// </summary>
        public static int GetChunkSeed(int worldSeed, int chunkX, int chunkZ)
        {
            unchecked
            {
                int seed = worldSeed;
                seed ^= chunkX * 73428767;
                seed = (seed << 13) | (int)((uint)seed >> 19);
                seed ^= chunkZ * 912931;
                seed = (seed << 7) | (int)((uint)seed >> 25);
                return seed;
            }
        }

        /// <summary>
        /// A fox may target a rabbit only when both are alive and the fox is
        /// within the pursuit radius, expressed in cell units. This is a pure
        /// eligibility predicate; the actual chase steering is delegated to the
        /// native Wolf-derived AI and is not reimplemented here.
        /// </summary>
        public static bool IsEligiblePredation(bool foxAlive, bool rabbitAlive, float squaredDistanceCells, float pursuitRadiusCells)
        {
            if (!foxAlive || !rabbitAlive || pursuitRadiusCells <= 0f)
            {
                return false;
            }

            float radiusSquared = pursuitRadiusCells * pursuitRadiusCells;
            return squaredDistanceCells >= 0f && squaredDistanceCells <= radiusSquared;
        }

        /// <summary>
        /// Guards against runaway growth across save/reload by clamping the
        /// tracked alive count to the ceiling. Never returns above the ceiling.
        /// </summary>
        public static int ClampTrackedCount(int trackedCount, int ceiling)
        {
            if (ceiling <= 0)
            {
                return 0;
            }

            return trackedCount < 0 ? 0 : (trackedCount > ceiling ? ceiling : trackedCount);
        }
    }
}

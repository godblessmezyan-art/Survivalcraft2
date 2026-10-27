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
        /// Highest valid local cell index on either horizontal axis of a terrain chunk.
        /// x25.01.17 indexes chunk arrays with <c>y + x * 256 + z * 256 * 16</c> and
        /// <c>x + z * 16</c>, so 16 is already outside them. <c>Game.Random.Int(min, max)</c>
        /// is inclusive on both ends, so rolling <c>Int(0, 16)</c> yields 16 roughly one time
        /// in seventeen and throws IndexOutOfRangeException inside the chunk.
        /// </summary>
        public const int LastChunkCellIndex = 15;

        /// <summary>Highest inclusive roll of the 0..99 band consumed by <see cref="ChanceMet"/>.</summary>
        public const int LastChanceRoll = 99;

        /// <summary>Forces a local chunk cell index into the only range the chunk arrays accept.</summary>
        public static int ClampChunkCell(int value)
        {
            return value < 0 ? 0 : (value > LastChunkCellIndex ? LastChunkCellIndex : value);
        }

        /// <summary>True only for a local chunk cell pair the chunk arrays can index safely.</summary>
        public static bool IsInsideChunk(int x, int z)
        {
            return x >= 0 && x <= LastChunkCellIndex && z >= 0 && z <= LastChunkCellIndex;
        }

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

        /// <summary>
        /// Base chance (out of 100) that a qualifying chunk will try to seed a fox.
        /// Foxes are deliberately rarer than their prey so a fresh map is not
        /// over-predated before the rabbit population establishes.
        /// </summary>
        public const int FoxSpawnChancePercent = 8;

        /// <summary>
        /// Base chance (out of 100) that a qualifying chunk will try to seed a
        /// rabbit. Rabbits are the forage base, so they are seeded more often than
        /// foxes.
        /// </summary>
        public const int RabbitSpawnChancePercent = 15;

        /// <summary>
        /// True when a deterministic roll in the range 0..99 falls below the given
        /// chance percentage. The roll and chance are both clamped defensively so a
        /// caller can never exceed the [0,100] band and trigger an out-of-range hit.
        /// </summary>
        public static bool ChanceMet(int roll0To99, int chancePercent)
        {
            // A draw outside the 0..99 band is invalid input and must never hit,
            // not even against a full chance. Guard the roll first, before any
            // chance short-circuit, so no caller can smuggle an out-of-range hit.
            if (roll0To99 < 0 || roll0To99 > 99)
            {
                return false;
            }
            if (chancePercent <= 0)
            {
                return false;
            }
            if (chancePercent >= 100)
            {
                return true;
            }
            return roll0To99 < chancePercent;
        }

        /// <summary>
        /// True while a chunk still has spawn attempts left in its per-chunk budget.
        /// The budget is the hard ceiling on creatures seeded by one chunk pass so a
        /// single generation never bursts the frame.
        /// </summary>
        public static bool CanSpawnWithinBudget(int attemptsSoFarThisChunk)
        {
            return attemptsSoFarThisChunk < MaxSpawnAttemptsPerChunk;
        }

        /// <summary>
        /// The <c>AutoChaseMask</c> we write onto the fox's inherited
        /// <c>ComponentChaseBehavior</c>. It is deliberately scoped to
        /// <c>LandOther</c> (the category rabbits inherit from the vanilla Alpaca
        /// parent), so the native chase AI treats the SCIE-Port rabbit as eligible
        /// prey. The value here is the single source of truth that the XDB override
        /// and the packaging validation must agree with.
        /// </summary>
        public const string FoxAutoChaseMask = "LandOther";

        /// <summary>
        /// The <c>Creature.Category</c> the SCIE-Port rabbit carries by inheritance
        /// from the vanilla Alpaca template. Fox predation works because this value
        /// is a member of <see cref="FoxAutoChaseMask"/>.
        /// </summary>
        public const string RabbitCreatureCategory = "LandOther";

        /// <summary>
        /// The vanilla wolf's own <c>ChaseNonPlayerProbability</c>, read directly
        /// from <c>Content.scpak</c> / <c>Database.xml</c>. It is very low (0.03)
        /// because real wolves mostly avoid non-player prey; that baseline is why an
        /// untouched fox inheritance would almost never hunt a rabbit.
        /// </summary>
        public const float VanillaWolfChaseNonPlayerProbability = 0.03f;

        /// <summary>
        /// The raised <c>ChaseNonPlayerProbability</c> we write onto the fox so the
        /// predator actually pursues rabbits at an ecologically meaningful rate. It
        /// must be strictly greater than <see cref="VanillaWolfChaseNonPlayerProbability"/>
        /// for predation to be observable, and stays a data value handled entirely by
        /// the native AI (no steering is reimplemented).
        /// </summary>
        public const float FoxChaseNonPlayerProbability = 0.35f;

        /// <summary>
        /// Mirrors the native <c>ComponentChaseBehavior</c> category-in-mask test:
        /// a creature is auto-chased when its <c>CreatureCategory</c> name appears in
        /// the comma-separated <c>AutoChaseMask</c> list exactly as the shipped DB
        /// serializes it. A numeric mask of <c>"0"</c> (the serialized <c>None</c>),
        /// and null/empty inputs, never match. Comparison is case-insensitive after
        /// trimming, matching how the enum names are written in <c>Database.xml</c>.
        /// </summary>
        public static bool IsCategoryChased(string autoChaseMask, string category)
        {
            if (string.IsNullOrEmpty(autoChaseMask) || string.IsNullOrEmpty(category))
            {
                return false;
            }

            string trimmedMask = autoChaseMask.Trim();

            // "0" is how the database serializes the CreatureCategory.None value;
            // such a predator auto-chases nothing.
            if (trimmedMask == "0")
            {
                return false;
            }

            string wanted = category.Trim();
            string[] entries = trimmedMask.Split(',');
            for (int i = 0; i < entries.Length; i++)
            {
                if (string.Equals(entries[i].Trim(), wanted, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

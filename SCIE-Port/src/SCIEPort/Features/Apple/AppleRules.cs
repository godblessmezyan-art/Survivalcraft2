using System;

namespace SCIEPort.Features.Apple
{
    /// <summary>
    /// Pure apple rules shared by the SCIE-Port apple runtime and the offline test
    /// suite. The apple slice reuses the two proven in-workspace plant idioms instead
    /// of inventing a new structure: it grows through staged development like the
    /// flax/sunflower crops (size lives in the low DATA bits) AND bears an edible
    /// fruit on a dedicated fruiting bit exactly like the audited <c>WildBerryPlantBlock</c>.
    /// A full sapling-to-trunk apple-tree is deliberately out of scope for this phase
    /// because it needs the private <c>Game.SubsystemSaplingBlockBehavior</c> schema,
    /// which has not been decompiled and validated yet. These rules carry no dependency
    /// on the Survivalcraft assemblies so growth pacing, the fruit-season gate,
    /// maturity, harvest yields and authority gating can be verified deterministically
    /// without a running game process. The apple is modelled as a temperate, full-sun
    /// perennial: cold hard stalls it, and it only ripens fruit in the autumn harvest
    /// season.
    /// </summary>
    public static class AppleRules
    {
        /// <summary>The number of developmental stages an apple plant walks through, zero based.</summary>
        public const int MaxAppleSize = 7;

        /// <summary>Mask covering the growth-size bits stored in an apple plant's DATA value.</summary>
        public const int SizeMask = 7;

        /// <summary>
        /// The single DATA bit that records whether a mature plant is currently bearing
        /// ripe fruit. It sits outside <see cref="SizeMask"/> so growth writes never
        /// clobber it, mirroring the audited WildBerry fruiting bit.
        /// </summary>
        public const int FruitMask = 8;

        /// <summary>
        /// Minimum skylight (out of 15) required above an apple plant before it may
        /// advance one growth step. Apples are full-sun fruiting plants, so they demand
        /// the same bright light as the heliotropic sunflower and will not ripen under a
        /// dense canopy or indoors.
        /// </summary>
        public const int MinLightToGrow = 10;

        /// <summary>Reads the growth size encoded in an apple plant DATA word.</summary>
        public static int GetSize(int data)
        {
            return data & SizeMask;
        }

        /// <summary>
        /// Writes a clamped growth size back into an apple plant DATA word, preserving
        /// the fruiting bit and every other bit.
        /// </summary>
        public static int SetSize(int data, int size)
        {
            return (data & ~SizeMask) | (Clamp(size, 0, MaxAppleSize) & SizeMask);
        }

        /// <summary>True when the given DATA word represents a fully mature plant.</summary>
        public static bool IsMature(int data)
        {
            return GetSize(data) >= MaxAppleSize;
        }

        /// <summary>True when the DATA word carries the ripe-fruit bit.</summary>
        public static bool GetFruiting(int data)
        {
            return (data & FruitMask) != 0;
        }

        /// <summary>
        /// Sets or clears the ripe-fruit bit while preserving the growth size and all
        /// other bits.
        /// </summary>
        public static int SetFruiting(int data, bool fruiting)
        {
            return fruiting ? data | FruitMask : data & ~FruitMask;
        }

        /// <summary>
        /// The number of poll passes required for one apple growth step under the
        /// current soil hydration, nitrogen and (height-adjusted) temperature. The base
        /// interval is slower than the annual sunflower because an apple is a perennial
        /// woody plant. Hydration and fertility speed it up, and because it is a
        /// temperate (not tropical warm-season) crop a hard frost stalls it hard while
        /// ordinary warmth does not accelerate it. Never returns below one.
        /// </summary>
        public static int GetAppleGrowthInterval(bool hydrated, int nitrogen, int temperature)
        {
            int interval = 10;
            if (hydrated)
            {
                interval -= 2;
            }
            if (nitrogen > 0)
            {
                interval -= 2;
            }
            if (temperature <= 2)
            {
                interval += 6;
            }
            return interval < 1 ? 1 : interval;
        }

        /// <summary>
        /// Only an upward-facing placement directly onto tilled soil may create a new
        /// apple plant, mirroring the proven grain/sunflower planting constraint.
        /// </summary>
        public static bool CanPlaceApple(bool isTopFace, bool isSoil)
        {
            return isTopFace && isSoil;
        }

        /// <summary>
        /// Whether a plant of the given maturity should be bearing ripe fruit this poll,
        /// given the current season's fruiting flag. Apples only ripen in the harvest
        /// season and only once the plant is fully mature, so the two conditions are
        /// combined. Callers compute <paramref name="isFruitSeason"/> from the game's
        /// season subsystem, keeping this rule free of any assembly dependency.
        /// </summary>
        public static bool ShouldBearFruit(bool isMaturePlant, bool isFruitSeason)
        {
            return isMaturePlant && isFruitSeason;
        }

        /// <summary>
        /// How many edible apples a ripe, fruiting plant yields on harvest. Apples are a
        /// heavier single fruit than sunflower seeds, so the yield is deliberately small.
        /// </summary>
        public static int GetAppleYield()
        {
            return 2;
        }

        /// <summary>
        /// How many replantable young plants a mature apple yields on top of its fruit,
        /// so a harvested stand can be re-sown without leaving the ground bare. Carried
        /// as a stage-zero plant item, exactly like the sunflower/flax replant drop.
        /// </summary>
        public static int GetReplantSaplingYield()
        {
            return 1;
        }

        /// <summary>
        /// Only the authoritative side (single-player local or a listen/host server) may
        /// advance world state such as mutating a growing or fruiting apple plant. An
        /// ordinary client must never drive growth that will not synchronize.
        /// </summary>
        public static bool CanAuthoritativeWorldMutation(bool isClientWorkType)
        {
            return !isClientWorkType;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }
            if (value > max)
            {
                return max;
            }
            return value;
        }
    }
}

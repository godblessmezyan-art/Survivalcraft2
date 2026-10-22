using System;

namespace SCIEPort.Features.Sunflower
{
    /// <summary>
    /// Pure sunflower rules shared by the SCIE-Port sunflower runtime and the offline
    /// test suite. Sunflower is a staged field crop (like flax and the vanilla grain
    /// blocks) so growth size lives in the low DATA bits. These rules carry no
    /// dependency on the Survivalcraft assemblies so that pacing, placement legality,
    /// maturity thresholds, harvest yields and authority gating can be verified
    /// deterministically without a running game process. The crop is modelled as a
    /// warm-season, sun-loving plant that is harvested for its edible seeds.
    /// </summary>
    public static class SunflowerRules
    {
        /// <summary>The number of developmental stages a sunflower walks through, zero based.</summary>
        public const int MaxSunflowerSize = 7;

        /// <summary>Mask covering the growth-size bits stored in a sunflower block's DATA value.</summary>
        public const int SizeMask = 7;

        /// <summary>
        /// Minimum skylight (out of 15) required above a sunflower before it may
        /// advance one growth step. Sunflowers are heliotropic, full-sun plants, so
        /// they demand more light than the shadeworthy flax and will not ripen under a
        /// tree canopy or indoors.
        /// </summary>
        public const int MinLightToGrow = 10;

        /// <summary>Reads the growth size encoded in a sunflower DATA word.</summary>
        public static int GetSize(int data)
        {
            return data & SizeMask;
        }

        /// <summary>
        /// Writes a clamped growth size back into a sunflower DATA word, preserving the
        /// other bits.
        /// </summary>
        public static int SetSize(int data, int size)
        {
            return (data & ~SizeMask) | (Clamp(size, 0, MaxSunflowerSize) & SizeMask);
        }

        /// <summary>True when the given DATA word represents a fully mature plant.</summary>
        public static bool IsMature(int data)
        {
            return GetSize(data) >= MaxSunflowerSize;
        }

        /// <summary>
        /// The number of poll passes required for one sunflower growth step under the
        /// current soil hydration, nitrogen and (height-adjusted) temperature. Wet,
        /// fertilized soil speeds ripening, and because the sunflower is a warm-season
        /// crop, temperature has a stronger effect than it does for flax: cold soil
        /// stalls it hard, warm soil pushes it along. Never returns below one.
        /// </summary>
        public static int GetSunflowerGrowthInterval(bool hydrated, int nitrogen, int temperature)
        {
            int interval = 8;
            if (hydrated)
            {
                interval -= 2;
            }
            if (nitrogen > 0)
            {
                interval -= 2;
            }
            if (temperature <= 4)
            {
                interval += 8;
            }
            else if (temperature >= 12)
            {
                interval -= 2;
            }
            return interval < 1 ? 1 : interval;
        }

        /// <summary>
        /// Only an upward-facing placement directly onto tilled soil may create a new
        /// sunflower plant, mirroring the vanilla/Kelly grain planting constraint.
        /// </summary>
        public static bool CanPlaceSunflower(bool isTopFace, bool isSoil)
        {
            return isTopFace && isSoil;
        }

        /// <summary>
        /// How many edible seeds a mature sunflower head yields on harvest. Seeds are
        /// the primary product (a small food, and the future feed for the batch-two
        /// chicken once that slice lands).
        /// </summary>
        public static int GetSeedYield()
        {
            return 4;
        }

        /// <summary>
        /// How many replantable seeds a mature sunflower yields on top of its edible
        /// crop, so a harvested patch can be re-sown without leaving the field bare.
        /// This is carried as a stage-zero plant item, exactly like the flax seed drop.
        /// </summary>
        public static int GetReplantSeedYield()
        {
            return 1;
        }

        /// <summary>
        /// Only the authoritative side (single-player local or a listen/host server)
        /// may advance world state such as mutating a growing sunflower. An ordinary
        /// client must never drive growth that will not synchronize.
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

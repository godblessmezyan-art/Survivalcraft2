using System;

namespace SCIEPort.Features.Flax
{
    /// <summary>
    /// Pure flax rules shared by the SCIE-Port flax runtime and the offline test
    /// suite. Flax is a staged field crop (like corn and the vanilla grain blocks)
    /// so growth size lives in the low DATA bits. These rules intentionally carry no
    /// dependency on the Survivalcraft assemblies so that pacing, placement legality,
    /// maturity thresholds, harvest yields and authority gating can be verified
    /// deterministically without a running game process.
    /// </summary>
    public static class FlaxRules
    {
        /// <summary>The number of developmental stages a flax plant walks through, zero based.</summary>
        public const int MaxFlaxSize = 7;

        /// <summary>Mask covering the growth-size bits stored in a flax block's DATA value.</summary>
        public const int SizeMask = 7;

        /// <summary>
        /// Minimum skylight (out of 15) required above a flax plant before it may
        /// advance one growth step. Flax is a bright-field crop and will not ripen in
        /// heavy shade or indoors.
        /// </summary>
        public const int MinLightToGrow = 8;

        /// <summary>Reads the growth size encoded in a flax DATA word.</summary>
        public static int GetSize(int data)
        {
            return data & SizeMask;
        }

        /// <summary>
        /// Writes a clamped growth size back into a flax DATA word, preserving the
        /// other bits.
        /// </summary>
        public static int SetSize(int data, int size)
        {
            return (data & ~SizeMask) | (Clamp(size, 0, MaxFlaxSize) & SizeMask);
        }

        /// <summary>True when the given DATA word represents a fully mature plant.</summary>
        public static bool IsMature(int data)
        {
            return GetSize(data) >= MaxFlaxSize;
        }

        /// <summary>
        /// The number of poll passes required for one flax growth step under the
        /// current soil hydration, nitrogen and (height-adjusted) temperature. Wet,
        /// fertilized and warm soil speeds ripening; cold soil slows it. Never returns
        /// below one.
        /// </summary>
        public static int GetFlaxGrowthInterval(bool hydrated, int nitrogen, int temperature)
        {
            int interval = 7;
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
                interval += 6;
            }
            else if (temperature >= 12)
            {
                interval -= 1;
            }
            return interval < 1 ? 1 : interval;
        }

        /// <summary>
        /// Only an upward-facing placement directly onto tilled soil may create a new
        /// flax plant, mirroring the vanilla/Kelly grain planting constraint.
        /// </summary>
        public static bool CanPlaceFlax(bool isTopFace, bool isSoil)
        {
            return isTopFace && isSoil;
        }

        /// <summary>
        /// How many fiber strands a mature flax plant yields on harvest. Fiber is the
        /// primary product (textiles / future rope and thread).
        /// </summary>
        public static int GetFiberYield()
        {
            return 2;
        }

        /// <summary>
        /// How many replantable seeds a mature flax plant yields alongside its fiber,
        /// so a harvested patch can be re-sown without leaving the field bare.
        /// </summary>
        public static int GetSeedYield()
        {
            return 1;
        }

        /// <summary>
        /// Only the authoritative side (single-player local or a listen/host server)
        /// may advance world state such as mutating a growing flax plant. An ordinary
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

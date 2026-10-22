using System;

namespace SCIEPort.Features.Crops
{
    /// <summary>
    /// Pure crop rules shared by the SCIE-Port corn runtime and the offline test
    /// suite. These rules deliberately have no dependency on the Survivalcraft
    /// assemblies so that growth pacing, placement legality, maturity thresholds
    /// and authority gating can be verified deterministically without a running
    /// game process.
    /// </summary>
    public static class CropRules
    {
        /// <summary>The number of developmental stages a corn stalk walks through, zero based.</summary>
        public const int MaxCropSize = 7;

        /// <summary>Mask covering the growth-size bits stored in a crop block's DATA value.</summary>
        public const int SizeMask = 7;

        /// <summary>
        /// Minimum skylight (out of 15) that must reach the cell directly above a
        /// stalk before it is allowed to advance one growth step. Corn is a sun
        /// crop and will not ripen in shade or indoors.
        /// </summary>
        public const int MinLightToGrow = 9;

        /// <summary>
        /// Encodes a growth size into the crop DATA word, preserving the other
        /// bits. The size is clamped to the valid stage range.
        /// </summary>
        public static int GetSize(int data)
        {
            return data & SizeMask;
        }

        /// <summary>Writes a clamped growth size back into a crop DATA word.</summary>
        public static int SetSize(int data, int size)
        {
            return (data & ~SizeMask) | (Clamp(size, 0, MaxCropSize) & SizeMask);
        }

        /// <summary>True when the given DATA word represents a fully mature stalk.</summary>
        public static bool IsMature(int data)
        {
            return GetSize(data) >= MaxCropSize;
        }

        /// <summary>
        /// The number of poll passes required for one corn growth step under the
        /// current soil hydration, nitrogen and (height-adjusted) temperature.
        /// Wet, fertilized and warm soil speeds ripening; cold soil slows it, and
        /// corn is a touch thirstier than soybean. Never returns below one.
        /// </summary>
        public static int GetCornGrowthInterval(bool hydrated, int nitrogen, int temperature)
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
                interval -= 1;
            }
            return interval < 1 ? 1 : interval;
        }

        /// <summary>
        /// Only an upward-facing placement directly onto a tilled soil block may
        /// create a new corn stalk. Mirrors the vanilla/Kelly planting constraint.
        /// </summary>
        public static bool CanPlaceCorn(bool isTopFace, bool isSoil)
        {
            return isTopFace && isSoil;
        }

        /// <summary>
        /// How many ears the mature stalk yields on harvest. Kept small and fixed
        /// so the drop is predictable for offline verification and balance.
        /// </summary>
        public static int GetEarYield()
        {
            return 1;
        }

        /// <summary>
        /// How many replantable seeds the mature stalk yields alongside its ear.
        /// </summary>
        public static int GetSeedYield()
        {
            return 2;
        }

        /// <summary>
        /// Only the authoritative side (single-player local or a listen/host
        /// server) may advance world state such as mutating a growing crop. An
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

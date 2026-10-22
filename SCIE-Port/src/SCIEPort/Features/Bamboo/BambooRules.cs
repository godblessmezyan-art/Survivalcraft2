using System;

namespace SCIEPort.Features.Bamboo
{
    /// <summary>
    /// Pure bamboo rules shared by the SCIE-Port bamboo runtime and the offline
    /// test suite. Like <see cref="Crops.CropRules"/> these deliberately avoid any
    /// dependency on the Survivalcraft assemblies so that the vertical segment
    /// stacking mechanic, placement legality, height cap and authority gating can
    /// be verified deterministically without a running game process.
    /// </summary>
    public static class BambooRules
    {
        /// <summary>
        /// The tallest a single bamboo column may grow, counted in stacked
        /// segments starting from the planted base. Keeps world mutation bounded.
        /// </summary>
        public const int MaxHeight = 8;

        /// <summary>
        /// Mask for the single DATA bit that marks the planted base segment of a
        /// bamboo column. Only the base segment drives the column upward so that a
        /// planted culm produces exactly one grower and cannot run away.
        /// </summary>
        public const int BaseFlagMask = 1;

        /// <summary>
        /// Minimum skylight (out of 15) that must reach the air cell directly above
        /// the current column top before a new segment is added. Bamboo is a shade
        /// tolerant runner and needs slightly less light than a sun crop.
        /// </summary>
        public const int MinLightToGrow = 8;

        /// <summary>True when the given DATA word marks the planted base segment.</summary>
        public static bool IsBase(int data)
        {
            return (data & BaseFlagMask) != 0;
        }

        /// <summary>Marks a DATA word as the planted base segment, preserving other bits.</summary>
        public static int MarkBase(int data)
        {
            return data | BaseFlagMask;
        }

        /// <summary>True for a stacked upper segment (not the planted base).</summary>
        public static bool IsUpperSegment(int data)
        {
            return !IsBase(data);
        }

        /// <summary>
        /// The number of poll passes required before the column extends one more
        /// segment. Wet soil hastens shooting; cold soil slows it. Never returns
        /// below one.
        /// </summary>
        public static int GetGrowthInterval(bool hydrated, int temperature)
        {
            int interval = 3;
            if (hydrated)
            {
                interval -= 1;
            }
            if (temperature <= 4)
            {
                interval += 3;
            }
            return interval < 1 ? 1 : interval;
        }

        /// <summary>
        /// Whether the column may extend another segment given the current height
        /// (segment count from base inclusive). Growth stops at the hard cap so a
        /// single culm never towers without bound.
        /// </summary>
        public static bool CanExtend(int currentHeight)
        {
            return currentHeight >= 1 && currentHeight < MaxHeight;
        }

        /// <summary>
        /// A new bamboo base may only be planted on an upward-facing placement over
        /// a valid substrate (tilled soil, sand or dirt). The caller resolves the
        /// substrate into the boolean.
        /// </summary>
        public static bool CanPlaceBase(bool isTopFace, bool hasValidSubstrate)
        {
            return isTopFace && hasValidSubstrate;
        }

        /// <summary>
        /// How many placeable bamboo items one broken segment yields. Each culm
        /// segment drops itself so chopping a column collects one item per block.
        /// </summary>
        public static int GetCulmYield()
        {
            return 1;
        }

        /// <summary>
        /// Only the authoritative side (single-player local or a listen/host
        /// server) may extend a growing bamboo column. An ordinary client must
        /// never mutate world state that will not synchronize back.
        /// </summary>
        public static bool CanAuthoritativeWorldMutation(bool isClientWorkType)
        {
            return !isClientWorkType;
        }
    }
}

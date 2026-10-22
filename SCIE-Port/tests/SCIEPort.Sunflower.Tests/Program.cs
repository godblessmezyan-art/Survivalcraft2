using System;
using SCIEPort.Features.Sunflower;

namespace SCIEPort.Sunflower.Tests
{
    internal static class Program
    {
        private static int s_assertions;

        private static int Main()
        {
            try
            {
                TestSizeRoundTrip();
                TestSizeClampsAndPreservesOtherBits();
                TestMaturityThreshold();
                TestSunflowerGrowthIntervals();
                TestLightRequirement();
                TestSunflowerPlacementRule();
                TestHarvestYields();
                TestAuthorityGate();
                Console.WriteLine("Sunflower crop rule tests passed: " + s_assertions);
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
                return 1;
            }
        }

        private static void TestSizeRoundTrip()
        {
            for (int size = 0; size <= SunflowerRules.MaxSunflowerSize; size++)
            {
                int data = SunflowerRules.SetSize(0, size);
                Assert(SunflowerRules.GetSize(data) == size, "Growth size must round-trip through DATA for stage " + size + ".");
            }
        }

        private static void TestSizeClampsAndPreservesOtherBits()
        {
            Assert(SunflowerRules.GetSize(SunflowerRules.SetSize(0, 99)) == SunflowerRules.MaxSunflowerSize, "Oversized stage must clamp to the maximum.");
            Assert(SunflowerRules.GetSize(SunflowerRules.SetSize(0, -3)) == 0, "Negative stage must clamp to zero.");

            // Bit 6 (0x40) sits outside the size mask and must survive a size write.
            int data = SunflowerRules.SetSize(0x40, 4);
            Assert(SunflowerRules.GetSize(data) == 4, "Size must be written into the low bits.");
            Assert((data & 0x40) != 0, "The SetSize write must preserve the unrelated 0x40 bit.");
        }

        private static void TestMaturityThreshold()
        {
            Assert(!SunflowerRules.IsMature(SunflowerRules.SetSize(0, SunflowerRules.MaxSunflowerSize - 1)), "A penultimate stage is not mature.");
            Assert(SunflowerRules.IsMature(SunflowerRules.SetSize(0, SunflowerRules.MaxSunflowerSize)), "The final stage is mature.");
        }

        private static void TestSunflowerGrowthIntervals()
        {
            Assert(SunflowerRules.GetSunflowerGrowthInterval(false, 0, 10) == 8, "Dry, unfertilized, mild sunflower is the base interval.");
            Assert(SunflowerRules.GetSunflowerGrowthInterval(true, 0, 10) == 6, "Hydration speeds ripening by two.");
            Assert(SunflowerRules.GetSunflowerGrowthInterval(true, 1, 10) == 4, "Hydration plus nitrogen is the fastest mild path.");
            Assert(SunflowerRules.GetSunflowerGrowthInterval(false, 0, 4) == 16, "Cold soil stalls the warm-season sunflower hard.");
            Assert(SunflowerRules.GetSunflowerGrowthInterval(false, 0, 12) == 6, "Warm soil pushes the sunflower along.");
            Assert(SunflowerRules.GetSunflowerGrowthInterval(true, 1, 12) == 2, "Ideal wet, fertile, warm soil ripens fast.");

            // The cold penalty is the design signature: a warm-season crop should be
            // punished far harder by cold than the mild-temperature interval is by warmth.
            Assert(SunflowerRules.GetSunflowerGrowthInterval(false, 0, 4) - 8 > 8 - SunflowerRules.GetSunflowerGrowthInterval(false, 0, 12),
                "Cold must slow the sunflower more than warmth speeds it.");
            Assert(SunflowerRules.GetSunflowerGrowthInterval(true, 1, 30) >= 1, "The interval must never drop below one.");
        }

        private static void TestLightRequirement()
        {
            // Sunflower is heliotropic and full-sun; it demands strictly more skylight
            // than the shade-tolerant flax (which grows at 8) and must stay below the
            // hard skylight maximum of 15 so open-field crops can still ripen.
            Assert(SunflowerRules.MinLightToGrow > 8, "Sunflower must demand more light than the shade-tolerant flax.");
            Assert(SunflowerRules.MinLightToGrow <= 15, "The light threshold must remain reachable in full open sunlight.");
        }

        private static void TestSunflowerPlacementRule()
        {
            Assert(SunflowerRules.CanPlaceSunflower(true, true), "An upward placement onto tilled soil plants a sunflower.");
            Assert(!SunflowerRules.CanPlaceSunflower(false, true), "A sideways placement may not plant a sunflower.");
            Assert(!SunflowerRules.CanPlaceSunflower(true, false), "A sunflower will not root on non-soil.");
            Assert(!SunflowerRules.CanPlaceSunflower(false, false), "Neither constraint alone is sufficient.");
        }

        private static void TestHarvestYields()
        {
            Assert(SunflowerRules.GetSeedYield() == 4, "A mature sunflower head yields four edible seeds.");
            Assert(SunflowerRules.GetReplantSeedYield() == 1, "A mature sunflower yields one replantable seed.");
            Assert(SunflowerRules.GetSeedYield() > 0, "The edible seed yield must be positive for the crop to be worth harvesting.");
        }

        private static void TestAuthorityGate()
        {
            Assert(SunflowerRules.CanAuthoritativeWorldMutation(false), "The authoritative side may advance sunflower growth.");
            Assert(!SunflowerRules.CanAuthoritativeWorldMutation(true), "An ordinary client must never advance sunflower growth.");
        }

        private static void Assert(bool condition, string message)
        {
            s_assertions++;
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}

using System;
using SCIEPort.Features.Flax;

namespace SCIEPort.Flax.Tests
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
                TestFlaxGrowthIntervals();
                TestFlaxPlacementRule();
                TestHarvestYields();
                TestAuthorityGate();
                Console.WriteLine("Flax crop rule tests passed: " + s_assertions);
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
            for (int size = 0; size <= FlaxRules.MaxFlaxSize; size++)
            {
                int data = FlaxRules.SetSize(0, size);
                Assert(FlaxRules.GetSize(data) == size, "Growth size must round-trip through DATA for stage " + size + ".");
            }
        }

        private static void TestSizeClampsAndPreservesOtherBits()
        {
            Assert(FlaxRules.GetSize(FlaxRules.SetSize(0, 99)) == FlaxRules.MaxFlaxSize, "Oversized stage must clamp to the maximum.");
            Assert(FlaxRules.GetSize(FlaxRules.SetSize(0, -3)) == 0, "Negative stage must clamp to zero.");

            // Bit 6 (0x40) sits outside the size mask and must survive a size write.
            int data = FlaxRules.SetSize(0x40, 4);
            Assert(FlaxRules.GetSize(data) == 4, "Size must be written into the low bits.");
            Assert((data & 0x40) != 0, "The SetSize write must preserve the unrelated 0x40 bit.");
        }

        private static void TestMaturityThreshold()
        {
            Assert(!FlaxRules.IsMature(FlaxRules.SetSize(0, FlaxRules.MaxFlaxSize - 1)), "A penultimate stage is not mature.");
            Assert(FlaxRules.IsMature(FlaxRules.SetSize(0, FlaxRules.MaxFlaxSize)), "The final stage is mature.");
        }

        private static void TestFlaxGrowthIntervals()
        {
            Assert(FlaxRules.GetFlaxGrowthInterval(false, 0, 10) == 7, "Dry, unfertilized, mild flax is the base interval.");
            Assert(FlaxRules.GetFlaxGrowthInterval(true, 0, 10) == 5, "Hydration speeds ripening by two.");
            Assert(FlaxRules.GetFlaxGrowthInterval(true, 1, 10) == 3, "Hydration plus nitrogen is the fastest mild path.");
            Assert(FlaxRules.GetFlaxGrowthInterval(false, 0, 4) == 13, "Cold soil slows flax markedly.");
            Assert(FlaxRules.GetFlaxGrowthInterval(false, 0, 12) == 6, "Warm soil nudges the interval down.");
            Assert(FlaxRules.GetFlaxGrowthInterval(true, 1, 12) == 2, "Ideal wet, fertile, warm soil ripens fast.");
            Assert(FlaxRules.GetFlaxGrowthInterval(true, 1, 30) >= 1, "The interval must never drop below one.");
        }

        private static void TestFlaxPlacementRule()
        {
            Assert(FlaxRules.CanPlaceFlax(true, true), "An upward placement onto tilled soil plants flax.");
            Assert(!FlaxRules.CanPlaceFlax(false, true), "A sideways placement may not plant flax.");
            Assert(!FlaxRules.CanPlaceFlax(true, false), "Flax will not root on non-soil.");
            Assert(!FlaxRules.CanPlaceFlax(false, false), "Neither constraint alone is sufficient.");
        }

        private static void TestHarvestYields()
        {
            Assert(FlaxRules.GetFiberYield() == 2, "A mature flax plant yields two fiber strands.");
            Assert(FlaxRules.GetSeedYield() == 1, "A mature flax plant yields one replantable seed.");
        }

        private static void TestAuthorityGate()
        {
            Assert(FlaxRules.CanAuthoritativeWorldMutation(false), "The authoritative side may advance flax growth.");
            Assert(!FlaxRules.CanAuthoritativeWorldMutation(true), "An ordinary client must never advance flax growth.");
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

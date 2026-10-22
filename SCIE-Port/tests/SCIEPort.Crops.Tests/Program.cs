using System;
using SCIEPort.Features.Crops;

namespace SCIEPort.Crops.Tests
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
                TestCornGrowthIntervals();
                TestCornPlacementRule();
                TestHarvestYields();
                TestAuthorityGate();
                Console.WriteLine("Corn crop rule tests passed: " + s_assertions);
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
            for (int size = 0; size <= CropRules.MaxCropSize; size++)
            {
                int data = CropRules.SetSize(0, size);
                Assert(CropRules.GetSize(data) == size, "Growth size must round-trip through DATA for stage " + size + ".");
            }
        }

        private static void TestSizeClampsAndPreservesOtherBits()
        {
            Assert(CropRules.GetSize(CropRules.SetSize(0, 99)) == CropRules.MaxCropSize, "Oversized stage must clamp to the maximum.");
            Assert(CropRules.GetSize(CropRules.SetSize(0, -3)) == 0, "Negative stage must clamp to zero.");

            // Bit 6 (0x40) sits outside the size mask and must survive a size write.
            int preserved = CropRules.SetSize(0x40, 5);
            Assert((preserved & 0x40) != 0, "SetSize must not clobber DATA bits above the size mask.");
            Assert(CropRules.GetSize(preserved) == 5, "SetSize must still apply the requested size.");
        }

        private static void TestMaturityThreshold()
        {
            Assert(!CropRules.IsMature(CropRules.SetSize(0, CropRules.MaxCropSize - 1)), "A stalk one stage short must not read as mature.");
            Assert(CropRules.IsMature(CropRules.SetSize(0, CropRules.MaxCropSize)), "The final stage must read as mature.");
        }

        private static void TestCornGrowthIntervals()
        {
            Assert(CropRules.GetCornGrowthInterval(false, 0, 10) == 8, "Dry, depleted, mild soil is the slow base cadence.");
            Assert(CropRules.GetCornGrowthInterval(true, 0, 10) == 6, "Hydration must speed ripening.");
            Assert(CropRules.GetCornGrowthInterval(true, 2, 10) == 4, "Hydrated and fertilized soil is fastest.");
            Assert(CropRules.GetCornGrowthInterval(true, 2, 4) == 12, "Cold soil adds a large growth penalty.");
            Assert(CropRules.GetCornGrowthInterval(true, 2, 15) == 3, "Warm soil trims one poll step.");
        }

        private static void TestCornPlacementRule()
        {
            Assert(CropRules.CanPlaceCorn(true, true), "Corn must plant on a soil top face.");
            Assert(!CropRules.CanPlaceCorn(false, true), "Corn must not plant on a side face.");
            Assert(!CropRules.CanPlaceCorn(true, false), "Corn must not plant outside soil.");
        }

        private static void TestHarvestYields()
        {
            Assert(CropRules.GetEarYield() >= 1, "A mature stalk must yield at least one ear.");
            Assert(CropRules.GetSeedYield() >= 1, "A mature stalk must yield replantable seed.");
        }

        private static void TestAuthorityGate()
        {
            Assert(CropRules.CanAuthoritativeWorldMutation(false), "Local/host must be allowed to advance crop growth.");
            Assert(!CropRules.CanAuthoritativeWorldMutation(true), "A normal client must never drive unsynchronized growth.");
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

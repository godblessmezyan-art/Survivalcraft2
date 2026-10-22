using System;
using SCIEPort.Features.Apple;

namespace SCIEPort.Apple.Tests
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
                TestFruitingRoundTrip();
                TestFruitingBitAndSizeAreIndependent();
                TestMaturityThreshold();
                TestAppleGrowthIntervals();
                TestTemperateSignatureHasNoWarmBoost();
                TestLightRequirement();
                TestFruitSeasonGate();
                TestApplePlacementRule();
                TestHarvestYields();
                TestAuthorityGate();
                Console.WriteLine("Apple plant rule tests passed: " + s_assertions);
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
            for (int size = 0; size <= AppleRules.MaxAppleSize; size++)
            {
                int data = AppleRules.SetSize(0, size);
                Assert(AppleRules.GetSize(data) == size, "Growth size must round-trip through DATA for stage " + size + ".");
            }
        }

        private static void TestSizeClampsAndPreservesOtherBits()
        {
            Assert(AppleRules.GetSize(AppleRules.SetSize(0, 99)) == AppleRules.MaxAppleSize, "Oversized stage must clamp to the maximum.");
            Assert(AppleRules.GetSize(AppleRules.SetSize(0, -3)) == 0, "Negative stage must clamp to zero.");

            // Bit 6 (0x40) sits outside both the size mask and the fruit bit and must
            // survive a size write.
            int data = AppleRules.SetSize(0x40, 4);
            Assert(AppleRules.GetSize(data) == 4, "Size must be written into the low bits.");
            Assert((data & 0x40) != 0, "The SetSize write must preserve the unrelated 0x40 bit.");
        }

        private static void TestFruitingRoundTrip()
        {
            Assert(!AppleRules.GetFruiting(AppleRules.SetFruiting(0, false)), "Clearing the fruit bit yields a non-fruiting plant.");
            Assert(AppleRules.GetFruiting(AppleRules.SetFruiting(0, true)), "Setting the fruit bit yields a fruiting plant.");
            Assert((AppleRules.SetFruiting(0, true) & AppleRules.FruitMask) != 0, "The fruit bit lives at FruitMask.");
            Assert((AppleRules.SetFruiting(AppleRules.FruitMask, false) & AppleRules.FruitMask) == 0, "Clearing removes only the fruit bit.");
        }

        private static void TestFruitingBitAndSizeAreIndependent()
        {
            // Setting fruit must not disturb the growth size, and vice versa, because the
            // two live in disjoint DATA bits (mirrors the audited WildBerry layout).
            int data = AppleRules.SetSize(0, 5);
            data = AppleRules.SetFruiting(data, true);
            Assert(AppleRules.GetSize(data) == 5, "Setting the fruit bit must preserve the growth size.");
            Assert(AppleRules.GetFruiting(data), "The fruit bit must be set.");

            data = AppleRules.SetSize(data, 2);
            Assert(AppleRules.GetSize(data) == 2, "A later size write must apply.");
            Assert(AppleRules.GetFruiting(data), "A later size write must preserve the fruit bit.");

            data = AppleRules.SetFruiting(data, false);
            Assert(AppleRules.GetSize(data) == 2, "Clearing the fruit bit must preserve the size.");
            Assert(!AppleRules.GetFruiting(data), "The fruit bit must be cleared.");
        }

        private static void TestMaturityThreshold()
        {
            Assert(!AppleRules.IsMature(AppleRules.SetSize(0, AppleRules.MaxAppleSize - 1)), "A penultimate stage is not mature.");
            Assert(AppleRules.IsMature(AppleRules.SetSize(0, AppleRules.MaxAppleSize)), "The final stage is mature.");
            Assert(AppleRules.IsMature(AppleRules.SetFruiting(AppleRules.SetSize(0, AppleRules.MaxAppleSize), true)), "A mature fruiting plant is still mature.");
        }

        private static void TestAppleGrowthIntervals()
        {
            Assert(AppleRules.GetAppleGrowthInterval(false, 0, 10) == 10, "Dry, unfertilized, mild apple is the slow perennial base interval.");
            Assert(AppleRules.GetAppleGrowthInterval(true, 0, 10) == 8, "Hydration speeds growth by two.");
            Assert(AppleRules.GetAppleGrowthInterval(true, 1, 10) == 6, "Hydration plus nitrogen is the fastest mild path.");
            Assert(AppleRules.GetAppleGrowthInterval(false, 0, 2) == 16, "A hard frost stalls the temperate apple plant.");
            Assert(AppleRules.GetAppleGrowthInterval(true, 1, 30) >= 1, "The interval must never drop below one.");

            // A woody perennial starts slower than the annual sunflower (base 8), so the
            // mild apple interval must be strictly larger than the mild sunflower base.
            Assert(AppleRules.GetAppleGrowthInterval(false, 0, 10) > 8, "Apple must grow slower at base than the annual sunflower.");
        }

        private static void TestTemperateSignatureHasNoWarmBoost()
        {
            // The apple is a temperate crop: unlike the warm-season sunflower, ordinary
            // summer warmth does NOT accelerate it. Only cold punishes it. So the mild and
            // hot intervals must be identical, and both faster than the frosted interval.
            Assert(AppleRules.GetAppleGrowthInterval(false, 0, 10) == AppleRules.GetAppleGrowthInterval(false, 0, 25),
                "Warmth must not accelerate a temperate apple beyond its mild base.");
            Assert(AppleRules.GetAppleGrowthInterval(false, 0, 2) > AppleRules.GetAppleGrowthInterval(false, 0, 10),
                "Cold must slow the apple plant relative to mild conditions.");
        }

        private static void TestLightRequirement()
        {
            // Apples are full-sun fruiting plants: they demand at least as much skylight
            // as the sunflower and must stay below the hard skylight maximum of 15 so an
            // open-grown plant can still ripen.
            Assert(AppleRules.MinLightToGrow > 8, "Apple must demand more light than the shade-tolerant flax.");
            Assert(AppleRules.MinLightToGrow <= 15, "The light threshold must remain reachable in full open sunlight.");
            Assert(AppleRules.MinLightToGrow == 10, "Apple matches the full-sun sunflower threshold.");
        }

        private static void TestFruitSeasonGate()
        {
            // A plant only bears ripe fruit when it is BOTH mature AND in the harvest
            // season; neither condition alone is sufficient.
            Assert(AppleRules.ShouldBearFruit(true, true), "A mature plant in fruit season bears fruit.");
            Assert(!AppleRules.ShouldBearFruit(true, false), "A mature plant outside fruit season does not bear fruit.");
            Assert(!AppleRules.ShouldBearFruit(false, true), "An immature plant bears no fruit even in season.");
            Assert(!AppleRules.ShouldBearFruit(false, false), "An immature plant out of season bears no fruit.");
        }

        private static void TestApplePlacementRule()
        {
            Assert(AppleRules.CanPlaceApple(true, true), "An upward placement onto tilled soil plants an apple.");
            Assert(!AppleRules.CanPlaceApple(false, true), "A sideways placement may not plant an apple.");
            Assert(!AppleRules.CanPlaceApple(true, false), "An apple will not root on non-soil.");
            Assert(!AppleRules.CanPlaceApple(false, false), "Neither constraint alone is sufficient.");
        }

        private static void TestHarvestYields()
        {
            Assert(AppleRules.GetAppleYield() == 2, "A fruiting apple plant yields two apples.");
            Assert(AppleRules.GetReplantSaplingYield() == 1, "A mature apple yields one replantable young plant.");
            Assert(AppleRules.GetAppleYield() > 0, "The edible apple yield must be positive for the plant to be worth harvesting.");
        }

        private static void TestAuthorityGate()
        {
            Assert(AppleRules.CanAuthoritativeWorldMutation(false), "The authoritative side may advance apple growth and fruiting.");
            Assert(!AppleRules.CanAuthoritativeWorldMutation(true), "An ordinary client must never advance apple growth or fruiting.");
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

using System;
using SCIEPort.Features.Bamboo;

namespace SCIEPort.Bamboo.Tests
{
    internal static class Program
    {
        private static int s_assertions;

        private static int Main()
        {
            try
            {
                TestBaseFlagRoundTrip();
                TestBaseFlagPreservesOtherBits();
                TestUpperSegmentDetection();
                TestHeightCap();
                TestGrowthIntervals();
                TestPlacementRule();
                TestCulmYield();
                TestAuthorityGate();
                Console.WriteLine("Bamboo rule tests passed: " + s_assertions);
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
                return 1;
            }
        }

        private static void TestBaseFlagRoundTrip()
        {
            Assert(BambooRules.IsBase(BambooRules.MarkBase(0)), "A marked DATA word must report as the planted base.");
            Assert(!BambooRules.IsBase(0), "An unmarked DATA word must not be treated as a base.");
            Assert(BambooRules.MarkBase(BambooRules.MarkBase(0)) == BambooRules.MarkBase(0), "Marking the base twice must be idempotent.");
        }

        private static void TestBaseFlagPreservesOtherBits()
        {
            int data = BambooRules.MarkBase(0x40);
            Assert((data & 0x40) != 0, "Marking the base must preserve unrelated DATA bits.");
            Assert(BambooRules.IsBase(data), "The base bit must remain set when other bits are present.");
        }

        private static void TestUpperSegmentDetection()
        {
            Assert(BambooRules.IsUpperSegment(0), "An unmarked segment is an upper culm.");
            Assert(!BambooRules.IsUpperSegment(BambooRules.MarkBase(0)), "The planted base is not an upper segment.");
        }

        private static void TestHeightCap()
        {
            Assert(BambooRules.CanExtend(1), "A lone base may extend upward.");
            Assert(BambooRules.CanExtend(BambooRules.MaxHeight - 1), "One below the cap may extend.");
            Assert(!BambooRules.CanExtend(BambooRules.MaxHeight), "A column at the cap must stop extending.");
            Assert(!BambooRules.CanExtend(BambooRules.MaxHeight + 5), "Oversized columns never extend.");
            Assert(!BambooRules.CanExtend(0), "A zero-height column is not a valid grower.");
        }

        private static void TestGrowthIntervals()
        {
            Assert(BambooRules.GetGrowthInterval(false, 10) == 3, "Dry, mild soil shoots on the base interval.");
            Assert(BambooRules.GetGrowthInterval(true, 10) == 2, "Hydration hastens shooting.");
            Assert(BambooRules.GetGrowthInterval(false, 4) == 6, "Cold soil slows shooting.");
            Assert(BambooRules.GetGrowthInterval(true, 4) == 5, "Cold outweighs hydration but still slows the net interval.");
            Assert(BambooRules.GetGrowthInterval(true, 0) == 5, "Extreme cold cannot push the interval below one.");
            Assert(BambooRules.GetGrowthInterval(false, 30) >= 1, "The interval must never drop below one poll pass.");
        }

        private static void TestPlacementRule()
        {
            Assert(BambooRules.CanPlaceBase(true, true), "An upward placement onto valid substrate plants a base.");
            Assert(!BambooRules.CanPlaceBase(false, true), "A sideways placement may not plant bamboo.");
            Assert(!BambooRules.CanPlaceBase(true, false), "Bamboo will not root on an invalid substrate.");
            Assert(!BambooRules.CanPlaceBase(false, false), "Neither constraint alone is sufficient.");
        }

        private static void TestCulmYield()
        {
            Assert(BambooRules.GetCulmYield() == 1, "Each broken segment yields exactly one bamboo item.");
        }

        private static void TestAuthorityGate()
        {
            Assert(BambooRules.CanAuthoritativeWorldMutation(false), "The authoritative side may extend the column.");
            Assert(!BambooRules.CanAuthoritativeWorldMutation(true), "An ordinary client must never mutate bamboo growth.");
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

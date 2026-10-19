using System;
using SCIEPort.Features.KellyPlants;

namespace SCIEPort.KellyPlants.Tests
{
    internal static class Program
    {
        private static int s_assertions;

        private static int Main()
        {
            try
            {
                TestChunkSeedDeterminism();
                TestChunkSeedUsesWorldSeed();
                TestSoybeanGrowthIntervals();
                TestSoybeanPlacementRule();
                TestExistingWorldRainSpawnIsDisabled();
                Console.WriteLine("Kelly plant rule tests passed: " + s_assertions);
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
                return 1;
            }
        }

        private static void TestChunkSeedDeterminism()
        {
            int first = KellyPlantRules.GetChunkSeed(12345, -14, 27);
            int second = KellyPlantRules.GetChunkSeed(12345, -14, 27);
            Assert(first == second, "Chunk seed must be stable for the same world and chunk.");
        }

        private static void TestChunkSeedUsesWorldSeed()
        {
            int first = KellyPlantRules.GetChunkSeed(12345, 8, -5);
            int second = KellyPlantRules.GetChunkSeed(12346, 8, -5);
            int third = KellyPlantRules.GetChunkSeed(12345, 9, -5);
            Assert(first != second, "Changing world seed must change Kelly chunk seed.");
            Assert(first != third, "Changing chunk coordinates must change Kelly chunk seed.");
        }

        private static void TestSoybeanGrowthIntervals()
        {
            Assert(KellyPlantRules.GetSoybeanGrowthInterval(false, 0, 10) == 6, "Dry depleted soil interval.");
            Assert(KellyPlantRules.GetSoybeanGrowthInterval(true, 0, 10) == 4, "Hydrated soil interval.");
            Assert(KellyPlantRules.GetSoybeanGrowthInterval(true, 2, 10) == 2, "Hydrated fertilized soil interval.");
            Assert(KellyPlantRules.GetSoybeanGrowthInterval(true, 2, 4) == 8, "Cold weather interval penalty.");
        }

        private static void TestSoybeanPlacementRule()
        {
            Assert(KellyPlantRules.CanPlaceSoybean(true, true), "Soybean must plant on a soil top face.");
            Assert(!KellyPlantRules.CanPlaceSoybean(false, true), "Soybean must not plant on a side face.");
            Assert(!KellyPlantRules.CanPlaceSoybean(true, false), "Soybean must not plant outside soil.");
        }

        private static void TestExistingWorldRainSpawnIsDisabled()
        {
            Assert(!KellyPlantRules.AllowPostRainSpawning, "Rain must not place mushrooms into loaded player areas.");
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

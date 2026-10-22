using System;
using SCIEPort.Features.Ecology;

namespace SCIEPort.Ecology.Tests
{
    internal static class Program
    {
        private static int s_assertions;

        private static int Main()
        {
            try
            {
                TestChunkSeedDeterminism();
                TestChunkSeedUsesWorldSeedAndCoordinates();
                TestSpawnDemandRespectsCeilingAndBudget();
                TestClampPreventsOverpopulation();
                TestAuthorityGate();
                TestHabitatSuitability();
                TestPredationEligibility();
                TestSpawnChanceAndBudget();
                TestPredationWiring();
                Console.WriteLine("Ecology rule tests passed: " + s_assertions);
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
            int first = EcologyRules.GetChunkSeed(424242, -14, 27);
            int second = EcologyRules.GetChunkSeed(424242, -14, 27);
            Assert(first == second, "Ecology chunk seed must be stable for the same world and chunk.");
        }

        private static void TestChunkSeedUsesWorldSeedAndCoordinates()
        {
            int a = EcologyRules.GetChunkSeed(100, 8, -5);
            int b = EcologyRules.GetChunkSeed(101, 8, -5);
            int c = EcologyRules.GetChunkSeed(100, 9, -5);
            int d = EcologyRules.GetChunkSeed(100, 8, -6);
            Assert(a != b, "Changing the world seed must change the ecology chunk seed.");
            Assert(a != c, "Changing chunk X must change the ecology chunk seed.");
            Assert(a != d, "Changing chunk Z must change the ecology chunk seed.");
        }

        private static void TestSpawnDemandRespectsCeilingAndBudget()
        {
            Assert(EcologyRules.GetSpawnDemand(0, EcologyRules.FoxCap) == EcologyRules.MaxSpawnAttemptsPerChunk,
                "Empty population must request at most the per-chunk budget, not the whole ceiling at once.");
            Assert(EcologyRules.GetSpawnDemand(2, EcologyRules.FoxCap) == 1,
                "One below ceiling must request exactly one spawn.");
            Assert(EcologyRules.GetSpawnDemand(3, EcologyRules.FoxCap) == 0,
                "A full population must request no spawns.");
            Assert(EcologyRules.GetSpawnDemand(10, EcologyRules.FoxCap) == 0,
                "An over-full tracked count must still request no spawns.");
            Assert(EcologyRules.GetSpawnDemand(0, 0) == 0,
                "A zero ceiling must disable spawning entirely.");
        }

        private static void TestClampPreventsOverpopulation()
        {
            Assert(EcologyRules.ClampTrackedCount(99, EcologyRules.RabbitCap) == EcologyRules.RabbitCap,
                "Tracked count must clamp to the ceiling to stop unbounded growth after reload.");
            Assert(EcologyRules.ClampTrackedCount(-5, EcologyRules.RabbitCap) == 0,
                "A negative tracked count must clamp to zero.");
            Assert(EcologyRules.ClampTrackedCount(2, EcologyRules.RabbitCap) == 2,
                "An in-range count must be preserved.");
        }

        private static void TestAuthorityGate()
        {
            Assert(EcologyRules.CanAuthoritativeWorldMutation(false),
                "Local and server (non-client) authority must permit world mutation.");
            Assert(!EcologyRules.CanAuthoritativeWorldMutation(true),
                "An ordinary client must never create entities or mutate the world.");
        }

        private static void TestHabitatSuitability()
        {
            Assert(EcologyRules.IsFoxHabitat(10, 6, true, true), "Temperate forested ground is fox habitat.");
            Assert(!EcologyRules.IsFoxHabitat(10, 6, false, true), "Fox cannot spawn on non-ground.");
            Assert(!EcologyRules.IsFoxHabitat(20, 6, true, true), "Too hot is outside fox temperature bounds.");
            Assert(EcologyRules.IsRabbitHabitat(10, 5, true, false), "Open mild grassland is rabbit habitat.");
            Assert(!EcologyRules.IsRabbitHabitat(10, 5, true, true), "Dense canopy is outside open-grassland rabbit preference.");
            Assert(!EcologyRules.IsRabbitHabitat(0, 5, true, false), "Frozen surface is outside rabbit temperature bounds.");
        }

        private static void TestPredationEligibility()
        {
            Assert(EcologyRules.IsEligiblePredation(true, true, 16f, 5f), "A rabbit within radius is eligible prey.");
            Assert(!EcologyRules.IsEligiblePredation(true, true, 36f, 5f), "A rabbit outside the pursuit radius is not eligible.");
            Assert(!EcologyRules.IsEligiblePredation(false, true, 1f, 5f), "A dead fox cannot hunt.");
            Assert(!EcologyRules.IsEligiblePredation(true, false, 1f, 5f), "A dead rabbit cannot be targeted.");
            Assert(!EcologyRules.IsEligiblePredation(true, true, 1f, 0f), "A non-positive pursuit radius disables predation.");
        }

        private static void TestSpawnChanceAndBudget()
        {
            Assert(EcologyRules.ChanceMet(0, 50), "A zero roll must meet a mid-band chance.");
            Assert(EcologyRules.ChanceMet(49, 50), "A roll one below the chance still hits.");
            Assert(!EcologyRules.ChanceMet(50, 50), "A roll equal to the chance is exclusive and must not hit.");
            Assert(!EcologyRules.ChanceMet(99, 0), "A zero chance must never hit.");
            Assert(EcologyRules.ChanceMet(0, 100), "A full chance must always hit.");
            Assert(!EcologyRules.ChanceMet(-1, 100), "A roll below the band must not hit.");
            Assert(!EcologyRules.ChanceMet(100, 100), "A roll above the band must not hit.");
            Assert(EcologyRules.FoxSpawnChancePercent < EcologyRules.RabbitSpawnChancePercent,
                "The predator must be seeded more rarely than its prey.");

            Assert(EcologyRules.CanSpawnWithinBudget(0), "A fresh chunk has budget for a first attempt.");
            Assert(EcologyRules.CanSpawnWithinBudget(EcologyRules.MaxSpawnAttemptsPerChunk - 1),
                "The final allowed attempt stays within budget.");
            Assert(!EcologyRules.CanSpawnWithinBudget(EcologyRules.MaxSpawnAttemptsPerChunk),
                "Attempts at the per-chunk ceiling must be blocked.");
            Assert(!EcologyRules.CanSpawnWithinBudget(EcologyRules.MaxSpawnAttemptsPerChunk + 5),
                "Attempts beyond the ceiling must be blocked.");
        }

        private static void TestPredationWiring()
        {
            Assert(EcologyRules.IsCategoryChased("LandOther", "LandOther"),
                "An exact single-category mask must chase that category.");
            Assert(EcologyRules.IsCategoryChased("LandPredator, LandOther, Bird", "LandOther"),
                "A membership check must find a category inside a comma-separated mask.");
            Assert(EcologyRules.IsCategoryChased("LandOther, Bird", "Bird"),
                "The last entry of a mask must still match.");
            Assert(EcologyRules.IsCategoryChased("landother", "LandOther"),
                "Category matching is case-insensitive, mirroring the enum names in the DB.");
            Assert(!EcologyRules.IsCategoryChased("0", "LandOther"),
                "The serialized None mask (0) must chase nothing.");
            Assert(!EcologyRules.IsCategoryChased("LandPredator", "LandOther"),
                "A mask that omits the prey category must not chase it.");
            Assert(!EcologyRules.IsCategoryChased(null, "LandOther"),
                "A null mask must not chase.");
            Assert(!EcologyRules.IsCategoryChased("LandOther", ""),
                "An empty category must never match.");

            Assert(EcologyRules.IsCategoryChased(EcologyRules.FoxAutoChaseMask, EcologyRules.RabbitCreatureCategory),
                "The fox AutoChaseMask must cover the rabbit's inherited category, or predation never targets rabbits.");
            Assert(EcologyRules.FoxChaseNonPlayerProbability > EcologyRules.VanillaWolfChaseNonPlayerProbability,
                "The fox chase probability must be raised above the vanilla wolf baseline to be observable.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception("Assertion failed: " + message);
            }

            s_assertions++;
        }
    }
}

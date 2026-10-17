using System;
using System.Collections.Generic;
using BuilderAssistant.Core;
using BuilderAssistant.Core.Blueprints;
using BuilderAssistant.Core.BuildEngine;
using BuilderAssistant.Core.Safety;
using BuilderAssistant.Core.Transactions;
using BuilderAssistant.Features.Generators;

namespace BuilderAssistant.Tests
{
    /// <summary>
    /// In-memory stand-in for the live terrain so every safety rule and the
    /// plan/execute/undo lifecycle can be proven without launching the game.
    /// Mirrors GameTerrainGateway semantics: y-bounds, chunk "loaded" (blocked)
    /// set, and an extent limit.
    /// </summary>
    internal sealed class FakeTerrain : ITerrainGateway
    {
        public readonly HashSet<CellPos> Blocked = new HashSet<CellPos>();
        private readonly Dictionary<CellPos, int> _values = new Dictionary<CellPos, int>();

        public int Writes;
        public int Reads;
        public int ExtentLimit = 4096;

        public bool IsCellEditable(CellPos cell)
        {
            if (!WorldBounds.IsYValid(cell.Y))
            {
                return false;
            }
            if (Blocked.Contains(cell))
            {
                return false;
            }
            if (cell.X < -ExtentLimit || cell.X > ExtentLimit ||
                cell.Z < -ExtentLimit || cell.Z > ExtentLimit)
            {
                return false;
            }
            return true;
        }

        public bool IsCellAir(CellPos cell)
        {
            int value;
            return TryGetCellValue(cell, out value) && value == 0;
        }

        public bool TryGetCellValue(CellPos cell, out int value)
        {
            value = 0;
            if (!IsCellEditable(cell))
            {
                return false;
            }
            Reads++;
            int existing;
            if (_values.TryGetValue(cell, out existing))
            {
                value = existing;
            }
            return true;
        }

        public bool SetCellValue(CellPos cell, int value)
        {
            if (!IsCellEditable(cell))
            {
                return false;
            }
            Writes++;
            _values[cell] = value;
            return true;
        }

        // Test helpers that bypass the write counter (simulate external world state).
        public int ValueAt(CellPos cell)
        {
            int v;
            return _values.TryGetValue(cell, out v) ? v : 0;
        }

        public void Occupy(CellPos cell, int value)
        {
            _values[cell] = value;
        }
    }

    /// <summary>
    /// Deterministic fake resolver. Every real material key maps to a distinct
    /// non-zero value; the special key "Nonexistent" fails to resolve, which is
    /// how a missing vanilla block would behave in the real palette.
    /// </summary>
    internal sealed class FakeResolver : IBlockValueResolver
    {
        public bool TryResolve(string materialKey, out int value)
        {
            value = 0;
            if (string.IsNullOrEmpty(materialKey) || materialKey == "Nonexistent")
            {
                return false;
            }
            int hash = materialKey.GetHashCode() & 0x7FFFFFFF;
            value = 1000 + (hash % 90000);
            if (value == 0)
            {
                value = 1;
            }
            return true;
        }
    }

    internal static class Program
    {
        private static int _failures;
        private static int _checks;

        private static int Main()
        {
            Blueprint blueprint = BuildingGeneratorRegistry.Default.Generate(new GenerationContext(0));

            TestCottageShape(blueprint);
            TestDeterministicRebuild();
            TestAllKeysResolvable(blueprint);
            TestPlanRejectsOutOfBoundsY();
            TestPlanRejectsNonAirByDefault();
            TestPlanAllowsOverrideWhenFlagged();
            TestPlanRejectsCountCap();
            TestPlanRejectsAxisCap();
            TestPlanRejectsUnknownMaterial();
            TestExecuteThenUndoPreservesLaterEdits();
            TestRollbackOnMidBuildFailure();
            TestCancelMidBuildRollsBack();
            TestRotationPreservesBlueprint();
            TestMirrorIsInvolution();

            Console.WriteLine();
            Console.WriteLine(_failures == 0
                ? "ALL " + _checks + " CHECKS PASSED"
                : (_checks - _failures) + "/" + _checks + " checks passed, " + _failures + " FAILED");
            return _failures == 0 ? 0 : 1;
        }

        private static void Check(bool condition, string label)
        {
            _checks++;
            if (condition)
            {
                Console.WriteLine("  PASS  " + label);
            }
            else
            {
                Console.WriteLine("  FAIL  " + label);
                _failures++;
            }
        }

        // --- Part 3: the cottage is a real 9x10x11 structure, not a hollow box. ---
        private static void TestCottageShape(Blueprint bp)
        {
            Console.WriteLine("[TestCottageShape]");
            Check(bp.SizeX == MedievalCottageGenerator.Width, "width == 9 (got " + bp.SizeX + ")");
            Check(bp.SizeZ == MedievalCottageGenerator.Length, "length == 11 (got " + bp.SizeZ + ")");
            Check(bp.SizeY == MedievalCottageGenerator.Height, "height == 10 (got " + bp.SizeY + ")");
            Check(bp.CellCount > 150, "has substantial volume, not a hollow box (cells=" + bp.CellCount + ")");
            Check(bp.CellCount <= BuildSafetyLimits.MaxBlocksPerOperation,
                "within per-transaction cap (cells=" + bp.CellCount + " <= " + BuildSafetyLimits.MaxBlocksPerOperation + ")");

            // Normalized so min corner is the origin.
            int minX = int.MaxValue, minY = int.MaxValue, minZ = int.MaxValue;
            foreach (BlueprintCell c in bp.Cells)
            {
                if (c.Relative.X < minX) minX = c.Relative.X;
                if (c.Relative.Y < minY) minY = c.Relative.Y;
                if (c.Relative.Z < minZ) minZ = c.Relative.Z;
            }
            Check(minX == 0 && minY == 0 && minZ == 0, "blueprint is normalized to min corner (0,0,0)");

            // A genuine interior: the center column has an air gap between the
            // floor (y=0) and the ceiling (y=5), proving it is not solid fill.
            bool interiorAir = false;
            FakeTerrain probe = new FakeTerrain();
            IBlockValueResolver res = new FakeResolver();
            BuildPlan plan = BuildPlanner.Plan(bp, new CellPos(0, 10, 0), res, probe, false);
            HashSet<string> written = new HashSet<string>();
            if (plan.Success)
            {
                foreach (PlannedChange pc in plan.Changes)
                {
                    written.Add(pc.Absolute.X + "," + pc.Absolute.Y + "," + pc.Absolute.Z);
                }
                interiorAir =
                    !written.Contains("4,12,5") && !written.Contains("4,13,5") && // room air
                    written.Contains("4,10,5") &&                                  // stone floor under it
                    written.Contains("4,15,5");                                    // roof/ceiling above it
            }
            Check(interiorAir, "has a real enclosed interior (air pocket under a roof)");
        }

        private static void TestDeterministicRebuild()
        {
            Console.WriteLine("[TestDeterministicRebuild]");
            Blueprint a = BuildingGeneratorRegistry.Default.Generate(new GenerationContext(0));
            Blueprint b = BuildingGeneratorRegistry.Default.Generate(new GenerationContext(0));
            Check(a.CellCount == b.CellCount, "cell count stable across rebuilds");
            bool identical = a.CellCount == b.CellCount;
            for (int i = 0; identical && i < a.Cells.Count; i++)
            {
                if (a.Cells[i].Relative != b.Cells[i].Relative || a.Cells[i].MaterialKey != b.Cells[i].MaterialKey)
                {
                    identical = false;
                }
            }
            Check(identical, "every cell (position+material) is identical and ordered across rebuilds");
        }

        private static void TestAllKeysResolvable(Blueprint bp)
        {
            Console.WriteLine("[TestAllKeysResolvable]");
            FakeResolver resolver = new FakeResolver();
            bool all = true;
            foreach (BlueprintCell c in bp.Cells)
            {
                int v;
                if (!resolver.TryResolve(c.MaterialKey, out v))
                {
                    all = false;
                    break;
                }
            }
            Check(all, "all cottage material keys resolve (no unknown block)");
        }

        // --- Part 2: planner refuses every unsafe plan and writes NOTHING. ---
        private static void TestPlanRejectsOutOfBoundsY()
        {
            Console.WriteLine("[TestPlanRejectsOutOfBoundsY]");
            Blueprint bp = BuildingGeneratorRegistry.Default.Generate(new GenerationContext(0));
            FakeTerrain terrain = new FakeTerrain();
            // Cottage is 10 tall; anchoring at y=250 pushes the roof to y=259 (>255).
            BuildPlan plan = BuildPlanner.Plan(bp, new CellPos(0, 250, 0), new FakeResolver(), terrain, false);
            Check(!plan.Success, "plan fails when structure would exceed world height");
            Check(terrain.Writes == 0, "no writes performed during a rejected plan");
        }

        private static void TestPlanRejectsNonAirByDefault()
        {
            Console.WriteLine("[TestPlanRejectsNonAirByDefault]");
            Blueprint bp = LineBlueprint(3);
            FakeTerrain terrain = new FakeTerrain();
            terrain.Occupy(new CellPos(10, 20, 10), 999); // a player block sits at one target cell
            BuildPlan plan = BuildPlanner.Plan(bp, new CellPos(10, 20, 10), new FakeResolver(), terrain, false);
            Check(!plan.Success, "plan refuses to overwrite a non-air target by default");
            Check(terrain.Writes == 0, "no writes performed when overwrite is refused");
        }

        private static void TestPlanAllowsOverrideWhenFlagged()
        {
            Console.WriteLine("[TestPlanAllowsOverrideWhenFlagged]");
            Blueprint bp = LineBlueprint(3);
            FakeTerrain terrain = new FakeTerrain();
            terrain.Occupy(new CellPos(10, 20, 10), 999);
            BuildPlan plan = BuildPlanner.Plan(bp, new CellPos(10, 20, 10), new FakeResolver(), terrain, true);
            Check(plan.Success, "explicit allowOverride flag permits replacing non-air (opt-in only)");
        }

        private static void TestPlanRejectsCountCap()
        {
            Console.WriteLine("[TestPlanRejectsCountCap]");
            BlueprintBuilder b = new BlueprintBuilder();
            b.FillBox(0, 0, 0, 22, 1, 22, BuildingMaterials.StoneBase); // 23*23*2 = 1058 cells
            Blueprint bp = b.ToBlueprint("too_big", 1);
            FakeTerrain terrain = new FakeTerrain();
            BuildPlan plan = BuildPlanner.Plan(bp, new CellPos(0, 10, 0), new FakeResolver(), terrain, true);
            Check(!plan.Success, "plan fails when cell count exceeds the 1024 cap");
            Check(terrain.Writes == 0, "over-cap plan writes nothing");
        }

        private static void TestPlanRejectsAxisCap()
        {
            Console.WriteLine("[TestPlanRejectsAxisCap]");
            BlueprintBuilder b = new BlueprintBuilder();
            b.FillBox(0, 0, 0, 64, 0, 0, BuildingMaterials.StoneBase); // 65 long on X
            Blueprint bp = b.ToBlueprint("too_long", 1);
            FakeTerrain terrain = new FakeTerrain();
            BuildPlan plan = BuildPlanner.Plan(bp, new CellPos(0, 10, 0), new FakeResolver(), terrain, true);
            Check(!plan.Success, "plan fails when a selection axis exceeds 64");
        }

        private static void TestPlanRejectsUnknownMaterial()
        {
            Console.WriteLine("[TestPlanRejectsUnknownMaterial]");
            BlueprintBuilder b = new BlueprintBuilder();
            b.Set(0, 0, 0, "Nonexistent");
            Blueprint bp = b.ToBlueprint("bad_mat", 1);
            FakeTerrain terrain = new FakeTerrain();
            BuildPlan plan = BuildPlanner.Plan(bp, new CellPos(5, 5, 5), new FakeResolver(), terrain, true);
            Check(!plan.Success, "plan fails on an unresolvable material key (no garbage written)");
            Check(terrain.Writes == 0, "unknown-material plan writes nothing");
        }

        // --- Part 2/3: batched execution, undo that respects later player edits. ---
        private static void TestExecuteThenUndoPreservesLaterEdits()
        {
            Console.WriteLine("[TestExecuteThenUndoPreservesLaterEdits]");
            Blueprint bp = LineBlueprint(5);
            FakeTerrain terrain = new FakeTerrain();
            FakeResolver resolver = new FakeResolver();
            CellPos origin = new CellPos(30, 40, 30);
            BuildPlan plan = BuildPlanner.Plan(bp, origin, resolver, terrain, false);
            Check(plan.Success, "5-cell line plans cleanly");

            BuildTask task = new BuildTask("line", origin, plan.Changes, false);
            while (!task.IsFinished)
            {
                task.Advance(terrain, BuildSafetyLimits.MaxBlocksPerBatch);
            }
            Check(task.State == BuildTaskState.Completed, "batched advance reaches Completed");

            bool allPlaced = true;
            foreach (PlannedChange pc in plan.Changes)
            {
                if (terrain.ValueAt(pc.Absolute) != pc.Value)
                {
                    allPlaced = false;
                }
            }
            Check(allPlaced, "every planned cell now holds the built value");

            // Player edits the FIRST built block after the build finished.
            CellPos playerCell = plan.Changes[0].Absolute;
            terrain.Occupy(playerCell, 555);

            int restored = task.Undo(terrain);
            Check(terrain.ValueAt(playerCell) == 555,
                "undo does NOT overwrite a cell the player edited after the build");
            Check(restored == plan.Changes.Count - 1,
                "undo restored the remaining untouched cells (" + restored + " of " + plan.Changes.Count + ")");

            bool originalsRestored = true;
            for (int i = 1; i < plan.Changes.Count; i++)
            {
                if (terrain.ValueAt(plan.Changes[i].Absolute) != 0)
                {
                    originalsRestored = false;
                }
            }
            Check(originalsRestored, "untouched cells reverted to their captured original (air)");
        }

        private static void TestRollbackOnMidBuildFailure()
        {
            Console.WriteLine("[TestRollbackOnMidBuildFailure]");
            Blueprint bp = LineBlueprint(40);
            FakeTerrain terrain = new FakeTerrain();
            CellPos origin = new CellPos(100, 60, 100);
            BuildPlan plan = BuildPlanner.Plan(bp, origin, new FakeResolver(), terrain, false);
            Check(plan.Success, "40-cell line plans cleanly");

            BuildTask task = new BuildTask("line40", origin, plan.Changes, false);
            task.Advance(terrain, BuildSafetyLimits.MaxBlocksPerBatch); // applies cells 0..31
            Check(!task.IsFinished, "first batch leaves the task Running (32 per batch)");

            // Simulate the chunk for the next target cell unloading mid-build.
            CellPos unloadCell = plan.Changes[32].Absolute;
            terrain.Blocked.Add(unloadCell);

            task.Advance(terrain, BuildSafetyLimits.MaxBlocksPerBatch);
            Check(task.State == BuildTaskState.Failed, "task fails when a target cell becomes uneditable mid-build");

            bool rolledBack = true;
            for (int i = 0; i < 32; i++)
            {
                if (terrain.ValueAt(plan.Changes[i].Absolute) != 0)
                {
                    rolledBack = false;
                }
            }
            Check(rolledBack, "all already-placed cells were rolled back to air on failure");
        }

        private static void TestCancelMidBuildRollsBack()
        {
            Console.WriteLine("[TestCancelMidBuildRollsBack]");
            Blueprint bp = LineBlueprint(40);
            FakeTerrain terrain = new FakeTerrain();
            CellPos origin = new CellPos(200, 30, 200);
            BuildPlan plan = BuildPlanner.Plan(bp, origin, new FakeResolver(), terrain, false);
            BuildTask task = new BuildTask("line40", origin, plan.Changes, false);
            task.Advance(terrain, BuildSafetyLimits.MaxBlocksPerBatch); // 32 applied

            bool cancelled = task.Cancel(terrain);
            Check(cancelled, "Cancel returns true for an in-flight task");
            Check(task.State == BuildTaskState.Cancelled, "task state is Cancelled");

            bool rolledBack = true;
            for (int i = 0; i < 32; i++)
            {
                if (terrain.ValueAt(plan.Changes[i].Absolute) != 0)
                {
                    rolledBack = false;
                }
            }
            Check(rolledBack, "cancel rolled back every placed cell to air");
        }

        // --- Part 4: relative coordinates + rotation/mirror reuse the same path. ---
        private static void TestRotationPreservesBlueprint()
        {
            Console.WriteLine("[TestRotationPreservesBlueprint]");
            Blueprint bp = BuildingGeneratorRegistry.Default.Generate(new GenerationContext(0));

            Blueprint r1 = bp.Reorient(1, false);
            Check(r1.CellCount == bp.CellCount, "one quarter-turn preserves cell count");
            Check(r1.SizeX == bp.SizeZ && r1.SizeZ == bp.SizeX, "one quarter-turn swaps the footprint axes");

            Blueprint r4 = bp.Reorient(4, false);
            Check(SameCellSet(bp, r4), "four quarter-turns return to the original layout");
        }

        private static void TestMirrorIsInvolution()
        {
            Console.WriteLine("[TestMirrorIsInvolution]");
            Blueprint bp = BuildingGeneratorRegistry.Default.Generate(new GenerationContext(0));
            Blueprint m = bp.Reorient(0, true);
            Check(m.CellCount == bp.CellCount, "mirror preserves cell count");
            Check(SameCellSet(bp, m.Reorient(0, true)), "mirroring twice returns the original layout");
        }

        // --- helpers ---
        private static Blueprint LineBlueprint(int cells)
        {
            BlueprintBuilder b = new BlueprintBuilder();
            for (int i = 0; i < cells; i++)
            {
                b.Set(i, 0, 0, BuildingMaterials.StoneBase);
            }
            return b.ToBlueprint("line", 1);
        }

        private static bool SameCellSet(Blueprint a, Blueprint b)
        {
            if (a.CellCount != b.CellCount)
            {
                return false;
            }
            HashSet<string> setA = new HashSet<string>();
            foreach (BlueprintCell c in a.Cells)
            {
                setA.Add(CellKey(c));
            }
            foreach (BlueprintCell c in b.Cells)
            {
                if (!setA.Contains(CellKey(c)))
                {
                    return false;
                }
            }
            return true;
        }

        private static string CellKey(BlueprintCell c)
        {
            return c.Relative.X + "," + c.Relative.Y + "," + c.Relative.Z + "|" + c.MaterialKey;
        }
    }
}

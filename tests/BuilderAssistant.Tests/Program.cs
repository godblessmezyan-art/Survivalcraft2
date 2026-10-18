using System;
using System.Collections.Generic;
using BuilderAssistant.Core;
using BuilderAssistant.Core.Blueprints;
using BuilderAssistant.Core.BuildEngine;
using BuilderAssistant.Core.Json;
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
        // Cells whose "chunk" is present but not loaded (recoverable by waiting).
        public readonly HashSet<CellPos> Blocked = new HashSet<CellPos>();
        // Cells whose "chunk" is entirely missing (also recoverable by waiting).
        public readonly HashSet<CellPos> Missing = new HashSet<CellPos>();
        // v0.6.3: non-air cells that hold a walk-through plant/snow (soft obstruction).
        // A non-air cell NOT in this set is treated as solid/hard (protect at all costs).
        public readonly HashSet<CellPos> Soft = new HashSet<CellPos>();
        private readonly Dictionary<CellPos, int> _values = new Dictionary<CellPos, int>();

        public int Writes;
        public int Reads;
        public int ExtentLimit = 4096;

        public CellEditability InspectCell(CellPos cell)
        {
            if (!WorldBounds.IsYValid(cell.Y))
            {
                return new CellEditability(cell, CellEditabilityStatus.InvalidY);
            }
            if (cell.X < -ExtentLimit || cell.X > ExtentLimit ||
                cell.Z < -ExtentLimit || cell.Z > ExtentLimit)
            {
                return new CellEditability(cell, CellEditabilityStatus.InvalidCell);
            }
            if (Missing.Contains(cell))
            {
                return new CellEditability(cell, CellEditabilityStatus.ChunkMissing);
            }
            if (Blocked.Contains(cell))
            {
                return new CellEditability(cell, CellEditabilityStatus.ChunkContentsNotReady);
            }
            return CellEditability.Ok(cell);
        }

        public bool IsCellEditable(CellPos cell)
        {
            return InspectCell(cell).IsEditable;
        }

        public bool IsCellAir(CellPos cell)
        {
            int value;
            return TryGetCellValue(cell, out value) && value == 0;
        }

        public bool IsCellSoftObstruction(CellPos cell)
        {
            int value;
            if (!TryGetCellValue(cell, out value))
            {
                return false;
            }
            if (value == 0)
            {
                return false; // air is not an obstruction
            }
            return Soft.Contains(cell); // only plants/snow; everything else is hard
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

            // P0 diagnostics + transactional-integrity regressions (chunk loading,
            // boundaries, partial rollback/undo). See BuildPlanner / BuildTask /
            // TransactionHistory changes.
            TestCrossChunkNotReadyClassifiedAndRecoverable();
            TestOutOfBoundsIsHardNotRecoverable();
            TestWorldHeightYBoundary();
            TestPartialRollbackIsTrackedAndRetried();
            TestPartialUndoRetainedAndRetried();
            TestUndoPendingYieldsToLaterPlayerEdit();

            // v0.6.3: bounded stall (transient chunk re-mesh pauses, never aborts a good
            // build) + clear-soft (walk-through plants/snow replaceable on opt-in, solid
            // terrain / player structures still protected). See BuildTask / BuildPlanner.
            TestTransientChunkStallPausesInsteadOfAborting();
            TestStallBudgetExhaustionRollsBackSafely();
            TestClearSoftReplacesVegetationWhenOptedIn();
            TestClearSoftNeverTouchesSolidBlocks();

            // v0.7.0: generic JSON blueprint import/export. Proves a structure can be
            // reliably saved, re-imported and reproduce the SAME block set, and that any
            // malformed / unsupported / illegal blueprint is rejected loudly (never a
            // partial or garbage import). See Core/Blueprints/BlueprintJson + Core/Json.
            TestJsonEngineBasicsAndStrictness();
            TestBlueprintJsonRoundTripCottage();
            TestBlueprintJsonNegativeCoordsNormalizeAndReproduce();
            TestBlueprintJsonRejectsDuplicateCoords();
            TestBlueprintJsonRejectsFractionalCoords();
            TestBlueprintJsonRejectsUnknownSchemaVersion();
            TestBlueprintJsonRejectsOverCap();
            TestBlueprintJsonRejectsAxisCap();
            TestBlueprintJsonRejectsUnknownMaterialId();
            TestBlueprintJsonRejectsEmptyCells();
            TestBlueprintJsonRejectsUnsupportedOrientation();
            TestBlueprintJsonRejectsBuildOps();
            TestBlueprintJsonRejectsSizeMismatch();
            TestBlueprintJsonRotationTransformReproduces();

            // v0.8.0: the in-game blueprint tool browses the library folder by tapping.
            // Prove the pure cursor (advance/wrap/empty) and its "[i/N]" toast label are
            // correct without touching files or the game. See Core/Blueprints/
            // BlueprintLibraryBrowser + WorldEditRuntime.CycleImportLibrary.
            TestBlueprintLibraryBrowserAdvance();
            TestBlueprintLibraryBrowserPositionLabel();

            // v0.8.1: paste-a-JSON-string import (clipboard-paste MVP). Proves the pure
            // BlueprintImportService reuses the canonical parser, rejects every bad input
            // with a clear reason AND never hands back a partial blueprint (so the caller
            // can guarantee the live clipboard is never clobbered), and that the naming
            // policy stops all-CJK names collapsing to "blueprint" and never overwrites an
            // existing stem. See Core/Blueprints/BlueprintImportService + BlueprintNamePolicy
            // + Infrastructure/BlueprintStorage + WorldEditRuntime.ImportFromText.
            TestImportValidBlueprint();
            TestImportRejectsEmptyText();
            TestImportRejectsNonJsonShape();
            TestImportRejectsOverSizeInput();
            TestImportRejectsInvalidJson();
            TestImportRejectsDuplicateCoords();
            TestImportRejectsOverCapBlueprint();
            TestImportRejectsUnknownMaterial();
            TestImportFailureNeverYieldsPartialBlueprint();
            TestNamePolicySanitize();
            TestNamePolicyCjkNoLongerCollapses();
            TestNamePolicyResolveUniqueNeverOverwrites();
            TestNamePolicyStableHashDeterministic();

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

            // Make the NEXT target cell (x=132) a HARD failure by shrinking the world
            // extent below it, while every already-placed cell (x<=131) stays editable so
            // the rollback can reach all of them. A transient recoverable block now pauses
            // (see the v0.6.3 stall tests) instead of aborting.
            terrain.ExtentLimit = 131;

            task.Advance(terrain, BuildSafetyLimits.MaxBlocksPerBatch);
            Check(task.State == BuildTaskState.Failed, "task fails immediately when a target cell becomes hard-uneditable mid-build");

            bool rolledBack = true;
            for (int i = 0; i < 32; i++)
            {
                if (terrain.ValueAt(plan.Changes[i].Absolute) != 0)
                {
                    rolledBack = false;
                }
            }
            Check(rolledBack, "all already-placed cells were rolled back to air on a hard mid-build failure");
            Check(task.PendingRollbackCount == 0, "a hard failure with reachable chunks leaves nothing pending");
        }

        // --- v0.6.3 bounded stall: a transiently re-meshed / not-yet-ready chunk pauses
        //     the build IN PLACE instead of aborting the whole structure. ---
        private static void TestTransientChunkStallPausesInsteadOfAborting()
        {
            Console.WriteLine("[TestTransientChunkStallPausesInsteadOfAborting]");
            Blueprint bp = LineBlueprint(40);
            FakeTerrain terrain = new FakeTerrain();
            CellPos origin = new CellPos(100, 60, 100);
            BuildPlan plan = BuildPlanner.Plan(bp, origin, new FakeResolver(), terrain, false);
            BuildTask task = new BuildTask("line40", origin, plan.Changes, false);
            task.Advance(terrain, BuildSafetyLimits.MaxBlocksPerBatch); // 32 placed

            // The next target's chunk briefly drops (recoverable, e.g. our own write
            // triggering a re-mesh). One Advance must NOT fail or roll back — it stalls.
            CellPos stallCell = plan.Changes[32].Absolute;
            terrain.Blocked.Add(stallCell);

            int applied = task.Advance(terrain, BuildSafetyLimits.MaxBlocksPerBatch);
            Check(task.State == BuildTaskState.Running, "a recoverable mid-build block leaves the task Running, not Failed");
            Check(applied == 0, "the stalled Advance applies nothing");
            Check(task.IdleAdvanceCalls == 1, "one zero-progress stall call is counted");
            Check(task.IsStalled, "the task reports it is stalled");

            // Already-placed cells must remain untouched while we wait (no rollback).
            bool stillPlaced = true;
            for (int i = 0; i < 32; i++)
            {
                if (terrain.ValueAt(plan.Changes[i].Absolute) == 0)
                {
                    stillPlaced = false;
                }
            }
            Check(stillPlaced, "a transient stall does NOT roll back the part already built");

            // The chunk streams back; the next Advance resumes and finishes the build.
            terrain.Blocked.Remove(stallCell);
            while (!task.IsFinished)
            {
                task.Advance(terrain, BuildSafetyLimits.MaxBlocksPerBatch);
            }
            Check(task.State == BuildTaskState.Completed, "build resumes and completes once the chunk is ready");
            Check(task.IdleAdvanceCalls == 0, "making real progress resets the idle-stall counter");
        }

        private static void TestStallBudgetExhaustionRollsBackSafely()
        {
            Console.WriteLine("[TestStallBudgetExhaustionRollsBackSafely]");
            Blueprint bp = LineBlueprint(40);
            FakeTerrain terrain = new FakeTerrain();
            CellPos origin = new CellPos(100, 60, 100);
            BuildPlan plan = BuildPlanner.Plan(bp, origin, new FakeResolver(), terrain, false);
            BuildTask task = new BuildTask("line40", origin, plan.Changes, false);
            task.Advance(terrain, BuildSafetyLimits.MaxBlocksPerBatch); // 32 placed

            // A chunk that NEVER arrives: keep it blocked and pump Advance past the budget.
            CellPos deadCell = plan.Changes[32].Absolute;
            terrain.Blocked.Add(deadCell);

            int guard = 0;
            while (!task.IsFinished && guard < BuildSafetyLimits.MaxIdleAdvanceCallsDuringBuild + 10)
            {
                task.Advance(terrain, BuildSafetyLimits.MaxBlocksPerBatch);
                guard++;
            }

            Check(task.State == BuildTaskState.Failed,
                "a chunk that never streams in eventually fails instead of hanging forever");
            Check(task.FailureReason != null && task.FailureReason.Contains("持续未就绪"),
                "give-up reason reports the exhausted stall budget");

            bool rolledBack = true;
            for (int i = 0; i < 32; i++)
            {
                if (terrain.ValueAt(plan.Changes[i].Absolute) != 0)
                {
                    rolledBack = false;
                }
            }
            Check(rolledBack, "exhausting the stall budget rolls the build back safely");
        }

        // --- v0.6.3 clear-soft: plants/snow may be replaced on an explicit opt-in, but
        //     solid terrain and player structures are never touched. ---
        private static void TestClearSoftReplacesVegetationWhenOptedIn()
        {
            Console.WriteLine("[TestClearSoftReplacesVegetationWhenOptedIn]");
            Blueprint bp = LineBlueprint(5);
            FakeTerrain terrain = new FakeTerrain();
            CellPos origin = new CellPos(50, 60, 50);
            CellPos plantCell = plan_FirstTarget(bp, origin);

            // A walk-through plant sits on one target cell.
            int plantValue = 4242;
            terrain.Occupy(plantCell, plantValue);
            terrain.Soft.Add(plantCell);

            // Default (clear-soft OFF): the plant blocks the plan and nothing is written.
            BuildPlan blocked = BuildPlanner.Plan(bp, origin, new FakeResolver(), terrain, false, false);
            Check(!blocked.Success, "a plant in the footprint blocks the plan when clear-soft is off");
            Check(blocked.FailedBySoftObstruction, "the block is classified as soft (offer to clear)");
            Check(terrain.Writes == 0, "no writes when clear-soft is off");

            // Opt-in: the plant is replaceable; the plan succeeds and reports the count.
            BuildPlan cleared = BuildPlanner.Plan(bp, origin, new FakeResolver(), terrain, false, true);
            Check(cleared.Success, "clear-soft mode lets the plan replace walk-through plants");
            Check(cleared.SoftClearedCount == 1, "the plan reports exactly one cleared soft obstruction");

            // Execute, then confirm the plant cell now holds the built block and Undo
            // restores the original plant value (never leaves a hole).
            BuildTask task = new BuildTask("line5", origin, cleared.Changes, false, true);
            while (!task.IsFinished)
            {
                task.Advance(terrain, BuildSafetyLimits.MaxBlocksPerBatch);
            }
            Check(task.State == BuildTaskState.Completed, "clear-soft build runs to completion");
            int builtValue = terrain.ValueAt(plantCell);
            Check(builtValue != plantValue && builtValue != 0, "the plant cell now holds the freshly built block");

            task.Undo(terrain);
            Check(terrain.ValueAt(plantCell) == plantValue, "undo restores the exact original plant (no erasure)");
        }

        private static void TestClearSoftNeverTouchesSolidBlocks()
        {
            Console.WriteLine("[TestClearSoftNeverTouchesSolidBlocks]");
            Blueprint bp = LineBlueprint(5);
            FakeTerrain terrain = new FakeTerrain();
            CellPos origin = new CellPos(70, 60, 70);
            CellPos solidCell = plan_FirstTarget(bp, origin);

            // A solid terrain / player-structure block (non-air, NOT in Soft).
            terrain.Occupy(solidCell, 999);

            BuildPlan plan = BuildPlanner.Plan(bp, origin, new FakeResolver(), terrain, false, true);
            Check(!plan.Success, "clear-soft still refuses to replace a solid block");
            Check(!plan.FailedBySoftObstruction, "a solid conflict is not offered as a clearable soft case");
            Check(plan.FailureReason.Contains("实心方块"), "reason names the solid-block conflict");
            Check(terrain.Writes == 0, "solid block is never written");
        }

        // Shared helper: the absolute cell a LineBlueprint's first cell maps to.
        private static CellPos plan_FirstTarget(Blueprint bp, CellPos origin)
        {
            return origin.Add(bp.Cells[0].Relative);
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

        // --- P0 diagnostics: classify editability instead of one collapsed error. ---
        private static void TestCrossChunkNotReadyClassifiedAndRecoverable()
        {
            Console.WriteLine("[TestCrossChunkNotReadyClassifiedAndRecoverable]");
            Blueprint bp = LineBlueprint(40); // spans 40 columns on X
            FakeTerrain terrain = new FakeTerrain();
            CellPos origin = new CellPos(1000, 60, 1000);

            // One cell's chunk is present-but-contents-not-ready, another's chunk is
            // entirely missing. Both are legitimately part of the world, just not
            // ready yet — the fix in v0.6.2 stops treating "unloaded" as a hard gate.
            terrain.Blocked.Add(new CellPos(1005, 60, 1000));
            terrain.Missing.Add(new CellPos(1020, 60, 1000));

            BuildPlan plan = BuildPlanner.Plan(bp, origin, new FakeResolver(), terrain, false);
            Check(!plan.Success, "cross-chunk not-ready cells fail the plan");
            Check(terrain.Writes == 0, "not-ready plan writes nothing");
            Check(plan.EditabilityFailures.Count == 2,
                "both offending cells are enumerated (got " + plan.EditabilityFailures.Count + ")");
            Check(plan.AllEditabilityFailuresRecoverable,
                "pure chunk-readiness failures are flagged recoverable (worth a wait)");
            Check(plan.FailureReason.Contains("区块内容未就绪") && plan.FailureReason.Contains("区块缺失"),
                "reason separates 区块内容未就绪 vs 区块缺失 instead of one collapsed message");
        }

        private static void TestOutOfBoundsIsHardNotRecoverable()
        {
            Console.WriteLine("[TestOutOfBoundsIsHardNotRecoverable]");
            Blueprint bp = LineBlueprint(3);
            FakeTerrain terrain = new FakeTerrain();
            terrain.ExtentLimit = 10; // world only extends to +/-10 on X/Z
            BuildPlan plan = BuildPlanner.Plan(bp, new CellPos(100, 60, 0), new FakeResolver(), terrain, false);
            Check(!plan.Success, "beyond-extent cells fail the plan");
            Check(!plan.AllEditabilityFailuresRecoverable,
                "out-of-bounds is a HARD failure (a wait can never make it editable)");
            Check(plan.FailureReason.Contains("超出有效范围"),
                "reason names the extent violation explicitly");
            Check(terrain.Writes == 0, "out-of-bounds plan writes nothing");
        }

        private static void TestWorldHeightYBoundary()
        {
            Console.WriteLine("[TestWorldHeightYBoundary]");
            FakeTerrain terrain = new FakeTerrain();

            // Y=255 is the last valid row (MaxYExclusive==256).
            BuildPlan ok = BuildPlanner.Plan(LineBlueprint(3), new CellPos(50, 255, 50),
                new FakeResolver(), terrain, false);
            Check(ok.Success, "structure at Y=255 is within the terrain column");

            // Y=256 overflows the world height and must be a hard, non-recoverable fail.
            BuildPlan bad = BuildPlanner.Plan(LineBlueprint(3), new CellPos(50, 256, 50),
                new FakeResolver(), terrain, false);
            Check(!bad.Success, "structure at Y=256 is rejected as height overflow");
            Check(!bad.AllEditabilityFailuresRecoverable, "height overflow is hard, not a wait");
            Check(bad.FailureReason.Contains("高度越界"), "reason identifies the Y bound violation");
            Check(terrain.Writes == 0, "height-overflow plans write nothing");
        }

        // --- Transactional integrity: never mis-report a partial restore. ---
        private static void TestPartialRollbackIsTrackedAndRetried()
        {
            Console.WriteLine("[TestPartialRollbackIsTrackedAndRetried]");
            Blueprint bp = LineBlueprint(40);
            FakeTerrain terrain = new FakeTerrain();
            CellPos origin = new CellPos(1000, 60, 1000);
            BuildPlan plan = BuildPlanner.Plan(bp, origin, new FakeResolver(), terrain, false);
            Check(plan.Success, "40-cell line plans cleanly");

            BuildTask task = new BuildTask("line40", origin, plan.Changes, false);
            task.Advance(terrain, BuildSafetyLimits.MaxBlocksPerBatch); // cells 0..31 applied

            CellPos appliedCell = new CellPos(1010, 60, 1000); // already placed
            terrain.Blocked.Add(appliedCell); // its chunk unloads before rollback can reach it
            // Trigger a HARD mid-build failure (a recoverable block would now just stall):
            // shrink the extent below the next target so it is a non-recoverable InvalidCell,
            // while every already-placed cell (x<=1031) remains within bounds.
            terrain.ExtentLimit = 1031;

            task.Advance(terrain, BuildSafetyLimits.MaxBlocksPerBatch);
            Check(task.State == BuildTaskState.Failed, "task fails when the next target becomes hard-uneditable");
            Check(task.PendingRollbackCount == 1,
                "the one blocked already-placed cell is parked, not silently forgotten (got " +
                task.PendingRollbackCount + ")");
            Check(terrain.ValueAt(appliedCell) != 0,
                "the still-unreachable cell visibly remains (partial rollback, not falsely clean)");

            int othersClean = 0;
            for (int i = 0; i < 32; i++)
            {
                if (plan.Changes[i].Absolute != appliedCell && terrain.ValueAt(plan.Changes[i].Absolute) == 0)
                {
                    othersClean++;
                }
            }
            Check(othersClean == 31, "every other already-placed cell was rolled back (31 of 31)");
            Check(!task.IsCleanlyFinished, "a partial rollback is not reported as a clean finish");

            terrain.Blocked.Remove(appliedCell); // chunk streams back
            int stillPending = task.RetryRollback(terrain);
            Check(stillPending == 0, "retry restores the deferred cell once the chunk is reachable");
            Check(terrain.ValueAt(appliedCell) == 0, "deferred cell is now back to its original (air)");
            Check(task.IsCleanlyFinished, "after the retry the task reports a clean finish");
        }

        private static void TestPartialUndoRetainedAndRetried()
        {
            Console.WriteLine("[TestPartialUndoRetainedAndRetried]");
            Blueprint bp = LineBlueprint(10);
            FakeTerrain terrain = new FakeTerrain();
            CellPos origin = new CellPos(500, 60, 500);
            BuildPlan plan = BuildPlanner.Plan(bp, origin, new FakeResolver(), terrain, false);
            BuildTask task = new BuildTask("line10", origin, plan.Changes, false);
            while (!task.IsFinished)
            {
                task.Advance(terrain, BuildSafetyLimits.MaxBlocksPerBatch);
            }
            Check(task.State == BuildTaskState.Completed, "line10 built to completion");

            TransactionHistory history = new TransactionHistory();
            history.Record(task);

            CellPos unloadCell = new CellPos(503, 60, 500);
            terrain.Blocked.Add(unloadCell); // its chunk unloads before the undo reaches it

            int pending;
            history.UndoLast(terrain, out pending);
            Check(pending == 1, "undo reports one cell still unrestored (chunk unloaded)");
            Check(history.Count == 1, "the partially-undone transaction is KEPT on the stack for retry");
            Check(history.HasUndoable, "a partial undo leaves the history marked undoable");
            Check(terrain.ValueAt(unloadCell) != 0, "the unreachable cell is not falsely claimed restored");

            terrain.Blocked.Remove(unloadCell); // chunk streams back
            history.UndoLast(terrain, out pending);
            Check(pending == 0, "the follow-up undo finishes the deferred restore");
            Check(terrain.ValueAt(unloadCell) == 0, "the cell is now returned to its original (air)");
            Check(history.Count == 0, "once fully restored the transaction leaves the stack");
        }

        private static void TestUndoPendingYieldsToLaterPlayerEdit()
        {
            Console.WriteLine("[TestUndoPendingYieldsToLaterPlayerEdit]");
            Blueprint bp = LineBlueprint(5);
            FakeTerrain terrain = new FakeTerrain();
            CellPos origin = new CellPos(200, 60, 200);
            BuildPlan plan = BuildPlanner.Plan(bp, origin, new FakeResolver(), terrain, false);
            BuildTask task = new BuildTask("line5", origin, plan.Changes, false);
            while (!task.IsFinished)
            {
                task.Advance(terrain, BuildSafetyLimits.MaxBlocksPerBatch);
            }

            CellPos playerCell = new CellPos(202, 60, 200);
            terrain.Blocked.Add(playerCell);

            task.Undo(terrain); // one cell parked
            Check(task.PendingUndoCount == 1, "undo parks the single unreachable cell");

            // While the chunk was unloaded the player rebuilt that cell themselves.
            terrain.Occupy(playerCell, 999);
            terrain.Blocked.Remove(playerCell);

            int stillPending = task.RetryUndo(terrain);
            Check(stillPending == 0, "retry does not keep a cell the player has since changed");
            Check(terrain.ValueAt(playerCell) == 999,
                "the player's later edit is preserved — undo never clobbers it");
        }

        // --- v0.7.0: generic JSON blueprint import/export. ---

        // The hand-rolled JSON engine must accept well-formed input (nested objects,
        // arrays, escapes, unicode, bools, null) and reject malformed input loudly.
        private static void TestJsonEngineBasicsAndStrictness()
        {
            Console.WriteLine("[TestJsonEngineBasicsAndStrictness]");
            JsonValue obj = JsonValue.Parse(
                "{\"schemaVersion\":1,\"name\":\"小屋 \\u00fc\",\"flag\":true,\"n\":null," +
                "\"size\":{\"x\":-3,\"y\":10,\"z\":11},\"arr\":[1,2,3],\"s\":\"a\\\"b\"}");
            int sv;
            Check(obj.TryGetInt("schemaVersion", out sv) && sv == 1, "parses integer member");
            Check(obj.Get("name").StringValue == "小屋 ü", "parses unicode escape + CJK name");
            Check(obj.Get("flag").BoolValue, "parses boolean true");
            Check(obj.Get("n").Kind == JsonKind.Null, "parses null");
            int negx;
            Check(obj.Get("size").TryGetInt("x", out negx) && negx == -3, "parses nested negative integer");
            Check(obj.Get("arr").Items.Count == 3, "parses array of three items");
            Check(obj.Get("s").StringValue == "a\"b", "parses escaped quote in string");

            bool threwTrailing = false;
            try { JsonValue.Parse("{} extra"); } catch (JsonParseException) { threwTrailing = true; }
            Check(threwTrailing, "rejects trailing characters after top-level value");

            bool threwTrailingComma = false;
            try { JsonValue.Parse("[1,2,]"); } catch (JsonParseException) { threwTrailingComma = true; }
            Check(threwTrailingComma, "rejects trailing comma (strict parser, no garbage)");

            bool threwBadNum = false;
            try { JsonValue.Parse("{\"x\":1e}"); } catch (JsonParseException) { threwBadNum = true; }
            Check(threwBadNum, "rejects malformed number token");
        }

        // The core promise: cottage -> JSON -> blueprint reproduces the SAME block set,
        // and re-serializing is byte-stable (deterministic ordering).
        private static void TestBlueprintJsonRoundTripCottage()
        {
            Console.WriteLine("[TestBlueprintJsonRoundTripCottage]");
            Blueprint original = BuildingGeneratorRegistry.Default.Generate(new GenerationContext(0));
            string json = BlueprintJson.Serialize(original);
            Check(json.Length > 0, "serialized a non-empty JSON document");

            BlueprintJsonDecodeResult decode = BlueprintJson.Decode(json);
            Check(decode.Success, "decoded the blueprint without errors (" + decode.JoinedErrors() + ")");
            if (decode.Blueprint == null) return;

            Check(decode.Blueprint.CellCount == original.CellCount, "round-trip preserves cell count");
            Check(SameCellSet(original, decode.Blueprint), "round-trip reproduces the identical (position, material) set");
            Check(decode.Blueprint.SizeX == original.SizeX &&
                  decode.Blueprint.SizeY == original.SizeY &&
                  decode.Blueprint.SizeZ == original.SizeZ, "round-trip preserves dimensions");

            string json2 = BlueprintJson.Serialize(decode.Blueprint);
            Check(json == json2, "re-serializing the decoded blueprint is byte-identical (deterministic)");
        }

        // Negative relative coordinates are legal input and must normalize to min (0,0,0),
        // reproducing the same shape after a round-trip.
        private static void TestBlueprintJsonNegativeCoordsNormalizeAndReproduce()
        {
            Console.WriteLine("[TestBlueprintJsonNegativeCoordsNormalizeAndReproduce]");
            string json = "{\"schemaVersion\":1,\"name\":\"neg\",\"cells\":[" +
                "{\"x\":-2,\"y\":0,\"z\":-5,\"key\":\"StoneBase\"}," +
                "{\"x\":0,\"y\":1,\"z\":0,\"key\":\"WoodWall\"}," +
                "{\"x\":3,\"y\":0,\"z\":2,\"key\":\"StoneBase\"}]}";
            BlueprintJsonDecodeResult decode = BlueprintJson.Decode(json);
            Check(decode.Success, "negative relative coords are accepted (" + decode.JoinedErrors() + ")");
            if (decode.Blueprint == null) return;

            int minX = int.MaxValue, minY = int.MaxValue, minZ = int.MaxValue;
            foreach (BlueprintCell c in decode.Blueprint.Cells)
            {
                if (c.Relative.X < minX) minX = c.Relative.X;
                if (c.Relative.Y < minY) minY = c.Relative.Y;
                if (c.Relative.Z < minZ) minZ = c.Relative.Z;
            }
            Check(minX == 0 && minY == 0 && minZ == 0, "blueprint is normalized so the min corner is (0,0,0)");

            string again = BlueprintJson.Serialize(decode.Blueprint);
            BlueprintJsonDecodeResult re = BlueprintJson.Decode(again);
            Check(re.Success && SameCellSet(decode.Blueprint, re.Blueprint), "normalized blueprint round-trips identically");
        }

        private static void TestBlueprintJsonRejectsDuplicateCoords()
        {
            Console.WriteLine("[TestBlueprintJsonRejectsDuplicateCoords]");
            string json = "{\"schemaVersion\":1,\"cells\":[" +
                "{\"x\":0,\"y\":0,\"z\":0,\"key\":\"StoneBase\"}," +
                "{\"x\":0,\"y\":0,\"z\":0,\"key\":\"WoodWall\"}]}";
            BlueprintJsonDecodeResult decode = BlueprintJson.Decode(json);
            Check(!decode.Success, "duplicate relative coordinates are rejected");
            Check(decode.FirstError != null && decode.FirstError.Contains("重复"), "reason names the duplicate coordinate");
        }

        private static void TestBlueprintJsonRejectsFractionalCoords()
        {
            Console.WriteLine("[TestBlueprintJsonRejectsFractionalCoords]");
            string json = "{\"schemaVersion\":1,\"cells\":[{\"x\":0.5,\"y\":0,\"z\":0,\"key\":\"StoneBase\"}]}";
            BlueprintJsonDecodeResult decode = BlueprintJson.Decode(json);
            Check(!decode.Success, "a fractional coordinate is rejected (coords must be integers)");
        }

        private static void TestBlueprintJsonRejectsUnknownSchemaVersion()
        {
            Console.WriteLine("[TestBlueprintJsonRejectsUnknownSchemaVersion]");
            string future = "{\"schemaVersion\":999,\"cells\":[{\"x\":0,\"y\":0,\"z\":0,\"key\":\"StoneBase\"}]}";
            BlueprintJsonDecodeResult d1 = BlueprintJson.Decode(future);
            Check(!d1.Success, "a schemaVersion newer than supported is rejected (no silent downgrade)");

            string missing = "{\"cells\":[{\"x\":0,\"y\":0,\"z\":0,\"key\":\"StoneBase\"}]}";
            BlueprintJsonDecodeResult d2 = BlueprintJson.Decode(missing);
            Check(!d2.Success, "a missing schemaVersion is rejected");
        }

        private static void TestBlueprintJsonRejectsOverCap()
        {
            Console.WriteLine("[TestBlueprintJsonRejectsOverCap]");
            // 23 x 23 x 2 = 1058 cells > the 1024 per-transaction cap.
            var sb = new System.Text.StringBuilder();
            sb.Append("{\"schemaVersion\":1,\"materials\":[{\"id\":0,\"key\":\"StoneBase\"}],\"cells\":[");
            int n = 0;
            for (int x = 0; x < 23 && n < 1058; x++)
                for (int z = 0; z < 23 && n < 1058; z++)
                    for (int y = 0; y < 2 && n < 1058; y++, n++)
                    {
                        if (n > 0) sb.Append(',');
                        sb.Append("{\"x\":").Append(x).Append(",\"y\":").Append(y)
                          .Append(",\"z\":").Append(z).Append(",\"m\":0}");
                    }
            sb.Append("]}");
            BlueprintJsonDecodeResult decode = BlueprintJson.Decode(sb.ToString());
            Check(!decode.Success, "an over-cap blueprint (>1024 cells) is rejected on import");
            Check(decode.FirstError != null && decode.FirstError.Contains("上限"), "reason mentions the block cap");
        }

        private static void TestBlueprintJsonRejectsAxisCap()
        {
            Console.WriteLine("[TestBlueprintJsonRejectsAxisCap]");
            var sb = new System.Text.StringBuilder();
            sb.Append("{\"schemaVersion\":1,\"materials\":[{\"id\":0,\"key\":\"StoneBase\"}],\"cells\":[");
            for (int x = 0; x < 65; x++) // 65 long on X > MaxSelectionAxisLength 64
            {
                if (x > 0) sb.Append(',');
                sb.Append("{\"x\":").Append(x).Append(",\"y\":0,\"z\":0,\"m\":0}");
            }
            sb.Append("]}");
            BlueprintJsonDecodeResult decode = BlueprintJson.Decode(sb.ToString());
            Check(!decode.Success, "a blueprint exceeding the 64 axis length is rejected");
        }

        private static void TestBlueprintJsonRejectsUnknownMaterialId()
        {
            Console.WriteLine("[TestBlueprintJsonRejectsUnknownMaterialId]");
            string json = "{\"schemaVersion\":1,\"materials\":[{\"id\":0,\"key\":\"StoneBase\"}],\"cells\":[" +
                "{\"x\":0,\"y\":0,\"z\":0,\"m\":7}]}"; // id 7 not in the table
            BlueprintJsonDecodeResult decode = BlueprintJson.Decode(json);
            Check(!decode.Success, "a cell referencing a non-existent material id is rejected");
            Check(decode.FirstError != null && decode.FirstError.Contains("不存在的材料"), "reason names the dangling material id");
        }

        private static void TestBlueprintJsonRejectsEmptyCells()
        {
            Console.WriteLine("[TestBlueprintJsonRejectsEmptyCells]");
            BlueprintJsonDecodeResult decode = BlueprintJson.Decode("{\"schemaVersion\":1,\"cells\":[]}");
            Check(!decode.Success, "an empty cells list is rejected");
        }

        private static void TestBlueprintJsonRejectsUnsupportedOrientation()
        {
            Console.WriteLine("[TestBlueprintJsonRejectsUnsupportedOrientation]");
            string json = "{\"schemaVersion\":1,\"cells\":[{\"x\":0,\"y\":0,\"z\":0,\"key\":\"StoneBase\",\"d\":4}]}";
            BlueprintJsonDecodeResult decode = BlueprintJson.Decode(json);
            Check(!decode.Success, "a per-cell data/orientation payload (reserved, unreproducible in v1) is rejected");
            Check(decode.FirstError != null && decode.FirstError.Contains("朝向"), "reason explains the unsupported orientation");
        }

        private static void TestBlueprintJsonRejectsBuildOps()
        {
            Console.WriteLine("[TestBlueprintJsonRejectsBuildOps]");
            string json = "{\"schemaVersion\":1,\"cells\":[{\"x\":0,\"y\":0,\"z\":0,\"key\":\"StoneBase\"}]," +
                "\"buildOps\":[{\"op\":\"fill\"}]}";
            BlueprintJsonDecodeResult decode = BlueprintJson.Decode(json);
            Check(!decode.Success, "a blueprint carrying executable buildOps is rejected rather than partially applied");
        }

        private static void TestBlueprintJsonRejectsSizeMismatch()
        {
            Console.WriteLine("[TestBlueprintJsonRejectsSizeMismatch]");
            // cells span x=0..0 (size 1) but declares size x=9 -> tampered/hallucinated file.
            string json = "{\"schemaVersion\":1,\"size\":{\"x\":9,\"y\":1,\"z\":1}," +
                "\"cells\":[{\"x\":0,\"y\":0,\"z\":0,\"key\":\"StoneBase\"}]}";
            BlueprintJsonDecodeResult decode = BlueprintJson.Decode(json);
            Check(!decode.Success, "a declared size inconsistent with the cells bounding box is rejected");
        }

        // Rotation applied to a decoded blueprint must match rotation applied to the
        // original and still round-trip, so AI/copied structures survive transforms.
        private static void TestBlueprintJsonRotationTransformReproduces()
        {
            Console.WriteLine("[TestBlueprintJsonRotationTransformReproduces]");
            Blueprint original = BuildingGeneratorRegistry.Default.Generate(new GenerationContext(0));
            Blueprint decoded = BlueprintJson.Decode(BlueprintJson.Serialize(original)).Blueprint;
            Check(decoded != null, "decoded cottage available for transform test");
            if (decoded == null) return;

            Blueprint rotOrig = original.Reorient(1, false);
            Blueprint rotDecoded = decoded.Reorient(1, false);
            Check(SameCellSet(rotOrig, rotDecoded), "one quarter-turn of the decoded blueprint matches the original's");
            Check(rotDecoded.SizeX == original.SizeZ && rotDecoded.SizeZ == original.SizeX,
                "quarter-turn swaps the footprint axes after round-trip");

            // A rotated blueprint must itself still round-trip through JSON unchanged.
            string rjson = BlueprintJson.Serialize(rotDecoded);
            BlueprintJsonDecodeResult r = BlueprintJson.Decode(rjson);
            Check(r.Success && SameCellSet(rotDecoded, r.Blueprint), "a rotated blueprint round-trips through JSON identically");
        }

        // --- v0.8.0: the blueprint tool's library browser (pure, no files/UI). ---

        // The cursor that walks the blueprint folder on each tap: an empty library is
        // nothing to load (-1), a fresh session (cursor -1) starts at the first entry,
        // taps move forward monotonically, and the last entry wraps back to the first.
        private static void TestBlueprintLibraryBrowserAdvance()
        {
            Console.WriteLine("[TestBlueprintLibraryBrowserAdvance]");

            Check(BlueprintLibraryBrowser.Advance(0, -1) == -1, "an empty library (count 0) loads nothing (-1)");
            Check(BlueprintLibraryBrowser.Advance(-3, 2) == -1, "a negative count is treated as empty (-1)");

            // Fresh session: cursor starts at -1, first tap selects index 0.
            Check(BlueprintLibraryBrowser.Advance(3, -1) == 0, "first tap from a fresh cursor (-1) selects entry 0");

            // Monotonic forward stepping.
            Check(BlueprintLibraryBrowser.Advance(3, 0) == 1, "second tap advances 0 -> 1");
            Check(BlueprintLibraryBrowser.Advance(3, 1) == 2, "third tap advances 1 -> 2");

            // Wrap-around: from the last entry the next tap returns to the first.
            Check(BlueprintLibraryBrowser.Advance(3, 2) == 0, "a tap past the last entry wraps back to 0");

            // Single-entry library always stays on 0 (browse is a no-op but never -1).
            Check(BlueprintLibraryBrowser.Advance(1, 0) == 0, "a one-entry library always resolves to index 0");

            // Simulate N taps over a 2-entry library: sequence must be 0,1,0,1,...
            int cursor = -1;
            bool alternates = true;
            for (int i = 0; i < 6; i++)
            {
                cursor = BlueprintLibraryBrowser.Advance(2, cursor);
                int expected = i % 2; // 0,1,0,1,0,1
                if (cursor != expected) alternates = false;
            }
            Check(alternates, "six taps over a 2-entry library alternate 0,1,0,1,0,1 (clean cycling)");
        }

        // The "[i/N]" position shown in the import toast: 1-based display index, and an
        // empty string whenever there is nothing valid to show (guards malformed state).
        private static void TestBlueprintLibraryBrowserPositionLabel()
        {
            Console.WriteLine("[TestBlueprintLibraryBrowserPositionLabel]");

            Check(BlueprintLibraryBrowser.PositionLabel(4, 0) == "[1/4]", "first entry of four shows [1/4]");
            Check(BlueprintLibraryBrowser.PositionLabel(4, 3) == "[4/4]", "last entry of four shows [4/4]");
            Check(BlueprintLibraryBrowser.PositionLabel(1, 0) == "[1/1]", "a single entry shows [1/1]");
            Check(BlueprintLibraryBrowser.PositionLabel(0, 0) == string.Empty, "an empty library yields no label");
            Check(BlueprintLibraryBrowser.PositionLabel(4, -1) == string.Empty, "a pre-selection cursor (-1) yields no label");
            Check(BlueprintLibraryBrowser.PositionLabel(4, 9) == string.Empty, "an out-of-range cursor yields no label (defensive)");
        }

        // === v0.8.1 clipboard-paste import: BlueprintImportService ===

        // A known-good JSON string produced by the SAME serializer the game uses to
        // persist a copied blueprint, so the paste path is exercised with real bytes.
        private static string GoodBlueprintJson()
        {
            BlueprintBuilder b = new BlueprintBuilder();
            b.Set(0, 0, 0, BuildingMaterials.StoneBase);
            b.Set(1, 0, 0, BuildingMaterials.StoneBase);
            b.Set(0, 1, 0, BuildingMaterials.Glass);
            Blueprint bp = b.ToBlueprint("paste-sample", 1);
            return BlueprintJson.Serialize(bp);
        }

        // Resolver that accepts every logical material key used in tests.
        private static bool AcceptAllMaterials(string key) { return true; }

        private static void TestImportValidBlueprint()
        {
            Console.WriteLine("[TestImportValidBlueprint]");
            string json = GoodBlueprintJson();
            BlueprintImportResult r = BlueprintImportService.FromString(json, AcceptAllMaterials);
            Check(r.Success, "a valid pasted JSON blueprint imports successfully");
            Check(r.Blueprint != null, "success yields a non-null blueprint");
            Check(r.BlockCount == 3, "imported blueprint has the expected 3 cells (got " + r.BlockCount + ")");
            Check(!string.IsNullOrEmpty(r.DisplayName) && r.DisplayName == "paste-sample", "display name is preserved from the JSON");
            Check(r.SizeX == 2 && r.SizeY == 2 && r.SizeZ == 1, "recomputed size matches the cell set (" + r.SizeX + "x" + r.SizeY + "x" + r.SizeZ + ")");
        }

        private static void TestImportRejectsEmptyText()
        {
            Console.WriteLine("[TestImportRejectsEmptyText]");
            Check(!BlueprintImportService.FromString(null, AcceptAllMaterials).Success, "null text is rejected");
            Check(!BlueprintImportService.FromString(string.Empty, AcceptAllMaterials).Success, "empty text is rejected");
            Check(!BlueprintImportService.FromString("   \t  ", AcceptAllMaterials).Success, "whitespace-only text is rejected");
            BlueprintImportResult r = BlueprintImportService.FromString("   ", AcceptAllMaterials);
            Check(r.Reason != null && r.Reason.Length > 0, "empty text carries a human-readable reason");
        }

        private static void TestImportRejectsNonJsonShape()
        {
            Console.WriteLine("[TestImportRejectsNonJsonShape]");
            BlueprintImportResult r = BlueprintImportService.FromString("CobblestoneBlock x 5 at 10,20,30", AcceptAllMaterials);
            Check(!r.Success, "plain prose that is not JSON is rejected");
            Check(r.Blueprint == null, "a rejected non-JSON paste yields NO blueprint");
            Check(r.Reason != null && r.Reason.IndexOf("JSON") >= 0, "the reason points at the JSON shape problem");
        }

        private static void TestImportRejectsOverSizeInput()
        {
            Console.WriteLine("[TestImportRejectsOverSizeInput]");
            // Starts with '{' so it passes the cheap shape gate, but is far over the cap,
            // proving the size guard fires BEFORE the tokenizer is ever asked to chew it.
            string huge = "{" + new string('a', BlueprintImportService.MaxImportChars);
            BlueprintImportResult r = BlueprintImportService.FromString(huge, AcceptAllMaterials);
            Check(!r.Success, "a pasted blob larger than the cap is rejected");
            Check(r.Blueprint == null, "an over-size paste yields NO blueprint");
            Check(r.Reason != null && r.Reason.IndexOf("过大") >= 0, "the reason says the input is too large");
        }

        private static void TestImportRejectsInvalidJson()
        {
            Console.WriteLine("[TestImportRejectsInvalidJson]");
            // Starts with '{' (shape gate passes) but is not parseable JSON.
            BlueprintImportResult r = BlueprintImportService.FromString("{ this is : not valid , json ", AcceptAllMaterials);
            Check(!r.Success, "malformed JSON is rejected");
            Check(r.Blueprint == null, "malformed JSON yields NO blueprint");
            Check(r.Reason != null && r.Reason.Length > 0, "malformed JSON surfaces a concrete parse reason");
        }

        private static void TestImportRejectsDuplicateCoords()
        {
            Console.WriteLine("[TestImportRejectsDuplicateCoords]");
            string json = "{\"schemaVersion\":1,\"kind\":\"builderassistant.blueprint\",\"name\":\"dup\"," +
                          "\"materials\":[{\"id\":0,\"key\":\"" + BuildingMaterials.StoneBase + "\",\"block\":\"\"}]," +
                          "\"cells\":[{\"x\":0,\"y\":0,\"z\":0,\"m\":0},{\"x\":0,\"y\":0,\"z\":0,\"m\":0}]}";
            BlueprintImportResult r = BlueprintImportService.FromString(json, AcceptAllMaterials);
            Check(!r.Success, "two cells at the same coordinate are rejected");
            Check(r.Blueprint == null, "a duplicate-coordinate paste yields NO blueprint");
        }

        private static void TestImportRejectsOverCapBlueprint()
        {
            Console.WriteLine("[TestImportRejectsOverCapBlueprint]");
            // 1025 unique cells: the count guard trips before any axis/coordinate check,
            // proving a pathologically large (or malicious) blueprint is refused outright.
            var sb = new System.Text.StringBuilder();
            sb.Append("{\"schemaVersion\":1,\"kind\":\"builderassistant.blueprint\",\"name\":\"big\",");
            sb.Append("\"materials\":[{\"id\":0,\"key\":\"").Append(BuildingMaterials.StoneBase).Append("\",\"block\":\"\"}],");
            sb.Append("\"cells\":[");
            int n = BuildSafetyLimits.MaxBlocksPerOperation + 1;
            for (int i = 0; i < n; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append("{\"x\":0,\"y\":0,\"z\":0,\"m\":0}"); // coords irrelevant: count trips first
            }
            sb.Append("]}");
            BlueprintImportResult r = BlueprintImportService.FromString(sb.ToString(), AcceptAllMaterials);
            Check(!r.Success, "a blueprint over the per-transaction block cap is rejected");
            Check(r.Blueprint == null, "an over-cap paste yields NO partial blueprint");
            Check(r.Reason != null && r.Reason.IndexOf("超过") >= 0, "the reason names the cap being exceeded");
        }

        private static void TestImportRejectsUnknownMaterial()
        {
            Console.WriteLine("[TestImportRejectsUnknownMaterial]");
            BlueprintBuilder b = new BlueprintBuilder();
            b.Set(0, 0, 0, BuildingMaterials.StoneBase);
            b.Set(1, 0, 0, "MysteryBlock"); // a block this build cannot rebuild
            string json = BlueprintJson.Serialize(b.ToBlueprint("unknown-mat", 1));

            // Structural decode succeeds (the shape is legal); only the material pre-flight
            // must reject it — proving the resolver hook is actually consulted.
            BlueprintImportResult withReject = BlueprintImportService.FromString(
                json, delegate(string key) { return key != "MysteryBlock"; });
            Check(!withReject.Success, "a material the resolver rejects blocks the import");
            Check(withReject.Blueprint == null, "an unsupported-material paste yields NO blueprint");
            Check(withReject.Reason != null && withReject.Reason.IndexOf("MysteryBlock") >= 0, "the reason names the offending material");

            BlueprintImportResult acceptAll = BlueprintImportService.FromString(json, AcceptAllMaterials);
            Check(acceptAll.Success, "the identical JSON imports when every material is supported");
        }

        // The heart of "失败不污染剪贴板": on EVERY failure path the service returns
        // Success=false with a null blueprint, so the runtime literally has nothing to
        // write into the clipboard unless the import genuinely succeeded.
        private static void TestImportFailureNeverYieldsPartialBlueprint()
        {
            Console.WriteLine("[TestImportFailureNeverYieldsPartialBlueprint]");
            string[] badInputs =
            {
                null,
                string.Empty,
                "   ",
                "not json at all",
                "{ broken ",
                "{\"schemaVersion\":99,\"cells\":[{\"x\":0,\"y\":0,\"z\":0,\"key\":\"" + BuildingMaterials.StoneBase + "\"}]}",
                "{\"schemaVersion\":1,\"cells\":[]}"
            };
            bool allNullBlueprint = true;
            bool allFailed = true;
            foreach (string s in badInputs)
            {
                BlueprintImportResult r = BlueprintImportService.FromString(s, AcceptAllMaterials);
                if (r.Success) allFailed = false;
                if (r.Blueprint != null) allNullBlueprint = false;
            }
            Check(allFailed, "every malformed/unsupported input fails");
            Check(allNullBlueprint, "no failure path ever returns a partial blueprint (clipboard stays intact)");
        }

        // === v0.8.1 naming policy: BlueprintNamePolicy ===

        private static void TestNamePolicySanitize()
        {
            Console.WriteLine("[TestNamePolicySanitize]");
            Check(BlueprintNamePolicy.Sanitize("My House_2024!") == "MyHouse_2024", "spaces/punct stripped, [A-Za-z0-9_-] kept");
            Check(BlueprintNamePolicy.Sanitize("___abc___") == "abc", "leading/trailing separators trimmed");
            Check(BlueprintNamePolicy.Sanitize("小屋") == string.Empty, "a pure-CJK name has no portable stem via Sanitize");
            string longName = new string('a', 80);
            Check(BlueprintNamePolicy.Sanitize(longName).Length == BlueprintNamePolicy.MaxStemLength, "stem is capped to MaxStemLength (" + BlueprintNamePolicy.MaxStemLength + ")");
        }

        private static void TestNamePolicyCjkNoLongerCollapses()
        {
            Console.WriteLine("[TestNamePolicyCjkNoLongerCollapses]");
            string a = BlueprintNamePolicy.ToStem("小屋");
            string b = BlueprintNamePolicy.ToStem("城堡");
            Check(a.StartsWith("bp_"), "a pure-CJK name becomes a bp_<hash> stem instead of 'blueprint'");
            Check(a != b, "two DIFFERENT Chinese names map to DIFFERENT stems (the old collapse bug is fixed)");
            Check(BlueprintNamePolicy.ToStem("小屋") == a, "the same Chinese name always yields the same stem (deterministic)");
            // ASCII names keep their readable stem.
            Check(BlueprintNamePolicy.ToStem("MyCottage") == "MyCottage", "an ASCII name keeps its readable stem");
        }

        private static void TestNamePolicyResolveUniqueNeverOverwrites()
        {
            Console.WriteLine("[TestNamePolicyResolveUniqueNeverOverwrites]");

            // Fresh library: the plain stem is returned and no collision is reported.
            string collided;
            string first = BlueprintNamePolicy.ResolveUnique("MyCottage", new List<string>(), out collided);
            Check(first == "MyCottage", "a free name uses its base stem");
            Check(collided == null, "a free name reports no collision");

            // A stem already taken -> _2, and collidedWith reports the original.
            var existing = new List<string> { "MyCottage" };
            string second = BlueprintNamePolicy.ResolveUnique("MyCottage", existing, out collided);
            Check(second == "MyCottage_2", "a colliding name is numbered to _2 (never overwrites)");
            Check(collided == "MyCottage", "the collision hint names the original stem");

            // _2 also taken -> _3.
            existing.Add("MyCottage_2");
            string third = BlueprintNamePolicy.ResolveUnique("MyCottage", existing, out collided);
            Check(third == "MyCottage_3", "numbering continues to _3 when _2 is taken");

            // The guarantee that matters for non-overwrite: the result is never an existing stem.
            existing.Add("MyCottage_3");
            string fourth = BlueprintNamePolicy.ResolveUnique("MyCottage", existing, out collided);
            Check(!existing.Contains(fourth), "the resolved stem is NEVER already present in the folder");

            // CJK duplicate: two identical Chinese names must not collide onto one file.
            string cjkStem = BlueprintNamePolicy.ToStem("秘密小屋");
            string cjkDup = BlueprintNamePolicy.ResolveUnique("秘密小屋", new List<string> { cjkStem }, out collided);
            Check(cjkDup != cjkStem && cjkDup.StartsWith(cjkStem), "a duplicate Chinese-named blueprint gets a numbered stem, not an overwrite");
        }

        private static void TestNamePolicyStableHashDeterministic()
        {
            Console.WriteLine("[TestNamePolicyStableHashDeterministic]");
            Check(BlueprintNamePolicy.StableHash("小屋") == BlueprintNamePolicy.StableHash("小屋"), "hash of the same text is stable");
            Check(BlueprintNamePolicy.StableHash("小屋") != BlueprintNamePolicy.StableHash("城堡"), "different text yields a different hash");
            Check(BlueprintNamePolicy.StableHash(string.Empty).Length == 8, "the hash renders as 8 hex chars");
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

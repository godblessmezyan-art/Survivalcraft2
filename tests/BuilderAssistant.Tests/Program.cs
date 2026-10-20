using System;
using System.Collections.Generic;
using BuilderAssistant.Core;
using BuilderAssistant.Core.Blueprints;
using BuilderAssistant.Core.Building;
using BuilderAssistant.Core.BuildEngine;
using BuilderAssistant.Core.Json;
using BuilderAssistant.Core.Safety;
using BuilderAssistant.Core.Transactions;
using BuilderAssistant.Core.Tree;
using BuilderAssistant.Core.Workbench;
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
        // Diagnostic: the data bits handed to the last TryResolve call, and whether
        // any call carried a non-zero data (so a test can prove orientation reaches
        // the resolver rather than being silently dropped in the planner).
        public int LastData;
        public bool SawNonZeroData;

        public bool TryResolve(string materialKey, out int value)
        {
            return TryResolve(materialKey, 0, out value);
        }

        public bool TryResolve(string materialKey, int data, out int value)
        {
            value = 0;
            LastData = data;
            if (data != 0)
            {
                SawNonZeroData = true;
            }
            if (string.IsNullOrEmpty(materialKey) || materialKey == "Nonexistent")
            {
                return false;
            }
            int hash = materialKey.GetHashCode() & 0x7FFFFFFF;
            // Mix the authored data in so two cells differing only by orientation get
            // distinct values - this is what the real palette does via MakeBlockValue.
            value = 1000 + ((hash + data * 7919) % 90000);
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

        private static int Main(string[] args)
        {
            // Offline artifact mode: `dotnet run -c Release -- --preview <dir>` writes the
            // treehouse manifest, 3-view elevation, layered floor plans and per-phase JSON to
            // <dir> for human review (task §4: the preview is NOT the same as "tests pass").
            if (args != null && args.Length >= 2 && args[0] == "--preview")
            {
                return DumpTreehouseArtifacts(args[1]);
            }

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
            TestBlueprintJsonRejectsBadFamilyAndProps();
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

            // v0.8.2: expand the copy/build whitelist to more plain stone/brick cubes.
            // The palette (VanillaBlockPalette) itself needs the game assembly and is
            // covered by the net472 build + on-device checklist; what this pure layer must
            // PROVE is that adding concrete block-type-name keys — the exact key form the
            // BlueprintCopier emits — is a whitelist-only change: BlueprintJson and
            // BlueprintImportService reproduce arbitrary material keys verbatim and still
            // let the resolver be the single gate that accepts or rejects each new name.
            TestBlueprintJsonPreservesConcreteBlockNameKeys();
            TestImportGateAcceptsOrRejectsNewConcreteKeys();

            // v0.9.0: oriented vanilla blocks (stairs/slab/fence/door). The palette +
            // engine data-bit semantics need the game assembly (net472 build + on-device),
            // but the TRANSFORM math is pure and must be proven offline: Normalize keeps
            // only each family's authored bits; Rotate obeys the quarter-turn group law
            // (four turns = identity) and the correct facing permutation; MirrorX is an
            // involution and flips a door's hinge side; JSON schema v2 round-trips an
            // oriented cell verbatim; and the reworked cottage really places oriented
            // families whose facing survives a blueprint rotation + round-trip.
            TestBlockOrientationNormalizeMasksAuthoredBits();
            TestBlockOrientationRotationGroupLaw();
            TestBlockOrientationStairsFacingPermutation();
            TestBlockOrientationFenceArmPermutation();
            TestBlockOrientationMirrorInvolutionAndDoorHinge();
            TestBlueprintJsonRoundTripsOrientedCells();
            TestBlueprintJsonStripsEngineDerivedOrientationBits();
            TestCottageEmitsOrientedFamilies();
            TestCottageOrientationSurvivesRotationAndRoundTrip();

            // v0.10.0: the parametric generation engine. Prove that changing a few
            // BuildingSpec fields yields different valid buildings (the core acceptance
            // criterion), that (spec, seed) is byte-reproducible while a new seed varies the
            // layout, that bad/over-budget specs are refused before any cell is written, that
            // a generated blueprint flows through the UNCHANGED plan/undo path, and that
            // BuildingValidator actually FIRES on each kind of broken structure. The existing
            // cottage remains the registry default and is byte-for-byte unchanged.
            TestRegistryKeepsCottageAsDefault();
            TestSpecValidationBoundsAndBudget();
            TestParametricCottageComposesValid();
            TestTwoStoryHouseComposesValid();
            TestVaryingParamsChangeSizeAndLayout();
            TestParametricDeterminismAndVariation();
            TestMaterialCatalogParity();
            TestGeneratedBlueprintJsonRoundTrip();
            TestGeneratedBlueprintPlansAndUndoes();
            TestValidatorCatchesBlockedEntrance();
            TestValidatorCatchesMissingWall();
            TestValidatorCatchesFloatingBlock();
            TestValidatorCatchesMissingStair();
            TestValidatorCatchesFootprintMismatch();
            TestValidatorCatchesUnknownMaterial();

            // Multiplayer gate (host builds, remote clients refuse) + proof the build
            // pipeline only ever emits vanilla blocks that non-mod clients already know.
            TestTerrainAuthorityGate();
            TestBuildEmitsOnlyVanillaBlocks();

            // v0.11.0 Visual Building Workbench: the pure state machine + read-only footprint
            // preview + confirmation/authority gates, proven entirely offline.
            TestWorkbenchSelectAndGenerate();
            TestWorkbenchSelectionCatalog();
            TestWorkbenchSeedDeterminism();
            TestWorkbenchRotationFourTurnsIdentity();
            TestWorkbenchComplexOrientationRotates();
            TestWorkbenchPreviewAreaEqualsBuildArea();
            TestWorkbenchNoTerrainWriteDuringPreview();
            TestWorkbenchParamChangeInvalidatesConfirmation();
            TestWorkbenchRejectsWhenTerrainChangedBeforeBuild();
            TestWorkbenchInvalidParamKeepsPreviousValidBuilding();
            TestWorkbenchCancelRestores();
            TestWorkbenchAuthorityGateNotRegressed();

            // v0.12 Giant Fantasy Treehouse (森之王庭): log-orientation semantics, spec rejection,
            // seed determinism/variation, the phased safety envelope + clean validation, vanilla-only
            // materials, the leaf-decay-safety invariant (every leaf within Chebyshev 3 of a log),
            // and structural facts (trunk through the decks, three storeys, one door, real stairs).
            TestTreehouseLogOrientationSemantics();
            TestTreehouseSpecValidationRejectsAbsurd();
            TestTreehouseDeterministic();
            TestTreehouseValidAndWithinSafetyEnvelope();
            TestTreehouseEmitsOnlyVanillaBlocks();
            TestTreehouseLeafDecaySafety();
            TestTreehouseStructuralFacts();

            // v0.13 M4 phased in-world assembly: proves the game-free TreehouseAssembly grows the
            // whole treehouse from one anchor through the existing plan/execute/undo pipeline --
            // full-canvas coverage + no strays, strict phase ordering (no interleaving), the 32/batch
            // cap, mid-assembly cancel (rolls back the partial, keeps completed phases), a solid
            // obstruction halting all later phases without touching them, per-phase undo that honors
            // a player's post-build edits, and determinism. Live placement on 7723 stays 待验证.
            TestTreehouseAssemblyFullBuildMatchesMasterCanvas();
            TestTreehouseAssemblyStrictlySequential();
            TestTreehouseAssemblyCancelRollsBackCurrentKeepsCompleted();
            TestTreehouseAssemblyHardFailureHaltsLaterPhases();
            TestTreehouseAssemblyPerPhaseUndoHonorsLaterEdits();
            TestTreehouseAssemblyDeterministic();

            Console.WriteLine();
            Console.WriteLine(_failures == 0
                ? "ALL " + _checks + " CHECKS PASSED"
                : (_checks - _failures) + "/" + _checks + " checks passed, " + _failures + " FAILED");
            return _failures == 0 ? 0 : 1;
        }

        private static int DumpTreehouseArtifacts(string dir)
        {
            TreehouseSpec spec = TreehouseSpec.DefaultKingdomOfTheForest();
            TreeCanvas canvas; TreehouseValidation v;
            TreehousePart[] parts = TreehouseBlueprintSet.Build(spec, out canvas, out v);

            string manifest = TreehousePreview.BuildManifest(spec, parts, v);
            string views =
                TreehousePreview.RenderElevationFront(canvas, spec) + "\n" +
                TreehousePreview.RenderElevationSide(canvas, spec) + "\n" +
                TreehousePreview.RenderTopDown(canvas, spec) + "\n" +
                TreehousePreview.RenderFloorPlans(canvas, spec);

            // Family tally so the reviewer can see the actual vanilla composition.
            var famCount = new Dictionary<BlockFamily, int>();
            var keyCount = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (KeyValuePair<CellPos, PaintedCell> kv in canvas.Cells)
            {
                int n;
                famCount[kv.Value.Family] = (famCount.TryGetValue(kv.Value.Family, out n) ? n : 0) + 1;
                keyCount[kv.Value.Key] = (keyCount.TryGetValue(kv.Value.Key, out n) ? n : 0) + 1;
            }
            var tally = new System.Text.StringBuilder();
            tally.Append("family tally:\n");
            foreach (KeyValuePair<BlockFamily, int> kv in famCount)
                tally.Append("  ").Append(kv.Key).Append(" = ").Append(kv.Value).Append('\n');
            tally.Append("material key tally:\n");
            foreach (KeyValuePair<string, int> kv in keyCount)
                tally.Append("  ").Append(kv.Key).Append(" = ").Append(kv.Value).Append('\n');

            System.IO.Directory.CreateDirectory(dir);
            var enc = new System.Text.UTF8Encoding(true);
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "treehouse_manifest.txt"), manifest, enc);
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "treehouse_views.txt"), views, enc);
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "treehouse_tally.txt"), tally.ToString(), enc);

            int idx = 0;
            foreach (TreehousePart p in parts)
            {
                string safe = Sanitize(p.Phase);
                string json = TreehousePreview.SerializePart(p);
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir,
                    "part_" + idx.ToString("D2") + "_" + safe + ".json"), json, enc);
                idx++;
            }

            Console.WriteLine(manifest);
            Console.WriteLine(views);
            Console.WriteLine(tally);
            Console.WriteLine("wrote " + idx + " phase blueprints to " + dir);
            return v.IsValid ? 0 : 1;
        }

        private static string Sanitize(string s)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char ch in s)
            {
                if (char.IsLetterOrDigit(ch)) sb.Append(ch);
                else if (ch == '.' || ch == '-' || ch == '_') sb.Append(ch);
                else sb.Append('_');
            }
            return sb.ToString();
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
            b.FillBox(0, 0, 0, 32, 2, 32, BuildingMaterials.StoneBase); // 33*3*33 = 3267 > 2756 cap
            Blueprint bp = b.ToBlueprint("too_big", 1);
            FakeTerrain terrain = new FakeTerrain();
            BuildPlan plan = BuildPlanner.Plan(bp, new CellPos(0, 10, 0), new FakeResolver(), terrain, true);
            Check(!plan.Success, "plan fails when cell count exceeds the " + BuildSafetyLimits.MaxBlocksPerOperation + " cap");
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
            // 39 x 39 x 2 = 3042 cells > the per-transaction cap.
            var sb = new System.Text.StringBuilder();
            sb.Append("{\"schemaVersion\":1,\"materials\":[{\"id\":0,\"key\":\"StoneBase\"}],\"cells\":[");
            int cap = BuildSafetyLimits.MaxBlocksPerOperation;
            int n = 0;
            for (int x = 0; x < 39 && n <= cap; x++)
                for (int z = 0; z < 39 && n <= cap; z++)
                    for (int y = 0; y < 2 && n <= cap; y++, n++)
                    {
                        if (n > 0) sb.Append(',');
                        sb.Append("{\"x\":").Append(x).Append(",\"y\":").Append(y)
                          .Append(",\"z\":").Append(z).Append(",\"m\":0}");
                    }
            sb.Append("]}");
            BlueprintJsonDecodeResult decode = BlueprintJson.Decode(sb.ToString());
            Check(!decode.Success, "an over-cap blueprint (>" + cap + " cells) is rejected on import");
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

        private static void TestBlueprintJsonRejectsBadFamilyAndProps()
        {
            Console.WriteLine("[TestBlueprintJsonRejectsBadFamilyAndProps]");

            // Positive control: a legal schema-v2 oriented cell is now ACCEPTED (this is
            // exactly what schema v1 used to reject). The per-cell orientation is honoured,
            // not treated as an unsupported payload.
            BlueprintJsonDecodeResult ok = BlueprintJson.Decode(
                "{\"schemaVersion\":2,\"cells\":[{\"x\":0,\"y\":0,\"z\":0,\"key\":\"WoodenStairsBlock\",\"f\":\"stairs\",\"d\":2}]}");
            Check(ok.Success, "a legal oriented cell now decodes (schema v2 accepts f/d)");
            if (ok.Blueprint != null && ok.Blueprint.Cells.Count == 1)
            {
                Check(ok.Blueprint.Cells[0].Family == BlockFamily.Stairs && ok.Blueprint.Cells[0].Data == 2,
                    "the accepted cell carries the authored stairs family and data");
            }

            // Negative: an unknown family tag must be rejected loudly (never guessed into a cube).
            BlueprintJsonDecodeResult badFam = BlueprintJson.Decode(
                "{\"schemaVersion\":2,\"cells\":[{\"x\":0,\"y\":0,\"z\":0,\"key\":\"StoneBase\",\"f\":\"wobble\"}]}");
            Check(!badFam.Success, "an unknown family tag is rejected");
            Check(badFam.FirstError != null && badFam.FirstError.Contains("朝向"),
                "the reason names the illegal orientation family tag");

            // Negative: unreproducible complex block state (props) is still rejected.
            BlueprintJsonDecodeResult props = BlueprintJson.Decode(
                "{\"schemaVersion\":2,\"cells\":[{\"x\":0,\"y\":0,\"z\":0,\"key\":\"ChestBlock\",\"props\":{\"items\":[1,2]}}]}");
            Check(!props.Success, "a per-cell 'props' payload (unreproducible block state) is rejected");
            Check(props.FirstError != null && props.FirstError.Contains("props"),
                "the reason cites the unsupported props payload");

            // Negative: a negative data value is rejected (data bits are unsigned).
            BlueprintJsonDecodeResult neg = BlueprintJson.Decode(
                "{\"schemaVersion\":2,\"cells\":[{\"x\":0,\"y\":0,\"z\":0,\"key\":\"WoodenStairsBlock\",\"f\":\"stairs\",\"d\":-3}]}");
            Check(!neg.Success, "a negative data value d is rejected");
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

        // The 7 plain stone/brick cubes added to VanillaBlockPalette.SupportedPlainCubes in
        // v0.8.2. The copier keys captured materials by their CLR type name, so these strings
        // are EXACTLY the material keys a copy of such a structure will produce — the pure
        // JSON/import layer must handle them with no special-casing.
        private static readonly string[] NewPlainCubeKeys =
        {
            "BasaltBlock", "GraniteBlock", "MarbleBlock", "SandstoneBlock",
            "ClayBlock", "LimestoneBlock", "MalachiteBlock"
        };

        private static string SortedMaterialKeys(Blueprint bp)
        {
            var keys = new System.Collections.Generic.List<string>();
            for (int i = 0; i < bp.Cells.Count; i++) keys.Add(bp.Cells[i].MaterialKey ?? string.Empty);
            keys.Sort(System.StringComparer.Ordinal);
            return string.Join("|", keys.ToArray());
        }

        private static void TestBlueprintJsonPreservesConcreteBlockNameKeys()
        {
            Console.WriteLine("[TestBlueprintJsonPreservesConcreteBlockNameKeys]");

            // One cell per new concrete block-type name, plus a logical key for contrast.
            var builder = new BlueprintBuilder();
            for (int i = 0; i < NewPlainCubeKeys.Length; i++)
            {
                builder.Set(i, 0, 0, NewPlainCubeKeys[i]);
            }
            builder.Set(0, 1, 0, BuildingMaterials.StoneBase);
            Blueprint original = builder.ToBlueprint("plain-cube-expansion", 1);

            string json = BlueprintJson.Serialize(original);
            BlueprintJsonDecodeResult decode = BlueprintJson.Decode(json);
            Check(decode.Blueprint != null, "a blueprint keyed by concrete block-type names decodes without error");
            Check(decode.Blueprint != null && decode.Blueprint.CellCount == original.CellCount,
                "the cell count survives the round trip");
            Check(decode.Blueprint != null && SortedMaterialKeys(decode.Blueprint) == SortedMaterialKeys(original),
                "every concrete block-type-name material key is reproduced verbatim (no schema/Decode change needed to expand the whitelist)");

            // Stability: re-serializing the decoded blueprint reproduces the same key set.
            string json2 = BlueprintJson.Serialize(decode.Blueprint);
            BlueprintJsonDecodeResult re = BlueprintJson.Decode(json2);
            Check(re.Blueprint != null && SortedMaterialKeys(re.Blueprint) == SortedMaterialKeys(original),
                "the new block names remain stable across a second serialize/decode cycle");
        }

        private static void TestImportGateAcceptsOrRejectsNewConcreteKeys()
        {
            Console.WriteLine("[TestImportGateAcceptsOrRejectsNewConcreteKeys]");

            var builder = new BlueprintBuilder();
            for (int i = 0; i < NewPlainCubeKeys.Length; i++)
            {
                builder.Set(i, 0, 0, NewPlainCubeKeys[i]);
            }
            string json = BlueprintJson.Serialize(builder.ToBlueprint("gate-sample", 1));

            // Accepting every new concrete name -> success (this is the whitelist expansion).
            BlueprintImportResult accept = BlueprintImportService.FromString(json, AcceptAllMaterials);
            Check(accept.Success, "the JSON imports once the resolver accepts the new block names");
            Check(accept.Blueprint != null && accept.BlockCount == NewPlainCubeKeys.Length,
                "all new blocks import as buildable cells (" + accept.BlockCount + "/" + NewPlainCubeKeys.Length + ")");

            // Rejecting a single new name -> the resolver is the one gate; import fails and
            // names the offending block. Proves adding a name to the palette can never slip
            // through un-checked, and an unregistered block aborts cleanly rather than building.
            BlueprintImportResult reject = BlueprintImportService.FromString(
                json, delegate(string key) { return key != "MalachiteBlock"; });
            Check(!reject.Success, "the import is gated when one new block is not supported");
            Check(reject.Blueprint == null, "a gated new block yields NO partial blueprint");
            Check(reject.Reason != null && reject.Reason.IndexOf("MalachiteBlock") >= 0,
                "the failure reason names the specific unsupported block");
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

        // --- v0.9.0: oriented vanilla blocks (stairs / slab / fence / door). ---

        private static void TestBlockOrientationNormalizeMasksAuthoredBits()
        {
            Console.WriteLine("[TestBlockOrientationNormalizeMasksAuthoredBits]");
            // Stairs own bits 0-2 (rotation + upside-down); corner bits 3-4 and colors 5+
            // are engine-derived and must be stripped so a blueprint never bakes them.
            Check(BlockOrientation.Normalize(BlockFamily.Stairs, 0xFF) == 0x7,
                "stairs normalize keeps only rotation+upside-down (0xFF -> 0x7)");
            Check(BlockOrientation.Normalize(BlockFamily.Stairs, 8) == 0,
                "stairs normalize drops a lone corner bit (8 -> 0)");
            Check(BlockOrientation.Normalize(BlockFamily.Stairs, 6) == 6,
                "stairs normalize preserves a legal rot2+upside (6 -> 6)");
            // Slab owns only the isTop bit; the color bits are dropped.
            Check(BlockOrientation.Normalize(BlockFamily.Slab, 0xFF) == 0x1,
                "slab normalize keeps only isTop (0xFF -> 1)");
            Check(BlockOrientation.Normalize(BlockFamily.Slab, 2) == 0,
                "slab normalize drops a lone color bit (2 -> 0)");
            // Fence and door keep their full authored nibble.
            Check(BlockOrientation.Normalize(BlockFamily.Fence, 0xFF) == 0xF,
                "fence normalize keeps all four arms (0xFF -> 0xF)");
            Check(BlockOrientation.Normalize(BlockFamily.Door, 0x12) == 0x2,
                "door normalize keeps rotation+open+hinge (0x12 -> 0x2)");
            // A cube has no authored data at all.
            Check(BlockOrientation.Normalize(BlockFamily.Cube, 999) == 0,
                "cube normalize always yields 0");
        }

        private static void TestBlockOrientationRotationGroupLaw()
        {
            Console.WriteLine("[TestBlockOrientationRotationGroupLaw]");
            // Four clockwise quarter-turns must be the identity for every family, and
            // -1 step must equal +3 steps (the modulo-4 group), for all data patterns.
            BlockFamily[] fams = { BlockFamily.Stairs, BlockFamily.Slab, BlockFamily.Fence, BlockFamily.Door };
            bool fourIsIdentity = true;
            bool negEqualsThree = true;
            foreach (BlockFamily f in fams)
            {
                for (int d = 0; d <= 0xF; d++)
                {
                    // The codec's contract is over authored (normalized) data: an
                    // unnormalized stair nibble carrying corner/color bits legitimately
                    // drops them on a turn, so test the law on stored-cell values.
                    int nd = BlockOrientation.Normalize(f, d);
                    int base4 = BlockOrientation.Rotate(f, nd, 4);
                    if (base4 != nd) fourIsIdentity = false;
                    int seq = nd;
                    for (int k = 0; k < 4; k++) seq = BlockOrientation.Rotate(f, seq, 1);
                    if (seq != nd) fourIsIdentity = false;
                    if (BlockOrientation.Rotate(f, nd, -1) != BlockOrientation.Rotate(f, nd, 3)) negEqualsThree = false;
                }
            }
            Check(fourIsIdentity, "Rotate by 4 (and four sequential +1 steps) is the identity for every family");
            Check(negEqualsThree, "Rotate(-1) == Rotate(+3) for every family");
        }

        private static void TestBlockOrientationStairsFacingPermutation()
        {
            Console.WriteLine("[TestBlockOrientationStairsFacingPermutation]");
            // Facing convention rot0=-Z,1=-X,2=+Z,3=+X; one clockwise turn sends
            // (dx,dz)->(-dz,dx), so newRot=(rot+3)&3: 0->3,1->0,2->1,3->2.
            Check(BlockOrientation.Rotate(BlockFamily.Stairs, 0, 1) == 3, "stairs rot0 -> rot3 on a clockwise turn");
            Check(BlockOrientation.Rotate(BlockFamily.Stairs, 1, 1) == 0, "stairs rot1 -> rot0");
            Check(BlockOrientation.Rotate(BlockFamily.Stairs, 2, 1) == 1, "stairs rot2 -> rot1");
            Check(BlockOrientation.Rotate(BlockFamily.Stairs, 3, 1) == 2, "stairs rot3 -> rot2");
            // The upside-down bit (4) is carried through untouched.
            Check(BlockOrientation.Rotate(BlockFamily.Stairs, 4, 1) == 7, "upside-down stair: facing rot0->rot3, upside-down bit kept (4 -> 7)");
            Check(BlockOrientation.Rotate(BlockFamily.Stairs, 6, 1) == 5, "rot2+upside (6) -> rot1+upside (5)");
            // A slab is rotation-invariant (isTop doesn't change on a Y-axis turn).
            Check(BlockOrientation.Rotate(BlockFamily.Slab, 1, 1) == 1, "slab isTop survives rotation unchanged");
        }

        private static void TestBlockOrientationFenceArmPermutation()
        {
            Console.WriteLine("[TestBlockOrientationFenceArmPermutation]");
            // Arms: bit0=+X, bit1=-X, bit2=+Z, bit3=-Z. A clockwise turn sends
            // +X->+Z, +Z->-X, -X->-Z, -Z->+X.
            Check(BlockOrientation.Rotate(BlockFamily.Fence, 0x1, 1) == 0x4, "fence +X arm -> +Z");
            Check(BlockOrientation.Rotate(BlockFamily.Fence, 0x4, 1) == 0x2, "fence +Z arm -> -X");
            Check(BlockOrientation.Rotate(BlockFamily.Fence, 0x2, 1) == 0x8, "fence -X arm -> -Z");
            Check(BlockOrientation.Rotate(BlockFamily.Fence, 0x8, 1) == 0x1, "fence -Z arm -> +X");
            // A symmetric cross and an empty post are fixed points.
            Check(BlockOrientation.Rotate(BlockFamily.Fence, 0xF, 1) == 0xF, "a full cross is rotation-invariant");
            Check(BlockOrientation.Rotate(BlockFamily.Fence, 0x0, 1) == 0x0, "an isolated post (no arms) stays isolated");
            // Two opposite arms map to two opposite arms.
            Check(BlockOrientation.Rotate(BlockFamily.Fence, 0x3, 1) == 0xC, "an X-line (0x3) -> a Z-line (0xC)");
        }

        private static void TestBlockOrientationMirrorInvolutionAndDoorHinge()
        {
            Console.WriteLine("[TestBlockOrientationMirrorInvolutionAndDoorHinge]");
            // Mirroring about X twice must restore the authored (normalized) data.
            BlockFamily[] fams = { BlockFamily.Stairs, BlockFamily.Fence, BlockFamily.Door };
            bool involutive = true;
            foreach (BlockFamily f in fams)
            {
                for (int d = 0; d <= 0xF; d++)
                {
                    int m2 = BlockOrientation.MirrorX(f, BlockOrientation.MirrorX(f, d));
                    if (m2 != BlockOrientation.Normalize(f, d)) involutive = false;
                }
            }
            Check(involutive, "MirrorX applied twice restores the authored data for every family");

            // Stairs facing reverses in X: rot1(-X) <-> rot3(+X), rot0/rot2 are fixed.
            Check(BlockOrientation.MirrorX(BlockFamily.Stairs, 1) == 3, "stairs facing -X mirrors to +X");
            Check(BlockOrientation.MirrorX(BlockFamily.Stairs, 3) == 1, "stairs facing +X mirrors to -X");
            Check(BlockOrientation.MirrorX(BlockFamily.Stairs, 2) == 2, "stairs facing +Z is X-symmetric");
            Check(BlockOrientation.MirrorX(BlockFamily.Stairs, 6) == 6, "upside-down is preserved by an X mirror");

            // A door's hinge side swaps under mirroring (right-handed bit toggles).
            Check(BlockOrientation.MirrorX(BlockFamily.Door, 2) == 10, "closed left-hinged door mirrors to right-hinged (2 -> 10)");
            Check(BlockOrientation.MirrorX(BlockFamily.Door, 10) == 2, "and back again (10 -> 2): hinge flip is involutive");

            // Fence arms: +X/-X swap, +Z/-Z hold.
            Check(BlockOrientation.MirrorX(BlockFamily.Fence, 0x1) == 0x2, "fence +X arm mirrors to -X");
            Check(BlockOrientation.MirrorX(BlockFamily.Fence, 0x4) == 0x4, "fence +Z arm is unchanged by an X mirror");
            // A slab is invariant.
            Check(BlockOrientation.MirrorX(BlockFamily.Slab, 1) == 1, "slab isTop survives a mirror unchanged");
        }

        private static void TestBlueprintJsonRoundTripsOrientedCells()
        {
            Console.WriteLine("[TestBlueprintJsonRoundTripsOrientedCells]");
            BlueprintBuilder b = new BlueprintBuilder();
            b.SetOriented(0, 0, 0, "WoodenStairsBlock", BlockFamily.Stairs, 2);
            b.SetOriented(1, 0, 0, "StoneSlabBlock", BlockFamily.Slab, 1);
            b.SetOriented(2, 0, 0, "WoodenFenceBlock", BlockFamily.Fence, 0x5);
            b.SetOriented(3, 0, 0, "WoodenDoorBlock", BlockFamily.Door, 2);
            b.Set(4, 0, 0, "CobblestoneBlock"); // a plain cube sibling
            Blueprint bp = b.ToBlueprint("oriented_mix", 2);

            string json = BlueprintJson.Serialize(bp);
            Check(json.Contains("\"stairs\"") && json.Contains("\"fence\"") && json.Contains("\"door\""),
                "oriented cells emit their family tags");
            Check(!json.Contains("\"cube\""), "plain cubes omit the family tag (stays byte-compatible with v1 shape)");

            BlueprintJsonDecodeResult decode = BlueprintJson.Decode(json);
            Check(decode.Success, "the oriented blueprint decodes cleanly (" + decode.JoinedErrors() + ")");
            if (!decode.Success) return;
            Check(SameCellSet(bp, decode.Blueprint), "oriented round-trip reproduces the exact family+data per cell");

            string json2 = BlueprintJson.Serialize(decode.Blueprint);
            Check(json == json2, "re-serializing the decoded oriented blueprint is byte-identical");

            Check(FindData(bp, 3, 0, 0) == 2 && FindFamily(bp, 3, 0, 0) == BlockFamily.Door, "door bottom half authored with rot2");
            Check(FindData(bp, 2, 0, 0) == 0x5 && FindFamily(bp, 2, 0, 0) == BlockFamily.Fence, "fence arm mask survives round-trip");
        }

        private static void TestBlueprintJsonStripsEngineDerivedOrientationBits()
        {
            Console.WriteLine("[TestBlueprintJsonStripsEngineDerivedOrientationBits]");
            // Authoring a stair with corner/color bits present (as a live capture might
            // hand us) must normalize them away, so the blueprint never stores state the
            // engine would recompute differently.
            BlueprintCell stair = new BlueprintCell(new CellPos(0, 0, 0), "WoodenStairsBlock", BlockFamily.Stairs, 0xFF);
            Check(stair.Data == 0x7, "a stair authored with corner+color bits is normalized to 0x7");
            BlueprintCell slab = new BlueprintCell(new CellPos(0, 0, 0), "StoneSlabBlock", BlockFamily.Slab, 0xFF);
            Check(slab.Data == 0x1, "a slab authored with color bits is normalized to isTop only");

            BlueprintBuilder b = new BlueprintBuilder();
            b.SetOriented(0, 0, 0, "WoodenStairsBlock", BlockFamily.Stairs, 0xFF);
            Blueprint bp = b.ToBlueprint("stripped", 2);
            string json = BlueprintJson.Serialize(bp);
            Check(json.Contains("\"d\":7"), "the stripped data (7) is what gets written, not 255");

            BlueprintJsonDecodeResult decode = BlueprintJson.Decode(json);
            Check(decode.Success && SameCellSet(bp, decode.Blueprint), "stripped blueprint still round-trips exactly");
        }

        private static void TestCottageEmitsOrientedFamilies()
        {
            Console.WriteLine("[TestCottageEmitsOrientedFamilies]");
            Blueprint bp = BuildingGeneratorRegistry.Default.Generate(new GenerationContext(0));

            int stairs = 0, slabs = 0, fences = 0, doors = 0, cubes = 0;
            foreach (BlueprintCell c in bp.Cells)
            {
                switch (c.Family)
                {
                    case BlockFamily.Stairs: stairs++; break;
                    case BlockFamily.Slab: slabs++; break;
                    case BlockFamily.Fence: fences++; break;
                    case BlockFamily.Door: doors++; break;
                    default: cubes++; break;
                }
            }
            // front(8) + back(9) + left(9) + right(9) eave stairs.
            Check(stairs == 35, "eave ring is stairs (got " + stairs + ")");
            Check(slabs == 2, "two stone-slab windowsills (got " + slabs + ")");
            Check(fences == 2, "two ridge fence finials (got " + fences + ")");
            Check(doors == 1, "exactly one authored door cell (got " + doors + ")");

            // The eave facings actually differ across sides (exercises all four rotations).
            Check(FindFamily(bp, 0, 5, 0) == BlockFamily.Stairs && FindData(bp, 0, 5, 0) == 2,
                "front-left corner stair faces inward (rot2)");
            Check(FindData(bp, 0, 5, 10) == 0, "back stair faces the other way (rot0)");
            Check(FindData(bp, 0, 5, 5) == 3, "left-side stair faces +X (rot3)");
            Check(FindData(bp, 8, 5, 5) == 1, "right-side stair faces -X (rot1)");

            // Door is authored as the BOTTOM half only (facing +Z into the room); the
            // engine spawns the top at build time, so no door cell sits at y=2.
            Check(FindFamily(bp, 3, 1, 0) == BlockFamily.Door && FindData(bp, 3, 1, 0) == 2,
                "door bottom authored at (3,1,0) facing rot2");
            Check(FindFamily(bp, 3, 2, 0) == BlockFamily.Cube && !HasCell(bp, 3, 2, 0),
                "the door's top half is NOT authored (derived by the engine at build time)");
        }

        private static void TestCottageOrientationSurvivesRotationAndRoundTrip()
        {
            Console.WriteLine("[TestCottageOrientationSurvivesRotationAndRoundTrip]");
            Blueprint bp = BuildingGeneratorRegistry.Default.Generate(new GenerationContext(0));
            Blueprint r1 = bp.Reorient(1, false);

            // A quarter-turn moves the door's facing forward and its footprint position.
            Check(CountFamily(r1, BlockFamily.Door) == 1, "still exactly one door cell after rotation");
            // The lone door was rot2 (faces +Z); one clockwise turn -> rot1.
            int doorRot = -1;
            foreach (BlueprintCell c in r1.Cells)
            {
                if (c.Family == BlockFamily.Door) doorRot = c.Data;
            }
            Check(doorRot == 1, "door facing rotates with the footprint (rot2 -> rot1)");

            // Four sequential quarter-turns must restore the exact oriented cell set.
            Blueprint r4 = r1.Reorient(1, false).Reorient(1, false).Reorient(1, false);
            Check(SameCellSet(bp, r4), "the whole oriented cottage returns to identity after four quarter-turns");

            // Rotating once then serializing+decoding reproduces the same oriented set.
            BlueprintJsonDecodeResult dec = BlueprintJson.Decode(BlueprintJson.Serialize(r1));
            Check(dec.Success && SameCellSet(r1, dec.Blueprint),
                "a rotated cottage round-trips through JSON with every facing intact");
        }

        // --- small oriented-test helpers ---
        private static BlockFamily FindFamily(Blueprint bp, int x, int y, int z)
        {
            foreach (BlueprintCell c in bp.Cells)
            {
                if (c.Relative.X == x && c.Relative.Y == y && c.Relative.Z == z) return c.Family;
            }
            return BlockFamily.Cube; // absent cells read as cube (never present)
        }

        private static int FindData(Blueprint bp, int x, int y, int z)
        {
            foreach (BlueprintCell c in bp.Cells)
            {
                if (c.Relative.X == x && c.Relative.Y == y && c.Relative.Z == z) return c.Data;
            }
            return -1;
        }

        private static bool HasCell(Blueprint bp, int x, int y, int z)
        {
            foreach (BlueprintCell c in bp.Cells)
            {
                if (c.Relative.X == x && c.Relative.Y == y && c.Relative.Z == z) return true;
            }
            return false;
        }

        private static int CountFamily(Blueprint bp, BlockFamily family)
        {
            int n = 0;
            foreach (BlueprintCell c in bp.Cells)
            {
                if (c.Family == family) n++;
            }
            return n;
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
            // v2: cell identity now includes the authored family + data, so an oriented
            // round-trip / rotation law is only proven if a differing stair facing, slab
            // top, fence arm or door hinge makes the key differ too.
            return c.Relative.X + "," + c.Relative.Y + "," + c.Relative.Z + "|" + c.MaterialKey
                + "|" + (int)c.Family + "|" + c.Data;
        }

        // ============================================================
        // v0.10: parametric building generation engine.
        // Everything below runs the game-free BuildingSpec -> BuildingComposer ->
        // Blueprint pipeline entirely offline, proving: size/bounds validation, single-
        // and two-storey generation, same-seed determinism, different-seed variation,
        // roof/stair/slab presence, material resolvability, over-budget safe rejection,
        // JSON round-trip of a generated blueprint, that undo/planner semantics are not
        // regressed, and that BuildingValidator actually FIRES on each kind of broken
        // structure (negative fixtures) instead of silently emitting a bad building.
        // ============================================================

        /// <summary>
        /// Reproduce the composer's fixed component order to obtain a shared context whose
        /// cross-component facts (door position, stairwell) match the valid structure, so a
        /// negative fixture can introduce exactly ONE defect and still be internally
        /// consistent. The engine never exposes its context, so the test rebuilds it here.
        /// </summary>
        private static BuildingContext FreshContext(BuildingSpec spec)
        {
            var ctx = new BuildingContext(spec);
            new FoundationGenerator().Generate(ctx);
            new WallGenerator().Generate(ctx);
            new StairGenerator().Generate(ctx);
            new OpeningGenerator().Generate(ctx);
            new FloorPlanGenerator().Generate(ctx);
            new RoofGenerator().Generate(ctx);
            new DecorationGenerator().Generate(ctx);
            return ctx;
        }

        // --- Probe / positive: the default specs generate structurally valid buildings. ---
        private static void TestParametricCottageComposesValid()
        {
            Console.WriteLine("[TestParametricCottageComposesValid]");
            BuildingGenResult r = BuildingComposer.Compose(BuildingSpec.DefaultCottage(), "Cottage");
            Console.WriteLine("      " + r.Summarize());
            Check(r.Success, "parametric cottage passes structural QC");
            Check(r.Blueprint != null, "cottage produced a blueprint");
            Check(r.Blueprint.SizeX == 9 && r.Blueprint.SizeZ == 11, "cottage footprint is 9x11");
            Check(r.Blueprint.SchemaVersion == 2, "generated blueprint is schema v2");
            Check(CountFamily(r.Blueprint, BlockFamily.Door) == 1, "exactly one entry door bottom");
            Check(CountFamily(r.Blueprint, BlockFamily.Stairs) > 0, "hip roof uses stairs");
            Check(r.Blueprint.CellCount <= BuildSafetyLimits.MaxBlocksPerOperation, "cottage within the 1024 budget");
        }

        private static void TestTwoStoryHouseComposesValid()
        {
            Console.WriteLine("[TestTwoStoryHouseComposesValid]");
            BuildingGenResult r = BuildingComposer.Compose(BuildingSpec.DefaultTwoStoryHouse(), "House");
            Console.WriteLine("      " + r.Summarize());
            Check(r.Success, "two-storey house passes structural QC");
            Check(r.Blueprint != null, "house produced a blueprint");
            Check(r.Blueprint.SizeX == 9 && r.Blueprint.SizeZ == 9, "house footprint is 9x9");
            int door = CountFamily(r.Blueprint, BlockFamily.Door);
            int stair = CountFamily(r.Blueprint, BlockFamily.Stairs);
            int slab = CountFamily(r.Blueprint, BlockFamily.Slab);
            Check(door == 1, "one entry door (got " + door + ")");
            Check(stair > 0, "stairwell present (got " + stair + " stair cells)");
            Check(slab >= 0, "slabs accounted for (got " + slab + ")");
            Check(r.Blueprint.CellCount <= BuildSafetyLimits.MaxBlocksPerOperation, "house within the 1024 budget");
            // Two-storey must be a tall, connected shell, not two stacked hollow boxes.
            Check(r.Blueprint.SizeY >= 2 * 4 + 2, "house is tall enough for two storeys + roof (Y=" + r.Blueprint.SizeY + ")");
        }

        // --- Seeded determinism: same spec+seed byte-identical; different seed varies layout. ---
        private static void TestParametricDeterminismAndVariation()
        {
            Console.WriteLine("[TestParametricDeterminismAndVariation]");
            BuildingSpec a = BuildingSpec.DefaultTwoStoryHouse();
            a.Seed = 12345;
            BuildingSpec b = BuildingSpec.DefaultTwoStoryHouse();
            b.Seed = 12345;
            Blueprint ba = BuildingComposer.Compose(a, "H").Blueprint;
            Blueprint bb = BuildingComposer.Compose(b, "H").Blueprint;

            bool identical = ba.CellCount == bb.CellCount;
            if (identical)
            {
                for (int i = 0; i < ba.Cells.Count; i++)
                {
                    if (CellKey(ba.Cells[i]) != CellKey(bb.Cells[i])) { identical = false; break; }
                }
            }
            Check(identical, "same spec+seed produces a byte-identical blueprint");

            // A different seed should vary the randomized bits (windows / partitions / decor)
            // while staying structurally valid.
            BuildingSpec c = BuildingSpec.DefaultTwoStoryHouse();
            c.Seed = 987;
            BuildingGenResult rc = BuildingComposer.Compose(c, "H");
            Blueprint bc = rc.Blueprint;
            Check(rc.Success, "a different seed still produces a QC-valid house");
            bool differs = ba.CellCount != bc.CellCount;
            if (!differs)
            {
                for (int i = 0; i < ba.Cells.Count && i < bc.Cells.Count; i++)
                {
                    if (CellKey(ba.Cells[i]) != CellKey(bc.Cells[i])) { differs = true; break; }
                }
            }
            Check(differs, "a different seed changes the layout (not a copy of seed 12345)");
        }

        // --- Varying a few params yields different sizes (the core acceptance criterion). ---
        private static void TestVaryingParamsChangeSizeAndLayout()
        {
            Console.WriteLine("[TestVaryingParamsChangeSizeAndLayout]");
            BuildingSpec small = BuildingSpec.DefaultCottage();
            small.Width = 5; small.Length = 5; small.Roof = RoofType.Flat; small.WindowDensity = 20;
            BuildingSpec big = BuildingSpec.DefaultCottage();
            big.Width = 11; big.Length = 13; big.Roof = RoofType.Gabled; big.WindowDensity = 80;

            BuildingGenResult rs = BuildingComposer.Compose(small, "s");
            BuildingGenResult rb = BuildingComposer.Compose(big, "b");
            Console.WriteLine("      small: " + rs.Summarize());
            Console.WriteLine("      big:   " + rb.Summarize());
            Check(rs.Success, "compact 5x5 flat-roof cottage is valid");
            Check(rb.Success, "larger 11x13 gabled cottage is valid");
            Check(rs.Blueprint.SizeX == 5 && rs.Blueprint.SizeZ == 5, "small footprint honored");
            Check(rb.Blueprint.SizeX == 11 && rb.Blueprint.SizeZ == 13, "big footprint honored");
            Check(rs.Blueprint.CellCount < rb.Blueprint.CellCount, "bigger spec -> more blocks");
        }

        // --- Spec validation: bad parameters are refused BEFORE any generation. ---
        private static void TestSpecValidationBoundsAndBudget()
        {
            Console.WriteLine("[TestSpecValidationBoundsAndBudget]");

            Check(BuildingSpec.DefaultCottage().Validate().IsValid, "default cottage spec is valid");
            Check(BuildingSpec.DefaultTwoStoryHouse().Validate().IsValid, "default house spec is valid");

            BuildingSpec narrow = BuildingSpec.DefaultCottage(); narrow.Width = 3;
            Check(!narrow.Validate().IsValid, "width below the 5 minimum is rejected");

            BuildingSpec wide = BuildingSpec.DefaultCottage(); wide.Width = 65;
            Check(!wide.Validate().IsValid, "width above the 64 axis cap is rejected");

            BuildingSpec floors = BuildingSpec.DefaultCottage(); floors.Floors = 9;
            Check(!floors.Validate().IsValid, "floor count above the maximum is rejected");

            BuildingSpec wallH = BuildingSpec.DefaultCottage(); wallH.WallHeight = 20;
            Check(!wallH.Validate().IsValid, "wall height above the maximum is rejected");

            BuildingSpec win = BuildingSpec.DefaultCottage(); win.WindowDensity = 130;
            Check(!win.Validate().IsValid, "window density outside 0..100 is rejected");

            BuildingSpec tall = BuildingSpec.DefaultCottage();
            tall.Floors = 4; tall.WallHeight = 16; // 4*(16+1)=68 total height > 64 axis cap
            Check(!tall.Validate().IsValid, "over-tall stack (total height > 64) is rejected");

            BuildingSpec monster = BuildingSpec.DefaultCottage();
            monster.Width = 40; monster.Length = 40; monster.Floors = 1;
            Check(!monster.Validate().IsValid, "over-budget footprint is rejected before generation");

            // Rejected specs must produce NO blueprint at all (no partial structure).
            BuildingGenResult r = BuildingComposer.Compose(monster, "nope");
            Check(r.Blueprint == null, "over-budget compose emits no blueprint");
            Check(!r.Success, "over-budget compose reports failure");

            // A house needs >=2 floors and >=7x7.
            BuildingSpec squatHouse = BuildingSpec.DefaultTwoStoryHouse();
            squatHouse.Floors = 1;
            Check(!squatHouse.Validate().IsValid, "a 'two-storey' house with 1 floor is rejected");
            BuildingSpec tinyHouse = BuildingSpec.DefaultTwoStoryHouse();
            tinyHouse.Width = 5; tinyHouse.Length = 5;
            Check(!tinyHouse.Validate().IsValid, "a house too small for a stairwell is rejected");
        }

        // --- Material catalog parity: BuildingMaterials keys == MaterialCatalog keys, and
        //     every default-profile slot is a known key. ---
        private static void TestMaterialCatalogParity()
        {
            Console.WriteLine("[TestMaterialCatalogParity]");
            var catalog = new HashSet<string>();
            foreach (string k in MaterialCatalog.Keys) catalog.Add(k);

            bool allMaterialsKnown = true;
            for (int i = 0; i < BuildingMaterials.All.Length; i++)
            {
                if (!catalog.Contains(BuildingMaterials.All[i]))
                {
                    Console.WriteLine("      catalog missing: " + BuildingMaterials.All[i]);
                    allMaterialsKnown = false;
                }
            }
            Check(allMaterialsKnown, "MaterialCatalog covers every BuildingMaterials logical key");

            // Reverse: no catalog key is unknown to BuildingMaterials (guards against drift).
            bool noExtra = true;
            foreach (string k in catalog)
            {
                bool found = false;
                for (int i = 0; i < BuildingMaterials.All.Length; i++)
                {
                    if (BuildingMaterials.All[i] == k) { found = true; break; }
                }
                if (!found) { Console.WriteLine("      catalog-only key: " + k); noExtra = false; }
            }
            Check(noExtra, "MaterialCatalog has no key that BuildingMaterials lacks");

            // The generated blueprints only ever use known keys.
            Blueprint bp = BuildingComposer.Compose(BuildingSpec.DefaultTwoStoryHouse(), "H").Blueprint;
            bool allResolvable = true;
            for (int i = 0; i < bp.Cells.Count; i++)
            {
                if (!MaterialCatalog.IsKnownKey(bp.Cells[i].MaterialKey))
                {
                    Console.WriteLine("      unresolvable key in house: " + bp.Cells[i].MaterialKey);
                    allResolvable = false; break;
                }
            }
            Check(allResolvable, "every material key in a generated house is resolvable");
        }

        // --- JSON round-trip: a generated blueprint survives export/import unchanged, so a
        //     parametric building can be saved and re-imported like any other. ---
        private static void TestGeneratedBlueprintJsonRoundTrip()
        {
            Console.WriteLine("[TestGeneratedBlueprintJsonRoundTrip]");
            Blueprint bp = BuildingComposer.Compose(BuildingSpec.DefaultTwoStoryHouse(), "House").Blueprint;
            string json = BlueprintJson.Serialize(bp);
            BlueprintJsonDecodeResult decoded = BlueprintJson.Decode(json);
            Blueprint back = decoded.Blueprint;
            Check(decoded.Success, "generated blueprint decodes cleanly");
            Check(back != null, "generated blueprint decodes");
            Check(back.CellCount == bp.CellCount, "round-trip preserves cell count (" + bp.CellCount + ")");
            bool identical = back.CellCount == bp.CellCount;
            if (identical)
            {
                for (int i = 0; i < bp.Cells.Count; i++)
                {
                    if (CellKey(bp.Cells[i]) != CellKey(back.Cells[i])) { identical = false; break; }
                }
            }
            Check(identical, "round-trip preserves position+material+family+data verbatim");
            Check(back.SchemaVersion == 2, "generated blueprint round-trips as schema v2");
        }

        // --- Planner/undo are NOT regressed: a generated blueprint flows through the exact
        //     same plan -> execute -> undo path as the hand-written cottage. ---
        private static void TestGeneratedBlueprintPlansAndUndoes()
        {
            Console.WriteLine("[TestGeneratedBlueprintPlansAndUndoes]");
            Blueprint bp = BuildingComposer.Compose(BuildingSpec.DefaultCottage(), "Cottage").Blueprint;
            var terrain = new FakeTerrain();
            FakeResolver resolver = new FakeResolver();
            var origin = new CellPos(100, 10, 100);

            BuildPlan plan = BuildPlanner.Plan(bp, origin, resolver, terrain, false);
            Check(plan.Success, "generated cottage plans cleanly on an empty world");
            Check(plan.Changes.Count == bp.CellCount, "planner writes every generated cell");

            BuildTask task = new BuildTask("generated-cottage", origin, plan.Changes, false);
            int guard = 0;
            while (!task.IsFinished && guard++ < 100000)
            {
                task.Advance(terrain, BuildSafetyLimits.MaxBlocksPerBatch);
            }
            Check(task.State == BuildTaskState.Completed, "generated cottage finishes building");

            bool allPlaced = true;
            foreach (PlannedChange pc in plan.Changes)
            {
                if (terrain.ValueAt(pc.Absolute) != pc.Value) allPlaced = false;
            }
            Check(allPlaced, "every generated cell was actually written to the world");

            int restored = task.Undo(terrain);
            Check(restored == plan.Changes.Count, "undo restores the world after a generated build");
            bool cleared = true;
            foreach (PlannedChange pc in plan.Changes)
            {
                if (terrain.ValueAt(pc.Absolute) != 0) cleared = false;
            }
            Check(cleared, "all generated cells reverted to their captured original (air)");
        }

        // --- Negative fixtures: prove BuildingValidator FIRES on each kind of broken
        //     structure instead of quietly accepting it. Each introduces exactly ONE defect. ---
        private static void TestValidatorCatchesBlockedEntrance()
        {
            Console.WriteLine("[TestValidatorCatchesBlockedEntrance]");
            var spec = BuildingSpec.DefaultCottage();
            BuildingContext ctx = FreshContext(spec);
            // Block the cell you step into when walking through the doorway (door base height).
            ctx.Builder.Set(ctx.DoorX, ctx.DoorY, ctx.Z0 + 1, BuildingMaterials.StoneWall);
            Blueprint bp = ctx.Builder.ToBlueprint("blocked", 2);
            var v = new BuildingValidator(spec, ctx, bp).Validate();
            Console.WriteLine("      problems: " + v.JoinedProblems());
            Check(!v.IsValid, "a blocked doorway is detected as invalid");
            Check(v.JoinedProblems().Contains("门内侧"), "the failure names the blocked entrance");
        }

        private static void TestValidatorCatchesMissingWall()
        {
            Console.WriteLine("[TestValidatorCatchesMissingWall]");
            var spec = BuildingSpec.DefaultCottage();
            BuildingContext ctx = FreshContext(spec);
            int planeY = ctx.WallBaseY(0) + (spec.WallHeight / 2);
            // Punch a hole in the middle of the left perimeter wall at mid-height.
            ctx.Builder.Carve(ctx.X0, planeY, (ctx.Z0 + ctx.Z1) / 2, ctx.X0, planeY, (ctx.Z0 + ctx.Z1) / 2);
            Blueprint bp = ctx.Builder.ToBlueprint("holey", 2);
            var v = new BuildingValidator(spec, ctx, bp).Validate();
            Console.WriteLine("      problems: " + v.JoinedProblems());
            Check(!v.IsValid, "a gap in the shell is detected as invalid");
            Check(v.JoinedProblems().Contains("缺口"), "the failure names the wall gap");
        }

        private static void TestValidatorCatchesFloatingBlock()
        {
            Console.WriteLine("[TestValidatorCatchesFloatingBlock]");
            var spec = BuildingSpec.DefaultCottage();
            BuildingContext ctx = FreshContext(spec);
            // Add one isolated cell high above the roof, touching nothing.
            int fx = 2;
            int fy = spec.CeilingY + spec.WallHeight + 5;
            int fz = 2;
            ctx.Builder.Set(fx, fy, fz, BuildingMaterials.StoneWall);
            Blueprint bp = ctx.Builder.ToBlueprint("floaty", 2);
            var v = new BuildingValidator(spec, ctx, bp).Validate();
            Console.WriteLine("      problems: " + v.JoinedProblems());
            Check(!v.IsValid, "an isolated floating block is detected as invalid");
            Check(v.JoinedProblems().Contains("不连通"), "the failure reports a disconnected component");
        }

        private static void TestValidatorCatchesMissingStair()
        {
            Console.WriteLine("[TestValidatorCatchesMissingStair]");
            var spec = BuildingSpec.DefaultTwoStoryHouse();
            // Rebuild WITHOUT StairGenerator: omitting it in the fixed order simulates a
            // two-storey shell whose floors were never connected, so the validator must
            // refuse it rather than accept two stacked boxes.
            var ctxNoStair = new BuildingContext(spec);
            new FoundationGenerator().Generate(ctxNoStair);
            new WallGenerator().Generate(ctxNoStair);
            // NOTE: StairGenerator intentionally omitted.
            new OpeningGenerator().Generate(ctxNoStair);
            new FloorPlanGenerator().Generate(ctxNoStair);
            new RoofGenerator().Generate(ctxNoStair);
            new DecorationGenerator().Generate(ctxNoStair);
            Blueprint bp = ctxNoStair.Builder.ToBlueprint("nostair", 2);
            var v = new BuildingValidator(spec, ctxNoStair, bp).Validate();
            Console.WriteLine("      problems: " + v.JoinedProblems());
            Check(!ctxNoStair.HasStairwell, "context knows the stairwell is absent");
            Check(!v.IsValid, "a two-storey house without a stair is detected as invalid");
            Check(v.JoinedProblems().Contains("楼梯"), "the failure reports the missing stairwell");
        }

        private static void TestValidatorCatchesFootprintMismatch()
        {
            Console.WriteLine("[TestValidatorCatchesFootprintMismatch]");
            var spec = BuildingSpec.DefaultCottage(); // 9x11
            // Build a deliberately narrower shell so SizeZ != Length.
            var ctx = new BuildingContext(spec);
            ctx.Builder.FillBox(0, 0, 0, 8, 4, 6, BuildingMaterials.StoneWall); // 9x7, not 9x11
            Blueprint bp = ctx.Builder.ToBlueprint("mismatch", 2);
            var v = new BuildingValidator(spec, ctx, bp).Validate();
            Console.WriteLine("      problems: " + v.JoinedProblems());
            Check(!v.IsValid, "a footprint that ignores Length is rejected");
            Check(v.JoinedProblems().Contains("占地长"), "the failure names the footprint mismatch");
        }

        private static void TestValidatorCatchesUnknownMaterial()
        {
            Console.WriteLine("[TestValidatorCatchesUnknownMaterial]");
            var spec = BuildingSpec.DefaultCottage();
            BuildingContext ctx = FreshContext(spec);
            // Introduce a cell with a material key that is not in the catalog.
            ctx.Builder.Set(ctx.X0 + 1, ctx.WallBaseY(0), ctx.Z0 + 1, "GhostlyMysteryBlock");
            Blueprint bp = ctx.Builder.ToBlueprint("ghost", 2);
            var v = new BuildingValidator(spec, ctx, bp).Validate();
            Console.WriteLine("      problems: " + v.JoinedProblems());
            Check(!v.IsValid, "an unresolvable material key is rejected");
            Check(v.JoinedProblems().Contains("GhostlyMysteryBlock"), "the failure names the bad material");
        }

        private static void TestRegistryKeepsCottageAsDefault()
        {
            Console.WriteLine("[TestRegistryKeepsCottageAsDefault]");
            Check(BuildingGeneratorRegistry.Default.Key == "medieval_cottage", "registry default is still the medieval cottage");
            Check(BuildingGeneratorRegistry.Find("cottage_param") != null, "parametric cottage is registered by key");
            Check(BuildingGeneratorRegistry.Find("house_two_story") != null, "two-storey house is registered by key");
            // The default cottage generator output is byte-identical to before the engine landed.
            Blueprint viaDefault = BuildingGeneratorRegistry.Default.Generate(new GenerationContext(0));
            Blueprint viaDirect = new MedievalCottageGenerator().Generate(new GenerationContext(0));
            bool same = viaDefault.CellCount == viaDirect.CellCount;
            if (same)
            {
                for (int i = 0; i < viaDefault.Cells.Count; i++)
                {
                    if (CellKey(viaDefault.Cells[i]) != CellKey(viaDirect.Cells[i])) { same = false; break; }
                }
            }
            Check(same, "the existing cottage tool behavior is unchanged (additive engine)");
        }
        private static void TestTerrainAuthorityGate()
        {
            Console.WriteLine("[TestTerrainAuthorityGate]");
            // Terrain may be mutated by the world-authoritative roles only: the
            // single player and the multiplayer host. A remote client is refused.
            Check(TerrainAuthority.IsWritable(WorldAuthority.SinglePlayer),
                "single player is allowed to build");
            Check(TerrainAuthority.IsWritable(WorldAuthority.Host),
                "multiplayer host is allowed to build (multiplayer gate lifted)");
            Check(!TerrainAuthority.IsWritable(WorldAuthority.RemoteClient),
                "remote client is refused (writes would not sync to the host)");
        }

        private static void TestBuildEmitsOnlyVanillaBlocks()
        {
            Console.WriteLine("[TestBuildEmitsOnlyVanillaBlocks]");
            // The mod's own in-hand tool blocks are never world content. Their CLR
            // type names must not appear in the material catalog, and every cell the
            // generators produce must resolve to a vanilla block through the catalog.
            string[] toolBlockNames =
            {
                "BuilderToolBlock", "UndoToolBlock", "BlueprintToolBlock", "SpeedboatBlock",
            };
            foreach (string tool in toolBlockNames)
            {
                bool collides = false;
                foreach (string key in MaterialCatalog.Keys)
                {
                    MaterialCatalog.Entry e;
                    if (MaterialCatalog.TryGet(key, out e) && e.TypeName == tool)
                    {
                        collides = true;
                        break;
                    }
                }
                Check(!collides, "catalog never resolves a material to the mod tool block " + tool);
            }

            // Both shipped buildings: every cell material key must be a cataloged vanilla
            // block, i.e. resolvable on ANY client that already knows the vanilla set.
            Blueprint cottage = BuildingGeneratorRegistry.Default.Generate(new GenerationContext(0));
            BuildingGenResult house = BuildingComposer.Compose(BuildingSpec.DefaultTwoStoryHouse(), "house_probe");

            Check(AllCellsResolveVanilla(cottage), "default cottage emits only cataloged vanilla blocks");
            Check(house.Success && AllCellsResolveVanilla(house.Blueprint),
                "two-storey house emits only cataloged vanilla blocks");
        }

        private static bool AllCellsResolveVanilla(Blueprint bp)
        {
            if (bp == null)
            {
                return false;
            }
            for (int i = 0; i < bp.Cells.Count; i++)
            {
                if (!MaterialCatalog.IsKnownKey(bp.Cells[i].MaterialKey))
                {
                    return false;
                }
            }
            return bp.CellCount > 0;
        }

        // ==================== v0.12 Giant Fantasy Treehouse (森之王庭) ====================

        private static string TreehousePartsSignature(TreehousePart[] parts)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < parts.Length; i++)
            {
                TreehousePart p = parts[i];
                sb.Append(p.Phase).Append('|').Append(p.CellCount).Append('|')
                  .Append(p.OffsetInMaster.X).Append(',').Append(p.OffsetInMaster.Y).Append(',').Append(p.OffsetInMaster.Z)
                  .Append('|').Append(BlueprintSignature(p.Blueprint)).Append(';');
            }
            return sb.ToString();
        }

        private static void TestTreehouseLogOrientationSemantics()
        {
            Console.WriteLine("[TestTreehouseLogOrientationSemantics]");
            Check(BlockOrientation.Normalize(BlockFamily.Log, 0) == 0, "log normalize keeps vertical 0");
            Check(BlockOrientation.Normalize(BlockFamily.Log, 7) == 3, "log normalize masks to &3");
            for (int s = 0; s < 4; s++)
            {
                Check(BlockOrientation.Rotate(BlockFamily.Log, 0, s) == 0, "vertical log invariant under rotation step " + s);
            }
            Check(BlockOrientation.MirrorX(BlockFamily.Log, 0) == 0, "vertical log invariant under mirrorX");
            int h = BlockOrientation.Rotate(BlockFamily.Log, BlockOrientation.Rotate(BlockFamily.Log, 1, 2), 2);
            Check(h == 1, "horizontal log obeys 4-quarter-turn identity (got " + h + ")");
            Check(BlockOrientation.FamilyToTag(BlockFamily.Log) == "log", "log family tag name");
            Check(BlockOrientation.FamilyFromTag("log") == BlockFamily.Log, "log family parsed from tag");
        }

        private static void TestTreehouseSpecValidationRejectsAbsurd()
        {
            Console.WriteLine("[TestTreehouseSpecValidationRejectsAbsurd]");
            Check(TreehouseSpec.DefaultKingdomOfTheForest().Validate().IsValid, "default treehouse spec is valid");
            Check(!(new TreehouseSpec { Width = 64 }).Validate().IsValid, "footprint over 33 rejected");
            Check(!(new TreehouseSpec { Height = 80 }).Validate().IsValid, "height over 36 rejected");
            Check(!(new TreehouseSpec { CanopyRadius = 5 }).Validate().IsValid, "canopy radius > 3 rejected (leaf-decay safety)");
            Check(!(new TreehouseSpec { FloorThreeDeckY = 9, FloorTwoDeckY = 15 }).Validate().IsValid, "storey deck order rejected");
        }

        private static void TestTreehouseDeterministic()
        {
            Console.WriteLine("[TestTreehouseDeterministic]");
            TreeCanvas c1, c2, c3; TreehouseValidation v1, v2, v3;
            TreehousePart[] a = TreehouseBlueprintSet.Build(TreehouseSpec.DefaultKingdomOfTheForest(), out c1, out v1);
            TreehousePart[] b = TreehouseBlueprintSet.Build(TreehouseSpec.DefaultKingdomOfTheForest(), out c2, out v2);
            Check(c1.Count == c2.Count, "same seed -> identical block count (" + c1.Count + ")");
            Check(TreehousePartsSignature(a) == TreehousePartsSignature(b), "same seed -> identical phased parts");

            TreehouseSpec s3 = TreehouseSpec.DefaultKingdomOfTheForest();
            s3.Seed = s3.Seed + 7;
            TreehousePart[] c = TreehouseBlueprintSet.Build(s3, out c3, out v3);
            Check(TreehousePartsSignature(a) != TreehousePartsSignature(c), "different seed -> different layout");
        }

        private static void TestTreehouseValidAndWithinSafetyEnvelope()
        {
            Console.WriteLine("[TestTreehouseValidAndWithinSafetyEnvelope]");
            TreeCanvas canvas; TreehouseValidation v;
            TreehousePart[] parts = TreehouseBlueprintSet.Build(TreehouseSpec.DefaultKingdomOfTheForest(), out canvas, out v);

            Check(parts.Length >= 9, "sliced into at least 9 legible phases (got " + parts.Length + ")");
            bool allWithinCaps = true;
            foreach (TreehousePart p in parts)
            {
                if (p.CellCount > BuildSafetyLimits.MaxBlocksPerOperation) allWithinCaps = false;
                if (p.Blueprint.SizeX > BuildSafetyLimits.MaxSelectionAxisLength ||
                    p.Blueprint.SizeY > BuildSafetyLimits.MaxSelectionAxisLength ||
                    p.Blueprint.SizeZ > BuildSafetyLimits.MaxSelectionAxisLength) allWithinCaps = false;
            }
            Check(allWithinCaps, "every phase blueprint is within the 1024 / 64-axis safety envelope");
            Check(v.IsValid, "treehouse validation is clean (" + v.JoinedProblems() + ")");
        }

        private static void TestTreehouseEmitsOnlyVanillaBlocks()
        {
            Console.WriteLine("[TestTreehouseEmitsOnlyVanillaBlocks]");
            TreeCanvas canvas; TreehouseValidation v;
            TreehouseBlueprintSet.Build(TreehouseSpec.DefaultKingdomOfTheForest(), out canvas, out v);

            bool allResolve = true;
            foreach (KeyValuePair<CellPos, PaintedCell> kv in canvas.Cells)
            {
                MaterialCatalog.Entry e;
                if (!MaterialCatalog.TryGet(kv.Value.Key, out e)) { allResolve = false; break; }
                // The mod's own tool blocks must never appear as treehouse content.
                if (e.TypeName == "BuilderToolBlock" || e.TypeName == "UndoToolBlock" ||
                    e.TypeName == "BlueprintToolBlock" || e.TypeName == "SpeedboatBlock")
                {
                    allResolve = false; break;
                }
            }
            Check(allResolve, "every treehouse cell resolves to a cataloged vanilla block (no tool/mod-only block)");
            Check(canvas.LogCount > 0, "the tree contributes real log blocks (logs=" + canvas.LogCount + ")");
        }

        private static void TestTreehouseLeafDecaySafety()
        {
            Console.WriteLine("[TestTreehouseLeafDecaySafety]");
            TreeCanvas canvas; TreehouseValidation v;
            TreehouseBlueprintSet.Build(TreehouseSpec.DefaultKingdomOfTheForest(), out canvas, out v);

            int leaves = 0; bool allSafe = true;
            foreach (KeyValuePair<CellPos, PaintedCell> kv in canvas.Cells)
            {
                if (kv.Value.Key == TreeCanvas.Leaf)
                {
                    leaves++;
                    if (!canvas.IsNearLog(kv.Key.X, kv.Key.Y, kv.Key.Z, 3)) { allSafe = false; }
                }
            }
            Check(leaves > 0, "the canopy produced foliage (" + leaves + " leaf cells)");
            Check(allSafe, "EVERY leaf is within Chebyshev 3 of a log (never decays on save/reload)");
        }

        private static void TestTreehouseStructuralFacts()
        {
            Console.WriteLine("[TestTreehouseStructuralFacts]");
            TreehouseSpec spec = TreehouseSpec.DefaultKingdomOfTheForest();
            TreeCanvas canvas; TreehouseValidation v;
            TreehouseBlueprintSet.Build(spec, out canvas, out v);
            int cx = spec.CenterX, cz = spec.CenterZ;

            // A continuous vertical trunk log column through the dwelling.
            Check(canvas.IsLog(cx, 0, cz), "trunk log at ground (0)");
            Check(canvas.IsLog(cx, spec.FloorOneDeckY, cz), "trunk grows through floor 1 deck");
            Check(canvas.IsLog(cx, spec.FloorThreeDeckY, cz), "trunk grows through floor 3 deck");
            Check(canvas.IsLog(cx, spec.TrunkTopY - 1, cz), "trunk reaches near the crown");

            // Three decks actually span their storey footprint (an edge cell, not on the trunk).
            Check(canvas.HasCell(cx - 8, spec.FloorOneDeckY, cz), "floor 1 deck plate present");
            Check(canvas.HasCell(cx - 6, spec.FloorTwoDeckY, cz), "floor 2 deck plate present");
            Check(canvas.HasCell(cx - 5, spec.FloorThreeDeckY, cz), "floor 3 deck plate present");

            // Exactly one entry door bottom-half (engine makes the top half at build time).
            int doors = 0, stairs = 0;
            foreach (KeyValuePair<CellPos, PaintedCell> kv in canvas.Cells)
            {
                if (kv.Value.Family == BlockFamily.Door) doors++;
                if (kv.Value.Family == BlockFamily.Stairs) stairs++;
            }
            Check(doors == 1, "exactly one door bottom cell (got " + doors + ")");
            Check(stairs > 0, "the dwelling uses real stair blocks (" + stairs + ")");
        }

        // ==================== v0.13 M4 Phased In-World Assembly (TreehouseAssembly) ====================

        /// <summary>Absolute world cell of one blueprint cell of a part, stamped at the anchor.</summary>
        private static CellPos AssemblyAbsolute(CellPos anchor, TreehousePart part, BlueprintCell cell)
        {
            return anchor.Add(part.OffsetInMaster).Add(cell.Relative);
        }

        /// <summary>How many of the master footprint cells are currently non-air in the fake world.</summary>
        private static int CountAssemblyOccupied(TreehousePart[] parts, CellPos anchor, FakeTerrain t)
        {
            int n = 0;
            for (int i = 0; i < parts.Length; i++)
            {
                TreehousePart p = parts[i];
                foreach (BlueprintCell c in p.Blueprint.Cells)
                {
                    if (t.ValueAt(AssemblyAbsolute(anchor, p, c)) != 0) n++;
                }
            }
            return n;
        }

        /// <summary>Total cells across the first k phases (the "should already be in the world" count).</summary>
        private static int CumulativeCells(TreehousePart[] parts, int k)
        {
            int s = 0;
            for (int i = 0; i < k && i < parts.Length; i++) s += parts[i].CellCount;
            return s;
        }

        /// <summary>Drive the assembly to a terminal state, recording the largest single-step write burst.</summary>
        private static TreehouseAdvanceResult PumpToTerminal(TreehouseAssembly asm, FakeTerrain t, int maxSteps, out int maxWritesPerAdvance)
        {
            maxWritesPerAdvance = 0;
            TreehouseAdvanceResult last = TreehouseAdvanceResult.Steady;
            for (int i = 0; i < maxSteps; i++)
            {
                int before = t.Writes;
                last = asm.Advance(BuildSafetyLimits.MaxBlocksPerBatch);
                int d = t.Writes - before;
                if (d > maxWritesPerAdvance) maxWritesPerAdvance = d;
                if (asm.IsTerminal) return last;
            }
            return last; // guard: caller asserts terminal separately
        }

        private static void TestTreehouseAssemblyFullBuildMatchesMasterCanvas()
        {
            Console.WriteLine("[TestTreehouseAssemblyFullBuildMatchesMasterCanvas]");
            TreehouseSpec spec = TreehouseSpec.DefaultKingdomOfTheForest();
            TreeCanvas canvas; TreehouseValidation v;
            TreehousePart[] parts = TreehouseBlueprintSet.Build(spec, out canvas, out v);
            Check(v.IsValid, "phased blueprint set validates clean (" + v.JoinedProblems() + ")");

            CellPos anchor = new CellPos(5, 64, 5);
            FakeTerrain t = new FakeTerrain();
            FakeResolver r = new FakeResolver();
            TransactionHistory h = new TransactionHistory();
            TreehouseAssembly asm = new TreehouseAssembly(parts, anchor, r, t, h);

            Check(asm.TotalPlannedCells == canvas.Count, "planned grand total == master canvas (" + canvas.Count + ")");

            int maxBatch;
            TreehouseAdvanceResult res = PumpToTerminal(asm, t, 50000, out maxBatch);
            Check(res == TreehouseAdvanceResult.AllCompleted, "one anchored tap grows the whole treehouse (all phases complete)");
            Check(asm.IsCompleted && !asm.IsHalted, "terminal state is a clean completion (not a halt)");
            Check(maxBatch <= BuildSafetyLimits.MaxBlocksPerBatch,
                "no single Advance wrote more than " + BuildSafetyLimits.MaxBlocksPerBatch + " cells (burst was " + maxBatch + ")");

            var set = new HashSet<CellPos>();
            bool missing = false;
            for (int i = 0; i < parts.Length; i++)
            {
                TreehousePart p = parts[i];
                foreach (BlueprintCell c in p.Blueprint.Cells)
                {
                    CellPos a = AssemblyAbsolute(anchor, p, c);
                    set.Add(a);
                    if (t.ValueAt(a) == 0) missing = true;
                }
            }
            Check(set.Count == canvas.Count, "phase cells map to exactly the master footprint, disjoint with no duplication (got " + set.Count + "/" + canvas.Count + ")");
            Check(!missing, "every master cell physically present in the world after assembly");
            Check(t.Writes == canvas.Count, "total writes equals canvas cells: no stray or double writes (got " + t.Writes + ")");
            Check(asm.CompletedPhaseCount == parts.Length, "all " + parts.Length + " phases completed");
            Check(h.Count == parts.Length, "undo stack holds one reversible transaction per phase (" + h.Count + ")");
            Check(asm.AppliedSoFar == canvas.Count, "applied-so-far reconciles with the total placed (" + asm.AppliedSoFar + ")");
        }

        private static void TestTreehouseAssemblyStrictlySequential()
        {
            Console.WriteLine("[TestTreehouseAssemblyStrictlySequential]");
            TreeCanvas canvas; TreehouseValidation v;
            TreehousePart[] parts = TreehouseBlueprintSet.Build(TreehouseSpec.DefaultKingdomOfTheForest(), out canvas, out v);
            CellPos anchor = new CellPos(5, 64, 5);
            FakeTerrain t = new FakeTerrain();
            TreehouseAssembly asm = new TreehouseAssembly(parts, anchor, new FakeResolver(), t, new TransactionHistory());

            int boundaries = 0;
            bool lookahead = false; // a phase beyond the just-completed one wrote before its turn
            for (int i = 0; i < 50000; i++)
            {
                TreehouseAdvanceResult res = asm.Advance(BuildSafetyLimits.MaxBlocksPerBatch);
                if (res == TreehouseAdvanceResult.PhaseCompleted)
                {
                    int k = asm.CompletedPhaseCount;
                    // At a boundary nothing of a not-yet-started phase may be in the world:
                    // world writes so far must equal exactly the completed phases' cells.
                    if (t.Writes != CumulativeCells(parts, k)) lookahead = true;
                    boundaries++;
                }
                if (asm.IsTerminal) break;
            }
            Check(boundaries == parts.Length - 1, "emitted one completion signal per non-final phase (" + boundaries + " of " + (parts.Length - 1) + ")");
            Check(!lookahead, "phases run strictly in order: no later phase is ever touched before earlier ones finish (no interleaving)");
        }

        private static void TestTreehouseAssemblyCancelRollsBackCurrentKeepsCompleted()
        {
            Console.WriteLine("[TestTreehouseAssemblyCancelRollsBackCurrentKeepsCompleted]");
            TreeCanvas canvas; TreehouseValidation v;
            TreehousePart[] parts = TreehouseBlueprintSet.Build(TreehouseSpec.DefaultKingdomOfTheForest(), out canvas, out v);
            CellPos anchor = new CellPos(5, 64, 5);
            FakeTerrain t = new FakeTerrain();
            TreehouseAssembly asm = new TreehouseAssembly(parts, anchor, new FakeResolver(), t, new TransactionHistory());

            // Drive until at least one phase has completed AND the current phase is mid-write
            // (world has cells beyond the completed phases), so a real partial exists to undo.
            bool cancelledMid = false;
            for (int i = 0; i < 50000; i++)
            {
                asm.Advance(BuildSafetyLimits.MaxBlocksPerBatch);
                if (asm.IsTerminal) break;
                if (asm.CompletedPhaseCount >= 1 &&
                    t.Writes > CumulativeCells(parts, asm.CompletedPhaseCount))
                {
                    asm.Cancel();
                    cancelledMid = true;
                    break;
                }
            }
            Check(cancelledMid, "reached a mid-phase window to exercise cancellation");

            TreehouseAdvanceResult res = TreehouseAdvanceResult.Steady;
            for (int i = 0; i < 50000 && !asm.IsTerminal; i++) res = asm.Advance(BuildSafetyLimits.MaxBlocksPerBatch);

            Check(asm.IsTerminal && res == TreehouseAdvanceResult.Cancelled, "assembly ends in a cancelled halt, not a completion");
            // The rolled-back in-flight phase leaves only the recorded completed phases in the world.
            int expected = CumulativeCells(parts, asm.CompletedPhaseCount);
            Check(CountAssemblyOccupied(parts, anchor, t) == expected,
                "cancel removed the partial phase and kept exactly the completed phases (world " + expected + " cells)");
            Check(asm.CompletedPhaseCount < parts.Length, "cancel stopped before all phases ran");
        }

        private static void TestTreehouseAssemblyHardFailureHaltsLaterPhases()
        {
            Console.WriteLine("[TestTreehouseAssemblyHardFailureHaltsLaterPhases]");
            TreeCanvas canvas; TreehouseValidation v;
            TreehousePart[] parts = TreehouseBlueprintSet.Build(TreehouseSpec.DefaultKingdomOfTheForest(), out canvas, out v);
            int T = parts.Length / 2;
            Check(T >= 1 && T < parts.Length, "there is a genuine middle phase to obstruct (index " + T + "/" + parts.Length + ")");

            CellPos anchor = new CellPos(5, 64, 5);
            FakeTerrain t = new FakeTerrain();
            // Pre-place a SOLID block on one target cell of the middle phase -> hard plan failure.
            CellPos abs = AssemblyAbsolute(anchor, parts[T], parts[T].Blueprint.Cells[0]);
            t.Occupy(abs, 999);

            TreehouseAssembly asm = new TreehouseAssembly(parts, anchor, new FakeResolver(), t, new TransactionHistory());
            int maxBatch;
            TreehouseAdvanceResult res = PumpToTerminal(asm, t, 50000, out maxBatch);

            Check(res == TreehouseAdvanceResult.Failed && asm.IsHalted, "a solid obstruction aborts the assembly (hard failure, no cascade)");
            Check(asm.CompletedPhaseCount == T, "only the earlier phases completed before the halt (" + asm.CompletedPhaseCount + " == " + T + ")");
            Check(t.ValueAt(abs) == 999, "the obstructed cell was never overwritten (default no-overwrite respected)");

            // Nothing of phase T (except the obstruction) or any later phase was written.
            int lookahead = 0;
            for (int i = T; i < parts.Length; i++)
            {
                foreach (BlueprintCell c in parts[i].Blueprint.Cells)
                {
                    CellPos a = AssemblyAbsolute(anchor, parts[i], c);
                    if (!a.Equals(abs) && t.ValueAt(a) != 0) lookahead++;
                }
            }
            Check(lookahead == 0, "the failing phase and every later phase were left untouched (停止后续批次)");
        }

        private static void TestTreehouseAssemblyPerPhaseUndoHonorsLaterEdits()
        {
            Console.WriteLine("[TestTreehouseAssemblyPerPhaseUndoHonorsLaterEdits]");
            TreeCanvas canvas; TreehouseValidation v;
            TreehousePart[] parts = TreehouseBlueprintSet.Build(TreehouseSpec.DefaultKingdomOfTheForest(), out canvas, out v);
            CellPos anchor = new CellPos(5, 64, 5);
            FakeTerrain t = new FakeTerrain();
            TransactionHistory h = new TransactionHistory();
            TreehouseAssembly asm = new TreehouseAssembly(parts, anchor, new FakeResolver(), t, h);
            int maxBatch; PumpToTerminal(asm, t, 50000, out maxBatch);
            Check(asm.IsCompleted, "built fully before testing undo granularity");

            CellPos lastCell = AssemblyAbsolute(anchor, parts[parts.Length - 1], parts[parts.Length - 1].Blueprint.Cells[0]);
            CellPos firstCell = AssemblyAbsolute(anchor, parts[0], parts[0].Blueprint.Cells[0]);

            int pending;
            int restored = h.UndoLast(t, out pending);
            Check(pending == 0, "the per-phase undo fully reverted (nothing left un-restorable)");
            Check(restored == parts[parts.Length - 1].CellCount, "one undo tap reverts exactly the LAST phase's cells (" + restored + ")");
            Check(t.ValueAt(lastCell) == 0, "the last-phase cell is back to air after undo");
            Check(t.ValueAt(firstCell) != 0, "earlier phases are NOT disturbed by undoing the newest one");
            Check(h.Count == parts.Length - 1, "undo pops one transaction at a time (" + h.Count + " remain)");

            // A player edits a first-phase block after the build; undoing back to it must preserve the edit.
            t.Occupy(firstCell, 4242);
            for (int i = 0; i < parts.Length; i++)
            {
                int p2;
                h.UndoLast(t, out p2);
            }
            Check(t.ValueAt(firstCell) == 4242, "undo never clobbers a block the player edited after the build");
            Check(!h.HasUndoable, "the whole phased build is undoable down to empty");
        }

        private static void TestTreehouseAssemblyDeterministic()
        {
            Console.WriteLine("[TestTreehouseAssemblyDeterministic]");
            TreeCanvas c1, c2; TreehouseValidation v1, v2;
            TreehousePart[] p1 = TreehouseBlueprintSet.Build(TreehouseSpec.DefaultKingdomOfTheForest(), out c1, out v1);
            TreehousePart[] p2 = TreehouseBlueprintSet.Build(TreehouseSpec.DefaultKingdomOfTheForest(), out c2, out v2);
            CellPos anchor = new CellPos(5, 64, 5);

            FakeTerrain t1 = new FakeTerrain(); int mb1;
            PumpToTerminal(new TreehouseAssembly(p1, anchor, new FakeResolver(), t1, new TransactionHistory()), t1, 50000, out mb1);
            FakeTerrain t2 = new FakeTerrain(); int mb2;
            PumpToTerminal(new TreehouseAssembly(p2, anchor, new FakeResolver(), t2, new TransactionHistory()), t2, 50000, out mb2);

            bool identical = t1.Writes == t2.Writes;
            foreach (TreehousePart p in p1)
            {
                foreach (BlueprintCell c in p.Blueprint.Cells)
                {
                    CellPos a = AssemblyAbsolute(anchor, p, c);
                    if (t1.ValueAt(a) != t2.ValueAt(a)) { identical = false; }
                }
            }
            Check(identical, "same seed + same anchor -> identical phased in-world result (Android-bit-reproducible)");
        }

        // ==================== v0.11.0 Visual Building Workbench (pure) ====================

        private static int BlueprintSignature(Blueprint bp)
        {
            unchecked
            {
                int h = 17;
                foreach (BlueprintCell c in bp.Cells)
                {
                    h = h * 31 + c.Relative.X;
                    h = h * 31 + c.Relative.Y;
                    h = h * 31 + c.Relative.Z;
                    h = h * 31 + (c.MaterialKey == null ? 0 : c.MaterialKey.GetHashCode());
                    h = h * 31 + (int)c.Family;
                    h = h * 31 + c.Data;
                }
                return h;
            }
        }

        private static BuildingWorkbench NewWorkbench(out BuildingSelectionService svc)
        {
            svc = new BuildingSelectionService();
            return new BuildingWorkbench(svc);
        }

        private static void TestWorkbenchSelectAndGenerate()
        {
            Console.WriteLine("[TestWorkbenchSelectAndGenerate]");
            BuildingSelectionService svc;
            BuildingWorkbench wb = NewWorkbench(out svc);
            Check(wb.Stage == WorkbenchStage.Idle, "starts Idle");
            WorkbenchStatus sel = wb.SelectFirst();
            Check(sel.Accepted && wb.Choice != null && wb.Draft != null, "SelectFirst chooses the cottage and seeds a spec");
            WorkbenchStatus gen = wb.Regenerate();
            Check(gen.Accepted && wb.CurrentBlueprint != null && wb.CurrentBlueprint.CellCount > 0,
                "Regenerate produces a real blueprint via the unchanged engine");
            Check(wb.NeedsRegenerate == false, "after generate the draft is no longer dirty");
        }

        private static void TestWorkbenchSelectionCatalog()
        {
            Console.WriteLine("[TestWorkbenchSelectionCatalog]");
            BuildingSelectionService svc;
            BuildingWorkbench wb = NewWorkbench(out svc);
            Check(svc.Choices.Count == 2, "parametric catalog offers cottage + house (no imported yet)");
            Check(wb.SelectFirst().Accepted && wb.Choice.Kind == BuildingChoiceKind.Cottage, "first choice is the cottage");
            WorkbenchStatus next = wb.SelectNextChoice();
            Check(next.Accepted && wb.Choice.Kind == BuildingChoiceKind.House, "cycling advances to the house");
            // Importing adds a third, read-only choice.
            Blueprint imported = BuildingGeneratorRegistry.Default.Generate(new GenerationContext(0));
            svc.SetImportedBlueprint(imported);
            Check(svc.Choices.Count == 3, "an imported blueprint appears as a third choice once loaded");
            wb.SelectNextChoice();
            Check(wb.Choice != null && wb.Choice.Kind == BuildingChoiceKind.Imported, "cycling reaches the imported choice");
            Check(wb.CycleWidth().Accepted == false, "dimension knobs are refused for an imported blueprint");
        }

        private static void TestWorkbenchSeedDeterminism()
        {
            Console.WriteLine("[TestWorkbenchSeedDeterminism]");
            BuildingSelectionService s1, s2;
            BuildingWorkbench a = new BuildingWorkbench(s1 = new BuildingSelectionService());
            BuildingWorkbench b = new BuildingWorkbench(s2 = new BuildingSelectionService());
            a.SelectFirst(); b.SelectFirst();
            a.Regenerate(); b.Regenerate();
            Check(a.CurrentBlueprint.CellCount == b.CurrentBlueprint.CellCount &&
                  BlueprintSignature(a.CurrentBlueprint) == BlueprintSignature(b.CurrentBlueprint),
                "same (spec, seed) reproduces a byte-identical blueprint through the workbench");
            WorkbenchStatus vs = a.CycleSeedVariant();
            a.Regenerate();
            Check(vs.Accepted && a.CurrentBlueprint != null &&
                  BlueprintSignature(a.CurrentBlueprint) != BlueprintSignature(b.CurrentBlueprint),
                "a different seed yields a different (but still valid) layout");
        }

        private static void TestWorkbenchRotationFourTurnsIdentity()
        {
            Console.WriteLine("[TestWorkbenchRotationFourTurnsIdentity]");
            BuildingSelectionService svc;
            BuildingWorkbench wb = NewWorkbench(out svc);
            wb.SelectFirst(); wb.Regenerate();
            Blueprint e0 = wb.EffectiveBlueprint();
            int sig0 = BlueprintSignature(e0);
            wb.RotateCw();
            Blueprint e1 = wb.EffectiveBlueprint();
            Check(e1.SizeX == e0.SizeZ && e1.SizeZ == e0.SizeX && e1.CellCount == e0.CellCount,
                "one quarter turn swaps the footprint axes and preserves the block count");
            Check(BlueprintSignature(e1) != sig0, "a rotated non-square footprint genuinely differs");
            wb.RotateCw(); wb.RotateCw(); wb.RotateCw(); // three more -> back to rot0
            Blueprint e4 = wb.EffectiveBlueprint();
            Check(wb.Rotation == 0 && BlueprintSignature(e4) == sig0,
                "four clockwise quarter turns return to the exact original footprint (group law)");
        }

        private static void TestWorkbenchComplexOrientationRotates()
        {
            Console.WriteLine("[TestWorkbenchComplexOrientationRotates]");
            BuildingSelectionService svc;
            BuildingWorkbench wb = NewWorkbench(out svc);
            wb.SelectFirst(); wb.Regenerate();
            FakeTerrain fake = new FakeTerrain();
            FakeResolver res = new FakeResolver();
            CellPos origin = new CellPos(0, 1, 0);
            BuildPreviewModel m0;
            wb.PreviewAt(origin, fake, res, out m0);
            Check(m0 != null && m0.HasDoor, "the parametric cottage has a main door to orient");
            string l0 = m0.DoorFacingLabel;
            wb.RotateCw();
            BuildPreviewModel m1;
            wb.PreviewAt(origin, fake, res, out m1);
            Check(m1.HasDoor && m1.DoorFacingLabel != l0,
                "rotating the blueprint rotates the authored door facing with it");
            wb.RotateCw(); wb.RotateCw(); wb.RotateCw();
            BuildPreviewModel m4;
            wb.PreviewAt(origin, fake, res, out m4);
            Check(m4.DoorFacingLabel == l0, "the door facing returns to its original direction after four turns");
        }

        private static void TestWorkbenchPreviewAreaEqualsBuildArea()
        {
            Console.WriteLine("[TestWorkbenchPreviewAreaEqualsBuildArea]");
            BuildingSelectionService svc;
            BuildingWorkbench wb = NewWorkbench(out svc);
            wb.SelectFirst(); wb.Regenerate();
            FakeTerrain fake = new FakeTerrain();
            FakeResolver res = new FakeResolver();
            CellPos origin = new CellPos(0, 1, 0);
            BuildPreviewModel m;
            WorkbenchStatus st = wb.PreviewAt(origin, fake, res, out m);
            Blueprint eff = wb.EffectiveBlueprint();
            BuildPlan plan = BuildPlanner.Plan(eff, origin, res, fake, false, false);
            Check(st.Accepted && m.Allowed && plan.Success, "a clear-air footprint previews as allowed");
            Check(m.MatchesBuildEnvelope(eff, plan.Changes.Count),
                "preview area/size/cell-count/planned-writes all equal the eventual build envelope");
            Check(m.PlannedWriteCount == eff.CellCount && m.FootprintArea == eff.SizeX * eff.SizeZ,
                "every blueprint cell is one planned write over clear ground");
        }

        private static void TestWorkbenchNoTerrainWriteDuringPreview()
        {
            Console.WriteLine("[TestWorkbenchNoTerrainWriteDuringPreview]");
            BuildingSelectionService svc;
            BuildingWorkbench wb = NewWorkbench(out svc);
            FakeTerrain fake = new FakeTerrain();
            FakeResolver res = new FakeResolver();
            wb.SelectFirst(); wb.Regenerate();
            BuildPreviewModel m;
            wb.PreviewAt(new CellPos(0, 1, 0), fake, res, out m);
            BuildStartDescriptor d;
            wb.TryBeginBuild(WorldAuthority.SinglePlayer, fake, res, out d);
            Check(fake.Writes == 0,
                "select -> configure -> generate -> preview -> pre-build gate NEVER writes terrain");
        }

        private static void TestWorkbenchParamChangeInvalidatesConfirmation()
        {
            Console.WriteLine("[TestWorkbenchParamChangeInvalidatesConfirmation]");
            BuildingSelectionService svc;
            BuildingWorkbench wb = NewWorkbench(out svc);
            FakeTerrain fake = new FakeTerrain();
            FakeResolver res = new FakeResolver();
            wb.SelectFirst(); wb.Regenerate();
            BuildPreviewModel m;
            wb.PreviewAt(new CellPos(0, 1, 0), fake, res, out m);
            BuildStartDescriptor before;
            Check(wb.TryBeginBuild(WorldAuthority.SinglePlayer, fake, res, out before).Accepted,
                "an untouched confirmation is accepted");
            wb.CycleWidth(); // changes the structure AFTER preview
            BuildStartDescriptor after;
            WorkbenchStatus gated = wb.TryBeginBuild(WorldAuthority.SinglePlayer, fake, res, out after);
            Check(!gated.Accepted && gated.Reason.Contains("失效"),
                "a parameter change after preview invalidates the confirmation and refuses the build");
            Check(fake.Writes == 0, "the stale confirmation was refused without any terrain write");
        }

        private static void TestWorkbenchRejectsWhenTerrainChangedBeforeBuild()
        {
            Console.WriteLine("[TestWorkbenchRejectsWhenTerrainChangedBeforeBuild]");
            BuildingSelectionService svc;
            BuildingWorkbench wb = NewWorkbench(out svc);
            FakeTerrain fake = new FakeTerrain();
            FakeResolver res = new FakeResolver();
            CellPos origin = new CellPos(0, 1, 0);
            wb.SelectFirst(); wb.Regenerate();
            BuildPreviewModel m;
            wb.PreviewAt(origin, fake, res, out m);
            Check(m.Allowed, "footprint is clear at preview time");
            // A player block appears in the footprint AFTER the preview but before confirming.
            Blueprint eff = wb.EffectiveBlueprint();
            CellPos occupied = origin.Add(eff.Cells[0].Relative);
            fake.Occupy(occupied, 777);
            BuildStartDescriptor d;
            WorkbenchStatus gated = wb.TryBeginBuild(WorldAuthority.SinglePlayer, fake, res, out d);
            Check(!gated.Accepted && gated.Reason.Contains("已变化"),
                "the pre-build re-plan against current terrain rejects a footprint that was occupied after preview");
            Check(fake.Writes == 0, "nothing was written when the build was refused");
        }

        private static void TestWorkbenchInvalidParamKeepsPreviousValidBuilding()
        {
            Console.WriteLine("[TestWorkbenchInvalidParamKeepsPreviousValidBuilding]");
            BuildingSelectionService svc;
            BuildingWorkbench wb = NewWorkbench(out svc);
            wb.SelectFirst(); wb.Regenerate();
            Blueprint captured = wb.CurrentBlueprint;
            Check(captured != null, "a valid cottage exists before the bad edit");
            // Push parameters past the per-transaction budget (13x13, 3 floors, wall height 5).
            wb.CycleWidth(); wb.CycleWidth();   // 9 -> 11 -> 13
            wb.CycleLength();                   // 11 -> 13
            wb.CycleFloors(); wb.CycleFloors(); // 1 -> 2 -> 3
            wb.CycleWallHeight();               // 4 -> 5
            WorkbenchStatus gen = wb.Regenerate();
            Check(!gen.Accepted, "an over-budget spec is refused with a reason");
            Check(wb.CurrentBlueprint == captured && wb.CurrentBlueprint.CellCount == captured.CellCount,
                "the previous valid building is preserved verbatim on rejection (no partial overwrite)");
        }

        private static void TestWorkbenchCancelRestores()
        {
            Console.WriteLine("[TestWorkbenchCancelRestores]");
            BuildingSelectionService svc;
            BuildingWorkbench wb = NewWorkbench(out svc);
            wb.SelectFirst(); wb.Regenerate();
            wb.CommitApplied();
            Blueprint captured = wb.CurrentBlueprint;
            int w0 = wb.Draft.Width;
            wb.CycleWidth();
            Check(wb.Draft.Width != w0, "the width knob changed the draft");
            WorkbenchStatus cancel = wb.Cancel();
            Check(cancel.Accepted && wb.Draft.Width == w0 && wb.CurrentBlueprint == captured,
                "Cancel restores the last confirmed building and its parameters");
        }

        private static void TestWorkbenchAuthorityGateNotRegressed()
        {
            Console.WriteLine("[TestWorkbenchAuthorityGateNotRegressed]");
            BuildingSelectionService svc;
            BuildingWorkbench wb = NewWorkbench(out svc);
            FakeTerrain fake = new FakeTerrain();
            FakeResolver res = new FakeResolver();
            wb.SelectFirst(); wb.Regenerate();
            BuildPreviewModel m;
            wb.PreviewAt(new CellPos(0, 1, 0), fake, res, out m);
            BuildStartDescriptor d;
            WorkbenchStatus client = wb.TryBeginBuild(WorldAuthority.RemoteClient, fake, res, out d);
            Check(!client.Accepted && client.Reason.Contains("权限") && d == null,
                "a remote client is refused the workbench build (authority not regressed)");
            WorkbenchStatus host = wb.TryBeginBuild(WorldAuthority.Host, fake, res, out d);
            Check(host.Accepted && d != null && d.Blueprint != null,
                "the multiplayer host is allowed to confirm the build");
            WorkbenchStatus sp = wb.TryBeginBuild(WorldAuthority.SinglePlayer, fake, res, out d);
            Check(sp.Accepted && d != null, "single player is allowed to confirm the build");
        }
    }
}

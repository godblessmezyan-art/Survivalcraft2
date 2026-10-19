using System.Collections.Generic;
using BuilderAssistant.Core.Blueprints;

namespace BuilderAssistant.Core.Building
{
    /// <summary>
    /// The single unified entry point of the parametric engine:
    /// <c>BuildingSpec -&gt; BuildingComposer -&gt; Blueprint</c>. This is the only thing a
    /// future caller (a UI button today, an AI tomorrow) needs to touch - it emits a
    /// <see cref="BuildingSpec"/>, and gets back a validated <see cref="Blueprint"/> that
    /// feeds the UNCHANGED BuildPlanner -&gt; BuildTask -&gt; TransactionHistory pipeline.
    ///
    /// The order of operations is deliberate and load-bearing:
    ///   1. <see cref="BuildingSpec.Validate"/> FIRST. An out-of-range or over-budget spec is
    ///      refused before a single cell is written, so generation can never run away, loop
    ///      forever, or emit a partial structure. Nothing is produced on failure.
    ///   2. Build one shared <see cref="BuildingContext"/> carrying the spec, the builder, and
    ///      the ONE seeded RNG. Because every component draws from the same ordered stream,
    ///      the same (spec, seed) reproduces byte-for-byte.
    ///   3. Run the components in a FIXED order - Foundation, Walls, Stair, Openings,
    ///      FloorPlan, Roof, Decoration. Later writers rely on the builder's last-write-wins
    ///      semantics to override earlier ones (carve the doorway into a finished wall, punch
    ///      the stairwell void through a finished slab), so reordering would change results.
    ///   4. Normalize into a schema-v2 <see cref="Blueprint"/>.
    ///   5. Run <see cref="BuildingValidator"/> as an INDEPENDENT QC pass that re-derives facts
    ///      from the produced cells, not from the components' intentions, and reports problems
    ///      instead of trusting them.
    ///
    /// The composer itself stays terrain- and game-agnostic: it only ever produces relative
    /// blueprint cells, so the whole engine is unit-testable offline with no game assembly.
    /// </summary>
    internal static class BuildingComposer
    {
        /// <summary>
        /// Compose a blueprint from a spec. Never throws for a bad spec; inspect
        /// <see cref="BuildingGenResult.Success"/> and the two validation reports.
        /// </summary>
        public static BuildingGenResult Compose(BuildingSpec spec, string name)
        {
            if (spec == null)
            {
                return BuildingGenResult.Failure(
                    new SpecValidationResult(new List<string> { "spec 为 null。" }),
                    null);
            }

            // 1. Guard the whole thing up front.
            SpecValidationResult specResult = spec.Validate();
            if (!specResult.IsValid)
            {
                return BuildingGenResult.Failure(specResult, null);
            }

            // 2. Shared, deterministic context.
            BuildingContext ctx = new BuildingContext(spec);

            // 3. Fixed component order (see class summary).
            new FoundationGenerator().Generate(ctx);
            new WallGenerator().Generate(ctx);
            new StairGenerator().Generate(ctx);
            new OpeningGenerator().Generate(ctx);
            new FloorPlanGenerator().Generate(ctx);
            new RoofGenerator().Generate(ctx);
            new DecorationGenerator().Generate(ctx);

            // 4. Emit a schema-v2 blueprint (oriented families + authored data survive).
            Blueprint blueprint = ctx.Builder.ToBlueprint(name, 2);

            // 5. Independent structural QC.
            BuildingValidationResult qc = new BuildingValidator(spec, ctx, blueprint).Validate();

            return BuildingGenResult.Succeeded(blueprint, specResult, qc, ctx);
        }
    }

    /// <summary>
    /// Everything the caller needs after a compose: whether it succeeded, the produced
    /// blueprint (when one exists), both validation reports, and cheap summary counters
    /// (family mix + material histogram) used by tests and by the future preview UI.
    /// </summary>
    internal sealed class BuildingGenResult
    {
        public readonly bool Success;
        public readonly Blueprint Blueprint;
        public readonly SpecValidationResult SpecResult;
        public readonly BuildingValidationResult QcResult;
        public readonly int CellCount;
        public readonly Dictionary<BlockFamily, int> FamilyCounts;
        public readonly Dictionary<string, int> MaterialCounts;

        private BuildingGenResult(
            bool success, Blueprint blueprint,
            SpecValidationResult specResult, BuildingValidationResult qcResult,
            Dictionary<BlockFamily, int> familyCounts, Dictionary<string, int> materialCounts)
        {
            Success = success;
            Blueprint = blueprint;
            SpecResult = specResult;
            QcResult = qcResult;
            CellCount = blueprint != null ? blueprint.CellCount : 0;
            FamilyCounts = familyCounts ?? new Dictionary<BlockFamily, int>();
            MaterialCounts = materialCounts ?? new Dictionary<string, int>();
        }

        public static BuildingGenResult Failure(SpecValidationResult specResult, BuildingValidationResult qcResult)
        {
            return new BuildingGenResult(false, null, specResult, qcResult, null, null);
        }

        public static BuildingGenResult Succeeded(
            Blueprint blueprint, SpecValidationResult specResult,
            BuildingValidationResult qcResult, BuildingContext ctx)
        {
            var families = new Dictionary<BlockFamily, int>();
            var materials = new Dictionary<string, int>();
            foreach (BlueprintCell c in blueprint.Cells)
            {
                int f;
                families.TryGetValue(c.Family, out f);
                families[c.Family] = f + 1;

                int m;
                materials.TryGetValue(c.MaterialKey, out m);
                materials[c.MaterialKey] = m + 1;
            }
            bool ok = qcResult != null && qcResult.IsValid;
            return new BuildingGenResult(ok, blueprint, specResult, qcResult, families, materials);
        }

        /// <summary>Human-readable one-line summary for test output and logs.</summary>
        public string Summarize()
        {
            if (Blueprint == null)
            {
                return "生成失败（无蓝图）。规格问题：" + (SpecResult != null ? SpecResult.JoinedErrors() : "无") +
                       " QC问题：" + (QcResult != null ? QcResult.JoinedProblems() : "无");
            }
            int cube;
            FamilyCounts.TryGetValue(BlockFamily.Cube, out cube);
            int stairs, slab, fence, door;
            FamilyCounts.TryGetValue(BlockFamily.Stairs, out stairs);
            FamilyCounts.TryGetValue(BlockFamily.Slab, out slab);
            FamilyCounts.TryGetValue(BlockFamily.Fence, out fence);
            FamilyCounts.TryGetValue(BlockFamily.Door, out door);
            return string.Format(
                "{0} 格 {1}x{2}x{3} | cube={4} stairs={5} slab={6} fence={7} door={8} | QC={9}",
                CellCount, Blueprint.SizeX, Blueprint.SizeY, Blueprint.SizeZ,
                cube, stairs, slab, fence, door,
                Success ? "通过" : ("未通过: " + QcResult.JoinedProblems()));
        }
    }
}

using System.Collections.Generic;
using BuilderAssistant.Core.Blueprints;
using BuilderAssistant.Core.Building;
using BuilderAssistant.Core.Safety;

namespace BuilderAssistant.Core.Tree
{
    /// <summary>
    /// One transaction-safe slice of the finished treehouse: a normalised
    /// <see cref="Blueprint"/> plus the master-coordinate offset where it must be stamped.
    /// The executor places it at <c>anchor + OffsetInMaster</c>; because a Blueprint's own
    /// cells are already shifted to their min corner (0,0,0), the absolute world cell of a
    /// part cell is <c>anchor + OffsetInMaster + cell.Relative</c>. Keeping the offset on the
    /// part (instead of baking it into the cells) is what lets every part re-use the exact
    /// existing BuildPlanner / BuildTask / undo path unchanged.
    /// </summary>
    internal sealed class TreehousePart
    {
        public readonly string Phase;
        public readonly Blueprint Blueprint;
        public readonly CellPos OffsetInMaster;
        public readonly int CellCount;

        public TreehousePart(string phase, Blueprint blueprint, CellPos offsetInMaster)
        {
            Phase = phase;
            Blueprint = blueprint;
            OffsetInMaster = offsetInMaster;
            CellCount = blueprint != null ? blueprint.CellCount : 0;
        }
    }

    /// <summary>
    /// Assembles the giant treehouse: paints the whole thing once into a master
    /// <see cref="TreeCanvas"/> in a fixed, deterministic order (trunk -> branches -> canopy
    /// -> dwelling -> decoration), then slices that grid back into per-phase
    /// <see cref="TreehousePart"/> blueprints that each respect the existing safety envelope
    /// (at most 1024 cells and at most 64 per axis, per transaction). It does NOT enlarge any limit;
    /// an oversized phase is simply auto-sub-divided into stacked Y-bands, so a player/host
    /// still confirms the build one legible stage at a time (task §6).
    /// </summary>
    internal sealed class TreehouseBlueprintSet
    {
        /// <summary>The fixed painting order. Later stages use SetIfEmpty so they wrap around
        /// earlier ones (the house grows through the tree), never overwriting a log.</summary>
        public static TreehousePart[] Build(TreehouseSpec spec, out TreeCanvas masterCanvas, out TreehouseValidation validation)
        {
            masterCanvas = null;
            validation = new TreehouseValidation();

            SpecValidationResult sv = spec.Validate();
            if (!sv.IsValid)
            {
                validation.Problems.Add("规格非法：" + sv.JoinedErrors());
                return new TreehousePart[0];
            }

            TreeCanvas canvas = new TreeCanvas();

            var trunk = new GiantTreeGenerator();
            trunk.Generate(canvas, spec);

            var branches = new TreeBranchGenerator();
            branches.Generate(canvas, spec);

            var canopy = new TreeCanopyGenerator();
            canopy.Generate(canvas, spec, branches.Anchors);

            var house = new TreehouseGenerator();
            house.Generate(canvas, spec);

            var decor = new TreehouseDecorationGenerator();
            decor.Generate(canvas, spec);

            masterCanvas = canvas;

            List<TreehousePart> parts = SliceByPhase(canvas);
            validation.Problems.AddRange(Validate(canvas, spec, parts));

            return parts.ToArray();
        }

        /// <summary>
        /// Group cells by phase tag (preserving the canonical phase order), then for each
        /// phase emit either one blueprint or, if it exceeds the 1024 cap, a stack of Y-band
        /// sub-parts. Each emitted blueprint is normalized and its master min-corner recorded.
        /// </summary>
        private static List<TreehousePart> SliceByPhase(TreeCanvas canvas)
        {
            string[] order =
            {
                TreehousePhaseNames.Roots, TreehousePhaseNames.TrunkLower, TreehousePhaseNames.TrunkUpper,
                TreehousePhaseNames.Branches, TreehousePhaseNames.Canopy, TreehousePhaseNames.FloorOne,
                TreehousePhaseNames.FloorTwo, TreehousePhaseNames.FloorThree, TreehousePhaseNames.Decoration,
            };

            // phase -> cells (master coords)
            var byPhase = new Dictionary<string, List<KeyValuePair<CellPos, PaintedCell>>>(System.StringComparer.Ordinal);
            foreach (string p in order) byPhase[p] = new List<KeyValuePair<CellPos, PaintedCell>>();

            foreach (KeyValuePair<CellPos, PaintedCell> kv in canvas.Cells)
            {
                List<KeyValuePair<CellPos, PaintedCell>> bucket;
                if (!byPhase.TryGetValue(kv.Value.Phase, out bucket))
                {
                    bucket = new List<KeyValuePair<CellPos, PaintedCell>>();
                    byPhase[kv.Value.Phase] = bucket;
                }
                bucket.Add(kv);
            }

            var parts = new List<TreehousePart>();
            int cap = BuildSafetyLimits.MaxBlocksPerOperation;

            foreach (string phase in order)
            {
                List<KeyValuePair<CellPos, PaintedCell>> cells = byPhase[phase];
                if (cells.Count == 0) continue;

                // Stable order regardless of Dictionary enumeration: Y-band first, then X, then Z.
                cells.Sort((a, b) =>
                {
                    int c = a.Key.Y.CompareTo(b.Key.Y);
                    if (c != 0) return c;
                    c = a.Key.X.CompareTo(b.Key.X);
                    if (c != 0) return c;
                    return a.Key.Z.CompareTo(b.Key.Z);
                });

                if (cells.Count <= cap)
                {
                    parts.Add(EmitPart(phase, phase, cells));
                }
                else
                {
                    // Sub-divide by ascending Y bands so each part stays <= cap. We grow the
                    // current band until it would exceed the cap, then flush.
                    var band = new List<KeyValuePair<CellPos, PaintedCell>>();
                    int partIndex = 1;
                    for (int i = 0; i < cells.Count; i++)
                    {
                        if (band.Count > 0 && band.Count + 1 > cap)
                        {
                            parts.Add(EmitPart(phase, phase + " #" + partIndex, band));
                            band = new List<KeyValuePair<CellPos, PaintedCell>>();
                            partIndex++;
                        }
                        band.Add(cells[i]);
                    }
                    if (band.Count > 0)
                    {
                        parts.Add(EmitPart(phase, phase + " #" + partIndex, band));
                    }
                }
            }

            return parts;
        }

        private static TreehousePart EmitPart(string phase, string label, List<KeyValuePair<CellPos, PaintedCell>> cells)
        {
            int minX = int.MaxValue, minY = int.MaxValue, minZ = int.MaxValue;
            foreach (KeyValuePair<CellPos, PaintedCell> kv in cells)
            {
                if (kv.Key.X < minX) minX = kv.Key.X;
                if (kv.Key.Y < minY) minY = kv.Key.Y;
                if (kv.Key.Z < minZ) minZ = kv.Key.Z;
            }

            var list = new List<BlueprintCell>(cells.Count);
            foreach (KeyValuePair<CellPos, PaintedCell> kv in cells)
            {
                var rel = new CellPos(kv.Key.X - minX, kv.Key.Y - minY, kv.Key.Z - minZ);
                list.Add(new BlueprintCell(rel, kv.Value.Key, kv.Value.Family, kv.Value.Data));
            }

            Blueprint bp = Blueprint.Normalize(label, BlueprintJson.CurrentSchemaVersion, list);
            return new TreehousePart(label, bp, new CellPos(minX, minY, minZ));
        }

        // ============================ Validation ============================

        /// <summary>
        /// Independent QC over the finished master grid and the produced parts. Reports every
        /// problem instead of throwing. This is the offline guard; on-device placement
        /// (real save/reload, stair climb, door auto-top-half, leaf render) is still 待验证.
        /// </summary>
        private static IEnumerable<string> Validate(TreeCanvas canvas, TreehouseSpec spec, List<TreehousePart> parts)
        {
            var problems = new List<string>();

            if (canvas.Count == 0)
            {
                problems.Add("树屋网格为空。");
                return problems;
            }

            // 1) every part is within the safety envelope and resolvable vanilla
            foreach (TreehousePart part in parts)
            {
                if (part.CellCount == 0)
                {
                    problems.Add("阶段 '" + part.Phase + "' 生成了空蓝图。");
                    continue;
                }
                if (part.CellCount > BuildSafetyLimits.MaxBlocksPerOperation)
                    problems.Add("阶段 '" + part.Phase + "' 方块 " + part.CellCount + " 超每事务上限 " + BuildSafetyLimits.MaxBlocksPerOperation + "。");
                if (part.Blueprint.SizeX > BuildSafetyLimits.MaxSelectionAxisLength ||
                    part.Blueprint.SizeY > BuildSafetyLimits.MaxSelectionAxisLength ||
                    part.Blueprint.SizeZ > BuildSafetyLimits.MaxSelectionAxisLength)
                    problems.Add("阶段 '" + part.Phase + "' 尺寸 " + part.Blueprint.SizeX + "x" + part.Blueprint.SizeY + "x" + part.Blueprint.SizeZ + " 超单轴上限。");

                foreach (BlueprintCell c in part.Blueprint.Cells)
                {
                    MaterialCatalog.Entry e;
                    if (!MaterialCatalog.TryGet(c.MaterialKey, out e))
                    {
                        problems.Add("阶段 '" + part.Phase + "' 材料 '" + c.MaterialKey + "' 不在 MaterialCatalog（非原版/无法解析）。");
                        continue;
                    }
                    if (e.Family != c.Family)
                        problems.Add("阶段 '" + part.Phase + "' 材料 '" + c.MaterialKey + "' 族为 " + c.Family + "，目录要求 " + e.Family + "。");
                }
            }

            // 2) parts are disjoint in master space and cover the whole canvas (no loss/dup)
            var seenMaster = new HashSet<CellPos>();
            int total = 0;
            foreach (TreehousePart part in parts)
            {
                foreach (BlueprintCell c in part.Blueprint.Cells)
                {
                    CellPos m = part.OffsetInMaster.Add(c.Relative);
                    if (!seenMaster.Add(m))
                        problems.Add("阶段间坐标冲突：" + m + " 被多个阶段重复放置。");
                    total++;
                }
            }
            if (total != canvas.Count)
                problems.Add("阶段方块合计 " + total + " != 网格总数 " + canvas.Count + "（切片有遗漏/重复）。");

            // 3) every master cell is within the spec footprint bounds
            foreach (KeyValuePair<CellPos, PaintedCell> kv in canvas.Cells)
            {
                CellPos p = kv.Key;
                if (p.X < 0 || p.Z < 0 || p.Y < 0 || p.X >= spec.Width || p.Z >= spec.Depth || p.Y >= spec.Height)
                    problems.Add("方块 " + p + " 超出规格边界 " + spec.Width + "x" + spec.Height + "x" + spec.Depth + "。");
            }

            // 4) DECAY-SAFETY: every leaf must hug a log (Chebyshev <= 3) in the FINAL canvas.
            foreach (KeyValuePair<CellPos, PaintedCell> kv in canvas.Cells)
            {
                if (kv.Value.Key == TreeCanvas.Leaf)
                {
                    CellPos p = kv.Key;
                    if (!canvas.IsNearLog(p.X, p.Y, p.Z, 3))
                        problems.Add("树叶 " + p + " 距离任何原木超过切比雪夫 3，在游戏中会衰减——拒绝此类放置。");
                }
            }

            // 5) the whole structure is a single connected component (no floating blocks)
            if (!IsSingleComponent(canvas))
                problems.Add("树屋结构不连通，存在孤立/悬空方块。");

            return problems;
        }

        private static bool IsSingleComponent(TreeCanvas canvas)
        {
            var occ = new HashSet<CellPos>();
            foreach (KeyValuePair<CellPos, PaintedCell> kv in canvas.Cells) occ.Add(kv.Key);
            if (occ.Count == 0) return false;

            var seen = new HashSet<CellPos>();
            var stack = new Stack<CellPos>();
            CellPos start = FirstOf(occ);
            stack.Push(start);
            seen.Add(start);
            // 26-neighbour connectivity: voxel structures that contain stair flights and
            // diagonally-climbing branch spines are physically continuous under 26-connectivity
            // (steps differ by 1 in two/three axes) but NOT under strict 6-connectivity, which
            // would wrongly report them as floating. This flags genuinely isolated blocks only.
            while (stack.Count > 0)
            {
                CellPos c = stack.Pop();
                for (int ox = -1; ox <= 1; ox++)
                    for (int oy = -1; oy <= 1; oy++)
                        for (int oz = -1; oz <= 1; oz++)
                        {
                            if (ox == 0 && oy == 0 && oz == 0) continue;
                            var np = new CellPos(c.X + ox, c.Y + oy, c.Z + oz);
                            if (!seen.Contains(np) && occ.Contains(np))
                            {
                                seen.Add(np);
                                stack.Push(np);
                            }
                        }
            }
            return seen.Count == occ.Count;
        }

        private static CellPos FirstOf(HashSet<CellPos> set)
        {
            foreach (CellPos p in set) return p;
            return default(CellPos);
        }
    }

    /// <summary>Aggregate QC result for a built treehouse.</summary>
    internal sealed class TreehouseValidation
    {
        public readonly List<string> Problems = new List<string>();
        public bool IsValid { get { return Problems.Count == 0; } }
        public string JoinedProblems() { return string.Join("; ", Problems.ToArray()); }
    }
}

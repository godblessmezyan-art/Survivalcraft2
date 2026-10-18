using System.Collections.Generic;
using System.Text;
using BuilderAssistant.Core.Blueprints;
using BuilderAssistant.Core.Safety;
using BuilderAssistant.Core.Transactions;

namespace BuilderAssistant.Core.BuildEngine
{
    /// <summary>
    /// Turns a blueprint + anchor into a validated list of absolute writes
    /// WITHOUT touching the world. Every rule from AGENTS.md is enforced here
    /// so the executor never has to re-check policy: hard block cap, per-axis
    /// selection cap, world-height bounds, chunk-loaded checks, the "no default
    /// overwrite of non-air" rule, and material resolvability.
    /// </summary>
    /// <remarks>
    /// The editability pass used to short-circuit on the first bad cell and report
    /// a single collapsed "目标区块未加载或越界", which could not distinguish a
    /// height overflow from an extent error from a chunk the engine simply has not
    /// streamed in yet. It now classifies EVERY target cell and aggregates the
    /// result, so a failure states exactly which checks fired, on which chunks,
    /// within which bounding box, and whether waiting for the engine could help.
    /// Crucially the write list is still only ever returned when ALL cells pass, so
    /// "no writes before full validation" is unchanged.
    /// </remarks>
    internal static class BuildPlanner
    {
        public static BuildPlan Plan(
            Blueprint blueprint,
            CellPos origin,
            IBlockValueResolver resolver,
            ITerrainGateway gateway,
            bool allowOverride)
        {
            if (blueprint == null || blueprint.CellCount == 0)
            {
                return BuildPlan.Fail("蓝图空或无效");
            }

            if (blueprint.CellCount > BuildSafetyLimits.MaxBlocksPerOperation)
            {
                return BuildPlan.Fail(
                    "方块数量 " + blueprint.CellCount + " 超过每事务上限 " + BuildSafetyLimits.MaxBlocksPerOperation);
            }

            if (blueprint.SizeX > BuildSafetyLimits.MaxSelectionAxisLength ||
                blueprint.SizeY > BuildSafetyLimits.MaxSelectionAxisLength ||
                blueprint.SizeZ > BuildSafetyLimits.MaxSelectionAxisLength)
            {
                return BuildPlan.Fail("蓝图尺寸超过单轴上限 " + BuildSafetyLimits.MaxSelectionAxisLength);
            }

            var changes = new List<PlannedChange>(blueprint.CellCount);
            var editabilityFailures = new List<CellEditability>();

            // First-failure trackers for the two non-editability policies. We keep
            // only the first of each so the reported reason stays concise, while
            // still scanning every cell to collect the full editability breakdown.
            bool sawNonAir = false;
            CellPos firstNonAir = default(CellPos);
            bool sawUnknownMaterial = false;
            string firstUnknownMaterial = null;

            int cellCount = blueprint.CellCount;
            for (int i = 0; i < cellCount; i++)
            {
                BlueprintCell cell = blueprint.Cells[i];
                CellPos absolute = origin.Add(cell.Relative);

                CellEditability eco;
                if (!WorldBounds.IsYValid(absolute.Y))
                {
                    eco = new CellEditability(absolute, CellEditabilityStatus.InvalidY);
                }
                else
                {
                    eco = gateway.InspectCell(absolute);
                }

                if (!eco.IsEditable)
                {
                    editabilityFailures.Add(eco);
                    continue;
                }

                if (!allowOverride && !gateway.IsCellAir(absolute))
                {
                    if (!sawNonAir)
                    {
                        sawNonAir = true;
                        firstNonAir = absolute;
                    }
                    continue;
                }

                int value;
                if (!resolver.TryResolve(cell.MaterialKey, out value))
                {
                    if (!sawUnknownMaterial)
                    {
                        sawUnknownMaterial = true;
                        firstUnknownMaterial = cell.MaterialKey;
                    }
                    continue;
                }

                changes.Add(new PlannedChange(absolute, value));
            }

            // Editability problems are reported first and with full detail: they are
            // the historical "目标区块未加载或越界" class and the most actionable.
            if (editabilityFailures.Count > 0)
            {
                bool allRecoverable = true;
                for (int i = 0; i < editabilityFailures.Count; i++)
                {
                    if (!editabilityFailures[i].IsRecoverableByWaiting)
                    {
                        allRecoverable = false;
                        break;
                    }
                }

                string reason = ComposeEditabilityReason(
                    blueprint, origin, cellCount, editabilityFailures, allRecoverable);
                return BuildPlan.Fail(reason, editabilityFailures, allRecoverable);
            }

            if (sawNonAir)
            {
                return BuildPlan.Fail("目标非空气方块，默认不覆盖: " + firstNonAir);
            }

            if (sawUnknownMaterial)
            {
                return BuildPlan.Fail("未知材料键: " + firstUnknownMaterial);
            }

            return BuildPlan.Ok(changes);
        }

        private static string ComposeEditabilityReason(
            Blueprint blueprint,
            CellPos origin,
            int cellCount,
            List<CellEditability> failures,
            bool allRecoverable)
        {
            var sb = new StringBuilder();
            sb.Append("目标区块未就绪或越界：").Append(cellCount)
              .Append(" 格中有 ").Append(failures.Count).Append(" 格不可编辑。");

            sb.Append(" 落点原点=").Append(origin)
              .Append("，包围盒 X[").Append(origin.X).Append("..").Append(origin.X + blueprint.SizeX - 1)
              .Append("] Y[").Append(origin.Y).Append("..").Append(origin.Y + blueprint.SizeY - 1)
              .Append("] Z[").Append(origin.Z).Append("..").Append(origin.Z + blueprint.SizeZ - 1)
              .Append("]。");

            sb.Append(" 分类统计：").Append(DescribeCounts(failures)).Append("。");

            sb.Append(" 示例：").Append(DescribeExamples(failures)).Append("。");

            if (allRecoverable)
            {
                sb.Append(" 结论：均为区块尚未就绪（可能跨越多个区块），等待引擎加载后有望成功，建议稍后重试而非改坐标。");
            }
            else
            {
                sb.Append(" 结论：含越界/无效格（高度或生成范围），等待无法解决，需调整落点或建筑尺寸。");
            }

            return sb.ToString();
        }

        private static string DescribeCounts(List<CellEditability> failures)
        {
            // Fixed order mirrors the enum so the log reads consistently.
            int[] counts = new int[7]; // index == (int)CellEditabilityStatus
            for (int i = 0; i < failures.Count; i++)
            {
                int idx = (int)failures[i].Status;
                if (idx >= 0 && idx < counts.Length)
                {
                    counts[idx]++;
                }
            }

            var sb = new StringBuilder();
            AppendCount(sb, counts, (int)CellEditabilityStatus.InvalidY, "高度越界");
            AppendCount(sb, counts, (int)CellEditabilityStatus.InvalidCell, "超出有效范围");
            AppendCount(sb, counts, (int)CellEditabilityStatus.TerrainNull, "地形未就绪");
            AppendCount(sb, counts, (int)CellEditabilityStatus.ChunkMissing, "区块缺失");
            AppendCount(sb, counts, (int)CellEditabilityStatus.ChunkStateNotReady, "区块状态未就绪");
            AppendCount(sb, counts, (int)CellEditabilityStatus.ChunkNotLoaded, "区块未加载");
            if (sb.Length == 0)
            {
                sb.Append("无");
            }
            return sb.ToString();
        }

        private static void AppendCount(StringBuilder sb, int[] counts, int idx, string label)
        {
            if (counts[idx] <= 0)
            {
                return;
            }
            if (sb.Length > 0)
            {
                sb.Append("、");
            }
            sb.Append(label).Append(counts[idx]).Append("格");
        }

        private static string DescribeExamples(List<CellEditability> failures)
        {
            // Show up to 3 distinct-status examples so the caller sees the shape of
            // the problem without a 470-line log.
            var sb = new StringBuilder();
            var seen = new HashSet<CellEditabilityStatus>();
            for (int i = 0; i < failures.Count && seen.Count < 3; i++)
            {
                CellEditability f = failures[i];
                if (!seen.Add(f.Status))
                {
                    continue;
                }
                if (sb.Length > 0)
                {
                    sb.Append("；");
                }
                sb.Append(f.Describe());
            }
            return sb.Length > 0 ? sb.ToString() : "无";
        }
    }
}

using System.Collections.Generic;
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
            for (int i = 0; i < blueprint.CellCount; i++)
            {
                BlueprintCell cell = blueprint.Cells[i];
                CellPos absolute = origin.Add(cell.Relative);

                if (!WorldBounds.IsYValid(absolute.Y))
                {
                    return BuildPlan.Fail("目标超出世界高度范围 y=" + absolute.Y);
                }

                if (!gateway.IsCellEditable(absolute))
                {
                    return BuildPlan.Fail("目标区块未加载或越界: " + absolute);
                }

                if (!allowOverride && !gateway.IsCellAir(absolute))
                {
                    return BuildPlan.Fail("目标非空气方块，默认不覆盖: " + absolute);
                }

                int value;
                if (!resolver.TryResolve(cell.MaterialKey, out value))
                {
                    return BuildPlan.Fail("未知材料键: " + cell.MaterialKey);
                }

                changes.Add(new PlannedChange(absolute, value));
            }

            return BuildPlan.Ok(changes);
        }
    }
}

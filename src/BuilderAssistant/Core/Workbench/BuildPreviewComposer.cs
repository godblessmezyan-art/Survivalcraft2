using System.Collections.Generic;
using BuilderAssistant.Core.Blueprints;
using BuilderAssistant.Core.BuildEngine;
using BuilderAssistant.Core.Transactions;

namespace BuilderAssistant.Core.Workbench
{
    /// <summary>
    /// Turns (effectiveBlueprint, origin, live gateway) into a <see cref="BuildPreviewModel"/>.
    /// This is the ONLY place the preview reads terrain, and it reads exclusively through the
    /// unchanged <see cref="BuildPlanner"/> (which never writes). Because the model's size,
    /// cell count and planned-write count all come from the very same blueprint + planner call
    /// the build will use, "预览面积 == 最终建造面积" holds by construction.
    /// </summary>
    internal static class BuildPreviewComposer
    {
        public static BuildPreviewModel Compose(
            Blueprint effective,
            CellPos origin,
            IBlockValueResolver resolver,
            ITerrainGateway gateway,
            bool allowOverride,
            bool clearSoft)
        {
            if (effective == null || effective.CellCount == 0)
            {
                return new BuildPreviewModel(
                    origin, 0, 0, 0,
                    origin.X, origin.X - 1, origin.Z, origin.Z - 1,
                    new CellPos[0], 0, 0,
                    origin.Y, origin.Y - 1,
                    -1, null, false,
                    false, "蓝图空或无效。", false, false,
                    0, 0, 0);
            }

            BuildPlan plan = BuildPlanner.Plan(effective, origin, resolver, gateway, allowOverride, clearSoft);

            int minX = origin.X;
            int maxX = origin.X + effective.SizeX - 1;
            int minZ = origin.Z;
            int maxZ = origin.Z + effective.SizeZ - 1;
            int minY = origin.Y;
            int maxY = origin.Y + effective.SizeY - 1;

            CellPos[] corners =
            {
                new CellPos(minX, minY, minZ),
                new CellPos(maxX, minY, minZ),
                new CellPos(maxX, minY, maxZ),
                new CellPos(minX, minY, maxZ),
            };
            int border = PerimeterCells(effective.SizeX, effective.SizeZ);
            int area = effective.SizeX * effective.SizeZ;

            int doorRot;
            string doorLabel;
            bool hasDoor;
            FindMainDoor(effective, origin, out doorRot, out doorLabel, out hasDoor);

            int plannedWrites = plan.Success && plan.Changes != null ? plan.Changes.Count : 0;

            return new BuildPreviewModel(
                origin, effective.SizeX, effective.SizeY, effective.SizeZ,
                minX, maxX, minZ, maxZ,
                corners, border, area,
                minY, maxY,
                doorRot, doorLabel, hasDoor,
                plan.Success, plan.Success ? null : plan.FailureReason,
                plan.AllEditabilityFailuresRecoverable, plan.FailedBySoftObstruction,
                effective.CellCount, plannedWrites, plan.SoftClearedCount);
        }

        /// <summary>
        /// Count of distinct ground cells forming the footprint's outer ring. For a solid
        /// 1-row rectangle this is the perimeter; degenerate 1-cell axes collapse without
        /// double-counting.
        /// </summary>
        private static int PerimeterCells(int sizeX, int sizeZ)
        {
            if (sizeX <= 0 || sizeZ <= 0)
            {
                return 0;
            }
            if (sizeX == 1 && sizeZ == 1)
            {
                return 1;
            }
            if (sizeX == 1)
            {
                return sizeZ;
            }
            if (sizeZ == 1)
            {
                return sizeX;
            }
            return 2 * sizeX + 2 * sizeZ - 4;
        }

        /// <summary>
        /// Locate the structure's main door (the lowest-Y door-family cell) and report the
        /// facing it will actually be placed with. The blueprint is already the effective
        /// (post-rotation) one, so its authored door rotation is the final facing. rot 0..3
        /// map to -Z / -X / +Z / +X exactly as BlockOrientation's facing convention defines.
        /// </summary>
        private static void FindMainDoor(
            Blueprint effective, CellPos origin,
            out int doorRot, out string doorLabel, out bool hasDoor)
        {
            doorRot = -1;
            doorLabel = null;
            hasDoor = false;

            int bestY = int.MaxValue;
            int bestData = 0;
            for (int i = 0; i < effective.Cells.Count; i++)
            {
                BlueprintCell c = effective.Cells[i];
                if (c.Family != BlockFamily.Door)
                {
                    continue;
                }
                int absY = origin.Y + c.Relative.Y;
                if (absY < bestY)
                {
                    bestY = absY;
                    bestData = c.Data;
                    hasDoor = true;
                }
            }

            if (hasDoor)
            {
                doorRot = bestData & 0x3;
                doorLabel = FacingLabel(doorRot);
            }
        }

        private static string FacingLabel(int rot)
        {
            switch (rot)
            {
                case 0: return "朝 -Z";
                case 1: return "朝 -X";
                case 2: return "朝 +Z";
                case 3: return "朝 +X";
                default: return "?";
            }
        }
    }
}

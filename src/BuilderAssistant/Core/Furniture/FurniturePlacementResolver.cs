using System;
using System.Collections.Generic;
using BuilderAssistant.Core.Blueprints;
using BuilderAssistant.Core.Safety;

namespace BuilderAssistant.Core.Furniture
{
    internal static class FurniturePlacementResolver
    {
        // Full bounding boxes are reserved, including holes under desks and in corners.
        // Expansion order is structural cells, then each template bottom-up. One shared
        // BuildTask owns all writes, so a furniture failure also rolls back the structure.
        public static Blueprint Compose(string name, int version, List<BlueprintCell> structure,
            List<FurniturePlacement> placements)
        {
            if (structure.Count > BuildSafetyLimits.MaxBlocksPerOperation)
                throw new ArgumentException("结构和家具合计超过每事务方块上限。");
            var all = new List<BlueprintCell>(structure);
            var occupied = new HashSet<CellPos>();
            foreach (BlueprintCell cell in structure)
            {
                ValidateCoordinate(cell.Relative);
                if (!occupied.Add(cell.Relative)) throw new ArgumentException("结构坐标重复: " + cell.Relative);
            }
            // Native doors create an upper half not explicitly present in cells.
            foreach (BlueprintCell cell in structure)
                if (cell.Family == BlockFamily.Door) occupied.Add(cell.Relative.Add(new CellPos(0, 1, 0)));
            foreach (FurniturePlacement placement in placements)
            {
                FurnitureTemplate template;
                if (placement == null || !FurnitureTemplateRegistry.Default.TryGet(placement.TemplateId, out template))
                    throw new ArgumentException("未知家具模板: " + (placement == null ? "null" : placement.TemplateId));
                if (placement.Rotation < 0 || placement.Rotation > 3)
                    throw new ArgumentException("家具 " + placement.TemplateId + " rotation 必须为 0..3。");
                if (placement.Color != "natural" || placement.Variant != "default")
                    throw new ArgumentException("家具 " + placement.TemplateId + " 仅支持 color=natural / variant=default。");
                ValidateCoordinate(placement.Position);
                int sx = (placement.Rotation & 1) == 0 ? template.SizeX : template.SizeZ;
                int sz = (placement.Rotation & 1) == 0 ? template.SizeZ : template.SizeX;
                for (int y = 0; y < template.SizeY; y++)
                for (int x = 0; x < sx; x++)
                for (int z = 0; z < sz; z++)
                {
                    CellPos pos = placement.Position.Add(new CellPos(x, y, z));
                    if (!occupied.Add(pos)) throw new ArgumentException("家具 " + placement.TemplateId + " 包围盒与结构/家具冲突: " + pos);
                }
                foreach (BlueprintCell cell in template.Cells)
                {
                    BlueprintCell turned = Blueprint.TransformCell(cell, template.SizeX, template.SizeZ,
                        placement.Rotation, placement.MirrorX);
                    all.Add(new BlueprintCell(placement.Position.Add(turned.Relative), turned.MaterialKey, turned.Family, turned.Data));
                }
                if (all.Count > BuildSafetyLimits.MaxBlocksPerOperation)
                    throw new ArgumentException("结构和家具合计超过每事务方块上限。");
            }
            if (all.Count == 0) throw new ArgumentException("蓝图没有任何方块或家具。");
            Blueprint normalized = Blueprint.Normalize(name, version, all);
            if (normalized.SizeX > BuildSafetyLimits.MaxSelectionAxisLength || normalized.SizeY > BuildSafetyLimits.MaxSelectionAxisLength || normalized.SizeZ > BuildSafetyLimits.MaxSelectionAxisLength)
                throw new ArgumentException("结构和家具尺寸超过单轴上限。");
            int minX = int.MaxValue, minY = int.MaxValue, minZ = int.MaxValue;
            foreach (BlueprintCell cell in all)
            {
                minX = Math.Min(minX, cell.Relative.X); minY = Math.Min(minY, cell.Relative.Y); minZ = Math.Min(minZ, cell.Relative.Z);
            }
            var offset = new CellPos(-minX, -minY, -minZ);
            var shiftedStructure = new List<BlueprintCell>();
            foreach (BlueprintCell cell in structure) shiftedStructure.Add(new BlueprintCell(cell.Relative.Add(offset), cell.MaterialKey, cell.Family, cell.Data));
            var shiftedPlacements = new List<FurniturePlacement>();
            foreach (FurniturePlacement p in placements) shiftedPlacements.Add(new FurniturePlacement(p.TemplateId, p.Position.Add(offset), p.Rotation, p.Variant, p.Color, p.MirrorX));
            return new Blueprint(name, version, normalized.SizeX, normalized.SizeY, normalized.SizeZ,
                new List<BlueprintCell>(normalized.Cells), shiftedStructure, shiftedPlacements);
        }

        private static void ValidateCoordinate(CellPos p)
        {
            if (p.X < -1000000 || p.X > 1000000 || p.Y < -1000000 || p.Y > 1000000 || p.Z < -1000000 || p.Z > 1000000)
                throw new ArgumentException("家具蓝图相对坐标超出安全范围 ±1000000。");
        }
    }
}

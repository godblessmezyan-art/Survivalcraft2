using System;
using System.Collections.Generic;
using BuilderAssistant.Core.Furniture;

namespace BuilderAssistant.Core.Blueprints
{
    /// <summary>
    /// A self-contained, terrain-independent structure definition: a set of
    /// relative cells with logical material keys. It carries no world position
    /// and no block indices, so the same blueprint can be rotated, mirrored,
    /// re-skinned, and stamped at any anchor. Cell coordinates are normalized
    /// so the minimum corner is (0,0,0).
    /// </summary>
    internal sealed class Blueprint
    {
        public string Name { get; }
        public int SchemaVersion { get; }
        public int SizeX { get; }
        public int SizeY { get; }
        public int SizeZ { get; }

        private readonly List<BlueprintCell> _cells;
        public IReadOnlyList<BlueprintCell> Cells { get { return _cells; } }
        public int CellCount { get { return _cells.Count; } }
        public IReadOnlyList<BlueprintCell> StructureCells { get; private set; }
        public IReadOnlyList<FurniturePlacement> FurniturePlacements { get; private set; }
        private readonly Dictionary<CellPos, string> _furnitureContexts = new Dictionary<CellPos, string>();
        public IEnumerable<CellPos> FurnitureFootprint { get { return _furnitureContexts.Keys; } }
        public string FurnitureContext(CellPos relative)
        {
            string context;
            return _furnitureContexts.TryGetValue(relative, out context) ? context : string.Empty;
        }

        public Blueprint(string name, int schemaVersion, int sizeX, int sizeY, int sizeZ, List<BlueprintCell> cells,
            List<BlueprintCell> structure = null, List<FurniturePlacement> placements = null)
        {
            if (cells == null) throw new ArgumentNullException("cells");
            Name = name;
            SchemaVersion = schemaVersion;
            SizeX = sizeX;
            SizeY = sizeY;
            SizeZ = sizeZ;
            _cells = cells;
            StructureCells = (structure ?? cells).AsReadOnly();
            FurniturePlacements = (placements ?? new List<FurniturePlacement>()).AsReadOnly();
            foreach (FurniturePlacement p in FurniturePlacements)
            {
                FurnitureTemplate t;
                if (!FurnitureTemplateRegistry.Default.TryGet(p.TemplateId, out t)) continue;
                int sx = (p.Rotation & 1) == 0 ? t.SizeX : t.SizeZ;
                int sz = (p.Rotation & 1) == 0 ? t.SizeZ : t.SizeX;
                string context = "家具 " + p.TemplateId + " @" + p.Position + ": ";
                for (int y = 0; y < t.SizeY; y++)
                for (int x = 0; x < sx; x++)
                for (int z = 0; z < sz; z++)
                    _furnitureContexts[p.Position.Add(new CellPos(x, y, z))] = context;
            }
        }

        /// <summary>
        /// Produce a copy of this blueprint with every relative cell passed
        /// through the given orientation. Rotation/mirror operate purely on the
        /// blueprint space; the terrain layer is untouched. This is what lets
        /// Phase-4 style rotation and mirroring reuse the exact same build path.
        /// </summary>
        public Blueprint Reorient(int rotationSteps, bool mirrorX)
        {
            // rotationSteps is 0..3 (quarter turns clockwise about the Y axis).
            int r = ((rotationSteps % 4) + 4) % 4;
            int w = SizeX;
            int l = SizeZ;

            if (FurniturePlacements.Count > 0)
            {
                var structure = new List<BlueprintCell>();
                foreach (BlueprintCell cell in StructureCells) structure.Add(TransformCell(cell, w, l, r, mirrorX));
                var placements = new List<FurniturePlacement>();
                foreach (FurniturePlacement p in FurniturePlacements)
                {
                    FurnitureTemplate t;
                    FurnitureTemplateRegistry.Default.TryGet(p.TemplateId, out t);
                    int sx = (p.Rotation & 1) == 0 ? t.SizeX : t.SizeZ;
                    int sz = (p.Rotation & 1) == 0 ? t.SizeZ : t.SizeX;
                    CellPos a = TransformCell(new BlueprintCell(p.Position, ""), w, l, r, mirrorX).Relative;
                    CellPos b = TransformCell(new BlueprintCell(p.Position.Add(new CellPos(sx - 1, 0, sz - 1)), ""), w, l, r, mirrorX).Relative;
                    int rotation = ((p.Rotation + (p.MirrorX ? -r : r)) % 4 + 4) % 4;
                    placements.Add(new FurniturePlacement(p.TemplateId,
                        new CellPos(Math.Min(a.X, b.X), p.Position.Y, Math.Min(a.Z, b.Z)),
                        rotation, p.Variant, p.Color, p.MirrorX ^ mirrorX));
                }
                return FurniturePlacementResolver.Compose(Name, SchemaVersion, structure, placements);
            }

            var result = new List<BlueprintCell>(_cells.Count);
            for (int i = 0; i < _cells.Count; i++)
            {
                BlueprintCell c = _cells[i];
                int x = c.Relative.X;
                int z = c.Relative.Z;
                int nx;
                int nz;
                switch (r)
                {
                    case 0: nx = x; nz = z; break;
                    case 1: nx = (l - 1 - z); nz = x; break;
                    case 2: nx = (w - 1 - x); nz = (l - 1 - z); break;
                    default: nx = z; nz = (w - 1 - x); break; // r == 3
                }
                if (mirrorX)
                {
                    int newW = (r == 1 || r == 3) ? l : w;
                    nx = newW - 1 - nx;
                }

                // Orientation must follow the geometry: rotate the authored data by the
                // same number of clockwise quarter turns the footprint was turned, then
                // apply the X-mirror only when the footprint was also mirrored (the
                // mirror operates in the already-rotated frame, matching Reorient).
                int data = BlockOrientation.Rotate(c.Family, c.Data, r);
                if (mirrorX)
                {
                    data = BlockOrientation.MirrorX(c.Family, data);
                }

                result.Add(new BlueprintCell(new CellPos(nx, c.Relative.Y, nz), c.MaterialKey, c.Family, data));
            }

            // Re-normalize so the min corner is again (0,0,0).
            return Normalize(Name, SchemaVersion, result);
        }

        internal static BlueprintCell TransformCell(BlueprintCell cell, int width, int length, int rotation, bool mirrorX)
        {
            int x = cell.Relative.X, z = cell.Relative.Z, nx, nz;
            switch (rotation)
            {
                case 0: nx = x; nz = z; break;
                case 1: nx = length - 1 - z; nz = x; break;
                case 2: nx = width - 1 - x; nz = length - 1 - z; break;
                default: nx = z; nz = width - 1 - x; break;
            }
            if (mirrorX) nx = ((rotation & 1) == 0 ? width : length) - 1 - nx;
            int data = BlockOrientation.Rotate(cell.Family, cell.Data, rotation);
            if (mirrorX) data = BlockOrientation.MirrorX(cell.Family, data);
            return new BlueprintCell(new CellPos(nx, cell.Relative.Y, nz), cell.MaterialKey, cell.Family, data);
        }

        internal static Blueprint Normalize(string name, int schemaVersion, List<BlueprintCell> cells)
        {
            if (cells.Count == 0)
            {
                return new Blueprint(name, schemaVersion, 0, 0, 0, new List<BlueprintCell>());
            }

            int minX = int.MaxValue, minY = int.MaxValue, minZ = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue, maxZ = int.MinValue;
            for (int i = 0; i < cells.Count; i++)
            {
                CellPos p = cells[i].Relative;
                if (p.X < minX) minX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.Z < minZ) minZ = p.Z;
                if (p.X > maxX) maxX = p.X;
                if (p.Y > maxY) maxY = p.Y;
                if (p.Z > maxZ) maxZ = p.Z;
            }

            var shifted = new List<BlueprintCell>(cells.Count);
            for (int i = 0; i < cells.Count; i++)
            {
                BlueprintCell c = cells[i];
                shifted.Add(new BlueprintCell(
                    new CellPos(c.Relative.X - minX, c.Relative.Y - minY, c.Relative.Z - minZ),
                    c.MaterialKey, c.Family, c.Data));
            }

            int sizeX = maxX - minX + 1;
            int sizeY = maxY - minY + 1;
            int sizeZ = maxZ - minZ + 1;
            return new Blueprint(name, schemaVersion, sizeX, sizeY, sizeZ, shifted);
        }
    }
}

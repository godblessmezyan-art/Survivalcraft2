using System;
using System.Collections.Generic;

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

        public Blueprint(string name, int schemaVersion, int sizeX, int sizeY, int sizeZ, List<BlueprintCell> cells)
        {
            if (cells == null) throw new ArgumentNullException("cells");
            Name = name;
            SchemaVersion = schemaVersion;
            SizeX = sizeX;
            SizeY = sizeY;
            SizeZ = sizeZ;
            _cells = cells;
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
                result.Add(new BlueprintCell(new CellPos(nx, c.Relative.Y, nz), c.MaterialKey));
            }

            // Re-normalize so the min corner is again (0,0,0).
            return Normalize(Name, SchemaVersion, result);
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
                    c.MaterialKey));
            }

            int sizeX = maxX - minX + 1;
            int sizeY = maxY - minY + 1;
            int sizeZ = maxZ - minZ + 1;
            return new Blueprint(name, schemaVersion, sizeX, sizeY, sizeZ, shifted);
        }
    }
}

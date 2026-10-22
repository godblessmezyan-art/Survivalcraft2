using System.Collections.Generic;

namespace BuilderAssistant.Core.Blueprints
{
    /// <summary>
    /// Composable scratchpad for generators. Cells are keyed by relative
    /// position so later writes overwrite earlier ones (last-write-wins), which
    /// is how openings (doors/windows) are carved: draw a solid wall, then
    /// punch air into it. All coordinates are RELATIVE - never world absolute.
    ///
    /// Since schema v2 a cell also carries an authored <see cref="BlockFamily"/> and
    /// data bits. The original cube-only API (Set/Fill/Walls/... with just a material
    /// key) keeps producing Cube/data-0 cells, so existing generators are unchanged;
    /// new oriented helpers (SetOriented) are used where a stair/slab/door/fence is
    /// emitted.
    /// </summary>
    internal sealed class BlueprintBuilder
    {
        private struct Content
        {
            public string MaterialKey;
            public BlockFamily Family;
            public int Data;
        }

        private readonly Dictionary<CellPos, Content> _cells = new Dictionary<CellPos, Content>();

        public int Count { get { return _cells.Count; } }

        public void Set(int x, int y, int z, string materialKey)
        {
            Set(new CellPos(x, y, z), materialKey);
        }

        public void Set(CellPos p, string materialKey)
        {
            _cells[p] = new Content { MaterialKey = materialKey, Family = BlockFamily.Cube, Data = 0 };
        }

        /// <summary>Place an oriented block with already-canonical authored data.
        /// Invalid/color/transient bits throw instead of being silently stripped.</summary>
        public void SetOriented(int x, int y, int z, string materialKey, BlockFamily family, int data)
        {
            // Construct once for the shared strict validation, then store the same values.
            var validated = new BlueprintCell(new CellPos(x, y, z), materialKey, family, data);
            _cells[new CellPos(x, y, z)] = new Content
            {
                MaterialKey = validated.MaterialKey,
                Family = validated.Family,
                Data = validated.Data,
            };
        }

        /// <summary>CellPos form of <see cref="SetOriented(int,int,int,string,BlockFamily,int)"/>,
        /// used by the copier which already holds a relative coordinate.</summary>
        public void SetOriented(CellPos p, string materialKey, BlockFamily family, int data)
        {
            SetOriented(p.X, p.Y, p.Z, materialKey, family, data);
        }

        /// <summary>Fill a solid box from (minX,minY,minZ) to (maxX,maxY,maxZ) inclusive.</summary>
        public void FillBox(int minX, int minY, int minZ, int maxX, int maxY, int maxZ, string materialKey)
        {
            for (int y = minY; y <= maxY; y++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        Set(x, y, z, materialKey);
                    }
                }
            }
        }

        /// <summary>Fill only the outer shell of a box (hollow interior).</summary>
        public void FillShell(int minX, int minY, int minZ, int maxX, int maxY, int maxZ, string materialKey)
        {
            for (int y = minY; y <= maxY; y++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        bool onEdge = x == minX || x == maxX || z == minZ || z == maxZ || y == minY || y == maxY;
                        if (onEdge)
                        {
                            Set(x, y, z, materialKey);
                        }
                    }
                }
            }
        }

        /// <summary>Fill the 4 vertical walls of a box footprint between two heights (inclusive).</summary>
        public void Walls(int minX, int minZ, int maxX, int maxZ, int fromY, int toY, string materialKey)
        {
            for (int y = fromY; y <= toY; y++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    Set(minX, y, z, materialKey);
                    Set(maxX, y, z, materialKey);
                }
                for (int x = minX; x <= maxX; x++)
                {
                    Set(x, y, minZ, materialKey);
                    Set(x, y, maxZ, materialKey);
                }
            }
        }

        /// <summary>Fill a single horizontal layer (one Y) across a rectangle.</summary>
        public void Layer(int minX, int minZ, int maxX, int maxZ, int y, string materialKey)
        {
            FillBox(minX, y, minZ, maxX, y, maxZ, materialKey);
        }

        /// <summary>Clear (make air) a rectangular opening, used to carve doors/windows.</summary>
        public void Carve(int minX, int minY, int minZ, int maxX, int maxY, int maxZ)
        {
            for (int y = minY; y <= maxY; y++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        _cells.Remove(new CellPos(x, y, z));
                    }
                }
            }
        }

        public Blueprint ToBlueprint(string name, int schemaVersion)
        {
            var list = new List<BlueprintCell>(_cells.Count);
            foreach (KeyValuePair<CellPos, Content> kv in _cells)
            {
                Content c = kv.Value;
                list.Add(new BlueprintCell(kv.Key, c.MaterialKey, c.Family, c.Data));
            }
            // Deterministic ordering so a blueprint rebuilds identically every time.
            list.Sort(delegate(BlueprintCell a, BlueprintCell b)
            {
                int byY = a.Relative.Y.CompareTo(b.Relative.Y);
                if (byY != 0) return byY;
                int byX = a.Relative.X.CompareTo(b.Relative.X);
                if (byX != 0) return byX;
                return a.Relative.Z.CompareTo(b.Relative.Z);
            });
            return Blueprint.Normalize(name, schemaVersion, list);
        }
    }
}

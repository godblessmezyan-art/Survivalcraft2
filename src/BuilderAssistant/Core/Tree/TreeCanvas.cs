using System.Collections.Generic;
using BuilderAssistant.Core.Blueprints;

namespace BuilderAssistant.Core.Tree
{
    /// <summary>
    /// A single painted voxel on the master treehouse canvas: a logical material key, the
    /// orientation family it belongs to, its authored data bits (already normalized to that
    /// family by <see cref="BlueprintCell"/> semantics), and which structural phase owns it.
    /// The phase tag is what lets <see cref="TreehouseBlueprintSet"/> slice the finished
    /// master grid back into per-phase, per-transaction-safe blueprints.
    /// </summary>
    internal readonly struct PaintedCell
    {
        public readonly string Key;
        public readonly BlockFamily Family;
        public readonly int Data;
        public readonly string Phase;

        public PaintedCell(string key, BlockFamily family, int data, string phase)
        {
            Key = key;
            // Data is normalized the same way a BlueprintCell normalizes it, so the canvas
            // and the eventual blueprint can never disagree about authored bits.
            Family = family;
            Data = BlockOrientation.Normalize(family, data);
            Phase = phase;
        }
    }

    /// <summary>
    /// The master coordinate scratchpad every tree generator paints into. Coordinates are
    /// MASTER-relative (0,0,0 is the chosen anchor of the whole build); this is not a
    /// blueprint yet. Painting is last-write-wins like <see cref="BlueprintBuilder"/>, but
    /// <see cref="SetIfEmpty"/> lets the house wrap around the trunk without cutting it: the
    /// trunk is drawn first and a plank floor simply skips cells the trunk already owns, so
    /// the tree visibly grows THROUGH each dwelling floor.
    ///
    /// It also maintains the set of log cells so the canopy generator (and the validator)
    /// can prove every leaf sits within Chebyshev distance 3 of a log - the exact survival
    /// condition <c>SubsystemWoodBlockBehavior.DecayLeavesIfNeeded</c> uses in x25.01.17.
    /// </summary>
    internal sealed class TreeCanvas
    {
        private readonly Dictionary<CellPos, PaintedCell> _cells = new Dictionary<CellPos, PaintedCell>();
        private readonly HashSet<CellPos> _logs = new HashSet<CellPos>();

        public int Count { get { return _cells.Count; } }
        public IEnumerable<KeyValuePair<CellPos, PaintedCell>> Cells { get { return _cells; } }

        public bool Contains(CellPos p) { return _cells.ContainsKey(p); }
        public bool HasCell(int x, int y, int z) { return _cells.ContainsKey(new CellPos(x, y, z)); }

        public bool TryGet(CellPos p, out PaintedCell cell) { return _cells.TryGetValue(p, out cell); }

        /// <summary>Paint (overwrite) a cell.</summary>
        public void Set(int x, int y, int z, string key, BlockFamily family, int data, string phase)
        {
            var p = new CellPos(x, y, z);
            // If we are replacing an existing log cell with a non-log, stop tracking it as a
            // log so the decay-safety query stays truthful.
            PaintedCell old;
            if (_cells.TryGetValue(p, out old) && IsLogFamily(old.Family) && !IsLogFamily(family))
            {
                _logs.Remove(p);
            }
            _cells[p] = new PaintedCell(key, family, data, phase);
            if (IsLogFamily(family)) _logs.Add(p);
        }

        /// <summary>Paint only if the cell is currently empty (air). Used so the dwelling
        /// floors wrap around the trunk / branches without carving them away.</summary>
        public void SetIfEmpty(int x, int y, int z, string key, BlockFamily family, int data, string phase)
        {
            var p = new CellPos(x, y, z);
            if (_cells.ContainsKey(p)) return;
            _cells[p] = new PaintedCell(key, family, data, phase);
            if (IsLogFamily(family)) _logs.Add(p);
        }

        /// <summary>
        /// Paint a STRUCTURAL dwelling/decoration cell: last-write-wins over any foliage or
        /// earlier structural cell (so walls, decks, stairs and the roof are authoritative and
        /// the canopy cannot leave holes in them), but a NO-OP on a trunk/branch log so the
        /// tree always grows THROUGH the house and every leaf's supporting log survives.
        /// Structural elements are never logs, so this never adds to the log set.
        /// </summary>
        public void SetStructural(int x, int y, int z, string key, BlockFamily family, int data, string phase)
        {
            var p = new CellPos(x, y, z);
            if (_logs.Contains(p)) return; // the tree is sacred
            _cells[p] = new PaintedCell(key, family, data, phase);
        }

        /// <summary>Remove a cell (make air). Used to carve doorways / stair openings after
        /// a solid wall or floor plate is drawn.</summary>
        public void Carve(int x, int y, int z)
        {
            var p = new CellPos(x, y, z);
            PaintedCell old;
            if (_cells.TryGetValue(p, out old) && IsLogFamily(old.Family)) _logs.Remove(p);
            _cells.Remove(p);
        }

        private static bool IsLogFamily(BlockFamily f)
        {
            return f == BlockFamily.Log;
        }

        public int LogCount { get { return _logs.Count; } }

        /// <summary>Whether a specific cell currently holds a log (trunk or branch). The
        /// dwelling generators query this so they NEVER overwrite the tree: every structural
        /// house/decoration cell is painted with <see cref="SetIfEmpty"/>, and doorway /
        /// stair / slab carving checks this first, so the trunk visibly grows through each
        /// floor and every leaf anchor survives (which keeps the decay-safety invariant true
        /// in the FINAL canvas, not just at paint time).</summary>
        public bool IsLog(int x, int y, int z) { return _logs.Contains(new CellPos(x, y, z)); }

        /// <summary>
        /// Whether the cell (x, y, z) lies within Chebyshev distance
        /// <paramref name="radius"/> of ANY painted log (a WoodBlock / trunk / branch cell).
        /// A leaf within radius 3 of a log never decays in the live game (see
        /// SubsystemWoodBlockBehavior), so the canopy uses radius 2 puffs around log anchors
        /// to keep a safety margin and stay provably stable.
        /// </summary>
        public bool IsNearLog(int x, int y, int z, int radius)
        {
            foreach (CellPos log in _logs)
            {
                int dx = Abs(log.X - x);
                if (dx > radius) continue;
                int dy = Abs(log.Y - y);
                if (dy > radius) continue;
                int dz = Abs(log.Z - z);
                if (dz > radius) continue;
                // Within radius on all three axes -> a log is nearby.
                return true;
            }
            return false;
        }

        private static int Abs(int v) { return v < 0 ? -v : v; }

        // --- Material keys the tree uses, kept here so generators do not hardcode strings ---
        public const string Log = BuildingMaterials.LogTrunk;      // OakWoodBlock (Log family)
        public const string Branch = BuildingMaterials.LogBranch;  // OakWoodBlock (Log family)
        public const string Leaf = BuildingMaterials.Leaves;        // SpruceLeavesBlock (Cube)
        public const string Plank = BuildingMaterials.WoodWall;     // PlanksBlock (Cube)
        public const string Trim = BuildingMaterials.WoodTrim;      // PlanksBlock (Cube)
        public const string Stone = BuildingMaterials.StoneWall;    // StoneBrickBlock (Cube)
        public const string Glass = BuildingMaterials.Glass;        // GlassBlock (Cube)
        public const string Stair = BuildingMaterials.InteriorStairs; // WoodenStairsBlock (Stairs)
        public const string Slab = BuildingMaterials.FlatDeck;      // StoneSlabBlock (Slab)
        public const string Rail = BuildingMaterials.GardenFence;   // WoodenFenceBlock (Fence)
        public const string Door = BuildingMaterials.EntryDoor;     // WoodenDoorBlock (Door)
    }
}

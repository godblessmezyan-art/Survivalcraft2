using System;
using System.Collections.Generic;
using BuilderAssistant.Core.Blueprints;
using Game;
using BuilderAssistant.Core;
using BuilderAssistant.Core.Safety;
using BuilderAssistant.Core.Transactions;

namespace BuilderAssistant.Features.WorldEdit
{
    /// <summary>
    /// The single adapter that translates the pure ITerrainGateway contract onto
    /// the real x25.01.17 SubsystemTerrain/Terrain. It is only ever constructed
    /// and used on the main thread (from SubsystemBlockBehavior.OnUse or the
    /// GuiUpdate tick), never from a background thread. ChangeCell is used for
    /// writes because it is the engine's own main-thread entry point that also
    /// handles lighting/geometry refresh and protection checks.
    /// </summary>
    internal sealed class GameTerrainGateway : ITerrainGateway, IConstructionTerrainGateway
    {
        private readonly SubsystemTerrain _subsystemTerrain;
        private readonly Terrain _terrain;

        // Cached BlockIndex -> Block map over BlocksManager.Blocks, built once on the
        // main thread. Used by IsCellSoftObstruction to decide whether a non-air cell
        // is a walk-through plant/snow (safe to clear) or solid/structural (protect).
        // Keyed by Block.BlockIndex (NOT the array slot) so the mapping is exact even
        // if the array order ever diverges from the numeric ids.
        private static Dictionary<int, Block> _idToBlock;

        public GameTerrainGateway(SubsystemTerrain subsystemTerrain)
        {
            _subsystemTerrain = subsystemTerrain;
            _terrain = subsystemTerrain.Terrain;
        }

        public CellEditability InspectCell(CellPos cell)
        {
            if (_terrain == null)
            {
                return new CellEditability(cell, CellEditabilityStatus.TerrainNull);
            }
            if (!WorldBounds.IsYValid(cell.Y))
            {
                return new CellEditability(cell, CellEditabilityStatus.InvalidY);
            }
            if (!_terrain.IsCellValid(cell.X, cell.Y, cell.Z))
            {
                // Engine says this coordinate is outside the valid terrain extent.
                // A wait will not fix it, so the planner treats it as a hard failure.
                return new CellEditability(cell, CellEditabilityStatus.InvalidCell);
            }

            TerrainChunk chunk = _terrain.GetChunkAtCell(cell.X, cell.Z);
            if (chunk == null)
            {
                // No chunk object for this column yet; it may still stream in.
                return new CellEditability(cell, CellEditabilityStatus.ChunkMissing);
            }

            int stateOrdinal = (int)chunk.State;
            bool loaded = chunk.IsLoaded;
            int chunkX = chunk.Coords.X;
            int chunkZ = chunk.Coords.Y;

            // v0.6.2: the engine's readiness signal is the chunk State pipeline, which
            // only reaches Valid after contents + light + geometry are generated, and an
            // allocated Cells array. TerrainChunk.IsLoaded is a separate disk/stream flag
            // that stays false for chunks the player is standing on and raycasting into,
            // so it is recorded for the log but NEVER gates editability — requiring it is
            // what made an on-screen, perfectly editable build site abort as "未加载".
            bool stateValid = chunk.State == TerrainChunkState.Valid;
            bool contentsReady = chunk.Cells != null;

            if (!stateValid)
            {
                return new CellEditability(
                    cell, CellEditabilityStatus.ChunkStateNotReady,
                    true, chunkX, chunkZ, stateOrdinal, loaded);
            }
            if (!contentsReady)
            {
                return new CellEditability(
                    cell, CellEditabilityStatus.ChunkContentsNotReady,
                    true, chunkX, chunkZ, stateOrdinal, loaded);
            }

            return CellEditability.Ok(cell);
        }

        public bool IsCellEditable(CellPos cell)
        {
            return InspectCell(cell).IsEditable;
        }

        public bool IsCellAir(CellPos cell)
        {
            int value;
            if (!TryGetCellValue(cell, out value))
            {
                return false;
            }
            return Terrain.ExtractContents(value) == 0;
        }

        /// <summary>
        /// Resolve a terrain contents value (a BlockIndex) to its real Block through a
        /// lazily built cache. Returns null when the id is unknown so callers fail safe.
        /// Only ever touched on the main thread, so no locking is required.
        /// </summary>
        private static Block BlockById(int blockIndex)
        {
            var map = _idToBlock;
            if (map == null)
            {
                map = new Dictionary<int, Block>();
                Block[] blocks = BlocksManager.Blocks;
                if (blocks != null)
                {
                    for (int i = 0; i < blocks.Length; i++)
                    {
                        Block b = blocks[i];
                        if (b != null && !map.ContainsKey(b.BlockIndex))
                        {
                            map[b.BlockIndex] = b;
                        }
                    }
                }
                _idToBlock = map;
            }
            Block found;
            return map.TryGetValue(blockIndex, out found) ? found : null;
        }

        /// <summary>
        /// A "soft obstruction" is a non-air cell a walk-through block occupies — grass,
        /// flowers, tall grass, dead bushes, snow layers and similar plants the engine
        /// flags IsCollidable == false. In clear-soft mode the builder may replace these.
        /// Air (contents 0) is handled elsewhere (returns false here so the "target not
        /// air" bookkeeping stays accurate); fluids are deliberately protected so a build
        /// never silently drains water; and every solid/collidable block — real terrain
        /// and any player structure — returns false so it is never wiped. An unresolvable
        /// id also returns false (fail safe: never clear what we cannot classify).
        /// </summary>
        public bool IsCellSoftObstruction(CellPos cell)
        {
            int value;
            if (!TryGetCellValue(cell, out value))
            {
                return false;
            }
            int contents = Terrain.ExtractContents(value);
            if (contents == 0)
            {
                return false; // air — not an obstruction at all
            }
            Block block = BlockById(contents);
            if (block == null)
            {
                return false; // unknown block -> protect, never auto-clear
            }
            if (block is FluidBlock)
            {
                return false; // water/lava -> never silently removed
            }
            return !block.IsCollidable; // walk-through plants/snow only
        }

        public bool TryGetCellValue(CellPos cell, out int value)
        {
            value = 0;
            if (!IsCellEditable(cell))
            {
                return false;
            }
            value = _terrain.GetCellValue(cell.X, cell.Y, cell.Z);
            return true;
        }

        // Quick-placement override permits only known inert terrain blocks. Fluids and
        // block entities in the edited cell or its direct neighbours are refused.
        internal bool IsQuickClearSafe(CellPos cell, int value, ISet<CellPos> plannedCells)
        {
            int id = Terrain.ExtractContents(value);
            Block target = BlockById(id);
            if (id != 0)
            {
                if (target == null) return false;
                string name = target.GetType().Name;
                if (!QuickSimpleTerrain(name)) return false;
            }
            var entities = _subsystemTerrain.Project.FindSubsystem<SubsystemBlockEntities>(false);
            if (entities == null) return false;
            CellPos[] offsets = { new CellPos(0,0,0), new CellPos(1,0,0), new CellPos(-1,0,0),
                new CellPos(0,1,0), new CellPos(0,-1,0), new CellPos(0,0,1), new CellPos(0,0,-1) };
            foreach (CellPos offset in offsets)
            {
                CellPos p = cell.Add(offset);
                int neighbour;
                if (!TryGetCellValue(p, out neighbour)) return false;
                int contents = Terrain.ExtractContents(neighbour);
                Block block = BlockById(contents);
                if ((contents != 0 && block == null) || block is FluidBlock ||
                    entities.GetBlockEntity(p.X, p.Y, p.Z) != null) return false;
                if (contents != 0 && (plannedCells == null || !plannedCells.Contains(p)) && !QuickSimpleTerrain(block.GetType().Name)) return false;
            }
            return true;
        }

        private static bool QuickSimpleTerrain(string name)
        {
            return name == "DirtBlock" || name == "StoneBlock" || name == "SandstoneBlock" ||
                name == "BasaltBlock" || name == "GraniteBlock" || name == "LimestoneBlock";
        }

        public bool SetCellValue(CellPos cell, int value)
        {
            if (!IsCellEditable(cell))
            {
                return false;
            }
            _subsystemTerrain.ChangeCell(cell.X, cell.Y, cell.Z, value);
            return PlacementMatches(_terrain.GetCellValue(cell.X, cell.Y, cell.Z), value);
        }

        public bool SupportsAttachment(int value, BlockFamily family, int face)
        {
            Block block = BlockById(Terrain.ExtractContents(value));
            if (block == null) return false;
            return family == BlockFamily.Ladder
                ? !block.IsFaceTransparent(_subsystemTerrain, face, value)
                : family == BlockFamily.Torch && block.IsCollidable;
        }

        private bool PlacementMatches(int current, int expected)
        {
            if (Terrain.ExtractContents(current) != Terrain.ExtractContents(expected)) return false;
            Block block = BlockById(Terrain.ExtractContents(expected));
            int actualData = Terrain.ExtractData(current), expectedData = Terrain.ExtractData(expected);
            // Native OnBlockAdded may derive geometry synchronously. Paint must stay exact.
            if (block is FenceBlock) return (actualData & ~15) == (expectedData & ~15);
            if (block is StairsBlock && (expectedData & 24) == 0)
                return (actualData & ~27) == (expectedData & ~27) && (actualData & 24) != 24;
            return ValuesEquivalent(current, expected);
        }

        public bool ValuesEquivalent(int current, int expected)
        {
            return Terrain.ReplaceLight(current, 0) == Terrain.ReplaceLight(expected, 0);
        }

        public bool ApplyWithJournal(CellPos cell, int value, out List<BlockUndoRecord> journal, out string error)
        {
            journal = new List<BlockUndoRecord>();
            error = null;
            int old;
            if (!TryGetCellValue(cell, out old)) { error = "无法读取原值"; return false; }
            var before = new Dictionary<CellPos, int> { { cell, old } };
            // Native door addition/removal synchronously edits a companion. Capture it BEFORE ChangeCell.
            Block next = BlockById(Terrain.ExtractContents(value));
            Block previous = BlockById(Terrain.ExtractContents(old));
            if (next is DoorBlock && Terrain.ExtractContents(old) != 0)
            { error = "门事务目前只允许空气落点，以保证关联半格可完整回滚"; return false; }
            if (previous is DoorBlock)
            { error = "覆盖已有门需要跨电路事务，目前不支持"; return false; }
            if (next is DoorBlock)
            {
                for (int dy = -1; dy <= 1; dy += 2)
                {
                    CellPos adjacent = cell.Add(new CellPos(0, dy, 0));
                    int adjacentValue;
                    if (!TryGetCellValue(adjacent, out adjacentValue)) { error = "门关联格未就绪"; return false; }
                    if (dy == -1)
                    {
                        Block support = BlockById(Terrain.ExtractContents(adjacentValue));
                        if (support == null || support.IsTransparent || support is DoorBlock)
                        { error = "门下方需要不透明的非门支撑"; return false; }
                    }
                    if (BlockById(Terrain.ExtractContents(adjacentValue)) is DoorBlock || (next is DoorBlock && dy == 1))
                    {
                        if (next is DoorBlock && dy == 1 && Terrain.ExtractContents(adjacentValue) != 0)
                        { error = "门上半格已被占用"; return false; }
                        before[adjacent] = adjacentValue;
                    }
                }
            }
            bool success = false;
            try { success = SetCellValue(cell, value); }
            catch (Exception ex) { error = "ChangeCell 异常: " + ex.Message; }
            finally
            {
                // ChangeCell invalidates chunk meshing. Raw reads here are synchronous, bounded,
                // main-thread reads from the same already-validated allocated chunk, not a new write gate.
                foreach (KeyValuePair<CellPos, int> snapshot in before)
                {
                    int actual = _terrain.GetCellValue(snapshot.Key.X, snapshot.Key.Y, snapshot.Key.Z);
                    if (!ValuesEquivalent(actual, snapshot.Value))
                        journal.Add(new BlockUndoRecord(snapshot.Key, snapshot.Value, actual));
                }
            }
            if (!success && error == null) error = "写入被保护或状态复核失败";
            return success;
        }

        /// <summary>
        /// Map a terrain contents value (a BlockIndex) to its vanilla block CLR type
        /// name, or null when the id is unknown. The copier uses this to turn a live
        /// cell into a concrete material key it can (or cannot) reproduce. Main-thread
        /// only, like the rest of the gateway.
        /// </summary>
        public static string TypeNameForContents(int contents)
        {
            if (contents == 0) return null;
            Block block = BlockById(contents);
            if (block == null) return null;
            return block.GetType().Name;
        }

        /// <summary>
        /// A short human classification of a non-air contents value for the copy
        /// report: "fluid" for water/lava, "植被/雪" for a walk-through (non-collidable)
        /// block, otherwise the block type name. Never assumes solidity.
        /// </summary>
        public static string DescribeContents(int contents)
        {
            if (contents == 0) return "空气";
            Block block = BlockById(contents);
            if (block == null) return "未知方块(id=" + contents + ")";
            if (block is FluidBlock) return "流体";
            if (!block.IsCollidable) return "植被/雪";
            return block.GetType().Name;
        }
    }
}

using System.Collections.Generic;
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
    internal sealed class GameTerrainGateway : ITerrainGateway
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

        public bool SetCellValue(CellPos cell, int value)
        {
            if (!IsCellEditable(cell))
            {
                return false;
            }
            _subsystemTerrain.ChangeCell(cell.X, cell.Y, cell.Z, value);
            return true;
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

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
            if (chunk.State != TerrainChunkState.Valid)
            {
                return new CellEditability(
                    cell, CellEditabilityStatus.ChunkStateNotReady,
                    true, CellEditability.UnknownCoord, CellEditability.UnknownCoord, stateOrdinal, loaded);
            }
            if (!loaded)
            {
                return new CellEditability(
                    cell, CellEditabilityStatus.ChunkNotLoaded,
                    true, CellEditability.UnknownCoord, CellEditability.UnknownCoord, stateOrdinal, loaded);
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
    }
}

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

        public bool IsCellEditable(CellPos cell)
        {
            if (_terrain == null)
            {
                return false;
            }
            if (!WorldBounds.IsYValid(cell.Y))
            {
                return false;
            }
            if (!_terrain.IsCellValid(cell.X, cell.Y, cell.Z))
            {
                return false;
            }

            TerrainChunk chunk = _terrain.GetChunkAtCell(cell.X, cell.Z);
            if (chunk == null)
            {
                return false;
            }
            return chunk.State == TerrainChunkState.Valid && chunk.IsLoaded;
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

using Engine;
using Game;
using Game.NetWork;

namespace SCIEPort.Features.Pyramid
{
    /// <summary>Authority-side interaction for switchable tomb decor (sarcophagus lid, brazier flame).</summary>
    public sealed class SubsystemPyramidDecor : SubsystemBlockBehavior
    {
        /// <inheritdoc/> PyramidDecor.csv supplies the native named binding.
        public override int[] HandledBlocks => new int[0];
        /// <inheritdoc/>
        public override bool OnInteract(TerrainRaycastResult hit, ComponentMiner miner)
        {
            if (miner?.ComponentPlayer == null || CommonLib.WorkType == WorkType.Client) return false;
            if (Terrain.ExtractContents(hit.Value) != PyramidDecorRules.Index) return false;
            int stored = Terrain.ExtractData(hit.Value);
            if (!PyramidDecorRules.IsSwitchable(PyramidDecorRules.Kind(stored))) return false;
            Point3 point = hit.CellFace.Point;
            int value = SubsystemTerrain.Terrain.GetCellValue(point.X, point.Y, point.Z);
            int data = Terrain.ExtractData(value) ^ PyramidDecorRules.StateBit;
            SubsystemTerrain.ChangeCell(point.X, point.Y, point.Z, Terrain.ReplaceData(value, data));
            return true;
        }
    }
}

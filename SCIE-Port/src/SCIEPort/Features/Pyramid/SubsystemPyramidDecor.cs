using Engine;
using Game;
using Game.NetWork;
using SCIEPort.Features.Shrines;

namespace SCIEPort.Features.Pyramid
{
    /// <summary>Authority-side interaction for switchable tomb decor (sarcophagus lid, brazier flame).</summary>
    public sealed class SubsystemPyramidDecor : SubsystemBlockBehavior
    {
        private bool removingMonument;

        /// <inheritdoc/> PyramidDecor.csv supplies the native named binding.
        public override int[] HandledBlocks => new int[0];
        /// <inheritdoc/>
        public override bool OnInteract(TerrainRaycastResult hit, ComponentMiner miner)
        {
            ComponentPlayer player = miner?.ComponentPlayer;
            if (player == null) return false;
            if (Terrain.ExtractContents(hit.Value) != PyramidDecorRules.Index) return false;
            int stored = Terrain.ExtractData(hit.Value);
            Point3 point = hit.CellFace.Point;
            if (PyramidMonuments.IsMonument(stored))
            {
                if (!PyramidMonuments.Switchable(PyramidDecorRules.Kind(stored))) return false;
                if (CommonLib.WorkType == WorkType.Client)
                {
                    if (player == CommonLib.MainPlayer)
                        CommonLib.Net?.QueuePackage(new PyramidPackage { Point = point, To = CommonLib.Net.Server });
                }
                else Project.FindSubsystem<SubsystemPyramid>(false)?.Enqueue(player, point);
                return true;
            }
            if (CommonLib.WorkType == WorkType.Client) return false;
            if (!PyramidDecorRules.IsSwitchable(PyramidDecorRules.Kind(stored))) return false;
            int value = SubsystemTerrain.Terrain.GetCellValue(point.X, point.Y, point.Z);
            int data = Terrain.ExtractData(value) ^ PyramidDecorRules.StateBit;
            SubsystemTerrain.ChangeCell(point.X, point.Y, point.Z, Terrain.ReplaceData(value, data));
            return true;
        }

        internal void UseMonument(ComponentPlayer player, Point3 point, int value)
        {
            int data = Terrain.ExtractData(value);
            if (!PyramidMonuments.IsMonument(data)) return;
            int kind = PyramidDecorRules.Kind(data), part = PyramidMonuments.Part(data);
            if (!PyramidMonuments.Switchable(kind)) return;
            var bounds = PyramidMonuments.CollisionBoxes(kind, part)[0];
            if (!ShrineWorld.CanReach(SubsystemTerrain, player, point, (bounds.Min + bounds.Max) * .5f)) return;
            Point3 root = point - PyramidMonuments.Offset(kind, part);
            if (!ShrineWorld.AllowsChange(root)) return;
            for (int index = 0; index < PyramidMonuments.Size(kind); index++)
                if (!Matches(root + PyramidMonuments.Offset(kind, index), kind, index)) return;
            int rootValue = SubsystemTerrain.Terrain.GetCellValue(root.X, root.Y, root.Z);
            int toggled = Terrain.ExtractData(rootValue) ^ PyramidDecorRules.StateBit;
            SubsystemTerrain.ChangeCell(root.X, root.Y, root.Z, Terrain.ReplaceData(rootValue, toggled));
        }

        /// <inheritdoc/>
        public override void OnBlockRemoved(int value, int newValue, int x, int y, int z)
        {
            if (CommonLib.WorkType == WorkType.Client || removingMonument) return;
            int data = Terrain.ExtractData(value);
            if (!PyramidMonuments.IsMonument(data)) return;
            int kind = PyramidDecorRules.Kind(data), removedPart = PyramidMonuments.Part(data);
            Point3 root = new Point3(x, y, z) - PyramidMonuments.Offset(kind, removedPart);
            removingMonument = true;
            try
            {
                for (int part = 0; part < PyramidMonuments.Size(kind); part++)
                {
                    Point3 point = root + PyramidMonuments.Offset(kind, part);
                    if (part != removedPart && Matches(point, kind, part))
                        SubsystemTerrain.ChangeCell(point.X, point.Y, point.Z, 0);
                }
            }
            finally { removingMonument = false; }
        }

        private bool Matches(Point3 point, int kind, int part)
        {
            int value = SubsystemTerrain.Terrain.GetCellValue(point.X, point.Y, point.Z);
            return Terrain.ExtractContents(value) == PyramidDecorRules.Index &&
                (Terrain.ExtractData(value) & ~PyramidDecorRules.StateBit) == PyramidMonuments.Data(kind, part);
        }
    }
}

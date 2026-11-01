using Game;
using Game.NetWork;

namespace SCIEPort.Features.Suspension
{
    /// <summary>One real item, using native inventory persistence and SCNET transfer/sync packets.</summary>
    public sealed class ComponentSuspendedItem : ComponentChest
    {
        /// <inheritdoc/>
        public override int GetSlotCapacity(int slotIndex, int value)
        {
            int id = Terrain.ExtractContents(value);
            var block = BlocksManager.Blocks[id];
            return slotIndex == 0 && id != 0 && block != null && block.BlockIndex == id && block.GetMaxStacking(value) > 0 ? 1 : 0;
        }
        /// <inheritdoc/>
        public override int GetSlotProcessCapacity(int slotIndex, int value) => 0;
        /// <inheritdoc/>
        public override bool AddNetSlotItems(int slotIndex, int value, int count)
            => CommonLib.WorkType != WorkType.Client && count == 1 && base.AddNetSlotItems(slotIndex, value, count);
        /// <inheritdoc/>
        public override int RemoveNetSlotItems(int slotIndex, int count)
            => CommonLib.WorkType != WorkType.Client && count > 0 ? base.RemoveNetSlotItems(slotIndex, count) : 0;
    }
}

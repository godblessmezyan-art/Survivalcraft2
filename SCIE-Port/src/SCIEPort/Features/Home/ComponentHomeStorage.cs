using Game;
using Game.NetWork;

namespace SCIEPort.Features.Home
{
    /// <summary>Eight deposit slots; the published sixteen-slot save layout remains intact.</summary>
    public sealed class ComponentHomeStorage : ComponentChest
    {
        // Do not shrink m_slots, SlotsCount, the XDB SlotsCount, or the native Slots/SlotN keys.
        // Native loading, saving, synchronization and DropAllItems must still see legacy items.
        /// <inheritdoc/>
        public override int GetSlotCapacity(int slotIndex, int value)
            => slotIndex >= 0 && slotIndex < HomeRules.UsableSlots && value != 0
                ? base.GetSlotCapacity(slotIndex, value) : 0;

        /// <inheritdoc/>
        public override bool AddNetSlotItems(int slotIndex, int value, int count)
        {
            // Native HandleMoveItem/HandleDragDrop run these same checks on the host.
            // Subtraction avoids count overflow before the base implementation adds it.
            int capacity = GetSlotCapacity(slotIndex, value);
            return count > 0 && count <= capacity && GetSlotCount(slotIndex) <= capacity - count
                && base.AddNetSlotItems(slotIndex, value, count);
        }

        /// <inheritdoc/>
        public override int RemoveNetSlotItems(int slotIndex, int count)
            => count > 0 ? base.RemoveNetSlotItems(slotIndex, count) : 0;

        /// <inheritdoc/>
        public override void SetSlotValue(int slotIndex, object obj)
        {
            var incoming = obj as ComponentInventoryBase.Slot;
            if (incoming == null || slotIndex < 0 || slotIndex >= SlotsCount || incoming.Count < 0) return;
            // Server snapshots are replication, not deposits: a client must be able to
            // display all old slots even though their deposit capacity is now zero.
            if (CommonLib.WorkType == WorkType.Client) { base.SetSlotValue(slotIndex, obj); return; }
            if (slotIndex < HomeRules.UsableSlots)
            {
                if (incoming.Count == 0 || incoming.Count <= GetSlotCapacity(slotIndex, incoming.Value))
                    base.SetSlotValue(slotIndex, obj);
            }
            else if (incoming.Count == 0 || incoming.Value == GetSlotValue(slotIndex) && incoming.Count <= GetSlotCount(slotIndex))
                base.SetSlotValue(slotIndex, obj);
        }

        /// <inheritdoc/>
        public override int GetSlotProcessCapacity(int slotIndex, int value)
            => slotIndex >= 0 && slotIndex < HomeRules.UsableSlots ? base.GetSlotProcessCapacity(slotIndex, value) : 0;

        /// <inheritdoc/>
        public override void ProcessSlotItems(IInventory sourceInventory, int sourceSlotIndex, int slotIndex,
            int value, int count, int processCount, out int processedValue, out int processedCount)
        {
            if (slotIndex >= 0 && slotIndex < HomeRules.UsableSlots)
                base.ProcessSlotItems(sourceInventory, sourceSlotIndex, slotIndex, value, count, processCount, out processedValue, out processedCount);
            else { processedValue = value; processedCount = processCount; }
        }
    }
}

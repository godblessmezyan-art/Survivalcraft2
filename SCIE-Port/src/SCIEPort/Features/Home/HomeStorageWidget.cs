using Engine;
using Game;

namespace SCIEPort.Features.Home
{
    /// <summary>Native touch transfers with a visible, take-only legacy section when occupied.</summary>
    internal sealed class HomeStorageWidget : ChestWidget
    {
        private readonly ComponentHomeStorage storage;
        private readonly InventorySlotWidget[] slots;
        private readonly LabelWidget title;
        private readonly string name;
        private int shownRows = -1;

        internal HomeStorageWidget(IInventory inventory, ComponentHomeStorage storage) : base(inventory, storage)
        {
            this.storage = storage;
            m_chestGrid.Children.Clear(); m_chestGrid.ColumnsCount = 4;
            slots = new InventorySlotWidget[storage.SlotsCount];
            m_chestGrid.RowsCount = (slots.Length + 3) / 4;
            for (int i = 0; i < slots.Length; i++)
            {
                InventorySlotWidget slot = i < HomeRules.UsableSlots
                    ? new InventorySlotWidget() : new HomeLegacySlotWidget(inventory);
                slot.CenterColor = i < HomeRules.UsableSlots ? new Color(48, 57, 67) : new Color(35, 42, 50);
                slot.BevelColor = i < HomeRules.UsableSlots ? new Color(164, 177, 189) : new Color(84, 108, 128);
                slot.AssignInventorySlot(storage, i); slots[i] = slot;
                m_chestGrid.Children.Add(slot); m_chestGrid.SetWidgetCell(slot, new Point2(i % 4, i / 4));
            }
            var block = storage.Entity.FindComponent<ComponentBlockEntity>(true);
            var terrain = storage.Project.FindSubsystem<SubsystemTerrain>(true); Point3 p = block.Coordinates;
            int value = terrain.Terrain.GetCellValue(p.X, p.Y, p.Z);
            name = BlocksManager.Blocks[Terrain.ExtractContents(value)].GetDisplayName(terrain, value);
            title = FindTitle(this);
            if (title != null) title.Color = new Color(229, 234, 238);
            RefreshLegacySection();
        }
        private void RefreshLegacySection()
        {
            bool legacy = false;
            for (int i = HomeRules.UsableSlots; i < slots.Length; i++)
                if (storage.GetSlotCount(i) > 0) { legacy = true; break; }
            int rows = ((legacy ? slots.Length : HomeRules.UsableSlots) + 3) / 4;
            if (rows != shownRows)
            {
                // Keep widgets stable and keep even hidden cells inside the grid bounds.
                if (rows > m_chestGrid.RowsCount) m_chestGrid.RowsCount = rows;
                for (int i = HomeRules.UsableSlots; i < slots.Length; i++)
                {
                    slots[i].IsVisible = legacy;
                    m_chestGrid.SetWidgetCell(slots[i], new Point2(i % 4, legacy ? i / 4 : 0));
                }
                m_chestGrid.RowsCount = rows; shownRows = rows;
            }
            if (title != null) title.Text = legacy ? name + " · 旧物取出" : name;
        }
        /// <inheritdoc/>
        public override void Update() { base.Update(); if (storage.IsAddedToProject) RefreshLegacySection(); }
        private static LabelWidget FindTitle(Widget widget)
        {
            var label = widget as LabelWidget; if (label != null) return label;
            var parent = widget as ContainerWidget;
            if (parent != null) foreach (Widget child in parent.Children) { var found = FindTitle(child); if (found != null) return found; }
            return null;
        }
    }

    // Legacy slots use a take-only tap rather than native drag processing. The native
    // processing path can consume the source before trying to return a transformed
    // item to it, which is unsafe for a source with zero deposit capacity.
    internal sealed class HomeLegacySlotWidget : InventorySlotWidget, IDragTargetWidget
    {
        private readonly IInventory destination;
        internal HomeLegacySlotWidget(IInventory destination) { this.destination = destination; }
        /// <inheritdoc/>
        public override void Update()
        {
            // Do not call base.Update: no split source, drag source, edit or processing.
            if (m_inventory == null || !Input.Click.HasValue) return;
            var click = Input.Click.Value;
            if (HitTestGlobal(click.Start) != this || HitTestGlobal(click.End) != this) return;
            int value = m_inventory.GetSlotValue(m_slotIndex), count = m_inventory.GetSlotCount(m_slotIndex);
            if (count <= 0) return;
            int target = ComponentInventoryBase.FindAcquireSlotForItem(destination, value);
            if (target >= 0) HandleMoveItem(m_inventory, m_slotIndex, destination, target, count);
            Input.Clear();
        }
        // DragDrop is not virtual in the fixed target. Re-map its interface to reject
        // incoming drops without changing the native packet or any other container.
        public new void DragDrop(Widget dragWidget, object data) { }
    }
}

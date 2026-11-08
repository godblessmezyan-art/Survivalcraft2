using Engine;
using Game;

namespace SCIEPort.Features.Suspension
{
    // Keep the native modal, player inventory, per-finger drag/drop and SCNET slot transfers.
    internal sealed class SuspendedItemWidget : ChestWidget
    {
        internal SuspendedItemWidget(IInventory inventory, ComponentSuspendedItem item) : base(inventory, item)
        {
            m_chestGrid.Children.Clear();
            m_chestGrid.ColumnsCount = 1; m_chestGrid.RowsCount = 1;
            m_chestGrid.HorizontalAlignment = WidgetAlignment.Center;
            var slot = new InventorySlotWidget { CenterColor = new Color(48, 57, 67), BevelColor = new Color(164, 177, 189) }; slot.AssignInventorySlot(item, 0);
            m_chestGrid.Children.Add(slot); m_chestGrid.SetWidgetCell(slot, Point2.Zero);
            Rename(this);
        }
        private static bool Rename(Widget widget)
        {
            var label = widget as LabelWidget;
            // The fixed target's first label is ChestWidget:1, independently of UI language.
            if (label != null) { label.Text = "悬浮陈列器 · 存放一件物品"; return true; }
            var parent = widget as ContainerWidget;
            if (parent != null) foreach (var child in parent.Children) if (Rename(child)) return true;
            return false;
        }
    }
}

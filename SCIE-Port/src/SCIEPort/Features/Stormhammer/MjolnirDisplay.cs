using System;
using System.Collections.Generic;
using System.Linq;
using Engine;
using Engine.Graphics;
using Game;
using GameEntitySystem;

namespace SCIEPort.Features.Stormhammer
{
    /// <summary>
    /// Paints the inventory and hotbar slots that hold the awakened hammer with the
    /// artifact skin: a blue-purple background and a pulsing violet bevel.
    ///
    /// Why it is driven from a subsystem instead of a widget hook: the game exposes no
    /// "slot decorated" callback, and InventorySlotWidget.Update only ever sets
    /// m_rectangleWidget.IsVisible, never CenterColor/BevelColor after construction
    /// (verified in the x25.01.17 decompile), so painting those two properties from
    /// outside is safe and cannot fight the native code. The awakened hammer is
    /// deliberately the only decorated block; the fake hammer keeps the plain slot.
    /// </summary>
    public static class MjolnirDisplay
    {
        /// <summary>How often the slot scan runs. The pulse is evaluated on these ticks.</summary>
        internal const float RefreshInterval = 0.12f;

        /// <summary>Block this decorator reacts to. Only the true Mjolnir, never the fake one.</summary>
        private static readonly int s_blockIndex = StormhammerAwakenedBlock.Index;

        private static readonly Dictionary<InventorySlotWidget, SlotSkin> s_skins =
            new Dictionary<InventorySlotWidget, SlotSkin>();

        private static double s_nextScan;

        private sealed class SlotSkin
        {
            public Color Center;
            public Color Bevel;
        }

        /// <summary>
        /// Refreshes every visible slot of every player. Cheap enough for 8 Hz: the walk is
        /// bounded by the widget count of one GUI, and colored slots are one or two.
        /// </summary>
        public static void Update(Project project, double time)
        {
            if (project == null || time < s_nextScan)
            {
                return;
            }

            s_nextScan = time + RefreshInterval;
            SubsystemPlayers players = project.FindSubsystem<SubsystemPlayers>(false);
            if (players == null)
            {
                return;
            }

            // One breathing cycle every ~1.5 s: background deepens, bevel brightens.
            float pulse = 0.5f + 0.5f * MathUtils.Sin((float)(time % 9.42) * 4.2f);

            foreach (ComponentPlayer player in players.ComponentPlayers)
            {
                ContainerWidget root = player?.GuiWidget;
                if (root == null)
                {
                    continue;
                }

                foreach (InventorySlotWidget slot in root.AllChildren.OfType<InventorySlotWidget>())
                {
                    if (slot.RootWidget == null)
                    {
                        // Widget no longer in a tree (inventory closed): restore and forget it.
                        Restore(slot);
                        continue;
                    }

                    int value = slot.m_inventory != null ? slot.m_inventory.GetSlotValue(slot.m_slotIndex) : 0;
                    bool decorated = Terrain.ExtractContents(value) == s_blockIndex;
                    if (!decorated)
                    {
                        Restore(slot);
                        continue;
                    }

                    if (!s_skins.TryGetValue(slot, out SlotSkin skin))
                    {
                        // Save whatever the slot currently shows so we can hand it back untouched.
                        skin = new SlotSkin { Center = slot.CenterColor, Bevel = slot.BevelColor };
                        s_skins[slot] = skin;
                    }

                    slot.CenterColor = new Color(
                        (byte)(52 + 34 * pulse),
                        (byte)(40 + 24 * pulse),
                        (byte)(108 + 60 * pulse),
                        (byte)(165 + 45 * pulse));
                    slot.BevelColor = new Color(
                        (byte)(140 + 75 * pulse),
                        (byte)(110 + 80 * pulse),
                        (byte)(235 + 20 * pulse),
                        (byte)(200 + 55 * pulse));
                }
            }
        }

        /// <summary>Called when the subsystem dies so a reloaded world starts clean.</summary>
        public static void ResetAll()
        {
            foreach (KeyValuePair<InventorySlotWidget, SlotSkin> pair in s_skins)
            {
                InventorySlotWidget slot = pair.Key;
                try
                {
                    slot.CenterColor = pair.Value.Center;
                    slot.BevelColor = pair.Value.Bevel;
                }
                catch (ObjectDisposedException)
                {
                    // Widget already torn down with the old GUI; nothing to restore.
                }
            }

            s_skins.Clear();
            s_nextScan = 0.0;
        }

        private static void Restore(InventorySlotWidget slot)
        {
            SlotSkin skin;
            if (!s_skins.TryGetValue(slot, out skin))
            {
                return;
            }

            s_skins.Remove(slot);
            slot.CenterColor = skin.Center;
            slot.BevelColor = skin.Bevel;
        }
    }
}

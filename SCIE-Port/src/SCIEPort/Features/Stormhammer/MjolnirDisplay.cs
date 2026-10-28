using System;
using System.Collections.Generic;
using System.Linq;
using Engine;
using Game;
using GameEntitySystem;
using SCIEPort.Features.Inventory;

namespace SCIEPort.Features.Stormhammer
{
    /// <summary>Shared artifact/Four Saints slot decorator. Historical public entry point is preserved.</summary>
    public static class MjolnirDisplay
    {
        internal const float RefreshInterval = 0.12f;
        private sealed class SlotSkin { internal Color Center, Bevel; }
        private static readonly Dictionary<InventorySlotWidget,SlotSkin> s_skins = new Dictionary<InventorySlotWidget,SlotSkin>();
        private static readonly HashSet<InventorySlotWidget> s_seen = new HashSet<InventorySlotWidget>();
        private static readonly List<InventorySlotWidget> s_removed = new List<InventorySlotWidget>();
        private static Project s_project;
        private static double s_nextScan;

        /// <summary>Paints native backgrounds without adding widgets, intercepting touches or changing item values.</summary>
        public static void Update(Project project,double time)
        {
            if(project==null)return;
            if(!ReferenceEquals(s_project,project)){ResetAll();s_project=project;}
            if(time<s_nextScan)return;
            s_nextScan=time+RefreshInterval;
            SubsystemPlayers players=project.FindSubsystem<SubsystemPlayers>(false);
            if(players==null)return;
            float pulse=0.5f+0.5f*MathUtils.Sin((float)(time%9.42)*4.2f);
            s_seen.Clear();
            foreach(ComponentPlayer player in players.ComponentPlayers)
            {
                ContainerWidget root=player?.GuiWidget;
                if(root==null)continue;
                foreach(InventorySlotWidget slot in root.AllChildren.OfType<InventorySlotWidget>())
                {
                    if(!slot.IsVisibleGlobal){Restore(slot);continue;}
                    int value=slot.m_inventory!=null?slot.m_inventory.GetSlotValue(slot.m_slotIndex):0;
                    int contents=Terrain.ExtractContents(value);
                    int clothingIndex=contents==203?Game.ClothingBlock.GetClothingIndex(Terrain.ExtractData(value)):-1;
                    var theme=PortInventoryRules.Select(contents,clothingIndex,BlocksManager.Blocks[contents]?.DefaultCategory);
                    if(theme==PortInventoryRules.Theme.None){Restore(slot);continue;}
                    s_seen.Add(slot);
                    SlotSkin original;
                    if(!s_skins.TryGetValue(slot,out original))
                    {
                        original=new SlotSkin {Center=slot.CenterColor,Bevel=slot.BevelColor};s_skins.Add(slot,original);
                    }
                    var colors=PortInventoryRules.Colors(theme,pulse);
                    slot.CenterColor=ToColor(colors.Center);slot.BevelColor=ToColor(colors.Border);
                }
            }
            // Release widgets from closed panels as well as from exited worlds.
            s_removed.Clear();
            foreach(var slot in s_skins.Keys)if(!s_seen.Contains(slot))s_removed.Add(slot);
            foreach(var slot in s_removed)Restore(slot);
            s_seen.Clear();s_removed.Clear();
        }
        private static Color ToColor(PortInventoryRules.Rgba c)=>new Color(c.R,c.G,c.B,c.A);
        /// <summary>Restores exact original colors, including native slots and removed equipment.</summary>
        public static void ResetAll()
        {
            foreach(var pair in s_skins)RestoreColors(pair.Key,pair.Value);
            s_skins.Clear();s_seen.Clear();s_removed.Clear();s_nextScan=0;s_project=null;
        }
        private static void Restore(InventorySlotWidget slot)
        {
            SlotSkin original;
            if(!s_skins.TryGetValue(slot,out original))return;
            RestoreColors(slot,original);s_skins.Remove(slot);
        }
        private static void RestoreColors(InventorySlotWidget slot,SlotSkin original)
        {
            try{slot.CenterColor=original.Center;slot.BevelColor=original.Bevel;}
            catch(ObjectDisposedException){ /* Native panel already destroyed this slot. */ }
        }
    }
}

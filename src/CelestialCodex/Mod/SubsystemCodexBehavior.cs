using System.Collections.Generic;
using Engine;
using Game;
using CelestialCodex.UI;

namespace CelestialCodex.Mod
{
    public sealed class SubsystemCelestialCodexBehavior : SubsystemBlockBehavior
    {
        private readonly List<CodexDialog> dialogs = new List<CodexDialog>();
        public override int[] HandledBlocks { get { return new[] { CelestialCodexBlock.Index }; } }
        public override bool OnUse(Ray3 ray, ComponentMiner miner)
        {
            if (Terrain.ExtractContents(miner.ActiveBlockValue) != CelestialCodexBlock.Index) return false;
            ComponentPlayer player = miner.ComponentPlayer;
            if (player == null || player.PlayerData == null || !player.PlayerData.IsMainPlayer || player.GameWidget == null || player.GameWidget.GuiWidget == null) return true;
            foreach (CodexDialog existing in dialogs) if (existing.Player == player && !existing.Closed) return true;
            dialogs.RemoveAll(item => item.Closed);
            CodexDialog created = null;
            try
            {
                created = new CodexDialog(player); dialogs.Add(created);
                DialogsManager.ShowDialog(player.GameWidget.GuiWidget, created);
            }
            catch (System.Exception e)
            {
                if (created != null) created.Close();
                Log.Warning("[CelestialCodex] Reader unavailable: " + e.GetType().Name);
                if (player.ComponentGui != null) player.ComponentGui.DisplaySmallMessage("书典暂不可用", Color.White, false, false);
            }
            return true;
        }
        public override void Dispose()
        {
            foreach (CodexDialog dialog in dialogs) dialog.Close();
            dialogs.Clear(); base.Dispose();
        }
    }
}

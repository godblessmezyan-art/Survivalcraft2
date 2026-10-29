using BuilderAssistant.Core;
using BuilderAssistant.Core.Blueprints;
using BuilderAssistant.Core.Workbench;
using Engine;
using Game;

namespace BuilderAssistant.Features.WorldEdit
{
    internal static class WorkbenchSession
    {
        private static BuildingSelectionService service;
        private static BuildingWorkbench workbench;
        private static ComponentMiner owner;
        private static object world;
        private static Dialog dialog;
        private static bool selecting;

        public static void Reset()
        {
            if (dialog != null) DialogsManager.HideDialog(dialog);
            dialog = null; owner = null; world = null; selecting = false;
            service = null; workbench = null;
        }
        public static void Tick(ComponentGui gui)
        {
            if (owner != null && gui != null && gui.m_componentPlayer != null &&
                !object.ReferenceEquals(world, gui.m_componentPlayer.Project)) Reset();
            if (dialog != null && dialog.ParentWidget != null && !DialogsManager.Dialogs.Contains(dialog))
            { dialog = null; selecting = false; }
        }
        private static void CloseDialog()
        {
            if (dialog != null) DialogsManager.HideDialog(dialog);
            dialog = null;
        }
        public static bool OnWorkbenchTool(ComponentMiner miner, Ray3 ray)
        {
            if (miner == null || miner.ComponentPlayer == null || miner.ComponentPlayer.GameWidget == null) return true;
            ComponentGui gui = miner.ComponentPlayer.ComponentGui;
            if (!object.ReferenceEquals(world, miner.Project)) Reset();
            if (WorldEditRuntime.Palette == null || WorldEditRuntime.IsBusy)
            { gui.DisplaySmallMessage("工作台不可用：世界未就绪或正在施工。", Color.White, false, false); return true; }
            if (owner != null && owner != miner) Reset();
            owner = miner; world = miner.Project;
            if (service == null) { service = new BuildingSelectionService(); workbench = new BuildingWorkbench(service); }
            service.SetImportedBlueprint(WorldEditRuntime.ClipboardBlueprint);
            if (dialog != null) return true;
            if (!selecting) { OpenConfig(); return true; }
            selecting = false;
            TerrainRaycastResult? hit = miner.Raycast<TerrainRaycastResult>(ray, RaycastMode.Interaction);
            if (!hit.HasValue || miner.m_subsystemTerrain == null)
            { gui.DisplaySmallMessage("已取消落点选择，可再次使用工作台。", Color.White, false, false); return true; }
            Blueprint bp = workbench.EffectiveBlueprint();
            if (bp == null || workbench.NeedsRegenerate) { OpenConfig(); return true; }
            CellPos surface = new CellPos(hit.Value.CellFace.X, hit.Value.CellFace.Y, hit.Value.CellFace.Z);
            CellPos origin = WorldEditRuntime.WorkbenchOriginFor(bp, surface);
            BuildPreviewModel model;
            WorkbenchStatus status = workbench.PreviewAt(origin, new GameTerrainGateway(miner.m_subsystemTerrain), WorldEditRuntime.Palette, out model, false);
            if (model == null) { gui.DisplaySmallMessage(status.Reason, Color.White, false, false); OpenConfig(); return true; }
            dialog = new WorkbenchConfirmDialog(miner.ComponentPlayer, model, Confirm, OpenConfig, CloseDialog);
            DialogsManager.ShowDialog(miner.ComponentPlayer.GameWidget.GuiWidget, dialog);
            return true;
        }
        private static void OpenConfig()
        {
            CloseDialog(); selecting = false;
            if (owner == null || owner.ComponentPlayer == null || owner.ComponentPlayer.GameWidget == null) return;
            service.SetImportedBlueprint(WorldEditRuntime.ClipboardBlueprint);
            if (workbench.Choice == null) workbench.SelectFirst();
            dialog = new BuildingWorkbenchDialog(owner.ComponentPlayer, service, workbench, BeginSelection, CloseDialog);
            DialogsManager.ShowDialog(owner.ComponentPlayer.GameWidget.GuiWidget, dialog);
        }
        private static void BeginSelection()
        {
            CloseDialog(); selecting = true;
            owner.ComponentPlayer.ComponentGui.DisplaySmallMessage("对准地面使用工作台选择落点；对准天空使用可取消。", Color.White, false, false);
        }
        private static void Confirm()
        {
            CloseDialog();
            if (owner == null || owner.m_subsystemTerrain == null || WorldEditRuntime.IsBusy) return;
            BuildStartDescriptor descriptor;
            WorkbenchStatus result = workbench.TryBeginBuild(WorldEditRuntime.CurrentAuthority(), new GameTerrainGateway(owner.m_subsystemTerrain), WorldEditRuntime.Palette, out descriptor);
            if (!result.Accepted || descriptor == null)
            { owner.ComponentPlayer.ComponentGui.DisplaySmallMessage(result.Reason, Color.White, false, false); OpenConfig(); return; }
            workbench.CommitApplied();
            WorldEditRuntime.WorkbenchBeginBuild(owner, owner.ComponentPlayer.ComponentGui, descriptor);
        }
    }
}

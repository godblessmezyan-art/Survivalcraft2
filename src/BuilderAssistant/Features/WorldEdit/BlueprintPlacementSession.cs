using System;
using System.Collections.Generic;
using BuilderAssistant.Core;
using BuilderAssistant.Core.Blueprints;
using BuilderAssistant.Core.BuildEngine;
using BuilderAssistant.Core.Safety;
using Engine;
using Game;

namespace BuilderAssistant.Features.WorldEdit
{
    internal static class BlueprintPlacementSession
    {
        private static ComponentMiner owner;
        private static object world;
        private static Dialog dialog;
        private static Blueprint selected;
        private static bool selecting, clear;
        private static int wait;
        private static CellPos origin;
        private static GameTerrainGateway gateway;
        private static Dictionary<CellPos, int> snapshot;
        private static int count;
        public static void Reset()
        {
            if (dialog != null) DialogsManager.HideDialog(dialog);
            dialog = null; owner = null; world = null; selected = null; selecting = false;
            gateway = null; snapshot = null; wait = 0; clear = true;
        }
        private static void Close() { if (dialog != null) DialogsManager.HideDialog(dialog); dialog = null; }
        private static void Cancel() { Close(); selecting = false; wait = 0; selected = null; gateway = null; snapshot = null; }
        private static void Message(string message) { if (owner != null && owner.ComponentPlayer != null) owner.ComponentPlayer.ComponentGui.DisplaySmallMessage("[蓝图] " + message, Color.White, false, false); }
        public static void Tick(ComponentGui gui)
        {
            if (owner != null && gui != null && gui.m_componentPlayer != null && !object.ReferenceEquals(world, gui.m_componentPlayer.Project)) { Reset(); return; }
            if (dialog != null && dialog.ParentWidget != null && !DialogsManager.Dialogs.Contains(dialog)) { dialog = null; selecting = false; }
            if (wait > 0 && owner != null)
            {
                if (!TerrainAuthority.IsWritable(WorldEditRuntime.CurrentAuthority()) || WorldEditRuntime.IsBusy) { wait = 0; Message("权限或施工状态已改变，放置已取消。"); return; }
                wait--; Prepare();
                if (wait == 0 && gateway != null && snapshot == null) Message("等待区块超时，未修改地形。");
            }
        }
        public static bool OnUse(ComponentMiner miner, Ray3 ray)
        {
            if (miner == null || miner.ComponentPlayer == null || miner.ComponentPlayer.GameWidget == null) return true;
            if (!object.ReferenceEquals(world, miner.Project) || (owner != null && owner != miner)) Reset();
            owner = miner; world = miner.Project;
            if (dialog != null) return true;
            if (wait > 0 || WorldEditRuntime.IsBusy) { Message("正在施工或等待区块。请稍候。 "); return true; }
            if (!selecting) { Open(); return true; }
            selecting = false;
            var hit = miner.Raycast<TerrainRaycastResult>(ray, RaycastMode.Interaction);
            if (!hit.HasValue) { Message("已取消放置。"); return true; }
            if (miner.m_subsystemTerrain == null || !TerrainAuthority.IsWritable(WorldEditRuntime.CurrentAuthority())) { Message("仅单人或房主可以建造。"); return true; }
            origin = WorldEditRuntime.WorkbenchOriginFor(selected, new CellPos(hit.Value.CellFace.X, hit.Value.CellFace.Y, hit.Value.CellFace.Z));
            gateway = new GameTerrainGateway(miner.m_subsystemTerrain); snapshot = null; wait = 150; Prepare(); return true;
        }
        private static void Open()
        {
            Cancel();
            if (owner == null || owner.ComponentPlayer == null || owner.ComponentPlayer.GameWidget == null) return;
            dialog = new BlueprintToolDialog(owner.ComponentPlayer, Begin, Cancel);
            DialogsManager.ShowDialog(owner.ComponentPlayer.GameWidget.GuiWidget, dialog);
        }
        private static void Begin(int rotation, bool clearSetting)
        {
            Blueprint bp = WorldEditRuntime.ClipboardBlueprint;
            string error = WorldEditRuntime.QuickValidate(bp);
            if (error != null) { Message(error); return; }
            if (!TerrainAuthority.IsWritable(WorldEditRuntime.CurrentAuthority())) { Message("联机客户端只能浏览；仅房主可建造。"); return; }
            if (WorldEditRuntime.IsBusy) { Message("正在施工或等待区块。"); return; }
            selected = rotation == 0 ? bp : bp.Reorient(rotation, false); clear = clearSetting;
            snapshot = null; Close(); selecting = true;
            Message("对准地面使用蓝图工具选择落点；对准天空使用可取消。");
        }
        private static void Prepare()
        {
            if (gateway == null || selected == null) { wait = 0; return; }
            BuildPlan plan = BuildPlanner.Plan(selected, origin, WorldEditRuntime.Palette, gateway, clear, false);
            if (!plan.Success)
            {
                if (plan.AllEditabilityFailuresRecoverable && wait > 0) return;
                wait = 0; snapshot = null; Message("预检失败：" + plan.FailureReason); gateway = null; return;
            }
            var values = new Dictionary<CellPos, int>(); int blocked = 0;
            var plannedCells = new HashSet<CellPos>();
            foreach (PlannedChange change in plan.Changes)
            {
                plannedCells.Add(change.Absolute);
                if (change.Family == BlockFamily.Door) plannedCells.Add(change.Absolute.Add(new CellPos(0, 1, 0)));
            }
            foreach (PlannedChange change in plan.Changes)
            {
                int v;
                if (!gateway.TryGetCellValue(change.Absolute, out v) || !gateway.IsQuickClearSafe(change.Absolute, v, plannedCells))
                { wait = 0; snapshot = null; Message("目标或邻近存在流体、方块实体、复杂方块或未就绪区域，无法安全清障：" + change.Absolute); gateway = null; return; }
                values.Add(change.Absolute, v); if (!gateway.IsCellAir(change.Absolute)) blocked++;
            }
            snapshot = values; count = blocked; wait = 0;
            if (count > 0)
            {
                dialog = new BlueprintClearConfirmDialog(owner.ComponentPlayer, selected, origin, count, Confirm, Open);
                DialogsManager.ShowDialog(owner.ComponentPlayer.GameWidget.GuiWidget, dialog);
            }
            else Start();
        }
        private static void Confirm() { Close(); Start(); }
        private static void Start()
        {
            if (owner == null || gateway == null || snapshot == null || WorldEditRuntime.IsBusy || !TerrainAuthority.IsWritable(WorldEditRuntime.CurrentAuthority())) { Message("状态或权限已变化，未施工。"); return; }
            BuildPlan plan = BuildPlanner.Plan(selected, origin, WorldEditRuntime.Palette, gateway, clear, false);
            if (!plan.Success) { Message("确认时重新校验失败：" + plan.FailureReason); snapshot = null; return; }
            var expected = snapshot; var g = gateway;
            var plannedCells = new HashSet<CellPos>(expected.Keys);
            foreach (PlannedChange change in plan.Changes)
                if (change.Family == BlockFamily.Door) plannedCells.Add(change.Absolute.Add(new CellPos(0, 1, 0)));
            foreach (PlannedChange change in plan.Changes)
            {
                int old, now;
                if (!expected.TryGetValue(change.Absolute, out old) || !g.TryGetCellValue(change.Absolute, out now) || !g.ValuesEquivalent(old, now) || !g.IsQuickClearSafe(change.Absolute, now, plannedCells))
                { Message("目标或邻近地形已变化，未施工。请重新选择落点。"); snapshot = null; return; }
            }
            bool started = WorldEditRuntime.QuickStart(plan, selected, g, origin, clear,
                (pos, value) => { int original; return TerrainAuthority.IsWritable(WorldEditRuntime.CurrentAuthority()) && expected.TryGetValue(pos, out original) && g.ValuesEquivalent(original, value) && g.IsQuickClearSafe(pos, value, plannedCells); });
            snapshot = null; gateway = null;
            Message(started ? "开始施工；可使用 902 撤销。" : "无法开始施工，未修改地形。");
        }
    }
}

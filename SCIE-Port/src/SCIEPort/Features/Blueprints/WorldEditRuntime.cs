using System;
using System.Collections.Generic;
using System.Text;
using Engine;
using Game;
using SCIEPort.Features.Blueprints.Core;
using SCIEPort.Features.Blueprints.Core.Blueprints;
using SCIEPort.Features.Blueprints.Core.BuildEngine;
using SCIEPort.Features.Blueprints.Core.Furniture;
using SCIEPort.Features.Blueprints.Infrastructure;

namespace SCIEPort.Features.Blueprints
{
    /// <summary>Per-local-player library and preview; confirmed builds start without a custom host handshake.</summary>
    internal sealed class WorldEditRuntime
    {
        private readonly SubsystemBlueprintArtifact host;
        internal readonly ComponentPlayer Player;
        internal readonly BlueprintPlacementSession Session;
        private readonly BlueprintHud hud;
        internal VanillaBlockPalette Palette { get { return host.Palette; } }
        internal Blueprint ClipboardBlueprint { get; private set; }
        internal string QuickBlueprintSource { get; private set; }
        private bool copyMode, hasCorner;
        private CellPos corner;
        private BlueprintCopier.CopyJob copy;
        internal bool IsBusy { get { return copy != null || host.Busy; } }
        internal WorldEditRuntime(SubsystemBlueprintArtifact host, ComponentPlayer player)
        { this.host = host; Player = player; Session = new BlueprintPlacementSession(this); hud = new BlueprintHud(this); }
        internal bool CanRequest() { return host.CanOperate(Player); }
        internal void Use(Ray3 ray)
        {
            if (!CanRequest()) return;
            if (copyMode)
            {
                var hit = Player.ComponentMiner.Raycast<TerrainRaycastResult>(ray, RaycastMode.Interaction);
                if (!hit.HasValue) { copyMode = false; hasCorner = false; Message("已取消选区。"); return; }
                var point = new CellPos(hit.Value.CellFace.X, hit.Value.CellFace.Y, hit.Value.CellFace.Z);
                if (!hasCorner) { corner = point; hasCorner = true; Message("起点：" + point); return; }
                copy = new BlueprintCopier.CopyJob(new GameTerrainGateway(Player.ComponentMiner.m_subsystemTerrain), corner, point, "新蓝图");
                hasCorner = false; copyMode = false; Message("正在复制。"); return;
            }
            Session.OnUse(Player.ComponentMiner, ray);
        }
        internal void OpenManager() { copyMode=false;hasCorner=false;copy=null;Session.Open(); }
        internal void BeginCopy()
        {
            if (IsBusy) { Message("请先完成当前操作。"); return; }
            Session.Reset(); copyMode = true; hasCorner = false; Message("选择起点。");
        }
        internal string QuickImport(string json, string source)
        { var r = BlueprintImportService.FromString(json, Palette.IsResolvableKey); return r.Success ? QuickAdopt(r.Blueprint, source) : r.Detail; }
        internal string QuickAdopt(Blueprint blueprint, string source)
        { string error = QuickValidate(blueprint); if (error != null) return error; ClipboardBlueprint = blueprint; QuickBlueprintSource = source; return null; }
        internal string QuickValidate(Blueprint blueprint)
        {
            if (blueprint == null) return "当前没有蓝图。";
            foreach (var cell in blueprint.Cells)
            { int value; string error; if (!Palette.TryResolve(cell.MaterialKey, cell.Family, cell.Data, out value, out error)) return "不支持的材料或状态：" + cell.MaterialKey + "；" + error; }
            return null;
        }
        internal string QuickLoad(string stem)
        { string error; var text = BlueprintStorage.Load(stem, out error); return text == null ? error : QuickImport(text, "本地蓝图库"); }
        internal string QuickSave(string name, out string filename)
        {
            filename = null; var bp = ClipboardBlueprint;
            if (bp == null) return "当前没有蓝图。";
            if (string.IsNullOrWhiteSpace(name)) return "请输入蓝图名称。";
            var renamed = new Blueprint(name.Trim(), bp.SchemaVersion, bp.SizeX, bp.SizeY, bp.SizeZ,
                new List<BlueprintCell>(bp.Cells), new List<BlueprintCell>(bp.StructureCells), new List<FurniturePlacement>(bp.FurniturePlacements));
            return BlueprintStorage.Save(name.Trim(), BlueprintJson.Serialize(renamed), out filename);
        }
        internal CellPos WorkbenchOriginFor(Blueprint bp, CellPos surface)
        { return new CellPos(surface.X - (bp.SizeX - 1) / 2, surface.Y + 1, surface.Z - (bp.SizeZ - 1) / 2); }
        internal bool QuickStart(BuildPlan plan, Blueprint bp, GameTerrainGateway gateway, CellPos origin, bool clear, Func<CellPos, int, bool> guard)
        { return host.StartDirect(Player, plan, bp, origin, clear, guard); }
        internal void Cancel()
        { Session.Reset(); copyMode=false;hasCorner=false;copy=null;host.CancelDirect(Player); }
        internal void Undo() { host.UndoDirect(Player); }
        internal void TickGui(ComponentGui gui)
        {
            if (!CanRequest())
            {
                bool pending = copy != null || copyMode;
                Session.Reset(); copy = null; copyMode = false; hasCorner = false;
                if (pending) Cancel();
            }
            else if (copy != null)
            {
                if (copy.Advance(256))
                {
                    var result = copy.Result; copy = null;
                    if (result.Blueprint == null) Message(result.Failure ?? "选区没有可复制的方块。");
                    else
                    {
                        QuickAdopt(result.Blueprint, "区域复制"); string filename; string error = QuickSave(result.Blueprint.Name, out filename);
                        Message("已复制 " + result.Copied + " 格，跳过 " + (result.Scanned - result.Copied) + " 格。" + (error == null ? "已保存。" : error));
                    }
                }
            }
            Session.Tick(gui); hud.Tick(gui);
        }
        internal void Message(string message) { Player.ComponentGui.DisplaySmallMessage("[天工绘卷] " + message, Color.White, false, false); }
        internal void Dispose() { Session.Reset();hud.Dispose();copy=null;ClipboardBlueprint=null; }
    }
}

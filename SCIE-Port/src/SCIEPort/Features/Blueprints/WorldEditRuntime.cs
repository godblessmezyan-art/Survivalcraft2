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
    /// <summary>Per-local-player library and preview. Terrain mutations are always server requests.</summary>
    internal sealed class WorldEditRuntime
    {
        private readonly SubsystemBlueprintArtifact host;
        internal readonly ComponentPlayer Player;
        internal readonly BlueprintPlacementSession Session;
        private readonly BlueprintHud hud;
        internal VanillaBlockPalette Palette { get { return host.Palette; } }
        internal Blueprint ClipboardBlueprint { get; private set; }
        internal string QuickBlueprintSource { get; private set; }
        private BlueprintPackage awaiting;
        private byte[] upload;
        private Guid request;
        private uint sequence;
        private int acknowledged;
        private double lastSend, began;
        private int retries;
        private Dialog confirmation;
        private bool copyMode, hasCorner;
        private CellPos corner;
        private BlueprintCopier.CopyJob copy;
        internal bool IsBusy { get { return awaiting != null || confirmation != null || upload != null || copy != null || tracking != null || host.Busy; } }
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
        {
            if (!CanRequest() || IsBusy || plan == null || !plan.Success) return false;
            upload = Encoding.UTF8.GetBytes(BlueprintJson.Serialize(bp));
            if (upload.Length > BlueprintTransfer.MaxBytes) { upload = null; Message("蓝图文件过大。"); return false; }
            request = Guid.NewGuid(); acknowledged = 0;
            Send(new BlueprintPackage { Kind = BlueprintPackage.Begin, Request = request, X = origin.X, Y = origin.Y, Z = origin.Z, Clear = clear, Total = upload.Length });
            Message("主机校验中。"); return true;
        }
        private void Send(BlueprintPackage command)
        {
            command.Sequence = ++sequence; awaiting = command; retries = 0;
            began = lastSend = Player.Project.FindSubsystem<SubsystemTime>(true).GameTime;
            host.Send(Player, command);
        }
        internal void Receive(BlueprintPackage reply)
        {
            if (reply.Owner != Player.PlayerGuid || reply.Caster != Player.Entity.EntityId) return;
            if (awaiting == null || reply.Sequence != awaiting.Sequence || reply.Request != awaiting.Request) { ReceiveProgress(reply);return; }
            var sent = awaiting; awaiting = null;
            if (reply.Kind == BlueprintPackage.Ack)
            {
                if (upload == null || reply.Offset < acknowledged || reply.Offset > upload.Length ||
                    (sent.Kind == BlueprintPackage.Chunk && reply.Offset != sent.Offset + sent.Payload.Length))
                { upload = null; Message("传输状态无效。"); return; }
                acknowledged = reply.Offset;
                if (acknowledged == upload.Length) { Send(new BlueprintPackage { Kind = BlueprintPackage.Finish, Request = request }); return; }
                int length = Math.Min(BlueprintTransfer.ChunkBytes, upload.Length - acknowledged);
                var part = new byte[length]; Buffer.BlockCopy(upload, acknowledged, part, 0, length);
                Send(new BlueprintPackage { Kind = BlueprintPackage.Chunk, Request = request, Offset = acknowledged, Payload = part }); return;
            }
            upload = null;
            string message = new UTF8Encoding(false, true).GetString(reply.Payload);
            if (reply.Kind == BlueprintPackage.Ready)
            {
                if (!CanRequest()) { Cancel(); return; }
                string detail = message + "\n位置：" + reply.X + ", " + reply.Y + ", " + reply.Z + "\n" + reply.Total + " 格";
                if (reply.Offset > 0) detail += "\n将覆盖 " + reply.Offset + " 格。";
                confirmation = new MessageDialog(reply.Offset > 0 ? "确认清障建造？" : "确认建造？", detail, "建造", "取消", result =>
                {
                    confirmation = null;
                    if (result == MessageDialogButton.Button1 && CanRequest()) Send(new BlueprintPackage { Kind = BlueprintPackage.Confirm, Request = reply.Request });
                    else Cancel();
                });
                DialogsManager.ShowDialog(Player.GuiWidget, confirmation); return;
            }
            Message(message);
            // Keep the command as the identity for final asynchronous progress, without retrying it.
            if (reply.Offset==1) { tracking = sent; }
        }
        private BlueprintPackage tracking;
        internal void ReceiveProgress(BlueprintPackage reply)
        {
            if (tracking == null || reply.Owner != Player.PlayerGuid || reply.Request != tracking.Request || reply.Sequence != tracking.Sequence) return;
            string message = new UTF8Encoding(false, true).GetString(reply.Payload); Message(message);
            if (reply.Offset!=1) tracking = null;
        }
        internal void TickNetwork(double now)
        {
            if (confirmation != null && !CanRequest()) { DialogsManager.HideDialog(confirmation); confirmation = null; Cancel(); }
            if (awaiting == null) return;
            if (now - began > 15 || retries >= 4) { awaiting = null; upload = null; Message("主机响应超时，请重试。"); return; }
            if (now - lastSend >= 2) { lastSend = now; retries++; host.Send(Player, awaiting); }
        }
        internal void Cancel()
        {
            Session.Reset(); copyMode = false; hasCorner = false; copy = null; upload = null; tracking=null;
            if (confirmation != null) DialogsManager.HideDialog(confirmation); confirmation = null;
            Send(new BlueprintPackage { Kind = BlueprintPackage.Cancel, Request = Guid.NewGuid() });
        }
        internal void Undo()
        { if (awaiting != null) { Message("请稍候。"); return; } Send(new BlueprintPackage { Kind = BlueprintPackage.Undo, Request = Guid.NewGuid() }); }
        internal void TickGui(ComponentGui gui)
        {
            if (!CanRequest())
            {
                bool pending = copy != null || copyMode || upload != null || confirmation != null;
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
        internal void Dispose() { Session.Reset(); hud.Dispose(); if (confirmation != null) DialogsManager.HideDialog(confirmation); confirmation = null; upload = null; awaiting = null; copy = null; ClipboardBlueprint = null; }
    }
}

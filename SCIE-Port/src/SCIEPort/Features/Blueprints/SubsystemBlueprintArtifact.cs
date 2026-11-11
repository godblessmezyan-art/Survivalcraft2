using System;
using System.Collections.Generic;
using System.Text;
using Engine;
using Game;
using Game.NetWork;
using TemplatesDatabase;
using SCIEPort.Features.Blueprints.Core;
using SCIEPort.Features.Blueprints.Core.Blueprints;
using SCIEPort.Features.Blueprints.Core.BuildEngine;
using SCIEPort.Features.Blueprints.Core.Safety;
using SCIEPort.Features.Blueprints.Core.Transactions;
using SCIEPort.Features.Blueprints.Core.Workbench;
using SCIEPort.Features.Codex;

namespace SCIEPort.Features.Blueprints
{
    /// <summary>World-owned authority. Network callbacks only enqueue; Update performs all terrain work.</summary>
    public sealed class SubsystemBlueprintArtifact : SubsystemBlockBehavior, IUpdateable
    {
        private sealed class Incoming { internal ComponentPlayer Actor; internal Client Connection; internal BlueprintPackage Packet; }
        private sealed class Peer
        {
            internal ComponentPlayer Actor; internal Client Connection; internal uint Sequence;
            internal BlueprintPackage LastReply;
            internal BlueprintTransfer Upload; internal CellPos Origin; internal bool Clear;
            internal Blueprint Blueprint; internal BlueprintPlacementPreview Preview; internal Guid Ticket; internal double Expires; internal double NextBegin;
            internal readonly TransactionHistory History = new TransactionHistory();
        }
        private readonly Queue<Incoming> incoming = new Queue<Incoming>();
        private readonly Dictionary<Guid, Peer> peers = new Dictionary<Guid, Peer>();
        private readonly Dictionary<ComponentPlayer, WorldEditRuntime> views = new Dictionary<ComponentPlayer, WorldEditRuntime>();
        private readonly Dictionary<ComponentPlayer, float> walk = new Dictionary<ComponentPlayer, float>();
        private readonly List<ComponentPlayer> retired = new List<ComponentPlayer>();
        private SubsystemPlayers players; private SubsystemTime clock; private SubsystemGameInfo info;
        private ITerrainGateway terrain, activeTerrain;
        internal Func<ComponentPlayer, ITerrainGateway> TerrainForPlayer;
        internal readonly VanillaBlockPalette Palette = new VanillaBlockPalette();
        private Peer running, undoing; private BuildTask task; private BlueprintPackage runningCommand;
        private bool cancelRequested, disposed;
        private int stalledRestore;
        private double nextErrorLog;
        private bool HasPendingUndo { get { foreach(var peer in peers.Values)if(peer.History.HasPendingUndo)return true;return false; } }
        private static bool Equivalent(int a,int b) { return Terrain.ReplaceLight(a,0)==Terrain.ReplaceLight(b,0); }
        internal bool Authority { get { return CommonLib.WorkType == WorkType.Local || CommonLib.WorkType == WorkType.Server; } }
        internal bool Busy { get { return task != null || undoing != null; } }
        /// <inheritdoc/>
        public override int[] HandledBlocks { get { return new[] { BlueprintToolBlock.Index }; } }
        /// <inheritdoc/>
        public UpdateOrder UpdateOrder { get { return (UpdateOrder)(-7); } }
        /// <inheritdoc/>
        public override void Load(ValuesDictionary values)
        {
            base.Load(values); players = Project.FindSubsystem<SubsystemPlayers>(true); clock = Project.FindSubsystem<SubsystemTime>(true);
            info=Project.FindSubsystem<SubsystemGameInfo>(true);
            TerrainForPlayer=p=>new GameTerrainGateway(Project.FindSubsystem<SubsystemTerrain>(true),p.ComponentMiner);
            terrain = new GameTerrainGateway(Project.FindSubsystem<SubsystemTerrain>(true)); Palette.Reset();
        }
        internal bool CanOperate(ComponentPlayer p)
        {
            return !disposed && p != null && ReferenceEquals(p.Project, Project) && players.ComponentPlayers.Contains(p) &&
                p.IsAddedToProject && p.ComponentHealth.Health > 0 &&
                Terrain.ExtractContents(p.ComponentMiner.ActiveBlockValue) == BlueprintToolBlock.Index && SubsystemArtifactCodex.Allowed(p, BlueprintToolBlock.Index);
        }
        internal WorldEditRuntime View(ComponentPlayer p)
        {
            WorldEditRuntime view;
            if (!views.TryGetValue(p, out view)) { view = new WorldEditRuntime(this, p); views.Add(p, view); }
            return view;
        }
        /// <inheritdoc/>
        public override bool OnUse(Ray3 ray, ComponentMiner miner)
        {
            if (Terrain.ExtractContents(miner.ActiveBlockValue) != BlueprintToolBlock.Index) return false;
            var p = miner.ComponentPlayer;
            if (CanOperate(p) && p.GameWidget != null && p.PlayerData.IsMainPlayer) View(p).Use(ray);
            return true;
        }
        internal void Enqueue(ComponentPlayer actor, Client connection, BlueprintPackage packet)
        {
            lock (incoming) { if (!disposed && incoming.Count < 128) incoming.Enqueue(new Incoming { Actor = actor, Connection = connection, Packet = packet }); }
        }
        internal void Send(ComponentPlayer p, BlueprintPackage packet)
        {
            packet.Owner = p.PlayerGuid; packet.Caster = p.Entity.EntityId;
            if (Authority) Enqueue(p, p.PlayerData.Client, packet); else if (CommonLib.WorkType == WorkType.Client) CommonLib.Net.QueuePackage(packet);
        }
        private void Deliver(Peer peer, BlueprintPackage reply)
        {
            reply.Owner = peer.Actor.PlayerGuid; reply.Caster = peer.Actor.Entity.EntityId;
            if (reply.Sequence == peer.Sequence) peer.LastReply = reply;
            if (peer.Connection != null && CommonLib.WorkType == WorkType.Server)
            { reply.To = peer.Connection; CommonLib.Net.QueuePackage(reply); }
            else { WorldEditRuntime view; if (views.TryGetValue(peer.Actor, out view)) view.Receive(reply); }
        }
        private void Reply(Peer peer, BlueprintPackage command, byte kind, string text, int offset = 0)
        {
            text = text ?? ""; if (text.Length > 700) text = text.Substring(0, 700);
            Deliver(peer, new BlueprintPackage { Kind = kind, Request = command.Request, Sequence = command.Sequence,
                X = peer.Origin.X, Y = peer.Origin.Y, Z = peer.Origin.Z, Clear = peer.Clear,
                Total = peer.Preview == null ? 0 : peer.Preview.Plan.Changes == null ? 0 : peer.Preview.Plan.Changes.Count,
                Offset = offset, Payload = Encoding.UTF8.GetBytes(text) });
        }
        private bool Near(Peer peer)
        {
            var pos = peer.Actor.ComponentBody.Position;
            return BlueprintTransfer.ValidOrigin(peer.Origin.X, peer.Origin.Y, peer.Origin.Z) &&
                (pos - new Vector3(peer.Origin.X, peer.Origin.Y, peer.Origin.Z)).LengthSquared() <= 128f * 128f;
        }
        private bool Region(Peer peer,BlueprintPlacementPreview preview)
        {
            foreach(var pos in preview.Snapshot.Keys)
            {
                if(!BlueprintTransfer.ValidOrigin(pos.X,pos.Y,pos.Z))return false;
                Territoriy territory;
                if(SubsystemBedrockBlockBehavior.CheckIsInTerritoriy(pos.X,pos.Z,out territory) && !SubsystemBedrockBlockBehavior.AllowPlayerAction(peer.Actor,territory))return false;
            }
            return true;
        }
        private bool Protected(BlueprintPlacementPreview preview)
        {
            foreach (var pair in preview.Snapshot)
            {
                int id = Terrain.ExtractContents(pair.Value); if (id == 0) continue;
                VanillaBlockCapability capability;
                // Entity containers, unknown mods, circuits, fluids and derived structures are protected.
                if (!VanillaBlockCapabilities.TryGetByTypeName(GameTerrainGateway.TypeNameForContents(id), out capability) || !capability.CanUndoTerrain) return true;
            }
            return false;
        }
        private void Accept(Incoming item)
        {
            var p = item.Actor; var c = item.Packet;
            if (!Authority)
            {
                foreach (var pair in views) if (pair.Key.PlayerGuid == c.Owner && pair.Key.Entity.EntityId == c.Caster) { pair.Value.Receive(c); break; }
                return;
            }
            if (p == null || !ReferenceEquals(p.PlayerData.Client, item.Connection) || !ReferenceEquals(p.Project, Project) ||
                !players.ComponentPlayers.Contains(p) || c.Owner != p.PlayerGuid || c.Caster != p.Entity.EntityId || c.Request == Guid.Empty) return;
            Peer peer;
            if (!peers.TryGetValue(p.PlayerGuid, out peer))
            {
                if (peers.Count >= 32)
                {
                    Guid evict=Guid.Empty;
                    foreach(var entry in peers)if(entry.Value!=running && entry.Value!=undoing && !entry.Value.History.HasPendingUndo && !players.ComponentPlayers.Contains(entry.Value.Actor)){evict=entry.Key;break;}
                    if(evict!=Guid.Empty)peers.Remove(evict);
                    else { var rejected=new Peer{Actor=p,Connection=item.Connection};Reply(rejected,c,BlueprintPackage.Status,"会话繁忙，请稍候。");return; }
                }
                peer = new Peer { Actor = p, Connection = item.Connection }; peers.Add(p.PlayerGuid, peer);
            }
            if (!ReferenceEquals(peer.Actor, p) || !ReferenceEquals(peer.Connection, item.Connection))
            {
                // Keep GUID-owned undo history on reconnect; discard connection-scoped tickets and sequence.
                peer.Actor = p; peer.Connection = item.Connection; peer.Sequence = 0; peer.LastReply = null; peer.Upload = null; peer.Preview = null; peer.Blueprint=null;peer.Ticket = Guid.Empty;
            }
            if (c.Sequence == peer.Sequence) { if (peer.LastReply != null) Deliver(peer, peer.LastReply); return; }
            if (!BlueprintTransfer.Newer(c.Sequence, peer.Sequence)) return;
            peer.Sequence = c.Sequence; peer.LastReply = null;
            // Cancellation and own-history undo require live membership, but must remain possible after unequipping/death.
            if (c.Kind == BlueprintPackage.Cancel)
            {
                peer.Upload = null; peer.Preview = null; peer.Blueprint=null;peer.Ticket = Guid.Empty;
                if (undoing == peer) { undoing=null;Reply(peer,c,BlueprintPackage.Status,"撤销已暂停，恢复后可继续撤销。");return; }
                if (running == peer) { cancelRequested = true; runningCommand = c; }
                Reply(peer, c, BlueprintPackage.Status, running == peer ? "正在取消并回滚。" : "已取消。",running==peer?1:0); return;
            }
            if (!CanOperate(p)) { Reply(peer, c, BlueprintPackage.Status, "神器未就绪或真名未解放。"); return; }
            if (c.Kind == BlueprintPackage.Undo)
            {
                if (Busy) { Reply(peer, c, BlueprintPackage.Status, "施工中，请稍候。"); return; }
                if(HasPendingUndo && !peer.History.HasPendingUndo) { Reply(peer,c,BlueprintPackage.Status,"请先恢复尚未完成的撤销。");return; }
                if (!peer.History.HasUndoable) { Reply(peer, c, BlueprintPackage.Status, "没有可撤销的建造。"); return; }
                undoing = peer; activeTerrain=TerrainForPlayer(p);stalledRestore=0; runningCommand = c; Reply(peer, c, BlueprintPackage.Status, "正在撤销。",1); return;
            }
            if ((c.Kind==BlueprintPackage.Begin || c.Kind==BlueprintPackage.Finish || c.Kind==BlueprintPackage.Confirm) &&
                (info==null || info.WorldSettings.GameMode==GameMode.Adventure)) { Reply(peer,c,BlueprintPackage.Status,"冒险世界禁止施工。");return; }
            if (c.Kind == BlueprintPackage.Begin)
            {
                if(HasPendingUndo) { Reply(peer,c,BlueprintPackage.Status,"请先完成尚未恢复的撤销。");return; }
                if(clock.GameTime<peer.NextBegin) { Reply(peer,c,BlueprintPackage.Status,"操作过快，请稍候。");return; }
                peer.NextBegin=clock.GameTime+.5;
                if (Busy) { Reply(peer, c, BlueprintPackage.Status, "施工中，请稍候。"); return; }
                int uploads = 0; foreach (var other in peers.Values) if ((other.Upload != null && other.Upload.Expires >= clock.GameTime) || other.Preview!=null) uploads++;
                if (uploads >= BlueprintTransfer.MaxUploads && peer.Upload == null && peer.Preview==null) { Reply(peer, c, BlueprintPackage.Status, "传输繁忙，请稍候。"); return; }
                peer.Origin = new CellPos(c.X, c.Y, c.Z); peer.Clear = c.Clear; peer.Preview = null; peer.Blueprint=null;peer.Ticket = Guid.Empty;
                if (!Near(peer) || c.Total <= 0 || c.Total > BlueprintTransfer.MaxBytes) { Reply(peer, c, BlueprintPackage.Status, "蓝图大小或落点超出限制。"); return; }
                peer.Upload = new BlueprintTransfer(c.Request, c.Total, clock.GameTime);
                Reply(peer, c, BlueprintPackage.Ack, "", 0); return;
            }
            if (c.Kind == BlueprintPackage.Chunk)
            {
                if (peer.Upload == null || peer.Upload.Request != c.Request || !peer.Upload.Append(c.Offset, c.Payload, clock.GameTime))
                { peer.Upload = null; Reply(peer, c, BlueprintPackage.Status, "分片无效或传输已过期。"); return; }
                Reply(peer, c, BlueprintPackage.Ack, "", peer.Upload.Received); return;
            }
            if (c.Kind == BlueprintPackage.Finish)
            {
                if (peer.Upload == null || peer.Upload.Request != c.Request) { Reply(peer, c, BlueprintPackage.Status, "传输已过期。"); return; }
                var upload = peer.Upload; peer.Upload = null;
                var result = BlueprintImportService.FromString(upload.Finish(clock.GameTime), Palette.IsResolvableKey);
                if (!result.Success) { Reply(peer, c, BlueprintPackage.Status, result.Detail); return; }
                peer.Blueprint = result.Blueprint;
                if (!Near(peer)) { peer.Blueprint=null;Reply(peer, c, BlueprintPackage.Status, "落点距离过远。"); return; }
                peer.Preview = BlueprintPlacementPreview.Inspect(peer.Blueprint, peer.Origin, Palette, terrain, peer.Clear);
                if (!peer.Preview.Ready || !Region(peer,peer.Preview) || (peer.Clear && Protected(peer.Preview)))
                { Reply(peer, c, BlueprintPackage.Status, peer.Preview.Ready ? "范围越界或包含无权修改、无法安全回收的方块。" : peer.Preview.Plan.FailureReason); peer.Preview = null;peer.Blueprint=null; return; }
                peer.Ticket = c.Request; peer.Expires = clock.GameTime + 30;
                Reply(peer, c, BlueprintPackage.Ready, peer.Blueprint.Name, peer.Preview.OccupiedCount); return;
            }
            if (c.Kind == BlueprintPackage.Confirm)
            {
                if (Busy || HasPendingUndo) { Reply(peer, c, BlueprintPackage.Status, "施工或恢复尚未结束，请重新预览。"); return; }
                if (peer.Preview == null || peer.Ticket != c.Request || clock.GameTime > peer.Expires || !Near(peer) || !Region(peer,peer.Preview) || !peer.Preview.StillMatches(terrain, Equivalent))
                { peer.Ticket = Guid.Empty; peer.Preview = null; peer.Blueprint=null;Reply(peer, c, BlueprintPackage.Status, "预览过期或地形已变化，请重新预览。"); return; }
                var snapshot = peer.Preview.Snapshot;
                var plan = BuildPlanner.Plan(peer.Blueprint, peer.Origin, Palette, terrain, peer.Clear, false);
                peer.Ticket = Guid.Empty;
                if (!plan.Success) { Reply(peer, c, BlueprintPackage.Status, plan.FailureReason); return; }
                running = peer; runningCommand = c; cancelRequested = false;stalledRestore=0;activeTerrain=TerrainForPlayer(p);
                task = new BuildTask(peer.Blueprint.Name, peer.Origin, plan.Changes, peer.Clear, false);
                var connection = peer.Connection; var actor = peer.Actor;
                task.SetPreWriteGuard((cell, value) => CanOperate(actor) && ReferenceEquals(actor.PlayerData.Client, connection) &&
                    snapshot.TryGetValue(cell, out var expected) && Equivalent(expected, value) &&
                    (!SubsystemBedrockBlockBehavior.CheckIsInTerritoriy(cell.X,cell.Z,out Territoriy territory) || SubsystemBedrockBlockBehavior.AllowPlayerAction(actor,territory)));
                peer.Preview = null; peer.Blueprint = null;
                Reply(peer, c, BlueprintPackage.Status, "开始施工。",1); return;
            }
        }
        /// <inheritdoc/>
        public void Update(float dt)
        {
            if (clock.GameTimeDelta <= 0) return;
            for (int i = 0; i < 8; i++)
            {
                Incoming item; lock (incoming) { if (incoming.Count == 0) break; item = incoming.Dequeue(); }
                try { Accept(item); }
                catch (Exception ex) { Log.Error("[SCIEPort:Blueprint] Request failed: " + ex); Peer peer; if (item.Actor != null && peers.TryGetValue(item.Actor.PlayerGuid, out peer)) { peer.Upload = null; peer.Preview = null; Reply(peer, item.Packet, BlueprintPackage.Status, "请求失败，未开始新施工。"); } }
            }
            if (Authority)
            {
                foreach (var peer in peers.Values) { if (peer.Upload != null && clock.GameTime > peer.Upload.Expires) peer.Upload = null; if (peer.Preview != null && clock.GameTime > peer.Expires) { peer.Preview = null; peer.Blueprint = null; peer.Ticket = Guid.Empty; } }
                Advance();
            }
            retired.Clear();
            foreach (var pair in views) { if(!players.ComponentPlayers.Contains(pair.Key))retired.Add(pair.Key);else pair.Value.TickNetwork(clock.GameTime); }
            foreach(var old in retired){views[old].Dispose();views.Remove(old);}
            retired.Clear(); foreach (var pair in walk) { pair.Key.ComponentLocomotion.WalkSpeed -= pair.Value; retired.Add(pair.Key); } foreach (var p in retired) walk.Remove(p);
            foreach (var p in players.ComponentPlayers) if (CanOperate(p))
            {
                float delta = p.ComponentLocomotion.WalkSpeed * .2f;
                if (!float.IsNaN(delta) && !float.IsInfinity(delta)) { walk[p] = delta; p.ComponentLocomotion.WalkSpeed += delta; }
            }
        }
        private void Advance()
        {
            // Exactly one branch can consume the world's 32-cell mutation budget in an update.
            if (undoing != null)
            {
                int pending; undoing.History.UndoLast(activeTerrain, out pending);
                if(pending>0 && ++stalledRestore==240){Reply(undoing,runningCommand,BlueprintPackage.Status,"撤销尚未完成，正在等待区块或权限。",1);Log.Warning("[SCIEPort:Blueprint] Undo waiting; owner="+undoing.Actor.PlayerGuid+" pending="+pending);}
                if (pending == 0) { Reply(undoing, runningCommand, BlueprintPackage.Status, "撤销完成。"); undoing = null; }
                return;
            }
            if (task == null) return;
            try
            {
                if (!task.IsFinished && (cancelRequested || !CanOperate(running.Actor))) { task.Cancel(activeTerrain); cancelRequested = false; }
                else if (!task.IsFinished) task.Advance(activeTerrain, BuildSafetyLimits.MaxBlocksPerBatch);
                else if (task.PendingRollbackCount > 0) task.RetryRollback(activeTerrain);
                if(task.PendingRollbackCount>0 && ++stalledRestore==240){Reply(running,runningCommand,BlueprintPackage.Status,"回滚尚未完成，正在等待区块或权限。",1);Log.Warning("[SCIEPort:Blueprint] Rollback waiting; owner="+running.Actor.PlayerGuid+" pending="+task.PendingRollbackCount);}
                if (!task.IsFinished || task.PendingRollbackCount > 0) return;
                bool complete = task.State == BuildTaskState.Completed;
                if (complete)
                {
                    running.History.Record(task);
                    int total=0;foreach(var peer in peers.Values)total+=peer.History.JournalCells;
                    while(total>100000)
                    {
                        bool trimmed=false;
                        foreach(var peer in peers.Values){int removed=peer.History.TrimOldest();if(removed>0){total-=removed;trimmed=true;if(total<=100000)break;}}
                        if(!trimmed)break;
                    }
                }
                Reply(running, runningCommand, BlueprintPackage.Status, complete ? "建造完成：" + task.AppliedCount + " 格。" : "施工停止并已回滚。" + task.FailureReason);
                task = null; running = null;
            }
            catch (Exception ex) { cancelRequested = true;if(clock.GameTime>=nextErrorLog){nextErrorLog=clock.GameTime+5;Log.Error("[SCIEPort:Blueprint] Terrain operation paused for rollback: " + ex);} }
        }
        internal void TickGui(ComponentGui gui)
        {
            var p = gui == null ? null : gui.m_componentPlayer;
            if (p == null || p.GameWidget == null) return;
            WorldEditRuntime view;
            if (CanOperate(p) || views.TryGetValue(p, out view)) View(p).TickGui(gui);
        }
        internal void Draw(ComponentGui gui, Camera camera, int order)
        { WorldEditRuntime view; if (gui != null && views.TryGetValue(gui.m_componentPlayer, out view)) view.Session.Draw(gui, camera, order); }
        /// <inheritdoc/>
        public override void Dispose()
        {
            disposed = true; foreach (var view in views.Values) view.Dispose(); views.Clear();
            foreach (var pair in walk) pair.Key.ComponentLocomotion.WalkSpeed -= pair.Value; walk.Clear();
            lock (incoming) incoming.Clear(); peers.Clear(); task = null; running = null; undoing = null; base.Dispose();
        }
    }
}

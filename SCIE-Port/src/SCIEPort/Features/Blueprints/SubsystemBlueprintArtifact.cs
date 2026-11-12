using System;
using System.Collections.Generic;
using Engine;
using Game;
using Game.NetWork;
using TemplatesDatabase;
using SCIEPort.Features.Blueprints.Core;
using SCIEPort.Features.Blueprints.Core.Blueprints;
using SCIEPort.Features.Blueprints.Core.BuildEngine;
using SCIEPort.Features.Blueprints.Core.Safety;
using SCIEPort.Features.Blueprints.Core.Transactions;

namespace SCIEPort.Features.Blueprints
{
    /// <summary>World-owned direct build scheduler; no custom client/host handshake.</summary>
    public sealed class SubsystemBlueprintArtifact : SubsystemBlockBehavior, IUpdateable
    {
        private readonly Dictionary<Guid, TransactionHistory> histories = new Dictionary<Guid, TransactionHistory>();
        private readonly Dictionary<ComponentPlayer, WorldEditRuntime> views = new Dictionary<ComponentPlayer, WorldEditRuntime>();
        private readonly Dictionary<ComponentPlayer, float> walk = new Dictionary<ComponentPlayer, float>();
        private readonly List<ComponentPlayer> retired = new List<ComponentPlayer>();
        private SubsystemPlayers players;
        private SubsystemTime clock;
        internal Func<ComponentPlayer, ITerrainGateway> TerrainForPlayer;
        internal Action<ComponentPlayer, string> Feedback;
        internal readonly VanillaBlockPalette Palette = new VanillaBlockPalette();
        private BuildTask task;
        private ComponentPlayer actor;
        private ITerrainGateway activeTerrain;
        private TransactionHistory undoing;
        private bool cancelRequested, disposed;
        private double nextErrorLog;
        internal bool Busy { get { return task != null || undoing != null; } }
        /// <inheritdoc/>
        public override int[] HandledBlocks { get { return new[] { BlueprintToolBlock.Index }; } }
        /// <inheritdoc/>
        public UpdateOrder UpdateOrder { get { return (UpdateOrder)(-7); } }
        /// <inheritdoc/>
        public override void Load(ValuesDictionary values)
        {
            base.Load(values);players=Project.FindSubsystem<SubsystemPlayers>(true);clock=Project.FindSubsystem<SubsystemTime>(true);
            TerrainForPlayer=p=>new GameTerrainGateway(Project.FindSubsystem<SubsystemTerrain>(true),p.ComponentMiner);Palette.Reset();
        }
        internal bool CanOperate(ComponentPlayer p)
        {
            return !disposed && p!=null && ReferenceEquals(p.Project,Project) && players.ComponentPlayers.Contains(p) &&
                p.IsAddedToProject && p.ComponentHealth.Health>0 && Terrain.ExtractContents(p.ComponentMiner.ActiveBlockValue)==BlueprintToolBlock.Index;
        }
        internal WorldEditRuntime View(ComponentPlayer p)
        {
            WorldEditRuntime view;if(!views.TryGetValue(p,out view)){view=new WorldEditRuntime(this,p);views.Add(p,view);}return view;
        }
        /// <inheritdoc/>
        public override bool OnUse(Ray3 ray, ComponentMiner miner)
        {
            if(Terrain.ExtractContents(miner.ActiveBlockValue)!=BlueprintToolBlock.Index)return false;
            var p=miner.ComponentPlayer;if(CanOperate(p)&&p.GameWidget!=null&&p.PlayerData.IsMainPlayer)View(p).Use(ray);return true;
        }
        private TransactionHistory History(ComponentPlayer p)
        {
            TransactionHistory history;if(!histories.TryGetValue(p.PlayerGuid,out history)){history=new TransactionHistory();histories.Add(p.PlayerGuid,history);}return history;
        }
        private bool PendingUndo { get { foreach(var history in histories.Values)if(history.HasPendingUndo)return true;return false; } }
        private void Notify(ComponentPlayer p,string text)
        {
            if(Feedback!=null){Feedback(p,text);return;}
            WorldEditRuntime view;if(p!=null&&views.TryGetValue(p,out view))view.Message(text);
        }
        internal bool StartDirect(ComponentPlayer p,BuildPlan plan,Blueprint bp,CellPos origin,bool clear,Func<CellPos,int,bool> guard)
        {
            if(!CanOperate(p)||Busy||PendingUndo||plan==null||!plan.Success||bp==null)return false;
            if(!BlueprintTransfer.ValidOrigin(origin.X,origin.Y,origin.Z))return false;
            actor=p;activeTerrain=TerrainForPlayer(p);cancelRequested=false;
            task=new BuildTask(bp.Name,origin,plan.Changes,clear,false);
            task.SetPreWriteGuard((cell,value)=>CanOperate(p)&&(guard==null||guard(cell,value)));
            Notify(p,"开始施工。");return true;
        }
        internal void CancelDirect(ComponentPlayer p)
        {
            if(actor!=p)return;
            if(undoing!=null){undoing=null;Notify(p,"撤销已暂停。");}
            if(task!=null)cancelRequested=true;
        }
        internal void UndoDirect(ComponentPlayer p)
        {
            if(!CanOperate(p))return;
            if(Busy){Notify(p,"施工中，请稍候。");return;}
            var history=History(p);
            if(!history.HasUndoable){Notify(p,"没有可撤销的建造。");return;}
            if(PendingUndo&&!history.HasPendingUndo){Notify(p,"请先完成尚未恢复的撤销。");return;}
            actor=p;activeTerrain=TerrainForPlayer(p);undoing=history;Notify(p,"正在撤销。");
        }
        private void TrimHistory()
        {
            int count=0;foreach(var history in histories.Values)count+=history.JournalCells;
            while(count>100000)
            {
                bool trimmed=false;foreach(var history in histories.Values){int n=history.TrimOldest();count-=n;trimmed|=n>0;if(count<=100000)break;}
                if(!trimmed)break;
            }
        }
        private void Advance()
        {
            if(undoing!=null)
            {
                int pending;undoing.UndoLast(activeTerrain,out pending);
                if(pending==0){undoing=null;Notify(actor,"撤销完成。");actor=null;}return;
            }
            if(task==null)return;
            try
            {
                if(!task.IsFinished&&(cancelRequested||!CanOperate(actor))){task.Cancel(activeTerrain);cancelRequested=false;}
                else if(!task.IsFinished)task.Advance(activeTerrain,BuildSafetyLimits.MaxBlocksPerBatch);
                else if(task.PendingRollbackCount>0)task.RetryRollback(activeTerrain);
                if(!task.IsFinished||task.PendingRollbackCount>0)return;
                if(task.State==BuildTaskState.Completed)
                {
                    History(actor).Record(task);TrimHistory();
                    Notify(actor,(CommonLib.WorkType==WorkType.Client?"已提交建造：":"建造完成：")+task.AppliedCount+" 格。");
                }
                else Notify(actor,"施工停止并已回滚。"+task.FailureReason);
                task=null;actor=null;
            }
            catch(Exception ex)
            {
                cancelRequested=true;if(clock.GameTime>=nextErrorLog){nextErrorLog=clock.GameTime+5;Log.Error("[SCIEPort:Blueprint] Restore pending: "+ex);}
            }
        }
        /// <inheritdoc/>
        public void Update(float dt)
        {
            if(clock.GameTimeDelta<=0)return;
            Advance();
            retired.Clear();foreach(var pair in views)if(!players.ComponentPlayers.Contains(pair.Key))retired.Add(pair.Key);
            foreach(var p in retired){views[p].Dispose();views.Remove(p);}
            foreach(var pair in walk)pair.Key.ComponentLocomotion.WalkSpeed-=pair.Value;walk.Clear();
            foreach(var p in players.ComponentPlayers)if(CanOperate(p))
            {
                float delta=p.ComponentLocomotion.WalkSpeed*.2f;if(!float.IsNaN(delta)&&!float.IsInfinity(delta)){walk[p]=delta;p.ComponentLocomotion.WalkSpeed+=delta;}
            }
        }
        internal void TickGui(ComponentGui gui)
        {
            var p=gui?.m_componentPlayer;if(p==null||p.GameWidget==null)return;
            WorldEditRuntime view;if(CanOperate(p)||views.TryGetValue(p,out view))View(p).TickGui(gui);
        }
        internal void Draw(ComponentGui gui,Camera camera,int order)
        {WorldEditRuntime view;if(gui!=null&&views.TryGetValue(gui.m_componentPlayer,out view))view.Session.Draw(gui,camera,order);}
        /// <inheritdoc/>
        public override void Dispose()
        {
            disposed=true;foreach(var view in views.Values)view.Dispose();views.Clear();
            foreach(var pair in walk)pair.Key.ComponentLocomotion.WalkSpeed-=pair.Value;walk.Clear();histories.Clear();task=null;undoing=null;actor=null;base.Dispose();
        }
    }
}

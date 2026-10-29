using System;
using System.Collections.Generic;
using Engine;
using Game;
using Game.NetWork;
using GameEntitySystem;
namespace SCIEPort.Features.AbyssReaper
{
    internal sealed class WorldCutRuntime : IDisposable
    {
        internal sealed class Cast
        {
            internal ComponentPlayer Owner;
            internal Guid OwnerGuid;
            internal uint Sequence;
            internal Vector3 Origin,Direction,Right;
            internal WorldCutMotion Motion;
            internal bool VisualOnly;
            internal readonly WorldCutHitLedger<Entity> Hits=new WorldCutHitLedger<Entity>();
        }
        private sealed class Peer : WorldCutSession
        {
            internal double LastAttempt=-100,Seen;
            internal string Feedback;
            internal double FeedbackUntil;
        }
        private struct Pending { internal ComponentPlayer Owner;internal Client Reply;internal WorldCutPackage Packet; }
        private readonly Project project;
        private readonly SubsystemPlayers players;
        private readonly SubsystemTime clock;
        private readonly WorldCutDamage damage;
        private readonly WorldCutScene scene;
        private readonly Dictionary<Guid,Peer> peers=new Dictionary<Guid,Peer>();
        private readonly List<Guid> retired=new List<Guid>();
        private readonly List<Cast> casts=new List<Cast>(WorldCutRules.MaxCasts);
        private readonly Queue<Pending> queue=new Queue<Pending>();
        private bool disposed;
        private bool Authority => WorldCutRules.Authority(CommonLib.WorkType==WorkType.Local,CommonLib.WorkType==WorkType.Server);
        internal WorldCutRuntime(Project p,SubsystemPlayers ps,SubsystemTime t,SubsystemTerrain terrain,SubsystemBodies bodies,bool allowPlayers,bool allowPassive,bool visible,bool low,float initial,float close)
        {
            project=p;players=ps;clock=t;damage=new WorldCutDamage(terrain,bodies,p.FindSubsystem<SubsystemGameInfo>(true),allowPlayers,allowPassive,initial,close);scene=new WorldCutScene(visible,low);
        }
        private double Now => clock.GameTime;
        private Peer Get(ComponentPlayer p,bool create=true)
        {
            Peer e;
            if(!peers.TryGetValue(p.PlayerGuid,out e) && create && peers.Count<WorldCutRules.MaxPeers) { e=new Peer();peers.Add(p.PlayerGuid,e); }
            if(e!=null){e.BindEntity(p.Entity.EntityId);e.Seen=Now;}return e;
        }
        internal bool Busy(ComponentPlayer p)
        {
            Peer e=Get(p,false);if(e!=null&&(e.PendingUntil>Now||e.ActiveUntil>Now))return true;
            // Ownership ends when cleanup has restored model orders, before a fresh normal input is allowed.
            foreach(var cast in casts)if(cast.OwnerGuid==p.PlayerGuid)return true;
            return false;
        }
        internal bool PendingRequest(ComponentPlayer p) { var e=Get(p,false);return e!=null&&e.PendingUntil>Now; }
        internal double Remaining(ComponentPlayer p) { var e=Get(p,false);return e==null?0:Math.Max(0,e.ReadyAt-Now); }
        internal string Feedback(ComponentPlayer p) { var e=Get(p,false);return e!=null&&e.FeedbackUntil>Now?e.Feedback:null; }
        internal void Report(ComponentPlayer p,WorldCutFailure reason)
        {
            var e=Get(p);
            if(e==null) { if(p.GameWidget!=null)p.ComponentGui.DisplaySmallMessage(Message(reason),new Color(193,151,231),false,false);return; }
            e.Feedback=Message(reason);e.FeedbackUntil=Now+2;
        }
        internal bool Request(ComponentPlayer p)
        {
            if(disposed)return false;
            var e=Get(p);if(e==null){Report(p,WorldCutFailure.Capacity);return false;}
            if(Now-e.LastAttempt<.2)return false;e.LastAttempt=Now;
            if(Busy(p)){Report(p,WorldCutFailure.Busy);return false;}
            if(e.ReadyAt>Now){Report(p,WorldCutFailure.Cooldown);return false;}
            if(!OwnerSafe(p)){Report(p,WorldCutFailure.Unavailable);return false;}
            if(!e.Queue(Now))return false;
            var packet=new WorldCutPackage { Kind=0,Caster=p.Entity.EntityId,Sequence=e.Sent };
            e.Feedback=null;
            if(Authority)Enqueue(p,null,packet);
            else if(CommonLib.WorkType==WorkType.Client && CommonLib.Net!=null)CommonLib.Net.QueuePackage(packet);
            else { e.PendingUntil=0;Report(p,WorldCutFailure.Unavailable);return false; }
            return true;
        }
        internal void Enqueue(ComponentPlayer player,Client reply,WorldCutPackage packet)
        {
            lock(queue)if(!disposed&&queue.Count<WorldCutRules.MaxQueue)queue.Enqueue(new Pending { Owner=player,Reply=reply,Packet=packet.Snapshot() });
        }
        private bool OwnerSafe(ComponentPlayer p) => p!=null && p.Project==project && p.Entity.IsAddedToProject && p.ComponentHealth.Health>0
            && p.PlayerData.IsReadyForPlaying && Terrain.ExtractContents(p.ComponentMiner.ActiveBlockValue)==AbyssReaperBlock.Index
            && project.FindSubsystem<SCIEPort.Features.FourSaints.SubsystemFourSaints>(false)?.IsActionLocked(p)!=true;
        private void Reject(ComponentPlayer p,Client to,WorldCutPackage request,WorldCutFailure reason)
        {
            var e=Get(p);if(e!=null)e.PendingUntil=0;Report(p,reason);
            if(CommonLib.WorkType==WorkType.Server && to!=null)CommonLib.Net.QueuePackage(new WorldCutPackage { To=to,Kind=2,Reason=(byte)reason,Caster=request.Caster,Sequence=request.Sequence,Cooldown=(float)Remaining(p) });
        }
        private void Accept(Pending pending)
        {
            var p=pending.Owner;var request=pending.Packet;
            if(!Authority || p==null || p.Project!=project || !p.Entity.IsAddedToProject || p.Entity.EntityId!=request.Caster)return;
            var e=Get(p);if(e==null){Reject(p,pending.Reply,request,WorldCutFailure.Capacity);return;}
            e.Bind(request.Caster,pending.Reply?.TokenId??Guid.Empty);
            if(request.Kind==4)
            {
                if(WorldCutRules.Newer(request.Sequence,e.Processed))e.Processed=request.Sequence;
                CancelCasts(p.PlayerGuid,request.Sequence,true);return;
            }
            if(!e.NewRequest(request.Sequence))return;
            if(!OwnerSafe(p)||clock.GameTimeDelta<=0){Reject(p,pending.Reply,request,WorldCutFailure.Unavailable);return;}
            if(e.ReadyAt>Now){Reject(p,pending.Reply,request,WorldCutFailure.Cooldown);return;}
            foreach(var existing in casts)if(existing.OwnerGuid==p.PlayerGuid){Reject(p,pending.Reply,request,WorldCutFailure.Busy);return;}
            if(casts.Count>=WorldCutRules.MaxCasts){Reject(p,pending.Reply,request,WorldCutFailure.Capacity);return;}
            // The request contains no trusted client origin/direction. Use replicated server eyes only.
            Vector3 direction=p.ComponentCreatureModel.EyeRotation.GetForwardVector();direction.Y=0;
            if(!WorldCutDamage.Finite(direction)||direction.LengthSquared()<.01f){Reject(p,pending.Reply,request,WorldCutFailure.Invalid);return;}
            direction=Vector3.Normalize(direction);Vector3 right=Vector3.Cross(direction,Vector3.UnitY);
            Vector3 origin=p.ComponentCreatureModel.EyePosition-Vector3.UnitY*.25f;
            float safe;var clearance=damage.Step(origin,direction,right,0,.05f,out safe);
            if(clearance!=WorldCutFailure.None){Reject(p,pending.Reply,request,clearance);return;}
            var cast=new Cast { Owner=p,OwnerGuid=p.PlayerGuid,Sequence=request.Sequence,Origin=origin,Direction=direction,Right=right,Motion=new WorldCutMotion(Now) };
            casts.Add(cast);e.Accept(Now);e.Feedback="断界斩 · 向后聚能";e.FeedbackUntil=Now+.8;
            Publish(cast,1);
        }
        private void Publish(Cast cast,byte kind)
        {
            if(CommonLib.WorkType==WorkType.Server)CommonLib.Net.QueuePackage(new WorldCutPackage { Kind=kind,Caster=cast.Owner.Entity.EntityId,Sequence=cast.Sequence,
                Origin=cast.Origin,Direction=cast.Direction,Distance=cast.Motion.Distance,Cooldown=(float)Remaining(cast.Owner) });
        }
        private void Receive(WorldCutPackage packet)
        {
            if(Authority || packet.Sequence==0 || !WorldCutRules.Finite(packet.Cooldown)||packet.Cooldown<0||packet.Cooldown>10.1f)return;
            ComponentPlayer p=null;foreach(var player in players.ComponentPlayers)if(player.Entity.EntityId==packet.Caster){p=player;break;}
            if(p==null)return;var e=Get(p);if(e==null)return;e.Bind(packet.Caster,Guid.Empty);
            if(packet.Kind==2)
            {
                if(packet.Sequence!=e.Sent || packet.Sequence==e.Cancelled)return;
                e.PendingUntil=0;e.ReadyAt=Math.Max(e.ReadyAt,Now+packet.Cooldown);Report(p,(WorldCutFailure)packet.Reason);return;
            }
            if(packet.Kind==5)
            {
                if(WorldCutRules.Newer(packet.Sequence,e.Visual))e.Visual=packet.Sequence;
                e.Cancelled=packet.Sequence;e.ActiveUntil=0;if(packet.Sequence==e.Sent)e.PendingUntil=0;
                CancelCasts(p.PlayerGuid,packet.Sequence,false);return;
            }
            if(packet.Sequence==e.Cancelled || (!WorldCutRules.Newer(packet.Sequence,e.Visual)&&packet.Sequence!=e.Visual))return;
            if(!WorldCutRules.Coordinates(packet.Origin.X,packet.Origin.Y,packet.Origin.Z)||!WorldCutDamage.Finite(packet.Direction)||!WorldCutRules.Finite(packet.Distance)||packet.Distance<0||packet.Distance>25)return;
            float length=packet.Direction.Length();if(length<.95f||length>1.05f||Math.Abs(packet.Direction.Y)>.01f)return;
            Cast cast=null;foreach(var current in casts)if(current.OwnerGuid==p.PlayerGuid&&current.Sequence==packet.Sequence){cast=current;break;}
            if(packet.Kind==3 && packet.Sequence==e.Closed)return;
            if(packet.Kind==1 && packet.Sequence==e.Visual)return; // Duplicate accepted event must not restart animation/cooldown.
            // A presentation slot shortage must never turn an accepted request into a timeout/cancel.
            e.Visual=packet.Sequence;e.ReadyAt=Math.Max(e.ReadyAt,Now+packet.Cooldown);if(packet.Sequence==e.Sent)e.PendingUntil=0;
            e.ActiveUntil=Now+(packet.Kind==3?WorldCutRules.CloseEnd:WorldCutRules.Windup+WorldCutRules.Range/WorldCutRules.Speed+WorldCutRules.CloseEnd);
            if(packet.Kind==3)e.Closed=packet.Sequence;
            if(cast==null)
            {
                if(casts.Count>=WorldCutRules.MaxCasts)return;
                cast=new Cast { Owner=p,OwnerGuid=p.PlayerGuid,Sequence=packet.Sequence,Origin=packet.Origin,Direction=packet.Direction,Right=Vector3.Cross(packet.Direction,Vector3.UnitY),Motion=new WorldCutMotion(Now),VisualOnly=true };
                casts.Add(cast);
            }
            if(packet.Kind==3)cast.Motion.Close(Now,packet.Distance);
        }
        internal void Cancel(ComponentPlayer p)
        {
            if(disposed)return;
            var e=Get(p,false);if(e==null)return;
            if(!Authority && (e.PendingUntil>0||Busy(p)) && e.Sent!=0 && e.Cancelled!=e.Sent)
            {
                e.Cancelled=e.Sent;
                if(CommonLib.Net!=null)CommonLib.Net.QueuePackage(new WorldCutPackage { Kind=4,Caster=p.Entity.EntityId,Sequence=e.Sent });
            }
            if(Authority && e.PendingUntil>0 && WorldCutRules.Newer(e.Sent,e.Processed))e.Processed=e.Sent;
            e.PendingUntil=0;e.ActiveUntil=0;CancelCasts(p.PlayerGuid,0,Authority);
        }
        private void CancelCasts(Guid owner,uint sequence,bool publish)
        {
            foreach(var cast in casts)if(cast.OwnerGuid==owner && (sequence==0||sequence==cast.Sequence))
            { cast.Motion.Cancel();Peer entry;if(peers.TryGetValue(owner,out entry)&&entry.MatchesEntity(cast.Owner.Entity.EntityId))entry.ActiveUntil=0;if(publish)Publish(cast,5); }
        }
        internal void Update(float dt)
        {
            if(disposed)return;
            damage.BeginFrame(casts.Count>0);
            for(int i=0;i<WorldCutRules.RequestsPerFrame;i++)
            {
                Pending pending;lock(queue){if(queue.Count==0)break;pending=queue.Dequeue();}
                try { if(Authority)Accept(pending);else Receive(pending.Packet); }
                catch(Exception error) { if(pending.Owner!=null)Reject(pending.Owner,pending.Reply,pending.Packet,WorldCutFailure.Invalid);Log.Warning("[SCIEPort:WorldCut] Request rejected: "+error.GetType().Name); }
            }
            foreach(var p in players.ComponentPlayers)
            {
                var e=Get(p,false);
                if(e!=null&&e.TimedOut(Now)){Cancel(p);Report(p,WorldCutFailure.Timeout);}
            }
            for(int i=casts.Count-1;i>=0;i--)
            {
                var cast=casts[i];
                try
                {
                    if(!OwnerSafe(cast.Owner)||Now-cast.Motion.Started>=WorldCutRules.Lifetime)
                    { if(Now-cast.Motion.Started>=WorldCutRules.Lifetime)Report(cast.Owner,WorldCutFailure.Capacity);Finish(cast,true);casts.RemoveAt(i);continue; }
                    // A skipped frame must not discard the closing damage edge before expiry.
                    if(cast.Motion.TakeCloseImpact(Now) && !cast.VisualOnly)damage.Apply(cast,cast.Motion.Distance,cast.Motion.Distance,true);
                    if(cast.Motion.Expired(Now)){Finish(cast,false);casts.RemoveAt(i);continue;}
                    Animate(cast);
                    cast.Motion.BeginFlight(Now);
                    if(cast.Motion.Phase==WorldCutPhase.Flying)
                    {
                        float target=cast.Motion.NextDistance(dt);
                        for(int step=0;step<4 && cast.Motion.Distance<target;step++)
                        {
                            float from=cast.Motion.Distance,to=Math.Min(target,from+WorldCutRules.Step),safe=to;
                            var collision=cast.VisualOnly?WorldCutFailure.None:damage.Step(cast.Origin,cast.Direction,cast.Right,from,to,out safe);
                            if(collision==WorldCutFailure.Capacity)break; // No budget => no unchecked advance or damage.
                            if(collision==WorldCutFailure.Protected||collision==WorldCutFailure.Unloaded){Report(cast.Owner,collision);Finish(cast,true);break;}
                            bool hit=!cast.VisualOnly && damage.Apply(cast,from,safe,false);
                            cast.Motion.Move(safe);
                            if(!cast.VisualOnly && (hit||collision==WorldCutFailure.Wall||safe>=WorldCutRules.Range))
                            { cast.Motion.Close(Now,safe);Publish(cast,3);break; }
                            if(cast.VisualOnly && safe>=WorldCutRules.Range){cast.Motion.Close(Now,safe);break;}
                        }
                    }
                    if(cast.Motion.TakeCloseImpact(Now) && !cast.VisualOnly)damage.Apply(cast,cast.Motion.Distance,cast.Motion.Distance,true);
                }
                catch(Exception error) { Finish(cast,true);Report(cast.Owner,WorldCutFailure.Invalid);Log.Warning("[SCIEPort:WorldCut] Cast cancelled: "+error.GetType().Name); }
                if(cast.Motion.Phase==WorldCutPhase.Finished){cast.Hits.Clear();casts.RemoveAt(i);}
            }
            retired.Clear();foreach(var pair in peers)if(Now-pair.Value.Seen>30&&pair.Value.ReadyAt<=Now&&pair.Value.PendingUntil<=Now)retired.Add(pair.Key);
            foreach(var key in retired)peers.Remove(key);
        }
        private void Animate(Cast cast)
        {
            double elapsed=Now-cast.Motion.Started;
            float t=(float)elapsed;
            float phase=t<WorldCutRules.Windup?.22f*t/WorldCutRules.Windup:t<WorldCutRules.SweepEnd?.22f+.38f*(t-WorldCutRules.Windup)/(WorldCutRules.SweepEnd-WorldCutRules.Windup):.60f+.40f*(t-WorldCutRules.SweepEnd)/.22f;
            var pose=AbyssMeleeMotion.Sample(0,phase);
            Vector3 order=new Vector3(pose.Pitch,pose.Yaw,pose.Roll),offset=new Vector3(pose.X,pose.Y,pose.Z);
            var fp=cast.Owner.Entity.FindComponent<ComponentFirstPersonModel>(false);if(fp!=null){fp.ItemRotationOrder=order;fp.ItemOffsetOrder=offset;}
            cast.Owner.ComponentCreatureModel.InHandItemRotationOrder=order;
            cast.Owner.ComponentCreatureModel.InHandItemOffsetOrder=offset*.35f;
        }
        private void Finish(Cast cast,bool cancel)
        {
            if(cancel&&cast.Motion.Phase!=WorldCutPhase.Finished)Publish(cast,5);
            Peer entry;if(peers.TryGetValue(cast.OwnerGuid,out entry)&&entry.MatchesEntity(cast.Owner.Entity.EntityId))entry.ActiveUntil=0;
            cast.Motion.Cancel();cast.Hits.Clear();cast.Owner.ComponentCreatureModel.InHandItemRotationOrder=Vector3.Zero;cast.Owner.ComponentCreatureModel.InHandItemOffsetOrder=Vector3.Zero;
            var fp=cast.Owner.Entity.FindComponent<ComponentFirstPersonModel>(false);if(fp!=null){fp.ItemRotationOrder=Vector3.Zero;fp.ItemOffsetOrder=Vector3.Zero;}
        }
        internal void Draw(Camera camera) { if(!disposed)scene.Draw(camera,casts,Now); }
        public void Dispose()
        {
            lock(queue){disposed=true;queue.Clear();}
            foreach(var cast in casts)
            {
                try { Finish(cast,false); }
                catch(Exception) { /* A retiring/disposed owner must not prevent releasing the rest. */ }
                finally { cast.Hits.Clear();cast.Owner=null; }
            }
            casts.Clear();peers.Clear();retired.Clear();
            try { damage.Clear(); } finally { scene.Dispose(); }
        }
        private static string Message(WorldCutFailure failure)
        {
            switch(failure)
            {
                case WorldCutFailure.Cooldown:return "断界斩冷却中";
                case WorldCutFailure.Busy:return "请先结束普攻或当前技能";
                case WorldCutFailure.Protected:return "领地保护：断界斩已停止";
                case WorldCutFailure.Unloaded:return "前方区块未就绪，断界斩已停止";
                case WorldCutFailure.Capacity:return "技能预算已满，请稍后再试";
                case WorldCutFailure.Timeout:return "主机响应超时，释放已取消";
                case WorldCutFailure.Wall:return "空间不足：弯月被墙体阻挡";
                default:return "当前无法释放断界斩";
            }
        }
    }
}

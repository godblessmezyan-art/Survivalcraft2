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
            internal Client Connection;internal Guid ConnectionToken;
            internal readonly WorldCutHitLedger<Entity> Hits=new WorldCutHitLedger<Entity>();
        }
        private sealed class Peer : WorldCutSession
        {
            internal double LastAttempt=-100,Seen;
            internal string Feedback;
            internal double FeedbackUntil;
            internal uint LocalCharge,ChargeSequence;
            internal double NextChargePing,ChargeReady,ChargeUntil;
            internal bool ChargeReleased;
            internal ComponentPlayer ChargeOwner;
            internal Client ChargeReply;internal Guid ChargeToken;
        }
        private struct Pending { internal ComponentPlayer Owner;internal Client Reply;internal Guid Token;internal WorldCutPackage Packet; }
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
            Peer e=Get(p,false);if(e!=null&&(e.PendingUntil>Now||e.ActiveUntil>Now||e.ChargeReleased))return true;
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
        private void Command(ComponentPlayer p,byte kind,uint sequence)
        {
            var packet=new WorldCutPackage{Kind=kind,Caster=p.Entity.EntityId,Sequence=sequence};
            if(Authority)Enqueue(p,null,packet);else CommonLib.Net?.QueuePackage(packet);
        }
        internal void BeginCharge(ComponentPlayer p)
        {
            if(disposed||!OwnerSafe(p)||Busy(p))return;
            var e=Get(p);if(e==null)return;
            if(e.LocalCharge!=0){if(Now>=e.NextChargePing){e.NextChargePing=Now+1;Command(p,WorldCutRules.ChargeBegin,e.LocalCharge);}return;}
            if(unchecked(++e.Sent)==0)++e.Sent;e.LocalCharge=e.Sent;e.NextChargePing=Now+1;
            Command(p,WorldCutRules.ChargeBegin,e.LocalCharge);
        }
        internal bool EndCharge(ComponentPlayer p,bool heavy)
        {
            if(disposed)return false;
            var e=Get(p,false);
            if(heavy)
            {
                var reason=OwnerFailure(p);
                if(reason!=WorldCutFailure.None)
                {
                    if(e!=null&&e.LocalCharge!=0){uint cancelled=e.LocalCharge;e.LocalCharge=0;Command(p,WorldCutRules.ChargeCancel,cancelled);}
                    Report(p,reason);return false;
                }
            }
            // A phone can first report Hold after the press began. Still route its heavy release
            // through the host's own charging clock, even if no begin was sampled locally.
            if(heavy&&(e==null||e.LocalCharge==0)){BeginCharge(p);e=Get(p,false);}
            if(e==null||e.LocalCharge==0){if(heavy)Report(p,e==null?WorldCutFailure.Capacity:Busy(p)?WorldCutFailure.Busy:WorldCutFailure.ChargeLost);return false;}
            uint sequence=e.LocalCharge;e.LocalCharge=0;
            if(heavy){e.PendingUntil=Now+2;e.Feedback="蓄力断界斩 · 主机确认中";e.FeedbackUntil=Now+2;}
            Command(p,heavy?WorldCutRules.ChargeRelease:WorldCutRules.ChargeCancel,sequence);return heavy;
        }
        private static void ClearCharge(Peer e)
        {e.ChargeSequence=0;e.ChargeReleased=false;e.ChargeOwner=null;e.ChargeReply=null;e.ChargeUntil=0;}
        internal void Enqueue(ComponentPlayer player,Client reply,WorldCutPackage packet)
        {
            lock(queue)if(!disposed&&queue.Count<WorldCutRules.MaxQueue)queue.Enqueue(new Pending { Owner=player,Reply=reply,Token=reply?.TokenId??Guid.Empty,Packet=packet.Snapshot() });
        }
        private bool OwnerSafe(ComponentPlayer p)=>OwnerFailure(p)==WorldCutFailure.None;
        private WorldCutFailure OwnerFailure(ComponentPlayer p)
        {
            if(p==null||p.Project!=project||!p.Entity.IsAddedToProject||p.ComponentHealth.Health<=0||!p.PlayerData.IsReadyForPlaying||Terrain.ExtractContents(p.ComponentMiner.ActiveBlockValue)!=AbyssReaperBlock.Index)return WorldCutFailure.Unavailable;
            var abyss=project.FindSubsystem<SubsystemAbyssReaper>(false);
            if(abyss?.Death?.Empowered(p)==true)return WorldCutFailure.Empowered;
            if(project.FindSubsystem<SCIEPort.Features.FourSaints.SubsystemFourSaints>(false)?.IsActionLocked(p)==true||abyss?.Space?.Busy(p)==true||abyss?.Death?.Busy(p)==true||abyss?.PortalTransferLocked(p)==true||abyss?.Horizon?.Busy(p)==true)return WorldCutFailure.Busy;
            return WorldCutFailure.None;
        }
        private void Reject(ComponentPlayer p,Client to,WorldCutPackage request,WorldCutFailure reason)
        {
            var e=Get(p);if(e!=null)e.PendingUntil=0;Report(p,reason);
            if(CommonLib.WorkType==WorkType.Server && to!=null)CommonLib.Net.QueuePackage(new WorldCutPackage { To=to,Kind=2,Reason=(byte)reason,Caster=request.Caster,Sequence=request.Sequence,Cooldown=(float)Remaining(p) });
        }
        private bool ArmCharge(Pending pending,Peer e)
        {
            var p=pending.Owner;if(!OwnerSafe(p)||clock.GameTimeDelta<=0||!e.NewRequest(pending.Packet.Sequence))return false;
            if(casts.Exists(c=>c.OwnerGuid==p.PlayerGuid)||e.ActiveUntil>Now)return false;
            float frequency=1.1f*(project.FindSubsystem<SCIEPort.Features.FourSaints.SubsystemFourSaints>(false)?.AttackFrequency(p)??1f);
            if(!WorldCutRules.Finite(frequency)||frequency<=0)return false;
            e.ChargeSequence=pending.Packet.Sequence;e.ChargeOwner=p;e.ChargeReply=pending.Reply;e.ChargeToken=pending.Token;e.ChargeReleased=false;
            e.ChargeReady=Now+WorldCutRules.ChargeTime/Math.Max(.1f,Math.Min(10,frequency));e.ChargeUntil=Now+WorldCutRules.ChargeLease;return true;
        }
        private void Accept(Pending pending)
        {
            var p=pending.Owner;var request=pending.Packet;
            if(!Authority || p==null || p.Project!=project || !p.Entity.IsAddedToProject || p.Entity.EntityId!=request.Caster)return;
            if(pending.Reply!=null&&(pending.Token!=pending.Reply.TokenId||pending.Reply.State<ClientState.ProjectLoaded||!ReferenceEquals(p.PlayerData.Client,pending.Reply)||pending.Reply.PlayerData?.ComponentPlayer!=p))return;
            var e=Get(p);if(e==null){Reject(p,pending.Reply,request,WorldCutFailure.Capacity);return;}
            e.Bind(request.Caster,pending.Reply?.TokenId??Guid.Empty);
            if(e.ChargeOwner!=null&&(e.ChargeOwner!=p||e.ChargeToken!=pending.Token))ClearCharge(e);
            if(request.Kind==WorldCutRules.ChargeBegin)
            {
                if(!OwnerSafe(p)||clock.GameTimeDelta<=0)return;
                if(e.ChargeSequence==request.Sequence){if(!e.ChargeReleased)e.ChargeUntil=Now+WorldCutRules.ChargeLease;return;}
                ArmCharge(pending,e);return;
            }
            if(request.Kind==WorldCutRules.ChargeCancel){if(WorldCutRules.Newer(request.Sequence,e.Processed))e.Processed=request.Sequence;if(e.ChargeSequence==request.Sequence)ClearCharge(e);return;}
            if(request.Kind==WorldCutRules.ChargeRelease)
            {
                if(e.ChargeSequence!=request.Sequence&&!ArmCharge(pending,e)){var reason=OwnerFailure(p);Reject(p,pending.Reply,request,reason==WorldCutFailure.None?WorldCutFailure.ChargeLost:reason);return;}
                if(e.ChargeSequence!=request.Sequence||e.ChargeOwner!=p||e.ChargeToken!=pending.Token||Now>=e.ChargeUntil){Reject(p,pending.Reply,request,WorldCutFailure.ChargeLost);return;}
                if(e.ChargeReleased)return;
                e.ChargeReleased=true;e.PendingUntil=Now+2;return;
            }
            if(request.Kind==4)
            {
                if(WorldCutRules.Newer(request.Sequence,e.Processed))e.Processed=request.Sequence;
                if(e.ChargeSequence==request.Sequence)ClearCharge(e);
                CancelCasts(p.PlayerGuid,request.Sequence,true);return;
            }
            if(request.Kind!=0)return;
            if(!e.NewRequest(request.Sequence))return;
            if(e.ChargeReleased){Reject(p,pending.Reply,request,WorldCutFailure.Busy);return;}
            ClearCharge(e);
            StartCast(pending,false);
        }
        private void StartCast(Pending pending,bool charged)
        {
            var p=pending.Owner;var request=pending.Packet;var e=Get(p);
            var unavailable=OwnerFailure(p);if(unavailable!=WorldCutFailure.None||clock.GameTimeDelta<=0){Reject(p,pending.Reply,request,unavailable==WorldCutFailure.None?WorldCutFailure.Unavailable:unavailable);return;}
            if(!charged&&e.ReadyAt>Now){Reject(p,pending.Reply,request,WorldCutFailure.Cooldown);return;}
            foreach(var existing in casts)if(existing.OwnerGuid==p.PlayerGuid){Reject(p,pending.Reply,request,WorldCutFailure.Busy);return;}
            if(casts.Count>=WorldCutRules.MaxCasts){Reject(p,pending.Reply,request,WorldCutFailure.Capacity);return;}
            // The request contains no trusted client origin/direction. Use replicated server eyes only.
            Vector3 direction=p.ComponentCreatureModel.EyeRotation.GetForwardVector();direction.Y=0;
            if(!WorldCutDamage.Finite(direction)){Reject(p,pending.Reply,request,WorldCutFailure.Aim);return;}
            // A horizontal cut still has a world heading when looking nearly straight up/down.
            // Read the replicated body heading; never accept a client-supplied direction.
            if(direction.LengthSquared()<.01f){direction=p.ComponentBody.Matrix.Forward;direction.Y=0;}
            if(!WorldCutDamage.Finite(direction)||direction.LengthSquared()<.01f){Reject(p,pending.Reply,request,WorldCutFailure.Aim);return;}
            direction=Vector3.Normalize(direction);Vector3 right=Vector3.Cross(direction,Vector3.UnitY);
            Vector3 origin=p.ComponentCreatureModel.EyePosition-Vector3.UnitY*.25f;
            float safe;var clearance=damage.Step(origin,direction,right,0,.05f,out safe);
            if(clearance!=WorldCutFailure.None){Reject(p,pending.Reply,request,clearance);return;}
            if(project.FindSubsystem<SubsystemAbyssReaper>(false)?.Space?.ReserveCut(p,request.Sequence)==false){Reject(p,pending.Reply,request,WorldCutFailure.Capacity);return;}
            var cast=new Cast { Owner=p,OwnerGuid=p.PlayerGuid,Sequence=request.Sequence,Origin=origin,Direction=direction,Right=right,Motion=new WorldCutMotion(Now),Connection=pending.Reply,ConnectionToken=pending.Token };
            casts.Add(cast);
            if(charged){e.PendingUntil=0;e.ActiveUntil=Now+WorldCutRules.Windup+WorldCutRules.Range/WorldCutRules.Speed+WorldCutRules.CloseEnd;}
            else e.Accept(Now);
            e.Feedback=charged?"蓄力重击 · 断界斩":"断界斩 · 向后聚能";e.FeedbackUntil=Now+.8;
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
            if(Authority&&e.LocalCharge!=0&&WorldCutRules.Newer(e.LocalCharge,e.Processed))e.Processed=e.LocalCharge;
            if(!Authority&&e.LocalCharge!=0)Command(p,WorldCutRules.ChargeCancel,e.LocalCharge);
            e.LocalCharge=0;ClearCharge(e);e.PendingUntil=0;e.ActiveUntil=0;CancelCasts(p.PlayerGuid,0,Authority);
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
                try { if(Authority)Accept(pending);else if(ReferenceEquals(pending.Reply,CommonLib.Net?.Server)&&pending.Token==(CommonLib.Net?.Server?.TokenId??Guid.Empty))Receive(pending.Packet); }
                catch(Exception error) { if(pending.Owner!=null)Reject(pending.Owner,pending.Reply,pending.Packet,WorldCutFailure.Execution);Log.Warning("[SCIEPort:WorldCut] Request rejected: "+error); }
            }
            if(Authority)foreach(var pair in peers)
            {
                var e=pair.Value;if(e.ChargeOwner==null)continue;var p=e.ChargeOwner;
                if(!OwnerSafe(p)||p.PlayerGuid!=pair.Key||Now>=e.ChargeUntil||e.ChargeReply!=null&&(e.ChargeReply.TokenId!=e.ChargeToken||e.ChargeReply.State<ClientState.ProjectLoaded||e.ChargeReply.PlayerData?.ComponentPlayer!=p)){ClearCharge(e);e.PendingUntil=0;continue;}
                if(e.ChargeReleased&&Now>=e.ChargeReady)
                {
                    var pending=new Pending{Owner=p,Reply=e.ChargeReply,Token=e.ChargeToken,Packet=new WorldCutPackage{Kind=WorldCutRules.ChargeRelease,Caster=p.Entity.EntityId,Sequence=e.ChargeSequence}};
                    ClearCharge(e);
                    try{StartCast(pending,true);}catch(Exception error){Reject(p,pending.Reply,pending.Packet,WorldCutFailure.Execution);Log.Warning("[SCIEPort:WorldCut] Heavy cast failed: "+error);}
                }
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
                    if(!OwnerSafe(cast.Owner)||cast.Owner.PlayerGuid!=cast.OwnerGuid||cast.Connection!=null&&(cast.Connection.TokenId!=cast.ConnectionToken||cast.Connection.State<ClientState.ProjectLoaded||cast.Connection.PlayerData?.ComponentPlayer!=cast.Owner)||Now-cast.Motion.Started>=WorldCutRules.Lifetime)
                    { if(Now-cast.Motion.Started>=WorldCutRules.Lifetime)Report(cast.Owner,WorldCutFailure.Capacity);Finish(cast,true);casts.RemoveAt(i);continue; }
                    // V2 residual damage belongs to its own four-second world-space rift job.
                    // The legacy 40-power closing impact is deliberately never dispatched.
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
                            if(!cast.VisualOnly)damage.Apply(cast,from,safe,false);
                            cast.Motion.Move(safe);
                            if(!cast.VisualOnly && (collision==WorldCutFailure.Wall||safe>=WorldCutRules.Range))
                            {
                                cast.Motion.Close(Now,safe);
                                project.FindSubsystem<SubsystemAbyssReaper>(false)?.Space?.AddCutRift(cast.Owner,cast.Sequence,cast.Origin,cast.Direction,safe);
                                Publish(cast,3);break;
                            }
                            if(cast.VisualOnly && safe>=WorldCutRules.Range){cast.Motion.Close(Now,safe);break;}
                        }
                    }
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
            project?.FindSubsystem<SubsystemAbyssReaper>(false)?.Space?.ReleaseCut(cast.Owner,cast.Sequence);
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
                case WorldCutFailure.Unavailable:return "角色未就绪或未持有镰刀，请恢复操作后重试";
                case WorldCutFailure.ChargeLost:return "重击蓄力已失效，请松开后重新长按";
                case WorldCutFailure.Empowered:return "夺魂赋刃期间只能普攻，结束后可重击";
                case WorldCutFailure.Aim:return "断界斩朝向无效，请调整视角后重试";
                case WorldCutFailure.Execution:return "断界斩执行异常，已记录游戏日志";
                case WorldCutFailure.Invalid:return "断界斩请求校验失败，请重新蓄力";
                default:return "断界斩请求未被接受，请重新蓄力";
            }
        }
    }
}

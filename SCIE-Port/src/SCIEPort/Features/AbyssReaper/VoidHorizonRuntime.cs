using System;
using System.Collections.Generic;
using Engine;
using Game;
using Game.NetWork;
using GameEntitySystem;
namespace SCIEPort.Features.AbyssReaper
{
    // Network callbacks only enqueue snapshots. Authority and effects run on the game update thread.
    internal sealed class VoidHorizonRuntime : IDisposable
    {
        private sealed class Cast
        {
            internal ComponentPlayer Owner;
            internal Guid OwnerGuid;
            internal Client Connection;
            internal uint Sequence;
            internal Vector3 Center,Direction;
            internal HorizonTimeline<Entity> Timeline;
            internal readonly HorizonCueTimeline Cues=new HorizonCueTimeline();
            internal double NextPull,NextSnapshot,LeaseUntil;
            internal bool VisualOnly;
        }
        private struct Pending { internal ComponentPlayer Owner; internal Client Reply; internal VoidHorizonPackage Packet; }
        private readonly SubsystemTerrain terrain;
        private readonly SubsystemBodies bodies;
        private readonly SubsystemTime clock;
        private readonly SubsystemPlayers players;
        private readonly SubsystemAudio audio;
        private readonly bool allowPassive;
        private readonly int quality;
        private readonly VoidHorizonScene scene=new VoidHorizonScene();
        private readonly Dictionary<Guid,HorizonSession> peers=new Dictionary<Guid,HorizonSession>();
        private readonly List<Cast> casts=new List<Cast>(VoidHorizonRules.MaxCasts);
        private readonly Queue<Pending> queue=new Queue<Pending>();
        private readonly List<Guid> retired=new List<Guid>();
        private readonly HashSet<Entity> passHits=new HashSet<Entity>();
        private uint revision;
        private bool disposed;
        private double Now => clock.GameTime;
        private bool Authority => CommonLib.WorkType==WorkType.Local || CommonLib.WorkType==WorkType.Server;
        internal VoidHorizonRuntime(SubsystemTerrain t,SubsystemBodies b,SubsystemTime c,SubsystemAudio sound,SubsystemPlayers ps,bool passive,int detail)
        { terrain=t;bodies=b;clock=c;audio=sound;players=ps;allowPassive=passive;quality=VoidSkillRules.Quality(detail); }
        private HorizonSession Get(ComponentPlayer p,bool create=false)
        {
            if(p==null || p.PlayerGuid==Guid.Empty)return null;
            HorizonSession peer;
            if(!peers.TryGetValue(p.PlayerGuid,out peer) && create && peers.Count<VoidHorizonRules.MaxPeers)
            { peer=new HorizonSession();peers.Add(p.PlayerGuid,peer); }
            if(peer!=null)peer.Seen=Now;
            return peer;
        }
        private Cast Find(ComponentPlayer p)
        {
            if(p==null)return null;
            foreach(var cast in casts)if(cast.Owner==p && cast.OwnerGuid==p.PlayerGuid)return cast;
            return null;
        }
        internal bool Busy(ComponentPlayer p) => p!=null && (Find(p)!=null || (Get(p)?.PendingUntil??0)>Now);
        internal double Remaining(ComponentPlayer p) => Math.Max(0,(Get(p)?.ReadyAt??0)-Now);
        internal string Feedback(ComponentPlayer p)
        {
            if(Find(p)!=null)return "虚夜 · 伤害免疫 · 可转动视角";
            var peer=Get(p);return peer!=null && peer.FeedbackUntil>Now?peer.Feedback:null;
        }
        private bool Actor(ComponentPlayer p)
            => p!=null && p.Project==terrain.Project && p.IsAddedToProject && p.PlayerGuid!=Guid.Empty
                && p.PlayerData.IsReadyForPlaying && p.ComponentHealth.Health>0 && p.ComponentSleep.SleepFactor<=0
                && p.ComponentRider.Mount==null && p.ComponentBody.ParentBody==null && p.ComponentBody.ChildBodies.Count==0
                && Terrain.ExtractContents(p.ComponentMiner.ActiveBlockValue)==AbyssReaperBlock.Index;
        private bool OwnerValid(Cast cast)
            => Actor(cast.Owner) && cast.Owner.PlayerGuid==cast.OwnerGuid
                && (cast.Connection==null || (cast.Connection.PlayerData?.ComponentPlayer==cast.Owner && cast.Connection.State>=ClientState.ProjectLoaded));
        // Native injury hooks run before ignoreInvulnerability. No persistent flag or healing hack.
        internal bool Protects(ComponentHealth health)
        {
            if(disposed || health==null)return false;
            foreach(var cast in casts)
                if(cast.Owner.ComponentHealth==health && OwnerValid(cast) && VoidHorizonRules.Live(Now-cast.Timeline.Started)
                    && (!cast.VisualOnly || Now<cast.LeaseUntil))return true;
            return false;
        }
        internal bool TryCast(ComponentPlayer p,out string reason)
        {
            reason="当前无法展开虚夜";
            if(disposed || !Actor(p) || !p.PlayerData.IsMainPlayer)return false;
            if(Busy(p)){reason="虚夜领域正在展开";return false;}
            if(Remaining(p)>0){reason="终焉坍缩冷却中";return false;}
            var peer=Get(p,true);if(peer==null){reason="领域容量已满";return false;}
            peer.Bind(p.Entity.EntityId,Guid.Empty);
            uint sequence=peer.Request(Now);if(sequence==0)return false;
            var packet=new VoidHorizonPackage { Kind=VoidHorizonRules.Request,Caster=p.Entity.EntityId,Sequence=sequence };
            if(Authority)Accept(new Pending { Owner=p,Packet=packet });
            else if(CommonLib.WorkType==WorkType.Client && CommonLib.Net!=null)CommonLib.Net.QueuePackage(packet);
            else { peer.PendingUntil=0;return false; }
            reason=Authority?(Find(p)!=null?"领域展开 · 虚夜":peer.Feedback):"正在向主机请求展开虚夜";
            return !Authority || Find(p)!=null;
        }
        internal void Enqueue(ComponentPlayer player,Client reply,VoidHorizonPackage packet)
        {
            lock(queue)if(!disposed && queue.Count<VoidHorizonRules.MaxQueue)queue.Enqueue(new Pending { Owner=player,Reply=reply,Packet=packet.Snapshot() });
        }
        private uint NextRevision() { if(unchecked(++revision)==0)++revision;return revision; }
        private void Reject(Pending pending,string message)
        {
            var peer=Get(pending.Owner,true);
            if(peer!=null){peer.PendingUntil=0;peer.Feedback=message;peer.FeedbackUntil=Now+2;}
            if(pending.Reply!=null && CommonLib.WorkType==WorkType.Server)
                CommonLib.Net.QueuePackage(new VoidHorizonPackage { To=pending.Reply,Kind=VoidHorizonRules.Reject,Caster=pending.Packet.Caster,Sequence=pending.Packet.Sequence,Revision=NextRevision(),Cooldown=(float)Remaining(pending.Owner) });
        }
        private void Accept(Pending pending)
        {
            var p=pending.Owner;var request=pending.Packet;
            if(!Authority || p==null || p.Project!=terrain.Project || !p.IsAddedToProject || p.Entity.EntityId!=request.Caster)return;
            // Recheck ownership after queueing: a connection may disconnect or replace its player.
            if(pending.Reply!=null && (pending.Reply.PlayerData?.ComponentPlayer!=p || pending.Reply.State<ClientState.ProjectLoaded))return;
            var peer=Get(p,true);if(peer==null){Reject(pending,"领域容量已满");return;}
            peer.Bind(request.Caster,pending.Reply?.TokenId??Guid.Empty);
            if(request.Kind==VoidHorizonRules.Cancel)
            {
                if(WorldCutRules.Newer(request.Sequence,peer.Processed))peer.Processed=request.Sequence;
                var active=Find(p);if(active!=null && active.Sequence==request.Sequence)Stop(active,true);
                peer.PendingUntil=0;return;
            }
            if(!peer.Process(request.Sequence))return;
            var subsystem=terrain.Project.FindSubsystem<SubsystemAbyssReaper>(false);
            if(!Actor(p) || clock.GameTimeDelta<=0 || subsystem?.OtherSkillBusy(p)==true
                || p.Project.FindSubsystem<SCIEPort.Features.FourSaints.SubsystemFourSaints>(false)?.IsActionLocked(p)==true)
            {Reject(pending,"当前无法展开虚夜");return;}
            if(Remaining(p)>0){Reject(pending,"终焉坍缩冷却中");return;}
            if(Find(p)!=null || casts.Count>=VoidHorizonRules.MaxCasts || bodies.Bodies.Count>512){Reject(pending,"领域容量已满");return;}
            Vector3 at=p.ComponentBody.Position,forward=p.ComponentCreatureModel.EyeRotation.GetForwardVector();forward.Y=0;
            if(!WorldCutDamage.Finite(at) || !WorldCutDamage.Finite(forward) || forward.LengthSquared()<.01f
                || !VoidPortalSafety.Ready(terrain,Terrain.ToCell(at.X),Terrain.ToCell(at.Z)) || !VoidPortalSafety.Unprotected(Terrain.ToCell(at.X),Terrain.ToCell(at.Z)))
            {Reject(pending,"区块未就绪或领地受保护");return;}
            // Origin, direction, damage and immunity are chosen by authority, never the request.
            var cast=new Cast { Owner=p,OwnerGuid=p.PlayerGuid,Connection=pending.Reply,Sequence=request.Sequence,Center=at,Direction=Vector3.Normalize(forward),Timeline=new HorizonTimeline<Entity>(Now),NextPull=Now+VoidSkillRules.DomainStart,NextSnapshot=Now+.5 };
            peer.ReadyAt=Now+VoidSkillRules.HorizonCooldown;peer.PendingUntil=0;
            casts.Add(cast);LockInput(p);Present(cast,0);Publish(cast,VoidHorizonRules.Snapshot);
        }
        private void Publish(Cast cast,byte kind)
        {
            if(CommonLib.WorkType!=WorkType.Server || cast.Connection==null)return;
            CommonLib.Net.QueuePackage(new VoidHorizonPackage { To=cast.Connection,Kind=kind,Caster=cast.Owner.Entity.EntityId,Sequence=cast.Sequence,Revision=NextRevision(),
                Origin=cast.Center,Direction=cast.Direction,Elapsed=(float)Math.Max(0,Math.Min(VoidSkillRules.HorizonDuration,Now-cast.Timeline.Started)),Cooldown=(float)Remaining(cast.Owner) });
        }
        private void Receive(VoidHorizonPackage packet)
        {
            if(Authority || !VoidHorizonRules.ValidSnapshot(packet.Elapsed,packet.Cooldown))return;
            ComponentPlayer p=null;
            foreach(var candidate in players.ComponentPlayers)if(candidate.PlayerData.IsMainPlayer && candidate.Entity.EntityId==packet.Caster){p=candidate;break;}
            if(p==null)return;
            var peer=Get(p);if(peer==null)return;
            if(packet.Kind==VoidHorizonRules.Snapshot && (!WorldCutDamage.Finite(packet.Origin) || !WorldCutDamage.Finite(packet.Direction)
                || Math.Abs(packet.Direction.Y)>.01f || packet.Direction.LengthSquared()<.98f || packet.Direction.LengthSquared()>1.02f))return;
            if(!peer.Receive(packet.Sequence,packet.Revision,packet.Kind))return;
            peer.ReadyAt=Math.Max(peer.ReadyAt,Now+packet.Cooldown);
            var cast=Find(p);
            if(packet.Kind!=VoidHorizonRules.Snapshot)
            {
                if(cast!=null)Stop(cast,false);
                if(packet.Kind==VoidHorizonRules.Reject){peer.Feedback=packet.Cooldown>0?"终焉坍缩冷却中":"主机未允许展开虚夜";peer.FeedbackUntil=Now+2;}
                return;
            }
            if(!Actor(p) || packet.Elapsed>=VoidSkillRules.HorizonDuration)
            {
                CancelSequence(p,peer);
                if(cast!=null)Stop(cast,false);
                return;
            }
            if(cast==null)
            {
                if(casts.Count>=VoidHorizonRules.MaxCasts)return;
                cast=new Cast { Owner=p,OwnerGuid=p.PlayerGuid,Sequence=packet.Sequence,Center=packet.Origin,Direction=packet.Direction,Timeline=new HorizonTimeline<Entity>(Now-packet.Elapsed),VisualOnly=true };
                casts.Add(cast);
            }
            // Catch up only: reordering must never rewind or extend the domain lifetime.
            if(packet.Elapsed>Now-cast.Timeline.Started)cast.Timeline=new HorizonTimeline<Entity>(Now-packet.Elapsed);
            cast.LeaseUntil=Now+VoidHorizonRules.Lease;
        }
        private bool Target(Cast cast,ComponentBody body)
        {
            if(body==null || body.Entity==cast.Owner.Entity || !body.IsAddedToProject || body.Project!=cast.Owner.Project || body.ParentBody!=null || body.ChildBodies.Count>0)return false;
            var creature=body.Entity.FindComponent<ComponentCreature>(false);
            if(creature==null || creature.ComponentHealth.Health<=0)return false;
            bool player=body.Entity.FindComponent<ComponentPlayer>(false)!=null;
            bool hostile=creature.Category==CreatureCategory.LandPredator || creature.Category==CreatureCategory.WaterPredator;
            if(!VoidHorizonRules.TargetAllowed(player,hostile,allowPassive))return false;
            Vector3 center=body.BoundingBox.Center(),delta=center-cast.Center;
            return WorldCutDamage.Finite(center) && VoidSkillRules.InDomain(delta.X,delta.Y,delta.Z,12f);
        }
        private void Affect(Cast cast,int mode)
        {
            if(!Authority || cast.VisualOnly)return;
            int scanned=0,accepted=0,rays=0;passHits.Clear();
            foreach(var body in bodies.Bodies)
            {
                if(++scanned>512 || accepted>=24)break;
                if(!Target(cast,body) || passHits.Contains(body.Entity))continue;
                if(++rays>64)break;
                if(!VoidCombatSafety.ClearPath(terrain,cast.Center+Vector3.UnitY,body.BoundingBox.Center(),48f))continue;
                if(mode==2 && !cast.Timeline.AddFinalTarget(body.Entity,24))continue;
                passHits.Add(body.Entity);accepted++;
                Vector3 delta=cast.Center-body.Position;
                if(mode==0)
                {
                    delta.Y=0;
                    if(delta.LengthSquared()>.25f && WorldCutDamage.Finite(body.Velocity) && body.Velocity.LengthSquared()<100f)
                        body.Velocity+=Vector3.Normalize(delta)*.35f;
                }
                else
                {
                    Vector3 target=body.BoundingBox.Center(),direction=target-cast.Center;
                    try { ComponentMiner.AttackBody(body,cast.Owner,target,direction.LengthSquared()>.01f?Vector3.Normalize(direction):Vector3.UnitZ,mode==2?VoidSkillRules.HorizonFinalPower:VoidSkillRules.HorizonTickPower,false); }
                    catch(Exception error){Log.Warning("[SCIEPort:Abyss] Horizon target failed: "+error.GetType().Name);}
                }
            }
        }
        internal static void LockInput(ComponentPlayer p)
        {
            var input=p.ComponentInput;
            input.m_playerInput.Move=Vector3.Zero;input.m_playerInput.SneakMove=Vector3.Zero;input.m_playerInput.Jump=false;
            input.m_playerInput.Hit=null;input.m_playerInput.Dig=null;input.m_playerInput.Aim=null;input.m_playerInput.Interact=null;
            p.ComponentLocomotion.WalkOrder=Vector2.Zero;p.ComponentLocomotion.FlyOrder=Vector3.Zero;
            p.ComponentLocomotion.SwimOrder=Vector3.Zero;p.ComponentLocomotion.JumpOrder=0;
        }
        internal void Update()
        {
            if(disposed)return;
            for(int i=0;i<VoidHorizonRules.RequestsPerFrame;i++)
            {
                Pending next;lock(queue){if(queue.Count==0)break;next=queue.Dequeue();}
                try { if(Authority)Accept(next);else Receive(next.Packet); }
                catch(Exception error){Log.Warning("[SCIEPort:Abyss] Horizon packet rejected: "+error.GetType().Name);}
            }
            for(int i=casts.Count-1;i>=0;i--)
            {
                var cast=casts[i];
                try
                {
                    if(!OwnerValid(cast) || (cast.VisualOnly && Now>=cast.LeaseUntil)){Cancel(cast.Owner);continue;}
                    double elapsed=Now-cast.Timeline.Started;
                    if(elapsed<0 || double.IsNaN(elapsed)){Stop(cast,Authority);continue;}
                    LockInput(cast.Owner);
                    if(!cast.VisualOnly)
                    {
                        if(cast.Timeline.TakeTick(Now)>=0)Affect(cast,1);
                        if(elapsed>=VoidSkillRules.DomainStart && elapsed<VoidSkillRules.DomainEnd && Now>=cast.NextPull){cast.NextPull=Now+.25;Affect(cast,0);}
                        if(cast.Timeline.TakeFinal(Now))Affect(cast,2);
                        if(Now>=cast.NextSnapshot){cast.NextSnapshot=Now+.5;Publish(cast,VoidHorizonRules.Snapshot);}
                    }
                    Present(cast,elapsed);
                    if(elapsed>=VoidSkillRules.HorizonDuration)Stop(cast,Authority);
                }
                catch(Exception error){Log.Warning("[SCIEPort:Abyss] Horizon cancelled: "+error.GetType().Name);Stop(cast,Authority);}
            }
            retired.Clear();
            foreach(var pair in peers)
            {
                var peer=pair.Value;
                if(peer.PendingUntil>0 && Now>=peer.PendingUntil)
                {
                    foreach(var p in players.ComponentPlayers)if(p.PlayerGuid==pair.Key){Cancel(p);break;}
                    peer.PendingUntil=0;peer.Feedback="主机响应超时，虚夜已取消";peer.FeedbackUntil=Now+2;
                }
                if(peer.ReadyAt<=Now && peer.PendingUntil==0 && Now-peer.Seen>30)retired.Add(pair.Key);
            }
            foreach(var id in retired)peers.Remove(id);
        }
        private void Present(Cast cast,double elapsed)
        {
            if(!cast.Owner.PlayerData.IsMainPlayer)return;
            int cue=cast.Cues.Take(elapsed);if(cue<0 || audio==null)return;
            string[] names={"Wake","Rift","Collapse","Burst"};
            try { audio.PlaySound("Audio/Abyss/Horizon"+names[cue],.8f,0f,cast.Owner.ComponentBody.Position,36f,false); }
            catch(Exception error){Log.Warning("[SCIEPort:Abyss] Horizon sound unavailable: "+error.GetType().Name);}
        }
        // The complete domain owns only its caster's viewport, not the shared world's sky.
        internal float SkyBlend => 0f;
        internal void Draw(Camera camera)
        {
            foreach(var cast in casts)
                if(camera.GameWidget.PlayerData.ComponentPlayer==cast.Owner && OwnerValid(cast) && (!cast.VisualOnly || Now<cast.LeaseUntil))
                    scene.Draw(camera,cast.Direction,(float)(Now-cast.Timeline.Started),quality);
        }
        internal void Cancel(ComponentPlayer p)
        {
            if(disposed || p==null)return;
            var peer=Get(p);var cast=Find(p);
            if(!Authority && peer!=null && (peer.PendingUntil>0 || cast!=null) && peer.Cancelled!=peer.Sent)
                CancelSequence(p,peer);
            if(peer!=null)peer.PendingUntil=0;
            if(cast!=null)Stop(cast,Authority);
        }
        private void Stop(Cast cast,bool publish)
        {
            // Remove protection first even if the outgoing cancellation fails.
            casts.Remove(cast);cast.Timeline.Cancel();
            if(cast.VisualOnly)
            {
                var peer=Get(cast.Owner);
                if(peer!=null)peer.Cancelled=cast.Sequence;
            }
            if(publish)Publish(cast,VoidHorizonRules.End);
        }
        private void CancelSequence(ComponentPlayer p,HorizonSession peer)
        {
            if(peer.Cancelled==peer.Sent)return;
            peer.Cancelled=peer.Sent;peer.PendingUntil=0;
            CommonLib.Net?.QueuePackage(new VoidHorizonPackage { Kind=VoidHorizonRules.Cancel,Caster=p.Entity.EntityId,Sequence=peer.Sent });
        }
        public void Dispose()
        {
            lock(queue){disposed=true;queue.Clear();}
            foreach(var cast in casts)cast.Timeline.Cancel();
            casts.Clear();peers.Clear();retired.Clear();passHits.Clear();scene.Dispose();
        }
    }
}

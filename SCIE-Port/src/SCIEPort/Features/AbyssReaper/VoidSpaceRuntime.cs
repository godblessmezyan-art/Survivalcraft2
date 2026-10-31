using System;
using System.Collections.Generic;
using Engine;
using Game;
using Game.NetWork;
using GameEntitySystem;
namespace SCIEPort.Features.AbyssReaper
{
    // All mutation is on the subsystem update thread. Presentation never dispatches damage or movement.
    internal sealed class VoidSpaceRuntime:IDisposable
    {
        internal sealed class Effect
        {
            internal ComponentPlayer Owner;internal Guid OwnerGuid;internal byte Skill;internal uint Sequence,Seed;
            internal Vector3 From,To,Direction;internal double Started,Expires;internal bool VisualOnly;internal VoidSpaceDamage.Job Damage;
        }
        internal sealed class ShatterCast
        {
            internal ComponentPlayer Owner;internal Guid OwnerGuid;internal uint Sequence,Seed;
            internal Vector3 Center,Direction;internal double Started;internal bool VisualOnly,Impacted;
            internal VoidShatterScene.Geometry Geometry;
        }
        private sealed class Peer
        {
            internal ComponentPlayer Player;internal ushort Entity;internal Guid Token;internal uint Sent;
            internal readonly uint[] Processed=new uint[4],Visual=new uint[4],Ended=new uint[4];
            internal readonly double[] ReadyAt=new double[4];
            internal uint PendingSequence;internal byte PendingSkill;internal double PendingUntil,NextRequest,NextClock,Seen;
            internal string Feedback;internal double FeedbackUntil;
        }
        private sealed class Transfer
        {
            internal ComponentPlayer Player;internal Guid OwnerGuid,Token;internal uint Sequence;
            internal Vector3 Origin,Target,Direction;internal double Until,NextSend;internal bool Committed,Finished,TimedOut;
        }
        private sealed class FallGuard{internal ComponentPlayer Player;internal Guid OwnerGuid;internal double Started,Until;}
        private sealed class Pose{internal double Started,Until;internal Vector3 Rotation,Offset;}
        private struct Pending{internal ComponentPlayer Player;internal Client Client;internal Guid Token;internal VoidSpacePackage Packet;}
        private readonly SubsystemTerrain terrain;private readonly SubsystemTime clock;private readonly SubsystemPlayers players;private readonly SubsystemAudio audio;
        private readonly VoidBlinkSafety blinkSafety;private readonly VoidSpaceDamage damage;private readonly int quality;
        private readonly Queue<Pending> queue=new Queue<Pending>();private readonly List<VoidSpacePackage> deferred=new List<VoidSpacePackage>();
        private readonly Dictionary<Guid,Peer> peers=new Dictionary<Guid,Peer>();
        private readonly Dictionary<ComponentPlayer,uint> cutReservations=new Dictionary<ComponentPlayer,uint>();
        private readonly Dictionary<Guid,Transfer> transfers=new Dictionary<Guid,Transfer>(),receipts=new Dictionary<Guid,Transfer>();
        private readonly Dictionary<Guid,FallGuard> fall=new Dictionary<Guid,FallGuard>();
        private readonly Dictionary<ComponentPlayer,Pose> poses=new Dictionary<ComponentPlayer,Pose>();
        private readonly List<ComponentPlayer> retiredPlayers=new List<ComponentPlayer>();private readonly List<Guid> retired=new List<Guid>();
        private readonly List<Effect> effects=new List<Effect>();private readonly List<ShatterCast> shatters=new List<ShatterCast>();
        private readonly List<VoidSpaceDamage.Job> impacts=new List<VoidSpaceDamage.Job>();
        private readonly VoidRiftScene riftScene=new VoidRiftScene();private readonly VoidShatterScene shatterScene=new VoidShatterScene();
        private Transfer clientTransfer;private ComponentPlayer clientPlayer;private bool disposed,hasClock;private Guid serverToken;
        private double clockOffset,lastServerEstimate,nextClock=-100;private int effectCursor;
        private double Now => clock.GameTime;
        private bool Authority => CommonLib.WorkType==WorkType.Local||CommonLib.WorkType==WorkType.Server;
        internal VoidSpaceRuntime(SubsystemTerrain terrain,SubsystemBodies bodies,SubsystemTime clock,SubsystemAudio audio,int quality)
        {
            this.terrain=terrain;this.clock=clock;this.audio=audio;this.quality=VoidSkillRules.Quality(quality);
            players=terrain.Project.FindSubsystem<SubsystemPlayers>(false);blinkSafety=new VoidBlinkSafety(terrain);damage=new VoidSpaceDamage(terrain,bodies);
        }
        private bool Actor(ComponentPlayer p,bool held=true)
        {
            if(p==null||p.Project!=terrain.Project||!p.IsAddedToProject||p.PlayerGuid==Guid.Empty||!p.PlayerData.IsReadyForPlaying||p.ComponentHealth.Health<=0)return false;
            if(CommonLib.WorkType==WorkType.Server&&!p.PlayerData.IsMainPlayer)
            {
                if(CommonLib.Net==null)return false;
                Client connection=p.PlayerData.Client;
                if(connection==null||connection.State<ClientState.ProjectLoaded||connection.PlayerData?.ComponentPlayer!=p)return false;
            }
            return !held||(p.ComponentSleep.SleepFactor<=0&&p.ComponentRider.Mount==null&&p.ComponentBody.ParentBody==null&&p.ComponentBody.ChildBodies.Count==0&&Terrain.ExtractContents(p.ComponentMiner.ActiveBlockValue)==AbyssReaperBlock.Index&&p.Project.FindSubsystem<SCIEPort.Features.FourSaints.SubsystemFourSaints>(false)?.IsActionLocked(p)!=true);
        }
        private Peer Get(ComponentPlayer p)
        {
            if(p==null)return null;Peer peer;
            if(!peers.TryGetValue(p.PlayerGuid,out peer)){if(peers.Count>=64)return null;peer=new Peer();peers.Add(p.PlayerGuid,peer);}
            Guid token=Authority?(CommonLib.WorkType==WorkType.Server&&!p.PlayerData.IsMainPlayer?p.PlayerData.Client?.TokenId??Guid.Empty:Guid.Empty):(CommonLib.Net?.Server?.TokenId??Guid.Empty);
            if(peer.Player!=p||peer.Entity!=p.Entity.EntityId||peer.Token!=token)
            {
                peer.Player=p;peer.Entity=p.Entity.EntityId;peer.Token=token;peer.PendingUntil=0;peer.PendingSequence=0;
                Array.Clear(peer.Processed,0,4);Array.Clear(peer.Visual,0,4);Array.Clear(peer.Ended,0,4);
            }
            peer.Seen=Now;return peer;
        }
        internal double Remaining(ComponentPlayer p,byte skill){var peer=Get(p);return peer==null||skill<1||skill>3?0:Math.Max(0,peer.ReadyAt[skill]-Now);}
        internal string Feedback(ComponentPlayer p){var peer=Get(p);return peer!=null&&peer.FeedbackUntil>Now?peer.Feedback:null;}
        private void Feedback(ComponentPlayer p,string text)
        {
            var peer=Get(p);if(peer==null)return;peer.Feedback=text;peer.FeedbackUntil=Now+2;
            if(p.PlayerData.IsMainPlayer)p.ComponentGui?.DisplaySmallMessage(text,new Color(205,173,239),false,false);
        }
        internal bool Busy(ComponentPlayer p)
        {
            if(p==null)return false;var peer=Get(p);if(peer!=null&&peer.PendingUntil>Now)return true;
            Pose pose;if(poses.TryGetValue(p,out pose)&&pose.Until>Now)return true;
            if(transfers.ContainsKey(p.PlayerGuid)||clientPlayer==p&&clientTransfer!=null&&!clientTransfer.Finished&&!clientTransfer.TimedOut)return true;
            foreach(var cast in shatters)if(cast.Owner==p&&Now<cast.Started+VoidShatterRules.Duration)return true;
            return false;
        }
        internal bool InputLocked(ComponentPlayer p) => p!=null&&(transfers.ContainsKey(p.PlayerGuid)||clientPlayer==p&&clientTransfer!=null&&!clientTransfer.Finished&&!clientTransfer.TimedOut);
        private bool Castable(ComponentPlayer p)
            => Actor(p)&&clock.GameTimeDelta>0&&p.Project.FindSubsystem<SCIEPort.Features.FourSaints.SubsystemFourSaints>(false)?.IsActionLocked(p)!=true
                &&p.Project.FindSubsystem<SubsystemAbyssReaper>(false)?.Horizon?.Busy(p)!=true
                &&p.Project.FindSubsystem<SubsystemAbyssReaper>(false)?.PortalTransferLocked(p)!=true
                &&p.Project.FindSubsystem<SubsystemAbyssReaper>(false)?.WorldCut?.Busy(p)!=true;
        internal bool Request(ComponentPlayer p,byte skill,out string reason)
        {
            reason="当前无法施展空间技能";if(disposed||(skill!=2&&skill!=3)||!Castable(p)||!p.PlayerData.IsMainPlayer)return false;
            var peer=Get(p);if(peer==null){reason="空间技能容量已满";return false;}
            if(Busy(p)){reason="请先结束当前空间技能";return false;}
            if(Remaining(p,skill)>0){reason=Name(skill)+"冷却中";return false;}
            if(!Authority&&CommonLib.Net?.Server==null){reason="尚未连接主机";return false;}
            if(skill==2&&clientTransfer!=null&&(clientTransfer.Finished||clientTransfer.TimedOut)){clientTransfer=null;clientPlayer=null;}
            if(unchecked(++peer.Sent)==0)++peer.Sent;
            peer.PendingSequence=peer.Sent;peer.PendingSkill=skill;peer.PendingUntil=Now+3;
            var packet=new VoidSpacePackage{Kind=0,Skill=skill,Caster=p.Entity.EntityId,Sequence=peer.Sent,Direction=p.ComponentCreatureModel.EyeRotation.GetForwardVector()};
            if(Authority)Enqueue(p,null,packet);else{Ping(p);CommonLib.Net.QueuePackage(packet);}
            reason=Name(skill)+" · 正在请求主机";return true;
        }
        internal void Enqueue(ComponentPlayer p,Client client,VoidSpacePackage packet)
        {lock(queue)if(!disposed&&queue.Count<VoidSpaceRules.MaxQueue)queue.Enqueue(new Pending{Player=p,Client=client,Token=client?.TokenId??Guid.Empty,Packet=packet.Copy()});}
        private void Ping(ComponentPlayer p)
        {
            if(Authority||CommonLib.Net?.Server==null||Time.RealTime<nextClock)return;
            nextClock=Time.RealTime+2;CommonLib.Net.QueuePackage(new VoidSpacePackage{Kind=1,Caster=p.Entity.EntityId,Echo=Time.RealTime});
        }
        private void Send(ComponentPlayer p,VoidSpacePackage packet)
        {
            packet.Caster=p.Entity.EntityId;packet.Stamp=Now;
            if(p.PlayerData.IsMainPlayer)return;
            packet.To=p.PlayerData.Client;if(packet.To!=null&&CommonLib.WorkType==WorkType.Server)CommonLib.Net?.QueuePackage(packet);
        }
        private void Broadcast(VoidSpacePackage packet)
        {
            packet.Stamp=Now;if(CommonLib.WorkType!=WorkType.Server||players==null)return;
            foreach(var p in players.ComponentPlayers)
                if(!p.PlayerData.IsMainPlayer&&Actor(p,false)&&Vector3.DistanceSquared(p.ComponentBody.Position,packet.Origin)<=96*96)
                {var copy=packet.Copy();copy.To=p.PlayerData.Client;if(copy.To!=null)CommonLib.Net?.QueuePackage(copy);}
        }
        private void Reject(ComponentPlayer p,VoidSpacePackage request,byte reason,string text=null)
        {
            var peer=Get(p);if(peer!=null)peer.PendingUntil=0;
            Feedback(p,text??Message(reason));Send(p,new VoidSpacePackage{Kind=9,Skill=request.Skill,Sequence=request.Sequence,Reason=reason,Cooldown=(float)Remaining(p,request.Skill)});
        }
        private static bool Direction(Vector3 v) => WorldCutDamage.Finite(v)&&v.LengthSquared()>=.9f&&v.LengthSquared()<=1.1f;
        private void Process(Pending pending)
        {
            var p=pending.Player;var packet=pending.Packet;
            if(!Authority||!Actor(p,packet.Kind!=1&&packet.Kind!=8)||p.Entity.EntityId!=packet.Caster)return;
            if(pending.Client!=null&&(pending.Token!=pending.Client.TokenId||pending.Client.State<ClientState.ProjectLoaded||!ReferenceEquals(pending.Client,p.PlayerData.Client)||pending.Client.PlayerData?.ComponentPlayer!=p))return;
            var peer=Get(p);if(peer==null)return;
            if(packet.Kind==1)
            {
                if(Now<peer.NextClock)return;peer.NextClock=Now+.5;
                Send(p,new VoidSpacePackage{Kind=1,Echo=packet.Echo});Sync(p);return;
            }
            if(packet.Kind==8&&packet.Skill>=1&&packet.Skill<=3)
            {
                if(packet.Skill==2&&transfers.ContainsKey(p.PlayerGuid)){Signal(p,packet);return;}
                if(packet.Sequence==peer.Processed[packet.Skill]||WorldCutRules.Newer(packet.Sequence,peer.Processed[packet.Skill]))
                {Cancel(p);peer.Ended[packet.Skill]=packet.Sequence;peer.Processed[packet.Skill]=packet.Sequence;}
                return;
            }
            if(packet.Skill!=2&&packet.Skill!=3)return;
            if(packet.Kind==4||packet.Kind==6){Signal(p,packet);return;}
            if(packet.Kind!=0||!WorldCutRules.Newer(packet.Sequence,peer.Processed[packet.Skill]))return;
            peer.Processed[packet.Skill]=packet.Sequence;
            if(Now<peer.NextRequest){Reject(p,packet,2,"操作过快，请稍后重试");return;}peer.NextRequest=Now+.15;
            if(!Castable(p)||Active(p)){Reject(p,packet,2);return;}
            if(peer.ReadyAt[packet.Skill]>Now){Reject(p,packet,1,Name(packet.Skill)+"冷却中");return;}
            if(!Direction(packet.Direction)){Reject(p,packet,6);return;}
            Vector3 direction=Vector3.Normalize(packet.Direction);
            if(packet.Skill==2)BeginBlink(p,packet,direction);else BeginShatter(p,packet,direction);
        }
        // Pending request belongs to the requester; it must not reject its own authoritative acceptance.
        private bool Active(ComponentPlayer p)
        {
            if(transfers.ContainsKey(p.PlayerGuid))return true;
            Pose pose;if(poses.TryGetValue(p,out pose)&&pose.Until>Now)return true;
            foreach(var cast in shatters)if(cast.Owner==p&&Now<cast.Started+VoidShatterRules.Duration)return true;
            return false;
        }
        private void BeginBlink(ComponentPlayer p,VoidSpacePackage request,Vector3 direction)
        {
            if(transfers.Count>=VoidSpaceRules.MaxTransfers||effects.Count+transfers.Count+cutReservations.Count>=VoidSpaceRules.MaxEffects){Reject(p,request,7);return;}
            Vector3 target;string reason;
            if(!blinkSafety.TryFind(p,direction,out target,out reason)){Reject(p,request,Failure(reason),reason);return;}
            Vector3 origin=p.ComponentBody.Position;
            if(p.PlayerData.IsMainPlayer){CompleteBlink(p,request.Sequence,origin,target,direction);return;}
            var transfer=new Transfer{Player=p,OwnerGuid=p.PlayerGuid,Token=p.PlayerData.Client.TokenId,Sequence=request.Sequence,Origin=origin,Target=target,Direction=direction,Until=Now+VoidSpaceRules.TransferLease};
            transfers[p.PlayerGuid]=transfer;Get(p).PendingUntil=0;SendTransfer(transfer,3);
        }
        private void CompleteBlink(ComponentPlayer p,uint sequence,Vector3 origin,Vector3 target,Vector3 direction)
        {
            VoidBlinkSafety.SetPosition(p,target);ArmFall(p);
            var peer=Get(p);peer.ReadyAt[2]=Now+VoidSpaceRules.BlinkCooldown;peer.PendingUntil=0;
            AddEffect(p,2,sequence,origin,target,direction,Now,unchecked(sequence*2654435761u),false);
            StartPose(p,.22);Feedback(p,"无界折跃 · 双生裂隙");
            Broadcast(EffectPacket(p,2,sequence,origin,target,direction,Now,unchecked(sequence*2654435761u)));
            Sound(p,"BlinkShift",target);
        }
        private void BeginShatter(ComponentPlayer p,VoidSpacePackage request,Vector3 direction)
        {
            if(shatters.Count>=VoidSpaceRules.MaxCasts){Reject(p,request,7);return;}
            Vector3 eye=p.ComponentCreatureModel.EyePosition,end=eye+direction*VoidShatterRules.Range;
            if(!WorldCutDamage.Finite(eye)){Reject(p,request,6);return;}
            var wall=terrain.Raycast(eye,end,false,true,(v,d)=>BlocksManager.Blocks[Terrain.ExtractContents(v)].IsCollidable);
            if(wall.HasValue)end=eye+direction*Math.Max(.1f,wall.Value.Distance-.18f);
            if(Vector3.DistanceSquared(eye,end)<.25f||!VoidCombatSafety.ClearPath(terrain,eye,end,15.01f)){Reject(p,request,Failure("空间不足"));return;}
            uint seed=unchecked(request.Sequence*2654435761u+(uint)p.PlayerGuid.GetHashCode());
            shatters.Add(new ShatterCast{Owner=p,OwnerGuid=p.PlayerGuid,Sequence=request.Sequence,Seed=seed,Center=end,Direction=direction,Started=Now,Geometry=VoidShatterScene.Create(seed)});
            StartPose(p,.65);
            var peer=Get(p);peer.ReadyAt[3]=Now+VoidShatterRules.Cooldown;peer.PendingUntil=0;Feedback(p,"万象破碎 · 现实开裂");
            Broadcast(EffectPacket(p,3,request.Sequence,end,end,direction,Now,seed));
        }
        internal void AddCutRift(ComponentPlayer p,uint sequence,Vector3 origin,Vector3 direction,float distance)
        {
            ReleaseCut(p,sequence);
            if(!Authority||!Actor(p)||distance<.1f||effects.Count>=VoidSpaceRules.MaxEffects)return;
            var peer=Get(p);if(peer==null||peer.Ended[1]==sequence)return;peer.Processed[1]=sequence;
            Vector3 end=origin+direction*Math.Min(WorldCutRules.Range,distance);uint seed=unchecked(sequence*2246822519u);
            AddEffect(p,1,sequence,origin,end,direction,Now,seed,false);Broadcast(EffectPacket(p,1,sequence,origin,end,direction,Now,seed));
        }
        internal bool ReserveCut(ComponentPlayer p,uint sequence)
        {
            if(cutReservations.ContainsKey(p)||effects.Count+transfers.Count+cutReservations.Count>=VoidSpaceRules.MaxEffects)return false;
            cutReservations[p]=sequence;return true;
        }
        internal void ReleaseCut(ComponentPlayer p,uint sequence)
        {uint reserved;if(p!=null&&cutReservations.TryGetValue(p,out reserved)&&reserved==sequence)cutReservations.Remove(p);}
        private VoidSpacePackage EffectPacket(ComponentPlayer p,byte skill,uint sequence,Vector3 from,Vector3 to,Vector3 direction,double started,uint seed)
            => new VoidSpacePackage{Kind=2,Skill=skill,Caster=p.Entity.EntityId,Sequence=sequence,Origin=from,Point=to,Direction=direction,Started=started,Seed=seed,Cooldown=(float)Remaining(p,skill)};
        private void AddEffect(ComponentPlayer p,byte skill,uint sequence,Vector3 from,Vector3 to,Vector3 direction,double started,uint seed,bool visual)
        {
            foreach(var e in effects)if(e.Owner==p&&e.Skill==skill&&e.Sequence==sequence)return;
            if(effects.Count>=VoidSpaceRules.MaxEffects)return;
            double duration=skill==1?VoidSpaceRules.CutRiftDuration:VoidSpaceRules.BlinkRiftDuration;
            var effect=new Effect{Owner=p,OwnerGuid=p.PlayerGuid,Sequence=sequence,Skill=skill,Seed=seed,From=from,To=to,Direction=direction,Started=started,Expires=started+duration,VisualOnly=visual};
            if(!visual)
            {
                effect.Damage=new VoidSpaceDamage.Job{Owner=p,Skill=skill,From=from,To=to,Direction=direction,Expires=effect.Expires};
                damage.Refresh(effect.Damage);
            }
            effects.Add(effect);
        }
        private void Sync(ComponentPlayer p)
        {
            foreach(var e in effects)if(Vector3.DistanceSquared(p.ComponentBody.Position,e.From)<96*96){var packet=EffectPacket(e.Owner,e.Skill,e.Sequence,e.From,e.To,e.Direction,e.Started,e.Seed);packet.Stamp=Now;packet.To=p.PlayerData.Client;CommonLib.Net?.QueuePackage(packet);}
            foreach(var cast in shatters)if(Vector3.DistanceSquared(p.ComponentBody.Position,cast.Center)<96*96){var packet=EffectPacket(cast.Owner,3,cast.Sequence,cast.Center,cast.Center,cast.Direction,cast.Started,cast.Seed);packet.Stamp=Now;packet.To=p.PlayerData.Client;CommonLib.Net?.QueuePackage(packet);}
        }
        private void SendTransfer(Transfer t,byte kind,byte reason=0)
            => Send(t.Player,new VoidSpacePackage{Kind=kind,Skill=2,Sequence=t.Sequence,Origin=t.Origin,Point=t.Target,Direction=t.Direction,Reason=reason,Cooldown=(float)Remaining(t.Player,2)});
        private bool Valid(Transfer t) => Actor(t.Player)&&t.Player.PlayerGuid==t.OwnerGuid&&t.Player.PlayerData.Client?.TokenId==t.Token;
        private void Signal(ComponentPlayer p,VoidSpacePackage packet)
        {
            Transfer t;
            if(!transfers.TryGetValue(p.PlayerGuid,out t))
            {
                if(packet.Kind==6&&receipts.TryGetValue(p.PlayerGuid,out t)&&Valid(t)&&t.Sequence==packet.Sequence)SendTransfer(t,7);
                return;
            }
            if(!Valid(t)||t.Player!=p||packet.Sequence!=t.Sequence||packet.Skill!=2)return;
            string reason;
            if(packet.Kind==8){Abort(t,4);return;}
            if(packet.Kind==4&&!t.Committed)
            {
                if(!blinkSafety.PathClear(p,t.Origin,t.Target,out reason)){Abort(t,3);return;}
                t.Committed=true;t.NextSend=Now+.3;SendTransfer(t,5);
            }
            else if(packet.Kind==6&&t.Committed)
            {
                if(!blinkSafety.PathClear(p,t.Origin,t.Target,out reason)){Abort(t,3);return;}
                CompleteBlink(p,t.Sequence,t.Origin,t.Target,t.Direction);t.Finished=true;t.Until=Now+10;
                receipts[p.PlayerGuid]=t;transfers.Remove(p.PlayerGuid);SendTransfer(t,7);
            }
        }
        private void Abort(Transfer t,byte reason)
        {
            if(t.Player.IsAddedToProject&&t.Player.Project==terrain.Project&&t.Player.ComponentHealth.Health>0)VoidBlinkSafety.SetPosition(t.Player,t.Origin);
            transfers.Remove(t.OwnerGuid);var peer=Get(t.Player);if(peer!=null)peer.PendingUntil=0;
            SendTransfer(t,8,reason);Feedback(t.Player,"折跃同步取消，已保留冷却并恢复原位置");
        }
        private ComponentPlayer Find(ushort id)
        {
            if(players!=null)foreach(var p in players.ComponentPlayers)if(p.Entity.EntityId==id)return p;return null;
        }
        private void ClientSignal(byte kind,Transfer t)
            => CommonLib.Net?.QueuePackage(new VoidSpacePackage{Kind=kind,Skill=2,Caster=t.Player.Entity.EntityId,Sequence=t.Sequence});
        private double ServerNow => lastServerEstimate=Math.Max(lastServerEstimate,Now+clockOffset);
        private void Receive(VoidSpacePackage packet)
        {
            if(Authority)return;
            if(packet.Kind==1)
            {
                double rtt=Time.RealTime-packet.Echo;
                if(!VoidSpaceRules.Finite(packet.Stamp)||!VoidSpaceRules.Finite(rtt)||packet.Stamp<0||rtt<0||rtt>10)return;
                clockOffset=packet.Stamp+rtt*.5-Now;hasClock=true;return;
            }
            if(!hasClock){if(deferred.Count<16)deferred.Add(packet);return;}
            if(packet.Skill<1||packet.Skill>3||packet.Sequence==0||!VoidSpaceRules.Finite(packet.Stamp)||!WorldCutRules.Finite(packet.Cooldown)||packet.Cooldown<0||packet.Cooldown>VoidShatterRules.Cooldown+.1)return;
            var p=Find(packet.Caster);if(p==null)return;var peer=Get(p);if(peer==null)return;
            if(packet.Kind==9)
            {
                if(peer.PendingSequence==packet.Sequence&&peer.PendingSkill==packet.Skill){peer.PendingUntil=0;peer.ReadyAt[packet.Skill]=Math.Max(peer.ReadyAt[packet.Skill],Now+Math.Max(0,packet.Cooldown-(ServerNow-packet.Stamp)));Feedback(p,Message(packet.Reason));}return;
            }
            if(packet.Kind==10)
            {
                if(WorldCutRules.Newer(packet.Sequence,peer.Ended[packet.Skill]))peer.Ended[packet.Skill]=packet.Sequence;
                RemoveEffects(p,packet.Skill,packet.Sequence);return;
            }
            if(packet.Kind==3||packet.Kind==5||packet.Kind==7||packet.Kind==8){ReceiveTransfer(p,packet,peer);return;}
            if(packet.Kind!=2||!Actor(p)||!Direction(packet.Direction)||!WorldCutRules.Coordinates(packet.Origin.X,packet.Origin.Y,packet.Origin.Z)||!WorldCutRules.Coordinates(packet.Point.X,packet.Point.Y,packet.Point.Z)||!VoidSpaceRules.Finite(packet.Started))return;
            double duration=packet.Skill==1?VoidSpaceRules.CutRiftDuration:packet.Skill==2?VoidSpaceRules.BlinkRiftDuration:VoidShatterRules.Duration,elapsed=ServerNow-packet.Started;
            if(elapsed<-.25||elapsed>=duration||!WorldCutRules.Newer(packet.Sequence,peer.Ended[packet.Skill])||(!WorldCutRules.Newer(packet.Sequence,peer.Visual[packet.Skill])&&packet.Sequence!=peer.Visual[packet.Skill]))return;
            peer.ReadyAt[packet.Skill]=Math.Max(peer.ReadyAt[packet.Skill],Now+Math.Max(0,packet.Cooldown-(ServerNow-packet.Stamp)));
            if(peer.PendingSequence==packet.Sequence&&peer.PendingSkill==packet.Skill)peer.PendingUntil=0;
            if(packet.Sequence==peer.Visual[packet.Skill])return;peer.Visual[packet.Skill]=packet.Sequence;
            double started=Now-Math.Max(0,elapsed);
            if(packet.Skill==3)
            {
                if(shatters.Count<VoidSpaceRules.MaxCasts)
                {
                    shatters.Add(new ShatterCast{Owner=p,OwnerGuid=p.PlayerGuid,Sequence=packet.Sequence,Seed=packet.Seed,Center=packet.Point,Direction=packet.Direction,Started=started,VisualOnly=true,Geometry=VoidShatterScene.Create(packet.Seed)});
                    if(Now<started+.65){StartPose(p,.65);poses[p].Started=started;poses[p].Until=started+.65;}
                }
            }
            else AddEffect(p,packet.Skill,packet.Sequence,packet.Origin,packet.Point,packet.Direction,started,packet.Seed,true);
        }
        private void ReceiveTransfer(ComponentPlayer p,VoidSpacePackage packet,Peer peer)
        {
            if(!p.PlayerData.IsMainPlayer||packet.Skill!=2||packet.Sequence!=peer.PendingSequence||peer.PendingSkill!=2)return;
            if((packet.Kind==3||packet.Kind==5)&&!Direction(packet.Direction))return;
            if(!WorldCutRules.Coordinates(packet.Origin.X,packet.Origin.Y,packet.Origin.Z)||!WorldCutRules.Coordinates(packet.Point.X,packet.Point.Y,packet.Point.Z)||Vector3.DistanceSquared(packet.Origin,packet.Point)>18.01f*18.01f)return;
            if(packet.Kind==3)
            {
                if(peer.PendingUntil<=Now||peer.Ended[2]==packet.Sequence)return;
                if(clientTransfer!=null&&clientTransfer.Sequence!=packet.Sequence)return;
                if(clientTransfer==null)clientTransfer=new Transfer{Player=p,OwnerGuid=p.PlayerGuid,Sequence=packet.Sequence,Origin=packet.Origin,Target=packet.Point,Direction=packet.Direction,Until=Now+VoidSpaceRules.TransferLease};
                clientPlayer=p;string reason;
                if(!Actor(p)||!blinkSafety.PathClear(p,packet.Origin,packet.Point,out reason)){ClientSignal(8,clientTransfer);peer.PendingUntil=0;Feedback(p,"本机目的地区块或碰撞尚未就绪");return;}
                ClientSignal(4,clientTransfer);
            }
            else if(clientTransfer!=null&&clientTransfer.Sequence==packet.Sequence)
            {
                var t=clientTransfer;
                if(packet.Kind==5&&!t.Finished&&!t.TimedOut&&Vector3.DistanceSquared(t.Target,packet.Point)<.001f)
                {
                    string reason;
                    if(!Actor(p)||!blinkSafety.PathClear(p,t.Origin,t.Target,out reason)){ClientSignal(8,t);return;}
                    if(!t.Committed){VoidBlinkSafety.SetPosition(p,t.Target);ArmFall(p);t.Committed=true;AddEffect(p,2,t.Sequence,t.Origin,t.Target,t.Direction,Now,unchecked(t.Sequence*2654435761u),true);StartPose(p,.22);}
                    ClientSignal(6,t);
                }
                else if(packet.Kind==7&&!t.Finished&&!t.TimedOut)
                {
                    if(!t.Committed||!Actor(p)||peer.Ended[2]==t.Sequence)return;
                    VoidBlinkSafety.SetPosition(p,t.Target);peer.ReadyAt[2]=Math.Max(peer.ReadyAt[2],Now+Math.Max(0,packet.Cooldown-(ServerNow-packet.Stamp)));peer.PendingUntil=0;t.Finished=true;t.Until=Now+10;
                }
                else if(packet.Kind==8&&!t.Finished)
                {
                    if(t.Committed&&p.IsAddedToProject&&p.ComponentHealth.Health>0)VoidBlinkSafety.SetPosition(p,t.Origin);
                    fall.Remove(p.PlayerGuid);RemoveEffects(p,2,t.Sequence);peer.PendingUntil=0;peer.Ended[2]=t.Sequence;t.Finished=true;t.Until=Now+10;
                    Feedback(p,"折跃取消，已恢复原位置；冷却未消耗");
                }
            }
        }
        private void ArmFall(ComponentPlayer p) => fall[p.PlayerGuid]=new FallGuard{Player=p,OwnerGuid=p.PlayerGuid,Started=Now,Until=Now+VoidSpaceRules.FallLease};
        internal bool ProtectFall(ComponentHealth health,ComponentCreature attacker,bool ignored,string cause)
        {
            var p=health?.Entity.FindComponent<ComponentPlayer>(false);FallGuard guard;
            if(p==null||!fall.TryGetValue(p.PlayerGuid,out guard)||guard.Player!=p||guard.OwnerGuid!=p.PlayerGuid||Now>=guard.Until||!Actor(p))return false;
            bool native=!string.IsNullOrEmpty(cause)&&cause==LanguageControl.Get("ComponentHealth",2);
            if(!VoidSpaceRules.IsFall(true,attacker!=null,ignored,native))return false;
            fall.Remove(p.PlayerGuid);return true;
        }
        private void StartPose(ComponentPlayer p,double duration){ClearPose(p);poses[p]=new Pose{Started=Now,Until=Now+duration};}
        private void Animate(ComponentPlayer p,double progress,Pose pose)
        {
            var sample=AbyssMeleeMotion.Sample(2,(float)Math.Min(1,progress));Vector3 order=new Vector3(sample.Pitch,sample.Yaw,sample.Roll),offset=new Vector3(sample.X,sample.Y,sample.Z);
            var fp=p.Entity.FindComponent<ComponentFirstPersonModel>(false);if(fp!=null){fp.ItemRotationOrder=order;fp.ItemOffsetOrder=offset;}
            p.ComponentCreatureModel.InHandItemRotationOrder=order;p.ComponentCreatureModel.InHandItemOffsetOrder=offset*.35f;pose.Rotation=order;pose.Offset=offset;
        }
        private void ClearPose(ComponentPlayer p)
        {
            Pose pose;if(!poses.TryGetValue(p,out pose))return;
            var fp=p.Entity.FindComponent<ComponentFirstPersonModel>(false);
            if(fp!=null&&fp.ItemRotationOrder==pose.Rotation)fp.ItemRotationOrder=Vector3.Zero;
            if(fp!=null&&fp.ItemOffsetOrder==pose.Offset)fp.ItemOffsetOrder=Vector3.Zero;
            if(p.ComponentCreatureModel.InHandItemRotationOrder==pose.Rotation)p.ComponentCreatureModel.InHandItemRotationOrder=Vector3.Zero;
            if(p.ComponentCreatureModel.InHandItemOffsetOrder==pose.Offset*.35f)p.ComponentCreatureModel.InHandItemOffsetOrder=Vector3.Zero;
            poses.Remove(p);
        }
        private void Sound(ComponentPlayer p,string name,Vector3 at)
        {try{audio?.PlaySound("Audio/Abyss/"+name,.75f,0,at,24,false);}catch(Exception e){Log.Warning("[SCIEPort:Space] Sound unavailable: "+e.GetType().Name);}}
        private void RemoveEffects(ComponentPlayer p,byte skill,uint sequence)
        {
            for(int i=effects.Count-1;i>=0;i--)if(effects[i].Owner==p&&effects[i].Skill==skill&&(sequence==0||effects[i].Sequence==sequence))effects.RemoveAt(i);
            for(int i=shatters.Count-1;i>=0;i--)if(shatters[i].Owner==p&&skill==3&&(sequence==0||shatters[i].Sequence==sequence))shatters.RemoveAt(i);
        }
        internal void Cancel(ComponentPlayer p)
        {
            if(p==null)return;cutReservations.Remove(p);var peer=Get(p);Transfer transfer;
            if(transfers.TryGetValue(p.PlayerGuid,out transfer))Abort(transfer,4);
            if(!Authority&&peer!=null&&peer.PendingSequence!=0&&peer.PendingUntil>Now)CommonLib.Net?.QueuePackage(new VoidSpacePackage{Kind=8,Skill=peer.PendingSkill,Caster=p.Entity.EntityId,Sequence=peer.PendingSequence});
            if(!Authority)
            {
                foreach(var e in effects)if(e.Owner==p)CommonLib.Net?.QueuePackage(new VoidSpacePackage{Kind=8,Skill=e.Skill,Caster=p.Entity.EntityId,Sequence=e.Sequence});
                foreach(var c in shatters)if(c.Owner==p)CommonLib.Net?.QueuePackage(new VoidSpacePackage{Kind=8,Skill=3,Caster=p.Entity.EntityId,Sequence=c.Sequence});
            }
            if(clientPlayer==p&&clientTransfer!=null&&!clientTransfer.Finished)
            {if(clientTransfer.Committed&&p.IsAddedToProject&&p.Project==terrain.Project&&p.ComponentHealth.Health>0)VoidBlinkSafety.SetPosition(p,clientTransfer.Origin);clientTransfer.TimedOut=true;clientTransfer.Until=Now+10;}
            for(byte skill=1;skill<=3;skill++)
            {
                if(Authority)
                {
                    foreach(var e in effects)if(e.Owner==p&&e.Skill==skill)Broadcast(new VoidSpacePackage{Kind=10,Skill=skill,Caster=p.Entity.EntityId,Sequence=e.Sequence,Origin=e.From});
                    foreach(var c in shatters)if(c.Owner==p&&skill==3)Broadcast(new VoidSpacePackage{Kind=10,Skill=3,Caster=p.Entity.EntityId,Sequence=c.Sequence,Origin=c.Center});
                }
                RemoveEffects(p,skill,0);if(peer!=null)peer.Ended[skill]=peer.Visual[skill];
            }
            for(int i=impacts.Count-1;i>=0;i--)if(impacts[i].Owner==p)impacts.RemoveAt(i);
            if(peer!=null){peer.PendingUntil=0;if(peer.PendingSkill>0)peer.Ended[peer.PendingSkill]=peer.PendingSequence;}
            fall.Remove(p.PlayerGuid);ClearPose(p);
        }
        internal void Update()
        {
            if(disposed)return;
            if(!Authority)
            {
                Guid token=CommonLib.Net?.Server?.TokenId??Guid.Empty;
                if(token!=serverToken){serverToken=token;hasClock=false;lastServerEstimate=0;nextClock=-100;deferred.Clear();if(clientPlayer!=null)Cancel(clientPlayer);}
                if(players!=null)foreach(var p in players.ComponentPlayers)if(p.PlayerData.IsMainPlayer&&Actor(p,false)){Ping(p);break;}
            }
            for(int i=0;i<VoidSpaceRules.RequestsPerFrame;i++)
            {
                Pending pending;lock(queue){if(queue.Count==0)break;pending=queue.Dequeue();}
                try{if(Authority)Process(pending);else if(ReferenceEquals(pending.Client,CommonLib.Net?.Server)&&pending.Token==serverToken)Receive(pending.Packet);}
                catch(Exception e){Log.Warning("[SCIEPort:Space] Command rejected: "+e.GetType().Name);if(Authority&&pending.Player!=null)Reject(pending.Player,pending.Packet,6);}
            }
            if(hasClock&&deferred.Count>0){var waiting=deferred.ToArray();deferred.Clear();foreach(var packet in waiting)Receive(packet);}
            retired.Clear();foreach(var pair in transfers)
            {
                var t=pair.Value;if(!Valid(t)||Now>=t.Until){retired.Add(pair.Key);continue;}
                VoidBlinkSafety.SetPosition(t.Player,t.Origin);
                if(Now>=t.NextSend){t.NextSend=Now+.3;SendTransfer(t,t.Committed?(byte)5:(byte)3);}
            }
            foreach(var id in retired)Abort(transfers[id],4);
            if(clientTransfer!=null)
            {
                if(!Actor(clientPlayer)||Now>=clientTransfer.Until)
                {
                    if(!clientTransfer.Finished&&!clientTransfer.TimedOut){ClientSignal(8,clientTransfer);Cancel(clientPlayer);}
                    else if(clientTransfer.Finished||Now>=clientTransfer.Until){clientTransfer=null;clientPlayer=null;}
                }
            }
            retired.Clear();foreach(var pair in receipts)if(!Valid(pair.Value)||Now>=pair.Value.Until)retired.Add(pair.Key);foreach(var id in retired)receipts.Remove(id);
            retired.Clear();foreach(var pair in fall)
            {
                var guard=pair.Value;var p=guard.Player;
                bool valid=Actor(p)&&p.PlayerGuid==guard.OwnerGuid&&Now<guard.Until;
                bool landed=p.ComponentBody.StandingOnValue.HasValue||p.ComponentBody.StandingOnBody!=null||p.ComponentBody.ImmersionFactor>.5f;
                // Health(Default=0) reads the previous Body(2) collision after this Input+1=-9
                // pass. Clear only a protected landing's vertical impact before health/RPC.
                if(valid&&landed&&p.ComponentBody.CollisionVelocityChange.Y>0)
                {var velocity=p.ComponentBody.CollisionVelocityChange;velocity.Y=0;p.ComponentBody.CollisionVelocityChange=velocity;}
                if(!valid||Now>guard.Started&&landed)retired.Add(pair.Key);
            }
            foreach(var id in retired)fall.Remove(id);
            retiredPlayers.Clear();foreach(var pair in poses)
            {
                if(!Actor(pair.Key)||Now>=pair.Value.Until)retiredPlayers.Add(pair.Key);
                else Animate(pair.Key,(Now-pair.Value.Started)/(pair.Value.Until-pair.Value.Started),pair.Value);
            }
            foreach(var p in retiredPlayers)ClearPose(p);
            for(int i=shatters.Count-1;i>=0;i--)
            {
                var cast=shatters[i];
                if(!Actor(cast.Owner)||cast.Owner.PlayerGuid!=cast.OwnerGuid){Cancel(cast.Owner);continue;}
                double elapsed=Now-cast.Started;
                if(!cast.VisualOnly&&!cast.Impacted&&Now>=cast.Started+VoidShatterRules.Impact)
                {
                    cast.Impacted=true;var job=new VoidSpaceDamage.Job{Owner=cast.Owner,From=cast.Center,To=cast.Center,Direction=cast.Direction,Skill=3,Expires=Now+2};damage.Refresh(job);impacts.Add(job);
                }
                if(Now>=cast.Started+VoidShatterRules.Duration){Get(cast.Owner).Ended[3]=cast.Sequence;shatters.RemoveAt(i);continue;}
            }
            int budget=VoidSpaceRules.FrameChecks;
            for(int i=impacts.Count-1;i>=0;i--)
            {
                var job=impacts[i];if(!Actor(job.Owner)||Now>=job.Expires){impacts.RemoveAt(i);continue;}
                damage.Apply(job,Now,ref budget,VoidSpaceRules.FrameChecks);if(job.Cursor>=job.Bodies.Count)impacts.RemoveAt(i);
            }
            for(int n=0;n<effects.Count;n++)
            {
                int i=(effectCursor+n)%effects.Count;var effect=effects[i];
                if(Actor(effect.Owner)&&effect.Owner.PlayerGuid==effect.OwnerGuid&&Now<effect.Expires&&effect.Damage!=null)damage.Apply(effect.Damage,Now,ref budget,32);
            }
            if(effects.Count>0)effectCursor=(effectCursor+1)%effects.Count;
            for(int i=effects.Count-1;i>=0;i--)
            {
                var e=effects[i];if(!Actor(e.Owner)||e.Owner.PlayerGuid!=e.OwnerGuid||Now>=e.Expires){var peer=Get(e.Owner);if(peer!=null)peer.Ended[e.Skill]=e.Sequence;effects.RemoveAt(i);}
            }
            retired.Clear();foreach(var pair in peers)
            {
                var peer=pair.Value;
                if(peer.PendingUntil>0&&Now>=peer.PendingUntil){Cancel(peer.Player);Feedback(peer.Player,"主机响应超时，空间技能已取消");}
                if(Now-peer.Seen>30&&peer.PendingUntil<=Now&&peer.ReadyAt[2]<=Now&&peer.ReadyAt[3]<=Now)retired.Add(pair.Key);
            }
            foreach(var id in retired)peers.Remove(id);
        }
        internal void Draw(Camera camera){if(disposed)return;riftScene.Draw(camera,effects,Now,quality);shatterScene.Draw(camera,shatters,Now,quality);}
        public void Dispose()
        {
            if(disposed)return;
            disposed=true;
            foreach(var transfer in new List<Transfer>(transfers.Values))try{Abort(transfer,4);}catch(Exception){}
            try{if(clientPlayer!=null)Cancel(clientPlayer);}catch(Exception){}
            foreach(var p in new List<ComponentPlayer>(poses.Keys))try{ClearPose(p);}catch(Exception){}
            lock(queue)queue.Clear();transfers.Clear();receipts.Clear();cutReservations.Clear();effects.Clear();shatters.Clear();impacts.Clear();fall.Clear();poses.Clear();peers.Clear();deferred.Clear();clientPlayer=null;clientTransfer=null;riftScene.Dispose();shatterScene.Dispose();
        }
        private static byte Failure(string reason) => reason.Contains("领地")?(byte)10:reason.Contains("区块")?(byte)9:reason.Contains("岩浆")?(byte)11:(byte)3;
        private static string Name(byte skill) => skill==2?"无界折跃":"万象破碎";
        private static string Message(byte reason)
        {
            switch(reason){case 1:return "技能冷却中";case 2:return "请先结束当前动作或技能";case 3:return "前方没有容纳身体或破碎区域的安全空间";case 4:return "网络同步超时或已取消";case 6:return "无效的空间技能方向";case 7:return "空间技能容量已满";case 9:return "前方区块尚未加载";case 10:return "领地保护禁止此操作";case 11:return "目的地存在岩浆危险";default:return "空间技能请求失败";}
        }
    }
}

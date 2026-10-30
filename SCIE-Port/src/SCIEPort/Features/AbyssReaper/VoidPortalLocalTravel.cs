using System;
using System.Collections.Generic;
using Engine;
using Game;
using Game.NetWork;
namespace SCIEPort.Features.AbyssReaper
{
    // Shared permanent gates; selections and teleport handshakes belong to each actor.
    internal sealed class VoidPortalLocalTravel : IVoidPortalTravelWorld, IDisposable
    {
        private readonly SubsystemTerrain terrain;
        private readonly SubsystemPlayers players;
        private readonly SubsystemTime clock;
        private readonly VoidPortalRegistry registry;
        private readonly VoidPortalTravel travel;
        private readonly HashSet<Guid> inside=new HashSet<Guid>();
        private readonly HashSet<Guid> current=new HashSet<Guid>();
        private readonly List<Guid> retired=new List<Guid>();
        private readonly Dictionary<Guid,ComponentPlayer> actors=new Dictionary<Guid,ComponentPlayer>();
        private readonly Dictionary<Guid,VoidChunkLease> leases=new Dictionary<Guid,VoidChunkLease>();
        private readonly HashSet<Guid> alreadyLoaded=new HashSet<Guid>();
        private readonly Dictionary<Guid,double> releaseAfter=new Dictionary<Guid,double>();
        private sealed class Selection
        {internal Guid Source,Target,Token;internal uint Sequence;internal ComponentPlayer Player;internal double Until;}
        private sealed class Transfer
        {
            internal Selection Choice;internal Vector3 Landing,Origin;internal double Until,NextSend;
            internal bool Ready,Committed,Finalized,TimedOut;internal VoidChunkLease Lease;
        }
        private readonly Dictionary<Guid,Selection> choices=new Dictionary<Guid,Selection>();
        private readonly Dictionary<Guid,Transfer> transfers=new Dictionary<Guid,Transfer>();
        private readonly Dictionary<Guid,Transfer> receipts=new Dictionary<Guid,Transfer>();
        private Transfer clientTransfer;
        private ComponentPlayer clientPlayer;
        internal VoidPortalNetwork Network{get;set;}
        internal VoidPortalLocalTravel(SubsystemTerrain terrain,SubsystemPlayers players,SubsystemTime clock,VoidPortalRegistry registry)
        { this.terrain=terrain;this.players=players;this.clock=clock;this.registry=registry;travel=new VoidPortalTravel(registry,this); }
        public bool IsAuthority => CommonLib.WorkType==WorkType.Local || CommonLib.WorkType==WorkType.Server;
        private bool Player(Guid id,out ComponentPlayer p)
        {
            return actors.TryGetValue(id,out p) && p!=null && p.IsAddedToProject && p.ComponentHealth.Health>0 && p.PlayerData.IsReadyForPlaying
                && p.ComponentSleep.SleepFactor<=0 && p.ComponentRider.Mount==null && p.ComponentBody.ParentBody==null && p.ComponentBody.ChildBodies.Count==0
                && p.Project.FindSubsystem<SCIEPort.Features.FourSaints.SubsystemFourSaints>(false)?.IsActionLocked(p)!=true;
        }
        internal void Choose(ComponentPlayer p,Guid source,Guid target,uint sequence)
        {
            travel.Cancel(p.PlayerGuid);Drop(p.PlayerGuid,"已重新选择目的地");
            releaseAfter.Remove(p.PlayerGuid);EndLoading(p.PlayerGuid);receipts.Remove(p.PlayerGuid);
            actors[p.PlayerGuid]=p;
            choices[p.PlayerGuid]=new Selection{Player=p,Source=source,Target=target,Sequence=sequence,Token=p.PlayerData.Client?.TokenId??Guid.Empty,Until=clock.GameTime+60};
            inside.Remove(p.PlayerGuid);travel.ObserveOutside(p.PlayerGuid);
        }
        private bool Valid(Selection c)
        {
            ComponentPlayer p;VoidPortalRegistry.Portal source,target;
            return Player(c.Player.PlayerGuid,out p)&&p==c.Player&&c.Until>clock.GameTime&&(p.PlayerData.Client?.TokenId??Guid.Empty)==c.Token
                &&registry.TryGet(c.Source,out source)&&source.Active&&registry.TryGet(c.Target,out target)&&target.Active;
        }
        internal bool InputLocked(ComponentPlayer p)
            => (clientTransfer!=null&&p==clientPlayer&&!clientTransfer.Finalized&&!clientTransfer.TimedOut)||transfers.ContainsKey(p.PlayerGuid);
        private static bool InPortal(ComponentPlayer player,VoidPortalRegistry.Portal portal)
        {
            Vector3 at=player.ComponentBody.Position;
            float along=portal.Facing==0?at.X-portal.X:at.Z-portal.Z;
            float across=portal.Facing==0?at.Z-portal.Z:at.X-portal.X;
            return Math.Abs(along)<1.3f && Math.Abs(across)<.55f && at.Y>=portal.Y-.15f && at.Y<=portal.Y+2f;
        }
        public bool IsActorEligible(Guid id,VoidPortalRegistry.Portal source)
        {
            ComponentPlayer p;
            return IsAuthority && Player(id,out p) && source.Active && InPortal(p,source)
                && VoidPortalSafety.Ready(terrain,Terrain.ToCell(source.X),Terrain.ToCell(source.Z))
                && VoidPortalSafety.Unprotected(Terrain.ToCell(source.X),Terrain.ToCell(source.Z));
        }
        public bool BeginLoading(Guid id,VoidPortalRegistry.Portal destination)
        {
            if(!IsAuthority || leases.ContainsKey(id) || alreadyLoaded.Contains(id))return false;
            // Preserve the existing loaded-destination path even if a repackaged runtime lacks the audited private ABI.
            if(ChunksReady(destination)){alreadyLoaded.Add(id);return true;}
            VoidChunkLease lease;
            if(!VoidChunkLease.TryCreate(terrain.TerrainUpdater,new Vector2(destination.X,destination.Z),out lease))return false;
            leases.Add(id,lease);return true;
        }
        public bool DestinationLoaded(Guid id,VoidPortalRegistry.Portal destination)
        {
            if(!IsAuthority||!(leases.ContainsKey(id)||alreadyLoaded.Contains(id))||!ChunksReady(destination))return false;
            ComponentPlayer p;if(!Player(id,out p))return false;
            if(p.PlayerData.IsMainPlayer)return true;
            Selection selection;if(Network==null||!choices.TryGetValue(id,out selection)||!Valid(selection))return false;
            Transfer transfer;
            if(!transfers.TryGetValue(id,out transfer))
            {
                float x,y,z;if(!TryFindLanding(id,destination,out x,out y,out z))return false;
                VoidChunkLease sourceLease;if(!VoidChunkLease.TryCreate(terrain.TerrainUpdater,p.ComponentBody.Position.XZ,out sourceLease))return false;
                transfer=new Transfer{Choice=selection,Landing=new Vector3(x,y,z),Origin=p.ComponentBody.Position,Until=clock.GameTime+8,Lease=sourceLease};transfers.Add(id,transfer);
            }
            if(clock.GameTime>=transfer.NextSend)
            {transfer.NextSend=clock.GameTime+.5;SendTransfer(p,transfer,VoidPortalNetwork.Prepare);}
            return transfer.Ready;
        }
        private bool ChunksReady(VoidPortalRegistry.Portal destination)
        {
            int x=Terrain.ToCell(destination.X),z=Terrain.ToCell(destination.Z);
            for(int dx=-4;dx<=4;dx+=4)for(int dz=-4;dz<=4;dz+=4)
                if(!VoidPortalSafety.Ready(terrain,x+dx,z+dz))return false;
            return true;
        }
        public bool TryFindLanding(Guid id,VoidPortalRegistry.Portal destination,out float x,out float y,out float z)
        {
            x=y=z=0;ComponentPlayer p;if(!Player(id,out p))return false;
            // Bounded cardinal and diagonal candidates, always outside the destination trigger.
            int[] dx={2,-2,0,0,2,-2,2,-2,3,-3,0,0};
            int[] dz={0,0,2,-2,2,2,-2,-2,0,0,3,-3};
            for(int i=0;i<dx.Length;i++)
            {
                var candidate=new Vector3((float)Math.Floor(destination.X)+dx[i]+.5f,destination.Y,(float)Math.Floor(destination.Z)+dz[i]+.5f);
                if(Reserved(id,p,candidate)||!VoidPortalSafety.SafeLanding(terrain,p,candidate))continue;
                x=candidate.X;y=candidate.Y;z=candidate.Z;return true;
            }
            return false;
        }
        private bool Reserved(Guid id,ComponentPlayer p,Vector3 at)
        {
            Vector3 size=p.ComponentBody.BoxSize;
            var box=new BoundingBox(at-new Vector3(size.X*.5f,0,size.Z*.5f),at+new Vector3(size.X*.5f,size.Y,size.Z*.5f));
            foreach(var pair in transfers)
            {
                if(pair.Key==id)continue;
                Vector3 other=pair.Value.Landing,extent=pair.Value.Choice.Player.ComponentBody.BoxSize;
                var reserved=new BoundingBox(other-new Vector3(extent.X*.5f,0,extent.Z*.5f),other+new Vector3(extent.X*.5f,extent.Y,extent.Z*.5f));
                if(box.Intersection(reserved))return true;
            }
            return false;
        }
        public bool TryMove(Guid id,float x,float y,float z)
        {
            ComponentPlayer p;var position=new Vector3(x,y,z);
            if(!IsAuthority || !Player(id,out p) || !VoidPortalSafety.SafeLanding(terrain,p,position))return false;
            if(!p.PlayerData.IsMainPlayer)
            {
                Transfer transfer;if(!transfers.TryGetValue(id,out transfer)||!transfer.Ready||Vector3.DistanceSquared(transfer.Landing,position)>.001f)return false;
                transfer.Committed=true;transfer.Until=clock.GameTime+5;transfer.NextSend=clock.GameTime+.5;
                SendTransfer(p,transfer,VoidPortalNetwork.Commit);
            }
            else SetPosition(p,position);
            // Keep the destination lease briefly while the native player/camera update location catches up.
            releaseAfter[id]=clock.GameTime+(p.PlayerData.IsMainPlayer?3:8);return true;
        }
        private static void SetPosition(ComponentPlayer p,Vector3 at)
        {
            p.ComponentBody.Position=at;p.ComponentBody.Velocity=Vector3.Zero;
            p.ComponentBody.netPosition=new NetPosition(at);p.ComponentBody.netVelocity=new NetVelocity(Vector3.Zero);
            p.ComponentBody.SendPosition=at;p.ComponentBody.SendVelocity=Vector3.Zero;
        }
        private void SendTransfer(ComponentPlayer p,Transfer transfer,byte kind)
        {Network.Send(p,new VoidPortalPackage{Kind=kind,Sequence=transfer.Choice.Sequence,Portal=transfer.Choice.Source,Target=transfer.Choice.Target,Point=transfer.Landing});}
        internal void Signal(ComponentPlayer p,VoidPortalPackage packet)
        {
            Transfer transfer;
            if(!transfers.TryGetValue(p.PlayerGuid,out transfer))
            {
                if(receipts.TryGetValue(p.PlayerGuid,out transfer)&&Valid(transfer.Choice)&&transfer.Choice.Sequence==packet.Sequence&&transfer.Choice.Source==packet.Portal&&transfer.Choice.Target==packet.Target)
                    SendTransfer(p,transfer,VoidPortalNetwork.Finish);
                return;
            }
            if(!Valid(transfer.Choice)||transfer.Choice.Player!=p
                ||transfer.Choice.Sequence!=packet.Sequence||transfer.Choice.Source!=packet.Portal||transfer.Choice.Target!=packet.Target)return;
            if(packet.Command==VoidPortalNetwork.Ready&&!transfer.Committed)transfer.Ready=true;
            else if(packet.Command==VoidPortalNetwork.Ack&&transfer.Committed)
            {
                // ReliableOrdered puts all pre-teleport BodyUpdates before this acknowledgement.
                // Reset native interpolation as well as Position before the next ComponentBody.Update.
                SetPosition(p,transfer.Landing);SendTransfer(p,transfer,VoidPortalNetwork.Finish);
                transfer.Lease?.Dispose();
                transfer.Until=clock.GameTime+10;receipts[p.PlayerGuid]=transfer;
                transfers.Remove(p.PlayerGuid);choices.Remove(p.PlayerGuid);
            }
            else if(packet.Command==VoidPortalNetwork.Cancel)Drop(p.PlayerGuid,"传送取消");
        }
        internal void Receive(ComponentPlayer p,VoidPortalPackage packet)
        {
            if(IsAuthority||!Network.MatchesChoice(p,packet))return;
            if(packet.Kind==VoidPortalNetwork.Prepare)
            {
                if(!WorldCutDamage.Finite(packet.Point)||!WorldCutRules.Coordinates(packet.Point.X,packet.Point.Y,packet.Point.Z)||packet.Sequence==0)return;
                if(clientTransfer!=null&&(clientTransfer.Finalized||clientTransfer.TimedOut)&&packet.Sequence!=clientTransfer.Choice.Sequence)
                {clientTransfer.Lease?.Dispose();clientTransfer=null;}
                if(clientTransfer!=null)
                {
                    if(packet.Sequence!=clientTransfer.Choice.Sequence||packet.Portal!=clientTransfer.Choice.Source||packet.Target!=clientTransfer.Choice.Target)return;
                }
                else
                {
                    if(!Network.Actor(p))return;
                    VoidChunkLease lease;if(!VoidChunkLease.TryCreateClient(terrain.TerrainUpdater,packet.Point.XZ,out lease))
                    {Network.Signal(p,VoidPortalNetwork.Cancel,packet.Sequence,packet.Portal,packet.Target);return;}
                    clientPlayer=p;clientTransfer=new Transfer{Choice=new Selection{Player=p,Sequence=packet.Sequence,Source=packet.Portal,Target=packet.Target},Origin=p.ComponentBody.Position,Landing=packet.Point,Until=clock.GameTime+10,Lease=lease};
                }
            }
            else if(clientTransfer!=null&&packet.Sequence==clientTransfer.Choice.Sequence&&packet.Portal==clientTransfer.Choice.Source&&packet.Target==clientTransfer.Choice.Target)
            {
                if(packet.Kind==VoidPortalNetwork.Commit)
                {
                    if(clientTransfer.Ready&&Vector3.DistanceSquared(packet.Point,clientTransfer.Landing)<.001f&&Network.Actor(p))
                    {
                        if(!clientTransfer.Committed)
                        {
                            if(!VoidPortalSafety.SafeLanding(terrain,p,packet.Point))return;
                            SetPosition(p,packet.Point);clientTransfer.Committed=true;clientTransfer.Until=clock.GameTime+5;
                        }
                        Network.Signal(p,VoidPortalNetwork.Ack,packet.Sequence,packet.Portal,packet.Target);
                    }
                }
                else if(packet.Kind==VoidPortalNetwork.Finish)
                {
                    if(packet.Command==VoidPortalNetwork.Cancel&&WorldCutDamage.Finite(packet.Point))SetPosition(p,packet.Point);
                    else SetPosition(p,clientTransfer.Landing);
                    // Keep client terrain through the native camera's next location update.
                    clientTransfer.Until=clock.GameTime+1;clientTransfer.Ready=false;clientTransfer.Committed=true;clientTransfer.Finalized=true;
                }
            }
        }
        private void Drop(Guid id,string reason)
        {
            travel.Cancel(id);
            Transfer transfer;
            if(transfers.TryGetValue(id,out transfer))
            {
                transfer.Lease?.Dispose();
                var p=transfer.Choice.Player;
                if(p.IsAddedToProject)
                {
                    SetPosition(p,transfer.Origin);
                    Network?.Send(p,new VoidPortalPackage{Kind=VoidPortalNetwork.Finish,Command=VoidPortalNetwork.Cancel,Sequence=transfer.Choice.Sequence,Portal=transfer.Choice.Source,Target=transfer.Choice.Target,Point=transfer.Origin});
                    Network?.Message(p,reason);
                }
                transfers.Remove(id);
            }
            choices.Remove(id);
        }
        public void EndLoading(Guid id)
        {
            alreadyLoaded.Remove(id);
            double until;if(releaseAfter.TryGetValue(id,out until) && clock.GameTime<until)return;
            releaseAfter.Remove(id);VoidChunkLease lease;
            if(leases.TryGetValue(id,out lease)){leases.Remove(id);lease.Dispose();}
        }
        internal void Update()
        {
            if(!IsAuthority)
            {
                if(clientTransfer==null)return;
                if(!Network.Actor(clientPlayer)||(clientTransfer.Finalized&&clock.GameTime>=clientTransfer.Until))
                {clientTransfer.Lease?.Dispose();clientTransfer=null;clientPlayer=null;return;}
                if(clock.GameTime>=clientTransfer.Until)
                {
                    // Retain the sequence tombstone so a delayed authoritative abort can restore
                    // the source even after releasing the input lock. Never pin the player forever.
                    clientTransfer.TimedOut=true;
                    clientTransfer.Lease?.Dispose();clientTransfer.Lease=null;
                    Network.Signal(clientPlayer,VoidPortalNetwork.Cancel,clientTransfer.Choice.Sequence,clientTransfer.Choice.Source,clientTransfer.Choice.Target);
                    clientTransfer.Until=clock.GameTime+2;
                }
                if(clientTransfer.TimedOut)return;
                if(!clientTransfer.Committed&&VoidPortalSafety.SafeLanding(terrain,clientPlayer,clientTransfer.Landing))
                {
                    clientTransfer.Ready=true;
                    if(clock.GameTime>=clientTransfer.NextSend)
                    {clientTransfer.NextSend=clock.GameTime+.5;Network.Signal(clientPlayer,VoidPortalNetwork.Ready,clientTransfer.Choice.Sequence,clientTransfer.Choice.Source,clientTransfer.Choice.Target);}
                }
                return;
            }
            retired.Clear();foreach(var pair in receipts)if(pair.Value.Until<=clock.GameTime||!Valid(pair.Value.Choice))retired.Add(pair.Key);
            foreach(var id in retired)receipts.Remove(id);
            retired.Clear();foreach(var pair in transfers)
            {
                var transfer=pair.Value;
                if(!Valid(transfer.Choice)||clock.GameTime>=transfer.Until)retired.Add(pair.Key);
                else
                {
                    // Pin only the source until the owning client acknowledges the committed move.
                    SetPosition(transfer.Choice.Player,transfer.Origin);
                    if(transfer.Committed&&clock.GameTime>=transfer.NextSend)
                    {transfer.NextSend=clock.GameTime+.5;SendTransfer(transfer.Choice.Player,transfer,VoidPortalNetwork.Commit);}
                }
            }
            foreach(var id in retired){travel.Cancel(id);Drop(id,"目的地失效或同步超时，传送已取消");}
            retired.Clear();foreach(var pair in releaseAfter)if(clock.GameTime>=pair.Value)retired.Add(pair.Key);
            foreach(var id in retired)EndLoading(id);
            current.Clear();int scanned=0;
            foreach(var p in players.ComponentPlayers)
            {
                if(++scanned>64)break;
                if(p==null || !p.IsAddedToProject || p.PlayerGuid==Guid.Empty)continue;
                Guid id=p.PlayerGuid;actors[id]=p;bool inAny=false;
                Selection selection;
                if(choices.TryGetValue(id,out selection)&&!Valid(selection)){travel.Cancel(id);Drop(id,"目的地已删除或选择已过期");}
                foreach(var portal in registry.Records)
                {
                    if(!portal.Active || !InPortal(p,portal))continue;
                    inAny=true;current.Add(id);
                    if(!inside.Contains(id)&&choices.TryGetValue(id,out selection)&&selection.Source==portal.Id&&!transfers.ContainsKey(id))
                    {
                        var result=travel.Begin(id,portal.Id,selection.Target,clock.GameTime);
                        Network?.Message(p,result==PortalTravelResult.Preparing?"星门：正在准备目的地":"星门：无法安全准备传送");
                    }
                    break;
                }
                if(!inAny)travel.ObserveOutside(id);
                var outcome=travel.Update(id,clock.GameTime);
                if(outcome==PortalTravelResult.Arrived){if(p.PlayerData.IsMainPlayer)choices.Remove(id);Network?.Message(p,"星门传送已就绪");}
                else if(outcome==PortalTravelResult.Timeout||outcome==PortalTravelResult.NoLanding||outcome==PortalTravelResult.Cancelled)
                {Drop(id,"目的地未就绪或没有安全落点，传送取消");Network?.Message(p,"传送取消，请离开门口后重试");}
            }
            inside.Clear();foreach(var id in current)inside.Add(id);
            retired.Clear();foreach(var pair in actors)if(pair.Value==null || !pair.Value.IsAddedToProject)retired.Add(pair.Key);
            foreach(var id in retired){travel.Forget(id);Drop(id,"玩家已离开");receipts.Remove(id);releaseAfter.Remove(id);EndLoading(id);inside.Remove(id);actors.Remove(id);}
        }
        public void Dispose(){travel.Dispose();clientTransfer?.Lease?.Dispose();clientTransfer=null;clientPlayer=null;foreach(var transfer in transfers.Values)transfer.Lease?.Dispose();transfers.Clear();receipts.Clear();choices.Clear();foreach(var lease in leases.Values)lease.Dispose();leases.Clear();alreadyLoaded.Clear();releaseAfter.Clear();inside.Clear();current.Clear();retired.Clear();actors.Clear();}
    }
}

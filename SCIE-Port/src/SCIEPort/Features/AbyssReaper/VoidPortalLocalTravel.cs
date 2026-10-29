using System;
using System.Collections.Generic;
using Engine;
using Game;
using Game.NetWork;
namespace SCIEPort.Features.AbyssReaper
{
    // Local player or host only until remote player position and request synchronization are verified.
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
        internal VoidPortalLocalTravel(SubsystemTerrain terrain,SubsystemPlayers players,SubsystemTime clock,VoidPortalRegistry registry)
        { this.terrain=terrain;this.players=players;this.clock=clock;this.registry=registry;travel=new VoidPortalTravel(registry,this); }
        public bool IsAuthority => CommonLib.WorkType==WorkType.Local || CommonLib.WorkType==WorkType.Server;
        private bool Player(Guid id,out ComponentPlayer p)
        {
            return actors.TryGetValue(id,out p) && p!=null && p.IsAddedToProject && p.PlayerData.IsMainPlayer && p.ComponentHealth.Health>0 && p.PlayerData.IsReadyForPlaying
                && p.ComponentSleep.SleepFactor<=0 && p.ComponentRider.Mount==null && p.ComponentBody.ParentBody==null && p.ComponentBody.ChildBodies.Count==0;
        }
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
        public bool BeginLoading(Guid id,VoidPortalRegistry.Portal destination) => IsAuthority;
        public bool DestinationLoaded(Guid id,VoidPortalRegistry.Portal destination)
            => IsAuthority && VoidPortalSafety.Ready(terrain,Terrain.ToCell(destination.X),Terrain.ToCell(destination.Z));
        public bool TryFindLanding(Guid id,VoidPortalRegistry.Portal destination,out float x,out float y,out float z)
        {
            x=y=z=0;ComponentPlayer p;if(!Player(id,out p))return false;
            // Bounded cardinal and diagonal candidates, always outside the destination trigger.
            int[] dx={2,-2,0,0,2,-2,2,-2,3,-3,0,0};
            int[] dz={0,0,2,-2,2,2,-2,-2,0,0,3,-3};
            for(int i=0;i<dx.Length;i++)
            {
                var candidate=new Vector3((float)Math.Floor(destination.X)+dx[i]+.5f,destination.Y,(float)Math.Floor(destination.Z)+dz[i]+.5f);
                if(!VoidPortalSafety.SafeLanding(terrain,p,candidate))continue;
                x=candidate.X;y=candidate.Y;z=candidate.Z;return true;
            }
            return false;
        }
        public bool TryMove(Guid id,float x,float y,float z)
        {
            ComponentPlayer p;var position=new Vector3(x,y,z);
            if(!IsAuthority || !Player(id,out p) || !VoidPortalSafety.SafeLanding(terrain,p,position))return false;
            p.ComponentBody.Position=position;p.ComponentBody.Velocity=Vector3.Zero;return true;
        }
        public void EndLoading(Guid id) { }
        internal void Update()
        {
            if(!IsAuthority)return;
            current.Clear();int scanned=0;
            foreach(var p in players.ComponentPlayers)
            {
                if(++scanned>16)break;
                if(p==null || !p.IsAddedToProject || p.PlayerGuid==Guid.Empty || !p.PlayerData.IsMainPlayer)continue;
                Guid id=p.PlayerGuid;actors[id]=p;bool inAny=false;
                foreach(var portal in registry.Records)
                {
                    if(!portal.Active || !InPortal(p,portal))continue;
                    inAny=true;current.Add(id);
                    if(!inside.Contains(id) && portal.Target!=Guid.Empty)
                    {
                        var result=travel.Begin(id,portal.Id,clock.GameTime);
                        if(result==PortalTravelResult.Preparing)p.ComponentGui?.DisplaySmallMessage("星门：正在检查目的地",Color.White,false,false);
                    }
                    break;
                }
                if(!inAny)travel.ObserveOutside(id);
                var outcome=travel.Update(id,clock.GameTime);
                if(outcome==PortalTravelResult.Arrived)p.ComponentGui?.DisplaySmallMessage("星门传送完成",Color.White,false,false);
                else if(outcome==PortalTravelResult.Timeout)p.ComponentGui?.DisplaySmallMessage("目的地区块未就绪，传送取消",Color.Yellow,false,false);
                else if(outcome==PortalTravelResult.NoLanding)p.ComponentGui?.DisplaySmallMessage("目的地没有安全落点",Color.Yellow,false,false);
            }
            inside.Clear();foreach(var id in current)inside.Add(id);
            retired.Clear();foreach(var pair in actors)if(pair.Value==null || !pair.Value.IsAddedToProject)retired.Add(pair.Key);
            foreach(var id in retired){travel.Forget(id);inside.Remove(id);actors.Remove(id);}
        }
        public void Dispose(){travel.Dispose();inside.Clear();current.Clear();retired.Clear();actors.Clear();}
    }
}

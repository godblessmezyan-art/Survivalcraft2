using System;
using Engine;
using Game;
using Game.NetWork;
namespace SCIEPort.Features.AbyssReaper
{
    // Authoritative operations; no packet data is trusted without proximity and geometry checks.
    internal sealed class VoidPortalOperations
    {
        private readonly SubsystemAbyssReaper runtime;
        private readonly SubsystemTerrain terrain;
        internal VoidPortalOperations(SubsystemAbyssReaper runtime,SubsystemTerrain terrain) { this.runtime=runtime;this.terrain=terrain; }
        private bool Actor(ComponentPlayer player)
        {
            return CommonLib.WorkType!=WorkType.Client && player!=null && player.IsAddedToProject && player.Project==runtime.Project
                && player.PlayerGuid!=Guid.Empty && player.ComponentHealth.Health>0 && player.PlayerData.IsReadyForPlaying
                && player.ComponentSleep.SleepFactor<=0 && player.ComponentRider.Mount==null
                && runtime.Portals!=null;
        }
        private bool Near(ComponentPlayer player,VoidPortalRegistry.Portal portal)
        {
            Vector3 center=new Vector3(portal.X,portal.Y+1.5f,portal.Z),eye=player.ComponentCreatureModel.EyePosition;
            return WorldCutDamage.Finite(eye) && Vector3.DistanceSquared(eye,center)<=36f
                && !terrain.Raycast(eye,center,false,true,(v,d)=>BlocksManager.Blocks[Terrain.ExtractContents(v)].IsCollidable).HasValue;
        }
        internal bool TryCreate(ComponentPlayer player,int x,int y,int z,bool alongX,string name,out Guid id)
        {
            id=Guid.Empty;if(!Actor(player)||!VoidPortalRegistry.ValidName(name)||!VoidPortalSafety.SafePortalSite(terrain,player,x,y,z,alongX))return false;
            Vector3 center=new Vector3(x+.5f,y+1.5f,z+.5f),eye=player.ComponentCreatureModel.EyePosition;
            if(!WorldCutDamage.Finite(eye)||Vector3.DistanceSquared(eye,center)>36f || terrain.Raycast(eye,center,false,true,(v,d)=>BlocksManager.Blocks[Terrain.ExtractContents(v)].IsCollidable).HasValue)return false;
            Guid candidate=Guid.NewGuid();
            if(!runtime.Portals.TryAdd(candidate,player.PlayerGuid,name,x+.5f,y,z+.5f,alongX?0f:90f))return false;
            id=candidate;return true;
        }
        private bool OwnsNearby(ComponentPlayer player,Guid id,out VoidPortalRegistry.Portal portal)
        {
            // All connected players manage the shared world registry. Owner is provenance only.
            portal=null;return Actor(player)&&runtime.Portals.TryGet(id,out portal)&&portal.Active;
        }
        internal bool Rename(ComponentPlayer player,Guid id,string name)
        { VoidPortalRegistry.Portal portal;return OwnsNearby(player,id,out portal)&&runtime.Portals.Rename(id,name); }
        internal bool Link(ComponentPlayer player,Guid id,Guid destination)
        { VoidPortalRegistry.Portal portal;return OwnsNearby(player,id,out portal)&&runtime.Portals.Link(id,destination); }
        internal bool Delete(ComponentPlayer player,Guid id)
        { VoidPortalRegistry.Portal portal;return OwnsNearby(player,id,out portal)&&runtime.Portals.Remove(id); }
    }
}

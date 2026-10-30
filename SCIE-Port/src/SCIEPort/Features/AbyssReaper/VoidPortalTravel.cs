using System;
using System.Collections.Generic;
namespace SCIEPort.Features.AbyssReaper
{
    internal enum PortalTravelResult { Preparing, Arrived, Invalid, Unavailable, Timeout, NoLanding, Cancelled, Busy }
    // Runtime adapter must perform authoritative, loaded-terrain, collision and territory checks.
    internal interface IVoidPortalTravelWorld
    {
        bool IsAuthority { get; }
        bool IsActorEligible(Guid actor, VoidPortalRegistry.Portal source);
        bool BeginLoading(Guid actor, VoidPortalRegistry.Portal destination);
        bool DestinationLoaded(Guid actor, VoidPortalRegistry.Portal destination);
        bool TryFindLanding(Guid actor, VoidPortalRegistry.Portal destination, out float x, out float y, out float z);
        bool TryMove(Guid actor, float x, float y, float z);
        void EndLoading(Guid actor);
    }
    internal sealed class VoidPortalTravel
    {
        internal const double LoadTimeout=10;
        internal const int MaxPending=16;
        private sealed class Request { internal Guid Source, Target; internal double Deadline; internal bool FollowLink; }
        private readonly Dictionary<Guid,Request> pending=new Dictionary<Guid,Request>();
        private readonly HashSet<Guid> mustExit=new HashSet<Guid>();
        private readonly VoidPortalRegistry registry;
        private readonly IVoidPortalTravelWorld world;
        internal VoidPortalTravel(VoidPortalRegistry registry, IVoidPortalTravelWorld world)
        { this.registry=registry ?? throw new ArgumentNullException(nameof(registry)); this.world=world ?? throw new ArgumentNullException(nameof(world)); }
        internal int PendingCount => pending.Count;
        internal void ObserveOutside(Guid actor) { if (!pending.ContainsKey(actor)) mustExit.Remove(actor); }
        internal PortalTravelResult Begin(Guid actor, Guid sourceId, double now)
        {
            VoidPortalRegistry.Portal source;
            return registry.TryGet(sourceId,out source)?Begin(actor,sourceId,source.Target,now,true):PortalTravelResult.Invalid;
        }
        internal PortalTravelResult Begin(Guid actor,Guid sourceId,Guid targetId,double now) => Begin(actor,sourceId,targetId,now,false);
        private PortalTravelResult Begin(Guid actor,Guid sourceId,Guid targetId,double now,bool followLink)
        {
            if(!world.IsAuthority || actor==Guid.Empty || double.IsNaN(now) || double.IsInfinity(now))return PortalTravelResult.Invalid;
            if(mustExit.Contains(actor)||pending.ContainsKey(actor)||pending.Count>=MaxPending)return PortalTravelResult.Busy;
            VoidPortalRegistry.Portal source,target;
            if(!registry.TryGet(sourceId,out source)||!source.Active||targetId==Guid.Empty||!registry.TryGet(targetId,out target)||!target.Active||target.Id==source.Id)return PortalTravelResult.Invalid;
            if(!world.IsActorEligible(actor,source))return PortalTravelResult.Unavailable;
            try
            {
                if(!world.BeginLoading(actor,target)) { world.EndLoading(actor); return PortalTravelResult.Unavailable; }
                pending.Add(actor,new Request { Source=source.Id,Target=target.Id,Deadline=now+LoadTimeout,FollowLink=followLink });
                return PortalTravelResult.Preparing;
            }
            catch { world.EndLoading(actor);throw; }
        }
        internal PortalTravelResult Update(Guid actor,double now)
        {
            Request request;
            if(!pending.TryGetValue(actor,out request))return PortalTravelResult.Invalid;
            try
            {
                if(!world.IsAuthority || double.IsNaN(now)||double.IsInfinity(now))return End(actor,PortalTravelResult.Cancelled);
                VoidPortalRegistry.Portal source,target;
                if(!registry.TryGet(request.Source,out source)||!source.Active||(request.FollowLink&&source.Target!=request.Target)||!registry.TryGet(request.Target,out target)||!target.Active)return End(actor,PortalTravelResult.Cancelled);
                if(!world.IsActorEligible(actor,source))return End(actor,PortalTravelResult.Cancelled);
                if(now>=request.Deadline)return End(actor,PortalTravelResult.Timeout);
                if(!world.DestinationLoaded(actor,target))return PortalTravelResult.Preparing;
                float x,y,z;
                if(!world.TryFindLanding(actor,target,out x,out y,out z)||!WorldCutRules.Coordinates(x,y,z))return End(actor,PortalTravelResult.NoLanding);
                // Revalidation is required after landing search and before any movement.
                if(!world.IsAuthority || !registry.TryGet(request.Source,out source) || !source.Active || !world.IsActorEligible(actor,source) || !registry.TryGet(request.Target,out target) || !target.Active || (request.FollowLink&&source.Target!=request.Target) || !world.DestinationLoaded(actor,target))return End(actor,PortalTravelResult.Cancelled);
                if(!world.TryMove(actor,x,y,z))return End(actor,PortalTravelResult.Cancelled);
                mustExit.Add(actor);return End(actor,PortalTravelResult.Arrived);
            }
            catch { End(actor,PortalTravelResult.Cancelled);throw; }
        }
        internal PortalTravelResult Cancel(Guid actor) => pending.ContainsKey(actor)?End(actor,PortalTravelResult.Cancelled):PortalTravelResult.Invalid;
        internal void Forget(Guid actor) { if(pending.ContainsKey(actor))End(actor,PortalTravelResult.Cancelled);mustExit.Remove(actor); }
        private PortalTravelResult End(Guid actor,PortalTravelResult result)
        { pending.Remove(actor);world.EndLoading(actor);return result; }
        internal void Dispose()
        {
            var actors=new List<Guid>(pending.Keys);
            foreach(Guid actor in actors) { pending.Remove(actor);world.EndLoading(actor); }
            mustExit.Clear();
        }
    }
}

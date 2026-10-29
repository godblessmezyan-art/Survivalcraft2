using System;
using SCIEPort.Features.AbyssReaper;
internal static class PortalTravelTests
{
    sealed class World : IVoidPortalTravelWorld
    {
        public bool authority=true,eligible=true,begin=true,loaded,landing=true,move=true,throwLanding;
        public int starts,ends,moves;public Action duringLanding;
        public bool IsAuthority=>authority;
        public bool IsActorEligible(Guid actor,VoidPortalRegistry.Portal source)=>eligible;
        public bool BeginLoading(Guid actor,VoidPortalRegistry.Portal dest){starts++;return begin;}
        public bool DestinationLoaded(Guid actor,VoidPortalRegistry.Portal dest)=>loaded;
        public bool TryFindLanding(Guid actor,VoidPortalRegistry.Portal dest,out float x,out float y,out float z)
        { if(throwLanding)throw new InvalidOperationException();duringLanding?.Invoke();x=dest.X+2;y=dest.Y;z=dest.Z;return landing; }
        public bool TryMove(Guid actor,float x,float y,float z){moves++;return move;}
        public void EndLoading(Guid actor){ends++;}
    }
    static int count;
    static void Check(bool v,string m){count++;if(!v)throw new Exception("Portal travel: "+m);}
    internal static void Run()
    {
        Guid actor=Guid.NewGuid(),a=Guid.NewGuid(),b=Guid.NewGuid(),owner=Guid.NewGuid();
        var registry=new VoidPortalRegistry(Guid.NewGuid(),12);
        Check(registry.TryAdd(a,owner,"A",0,64,0,0)&&registry.TryAdd(b,owner,"B",30,64,0,0)&&registry.Link(a,b),"setup");
        var w=new World();var t=new VoidPortalTravel(registry,w);
        w.authority=false;Check(t.Begin(actor,a,0)==PortalTravelResult.Invalid&&w.starts==0,"client cannot start");w.authority=true;
        Check(t.Begin(actor,a,0)==PortalTravelResult.Preparing&&w.starts==1,"begin");
        Check(t.Update(actor,1)==PortalTravelResult.Preparing&&w.moves==0,"unloaded never moves");
        Check(t.Update(actor,10)==PortalTravelResult.Timeout&&w.ends==1&&w.moves==0,"timeout cleanup");
        Check(t.Begin(actor,a,11)==PortalTravelResult.Preparing,"retry after timeout");w.loaded=true;
        Check(t.Update(actor,12)==PortalTravelResult.Arrived&&w.moves==1&&w.ends==2,"valid arrival and cleanup");
        Check(t.Begin(actor,a,13)==PortalTravelResult.Busy,"cannot retrigger before exit");t.Forget(actor);
        Check(t.Begin(actor,a,13)==PortalTravelResult.Preparing && t.Cancel(actor)==PortalTravelResult.Cancelled,"disconnect forgets retrigger state and cancels pending");
        w.landing=false;Check(t.Begin(actor,a,14)==PortalTravelResult.Preparing&&t.Update(actor,15)==PortalTravelResult.NoLanding&&w.moves==1,"no safe landing");w.landing=true;
        Check(t.Begin(actor,a,16)==PortalTravelResult.Preparing,"start for unlink");registry.Link(a,Guid.Empty);
        Check(t.Update(actor,17)==PortalTravelResult.Cancelled&&w.moves==1,"target removed while pending");registry.Link(a,b);
        Check(t.Begin(actor,a,18)==PortalTravelResult.Preparing,"start for death");w.eligible=false;
        Check(t.Update(actor,19)==PortalTravelResult.Cancelled&&w.moves==1,"actor no longer eligible");w.eligible=true;
        Check(t.Begin(actor,a,20)==PortalTravelResult.Preparing,"start for client switch");w.authority=false;
        Check(t.Update(actor,21)==PortalTravelResult.Cancelled&&w.moves==1,"authority lost");w.authority=true;
        Check(t.Begin(actor,a,22)==PortalTravelResult.Preparing,"start for exception");w.throwLanding=true;
        bool threw=false;try{t.Update(actor,23);}catch(InvalidOperationException){threw=true;}
        Check(threw&&t.PendingCount==0,"exception releases state");w.throwLanding=false;
        Check(t.Begin(actor,a,24)==PortalTravelResult.Preparing,"start for invalidated source");
        w.duringLanding=()=>registry.Remove(a);
        Check(t.Update(actor,25)==PortalTravelResult.Cancelled&&w.moves==1,"source deletion during search cannot move");w.duringLanding=null;
        Check(registry.TryAdd(a,owner,"A",0,64,0,0)&&registry.Link(a,b),"restore source");
        Check(t.Begin(actor,a,26)==PortalTravelResult.Preparing,"start for dispose");int ends=w.ends;t.Dispose();
        Check(t.PendingCount==0&&w.ends==ends+1,"dispose releases loading");
        Console.WriteLine("Portal travel authority/timeout/cleanup/retrigger: "+count+" checks passed (simulated world only).");
    }
}

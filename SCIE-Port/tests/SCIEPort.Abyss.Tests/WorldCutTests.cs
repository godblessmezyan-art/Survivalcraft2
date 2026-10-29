using System;
using System.Collections.Generic;
using SCIEPort.Features.AbyssReaper;
internal static class WorldCutTests
{
    private static int count;
    private static void Check(bool value,string message) { count++;if(!value)throw new Exception("WorldCut: "+message); }
    internal static void Run()
    {
        var cast=new WorldCutMotion(10);
        Check(!cast.BeginFlight(10.37),"windup cannot release early");
        Check(cast.BeginFlight(10.39),"windup releases");Check(!cast.BeginFlight(11),"release once");
        Check(cast.NextDistance(float.NaN)==0,"nonfinite delta does not advance");
        Check(cast.NextDistance(-1)==0,"negative delta does not advance");
        Check(cast.NextDistance(99)==2,"hitch movement bounded");
        int steps=0;
        while(cast.Distance<25) { float from=cast.Distance;cast.Move(cast.NextDistance(.016f));Check(cast.Distance>=from&&cast.Distance<=25,"range budget");if(++steps>1000)throw new Exception("unbounded flight"); }
        Check(cast.Distance==25,"ends at exactly 25");Check(cast.Close(11,25),"range closes");Check(!cast.Close(11.1,4),"close position cannot be replayed");
        Check(!cast.TakeCloseImpact(11.21),"close damage delayed");Check(cast.TakeCloseImpact(11.23),"close damage edge");Check(!cast.TakeCloseImpact(12),"close damage once");Check(cast.Expired(11.5),"end cleanup");
        var hitClose=new WorldCutMotion(0);hitClose.BeginFlight(.4);hitClose.Move(7);Check(hitClose.Close(.7,7),"contact closes early");
        Check(hitClose.TakeCloseImpact(1.3),"skipped frame still yields closure edge");Check(hitClose.Expired(1.3),"skip cleanup after impact");
        var cancelled=new WorldCutMotion(0);cancelled.Cancel();Check(!cancelled.BeginFlight(1)&&!cancelled.TakeCloseImpact(1),"cancel has no release/damage");
        var ledger=new WorldCutHitLedger<int>();
        for(int i=0;i<24;i++){Check(ledger.Add(i,false),"sweep first hit");Check(!ledger.Add(i,false),"sweep duplicate");Check(ledger.Add(i,true),"closure independently first hit");Check(!ledger.Add(i,true),"closure duplicate");}
        Check(!ledger.Add(25,false)&&!ledger.Add(25,true),"phase budgets");Check(ledger.Count(false)==24&&ledger.Count(true)==24,"two bounded phases");ledger.Clear();Check(ledger.Add(0,false),"released ledger reusable");
        Check(WorldCutRules.Swept(2,2.9f,0,.2f,.2f,.6f,1.5f,2),"six-wide blade");
        Check(!WorldCutRules.Swept(2,3.3f,0,.2f,.2f,.6f,1.5f,2),"outside width");
        Check(!WorldCutRules.Swept(2,0,2,.2f,.2f,.6f,1.5f,2),"outside height");
        Check(!WorldCutRules.Swept(8,0,0,.2f,.2f,.6f,1.5f,2),"no damage beyond swept interval");
        Check(WorldCutRules.Swept(1.8f,0,0,.1f,.2f,.6f,1.5f,2),"before wall permitted");
        Check(!WorldCutRules.Swept(2.7f,0,0,.1f,.2f,.6f,1.5f,2),"wall-clipped prefix blocks target behind wall");
        Check(!WorldCutRules.Swept(25.5f,0,0,.1f,.2f,.6f,24.5f,25),"range does not overshoot");
        Check(WorldCutRules.Collapse(2,0,0,.2f,.2f,.6f),"closing radius");Check(!WorldCutRules.Collapse(4,0,0,.2f,.2f,.6f),"closing bounded");
        var gate=new WorldCutInputGate();Check(gate.CanRequest(false,false,false),"idle can cast");gate.RouteNormal(true,false);Check(!gate.CanRequest(false,false,false),"normal hold cannot be stolen on release");
        gate.RouteNormal(false,false);Check(gate.CanRequest(false,false,false),"normal release completes");Check(!gate.CanRequest(false,true,false),"normal swing wins");
        gate.Accepted();Check(!gate.RouteNormal(true,true),"skill owns input");Check(!gate.RouteNormal(true,false),"held attack during cast drained");Check(!gate.RouteNormal(false,false),"drain release swallowed");Check(gate.RouteNormal(true,false),"fresh normal input restored");
        var skippedMelee=new AbyssCombo();skippedMelee.Input(true,0);skippedMelee.Input(false,.01);bool normalImpactPending=true;
        Check(skippedMelee.Phase(1)==1&&!gate.CanRequest(false,normalImpactPending,false),"hitch must not let skill steal unresolved normal impact");
        Check(skippedMelee.Impact(1),"original combo still settles skipped impact");normalImpactPending=false;gate.Reset();Check(gate.CanRequest(false,normalImpactPending,false),"skill available after normal impact settled");
        var cooldown=new WorldCutSession();Check(cooldown.Queue(0),"request accepted");Check(!cooldown.Queue(.1),"no double request");Check(cooldown.Sent==1,"one sequence");
        Check(cooldown.NewRequest(1)&&!cooldown.NewRequest(1),"server duplicate rejected");Check(cooldown.Accept(.5),"server accept starts cooldown");
        Check(cooldown.ActiveUntil>.5,"input ownership independent of visual object allocation");
        Check(cooldown.ReadyAt==10.5&&!cooldown.Queue(10.49),"ten seconds cooldown survives panel detach");Check(!cooldown.Accept(1)&&cooldown.ReadyAt==10.5,"duplicate cannot reset cooldown");Check(cooldown.Queue(10.5),"ready exactly at deadline");Check(cooldown.TimedOut(12.5),"lost acknowledgement timeout");
        Check(cooldown.PendingUntil>0,"expired request remains cancellable before cleanup");cooldown.Cancelled=cooldown.Sent;cooldown.PendingUntil=0;Check(!cooldown.TimedOut(20),"timeout handled once");
        var late=new WorldCutSession();late.BindEntity(5);late.Queue(0);late.Cancelled=late.Sent;late.Bind(5,Guid.Empty);
        Check(late.Cancelled==late.Sent,"first late acknowledgement cannot clear cancellation tombstone");
        late.Closed=1;late.BindEntity(5);Check(late.Closed==1,"duplicate close remains tombstoned");
        double savedReady=cooldown.ReadyAt;cooldown.Bind(10,Guid.Empty);Check(cooldown.NewRequest(1),"new entity sequence scope");
        cooldown.Bind(10,Guid.Empty);Check(!cooldown.NewRequest(1),"same connection replay blocked");
        cooldown.Bind(10,new Guid("42de7768-2785-41ef-bbed-3ab91e0d7ec9"));Check(cooldown.NewRequest(1),"reconnect accepts fresh sequence");Check(cooldown.ReadyAt==savedReady,"reconnect cannot reset cooldown");
        Check(late.MatchesEntity(5)&&!late.MatchesEntity(6),"old entity cleanup cannot clear new entity action");
        Check(WorldCutRules.Coordinates(-999999,254,999999)&&!WorldCutRules.Coordinates(1000000,64,0)&&!WorldCutRules.Coordinates(0,0,0)&&!WorldCutRules.Coordinates(float.NaN,64,0),"coordinate/height/nonfinite limits");
        Check(WorldCutRules.Damage(float.NaN,100)==100&&WorldCutRules.Damage(float.PositiveInfinity,40)==40,"nonfinite config safe defaults");
        Check(WorldCutRules.Damage(-5,100)==0&&WorldCutRules.Damage(10000,100)==1000,"configured damage bounded");
        Check(WorldCutRules.Newer(1,uint.MaxValue)&&!WorldCutRules.Newer(uint.MaxValue,1)&&!WorldCutRules.Newer(0,0),"sequence wrap and zero");
        for(byte kind=0;kind<8;kind++)
        {
            Check(!WorldCutRules.Dispatch(true,false,false,kind),"spoofed sender rejected");Check(!WorldCutRules.Dispatch(false,false,true,kind),"client events require server");
            Check(WorldCutRules.Dispatch(true,false,true,kind)==(kind==0||kind==4),"server accepts only request/cancel");
            Check(WorldCutRules.Dispatch(false,true,false,kind)==(kind==1||kind==2||kind==3||kind==5),"client accepts only server events");
        }
        Check(WorldCutRules.Authority(true,false)&&WorldCutRules.Authority(false,true)&&!WorldCutRules.Authority(false,false),"authority must be explicit");
        // Geometry and simulation do not receive a visual-quality flag: off/low/full share one result.
        for(int quality=0;quality<3;quality++)
        {
            var sim=new WorldCutMotion(0);sim.BeginFlight(.4);for(int i=0;i<100;i++)sim.Move(sim.NextDistance(.016f));Check(sim.Distance==25,"visual setting independent travel");
        }
        for(int i=0;i<=100;i++)
        {
            float side,forward,thickness;WorldCutRules.Crescent(i/100f,out side,out forward,out thickness);
            Check(Math.Abs(side)<=3.001f&&forward<=.0001f&&thickness>=0,"crescent silhouette bounds");
        }
        foreach(float width in new[]{320f,640f,853f,1280f,1920f})foreach(float height in new[]{360f,480f,720f,1080f})
        {
            WorldCutHudLayout.Rect rect;var blockers=new List<WorldCutHudLayout.Rect>{new WorldCutHudLayout.Rect(0,height-100,width,100)};
            Check(WorldCutHudLayout.Place(width,height,blockers,out rect),"touch HUD fits resolutions");Check(rect.X>=0&&rect.Y>=0&&rect.X+rect.W<=width&&rect.Y+rect.H<=height,"HUD remains in viewport");
            Check(!rect.Overlaps(blockers[0]),"native hotbar not covered");Check(WorldCutHudLayout.Slot>=44,"minimum touch target");
        }
        WorldCutHudLayout.Rect hidden;Check(!WorldCutHudLayout.Place(270,200,new List<WorldCutHudLayout.Rect>(),out hidden),"hide when no safe space");
        var covered=new List<WorldCutHudLayout.Rect>{new WorldCutHudLayout.Rect(0,0,1000,1000)};Check(!WorldCutHudLayout.Place(640,480,covered,out hidden),"hide rather than cover native controls");
        Console.WriteLine("WorldCut state/input/dedup/geometry/authority/cooldown/layout: "+count+" checks passed.");
    }
}

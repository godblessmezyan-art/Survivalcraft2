using System;
using SCIEPort.Features.AbyssReaper;
internal static class VoidHorizonTests
{
    private static int count;
    private static void Check(bool value,string text){count++;if(!value)throw new Exception("Void horizon: "+text);}
    internal static void Run()
    {
        Check(!VoidHorizonRules.Live(-.001)&&VoidHorizonRules.Live(0)&&VoidHorizonRules.Live(4.199)&&!VoidHorizonRules.Live(4.2),"immunity starts on acceptance and ends before cleanup frame");
        Check(!VoidHorizonRules.Live(double.NaN)&&!VoidHorizonRules.Live(double.PositiveInfinity),"invalid clock never grants immunity");
        for(int hostile=0;hostile<2;hostile++)for(int passive=0;passive<2;passive++)
            Check(!VoidHorizonRules.TargetAllowed(true,hostile!=0,passive!=0),"players excluded irrespective of target settings");
        Check(VoidHorizonRules.TargetAllowed(false,true,false)&&VoidHorizonRules.TargetAllowed(false,false,false)&&VoidHorizonRules.TargetAllowed(false,false,true),"non-player policy retained");
        foreach(float health in new[]{.01f,.5f,1f,100f,1000000f,float.MaxValue})
            Check(VoidHorizonRules.ExecutionAmount(health)>=health && !float.IsInfinity(VoidHorizonRules.ExecutionAmount(health)),"lethal normalized health independent of armor and resilience");
        Check(VoidHorizonRules.ExecutionAmount(float.NaN)==0&&VoidHorizonRules.ExecutionAmount(0)==0,"invalid health cannot become damage");
        for(byte kind=0;kind<10;kind++)
        {
            Check(!VoidHorizonRules.Dispatch(true,false,false,kind),"spoofed owner rejected");
            Check(!VoidHorizonRules.Dispatch(false,false,true,kind),"peer cannot impersonate server");
            Check(VoidHorizonRules.Dispatch(true,false,true,kind)==(kind==0||kind==3),"server accepts only request/cancel");
            Check(VoidHorizonRules.Dispatch(false,true,false,kind)==(kind==1||kind==2||kind==4),"client accepts only authoritative state");
        }
        Check(VoidHorizonRules.ValidSnapshot(0,180)&&VoidHorizonRules.ValidSnapshot(4.2f,0)&&!VoidHorizonRules.ValidSnapshot(float.NaN,0)&&!VoidHorizonRules.ValidSnapshot(1,float.PositiveInfinity)&&!VoidHorizonRules.ValidSnapshot(4.201f,1)&&!VoidHorizonRules.ValidSnapshot(0,-1),"wire bounds");
        var peer=new HorizonSession{Seen=0,Feedback=null,FeedbackUntil=0};Guid token=Guid.NewGuid();peer.Bind(11,token);
        Check(peer.Request(10)==1&&peer.Request(10.1)==0,"one pending request");
        Check(peer.Process(1)&&!peer.Process(1)&&!peer.Process(0),"server replay protection");
        Check(!peer.Receive(2,1,VoidHorizonRules.Snapshot)&&peer.Receive(1,1,VoidHorizonRules.Snapshot),"snapshot bound to pending cast");
        Check(!peer.Receive(1,1,VoidHorizonRules.Snapshot)&&peer.Receive(1,3,VoidHorizonRules.Snapshot)&&!peer.Receive(1,2,VoidHorizonRules.Snapshot),"duplicate and reordered snapshots ignored");
        Check(peer.Receive(1,4,VoidHorizonRules.End)&&!peer.Receive(1,5,VoidHorizonRules.Snapshot),"terminal tombstone prevents resurrection");
        peer.ReadyAt=190;peer.Bind(12,Guid.NewGuid());
        Check(peer.Request(20)==0&&peer.ReadyAt==190&&peer.Process(1),"respawn/reconnect resets replay guard without clearing cooldown");
        Check(peer.Request(190)==2,"fresh request after exact cooldown");
        peer.Cancelled=peer.Sent;Check(!peer.Receive(peer.Sent,6,VoidHorizonRules.Snapshot),"local cancellation survives delayed accept");
        var rollover=new HorizonSession{Sent=uint.MaxValue,Processed=uint.MaxValue,Received=uint.MaxValue};
        Check(rollover.Request(0)==1&&rollover.Process(1)&&rollover.Receive(1,1,VoidHorizonRules.Snapshot),"uint wrap skips reserved zero");
        Console.WriteLine("Void horizon authority/replay/immunity: "+count+" checks passed (rules only).");
    }
}

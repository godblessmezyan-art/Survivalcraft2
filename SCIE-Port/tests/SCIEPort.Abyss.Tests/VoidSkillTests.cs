using System;
using SCIEPort.Features.AbyssReaper;
internal static class VoidSkillTests
{
    static int count;
    static void Check(bool value,string name) { count++;if(!value)throw new Exception("Void skill: "+name); }
    internal static void Run()
    {
        Check(VoidSkillRules.VfxSegments(-1,8,16,24)==8 && VoidSkillRules.VfxSegments(1,8,16,24)==16 && VoidSkillRules.VfxSegments(9,8,16,24)==24,"quality bounds and budgets");
        Check(VoidSkillRules.InBlinkRange(18)&&!VoidSkillRules.InBlinkRange(18.01f)&&!VoidSkillRules.InBlinkRange(float.NaN),"blink range boundary");
        Check(VoidSkillRules.InDomain(40,4,0,4)&&!VoidSkillRules.InDomain(40.01f,0,0,4)&&!VoidSkillRules.InDomain(0,4.01f,0,4),"domain radius and height");
        Check(VoidSkillRules.Phase(0)==HorizonPhase.Awakening && VoidSkillRules.Phase(2)==HorizonPhase.SkyRift && VoidSkillRules.Phase(5)==HorizonPhase.Descent && VoidSkillRules.Phase(8)==HorizonPhase.Domain && VoidSkillRules.Phase(14)==HorizonPhase.Collapse && VoidSkillRules.Phase(17)==HorizonPhase.Finished,"five phase edges");
        var cooldown=new VoidCooldowns();var id=Guid.NewGuid();
        Check(!cooldown.Start(Guid.Empty,0,15)&&cooldown.Start(id,10,15),"cooldown begins only for valid actor");
        Check(!cooldown.Start(id,24.99,15)&&cooldown.Remaining(id,24)==1,"blink exact cooldown");
        Check(cooldown.Start(id,25,180)&&cooldown.Remaining(id,25)==180,"horizon cooldown after ready");
        Check(!cooldown.Start(Guid.NewGuid(),double.NaN,15),"invalid clock cannot begin cooldown");
        var timeline=new HorizonTimeline<int>(10);
        Check(timeline.TakeTick(17.9)==-1 && timeline.TakeTick(18)==0 && timeline.TakeTick(18.5)==-1,"first tick only once");
        Check(timeline.TakeTick(21.2)==3 && timeline.TakeTick(24)==-1,"no catch-up burst beyond domain");
        Check(!timeline.TakeFinal(25.99) && timeline.TakeFinal(26) && !timeline.TakeFinal(26.1),"single terminal edge");
        Check(timeline.TakeTick(20)==-1,"terminal prevents out-of-order erosion");
        Check(timeline.AddFinalTarget(1,2)&&!timeline.AddFinalTarget(1,2)&&timeline.AddFinalTarget(2,2)&&!timeline.AddFinalTarget(3,2),"final damage dedup and budget");
        var invalid=new HorizonTimeline<int>(0);Check(!invalid.TakeFinal(double.PositiveInfinity),"invalid clock cannot settle");
        var hitch=new HorizonTimeline<int>(0);Check(hitch.TakeFinal(18)&&!hitch.TakeFinal(19),"hitch settles final once before cleanup");
        var cancel=new HorizonTimeline<int>(0);cancel.Cancel();Check(cancel.TakeTick(9)==-1&&!cancel.TakeFinal(16)&&!cancel.AddFinalTarget(1,2),"cancel suppresses all impacts");
        Console.WriteLine("Void skill geometry/timeline/dedup: "+count+" checks passed (rules only).");
    }
}

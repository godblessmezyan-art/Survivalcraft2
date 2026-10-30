using System;
using SCIEPort.Features.AbyssReaper;
internal static class VoidSkillTests
{
    static int count;
    static void Check(bool value,string name) { count++;if(!value)throw new Exception("Void skill: "+name); }
    internal static void Run()
    {
        Check(VoidSkillRules.VfxSegments(-1,8,16,24)==8 && VoidSkillRules.VfxSegments(1,8,16,24)==16 && VoidSkillRules.VfxSegments(9,8,16,24)==24,"quality bounds and budgets");
        Check(VoidHorizonVisualRules.Smooth(-1)==0 && VoidHorizonVisualRules.Smooth(0)==0 && VoidHorizonVisualRules.Smooth(.5f)==.5f && VoidHorizonVisualRules.Smooth(2)==1,"domain transition clamp and midpoint");
        Check(VoidHorizonVisualRules.PrimitiveBudget(-1)==2640 && VoidHorizonVisualRules.PrimitiveBudget(1)==3960 && VoidHorizonVisualRules.PrimitiveBudget(4)==5280,"presentation budgets by quality");
        Check(VoidSkillRules.InBlinkRange(18)&&!VoidSkillRules.InBlinkRange(18.01f)&&!VoidSkillRules.InBlinkRange(float.NaN),"blink range boundary");
        Check(VoidSkillRules.InDomain(40,4,0,4)&&!VoidSkillRules.InDomain(40.01f,0,0,4)&&!VoidSkillRules.InDomain(0,4.01f,0,4),"domain radius and height");
        Check(VoidSkillRules.Phase(0)==HorizonPhase.Awakening && VoidSkillRules.Phase(2)==HorizonPhase.SkyRift && VoidSkillRules.Phase(5)==HorizonPhase.Descent && VoidSkillRules.Phase(8)==HorizonPhase.Domain && VoidSkillRules.Phase(14)==HorizonPhase.Collapse && VoidSkillRules.Phase(17)==HorizonPhase.Finished,"five phase edges");
        var blinkAction=new BlinkActionClock(10);
        Check(blinkAction.Progress(9)==-1 && blinkAction.Progress(10)==0 && blinkAction.Progress(10.2)>0 && blinkAction.Progress(10.43)==-1,"blink action boundaries and invalid time");
        var input=new WorldCutInputGate();input.Accepted();
        Check(!input.CanRequest(false,false,blinkAction.Progress(10.1)>=0) && !input.RouteNormal(true,blinkAction.Progress(10.1)>=0),"blink action prevents skill and held attack takeover");
        Check(!input.RouteNormal(true,blinkAction.Progress(10.43)>=0) && !input.RouteNormal(false,false) && input.RouteNormal(true,false),"held attack drains before fresh normal input");
        blinkAction.Cancel();Check(blinkAction.Progress(10)==-1 && blinkAction.Progress(double.NaN)==-1,"blink action cancel releases input");
        var cooldown=new VoidCooldowns();var id=Guid.NewGuid();
        Check(!cooldown.Start(Guid.Empty,0,15)&&cooldown.Start(id,10,15),"cooldown begins only for valid actor");
        Check(!cooldown.Start(id,24.99,15)&&cooldown.Remaining(id,24)==1,"blink exact cooldown");
        Check(cooldown.Start(id,25,180)&&cooldown.Remaining(id,25)==180,"horizon cooldown after ready");
        Check(!cooldown.Start(Guid.NewGuid(),double.NaN,15),"invalid clock cannot begin cooldown");
        var cues=new HorizonCueTimeline();
        Check(cues.Take(-1)==-1 && cues.Take(double.NaN)==-1 && cues.Take(0)==0 && cues.Take(1)==-1,"cue begins once and rejects invalid time");
        Check(cues.Take(2)==1 && cues.Take(5)==-1 && cues.Take(14)==2 && cues.Take(15.7)==-1 && cues.Take(16)==3 && cues.Take(17)==-1,"ordered phase cues and silence");
        Check(cues.Take(2)==-1 && cues.Take(double.PositiveInfinity)==-1,"cue clock regression and infinity rejected");
        var skipped=new HorizonCueTimeline();Check(skipped.Take(15.5)==-1 && skipped.Take(16)==3,"late collapse is skipped to preserve silence");
        var late=new HorizonCueTimeline();
        Check(late.Take(16.2)==3 && late.Take(16.3)==-1,"frame hitch plays current cue once without replaying old stages");
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

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
        Check(VoidHorizonVisualRules.PrimitiveBudget(-1)==3456 && VoidHorizonVisualRules.PrimitiveBudget(1)==5184 && VoidHorizonVisualRules.PrimitiveBudget(4)==6912,"presentation budgets by quality");
        Check(VoidSkillRules.InBlinkRange(18)&&!VoidSkillRules.InBlinkRange(18.01f)&&!VoidSkillRules.InBlinkRange(float.NaN),"blink range boundary");
        Check(VoidSkillRules.InDomain(128,4,0,4)&&!VoidSkillRules.InDomain(128.01f,0,0,4)&&!VoidSkillRules.InDomain(0,4.01f,0,4),"domain radius and height");
        Check(VoidSkillRules.Phase(0)==HorizonPhase.Awakening && VoidSkillRules.Phase(.5)==HorizonPhase.Descent && VoidSkillRules.Phase(1.3)==HorizonPhase.Domain && VoidSkillRules.Phase(4.2)==HorizonPhase.Finished,"entry then hole then engulf then instant exit");
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
        Check(cues.Take(-1)==-1 && cues.Take(double.NaN)==-1 && cues.Take(0)==0 && cues.Take(.4)==-1,"cue begins once and rejects invalid time");
        Check(cues.Take(.5)==1 && cues.Take(1.3)==2 && cues.Take(3.2)==-1 && cues.Take(4)==3 && cues.Take(4.2)==-1,"cues follow entry, black hole, expansion and final release");
        Check(cues.Take(2)==-1 && cues.Take(double.PositiveInfinity)==-1,"cue regression rejected");
        var late=new HorizonCueTimeline();Check(late.Take(4.1)==3 && late.Take(4.15)==-1,"no stale cue burst");
        var timeline=new HorizonTimeline<int>(10);
        Check(!timeline.TakeFinal(14.199) && timeline.TakeFinal(14.2) && !timeline.TakeFinal(15),"execute once exactly on full coverage");
        Check(timeline.AddFinalTarget(1,2)&&!timeline.AddFinalTarget(1,2)&&timeline.AddFinalTarget(2,2)&&!timeline.AddFinalTarget(3,2),"settlement ledger dedup");
        Check(VoidHorizonVisualRules.Entry(.5)==1 && VoidHorizonVisualRules.Hole(.5)==0 && VoidHorizonVisualRules.Hole(1.3)==1,"domain enters before hole forms");
        float prior=0;for(int i=0;i<=100;i++)
        {
            double t=1.3+(4.2-1.3)*i/100;float angle=VoidHorizonVisualRules.ExpansionAngle(t);
            Check(angle>=prior && angle<=(float)Math.PI+.00001f,"hole-origin expansion monotonically covers sphere");prior=angle;
        }
        Check(Math.Abs(prior-(float)Math.PI)<.00001f && VoidSkillRules.FinalImpact==VoidSkillRules.HorizonDuration,"full coverage and exit are the same deadline");
        var invalid=new HorizonTimeline<int>(0);Check(!invalid.TakeFinal(double.PositiveInfinity),"invalid clock cannot settle");
        var hitch=new HorizonTimeline<int>(0);Check(hitch.TakeFinal(18)&&!hitch.TakeFinal(19),"hitch still executes once");
        var cancel=new HorizonTimeline<int>(0);cancel.Cancel();Check(!cancel.TakeFinal(20)&&!cancel.AddFinalTarget(1,2),"cancel suppresses all impacts");
        Console.WriteLine("Void skill geometry/timeline/dedup: "+count+" checks passed (rules only).");
    }
}

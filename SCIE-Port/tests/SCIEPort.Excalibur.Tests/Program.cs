using System;
using System.Collections.Generic;
using SCIEPort.Features.Excalibur;
using SCIEPort.Features.Artifacts;
using SCIEPort.Features.Stormhammer;
static class Program
{
    static int checks;static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;}
    static void Main()
    {
        Check(ExcaliburRules.Cooldown(1)==15&&ExcaliburRules.Cooldown(2)==35&&ExcaliburRules.Cooldown(3)==75,"v2 cooldowns");
        Check(ExcaliburRules.Power(1,1,0,0)==0&&ExcaliburRules.Power(3,1,0,0)==0,"enchantment and Avalon never deal beam damage");
        Check(ExcaliburRules.Power(2,35,0,0)==90&&ExcaliburRules.Power(2,35,4,0)==90,"second-skill core and edge unchanged");
        foreach(float a in new[]{0,1,16,34.999f,35f})foreach(float b in new[]{-4,-1.5f,0,1.5f,4})
            Check(ExcaliburRules.Power(2,a,b,0)==90,"inclusive geometry");
        foreach(float bad in new[]{float.NaN,float.NegativeInfinity,float.PositiveInfinity})
        {Check(ExcaliburRules.Power(2,bad,0,0)==0,"bad forward");Check(ExcaliburRules.Power(2,1,bad,0)==0,"bad side");Check(ExcaliburRules.Power(2,1,0,bad)==0,"bad height");Check(!ExcaliburRules.Sanctuary(bad),"invalid sanctuary clock");}
        foreach(var v in new[]{new[]{35.001f,0f,0f},new[]{-0.001f,0f,0f},new[]{10f,4.001f,0f},new[]{10f,0f,4.001f}})
            Check(ExcaliburRules.Power(2,v[0],v[1],v[2])==0,"hard range/width/height");
        Check(24+ExcaliburRules.AddonPower<(40+25)*.65f,"enchanted melee lower than Mjolnir");
        Check(ExcaliburRules.ImpactPower==90&&ExcaliburRules.TickPower==45&&ExcaliburRules.BeamSeconds==6,"user requested impact90 + six 45-point pulses");
        Check(ExcaliburRules.AttackFrequency(true)==1.1f&&ExcaliburRules.AttackFrequency(false)==1.1f,"default attack speed unchanged; old blessing removed");
        Check(ExcaliburRules.MeleeReach==2+1&&ExcaliburRules.EnchantDuration==8&&ExcaliburRules.AddonPower==15,"enchant reach, duration, flat add-on");
        Check(ExcaliburRules.Release==1.75f&&ExcaliburRules.Duration==2.45f,"visible downward swing and recovery");
        Check(!ExcaliburRules.Sanctuary(.599f)&&ExcaliburRules.Sanctuary(.6f)&&ExcaliburRules.Sanctuary(3.599f)&&!ExcaliburRules.Sanctuary(3.6f),"three-second sanctuary boundaries unchanged");
        Check(ExcaliburRules.AvalonHealing==.2f&&ExcaliburRules.RegenPerSecond==.1f&&ExcaliburRules.RegenSeconds==75,"instant20 percent and one of ten native health points per second");
        Check(ExcaliburRules.BeamTick(ExcaliburRules.Release)==0&&ExcaliburRules.BeamTick(ExcaliburRules.Release+.999f)==0&&ExcaliburRules.BeamTick(ExcaliburRules.Release+1)==1&&ExcaliburRules.BeamTick(ExcaliburRules.BeamEnd)==6,"global one-second DOT boundaries");
        Check(ExcaliburRules.BeamOpacity(ExcaliburRules.Release)==1&&ExcaliburRules.BeamOpacity(ExcaliburRules.BeamEnd-2)==1&&Math.Abs(ExcaliburRules.BeamOpacity(ExcaliburRules.BeamEnd-1)-.5f)<.00001f&&ExcaliburRules.BeamOpacity(ExcaliburRules.BeamEnd)==0,"hold four seconds, smooth two-second fade");
        float previous=1;for(float age=ExcaliburRules.BeamEnd-2;age<=ExcaliburRules.BeamEnd;age+=.01f){float opacity=ExcaliburRules.BeamOpacity(age);Check(opacity<=previous+.00001f&&opacity>=0,"monotonic fade");previous=opacity;}
        var ledger=new ExcaliburRules.HitLedger<int>();
        for(int i=0;i<64;i++){Check(ledger.Add(i),"distinct target");for(int repeat=0;repeat<4;repeat++)Check(!ledger.Add(i),"duplicate frame/body/replay");}
        Check(!ledger.Add(65)&&ledger.Count==64,"hard target budget");
        Check(new ExcaliburRules.HitLedger<int>().Add(0),"new cast can hit same target");
        Check(ExcaliburRules.Newer(1,uint.MaxValue)&&!ExcaliburRules.Newer(5,5)&&!ExcaliburRules.Newer(3,4),"sequence wrap/duplicate/reorder");
        for(float t=0;t<=2.2f;t+=.025f){float p,y,l;ExcaliburRules.Pose(t,out p,out y,out l);Check(ExcaliburRules.Finite(p)&&Math.Abs(p)<1.2f&&Math.Abs(y)<.3f&&Math.Abs(l)<.5f,"bounded raise/swing pose");}
        foreach(var size in new[]{new[]{1280f,720f},new[]{1600f,720f},new[]{1024f,768f},new[]{960f,540f},new[]{800f,480f}})
        foreach(bool left in new[]{false,true})
        {
            float w=size[0],h=size[1];var look=new ArtifactSkillLayout.Rect{X=left?16:w-144,Y=h-156,W=128,H=128};
            var obstacles=new List<ArtifactSkillLayout.Rect>{look,new ArtifactSkillLayout.Rect{X=left?w-144:16,Y=h-156,W=128,H=128},new ArtifactSkillLayout.Rect{X=w/2-200,Y=h-58,W=400,H=50}};
            ArtifactSkillLayout.Rect[] places;bool safe=ArtifactSkillLayout.Place(w,h,look,obstacles,new[]{72f,72f,88f},left,out places);
            Check(safe,"phone logical resolution/layout");
            foreach(var place in places){Check(place.X>=8&&place.Y>=8&&place.X+place.W<=w-8&&place.Y+place.H<=h-8,"onscreen");foreach(var o in obstacles)Check(!place.Overlaps(o),"native controls untouched");}
            Check(places[2].Y<places[0].Y,"ultimate above skills");
        }
        ArtifactSkillLayout.Rect[] hidden;
        Check(!ArtifactSkillLayout.Place(300,300,new ArtifactSkillLayout.Rect{X=100,Y=100,W=128,H=128},new[]{new ArtifactSkillLayout.Rect{X=0,Y=0,W=300,H=300}},new[]{72f,72f,88f},false,out hidden),"hide if no safe placement");
        Console.WriteLine("Excalibur rules/balance/once-only ledger/phone layout: "+checks+" assertions passed (offline).");
    }
}

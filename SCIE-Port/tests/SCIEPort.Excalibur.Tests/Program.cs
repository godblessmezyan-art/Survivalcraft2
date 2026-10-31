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
        Check(ExcaliburRules.Cooldown(1)==10&&ExcaliburRules.Cooldown(2)==35&&ExcaliburRules.Cooldown(3)==65,"cooldowns");
        Check(ExcaliburRules.Power(1,10,0,0)==18,"lake power");
        Check(ExcaliburRules.Power(3,35,0,0)==55&&ExcaliburRules.Power(3,35,4,0)==25,"core and edge");
        foreach(float a in new[]{0,1,16,34.999f,35f})foreach(float b in new[]{-4,-1.5f,0,1.5f,4})
            Check(ExcaliburRules.Power(3,a,b,0)==(Math.Abs(b)<=1.5f?55:25),"inclusive geometry");
        foreach(float bad in new[]{float.NaN,float.NegativeInfinity,float.PositiveInfinity})
        {Check(ExcaliburRules.Power(3,bad,0,0)==0,"bad forward");Check(ExcaliburRules.Power(3,1,bad,0)==0,"bad side");Check(ExcaliburRules.Power(3,1,0,bad)==0,"bad height");}
        foreach(var v in new[]{new[]{35.001f,0f,0f},new[]{-0.001f,0f,0f},new[]{10f,4.001f,0f},new[]{10f,0f,4.001f}})
            Check(ExcaliburRules.Power(3,v[0],v[1],v[2])==0,"hard range/width/height");
        Check(ExcaliburRules.Power(1,16.001f,0,0)==0&&ExcaliburRules.Power(2,1,0,0)==0,"short wave and nondamaging blessing");
        Check(18/10f<LightningRules.JudgmentDamage(0)/8*.2f,"lake throughput below one fifth of Mjolnir judgment");
        Check(55/65f<(LightningRules.TribulationDamage(0)+4*LightningRules.AftershockDamage)/55*.2f,"ultimate core throughput below one fifth");
        Check(Math.Abs(ExcaliburRules.AttackFrequency(true)-1.265f)<.0001f&&ExcaliburRules.AttackFrequency(false)==1.1f,"multiplicative blessing and passive");
        Check(ExcaliburRules.Release==2.8f&&ExcaliburRules.Duration==4,"visual/impact time");
        var ledger=new ExcaliburRules.HitLedger<int>();
        for(int i=0;i<64;i++){Check(ledger.Add(i),"distinct target");for(int repeat=0;repeat<4;repeat++)Check(!ledger.Add(i),"duplicate frame/body/replay");}
        Check(!ledger.Add(65)&&ledger.Count==64,"hard target budget");
        Check(new ExcaliburRules.HitLedger<int>().Add(0),"new cast can hit same target");
        Check(ExcaliburRules.Newer(1,uint.MaxValue)&&!ExcaliburRules.Newer(5,5)&&!ExcaliburRules.Newer(3,4),"sequence wrap/duplicate/reorder");
        for(float t=0;t<=4;t+=.05f){float p,y,l;ExcaliburRules.Pose(t,out p,out y,out l);Check(ExcaliburRules.Finite(p)&&Math.Abs(p)<1.2f&&Math.Abs(y)<.3f&&Math.Abs(l)<.5f,"bounded raise/swing pose");}
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

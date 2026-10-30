using System;
using System.Collections.Generic;
using SCIEPort.Features.Poseidon;
using Rect=SCIEPort.Features.Gungnir.GungnirHudLayout.Rect;
internal static class Program
{
    static int count;
    static void Check(bool b,string msg){if(!b)throw new Exception(msg);count++;}
    static bool Near(float a,float b)=>Math.Abs(a-b)<.00001f;
    static void Main()
    {
        Check(PoseidonRules.Cooldown(1)==15&&PoseidonRules.Cooldown(2)==11&&PoseidonRules.Cooldown(3)==60,"base cooldowns");
        Check(PoseidonRules.Cooldown(1,true)==7&&PoseidonRules.Cooldown(2,true)==5&&PoseidonRules.Cooldown(3,true)==60,"domain cooldowns");
        Check(PoseidonRules.Damage(1,false)==14&&PoseidonRules.Damage(2,false)==44&&PoseidonRules.Damage(3,false)==55,"all three attack powers");
        for(byte skill=1;skill<=3;skill++)Check(PoseidonRules.Damage(skill,true)==2*PoseidonRules.Damage(skill,false),"water damage x2");
        Check(!PoseidonRules.Skill(0)&&!PoseidonRules.Skill(4)&&PoseidonRules.Skill(3),"skill bounds");
        Check(PoseidonRules.ShieldPoints(5,false)==5&&PoseidonRules.ShieldPoints(5,true)==10,"shield points");
        float shield=5;Check(Near(PoseidonRules.Absorb(ref shield,.2f,false),0)&&Near(shield,3),"partial shield consumption");
        Check(Near(PoseidonRules.Absorb(ref shield,.5f,false),.2f)&&Near(shield,0),"overflow residual injury");
        shield=5;Check(Near(PoseidonRules.Absorb(ref shield,.4f,true),0)&&Near(shield,3),"water shields twice the same damage");
        for(int i=0;i<50;i++){Check(Near(PoseidonRules.ShieldPoints(shield,false),3),"leave water retains charges");Check(Near(PoseidonRules.ShieldPoints(shield,true),6),"enter water no charge regen");}
        Check(PoseidonRules.Absorb(ref shield,0,true)==0&&shield==3,"zero injury");
        Check(float.IsNaN(PoseidonRules.Absorb(ref shield,float.NaN,false))&&shield==3,"nonfinite shield ignored");
        shield=PoseidonRules.ShieldMaximum;shield=PoseidonRules.ShieldMaximum;Check(shield==5,"refresh nonstacking");
        float speed=3,delta=0;
        for(int i=0;i<1000;i++){speed-=delta;delta=PoseidonRules.SpeedDelta(speed);speed+=delta;Check(Near(speed,9),"swim +200% no accumulation");}
        speed-=delta;Check(Near(speed,3),"swim restored");
        Check(PoseidonRules.SpeedDelta(0)==0&&PoseidonRules.SpeedDelta(-2f)==0&&Near(PoseidonRules.SpeedDelta(2f),4f),"water speed bonus is exactly +200%");
        Check(PoseidonRules.WaveDistance(0)==0&&PoseidonRules.WaveDistance(99)==24,"wave travel bounded");
        foreach(float dt in new[]{1f/120,1f/30,.08f,.3f,2f})
        {
            float previous=0;
            for(float t=0;t<3;t+=dt){float next=PoseidonRules.WaveDistance(t);Check(next>=previous&&next<=24,"swept wave framerate invariance");previous=next;}
        }
        Check(PoseidonRules.Alive(3,19.99f)&&!PoseidonRules.Alive(3,20),"domain exact hard lease");
        Check(PoseidonRules.DomainBlend(20)==0&&Near(PoseidonRules.DomainBlend(19),1)&&PoseidonRules.DomainBlend(0)==0,"domain fade restore");
        Check(PoseidonRules.InSweep(5,4,2,4,6,.5f)&&!PoseidonRules.InSweep(5,4.6f,2,4,6,.5f),"wave slab edges");
        Check(PoseidonRules.InVortex(14,4,18)&&PoseidonRules.InVortex(0,4,18)&&!PoseidonRules.InVortex(19,4,18)&&!PoseidonRules.InVortex(14,11,18),"vortex volume");
        // 四面八方：same distance, every bearing. The rule only reads the radius, so no direction can be favoured.
        for(int bearing=0;bearing<360;bearing+=5)
        {
            double a=bearing*Math.PI/180;float x=(float)Math.Cos(a)*12f,z=(float)Math.Sin(a)*12f;
            Check(PoseidonRules.InVortex((float)Math.Sqrt(x*x+z*z),2,18),"vortex covers bearing "+bearing);
        }
        Check(PoseidonRules.InVortex(18,2,18)&&!PoseidonRules.InVortex(18.6f,2,18),"vortex outer boundary");
        Check(PoseidonRules.DomainRadiusAt(-1)==0&&PoseidonRules.DomainRadiusAt(0)==0&&Near(PoseidonRules.DomainRadiusAt(99),PoseidonRules.DomainRadius),"surge settles at full radius");
        float previousRadius=0;
        for(float t=0;t<4;t+=1f/60) { float r=PoseidonRules.DomainRadiusAt(t);Check(r>=previousRadius&&r<=PoseidonRules.DomainRadius,"surge radius monotonic");previousRadius=r; }
        Check(PoseidonRules.Newer(0,uint.MaxValue)&&!PoseidonRules.Newer(1,1)&&!PoseidonRules.Newer(0,1),"network replay");
        Check(PoseidonHudLayout.Circle(34,34,68,68)&&!PoseidonHudLayout.Circle(0,0,68,68)&&!PoseidonHudLayout.Circle(float.NaN,34,68,68),"round real hit region");
        int placed=0;
        foreach(float w in new[]{640f,854f,1280f,1920f})foreach(float aspect in new[]{16f/9,2772f/1280})foreach(float scale in new[]{1f,1.25f,1.5f})foreach(bool left in new[]{false,true})
        {
            float width=w/scale,height=w/aspect/scale;
            var look=new Rect{X=left?8:width-120,Y=height-128,Width=112,Height=112};
            var hotbar=new Rect{X=width*.25f,Y=height-58,Width=width*.5f,Height=50};
            var obstacles=new List<Rect>{look,hotbar};PoseidonHudLayout.Placement p;
            if(!PoseidonHudLayout.Place(width,height,look,obstacles,left,out p))continue;placed++;
            for(int i=0;i<3;i++){var r=p.At(i);Check(r.X>=8&&r.Y>=8&&r.Right<=width-8&&r.Bottom<=height-8,"viewport bounds");Check(!r.Intersects(look)&&!r.Intersects(hotbar),"native controls");for(int j=0;j<i;j++)Check(!r.Intersects(p.At(j)),"skills separated");}
        }
        Check(placed>=30,"common viewports must find a Look-adjacent arc");
        var covered=new List<Rect>{new Rect{X=0,Y=0,Width=1280,Height=720}};PoseidonHudLayout.Placement ignored;
        Check(!PoseidonHudLayout.Place(1280,720,new Rect{X=1160,Y=590,Width=112,Height=112},covered,false,out ignored),"fully blocked hide safely");
        Console.WriteLine("Poseidon V2: "+count+" rule/layout checks; "+placed+" fitting viewport scenarios. Device pending.");
    }
}

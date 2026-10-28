using System;
using SCIEPort.Features.Poseidon;
using SCIEPort.Features.Gungnir;

internal static class Program
{
    private static int s_count;
    private static void Check(bool value,string message) { if(!value)throw new Exception(message);s_count++; }
    private static void Main()
    {
        Check(PoseidonRules.DashCooldown==8 && PoseidonRules.WaveCooldown==26,"cooldowns");
        Check(PoseidonRules.DashSpeed(true)>PoseidonRules.DashSpeed(false) && PoseidonRules.DashSpeed(true)<25,"native body velocity cap");
        Check(PoseidonRules.Damage(1,true)>PoseidonRules.Damage(1,false),"water dash bonus");
        Check(PoseidonRules.WaveDistance(-1)==0 && PoseidonRules.WaveDistance(0.3f)==0 && PoseidonRules.WaveDistance(99)==24,"wave windup/range");
        Check(!PoseidonRules.Finite(float.NaN) && !PoseidonRules.Finite(float.PositiveInfinity) && PoseidonRules.Finite(0),"packet finite input");
        Check(PoseidonRules.Newer(1,0) && !PoseidonRules.Newer(1,1) && !PoseidonRules.Newer(0,1) && PoseidonRules.Newer(0,uint.MaxValue),"replay/wrap");
        Check(PoseidonRules.MaxCasts==8 && PoseidonRules.MaxHits==24 && PoseidonRules.MaxCandidates==96,"hard budgets");
        Check(PoseidonRules.Alive(1,0) && !PoseidonRules.Alive(1,.55f) && !PoseidonRules.Alive(2,1.81f),"hard leases");
        Check(PoseidonRules.InSweep(5,4,2,4,6,.5f),"wave width edge");
        Check(!PoseidonRules.InSweep(5,4.6f,2,4,6,.5f) && !PoseidonRules.InSweep(5,0,4.6f,4,6,.5f),"outside wave");
        Check(!PoseidonRules.InSweep(2,0,1,4,6,.5f) && !PoseidonRules.InSweep(7,0,1,4,6,.5f),"outside swept slab");
        Check(PoseidonRules.SegmentDistanceSquared(1,1,0,0,0,0,2,0,0)==1,"dash perpendicular");
        Check(PoseidonRules.SegmentDistanceSquared(3,0,0,0,0,0,2,0,0)==1,"dash clamped endpoint");
        Check(PoseidonRules.SegmentDistanceSquared(0,1,0,0,0,0,0,0,0)==1,"zero displacement");
        // Same delta ownership formula as runtime: never compound 2.6x every update, restore exactly.
        float speed=3,delta=0;
        for(int i=0;i<1000;i++)
        {
            float next=PoseidonRules.SwimDelta(speed,delta,true);
            speed+=next-delta;delta=next;
            Check(Math.Abs(speed-7.8f)<.00001f,"passive compounded");
        }
        float restored=PoseidonRules.SwimDelta(speed,delta,false);speed+=restored-delta;Check(Math.Abs(speed-3)<.00001f,"passive restore");
        foreach(float dt in new[]{1f/120,1f/60,1f/30,.1f,.25f})
        {
            float previous=0;
            for(float age=0;age<=2.1f;age+=dt)
            {
                float next=PoseidonRules.WaveDistance(age);
                Check(next>=previous && next<=24,"frame-independent clamped wave travel");previous=next;
            }
        }
        foreach(float width in new[]{640f,854f,1280f,1920f})
        foreach(float scale in new[]{1f,1.25f,1.5f})
        foreach(bool left in new[]{false,true})
        {
            float w=width/scale,h=width*9/16/scale;
            if(w<300 || h<240)continue;
            var look=new GungnirHudLayout.Rect {X=left?8:w-120,Y=h-128,Width=112,Height=112};
            var hotbar=new GungnirHudLayout.Rect {X=w*.25f,Y=h-58,Width=w*.5f,Height=50};
            var r=GungnirHudLayout.Place(w,h,look,hotbar,left);
            Check(r.Blessing.X>=0 && r.Judgment.X>=0 && r.Blessing.Right<=w && r.Judgment.Right<=w,"badge horizontal bounds");
            Check(r.Blessing.Y>=0 && r.Judgment.Y>=0 && r.Blessing.Bottom<=h && r.Judgment.Bottom<=h,"badge vertical bounds");
            Check(!r.Blessing.Intersects(look) && !r.Judgment.Intersects(look) && !r.Blessing.Intersects(hotbar) && !r.Judgment.Intersects(hotbar),"native control overlap");
        }
        Console.WriteLine("Poseidon pure rules/layout: "+s_count+" assertions passed (not live gameplay).");
    }
}

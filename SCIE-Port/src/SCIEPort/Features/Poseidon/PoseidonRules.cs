using System;
namespace SCIEPort.Features.Poseidon
{
    /// <summary>Pure V2 tide, shield and domain rules; one health point is 0.1 native health.</summary>
    internal static class PoseidonRules
    {
        internal const float ShieldCooldown=15, WaveCooldown=11, DomainCooldown=60, DomainDuration=20;
        internal const float ShieldDuration=.7f, ShieldRadius=6, WaveWindup=.3f, WaveDuration=1.5f;
        internal const float WaveSpeed=16, WaveWidth=8, WaveHeight=4, DomainWaveDuration=2.5f;
        internal const int MaxCasts=8, MaxHits=24, MaxCandidates=96;
        internal const float SwimMultiplier=3, ShieldMaximum=5, NativeHealthPerPoint=.1f;
        internal static bool Skill(byte skill)=>skill>=1&&skill<=3;
        internal static float Cooldown(byte skill,bool domain=false)=>skill==1?(domain?7:15):skill==2?(domain?5:11):60;
        internal static float Damage(byte skill,bool water)=>(skill==1?14:skill==2?44:55)*(water?2:1);
        internal static float Duration(byte skill)=>skill==1?ShieldDuration:skill==2?WaveWindup+WaveDuration:DomainDuration;
        internal static float ShieldPoints(float charges,bool water)=>Math.Max(0,Math.Min(5,charges))*(water?2:1);
        internal static float Absorb(ref float charges,float amount,bool water)
        {
            if(!Finite(amount)||amount<=0)return amount;
            float unit=NativeHealthPerPoint*(water?2:1),used=Math.Min(Math.Max(0,charges),amount/unit);
            charges=Math.Max(0,charges-used);return Math.Max(0,amount-used*unit);
        }
        internal static bool Newer(uint a,uint b)=>unchecked((int)(a-b))>0;
        internal static bool Finite(float x)=>!float.IsNaN(x)&&!float.IsInfinity(x);
        internal static float SwimDelta(float current,float owned,bool active)=>active?Math.Max(0,current-owned)*(SwimMultiplier-1):0;
        internal static float WaveDistance(float age,bool domain=false)=>Math.Max(0,Math.Min(domain?DomainWaveDuration:WaveDuration,age-WaveWindup))*(domain?14:WaveSpeed);
        internal static bool Alive(byte skill,float age)=>age>=0&&age<(skill==1?ShieldDuration:skill==2?WaveWindup+WaveDuration:DomainDuration);
        internal static float DomainBlend(float remaining)=>remaining<=0?0:Math.Min(1,Math.Min(remaining/.8f,(DomainDuration-remaining)/.5f));
        internal static bool InSweep(float along,float across,float height,float previous,float next,float radius,bool domain=false)
            =>along>=previous-radius&&along<=next+radius&&Math.Abs(across)<=(domain?7:WaveWidth/2)+radius&&height>=-radius&&height<=(domain?8:WaveHeight)+radius;
        internal static bool InVortex(float along,float across,float height)
            =>height>=-.5f&&height<=10&&((along-14)*(along-14)+(Math.Abs(across)-4)*(Math.Abs(across)-4)<=9);
    }
}

using System;
namespace SCIEPort.Features.Poseidon
{
    /// <summary>Pure V2 tide, shield and domain rules; one health point is 0.1 native health.</summary>
    internal static class PoseidonRules
    {
        internal const float ShieldCooldown=15, WaveCooldown=11, DomainCooldown=60, DomainDuration=20;
        internal const float ShieldDuration=.7f, ShieldRadius=6, WaveWindup=.3f, WaveDuration=1.5f;
        internal const float WaveSpeed=16, WaveWidth=8, WaveHeight=4;
        // The domain is omnidirectional: one full-circle surge, then a standing whirlpool field of the same radius.
        internal const float DomainRadius=18, DomainHeight=10, DomainSurgeDuration=2.2f, DomainSpoutRadius=8;
        internal const int MaxCasts=8, MaxHits=24, MaxCandidates=96;
        internal const float WaterSpeedMultiplier=3, WaterSpeedBonus=WaterSpeedMultiplier-1, SwimImmersion=.5f;
        internal const float ShieldMaximum=5, NativeHealthPerPoint=.1f;
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
        // Owned increment only: callers subtract it before recomputing, so repeated frames never accumulate.
        internal static float SpeedDelta(float baseSpeed)=>Math.Max(0,baseSpeed)*WaterSpeedBonus;
        internal static float WaveDistance(float age)=>Math.Max(0,Math.Min(WaveDuration,age-WaveWindup))*WaveSpeed;
        // Zero at the caster, full DomainRadius once the surge front has travelled outwards.
        internal static float DomainSurge(float age)=>Saturate((age-WaveWindup)/DomainSurgeDuration);
        internal static float DomainRadiusAt(float age)=>DomainRadius*(float)Math.Sqrt(DomainSurge(age));
        internal static bool Alive(byte skill,float age)=>age>=0&&age<(skill==1?ShieldDuration:skill==2?WaveWindup+WaveDuration:DomainDuration);
        internal static float DomainBlend(float remaining)=>remaining<=0?0:Math.Min(1,Math.Min(remaining/.8f,(DomainDuration-remaining)/.5f));
        internal static bool InSweep(float along,float across,float height,float previous,float next,float radius)
            =>along>=previous-radius&&along<=next+radius&&Math.Abs(across)<=WaveWidth/2+radius&&height>=-radius&&height<=WaveHeight+radius;
        // Circumference test: every bearing at the same distance is inside the whirlpool.
        internal static bool InVortex(float radial,float height,float radius)
            =>height>=-.5f&&height<=DomainHeight&&radial>=0&&radial<=Math.Min(radius,DomainRadius);
        private static float Saturate(float x)=>x<0?0:x>1?1:x;
    }
}

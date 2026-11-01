using System;
using System.Collections.Generic;
namespace SCIEPort.Features.Excalibur
{
    // Shared by authority, presentation and offline tests. Damage is nominal AttackBody power.
    internal static class ExcaliburRules
    {
        internal const float Release=1.54f, Duration=2.2f, Range=35f, Width=8f;
        internal const float EnchantDuration=8, MeleeReach=3, AddonPower=15;
        internal const float AvalonWarmup=.6f, AvalonShield=3, AvalonDuration=4.2f, AvalonHealing=.12f;
        internal const int MaxCasts=8, MaxCandidates=256, MaxHits=64;
        internal static bool Finite(float x)=>!float.IsNaN(x)&&!float.IsInfinity(x);
        internal static bool Skill(byte s)=>s>=1&&s<=3;
        internal static bool Newer(uint a,uint b)=>a!=b&&unchecked((int)(a-b))>0;
        internal static float Cooldown(byte s)=>s==1?25:s==2?35:90;
        internal static float Length(byte s)=>s==3?AvalonDuration:s==2?Duration:.45f;
        internal static float Impact(byte s)=>s==2?Release:.25f;
        internal static float AttackFrequency(bool enchanted)=>1.1f;
        internal static float ReleaseTimeline(float age)=>age*(4f/Duration);
        internal static bool Sanctuary(float age)=>Finite(age)&&age>=AvalonWarmup&&age<AvalonWarmup+AvalonShield;
        internal static float HealProgress(float age)=>Finite(age)?AvalonHealing*Math.Max(0,Math.Min(1,(age-AvalonWarmup)/AvalonShield)):0;
        internal static float Power(byte skill,float along,float across,float height)
        {
            if(!Finite(along)||!Finite(across)||!Finite(height)||along<0)return 0;
            if(skill!=2||along>Range||Math.Abs(across)>Width/2||Math.Abs(height)>4)return 0;
            return Math.Abs(across)<=1.5f&&Math.Abs(height)<=1.5f?55:25;
        }
        // Mark before entering native damage/hooks. Duplicate bodies, frames and exceptions cannot retry.
        internal sealed class HitLedger<T>
        {
            private readonly HashSet<T> seen=new HashSet<T>();
            internal int Count=>seen.Count;
            internal bool Add(T target)=>seen.Count<MaxHits&&seen.Add(target);
        }
        internal static void Pose(float age,out float pitch,out float yaw,out float lift)
        {
            age=ReleaseTimeline(age);
            float up=Math.Max(0,Math.Min(1,age/.65f));
            float swing=Math.Max(0,Math.Min(1,(age-2.65f)/.35f));
            pitch=-1.05f*up+2.1f*swing;yaw=-.18f+.38f*swing;lift=.40f*up-.55f*swing;
        }
    }
}

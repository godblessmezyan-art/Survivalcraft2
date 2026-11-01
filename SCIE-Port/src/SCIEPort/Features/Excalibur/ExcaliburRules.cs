using System;
using System.Collections.Generic;
namespace SCIEPort.Features.Excalibur
{
    // Shared by authority, presentation and offline tests. Damage is nominal AttackBody power.
    internal static class ExcaliburRules
    {
        internal const float Release=1.75f, Duration=2.45f, Range=35f, Width=8f;
        internal const float SwingStart=1.05f, BeamSeconds=6f, BeamFade=2f, ImpactPower=90f, TickPower=45f;
        internal const float BeamEnd=Release+BeamSeconds;
        internal const float EnchantDuration=8, MeleeReach=3, AddonPower=15;
        internal const float AvalonWarmup=.6f, AvalonShield=3, AvalonDuration=4.2f, AvalonHealing=.20f;
        internal const float RegenSeconds=75f, RegenPerSecond=.1f;
        internal const int MaxCasts=8, MaxCandidates=256, MaxHits=64;
        internal static bool Finite(float x)=>!float.IsNaN(x)&&!float.IsInfinity(x);
        internal static bool Skill(byte s)=>s>=1&&s<=3;
        internal static bool Newer(uint a,uint b)=>a!=b&&unchecked((int)(a-b))>0;
        internal static float Cooldown(byte s)=>s==1?15:s==2?35:75;
        internal static float Length(byte s)=>s==3?AvalonDuration:s==2?Duration:.45f;
        internal static float Impact(byte s)=>s==2?Release:.25f;
        internal static float AttackFrequency(bool enchanted)=>1.1f;
        internal static float Smooth(float t){t=Math.Max(0,Math.Min(1,t));return t*t*(3-2*t);}
        internal static float Swing(float age)=>Smooth((age-SwingStart)/(Release-SwingStart));
        internal static float BeamOpacity(float age)=>age<Release||age>=BeamEnd?0:1-Smooth((age-(BeamEnd-BeamFade))/BeamFade);
        internal static int BeamTick(float age)=>!Finite(age)||age<Release?0:Math.Min((int)BeamSeconds,(int)Math.Floor(age-Release+.00001f));
        internal static bool Sanctuary(float age)=>Finite(age)&&age>=AvalonWarmup&&age<AvalonWarmup+AvalonShield;
        internal static float Power(byte skill,float along,float across,float height)
        {
            if(!Finite(along)||!Finite(across)||!Finite(height)||along<0)return 0;
            if(skill!=2||along>Range||Math.Abs(across)>Width/2||Math.Abs(height)>4)return 0;
            return ImpactPower;
        }
        // Mark before entering native damage/hooks. Duplicate bodies, frames and exceptions cannot retry.
        internal sealed class HitLedger<T>
        {
            private readonly HashSet<T> seen=new HashSet<T>();
            internal int Count=>seen.Count;
            internal bool Add(T target)=>seen.Count<MaxHits&&seen.Add(target);
            internal void Clear()=>seen.Clear();
        }
        internal static void Pose(float age,out float pitch,out float yaw,out float lift)
        {
            float up=Smooth(age/.6f),swing=Swing(age),recover=1-Smooth((age-1.85f)/.6f);
            pitch=(-.95f*up+1.8f*swing)*recover;yaw=(-.18f*up+.36f*swing)*recover;lift=(.42f*up-.56f*swing)*recover;
        }
        // Native FPP applies ItemOffset twice and adds the CSV's -12 degree pitch.
        // Cancel that pitch while raising; drive the blade past horizontal during the cut.
        internal static void FirstPersonPose(float age,out float pitch,out float yaw,out float lift,out float forward)
        {
            float up=Smooth(age/.6f),swing=Swing(age),recover=1-Smooth((age-1.85f)/.6f);
            pitch=(.25944f*up-2.1f*swing)*recover;yaw=(-.08f*up+.14f*swing)*recover;
            lift=(.42f*up-.58f*swing)*recover;forward=.025f*up*recover;
        }
    }
}

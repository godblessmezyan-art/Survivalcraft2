using System;
namespace SCIEPort.Features.AbyssReaper
{
    internal static class VoidSpaceRules
    {
        internal const byte Cut=1,Blink=2,Shatter=3;
        internal const byte Request=0,Clock=1,Event=2,Prepare=3,Ready=4,Commit=5,Ack=6,Finish=7,Cancel=8,Reject=9,End=10;
        internal const float CutRiftPower=60,CutRiftDuration=4,BlinkRange=18,BlinkCooldown=3,BlinkPower=75,BlinkRiftRadius=2.2f,BlinkRiftDuration=3;
        internal const int MaxQueue=64,RequestsPerFrame=8,MaxEffects=16,MaxTransfers=4,MaxCasts=4,FrameChecks=128;
        internal const double FallLease=4,TransferLease=2;
        internal static bool Finite(double value) => !double.IsNaN(value)&&!double.IsInfinity(value);
        internal static float Fade(double elapsed,double duration)
        {
            if(!Finite(elapsed)||elapsed<0||elapsed>=duration)return 0;
            return (float)Math.Min(1,Math.Min(elapsed/.12,(duration-elapsed)/.45));
        }
        internal static bool Live(double elapsed,double duration) => Finite(elapsed)&&elapsed>=0&&elapsed<duration;
        internal static bool IsFall(bool active,bool attacker,bool ignored,bool nativeCause) => active&&!attacker&&!ignored&&nativeCause;
        internal static double Cooldown(byte skill) => skill==Blink?BlinkCooldown:skill==Shatter?VoidShatterRules.Cooldown:0;
    }
    internal static class VoidShatterRules
    {
        internal const float Range=15,Radius=10,Power=160;
        internal const double Cooldown=36,Duration=1.8,Impact=1.35;
        internal static int Fragments(int quality) => VoidSkillRules.VfxSegments(quality,12,20,28);
        internal static float Break(double t) => VoidHorizonVisualRules.Smooth((float)((t-.3)/.9));
        internal static float Collapse(double t) => VoidHorizonVisualRules.Smooth((float)((t-1.2)/.2));
        internal static float Blast(double t) => VoidHorizonVisualRules.Smooth((float)((t-1.4)/.4));
    }
}

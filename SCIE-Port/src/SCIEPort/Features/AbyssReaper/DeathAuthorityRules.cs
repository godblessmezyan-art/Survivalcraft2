using System;
namespace SCIEPort.Features.AbyssReaper
{
    internal static class DeathAuthorityRules
    {
        internal const int OwnerLimit=6,WorldLimit=24,QueueLimit=64,FrameRequests=8,FrameCandidates=128;
        internal const double EmpowerDuration=12,EmpowerCooldown=3,WaveCooldown=36,WaveDuration=1.6,WaveImpact=1.25,SummonCooldown=30,ToggleCooldown=1;
        internal const float WaveRadius=24,WaveHeight=16,GuardRange=24,Leash=48;
        internal const byte Toggle=1,Empower=2,Wave=3,Summon=4,Strike=5,Sync=6,Dismiss=7,Recall=8;
        internal static bool Finite(double v)=>!double.IsNaN(v)&&!double.IsInfinity(v);
        internal static float LastLife(float resilience)=>1f/Math.Max(1,Math.Min(1000000,WorldCutRules.Finite(resilience)?resilience:100));
        internal static bool Owned(Guid owner,Guid actor)=>owner!=Guid.Empty&&owner==actor;
    }
}

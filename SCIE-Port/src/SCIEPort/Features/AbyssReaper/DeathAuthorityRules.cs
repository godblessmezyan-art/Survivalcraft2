using System;
namespace SCIEPort.Features.AbyssReaper
{
    internal static class DeathAuthorityRules
    {
        internal const int OwnerLimit=7,WorldLimit=24,QueueLimit=64,FrameRequests=8,FrameCandidates=128,DiscardLimit=128;
        internal const double EmpowerDuration=12,EmpowerCooldown=3,WaveCooldown=36,WaveDuration=1.6,WaveImpact=1.25,SummonCooldown=30,ToggleCooldown=1;
        internal const float WaveRadius=24,WaveHeight=16,GuardRange=24,Leash=48;
        internal const byte Toggle=1,Empower=2,Wave=3,Summon=4,Strike=5,Sync=6,Dismiss=7,Recall=8,Stow=9,Manifest=10,RidingMode=11,RiderInput=12,ManifestOne=13,StowOne=14;
        internal const float WaveHeal=97;internal const double StoredHealInterval=2;
        internal const float PursuitRange=64,FollowStart=9,FollowStop=5.5f,CerberusJumpSpeed=10,CerberusJumpHeight=4;
        internal const double PursuitLease=20,RiderPriority=.85;
        internal const string HoundTemplate="Wolf_Gray",MountTemplate="Horse_Black_Saddled";
        internal static bool Finite(double v)=>!double.IsNaN(v)&&!double.IsInfinity(v);
        internal static float LastLife(float resilience)=>1f/Math.Max(1,WorldCutRules.Finite(resilience)?resilience:100);
        internal static bool Owned(Guid owner,Guid actor)=>owner!=Guid.Empty&&owner==actor;
    }
}

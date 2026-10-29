using System;
using System.Collections.Generic;
namespace SCIEPort.Features.AbyssReaper
{
    internal enum VoidSkill { Portal=1, WorldCut=2, Blink=3, Horizon=4 }
    internal enum HorizonPhase { Awakening, SkyRift, Descent, Domain, Collapse, Finished }
    // Pure rules: never touch player, terrain, graphics or network state.
    internal static class VoidSkillRules
    {
        internal const float BlinkRange=18f, BlinkRadius=4f, BlinkPower=70f;
        internal const float HorizonRadius=40f, HorizonTickPower=12f, HorizonFinalPower=180f;
        internal const double BlinkCooldown=15, HorizonCooldown=180, HorizonDuration=17;
        internal static bool InBlinkRange(float distance) => WorldCutRules.Finite(distance) && distance>=0 && distance<=BlinkRange;
        internal static bool InDomain(float x,float y,float z,float maxHeight)
            => WorldCutRules.Finite(x)&&WorldCutRules.Finite(y)&&WorldCutRules.Finite(z)&&WorldCutRules.Finite(maxHeight)
                && maxHeight>=0 && Math.Abs(y)<=maxHeight && (double)x*x+(double)z*z<=HorizonRadius*HorizonRadius;
        internal static HorizonPhase Phase(double elapsed)
        {
            if(double.IsNaN(elapsed)||elapsed<0||elapsed>=HorizonDuration)return HorizonPhase.Finished;
            if(elapsed<2)return HorizonPhase.Awakening;if(elapsed<5)return HorizonPhase.SkyRift;
            if(elapsed<8)return HorizonPhase.Descent;if(elapsed<14)return HorizonPhase.Domain;
            return HorizonPhase.Collapse;
        }
    }
    // A one-shot settlement ledger independent of presentation and frame frequency.
    internal sealed class HorizonTimeline<T>
    {
        private readonly HashSet<T> finalHits=new HashSet<T>();
        private int tick=-1;
        private bool cancelled,final;
        internal readonly double Started;
        internal HorizonTimeline(double now) { Started=now; }
        internal int TakeTick(double now)
        {
            if(cancelled || final || double.IsNaN(now) || double.IsInfinity(now))return -1;
            double elapsed=now-Started;if(elapsed<8 || elapsed>=14)return -1;
            int next=(int)Math.Floor(elapsed-8);if(next<=tick)return -1;
            tick=next;return tick;
        }
        internal bool TakeFinal(double now)
        {
            if(cancelled||final||double.IsNaN(now)||double.IsInfinity(now)||now-Started<16)return false;
            final=true;return true;
        }
        internal bool AddFinalTarget(T target,int max)
            => !cancelled && final && max>0 && finalHits.Count<max && finalHits.Add(target);
        internal void Cancel() { cancelled=true;finalHits.Clear(); }
    }
}

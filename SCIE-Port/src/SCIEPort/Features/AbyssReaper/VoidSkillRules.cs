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
        internal const float HorizonRadius=128f, HorizonHeight=256f;
        internal const double BlinkCooldown=15, HorizonCooldown=180, HorizonDuration=4.2;
        internal const double DomainStart=.5, ExpansionStart=1.3, FinalImpact=HorizonDuration;
        internal static int Quality(int value) => Math.Max(0,Math.Min(2,value));
        internal static int VfxSegments(int quality,int low,int medium,int high) => Quality(quality)==0?low:Quality(quality)==1?medium:high;
        internal static bool InBlinkRange(float distance) => WorldCutRules.Finite(distance) && distance>=0 && distance<=BlinkRange;
        internal static bool InDomain(float x,float y,float z,float maxHeight)
            => WorldCutRules.Finite(x)&&WorldCutRules.Finite(y)&&WorldCutRules.Finite(z)&&WorldCutRules.Finite(maxHeight)
                && maxHeight>=0 && Math.Abs(y)<=maxHeight && (double)x*x+(double)z*z<=HorizonRadius*HorizonRadius;
        internal static HorizonPhase Phase(double elapsed)
        {
            if(double.IsNaN(elapsed)||elapsed<0||elapsed>=HorizonDuration)return HorizonPhase.Finished;
            if(elapsed<DomainStart)return HorizonPhase.Awakening;
            return elapsed<ExpansionStart?HorizonPhase.Descent:HorizonPhase.Domain;
        }
    }
    // Visual envelopes are pure and cannot advance gameplay timers.
    internal static class VoidHorizonVisualRules
    {
        internal static float Smooth(float value) { value=Math.Max(0,Math.Min(1,value));return value*value*(3-2*value); }
        // Background/hole 53 + expanding membrane/halo 18 = 71 triangles per segment, capped at 72.
        internal static int PrimitiveBudget(int quality) => VoidSkillRules.VfxSegments(quality,3456,5184,6912);
        internal static float Entry(double t) => Smooth((float)(t/VoidSkillRules.DomainStart));
        internal static float Hole(double t) => Smooth((float)((t-VoidSkillRules.DomainStart)/(VoidSkillRules.ExpansionStart-VoidSkillRules.DomainStart)));
        internal static float Expansion(double t) => Math.Max(0,Math.Min(1,(float)((t-VoidSkillRules.ExpansionStart)/(VoidSkillRules.FinalImpact-VoidSkillRules.ExpansionStart))));
        // Gentle growth followed by a rapid engulfing front; no full-screen hold or exit tail.
        internal static float ExpansionAngle(double t)
        {
            float q=Expansion(t);return .30f+.24f*q+((float)Math.PI-.54f)*(float)Math.Pow(q,8);
        }

    }
    // At most one cue per stage, with no stale cue burst after a frame hitch.
    internal sealed class HorizonCueTimeline
    {
        private int last=-1;
        private double lastTime=-1;
        internal int Take(double elapsed)
        {
            if(double.IsNaN(elapsed)||double.IsInfinity(elapsed)||elapsed<0||elapsed>=VoidSkillRules.HorizonDuration)return -1;
            if(elapsed<lastTime)return -1;
            lastTime=elapsed;
            int cue=elapsed<.25?0:elapsed>=VoidSkillRules.DomainStart && elapsed<VoidSkillRules.DomainStart+.25?1:elapsed>=VoidSkillRules.ExpansionStart && elapsed<VoidSkillRules.ExpansionStart+.25?2:elapsed>=VoidSkillRules.FinalImpact-.25?3:-1;
            if(cue<0 || cue<=last)return -1;
            last=cue;return cue;
        }
    }
    // Presentation clock; never controls teleport, target selection or damage.
    internal sealed class BlinkActionClock
    {
        internal const double Duration=.42;
        private readonly double start;
        private bool cancelled;
        internal BlinkActionClock(double time) { start=time; }
        internal double Progress(double now)
        {
            if(cancelled || double.IsNaN(now) || double.IsInfinity(now) || now<start || now-start>=Duration)return -1;
            return (now-start)/Duration;
        }
        internal void Cancel() { cancelled=true; }
    }
    internal sealed class VoidCooldowns
    {
        private const int MaxPlayers=64;
        private readonly Dictionary<Guid,double> until=new Dictionary<Guid,double>();
        private readonly List<Guid> expired=new List<Guid>();
        internal double Remaining(Guid player,double now)
        {
            double deadline;
            if(double.IsNaN(now)||double.IsInfinity(now))return double.PositiveInfinity;
            return until.TryGetValue(player,out deadline)?Math.Max(0,deadline-now):0;
        }
        internal bool Start(Guid player,double now,double duration)
        {
            if(player==Guid.Empty || double.IsNaN(now)||double.IsInfinity(now)||double.IsNaN(duration)||double.IsInfinity(duration)||duration<=0||Remaining(player,now)>0)return false;
            if(until.Count>=MaxPlayers)
            {
                expired.Clear();foreach(var pair in until)if(pair.Value<=now)expired.Add(pair.Key);
                foreach(var id in expired)until.Remove(id);
                if(until.Count>=MaxPlayers)return false;
            }
            until[player]=now+duration;return true;
        }
        internal void Clear(){until.Clear();expired.Clear();}
    }
    // A one-shot settlement ledger independent of presentation and frame frequency.
    internal sealed class HorizonTimeline<T>
    {
        private readonly HashSet<T> finalHits=new HashSet<T>();
        private bool cancelled,final;
        internal readonly double Started;
        internal HorizonTimeline(double now) { Started=now; }
        internal bool TakeFinal(double now)
        {
            if(cancelled||final||double.IsNaN(now)||double.IsInfinity(now)||now<Started+VoidSkillRules.FinalImpact)return false;
            final=true;return true;
        }
        internal bool AddFinalTarget(T target,int max)
            => !cancelled && final && max>0 && finalHits.Count<max && finalHits.Add(target);
        internal void Cancel() { cancelled=true;finalHits.Clear(); }
    }
}

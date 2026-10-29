using System;
using System.Collections.Generic;

namespace SCIEPort.Features.AbyssReaper
{
    internal enum WorldCutPhase { Windup, Flying, Closing, Finished }
    internal enum WorldCutFailure : byte { None, Cooldown, Busy, Unavailable, Protected, Unloaded, Capacity, Timeout, Invalid, Wall }

    // Independent of Engine, widgets, particles and network transport.
    internal static class WorldCutRules
    {
        internal const float Cooldown = 10f, Range = 25f, HalfWidth = 3f, Speed = 25f;
        internal const float Windup = .38f, SweepEnd = .72f, CloseImpact = .22f, CloseEnd = .42f;
        internal const float Step = .5f, MaxAdvance = 2f, Lifetime = 4f;
        internal const int MaxCasts = 2, MaxQueue = 32, RequestsPerFrame = 4, MaxPeers = 64;
        internal const int MaxBodies = 512, MaxBodyChecks = 4096, MaxRays = 256, MaxPhaseHits = 24;
        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        internal static bool Coordinates(float x,float y,float z) => Finite(x)&&Finite(y)&&Finite(z)&&Math.Abs(x)<1000000&&Math.Abs(z)<1000000&&y>=1&&y<=254;
        internal static float Damage(float value,float fallback) => Finite(value)?Math.Max(0,Math.Min(1000,value)):fallback;
        internal static bool Newer(uint value, uint previous) => value != 0 && unchecked((int)(value-previous)) > 0;
        internal static bool Authority(bool local, bool server) => local || server;
        internal static bool Dispatch(bool server,bool fromServer,bool ownsCaster,byte kind)
            => server ? ownsCaster && (kind==0||kind==4) : fromServer && (kind==1||kind==2||kind==3||kind==5);
        internal static bool Swept(float along,float side,float up,float halfAlong,float halfSide,float halfUp,float from,float to)
            => Finite(along) && Finite(side) && Finite(up) && from>=0 && to>=from && to<=Range
            && along+halfAlong>=from-.9f && along-halfAlong<=to && Math.Abs(side)<=HalfWidth+halfSide && Math.Abs(up)<=.8f+halfUp;
        internal static bool Collapse(float along,float side,float up,float halfAlong,float halfSide,float halfUp)
        {
            float x=Math.Max(0,Math.Abs(along)-halfAlong),z=Math.Max(0,Math.Abs(side)-halfSide);
            return Finite(x) && Finite(z) && Finite(up) && x*x+z*z<=HalfWidth*HalfWidth && Math.Abs(up)<=1.2f+halfUp;
        }
        // A true ribbon silhouette, not a chain of spheres. Its leading edge never exceeds Range.
        internal static void Crescent(float t,out float side,out float forward,out float thickness)
        {
            float angle=(t*2-1)*1.3f;
            side=HalfWidth*(float)(Math.Sin(angle)/Math.Sin(1.3));
            forward=.9f*((float)Math.Cos(angle)-1);
            thickness=.64f*(float)Math.Pow(Math.Max(0,Math.Sin(t*Math.PI)),.7);
        }
    }

    internal sealed class WorldCutMotion
    {
        internal WorldCutPhase Phase { get; private set; }
        internal float Distance { get; private set; }
        internal readonly double Started;
        internal double CloseStarted { get; private set; }
        private bool impact;
        internal WorldCutMotion(double now) { Started=now; Phase=WorldCutPhase.Windup; }
        internal bool BeginFlight(double now)
        {
            if(Phase!=WorldCutPhase.Windup || now<Started+WorldCutRules.Windup)return false;
            Phase=WorldCutPhase.Flying;return true;
        }
        internal float NextDistance(float dt) => Math.Min(WorldCutRules.Range,Distance+Math.Min(WorldCutRules.MaxAdvance,Math.Max(0,WorldCutRules.Finite(dt)?dt:0)*WorldCutRules.Speed));
        internal void Move(float distance) { if(Phase==WorldCutPhase.Flying)Distance=Math.Max(Distance,Math.Min(WorldCutRules.Range,distance)); }
        internal bool Close(double now,float distance)
        {
            if(Phase==WorldCutPhase.Closing || Phase==WorldCutPhase.Finished)return false;
            Distance=Math.Max(0,Math.Min(WorldCutRules.Range,distance));CloseStarted=now;Phase=WorldCutPhase.Closing;return true;
        }
        internal bool TakeCloseImpact(double now)
        {
            if(Phase!=WorldCutPhase.Closing || impact || now<CloseStarted+WorldCutRules.CloseImpact)return false;
            impact=true;return true;
        }
        internal bool Expired(double now) => Phase==WorldCutPhase.Finished || now-Started>=WorldCutRules.Lifetime
            || (Phase==WorldCutPhase.Closing && now-CloseStarted>=WorldCutRules.CloseEnd);
        internal void Cancel() { Phase=WorldCutPhase.Finished; }
    }

    internal sealed class WorldCutHitLedger<T>
    {
        private readonly HashSet<T> sweep=new HashSet<T>(),close=new HashSet<T>();
        internal bool Contains(T target,bool closing) => (closing?close:sweep).Contains(target);
        internal bool Add(T target,bool closing)
        {
            var set=closing?close:sweep;return set.Count<WorldCutRules.MaxPhaseHits && set.Add(target);
        }
        internal int Count(bool closing) => (closing?close:sweep).Count;
        internal void Clear() { sweep.Clear();close.Clear(); }
    }

    // No player/widget references: cooldown persists when the weapon panel is detached.
    internal class WorldCutSession
    {
        internal uint Sent=0,Processed=0,Visual=0,Cancelled=0,Closed=0;
        internal double ReadyAt,PendingUntil;
        internal double ActiveUntil=0;
        private ushort entity;
        private Guid connection;
        private bool bound;
        internal void Bind(ushort entityId,Guid token)
        {
            if(!bound || entity!=entityId || connection!=token)
            { entity=entityId;connection=token;bound=true;Processed=0;Visual=0;Cancelled=0;Closed=0; }
        }
        internal bool MatchesEntity(ushort entityId) => bound && entity==entityId;
        internal void BindEntity(ushort entityId) { Bind(entityId,connection); }
        internal bool Queue(double now)
        {
            if(now<ReadyAt || PendingUntil>0)return false;
            if(unchecked(++Sent)==0)++Sent;PendingUntil=now+2;return true;
        }
        internal bool NewRequest(uint sequence)
        {
            if(!WorldCutRules.Newer(sequence,Processed))return false;Processed=sequence;return true;
        }
        internal bool Accept(double now)
        {
            if(now<ReadyAt)return false;ReadyAt=now+WorldCutRules.Cooldown;PendingUntil=0;ActiveUntil=now+WorldCutRules.Windup+WorldCutRules.Range/WorldCutRules.Speed+WorldCutRules.CloseEnd;return true;
        }
        internal bool TimedOut(double now) => PendingUntil>0 && PendingUntil<=now;
    }

    internal sealed class WorldCutInputGate
    {
        private bool normalHeld,draining;
        internal bool CanRequest(bool ray,bool meleeBusy,bool skillBusy) => !normalHeld && !ray && !meleeBusy && !skillBusy && !draining;
        internal void Accepted() { draining=true; }
        internal bool RouteNormal(bool ray,bool skillBusy)
        {
            if(skillBusy || draining) { if(!skillBusy && !ray)draining=false;normalHeld=false;return false; }
            normalHeld=ray;return true;
        }
        internal void Reset() { normalHeld=false;draining=false; }
    }
}

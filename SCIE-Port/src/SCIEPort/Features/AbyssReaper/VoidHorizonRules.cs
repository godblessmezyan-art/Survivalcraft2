using System;
namespace SCIEPort.Features.AbyssReaper
{
    internal static class VoidHorizonRules
    {
        // After terrain 0/100, models 1/99/201, particles 300 and overlays 1101;
        // x25.01.17 retains their depth attachment throughout this viewport pass.
        internal const int DrawOrder=2147483631, MaxCasts=4, MaxPeers=64, MaxQueue=32, RequestsPerFrame=8;
        internal const int SettlementBudget=128;
        internal static float ExecutionAmount(float health) => !WorldCutRules.Finite(health)||health<=0?0:(float)Math.Min(float.MaxValue,(double)health+Math.Max(1,health));
        internal const byte Request=0, Snapshot=1, Reject=2, Cancel=3, End=4;
        internal const double SnapshotInterval=.5, Lease=2;
        internal static bool Live(double elapsed) => !double.IsNaN(elapsed) && elapsed>=0 && elapsed<VoidSkillRules.HorizonDuration;
        internal static bool Dispatch(bool server,bool fromServer,bool ownsCaster,byte kind)
            => server ? ownsCaster && (kind==Request || kind==Cancel) : fromServer && (kind==Snapshot || kind==Reject || kind==End);
        // Friendly-fire settings never opt players into this area attack (including its pull).
        // The ultimate deliberately ignores the old hostile/passive restriction: all NPCs qualify.
        internal static bool TargetAllowed(bool player,bool hostile,bool allowPassive) => !player;
        internal static bool ValidSnapshot(float elapsed,float cooldown)
            => WorldCutRules.Finite(elapsed) && elapsed>=0 && elapsed<=VoidSkillRules.HorizonDuration && WorldCutRules.Finite(cooldown) && cooldown>=0 && cooldown<=180.1f;
    }
    // Binds replay protection to an entity AND connection, but keeps the player's cooldown.
    internal sealed class HorizonSession
    {
        internal uint Sent,Processed,Received,Cancelled;
        internal double ReadyAt,PendingUntil,LastAttempt=-100,Seen=0;
        internal string Feedback=null;
        internal double FeedbackUntil=0;
        private ushort entity;
        private Guid connection;
        internal void Bind(ushort id,Guid token)
        {
            if(entity==id && connection==token)return;
            entity=id;connection=token;Processed=Received=Cancelled=0;PendingUntil=0;
        }
        internal bool Process(uint sequence)
        {
            if(!WorldCutRules.Newer(sequence,Processed))return false;
            Processed=sequence;return true;
        }
        internal uint Request(double now)
        {
            if(!WorldCutRules.Finite((float)now) || now<ReadyAt || PendingUntil>now || now-LastAttempt<.25)return 0;
            LastAttempt=now;PendingUntil=now+VoidHorizonRules.Lease;
            if(unchecked(++Sent)==0)++Sent;
            return Sent;
        }
        internal bool Receive(uint sequence,uint revision,byte kind)
        {
            if(sequence==0 || sequence!=Sent || !WorldCutRules.Newer(revision,Received))return false;
            if(kind==VoidHorizonRules.Snapshot && sequence==Cancelled)return false;
            Received=revision;PendingUntil=0;
            if(kind==VoidHorizonRules.End || kind==VoidHorizonRules.Reject)Cancelled=sequence;
            return true;
        }
    }
}

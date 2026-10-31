using System;
using System.Collections.Generic;
using Engine;
using Game;
using Game.NetWork;
using Game.NetWork.Packages;
using TemplatesDatabase;
namespace SCIEPort.Features.AbyssReaper
{
    internal sealed partial class DeathAuthorityRuntime
    {
        private static readonly List<DeathAuthorityRuntime> recoveryWorlds=new List<DeathAuthorityRuntime>();
        // The verified target invokes SubsystemUpdate hooks AFTER component updates.
        // No native method patch, global player setting or replacement health component.
        internal static void AfterNativeUpdates()
        {
            foreach(var world in recoveryWorlds)if(!world.disposed&&world.Authority)world.LimitWorldRecovery();
        }
        private void LimitWorldRecovery()
        {
            float naturalAllowance=terrain.Project.FindSubsystem<SubsystemGameInfo>(true).TotalElapsedGameTimeDelta/900f+.00001f;
            foreach(var guard in guards.Values)
            {
                var c=guard.Creature;if(!c.IsAddedToProject)continue;var health=c.ComponentHealth;
                if(health.Health>guard.WorldHealthCeiling)
                {
                    float removed=health.Health-guard.WorldHealthCeiling;health.Health=guard.WorldHealthCeiling;
                    health.m_lastHealth=health.Health;health.HealthChange=Math.Min(0,health.HealthChange);
                    // Native clients do not regenerate NPCs. Ordinary natural-heal undo
                    // needs no extra packet every frame; larger external heals are corrected.
                    if(removed>naturalAllowance)CommonLib.Net?.QueuePackage(new ComponentHealthPackage(health));
                }
                else guard.WorldHealthCeiling=health.Health;
            }
        }
        private void SetGuardHealth(ComponentCreature c,float fraction)
        {
            if(c==null||!c.IsAddedToProject||c.ComponentHealth.Health<=0)return;
            c.ComponentHealth.Health=MathUtils.Saturate(fraction);DeathGuard guard;
            if(guards.TryGetValue(c.Entity.EntityId,out guard))guard.WorldHealthCeiling=c.ComponentHealth.Health;
            CommonLib.Net?.QueuePackage(new ComponentHealthPackage(c.ComponentHealth));
        }
        private void HealPoints(ComponentCreature c,float points)
        {
            if(c==null||c.ComponentHealth.Health<=0||points<=0)return;
            float maximum=c.ComponentHealth.AttackResilience;if(!WorldCutRules.Finite(maximum)||maximum<=0)return;
            SetGuardHealth(c,Math.Min(1,c.ComponentHealth.Health+points/maximum));
        }
        private bool StoredHeal(Friend row,float points)
        {
            if(points<=0)return false;
            if(row.HealthKnown)
            {
                float old=row.HealthFraction;if(old>0)row.HealthFraction=Math.Min(1,old+points/row.HealthMaximum);
                return row.HealthFraction!=old;
            }
            float pending=row.PendingHeal;row.PendingHeal=Math.Min(1000000000,pending+points);return row.PendingHeal!=pending;
        }
        private void UpdateStoredRecovery()
        {
            foreach(var row in hidden.Values)
            {
                double delta=Math.Max(0,Now-row.RegenStamp);row.RegenStamp=Now;
                row.RegenCarry+=delta;if(row.RegenCarry<DeathAuthorityRules.StoredHealInterval)continue;
                double ticks=Math.Floor(row.RegenCarry/DeathAuthorityRules.StoredHealInterval);row.RegenCarry-=ticks*DeathAuthorityRules.StoredHealInterval;
                if(StoredHeal(row,(float)Math.Min(1000000000,ticks)))FriendPacket(row);
            }
        }
        private void ReadRecovery(Friend row,ValuesDictionary data)
        {
            row.RegenStamp=Now;
            try{row.RegenCarry=data.GetValue<double>("RegenCarry",0);if(!DeathAuthorityRules.Finite(row.RegenCarry)||row.RegenCarry<0||row.RegenCarry>=2)row.RegenCarry=0;}catch(Exception){row.RegenCarry=0;}
            try{row.PendingHeal=data.GetValue<float>("PendingHeal",0);if(!WorldCutRules.Finite(row.PendingHeal)||row.PendingHeal<0||row.PendingHeal>1000000000)row.PendingHeal=0;}catch(Exception){row.PendingHeal=0;}
            if(row.HealOnReturn)
            {
                // Old full-heal flags become one 97-point heal under the new rule.
                if(!row.Hidden)row.HealthKnown=false;
                row.HealOnReturn=false;
                if(row.Hidden)StoredHeal(row,DeathAuthorityRules.WaveHeal);
                else row.PendingHeal=Math.Min(1000000000,row.PendingHeal+DeathAuthorityRules.WaveHeal);
            }
            if(row.Hidden&&row.HealthKnown&&row.PendingHeal>0)
            {float points=row.PendingHeal;row.PendingHeal=0;StoredHeal(row,points);}
        }
    }
}

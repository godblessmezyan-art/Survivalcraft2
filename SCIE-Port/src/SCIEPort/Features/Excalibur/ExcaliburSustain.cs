using System;
using Game;
using Game.NetWork;
using Game.NetWork.Packages;
using Engine;
namespace SCIEPort.Features.Excalibur
{
    public sealed partial class SubsystemExcalibur
    {
        private void AdvanceBeam(State s)
        {
            var beam=s.Beam;if(beam==null)return;
            float age=(float)(Now-beam.Start);
            if(!Actor(s.Player)||!Fresh(s)||age<0||age>ExcaliburRules.BeamEnd+.25f){s.Beam=null;s.NextSnapshot=0;return;}
            int tick=ExcaliburRules.BeamTick(age);
            if(Authority&&tick>beam.LastTick)
            {
                // Consume before native hooks; no retries or a catch-up burst after a stalled frame.
                beam.LastTick=tick;
                try{StrikePhase(beam,tick,ExcaliburRules.TickPower);}
                catch(Exception error){s.Beam=null;s.NextSnapshot=0;Log.Warning("[SCIEPort:Excalibur] Beam stopped: "+error);return;}
            }
            if(age>=ExcaliburRules.BeamEnd){s.Beam=null;s.NextSnapshot=0;}
        }
        private void StartAvalon(State s)
        {
            if(!Authority)return;
            s.RegenUntil=s.Ready[2];s.NextRegen=Now+1;
            RestoreHealth(s.Player,MaximumHealth(s.Player)*ExcaliburRules.AvalonHealing);
        }
        private void RestoreHealth(ComponentPlayer p,float amount)
        {
            if(!Authority||!p.IsAddedToProject||p.ComponentHealth.Health<=0||!ExcaliburRules.Finite(amount)||amount<=0)return;
            var health=p.ComponentHealth;float before=health.Health,max=MaximumHealth(p);
            if(before>=max)return;
            health.Health=Math.Min(max,before+amount);
            if(CommonLib.WorkType==WorkType.Server&&health.Health>before)CommonLib.Net.QueuePackage(new ComponentHealthPackage(health));
        }
        private void TickRegeneration(State s)
        {
            if(!Authority||s.RegenUntil<=0)return;
            var p=s.Player;
            if(!p.IsAddedToProject||p.ComponentHealth.Health<=0){s.RegenUntil=0;s.NextRegen=0;s.NextSnapshot=0;return;}
            double end=Math.Min(Now,s.RegenUntil);
            if(s.NextRegen>0&&end>=s.NextRegen)
            {
                int pulses=Math.Min(75,(int)Math.Floor(end-s.NextRegen+.000001)+1);
                s.NextRegen+=pulses;
                // Full-health pulses are consumed too; there is no bank of unused healing.
                RestoreHealth(p,pulses*ExcaliburRules.RegenPerSecond);
            }
            if(Now>=s.RegenUntil){s.RegenUntil=0;s.NextRegen=0;s.NextSnapshot=0;}
        }
    }
}

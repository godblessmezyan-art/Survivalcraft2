using Game;
namespace SCIEPort.Features.AbyssReaper
{
    // Contract-local values. Native wolves/horses and ordinary converted animals stay unchanged.
    internal static class DeathSummonStats
    {
        internal const float HoundHealth=37,HoundDamage=17,HoundSpeed=5,HorseHealth=44,HorseSpeed=10,CerberusHealth=444,CerberusSpeed=7;
        internal static bool Profile(bool special,string template,out float health,out float speed)
        {
            health=speed=0;
            if(template==BoneDragonRules.Template){health=BoneDragonRules.Health;speed=BoneDragonRules.GroundSpeed;return true;}
            if(template==CerberusRules.Template){health=CerberusHealth;speed=CerberusSpeed;return true;}
            if(!special)return false;
            if(template==DeathAuthorityRules.HoundTemplate){health=HoundHealth;speed=HoundSpeed;return true;}
            if(template==DeathAuthorityRules.MountTemplate){health=HorseHealth;speed=HorseSpeed;return true;}
            return false;
        }
        // Compute our own deltas once after native load randomization. Repeated updates
        // never multiply speed or erase additive bonuses owned by other features.
        internal sealed class Bonus
        {
            private ComponentLocomotion locomotion;private ComponentHealth health;
            private float speedDelta,healthDelta,flyDelta;
            internal void Apply(ComponentCreature creature,bool special,string template)
            {
                Release();float resilience,speed;if(!Profile(special,template,out resilience,out speed))return;
                locomotion=creature.ComponentLocomotion;health=creature.ComponentHealth;
                speedDelta=speed-locomotion.WalkSpeed;healthDelta=resilience-health.AttackResilience;
                locomotion.WalkSpeed+=speedDelta;health.AttackResilience+=healthDelta;
                if(template==BoneDragonRules.Template){flyDelta=BoneDragonRules.FlightSpeed-locomotion.FlySpeed;locomotion.FlySpeed+=flyDelta;}
                // Health is a fraction; changing resilience preserves the current health percentage.
            }
            internal void Release()
            {
                if(locomotion!=null){locomotion.WalkSpeed-=speedDelta;locomotion.FlySpeed-=flyDelta;}
                if(health!=null)health.AttackResilience-=healthDelta;
                locomotion=null;health=null;speedDelta=healthDelta=flyDelta=0;
            }
        }
    }
}

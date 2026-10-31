using System;
using System.Globalization;
using Game;
using GameEntitySystem;
using TemplatesDatabase;
namespace SCIEPort.Features.AbyssReaper
{
    internal sealed partial class DeathAuthorityRuntime
    {
        private double nextArmyHealth;
        private static bool ValidHealth(float fraction,float maximum)
            =>WorldCutRules.Finite(fraction)&&WorldCutRules.Finite(maximum)&&fraction>=0&&fraction<=1&&maximum>0&&maximum<=1000000000;
        private static void ReadLiveHealth(Friend row,ComponentCreature creature)
        {
            row.HealthFraction=creature.ComponentHealth.Health;row.HealthMaximum=creature.ComponentHealth.AttackResilience;
            row.HealthKnown=ValidHealth(row.HealthFraction,row.HealthMaximum);
        }
        private void ReadSavedHealth(Friend row,ValuesDictionary data)
        {
            // Health metadata is optional presentation data, never a reason to lose a contract.
            try
            {
                row.HealthFraction=data.GetValue<float>("HealthFraction",-1);row.HealthMaximum=data.GetValue<float>("HealthMaximum",0);
                row.HealthKnown=ValidHealth(row.HealthFraction,row.HealthMaximum);
                if((!row.HealthKnown||row.HealOnReturn)&&row.Hidden&&row.Snapshot!=null)
                {
                    var snapshot=new EntityDataList(terrain.Project.GameDatabase,row.Snapshot,false);
                    if(snapshot.EntitiesData.Count==1&&snapshot.EntitiesData[0].ValuesDictionary.DatabaseObject.Name==row.Template)
                    {
                        var health=snapshot.EntitiesData[0].ValuesDictionary.GetValue<ValuesDictionary>("Health");
                        row.HealthFraction=health.GetValue<float>("Health");row.HealthMaximum=health.GetValue<float>("AttackResilience");
                        row.HealthKnown=ValidHealth(row.HealthFraction,row.HealthMaximum);
                    }
                }
                float maximum,speed;if(row.HealthKnown&&DeathSummonStats.Profile(row.Special,row.Template,out maximum,out speed))row.HealthMaximum=maximum;
            }
            catch(Exception){row.HealthKnown=false;}
        }
        private void UpdateArmyHealth()
        {
            if(Now<nextArmyHealth)return;nextArmyHealth=Now+.5;
            // Existing live guards only, at most 24. No world/body scan or entity creation.
            foreach(var guard in guards.Values)
            {
                var row=Row(guard);float current=row.HealthKnown?(float)Math.Round(row.HealthFraction*row.HealthMaximum,1):-2;
                float maximum=row.HealthKnown?row.HealthMaximum:-2;
                if(current==guard.SentHealth&&maximum==guard.SentMaximum)continue;
                guard.SentHealth=current;guard.SentMaximum=maximum;FriendPacket(row);
            }
        }
        internal static string HealthText(Friend row)
        {
            if(!row.HealthKnown)return "生命 待同步";
            return (row.Pending?"上次 ":"生命 ")+HealthNumber(row.HealthFraction*row.HealthMaximum)+"/"+HealthNumber(row.HealthMaximum);
        }
        private static string HealthNumber(float number)
        {
            if(number>=10000)return (number/10000).ToString("0.#",CultureInfo.InvariantCulture)+"万";
            if(number>=1000)return (number/1000).ToString("0.#",CultureInfo.InvariantCulture)+"k";
            return number.ToString("0.#",CultureInfo.InvariantCulture);
        }
    }
}

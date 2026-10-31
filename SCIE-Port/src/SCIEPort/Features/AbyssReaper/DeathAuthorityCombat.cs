using System;
using Engine;
using Game;
using Game.NetWork;
using Game.NetWork.Packages;
namespace SCIEPort.Features.AbyssReaper
{
    internal sealed partial class DeathAuthorityRuntime
    {
        private bool SafeTarget(ComponentCreature c)
        {
            if(c==null||!c.IsAddedToProject||c.Project!=terrain.Project||c.ComponentHealth.Health<=0||c is ComponentPlayer||IsFriendly(c)||VoidSpaceDamage.IsPlayerCarrier(c.ComponentBody))return false;
            var box=c.ComponentBody.BoundingBox;var center=box.Center();if(!WorldCutRules.Coordinates(center.X,center.Y,center.Z))return false;
            for(int i=0;i<5;i++){float x=i==4?center.X:i%2==0?box.Min.X:box.Max.X,z=i==4?center.Z:i/2==0?box.Min.Z:box.Max.Z;if(!VoidPortalSafety.Ready(terrain,Terrain.ToCell(x),Terrain.ToCell(z))||!VoidPortalSafety.Unprotected(Terrain.ToCell(x),Terrain.ToCell(z)))return false;}return true;
        }
        private bool Convertible(ComponentCreature c)=>SafeTarget(c)&&!c.ComponentHealth.IsInvulnerable&&c.Entity.FindComponent<ComponentBehaviorSelector>(false)!=null&&c.Entity.FindComponent<ComponentPathfinding>(false)!=null&&c.Entity.FindComponent<ComponentMiner>(false)!=null;
        private bool Hostile(ComponentCreature c,ComponentPlayer p)
        {
            if(!SafeTarget(c))return false;
            if(c.Category==CreatureCategory.LandPredator||c.Category==CreatureCategory.WaterPredator)return true;
            var chase=c.Entity.FindComponent<ComponentChaseBehavior>(false);if(chase?.Target==p||IsFriendly(chase?.Target))return true;
            Threat t;return threats.TryGetValue(p.PlayerGuid,out t)&&t.Creature==c&&Now<t.Until;
        }
        internal bool FriendlyDamage(ComponentBody target,ComponentCreature attacker)
        {
            var victim=target?.Entity.FindComponent<ComponentCreature>(false);
            return IsFriendly(attacker)&&(victim is ComponentPlayer||IsFriendly(victim)||!SafeTarget(victim))||IsFriendly(victim)&&attacker is ComponentPlayer;
        }
        internal void NoteAttack(ComponentHealth health,ComponentCreature attacker)
        {
            if(!Authority||attacker==null||attacker is ComponentPlayer||IsFriendly(attacker))return;
            Guid id=Guid.Empty;var p=health?.Entity.FindComponent<ComponentPlayer>(false);if(p!=null)id=p.PlayerGuid;
            else{var c=health?.Entity.FindComponent<ComponentCreature>(false);DeathGuard g;if(c!=null&&guards.TryGetValue(c.Entity.EntityId,out g))id=g.Owner;}
            if(id!=Guid.Empty&&threats.Count<64)threats[id]=new Threat{Creature=attacker,Until=Now+10};
        }
        private void Strike(ComponentPlayer p,Vector3 direction)
        {
            if(!Authority)return;
            var s=Get(p);if(!s.Death||BusyWave(p)||Now<s.NextStrike||!WorldCutDamage.Finite(direction)||direction.LengthSquared()<.9f||direction.LengthSquared()>1.1f)return;
            direction=Vector3.Normalize(direction);var eyeDirection=p.ComponentCreatureModel.EyeRotation.GetForwardVector();if(Vector3.Dot(direction,eyeDirection)<-.2f)return;
            float frequency=1.1f*(p.Project.FindSubsystem<SCIEPort.Features.FourSaints.SubsystemFourSaints>(false)?.AttackFrequency(p)??1f);if(!WorldCutRules.Finite(frequency)||frequency<=0)return;
            int stage=Now-s.LastStrike>1.8?0:s.Combo;double duration=stage==0?.35:stage==1?.42:.58;s.NextStrike=Now+duration/Math.Max(.1f,Math.Min(10,frequency));s.Combo=(stage+1)%3;s.LastStrike=Now;
            Send(new DeathAuthorityPackage{Kind=5,Caster=p.Entity.EntityId,Owner=p.PlayerGuid,Sequence=s.Processed,Action=(byte)stage,Direction=direction});
            Vector3 origin=p.ComponentCreatureModel.EyePosition,flat=new Vector3(direction.X,0,direction.Z);flat=flat.LengthSquared()<.001f?Vector3.UnitZ:Vector3.Normalize(flat);
            candidates.Clear();bodies.FindBodiesInArea(origin.XZ-new Vector2(5),origin.XZ+new Vector2(5),candidates);swingHits.Clear();int checkedBodies=0;
            foreach(var body in candidates)
            {
                if(++checkedBodies>128||swingHits.Count>=24)break;var c=body?.Entity.FindComponent<ComponentCreature>(false);if(!SafeTarget(c)||swingHits.Contains(body.Entity))continue;
                Vector3 delta=body.BoundingBox.Center()-origin,horizontal=new Vector3(delta.X,0,delta.Z);float dot=horizontal.LengthSquared()<.001f?1:Vector3.Dot(Vector3.Normalize(horizontal),flat);
                if(!AbyssMeleeMotion.Sector(delta.Length(),dot,delta.Y,stage)||!VoidCombatSafety.ClearPath(terrain,origin,body.BoundingBox.Center(),5))continue;
                swingHits.Add(body.Entity);
                if(Empowered(p))
                {
                    if(!Convertible(c)){Message(p,"目标不支持灵魂转化");continue;}
                    Mark mark;if(marks.TryGetValue(c,out mark)&&mark.Owner==p.PlayerGuid&&Now<mark.Until)
                    {
                        if(Count(p.PlayerGuid)>=DeathAuthorityRules.OwnerLimit||ArmyCount>=DeathAuthorityRules.WorldLimit){Message(p,"召唤名额已满（七只），请在军势面板释放契约");continue;}
                        if(Adopt(c,p.PlayerGuid,false))
                        {
                            // Use the post-bind resilience: native Health=1 means full health,
                            // whereas one hit point is the reciprocal of AttackResilience.
                            SetGuardHealth(c,DeathAuthorityRules.LastLife(c.ComponentHealth.AttackResilience));
                            FriendPacket(Row(guards[c.Entity.EntityId]));marks.Remove(c);Message(p,"灵魂契约成立 · 保留1点生命");
                        }
                    }
                    else
                    {
                        if(marks.Count>=256&&!marks.ContainsKey(c)){Message(p,"印记容量已满，请稍候");continue;}
                        float remaining=Math.Min(c.ComponentHealth.Health,DeathAuthorityRules.LastLife(c.ComponentHealth.AttackResilience));float loss=c.ComponentHealth.Health-remaining;
                        if(loss>0)c.ComponentHealth.Injure(loss,p,false,"死亡印记");
                        if(c.ComponentHealth.Health>0&&c.ComponentHealth.Health<=remaining+.0001f)marks[c]=new Mark{Owner=p.PlayerGuid,Until=s.EmpowerUntil};
                    }
                }
                else ComponentMiner.AttackBody(body,p,body.BoundingBox.Center(),direction,stage==2?34:22,true);
            }
        }
        private void UpdateWaves()
        {
            int budget=128;
            for(int i=waves.Count-1;i>=0;i--)
            {
                var w=waves[i];if(!Actor(w.Owner)||w.Owner.PlayerGuid!=w.Guid){waves.RemoveAt(i);continue;}
                double t=Now-w.Started;
                if(!w.Impacted&&t>=DeathAuthorityRules.WaveImpact){w.Impacted=true;HealArmy(w.Guid);bodies.FindBodiesInArea(w.Center.XZ-new Vector2(24),w.Center.XZ+new Vector2(24),w.Targets);}
                if(w.Impacted)while(budget>0&&w.Cursor<w.Targets.Count)
                {
                    budget--;var b=w.Targets.Array[w.Cursor++];var c=b?.Entity.FindComponent<ComponentCreature>(false);if(!Hostile(c,w.Owner)||!w.Seen.Add(c.Entity))continue;var delta=b.BoundingBox.Center()-w.Center;
                    if(Math.Abs(delta.Y)<=16&&delta.X*delta.X+delta.Z*delta.Z<=24*24)c.ComponentHealth.Injure(VoidHorizonRules.ExecutionAmount(c.ComponentHealth.Health),null,true,"死亡领域");
                }
                if(t>=1.6&&(!w.Impacted||w.Cursor>=w.Targets.Count||t>3.6))waves.RemoveAt(i);
            }
        }
    }
}

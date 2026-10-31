using System;
using Engine;
using Game;
namespace SCIEPort.Features.AbyssReaper
{
    internal sealed partial class DeathAuthorityRuntime
    {
        private bool HasCerberus(Guid owner)
        {
            foreach(var g in guards.Values)if(g.Owner==owner&&g.Cerberus&&g.Creature.IsAddedToProject&&g.Creature.ComponentHealth.Health>0)return true;
            foreach(var f in hidden.Values)if(f.Owner==owner&&f.Template==CerberusRules.Template)return true;
            foreach(var f in saved.Values)if(!f.Discard&&f.Owner==owner&&f.Template==CerberusRules.Template)return true;
            return false;
        }
        private static Vector3 BitePoint(ComponentCreature c,ComponentCreature target)
        {
            var p=c.ComponentBody.BoundingBox.Center();var box=target.ComponentBody.BoundingBox;
            return new Vector3(MathUtils.Clamp(p.X,box.Min.X,box.Max.X),MathUtils.Clamp(p.Y,box.Min.Y,box.Max.Y),MathUtils.Clamp(p.Z,box.Min.Z,box.Max.Z));
        }
        private bool BiteReach(ComponentCreature c,ComponentCreature target)
            =>target!=null&&Vector3.DistanceSquared(c.ComponentBody.BoundingBox.Center(),BitePoint(c,target))<=CerberusRules.Range*CerberusRules.Range;
        private void UpdateCerberusAttack(DeathGuard g,ComponentPlayer owner,ref int hitBudget)
        {
            var c=g.Creature;
            if(g.BiteTarget!=null&&Now>=g.BiteImpactAt)
            {
                var target=g.BiteTarget;g.BiteTarget=null; // Consume before dispatch, including hook failures.
                if(Now-g.BiteStarted<1.25&&hitBudget>0&&Hostile(target,owner)&&BiteReach(c,target)
                    &&VoidCombatSafety.ClearPath(terrain,c.ComponentCreatureModel.EyePosition,BitePoint(c,target),6))
                {
                    hitBudget--;Vector3 delta=target.ComponentBody.BoundingBox.Center()-c.ComponentBody.BoundingBox.Center();
                    // One native armor/health dispatch. Never one call per head, never special * 2.
                    ComponentMiner.AttackBody(target.ComponentBody,c,BitePoint(c,target),delta.LengthSquared()>.001f?Vector3.Normalize(delta):c.ComponentBody.Matrix.Forward,CerberusRules.Damage,true);
                }
            }
            if(g.BiteTarget!=null||g.Target==null||!Hostile(g.Target,owner)||!BiteReach(c,g.Target))return;
            var direction=g.Target.ComponentBody.Position-c.ComponentBody.Position;
            if(direction.XZ.LengthSquared()>.001f)
            {
                float angle=Vector2.Angle(c.ComponentBody.Matrix.Forward.XZ,direction.XZ);
                c.ComponentLocomotion.TurnOrder=new Vector2(MathUtils.Clamp(angle,-1,1),0);
                if(Math.Abs(angle)>1.1f)return;
            }
            if(Now<g.NextAttack||!VoidCombatSafety.ClearPath(terrain,c.ComponentCreatureModel.EyePosition,BitePoint(c,g.Target),6))return;
            g.BiteTarget=g.Target;g.BiteStarted=Now;g.BiteImpactAt=Now+CerberusRules.ImpactDelay;g.NextAttack=Now+CerberusRules.Cooldown;
            (c.ComponentCreatureModel as ComponentCerberusModel)?.Bite(Now);
            Send(BitePacket(g));
        }
        private DeathAuthorityPackage BitePacket(DeathGuard g)=>new DeathAuthorityPackage{Kind=8,Caster=g.Creature.Entity.EntityId,Target=g.BiteTarget?.Entity.EntityId??(ushort)0,Owner=g.Owner,A=(float)Math.Max(0,Now-g.BiteStarted)};
        private void ReceiveBite(DeathAuthorityPackage q)
        {
            if(!hasClock||!WorldCutRules.Finite(q.A)||q.A<0||q.A>=CerberusRules.AnimationDuration)return;
            double age=Math.Max(0,Math.Max(lastServerTime,Now+clockOffset)-q.Stamp)+q.A;
            if(age>=CerberusRules.AnimationDuration)return;
            bool owned=false;foreach(var f in clientFriends.Values)if(f.Cerberus&&!f.Hidden&&!f.Pending&&f.EntityId==q.Caster&&f.Owner==q.Owner){owned=true;break;}if(!owned)return;
            terrain.Project.FindEntityById(q.Caster,entity=>entity.FindComponent<ComponentCerberusModel>(false)?.ReceiveBite(q.Revision,(float)age));
        }
    }
}

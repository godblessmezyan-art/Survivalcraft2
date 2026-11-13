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
        private bool TailReach(ComponentCreature c,ComponentCreature target)
        {
            if(target==null)return false;
            Vector3 delta=target.ComponentBody.BoundingBox.Center()-c.ComponentBody.BoundingBox.Center();
            return delta.X*delta.X+delta.Z*delta.Z<=CerberusRules.TailRange*CerberusRules.TailRange&&Math.Abs(delta.Y)<=2.6f;
        }
        /// <summary>Host-authoritative three-layer bite, serpent tail sweep, roar and awakening.</summary>
        private void UpdateCerberusAttack(DeathGuard g,ComponentPlayer owner,ref int hitBudget)
        {
            var c=g.Creature;double now=Now;var model=c.ComponentCreatureModel as ComponentCerberusModel;
            UpdateCerberusRage(g,model);
            // Three staggered impacts, each re-validated: the target may die or leave after the first.
            while(g.BiteLayers<3&&g.BiteStarted>=0&&now>=g.BiteStarted+CerberusRules.BiteDelays[g.BiteLayers])
            {
                g.BiteLayers++;
                var target=g.BiteTarget;
                if(target!=null&&hitBudget>0&&GuardEnemy(g,target,owner)&&BiteReach(c,target)
                    &&VoidCombatSafety.ClearPath(terrain,c.ComponentCreatureModel.EyePosition,BitePoint(c,target),6))
                {
                    hitBudget--;g.PursuitUntil=now+DeathAuthorityRules.PursuitLease;
                    Vector3 delta=target.ComponentBody.BoundingBox.Center()-c.ComponentBody.BoundingBox.Center();
                    // One native armor/health dispatch per head, never a multiplied packet.
                    ComponentMiner.AttackBody(target.ComponentBody,c,BitePoint(c,target),delta.LengthSquared()>.001f?Vector3.Normalize(delta):c.ComponentBody.Matrix.Forward,CerberusRules.Damage,true);
                }
            }
            if(g.BiteLayers>=3)g.BiteTarget=null;
            // The tail sweeps once per cast; its own impact moment is settled here.
            if(g.TailStarted>=0&&!g.TailImpacted&&now>=g.TailImpactAt)
            {
                g.TailImpacted=true;
                CerberusTailImpact(g,owner,ref hitBudget);
            }
            if(g.BiteTarget!=null||g.TailStarted>=0&&now<g.TailStarted+CerberusRules.TailDuration)return;
            if(g.Target==null||!GuardEnemy(g,g.Target,owner))return;
            var direction=g.Target.ComponentBody.Position-c.ComponentBody.Position;
            float angle=0;
            if(direction.XZ.LengthSquared()>.001f)
            {
                if(!g.Ridden||g.AutoDriving){var steed=c.Entity.FindComponent<ComponentCerberusSteed>(false);if(steed!=null){steed.FaceTarget=g.Target.ComponentBody.Position;steed.FaceUntil=now+.3;}}
                angle=Vector2.Angle(c.ComponentBody.Matrix.Forward.XZ,direction.XZ);
                if(!g.Ridden||g.AutoDriving)c.ComponentLocomotion.TurnOrder=new Vector2(MathUtils.Clamp(angle,-1,1),0);
            }
            // A serpent tail answers flankers and crowds, never a clean frontal duel.
            bool crowded=angle>1.15f&&TailReach(c,g.Target)||CerberusTailOpens(g,owner);
            if(crowded&&now>=g.NextTail)
            {
                g.TailStarted=now;g.TailImpactAt=now+CerberusRules.TailImpactDelay;g.TailImpacted=false;g.NextTail=now+CerberusRules.TailCooldown;
                g.PursuitUntil=now+DeathAuthorityRules.PursuitLease;
                model?.TailAttack(now);
                Send(CerberusPacket(g,11,now));
                return;
            }
            if(Math.Abs(angle)>1.1f)return;
            if(now<g.NextAttack||!VoidCombatSafety.ClearPath(terrain,c.ComponentCreatureModel.EyePosition,BitePoint(c,g.Target),6))return;
            g.BiteTarget=g.Target;g.BiteStarted=now;g.BiteLayers=0;g.NextAttack=now+CerberusRules.Cooldown;
            model?.Bite(now);
            Send(BitePacket(g));
            // Entering battle earns one roar; low health adds two more layers of threat.
            if(!g.RoarEntered) { g.RoarEntered=true;CerberusRoar(g,now); }
        }
        /// <summary>True when at least two hostile bodies stand inside tail reach.</summary>
        private bool CerberusTailOpens(DeathGuard g,ComponentPlayer owner)
        {
            if(Now<g.NextTail)return false;
            var c=g.Creature;Vector3 center=c.ComponentBody.BoundingBox.Center();int found=0,checkedBodies=0;
            candidates.Clear();bodies.FindBodiesInArea(center.XZ-new Vector2(CerberusRules.TailRange),center.XZ+new Vector2(CerberusRules.TailRange),candidates);
            foreach(var body in candidates)
            {
                if(++checkedBodies>48)break;
                var target=body?.Entity.FindComponent<ComponentCreature>(false);
                if(!GuardEnemy(g,target,owner)||!TailReach(c,target))continue;
                if(++found>=2)return true;
            }
            return false;
        }
        private void CerberusTailImpact(DeathGuard g,ComponentPlayer owner,ref int hitBudget)
        {
            var c=g.Creature;Vector3 center=c.ComponentBody.BoundingBox.Center();
            int hits=0,checkedBodies=0;
            candidates.Clear();bodies.FindBodiesInArea(center.XZ-new Vector2(CerberusRules.TailRange),center.XZ+new Vector2(CerberusRules.TailRange),candidates);
            foreach(var body in candidates)
            {
                if(++checkedBodies>64||hits>=6||hitBudget<=0)break;
                var target=body?.Entity.FindComponent<ComponentCreature>(false);
                if(target==null||!GuardEnemy(g,target,owner)||!TailReach(c,target))continue;
                if(!VoidCombatSafety.ClearPath(terrain,c.ComponentCreatureModel.EyePosition,body.BoundingBox.Center(),6))continue;
                hitBudget--;hits++;
                Vector3 delta=body.BoundingBox.Center()-center;
                ComponentMiner.AttackBody(body,c,body.BoundingBox.Center(),delta.LengthSquared()>.001f?Vector3.Normalize(delta):c.ComponentBody.Matrix.Forward,CerberusRules.TailDamage,true);
            }
        }
        private void CerberusRoar(DeathGuard g,double now)
        {
            if(now<g.NextRoar)return;
            g.NextRoar=now+8;g.RoarStarted=now;
            (g.Creature.ComponentCreatureModel as ComponentCerberusModel)?.Roar(now);
            Send(CerberusPacket(g,12,now));
        }
        private void UpdateCerberusRage(DeathGuard g,ComponentCerberusModel model)
        {
            var health=g.Creature.ComponentHealth;float ceiling=Math.Max(.01f,g.WorldHealthCeiling);
            bool rage=health.Health>0&&health.Health<=ceiling*CerberusRules.RageFraction;
            if(rage==g.Rage)return;
            g.Rage=rage;model?.SetRage(rage);
            Send(CerberusPacket(g,13,Now,3,rage?1f:0f));
        }
        private void BeginCerberusIntro(DeathGuard g,double now)
        {
            g.IntroStarted=now;g.IntroUntil=now+CerberusRules.IntroDuration;g.RoarEntered=false;cerberusIntros[g.Owner]=g.IntroUntil;
            g.Target=g.BiteTarget=null;g.BiteLayers=0;g.Path.Stop();g.Creature.ComponentLocomotion.WalkOrder=null;
            (g.Creature.ComponentCreatureModel as ComponentCerberusModel)?.BeginIntro(now);
            Send(CerberusPacket(g,13,now,1));
        }
        /// <summary>Owner skip. The host ends the freeze for every client in the same tick.</summary>
        private void SkipCerberusIntro(ComponentPlayer p)
        {
            double now=Now;
            foreach(var g in guards.Values)
            {
                if(g.Owner!=p.PlayerGuid||!g.Cerberus||now>=g.IntroUntil)continue;
                g.IntroUntil=now;cerberusIntros.Remove(g.Owner);
                (g.Creature.ComponentCreatureModel as ComponentCerberusModel)?.EndIntro(now);
                Send(CerberusPacket(g,13,now,2));
            }
        }
        private DeathAuthorityPackage CerberusPacket(DeathGuard g,int kind,double started,byte action=0,float state=0)
            =>new DeathAuthorityPackage{Kind=(byte)kind,Caster=g.Creature.Entity.EntityId,Owner=g.Owner,Action=action,A=kind==13&&action==3?state:(float)Math.Max(0,Now-started)};
        private DeathAuthorityPackage BitePacket(DeathGuard g)=>CerberusPacket(g,8,g.BiteStarted);
        private DeathAuthorityPackage TailPacket(DeathGuard g)=>CerberusPacket(g,11,g.TailStarted);
        private DeathAuthorityPackage RoarPacket(DeathGuard g)=>CerberusPacket(g,12,g.RoarStarted);
        private DeathAuthorityPackage IntroPacket(DeathGuard g)=>CerberusPacket(g,13,g.IntroStarted,1);
        /// <summary>Kind 8 bite, 11 tail, 12 roar, 13 awakening/rage. Presentation only.</summary>
        private void ReceiveCerberusEffect(DeathAuthorityPackage q)
        {
            if(!hasClock||!WorldCutRules.Finite(q.A)||q.A<0)return;
            int duration=q.Kind==8?(int)Math.Ceiling(CerberusRules.AnimationDuration):q.Kind==11?(int)Math.Ceiling(CerberusRules.TailDuration):q.Kind==12?(int)Math.Ceiling(CerberusRules.RoarDuration):0;
            if(q.Kind!=13&&q.A>=duration)return;
            if(q.Kind==13&&q.Action!=1&&q.Action!=2&&q.Action!=3)return;
            if(q.Kind==13&&q.Action==3&&q.A!=0&&q.A!=1)return;
            double age=Math.Max(0,Math.Max(lastServerTime,Now+clockOffset)-q.Stamp)+q.A;
            if(q.Kind!=13&&age>=duration)return;
            bool owned=false;foreach(var f in clientFriends.Values)if(f.Cerberus&&!f.Hidden&&!f.Pending&&f.EntityId==q.Caster&&f.Owner==q.Owner){owned=true;break;}
            if(!owned)return;
            if(q.Kind==13&&q.Action==1)cerberusIntros[q.Owner]=Now+Math.Max(0,CerberusRules.IntroDuration-age);
            else if(q.Kind==13&&q.Action==2)cerberusIntros.Remove(q.Owner);
            float state=q.Kind==13&&q.Action==3?q.A:(float)age;
            terrain.Project.FindEntityById(q.Caster,entity=>entity.FindComponent<ComponentCerberusModel>(false)?.ReceiveEffect(q.Kind,q.Revision,state,q.Action));
        }
        private void ReceiveBite(DeathAuthorityPackage q)=>ReceiveCerberusEffect(q);
    }
}

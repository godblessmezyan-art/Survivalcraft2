using System;
using Engine;
using Game;
namespace SCIEPort.Features.AbyssReaper
{
    internal sealed partial class DeathAuthorityRuntime
    {
        private void SetKnightCharge(DeathGuard guard,bool active)
        {
            if(!guard.Knight||guard.Charging==active)return;
            guard.Charge(active);FriendPacket(Row(guard));
        }
        private static bool KnightChargeMotion(DeathGuard guard)
        {
            if(guard.Target==null)return false;
            var body=guard.Creature.ComponentBody;Vector3 delta=guard.Target.ComponentBody.Position-body.Position;
            if(!NightmareKnightRules.ChargeDistance(delta.Length())||Math.Abs(delta.Y)>.5f||body.ImmersionFactor>.2f||!body.StandingOnValue.HasValue&&body.StandingOnBody==null)return false;
            return Vector2.Dot(body.Matrix.Forward.XZ,Vector2.Normalize(delta.XZ))>=.995f;
        }
        private bool CanKnightCharge(DeathGuard guard,ComponentPlayer owner)
        {
            if(!guard.Knight||guard.Mode==DeathGuardMode.Free||!GuardEnemy(guard,guard.Target,owner)||guard.Path.IsStuck||!KnightChargeMotion(guard))return false;
            var body=guard.Creature.ComponentBody;Vector2 toward=Vector2.Normalize((guard.Target.ComponentBody.Position-body.Position).XZ);
            // Only accelerate along an already clear, fully supported forward corridor.
            Vector3 ahead=body.Position+new Vector3(toward.X,0,toward.Y)*3;
            return SafeRoamRoute(body,ahead,.4f)&&VoidCombatSafety.ClearPath(terrain,body.Position+Vector3.UnitY,guard.Target.ComponentBody.Position+Vector3.UnitY,17);
        }
        private static bool KnightReach(DeathGuard guard,ComponentCreature target)
            =>target!=null&&Math.Abs(target.ComponentBody.Position.Y-guard.Creature.ComponentBody.Position.Y)<=1.5f&&Vector3.DistanceSquared(target.ComponentBody.Position,guard.Creature.ComponentBody.Position)<=NightmareKnightRules.Range*NightmareKnightRules.Range;
        private static bool KnightFacing(DeathGuard guard,ComponentCreature target)
        {
            Vector2 delta=(target.ComponentBody.Position-guard.Creature.ComponentBody.Position).XZ;
            return delta.LengthSquared()<.01f||Vector2.Dot(guard.Creature.ComponentBody.Matrix.Forward.XZ,Vector2.Normalize(delta))>=.35f;
        }
        private void UpdateKnightAttack(DeathGuard guard,ComponentPlayer owner,ref int budget)
        {
            if(!Authority)return;var c=guard.Creature;
            if(guard.SwordTarget!=null&&Now>=guard.SwordImpactAt)
            {
                var target=guard.SwordTarget;guard.SwordTarget=null;
                if(budget>0&&Now-guard.SwordStarted<NightmareKnightRules.SwingDuration&&guard.Target==target&&GuardEnemy(guard,target,owner)&&KnightReach(guard,target)&&KnightFacing(guard,target)&&VoidCombatSafety.ClearPath(terrain,c.ComponentCreatureModel.EyePosition,target.ComponentBody.BoundingBox.Center(),6))
                {
                    budget--;guard.PursuitUntil=Now+DeathAuthorityRules.PursuitLease;Vector3 direction=target.ComponentBody.Position-c.ComponentBody.Position;
                    ComponentMiner.AttackBody(target.ComponentBody,c,target.ComponentBody.BoundingBox.Center(),direction.LengthSquared()>.001f?Vector3.Normalize(direction):c.ComponentBody.Matrix.Forward,NightmareKnightRules.Damage,true);
                }
            }
            if(guard.SwordTarget!=null||guard.Target==null||!GuardEnemy(guard,guard.Target,owner)||!KnightReach(guard,guard.Target))return;
            Vector2 to=(guard.Target.ComponentBody.Position-c.ComponentBody.Position).XZ;
            if(to.LengthSquared()>.01f)c.ComponentLocomotion.TurnOrder=new Vector2(MathUtils.Clamp(Vector2.Angle(c.ComponentBody.Matrix.Forward.XZ,to),-1,1),0);
            if(Now<guard.NextAttack||!KnightFacing(guard,guard.Target)||!VoidCombatSafety.ClearPath(terrain,c.ComponentCreatureModel.EyePosition,guard.Target.ComponentBody.BoundingBox.Center(),6))return;
            SetKnightCharge(guard,false);guard.SwordTarget=guard.Target;guard.SwordStarted=Now;guard.SwordImpactAt=Now+NightmareKnightRules.ImpactDelay;guard.NextAttack=Now+NightmareKnightRules.Cooldown;
            (c.ComponentCreatureModel as ComponentNightmareKnightModel)?.Swing(Now);Send(KnightSwingPacket(guard));
        }
        private DeathAuthorityPackage KnightSwingPacket(DeathGuard guard)=>new DeathAuthorityPackage{Kind=10,Caster=guard.Creature.Entity.EntityId,Target=guard.Key,Owner=guard.Owner,A=(float)Math.Max(0,Now-guard.SwordStarted)};
        private void ReceiveKnightSwing(DeathAuthorityPackage q)
        {
            if(!hasClock||!WorldCutRules.Finite(q.A)||q.A<0||q.A>=NightmareKnightRules.SwingDuration)return;
            double age=Math.Max(0,Math.Max(lastServerTime,Now+clockOffset)-q.Stamp)+q.A;if(age>=NightmareKnightRules.SwingDuration)return;
            Friend row;if(!clientFriends.TryGetValue(q.Target,out row)||!row.Knight||row.Hidden||row.Pending||row.EntityId!=q.Caster||row.Owner!=q.Owner)return;
            terrain.Project.FindEntityById(q.Caster,entity=>entity.FindComponent<ComponentNightmareKnightModel>(false)?.ReceiveSwing(q.Revision,(float)age));
        }
    }
}

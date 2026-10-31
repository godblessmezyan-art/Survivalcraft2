using System;
using System.Collections.Generic;
using Engine;
using Game;
namespace SCIEPort.Features.AbyssReaper
{
    internal sealed partial class DeathAuthorityRuntime
    {
        private readonly Dictionary<ComponentBody,float> softMass=new Dictionary<ComponentBody,float>();
        private static void GuardMoveTo(DeathGuard guard,Vector3 point,float speed,float range,ComponentBody ignored=null)
        {
            // Let native pathfinding make progress when its target has not moved.
            if(guard.Path.Destination.HasValue&&Vector3.DistanceSquared(guard.Path.Destination.Value,point)<.64f&&Math.Abs(guard.Path.Speed-speed)<.01f&&Math.Abs(guard.Path.Range-range)<.05f)return;
            guard.Path.SetDestination(point,speed,range,128,false,false,true,ignored);
        }
        private void BrakeNearPlayers(DeathGuard guard)
        {
            if(guard.Ridden)return;var body=guard.Creature.ComponentBody;float gap=1.7f+Math.Max(body.BoxSize.X,body.BoxSize.Z)*.5f;
            foreach(var player in players.ComponentPlayers)if(player.IsAddedToProject)
            {
                Vector3 away=body.Position-player.ComponentBody.Position;away.Y=0;float distance=away.LengthSquared();if(distance<.01f||distance>gap*gap)continue;
                away=Vector3.Normalize(away);float approach=Vector3.Dot(body.Velocity,away);
                if(approach<-.01f){body.Velocity-=away*approach;guard.Path.Stop();guard.Creature.ComponentLocomotion.WalkOrder=null;}
            }
        }
        private bool GuardEnemy(DeathGuard guard,ComponentCreature target,ComponentPlayer owner)
            =>SafeTarget(target)&&(Hostile(target,owner)||guard.Target==target&&Now<guard.PursuitUntil);
        private ComponentCreature ChooseGuardTarget(DeathGuard guard,ComponentPlayer owner,ref int budget)
        {
            Threat threat;
            if(threats.TryGetValue(owner.PlayerGuid,out threat)&&Now<threat.Until&&SafeTarget(threat.Creature)&&Vector3.DistanceSquared(owner.ComponentBody.Position,threat.Creature.ComponentBody.Position)<=DeathAuthorityRules.PursuitRange*DeathAuthorityRules.PursuitRange)
            {guard.PursuitUntil=Now+DeathAuthorityRules.PursuitLease;return threat.Creature;}
            if(guard.Target!=null&&Now<guard.PursuitUntil&&GuardEnemy(guard,guard.Target,owner)&&Vector3.DistanceSquared(owner.ComponentBody.Position,guard.Target.ComponentBody.Position)<=DeathAuthorityRules.PursuitRange*DeathAuthorityRules.PursuitRange)return guard.Target;
            if(budget<=0)return null;
            candidates.Clear();bodies.FindBodiesInArea(owner.ComponentBody.Position.XZ-new Vector2(24),owner.ComponentBody.Position.XZ+new Vector2(24),candidates);
            ComponentCreature chosen=null;float best=24*24;int checkedBodies=0;
            foreach(var body in candidates)
            {
                if(++checkedBodies>128||budget--<=0)break;var target=body.Entity.FindComponent<ComponentCreature>(false);if(!Hostile(target,owner))continue;
                float distance=Vector3.DistanceSquared(owner.ComponentBody.Position,body.Position);if(distance<best){best=distance;chosen=target;}
            }
            if(chosen!=null)guard.PursuitUntil=Now+DeathAuthorityRules.PursuitLease;return chosen;
        }
        private static bool MountApproach(ComponentPlayer player,DeathGuard guard)
        {
            if(!(guard.Cerberus||guard.Warhorse)||player.PlayerGuid!=guard.Owner)return false;
            Vector3 to=guard.Creature.ComponentBody.Position+Vector3.UnitY*1.9f-player.ComponentCreatureModel.EyePosition;
            return to.LengthSquared()<25&&to.LengthSquared()>.01f&&Vector3.Dot(Vector3.Normalize(to),player.ComponentCreatureModel.EyeRotation.GetForwardVector())>.65f;
        }
        private bool YieldToPlayer(DeathGuard guard)
        {
            if(guard.Ridden)return false;var body=guard.Creature.ComponentBody;
            float gap=1.7f+Math.Max(body.BoxSize.X,body.BoxSize.Z)*.5f;
            foreach(var player in players.ComponentPlayers)
            {
                if(!player.IsAddedToProject||player.ComponentHealth.Health<=0)continue;
                Vector3 away=body.Position-player.ComponentBody.Position;away.Y=0;float distance=away.LengthSquared();if(distance>=gap*gap)continue;
                Vector3 velocity=body.Velocity;
                if(distance>.01f){away=Vector3.Normalize(away);float approach=Vector3.Dot(velocity,away);if(approach<0)velocity-=away*approach;}
                else{away=player.ComponentBody.Matrix.Right;velocity.X=velocity.Z=0;}
                body.Velocity=velocity;
                if(MountApproach(player,guard)){guard.Path.Stop();return true;}
                Vector3 destination=player.ComponentBody.Position+away*(DeathAuthorityRules.FollowStop+1);
                if(VoidPortalSafety.Ready(terrain,Terrain.ToCell(destination.X),Terrain.ToCell(destination.Z))&&VoidPortalSafety.Unprotected(Terrain.ToCell(destination.X),Terrain.ToCell(destination.Z)))GuardMoveTo(guard,destination,.55f,.75f);
                else guard.Path.Stop();
                return true;
            }
            return false;
        }
        private void MoveGuard(DeathGuard guard,ComponentPlayer owner)
        {
            var c=guard.Creature;bool mounted=guard.Ridden;
            if(mounted&&!guard.AutoDriving){guard.Path.Stop();return;}
            if(!mounted&&YieldToPlayer(guard))return;
            if(guard.Target!=null)
            {
                float stop=guard.Cerberus?2.6f:1.2f+(c.ComponentBody.BoxSize.X+guard.Target.ComponentBody.BoxSize.X)*.5f;
                if(Vector3.DistanceSquared(c.ComponentBody.Position,guard.Target.ComponentBody.Position)>stop*stop)GuardMoveTo(guard,guard.Target.ComponentBody.Position,1,stop,mounted?owner.ComponentBody:null);
                else guard.Path.Stop();
            }
            else if(mounted)guard.Path.Stop();
            else
            {
                float distance=Vector3.DistanceSquared(c.ComponentBody.Position,owner.ComponentBody.Position);
                if(distance>DeathAuthorityRules.FollowStart*DeathAuthorityRules.FollowStart)guard.Following=true;
                if(distance<(DeathAuthorityRules.FollowStop+2)*(DeathAuthorityRules.FollowStop+2))guard.Following=false;
                if(!guard.Following){guard.Path.Stop();return;}
                Vector3 side=owner.ComponentBody.Matrix.Right*((guard.Key%3)-1)*1.8f;
                Vector3 destination=owner.ComponentBody.Position-owner.ComponentBody.Matrix.Forward*DeathAuthorityRules.FollowStop+side;
                GuardMoveTo(guard,destination,.65f,1.5f);
            }
        }
        private void ClearSoftMass(){foreach(var pair in softMass)pair.Key.Mass+=pair.Value;softMass.Clear();}
        private void SoftenBody(ComponentBody body)
        {
            if(body.ParentBody!=null||body.ChildBodies.Count>0)return;
            float range=2.3f+Math.Max(body.BoxSize.X,body.BoxSize.Z)*.5f;
            foreach(var player in players.ComponentPlayers)if(player.IsAddedToProject&&Vector3.DistanceSquared(player.ComponentBody.Position,body.Position)<range*range)
            {float delta=Math.Max(0,body.Mass-20);body.Mass-=delta;softMass[body]=delta;return;}
        }
        private void UpdateFriendlyYield()
        {
            ClearSoftMass();
            if(Authority){foreach(var g in guards.Values)if(g.Creature.IsAddedToProject&&!g.Ridden)SoftenBody(g.Creature.ComponentBody);}
            else foreach(var row in clientFriends.Values)if(!row.Hidden&&!row.Pending)terrain.Project.FindEntityById(row.EntityId,entity=>{var body=entity.FindComponent<ComponentBody>(false);if(body!=null)SoftenBody(body);});
        }
    }
}

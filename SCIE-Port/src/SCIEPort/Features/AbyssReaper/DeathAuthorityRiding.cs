using System;
using System.Collections.Generic;
using Engine;
using Game;
using Game.NetWork;
namespace SCIEPort.Features.AbyssReaper
{
    internal sealed partial class DeathAuthorityRuntime
    {
        private sealed class RidePlan{internal uint Revision;internal ushort Target;internal Vector3 Destination;internal float Stop;internal double Until;internal bool Move,Enabled;}
        private readonly Dictionary<ushort,RidePlan> ridePlans=new Dictionary<ushort,RidePlan>();
        private readonly HashSet<Game.ComponentPathfinding> drivingPaths=new HashSet<Game.ComponentPathfinding>();
        private readonly List<Game.ComponentPathfinding> endedPaths=new List<Game.ComponentPathfinding>();
        private readonly HashSet<Game.ComponentPathfinding> pathsSeen=new HashSet<Game.ComponentPathfinding>();
        private readonly List<ushort> expiredPlans=new List<ushort>();
        private double riderManualUntil,nextRiderInput;private ushort manualMount;
        private readonly Dictionary<ComponentHealth,float> riderFallBonuses=new Dictionary<ComponentHealth,float>();
        private static bool RiderOrder(ComponentPlayer p)
        {var input=p.ComponentInput.m_playerInput;return Math.Abs(input.Move.X)>.05f||Math.Abs(input.Move.Z)>.05f||input.Jump||(p.ComponentRider.Mount?.Entity.FindComponent<ComponentBoneDragonLocomotion>(false)?.VerticalInput??false)||p.ComponentGui.ModalPanelWidget!=null||DialogsManager.HasDialogs(p.GuiWidget);}
        private DeathGuard RiddenGuard(ComponentPlayer p)
        {
            var mount=p?.ComponentRider.Mount;if(mount==null)return null;DeathGuard g;
            return guards.TryGetValue(mount.Entity.EntityId,out g)&&g.Creature.Entity==mount.Entity&&g.Owner==p.PlayerGuid&&(g.Cerberus||g.Warhorse||g.Dragon)?g:null;
        }
        internal Friend RiddenFriend(ComponentPlayer p)
        {
            if(Authority){var guard=RiddenGuard(p);return guard==null?null:Row(guard);}
            var mount=p?.ComponentRider.Mount;if(mount!=null)foreach(var f in clientFriends.Values)if(!f.Hidden&&!f.Pending&&f.EntityId==mount.Entity.EntityId&&f.Owner==p.PlayerGuid&&(f.Cerberus||f.Warhorse||f.Dragon))return f;
            return null;
        }
        private bool RidingActor(ComponentPlayer p)=>Actor(p,false)&&p.ComponentSleep.SleepFactor<=0&&RiddenFriend(p)!=null;
        private void ManualRide(ComponentPlayer p,DeathGuard g)
        {
            g.ManualUntil=Now+DeathAuthorityRules.RiderPriority;g.AutoDriving=false;g.Path.Stop();g.NextThink=0;
        }
        private void ToggleRiding(ComponentPlayer p,ushort key)
        {
            var g=RiddenGuard(p);if(g==null||g.Key!=key){StatePacket(p,"请先骑上自己的契约坐骑");return;}
            g.RidingAuto=!g.RidingAuto;g.Target=g.BiteTarget=null;g.AutoDriving=false;g.NextThink=g.NextPlan=0;g.Path.Stop();
            FriendPacket(Row(g));PublishRidePlan(g);StatePacket(p,g.RidingAuto?"骑乘自动追击已开启 · 手动操作优先":"骑乘自动追击已关闭");
        }
        private void PublishRidePlan(DeathGuard g)
        {
            if(!g.Ridden||Now<g.NextPlan)return;g.NextPlan=Now+.25;
            var owner=OwnerPlayer(g.Owner);if(CommonLib.WorkType!=WorkType.Server||owner==null||owner.PlayerData.IsMainPlayer||owner.PlayerData.Client==null)return;
            bool move=g.AutoDriving&&g.Target!=null;Vector3 at=move?g.Target.ComponentBody.Position:Vector3.Zero;
            float stop=g.Dragon?3.4f:g.Cerberus?2.6f:1.2f+(g.Creature.ComponentBody.BoxSize.X+(g.Target?.ComponentBody.BoxSize.X??0))*.5f;
            Send(new DeathAuthorityPackage{Kind=9,Caster=g.Creature.Entity.EntityId,Target=move?g.Target.Entity.EntityId:(ushort)0,Owner=g.Owner,Flags=(byte)(move?1:g.AutoDriving?2:0),Position=at,A=Math.Min(6,stop)},owner.PlayerData.Client);
        }
        private void ReceiveRidePlan(DeathAuthorityPackage q)
        {
            if(!hasClock||q.Flags>2||!WorldCutRules.Finite(q.A)||q.A<0||q.A>6||q.Flags==1&&(!WorldCutRules.Coordinates(q.Position.X,q.Position.Y,q.Position.Z)||q.Target==0))return;
            Friend own=null;foreach(var f in clientFriends.Values)if(f.EntityId==q.Caster&&f.Owner==q.Owner&&f.RidingAuto&&!f.Hidden&&!f.Pending&&(f.Cerberus||f.Warhorse||f.Dragon)){own=f;break;}if(own==null)return;
            RidePlan old;if(ridePlans.TryGetValue(q.Caster,out old)&&!WorldCutRules.Newer(q.Revision,old.Revision))return;
            double age=Math.Max(0,Math.Max(lastServerTime,Now+clockOffset)-q.Stamp);if(age>1)return;
            if(ridePlans.Count>=24&&!ridePlans.ContainsKey(q.Caster))return;
            ridePlans[q.Caster]=new RidePlan{Revision=q.Revision,Target=q.Target,Destination=q.Position,Stop=q.A,Move=q.Flags==1,Enabled=q.Flags!=0,Until=Now+1-age};
        }
        private void SendRiderInput(ComponentPlayer p,Friend row)
        {
            if(Now<nextRiderInput)return;nextRiderInput=Now+.25;
            var s=Get(p);if(s==null||!worldKnown)return;if(unchecked(++s.Sent)==0)++s.Sent;
            CommonLib.Net?.QueuePackage(new DeathAuthorityPackage{Kind=0,Action=DeathAuthorityRules.RiderInput,Caster=p.Entity.EntityId,Target=row.Id,Sequence=s.Sent,World=worldId});
        }
        private void UpdateClientRiding()
        {
            expiredPlans.Clear();foreach(var plan in ridePlans)if(Now>plan.Value.Until+2)expiredPlans.Add(plan.Key);foreach(var key in expiredPlans)ridePlans.Remove(key);
            pathsSeen.Clear();
            foreach(var p in players.ComponentPlayers)if(p.PlayerData.IsMainPlayer&&Actor(p,false))
            {
                var row=RiddenFriend(p);if(row==null)continue;var mount=p.ComponentRider.Mount;
                var creature=mount.Entity.FindComponent<ComponentCreature>(false);var path=mount.Entity.FindComponent<ComponentPathfinding>(false);var steed=mount.Entity.FindComponent<ComponentSteedBehavior>(false);
                if(creature==null||path==null||steed==null)continue;
                if(manualMount!=mount.Entity.EntityId){manualMount=mount.Entity.EntityId;riderManualUntil=Now+.5;}
                if(RiderOrder(p)){riderManualUntil=Now+DeathAuthorityRules.RiderPriority;if(row.RidingAuto)SendRiderInput(p,row);}
                RidePlan plan;bool fresh=ridePlans.TryGetValue(row.EntityId,out plan)&&Now<plan.Until;
                bool drive=row.RidingAuto&&Now>=riderManualUntil&&(!fresh||plan.Enabled);
                // Native SCNET physics for a ridden body belongs to its local rider.
                // Only movement follows the host's plan; targeting/hits remain on the host.
                if(drive)
                {
                    pathsSeen.Add(path);drivingPaths.Add(path);
                    if(!fresh||!plan.Move){path.Stop();creature.ComponentLocomotion.WalkOrder=null;}
                    else
                    {
                        Vector3 destination=plan.Destination;
                        if(Vector3.DistanceSquared(destination,creature.ComponentBody.Position)>DeathAuthorityRules.PursuitRange*DeathAuthorityRules.PursuitRange){path.Stop();creature.ComponentLocomotion.WalkOrder=null;}
                        else
                        {
                            if(!path.Destination.HasValue||Vector3.DistanceSquared(path.Destination.Value,destination)>.16f)path.SetDestination(destination,1,plan.Stop,128,false,false,true,p.ComponentBody);
                            Vector2 direction=(destination-creature.ComponentBody.Position).XZ;if(direction.LengthSquared()>.01f)creature.ComponentLocomotion.TurnOrder=new Vector2(MathUtils.Clamp(Vector2.Angle(creature.ComponentBody.Matrix.Forward.XZ,direction),-1,1),0);
                            var cerberus=steed as ComponentCerberusSteed;if(cerberus!=null){cerberus.FaceTarget=destination;cerberus.FaceUntil=Now+.3;}
                        }
                    }
                }
                if(!drive&&drivingPaths.Contains(path)){path.Stop();drivingPaths.Remove(path);}
                var selector=mount.Entity.FindComponent<ComponentBehaviorSelector>(false);if(selector!=null){selector.IsDisableBehavior=true;foreach(var behavior in selector.m_behaviors)behavior.IsActive=!drive&&behavior is ComponentSteedBehavior;}
                steed.IsActive=!drive;var dragon=creature.ComponentLocomotion as ComponentBoneDragonLocomotion;if(dragon!=null)dragon.AutoDriving=drive;
                if(!drive){var cerberus=steed as ComponentCerberusSteed;if(cerberus!=null)cerberus.FaceTarget=null;}
            }
            endedPaths.Clear();foreach(var path in drivingPaths)if(!pathsSeen.Contains(path)){path.Stop();endedPaths.Add(path);}foreach(var path in endedPaths)drivingPaths.Remove(path);
        }
        private void ClearRiding()
        {foreach(var path in drivingPaths)path.Stop();drivingPaths.Clear();pathsSeen.Clear();endedPaths.Clear();ridePlans.Clear();expiredPlans.Clear();manualMount=0;riderManualUntil=nextRiderInput=0;ClearRidingHud();ClearRiderFall();}
        private void ClearRiderFall(){foreach(var pair in riderFallBonuses)pair.Key.FallResilience-=pair.Value;riderFallBonuses.Clear();}
        private void UpdateRiderFall()
        {
            ClearRiderFall();foreach(var player in players.ComponentPlayers)if(player.IsAddedToProject&&(player.ComponentRider.Mount?.Entity.FindComponent<ComponentCerberusModel>(false)!=null||player.ComponentRider.Mount?.Entity.FindComponent<ComponentBoneDragonModel>(false)!=null))
            {var health=player.ComponentHealth;float extra=Math.Max(0,12-health.FallResilience);health.FallResilience+=extra;riderFallBonuses[health]=extra;}
        }
    }
}

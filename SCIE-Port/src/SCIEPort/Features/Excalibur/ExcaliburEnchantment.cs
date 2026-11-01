using System;
using System.Collections.Generic;
using Engine;
using Game;
using Game.NetWork;
using Game.NetWork.Packages;
using SCIEPort.Features.FourSaints;
namespace SCIEPort.Features.Excalibur
{
    public sealed partial class SubsystemExcalibur
    {
        private sealed class Addon
        {internal ComponentPlayer Owner;internal ComponentBody Target;internal ComponentHealth Health;internal float Before;internal double Due;}
        private readonly List<Addon> addons=new List<Addon>(32);
        internal bool Enchanted(ComponentPlayer p)
            =>Actor(p)&&states.TryGetValue(p.Entity.EntityId,out var s)&&Fresh(s)&&Now<s.BlessingEnd;
        internal bool EnchantedDraw(DrawBlockEnvironmentData e)
        {
            if(e==null)return false;
            foreach(var s in states.Values)
            {
                if(!Enchanted(s.Player))continue;
                var fp=s.Player.Entity.FindComponent<ComponentFirstPersonModel>(false);
                var human=s.Player.ComponentCreatureModel as ComponentHumanModel;
                if((fp!=null&&ReferenceEquals(fp.m_drawBlockEnvironmentData,e))||(human!=null&&ReferenceEquals(human.m_drawBlockEnvironmentData,e)))return true;
            }
            return false;
        }
        internal bool Protects(ComponentHealth health)
        {
            var p=health?.Entity.FindComponent<ComponentPlayer>(false);
            if(!Actor(p)||!states.TryGetValue(p.Entity.EntityId,out var s)||!Fresh(s))return false;
            return s.Active!=null&&s.Active.Skill==3&&ExcaliburRules.Sanctuary((float)(Now-s.Active.Start));
        }
        private static float MaximumHealth(ComponentPlayer p)=>FourSaintsRules.MaximumHealth(SubsystemFourSaints.Equipment(p));
        private void CaptureEnchantedInput(ComponentPlayer p,State s)
        {
            if(!Enchanted(p)||Busy(p)||p.ComponentInput==null||(!p.PlayerData.IsMainPlayer&&CommonLib.WorkType!=WorkType.Local))return;
            var input=p.ComponentInput;var hit=input.m_playerInput.Hit??input.m_playerInput.Dig;
            if(!hit.HasValue)return;
            input.m_playerInput.Hit=null;input.m_playerInput.Dig=null;input.m_playerInput.Interact=null;
            if(!input.AllowHandleInput||p.GameWidget==null||!p.GameWidget.ActiveCamera.IsEntityControlEnabled||DialogsManager.HasDialogs(p.GuiWidget)||Now-s.LastRequest<.15)return;
            s.LastRequest=Now;
            var request=new ExcaliburPackage{Kind=2,Caster=p.Entity.EntityId,Sequence=++s.Sent,Direction=hit.Value.Direction};
            if(Authority)Enqueue(p,request);else if(CommonLib.WorkType==WorkType.Client)CommonLib.Net.QueuePackage(request);
        }
        internal ComponentBody EnchantedTarget(ComponentPlayer p,Vector3 direction,out Vector3 point)
        {
            point=Vector3.Zero;if(!Enchanted(p)||!Finite(direction)||direction.LengthSquared()<.98f||direction.LengthSquared()>1.02f)return null;
            Vector3 origin=p.ComponentCreatureModel.EyePosition,end=origin+direction*ExcaliburRules.MeleeReach;
            var result=bodies.Raycast(origin,end,0f,(body,distance)=>body.Entity!=p.Entity&&!body.IsChildOfBody(p.ComponentBody)&&!p.ComponentBody.IsChildOfBody(body));
            if(!result.HasValue)return null;
            var b=result.Value.ComponentBody;point=result.Value.HitPoint();
            if(!b.Entity.IsAddedToProject||b.Project!=Project||Vector3.Distance(origin,point)>ExcaliburRules.MeleeReach||!Loaded(point)||!Sight(origin,point))return null;
            if(b.Player!=null&&!info.WorldSettings.IsFriendlyFireEnabled)return null;
            var health=b.Entity.FindComponent<ComponentHealth>(false);if(health==null||health.Health<=0)return null;
            var moving=Project.FindSubsystem<SubsystemMovingBlocks>(false);
            if(moving!=null){var wall=moving.Raycast(origin,point,true);if(wall.HasValue)return null;}
            return b;
        }
        private void AcceptMelee(ComponentPlayer p,ExcaliburPackage packet)
        {
            if(!Authority||packet.Skill!=0||!Enchanted(p)||Busy(p))return;
            var s=Get(p);if(!ExcaliburRules.Newer(packet.Sequence,s.Accepted))return;s.Accepted=packet.Sequence;
            float external=Project.FindSubsystem<SubsystemFourSaints>(false)?.AttackFrequency(p)??1;
            if(Now-s.LastMelee<.66/(external*1.1f))return;
            Vector3 point;var target=EnchantedTarget(p,packet.Direction,out point);if(target==null)return;
            // Hit retains the real miner gate, tool/strength/probability/armor hooks. The
            // custom request changes only the verified player's 2 -> 3 block hit selection.
            double before=p.ComponentMiner.m_lastHitTime;p.ComponentMiner.Hit(target,point,packet.Direction);
            if(p.ComponentMiner.m_lastHitTime!=before){s.LastMelee=Now;s.ImpactPosition=point;s.ImpactUntil=Now+.25;Publish(s,3,point);}
        }
        // Called before native AttackBody changes health. A delayed, observed health loss is
        // required, so misses, blocked/invulnerable attacks and the beam cannot proc the enchant.
        internal void PrepareMelee(ComponentBody target,ComponentPlayer p,float power,bool melee)
        {
            if(!Authority||!melee||power<=0||!Enchanted(p)||Busy(p)||target==null||addons.Count>=32)return;
            var health=target.Entity.FindComponent<ComponentHealth>(false);
            if(health==null||health.Health<=0||health.IsInvulnerable||!target.Entity.IsAddedToProject)return;
            if(target.Player!=null&&!info.WorldSettings.IsFriendlyFireEnabled)return;
            var eye=p.ComponentCreatureModel.EyePosition;
            // Native's hit point can be anywhere on a large body, so use nearest AABB point.
            var point=Vector3.Max(target.BoundingBox.Min,Vector3.Min(eye,target.BoundingBox.Max));
            if(Related(p.ComponentBody,target)||Vector3.Distance(eye,point)>3||!Sight(eye,point))return;
            var s=Get(p);if(Now-s.LastAddon<.2)return;s.LastAddon=Now;
            addons.Add(new Addon{Owner=p,Target=target,Health=health,Before=health.Health,Due=Now+.03});
        }
        private void ApplyAddons()
        {
            for(int i=addons.Count-1;i>=0;i--)
            {
                var a=addons[i];if(Now<a.Due)continue;addons.RemoveAt(i);
                if(!Authority||!Actor(a.Owner)||!a.Target.Entity.IsAddedToProject||a.Health.Health<=0||a.Health.Health>=a.Before||a.Health.IsInvulnerable||Busy(a.Owner))continue;
                try
                {
                    if(!Loaded(a.Target.Position)||Protected(a.Target.Position))continue;
                    a.Health.Injure(ExcaliburRules.AddonPower/Math.Max(a.Health.AttackResilience,.001f),a.Owner,false,"圣剑·湖光附魔");
                    var s=Get(a.Owner);s.ImpactPosition=a.Target.BoundingBox.Center();s.ImpactUntil=Now+.25;
                    if(CommonLib.WorkType==WorkType.Server)Publish(s,3,s.ImpactPosition,true);
                    var position=s.ImpactPosition+Vector3.UnitY*.4f;var velocity=new Vector3(.2f,1.5f,.2f);var color=new Color(255,218,112);
                    if(CommonLib.WorkType==WorkType.Server)CommonLib.Net.QueuePackage(new ComponentHealthPackage(position,velocity,color,"15"));
                    Project.FindSubsystem<SubsystemParticles>(false)?.AddParticleSystem(new HitValueParticleSystem(position,velocity,color,"15"));
                }
                catch(Exception error){Log.Warning("[SCIEPort:Excalibur] Enchantment stopped: "+error);}
            }
        }
    }
}

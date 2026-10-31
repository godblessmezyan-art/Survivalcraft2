using System;
using Engine;
using Game;
namespace SCIEPort.Features.AbyssReaper
{
    // Owns selector/path orders; does not append reflection-created components to native entities.
    internal sealed class DeathGuard
    {
        internal ComponentCreature Creature;internal Guid Owner;internal ushort Key;internal bool Special;internal string OriginalName,Template;
        internal bool Warhorse=>Special&&Template==DeathAuthorityRules.MountTemplate;
        internal bool Cerberus=>Template==CerberusRules.Template;
        internal ComponentCreature BiteTarget;internal double BiteStarted=-100,BiteImpactAt;
        internal bool Ridden=>Creature?.Entity.FindComponent<ComponentMount>(false)?.Rider!=null;
        private float mountBonus;private bool wasRidden;
        internal double SpawnTime,NextThink,NextAttack,StuckSince;internal ComponentCreature Target;
        private ComponentBehaviorSelector selector;private ComponentChaseBehavior chase;internal ComponentPathfinding Path;
        private bool disabled,autoDespawn;private CreatureCategory mask;private float retaliate,touch,nonplayer;
        internal bool Bind(ComponentCreature c,Guid owner,bool special)
        {
            selector=c.Entity.FindComponent<ComponentBehaviorSelector>(false);Path=c.Entity.FindComponent<ComponentPathfinding>(false);
            if(selector==null||Path==null||c.Entity.FindComponent<ComponentMiner>(false)==null||c.ComponentCreatureModel==null)return false;
            Creature=c;Owner=owner;Special=special;Template=c.Entity.ValuesDictionary?.DatabaseObject?.Name??"";SpawnTime=c.ComponentSpawn?.SpawnTime??0;
            OriginalName=c.DisplayName;disabled=selector.IsDisableBehavior;autoDespawn=c.ComponentSpawn?.AutoDespawn??false;
            selector.IsDisableBehavior=true;foreach(var b in selector.m_behaviors)b.IsActive=false;
            chase=c.Entity.FindComponent<ComponentChaseBehavior>(false);
            if(chase!=null){mask=chase.m_autoChaseMask;retaliate=chase.m_chaseWhenAttackedProbability;touch=chase.m_chaseOnTouchProbability;nonplayer=chase.m_chaseNonPlayerProbability;chase.m_autoChaseMask=0;chase.m_chaseWhenAttackedProbability=0;chase.m_chaseOnTouchProbability=0;chase.m_chaseNonPlayerProbability=0;chase.m_target=null;chase.IsActive=false;}
            if(c.ComponentSpawn!=null)c.ComponentSpawn.AutoDespawn=false;
            c.DisplayName=Cerberus?CerberusRules.Name:special?(Warhorse?"夜魇战马":"冥界猎犬"):"契约 · "+OriginalName;RefreshSpeed();return true;
        }
        internal void Suppress()
        {
            bool ridden=Ridden;if(ridden&&!wasRidden)Path.Stop();wasRidden=ridden;
            foreach(var b in selector.m_behaviors)b.IsActive=ridden&&b is ComponentSteedBehavior;
            if(chase!=null){chase.m_target=null;chase.m_importanceLevel=0;}RefreshSpeed();
        }
        private void RefreshSpeed()
        {
            var locomotion=Creature?.Entity.FindComponent<ComponentLocomotion>(false);if(locomotion==null)return;
            locomotion.WalkSpeed-=mountBonus;mountBonus=Warhorse?Math.Max(0,locomotion.WalkSpeed)*.7f:0;locomotion.WalkSpeed+=mountBonus;
        }
        internal void Restore()
        {
            if(Creature==null)return;Path.Stop();Creature.DisplayName=OriginalName;selector.IsDisableBehavior=disabled;
            var locomotion=Creature.Entity.FindComponent<ComponentLocomotion>(false);if(locomotion!=null)locomotion.WalkSpeed-=mountBonus;mountBonus=0;
            if(Creature.ComponentSpawn!=null)Creature.ComponentSpawn.AutoDespawn=autoDespawn;
            if(chase!=null){chase.m_autoChaseMask=mask;chase.m_chaseWhenAttackedProbability=retaliate;chase.m_chaseOnTouchProbability=touch;chase.m_chaseNonPlayerProbability=nonplayer;chase.m_target=null;chase.m_importanceLevel=0;}
        }
    }
}

using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;
using Game.NetWork;
using GameEntitySystem;
namespace SCIEPort.Features.AbyssReaper
{
    // Local authority. Gameplay, draw geometry and sky tint do not depend on one another.
    internal sealed class VoidHorizonRuntime : IDisposable
    {
        private readonly SubsystemTerrain terrain;private readonly SubsystemBodies bodies;private readonly SubsystemTime clock;
        private readonly bool allowPlayers,allowPassive;private readonly int quality;
        private readonly VoidCooldowns ready=new VoidCooldowns();
        private readonly VoidHorizonScene scene=new VoidHorizonScene();
        private readonly HashSet<Entity> passHits=new HashSet<Entity>();
        private ComponentPlayer owner;private Vector3 center;private HorizonTimeline<Entity> timeline;private double nextPull;
        internal VoidHorizonRuntime(SubsystemTerrain t,SubsystemBodies b,SubsystemTime c,bool players,bool passive,int quality)
        {terrain=t;bodies=b;clock=c;allowPlayers=players;allowPassive=passive;this.quality=VoidSkillRules.Quality(quality);}
        internal bool Active => timeline!=null;
        internal bool Busy(ComponentPlayer p) => Active && p==owner && clock.GameTime-timeline.Started<2;
        internal double Remaining(ComponentPlayer p) => p!=null?ready.Remaining(p.PlayerGuid,clock.GameTime):0;
        private bool Actor(ComponentPlayer p)
        {
            return (CommonLib.WorkType==WorkType.Local || CommonLib.WorkType==WorkType.Server) && p!=null && p.IsAddedToProject && p.PlayerData.IsMainPlayer && p.PlayerData.IsReadyForPlaying
                && p.PlayerGuid!=Guid.Empty && p.ComponentHealth.Health>0 && p.ComponentSleep.SleepFactor<=0 && p.ComponentRider.Mount==null
                && p.ComponentBody.ParentBody==null && p.ComponentBody.ChildBodies.Count==0
                && Terrain.ExtractContents(p.ComponentMiner.ActiveBlockValue)==AbyssReaperBlock.Index;
        }
        internal bool TryCast(ComponentPlayer p,out string reason)
        {
            reason="当前无法释放终焉坍缩";
            if(!Actor(p)){reason="终焉坍缩暂仅支持单人或本地主机";return false;}
            if(Active){reason="虚空领域已经存在";return false;}
            if(Remaining(p)>0){reason="终焉坍缩冷却中";return false;}
            if(bodies.Bodies.Count>512){reason="实体预算已满";return false;}
            Vector3 at=p.ComponentBody.Position;
            if(!WorldCutDamage.Finite(at)||!VoidPortalSafety.Ready(terrain,Terrain.ToCell(at.X),Terrain.ToCell(at.Z))||!VoidPortalSafety.Unprotected(Terrain.ToCell(at.X),Terrain.ToCell(at.Z)))
            {reason="区块未就绪或领地受保护";return false;}
            if(!ready.Start(p.PlayerGuid,clock.GameTime,VoidSkillRules.HorizonCooldown)){reason="终焉冷却或容量不足";return false;}
            owner=p;center=at;timeline=new HorizonTimeline<Entity>(clock.GameTime);nextPull=clock.GameTime+8;
            reason="终焉 · 万象坍缩";return true;
        }
        private bool Target(ComponentBody body)
        {
            if(body==null || body.Entity==owner.Entity || !body.IsAddedToProject || body.Project!=owner.Project || body.ParentBody!=null || body.ChildBodies.Count>0)return false;
            var creature=body.Entity.FindComponent<ComponentCreature>(false);if(creature==null || creature.ComponentHealth.Health<=0)return false;
            bool player=creature is ComponentPlayer,hostile=creature.Category==CreatureCategory.LandPredator||creature.Category==CreatureCategory.WaterPredator;
            if(player?(!allowPlayers||!owner.Project.FindSubsystem<SubsystemGameInfo>(true).WorldSettings.IsFriendlyFireEnabled):(!hostile&&!allowPassive))return false;
            Vector3 c=body.BoundingBox.Center(),d=c-center;
            return WorldCutDamage.Finite(c)&&VoidSkillRules.InDomain(d.X,d.Y,d.Z,12f);
        }
        private void Affect(int mode)
        {
            int scanned=0,accepted=0,rays=0;passHits.Clear();
            foreach(var body in bodies.Bodies)
            {
                if(++scanned>512||accepted>=24)break;
                if(!Target(body) || passHits.Contains(body.Entity))continue;
                if(++rays>64)break;
                if(!VoidCombatSafety.ClearPath(terrain,center+Vector3.UnitY,body.BoundingBox.Center(),48f))continue;
                if(mode==2 && !timeline.AddFinalTarget(body.Entity,24))continue;
                passHits.Add(body.Entity);accepted++;
                Vector3 delta=center-body.Position;
                if(mode==0)
                {
                    delta.Y=0;if(delta.LengthSquared()>.25f)
                    {
                        Vector3 velocity=body.Velocity;
                        if(velocity.LengthSquared()<100f)body.Velocity=velocity+Vector3.Normalize(delta)*.35f;
                    }
                }
                else
                {
                    var target=body.BoundingBox.Center();Vector3 dir=target-center;
                    try { ComponentMiner.AttackBody(body,owner,target,dir.LengthSquared()>.01f?Vector3.Normalize(dir):Vector3.UnitZ,mode==2?VoidSkillRules.HorizonFinalPower:VoidSkillRules.HorizonTickPower,false); }
                    catch(Exception e) { Log.Warning("[SCIEPort:Abyss] Horizon target failed: "+e.GetType().Name); }
                }
            }
        }
        internal void Update()
        {
            if(!Active)return;
            if(!Actor(owner)){Cancel();return;}
            double elapsed=clock.GameTime-timeline.Started;
            try
            {
                int tick=timeline.TakeTick(clock.GameTime);
                if(tick>=0)Affect(1);
                if(elapsed>=8 && elapsed<14 && clock.GameTime>=nextPull) {nextPull=clock.GameTime+.25;Affect(0);}
                if(timeline.TakeFinal(clock.GameTime))Affect(2);
                if(elapsed>=VoidSkillRules.HorizonDuration)Cancel();
            }
            catch(Exception error) { Log.Warning("[SCIEPort:Abyss] Horizon cancelled: "+error.GetType().Name);Cancel(); }
        }
        internal float SkyBlend
        {
            get
            {
                if(!Active)return 0;float t=(float)(clock.GameTime-timeline.Started);
                return MathUtils.Saturate(MathUtils.Min(t/2f,(17f-t)/1f));
            }
        }
        internal void Draw(Camera camera)
        {
            if(Active)scene.Draw(camera,center,(float)(clock.GameTime-timeline.Started),SkyBlend,quality);
        }
        private void Cancel(){if(timeline!=null)timeline.Cancel();timeline=null;owner=null;passHits.Clear();}
        public void Dispose(){Cancel();ready.Clear();passHits.Clear();scene.Dispose();}
    }
}

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
        private readonly PrimitivesRenderer3D renderer=new PrimitivesRenderer3D();
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
            if(!Active)return;
            float t=(float)(clock.GameTime-timeline.Started),fade=SkyBlend;
            var core=renderer.FlatBatch(-2,DepthStencilState.DepthRead,RasterizerState.CullNone,BlendState.Opaque);
            var light=renderer.FlatBatch(-1,DepthStencilState.DepthRead,RasterizerState.CullNone,BlendState.AlphaBlend);
            Vector3 sky=camera.ViewPosition+new Vector3(0,65,55);
            Vector3 right=Vector3.UnitX,up=Vector3.Normalize(new Vector3(0,55,-65));
            if(t>=2 && t<17)
            {
                int crackSegments=VoidSkillRules.VfxSegments(quality,12,18,24);
                Vector3 previous=sky-right*58,previousTop=previous+up*.8f,previousBottom=previous-up*.8f;
                for(int i=1;i<=crackSegments;i++)
                {
                    Vector3 point=sky+right*(-58+i*(116f/crackSegments))+up*((i%2==0?1:-1)*(1.5f+(i%3)));
                    float width=.5f+(i%3)*.45f;Vector3 top=point+up*width,bottom=point-up*width;
                    core.QueueQuad(previousTop,top,bottom,previousBottom,new Color(3,0,8));
                    light.QueueLine(previousTop,top,new Color(170,82,225,(int)(230*fade)));
                    light.QueueLine(previousBottom,bottom,new Color(112,51,171,(int)(200*fade)));
                    previous=point;previousTop=top;previousBottom=bottom;
                }
            }
            if(t>=5 && t<17)
            {
                float appear=MathUtils.Saturate((t-5)/3),collapse=t<14?1:MathUtils.Saturate((16-t)/2);
                float radius=18f*appear*collapse;
                int ringSegments=VoidSkillRules.VfxSegments(quality,16,32,48);
                for(int i=0;i<ringSegments;i++)
                {
                    float a=(float)(i*Math.PI*2/ringSegments),b=(float)((i+1)*Math.PI*2/ringSegments);
                    Vector3 p=sky+right*(float)Math.Cos(a)*radius+up*(float)Math.Sin(a)*radius;
                    Vector3 q=sky+right*(float)Math.Cos(b)*radius+up*(float)Math.Sin(b)*radius;
                    core.QueueTriangle(sky,p,q,new Color(2,0,5));
                    for(int ring=0;ring<3;ring++)
                    {
                        float angle=(t*(.4f+ring*.23f))*(ring%2==0?1:-1),r=radius*(1.12f+ring*.2f);
                        Vector3 r1=sky+right*(float)Math.Cos(a+angle)*r+up*(float)Math.Sin(a+angle)*r*.48f;
                        Vector3 r2=sky+right*(float)Math.Cos(b+angle)*r+up*(float)Math.Sin(b+angle)*r*.48f;
                        int opacity=((i+ring*3)%7<3)?230:105;
                        light.QueueLine(r1,r2,new Color(128+ring*20,51+ring*10,190+ring*15,(int)(opacity*fade)));
                    }
                }
            }
            if(t>=8 && t<17)
            {
                float radius=t<16?40f:40f*MathUtils.Saturate(t-16);
                int fieldSegments=VoidSkillRules.VfxSegments(quality,24,40,64);
                for(int i=0;i<fieldSegments;i++)
                {
                    float a=(float)(i*Math.PI*2/fieldSegments),b=(float)((i+1)*Math.PI*2/fieldSegments);
                    Vector3 p=center+new Vector3((float)Math.Cos(a)*radius,.05f,(float)Math.Sin(a)*radius);
                    Vector3 q=center+new Vector3((float)Math.Cos(b)*radius,.05f,(float)Math.Sin(b)*radius);
                    light.QueueLine(p,q,new Color(146,64,206,(int)(210*fade)));
                }
            }
            renderer.Flush(camera.ViewProjectionMatrix);
        }
        private void Cancel(){if(timeline!=null)timeline.Cancel();timeline=null;owner=null;passHits.Clear();}
        public void Dispose(){Cancel();ready.Clear();passHits.Clear();renderer.Clear();}
    }
}

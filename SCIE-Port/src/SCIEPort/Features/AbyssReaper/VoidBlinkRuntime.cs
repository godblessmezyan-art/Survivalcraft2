using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;
using Game.NetWork;
using GameEntitySystem;
namespace SCIEPort.Features.AbyssReaper
{
    // Single-player authority only; remote position protocol is intentionally not guessed.
    internal sealed class VoidBlinkRuntime : IDisposable
    {
        private sealed class Visual { internal Vector3 From,To;internal double Expires; }
        private readonly SubsystemTerrain terrain;private readonly SubsystemBodies bodies;private readonly SubsystemTime clock;
        private readonly bool allowPlayers,allowPassive;private readonly int quality;
        private readonly VoidCooldowns cooldowns=new VoidCooldowns();
        private readonly List<Visual> visuals=new List<Visual>();
        private readonly HashSet<Entity> hits=new HashSet<Entity>();
        private readonly PrimitivesRenderer3D renderer=new PrimitivesRenderer3D();
        internal VoidBlinkRuntime(SubsystemTerrain terrain,SubsystemBodies bodies,SubsystemTime clock,bool players,bool passive,int quality)
        {this.terrain=terrain;this.bodies=bodies;this.clock=clock;allowPlayers=players;allowPassive=passive;this.quality=VoidSkillRules.Quality(quality);}
        internal double Remaining(ComponentPlayer p) => p!=null?cooldowns.Remaining(p.PlayerGuid,clock.GameTime):0;
        private bool Actor(ComponentPlayer p)
        {
            return (CommonLib.WorkType==WorkType.Local || CommonLib.WorkType==WorkType.Server) && p!=null && p.IsAddedToProject && p.PlayerData.IsMainPlayer && p.PlayerData.IsReadyForPlaying
                && p.PlayerGuid!=Guid.Empty && p.ComponentHealth.Health>0 && p.ComponentSleep.SleepFactor<=0 && p.ComponentRider.Mount==null
                && p.ComponentBody.ParentBody==null && p.ComponentBody.ChildBodies.Count==0
                && Terrain.ExtractContents(p.ComponentMiner.ActiveBlockValue)==AbyssReaperBlock.Index;
        }
        private bool Target(ComponentPlayer p,ComponentBody body)
        {
            if(body==null || body.Entity==p.Entity || !body.IsAddedToProject || body.Project!=p.Project)return false;
            var creature=body.Entity.FindComponent<ComponentCreature>(false);if(creature==null || creature.ComponentHealth.Health<=0)return false;
            Vector3 center=body.BoundingBox.Center();
            if(!WorldCutDamage.Finite(center) || !VoidPortalSafety.Ready(terrain,Terrain.ToCell(center.X),Terrain.ToCell(center.Z)) || !VoidPortalSafety.Unprotected(Terrain.ToCell(center.X),Terrain.ToCell(center.Z)))return false;
            bool player=creature is ComponentPlayer;
            bool predator=creature.Category==CreatureCategory.LandPredator||creature.Category==CreatureCategory.WaterPredator;
            return player?allowPlayers && p.Project.FindSubsystem<SubsystemGameInfo>(true).WorldSettings.IsFriendlyFireEnabled:predator||allowPassive;
        }
        private bool Landing(ComponentPlayer p,Vector3 center,out Vector3 result)
        {
            result=Vector3.Zero;
            int[] dx={0,1,-1,0,0,1,-1,1,-1,2,-2,0,0};int[] dz={0,0,0,1,-1,1,1,-1,-1,0,0,2,-2};
            for(int i=0;i<dx.Length;i++)
            {
                var at=new Vector3((float)Math.Floor(center.X)+dx[i]+.5f,(float)Math.Floor(center.Y),(float)Math.Floor(center.Z)+dz[i]+.5f);
                if(!VoidPortalSafety.SafeLanding(terrain,p,at) || !VoidCombatSafety.ClearPath(terrain,p.ComponentCreatureModel.EyePosition,at+Vector3.UnitY*.5f,VoidSkillRules.BlinkRange))continue;
                result=at;return true;
            }
            return false;
        }
        internal bool TryCast(ComponentPlayer p,out string reason)
        {
            reason="当前无法折跃";
            if(!Actor(p)) { reason="普通客户端折跃尚未启用";return false; }
            if(Remaining(p)>0){reason="虚影瞬狩冷却中";return false;}
            if(bodies.Bodies.Count>512){reason="附近实体过多，请稍后重试";return false;}
            Vector3 eye=p.ComponentCreatureModel.EyePosition,dir=p.ComponentCreatureModel.EyeRotation.GetForwardVector();
            if(!WorldCutDamage.Finite(eye)||!WorldCutDamage.Finite(dir)||dir.LengthSquared()<.1f)return false;
            dir=Vector3.Normalize(dir);Vector3 end=eye+dir*VoidSkillRules.BlinkRange;
            var ground=terrain.Raycast(eye,end,false,true,(v,d)=>BlocksManager.Blocks[Terrain.ExtractContents(v)].IsCollidable);
            var hit=bodies.Raycast(eye,end,0,(b,d)=>b!=p.ComponentBody);
            bool hunt=hit.HasValue && (!ground.HasValue || hit.Value.Distance<ground.Value.Distance);
            Vector3 center;
            if(hunt)
            {
                var target=hit.Value.ComponentBody;
                if(!Target(p,target)){reason="目标不可攻击";return false;}
                Vector3 facing=target.Rotation.GetForwardVector();facing.Y=0;
                if(facing.LengthSquared()<.01f)facing=dir;else facing=Vector3.Normalize(facing);
                center=target.Position-facing*2f;
            }
            else
            {
                if(!ground.HasValue || ground.Value.CellFace.Face!=4){reason="请瞄准 18 格内平整地面";return false;}
                var f=ground.Value.CellFace;center=new Vector3(f.X+.5f,f.Y+1,f.Z+.5f);
            }
            Vector3 destination;
            if(!Landing(p,center,out destination)){reason="没有可到达的安全落点";return false;}
            Vector3 old=p.ComponentBody.Position;
            // Capture cooldown before external damage hooks and never write a player on clients.
            if(!cooldowns.Start(p.PlayerGuid,clock.GameTime,VoidSkillRules.BlinkCooldown)){reason="折跃冷却或容量不足";return false;}
            p.ComponentBody.Position=destination;p.ComponentBody.Velocity=Vector3.Zero;
            if(visuals.Count<8)visuals.Add(new Visual { From=old,To=destination,Expires=clock.GameTime+.55 });
            if(hunt)Spin(p,destination);
            reason=hunt?"虚影瞬狩 · 回旋斩":"虚影瞬狩 · 空间折跃";return true;
        }
        private void Spin(ComponentPlayer player,Vector3 origin)
        {
            hits.Clear();int scanned=0;
            foreach(var body in bodies.Bodies)
            {
                if(++scanned>512 || hits.Count>=24)break;
                if(!Target(player,body) || hits.Contains(body.Entity))continue;
                Vector3 center=body.BoundingBox.Center(),delta=center-origin;
                if(!WorldCutDamage.Finite(center)||delta.LengthSquared()>16f)continue;
                int x=Terrain.ToCell(center.X),z=Terrain.ToCell(center.Z);
                if(!VoidPortalSafety.Ready(terrain,x,z)||!VoidPortalSafety.Unprotected(x,z))continue;
                if(!VoidCombatSafety.ClearPath(terrain,origin+Vector3.UnitY,center,VoidSkillRules.BlinkRange))continue;
                hits.Add(body.Entity);
                try { ComponentMiner.AttackBody(body,player,center,delta.LengthSquared()>.01f?Vector3.Normalize(delta):Vector3.UnitZ,VoidSkillRules.BlinkPower,false); }
                catch(Exception e) { Log.Warning("[SCIEPort:Abyss] Blink target failed: "+e.GetType().Name); }
            }
            hits.Clear();
        }
        internal void Update(){for(int i=visuals.Count-1;i>=0;i--)if(clock.GameTime>=visuals[i].Expires)visuals.RemoveAt(i);}
        internal void Draw(Camera camera)
        {
            if(visuals.Count==0)return;
            var batch=renderer.FlatBatch(-1,DepthStencilState.DepthRead,RasterizerState.CullNone,BlendState.AlphaBlend);
            foreach(var v in visuals)
            {
                float fade=MathUtils.Saturate((float)((v.Expires-clock.GameTime)/.55));
                foreach(var pos in new[]{v.From,v.To})
                    for(int i=0;i<VoidSkillRules.VfxSegments(quality,3,5,7);i++)
                    {
                        Vector3 a=pos+Vector3.UnitY*(i*.32f)+Vector3.UnitX*((i%2==0?-.12f:.12f)*fade);
                        Vector3 b=pos+Vector3.UnitY*((i+1)*.32f)+Vector3.UnitX*((i%2==0?.12f:-.12f)*fade);
                        batch.QueueLine(a,b,new Color(144,65,200,(int)(220*fade)));
                    }
            }
            renderer.Flush(camera.ViewProjectionMatrix);
        }
        public void Dispose(){cooldowns.Clear();visuals.Clear();hits.Clear();renderer.Clear();}
    }
}

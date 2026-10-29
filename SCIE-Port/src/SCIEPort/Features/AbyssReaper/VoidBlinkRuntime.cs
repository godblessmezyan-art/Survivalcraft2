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
        private sealed class Visual { internal Vector3 From,To;internal double Started,Expires;internal bool Hunt; }
        private readonly SubsystemTerrain terrain;private readonly SubsystemBodies bodies;private readonly SubsystemTime clock;
        private readonly bool allowPlayers,allowPassive;private readonly int quality;
        private readonly SubsystemAudio audio;
        private ComponentPlayer actionOwner;private BlinkActionClock action;private Vector3 lastOrder,lastOffset;private bool orderOwned;
        private readonly VoidCooldowns cooldowns=new VoidCooldowns();
        private readonly List<Visual> visuals=new List<Visual>();
        private readonly HashSet<Entity> hits=new HashSet<Entity>();
        private readonly PrimitivesRenderer3D renderer=new PrimitivesRenderer3D();
        internal VoidBlinkRuntime(SubsystemTerrain terrain,SubsystemBodies bodies,SubsystemTime clock,SubsystemAudio audio,bool players,bool passive,int quality)
        {this.terrain=terrain;this.bodies=bodies;this.clock=clock;this.audio=audio;allowPlayers=players;allowPassive=passive;this.quality=VoidSkillRules.Quality(quality);}
        internal double Remaining(ComponentPlayer p) => p!=null?cooldowns.Remaining(p.PlayerGuid,clock.GameTime):0;
        internal bool Busy(ComponentPlayer p) => actionOwner==p && action!=null && action.Progress(clock.GameTime)>=0;
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
            int hitCount=hunt?Spin(p,destination):0;
            try
            {
                if(visuals.Count<8)visuals.Add(new Visual { From=old,To=destination,Started=clock.GameTime,Expires=clock.GameTime+.55,Hunt=hunt });
                CancelAction();actionOwner=p;action=new BlinkActionClock(clock.GameTime);
                try { audio?.PlaySound(hunt?"Audio/Abyss/BlinkHunt":"Audio/Abyss/BlinkShift",.75f,0f,destination,18f,false); }
                catch(Exception error) { Log.Warning("[SCIEPort:Abyss] Blink sound unavailable: "+error.GetType().Name); }
            }
            catch(Exception error) { CancelAction();Log.Warning("[SCIEPort:Abyss] Blink presentation unavailable: "+error.GetType().Name); }
            reason=hunt?"虚影瞬狩 · 回旋斩（"+hitCount+"个目标）":"虚影瞬狩 · 空间折跃";return true;
        }
        private int Spin(ComponentPlayer player,Vector3 origin)
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
            int count=hits.Count;hits.Clear();return count;
        }
        internal void Update()
        {
            for(int i=visuals.Count-1;i>=0;i--)if(clock.GameTime>=visuals[i].Expires)visuals.RemoveAt(i);
            if(action==null)return;
            double q=action.Progress(clock.GameTime);
            if(q<0 || !Actor(actionOwner)) { CancelAction();return; }
            try
            {
                float progress=(float)q;
                var pose=AbyssMeleeMotion.Sample(2,progress);
                Vector3 rotation=new Vector3(pose.Pitch,pose.Yaw,pose.Roll),offset=new Vector3(pose.X,pose.Y,pose.Z);
                var fp=actionOwner.Entity.FindComponent<ComponentFirstPersonModel>(false);
                if(fp!=null){fp.ItemRotationOrder=rotation;fp.ItemOffsetOrder=offset;}
                actionOwner.ComponentCreatureModel.InHandItemRotationOrder=rotation;
                actionOwner.ComponentCreatureModel.InHandItemOffsetOrder=offset*.35f;
                lastOrder=rotation;lastOffset=offset;orderOwned=true;
            }
            catch(Exception error) { CancelAction();Log.Warning("[SCIEPort:Abyss] Blink animation cancelled: "+error.GetType().Name); }
        }
        internal void Cancel(ComponentPlayer p) { if(actionOwner==p)CancelAction(); }
        private void CancelAction()
        {
            if(action!=null)action.Cancel();
            if(orderOwned && actionOwner!=null)
            {
                try
                {
                    var fp=actionOwner.Entity.FindComponent<ComponentFirstPersonModel>(false);
                    if(fp!=null && fp.ItemRotationOrder==lastOrder)fp.ItemRotationOrder=Vector3.Zero;
                    if(actionOwner.ComponentCreatureModel.InHandItemRotationOrder==lastOrder)actionOwner.ComponentCreatureModel.InHandItemRotationOrder=Vector3.Zero;
                    if(fp!=null&&fp.ItemOffsetOrder==lastOffset)fp.ItemOffsetOrder=Vector3.Zero;
                    if(actionOwner.ComponentCreatureModel.InHandItemOffsetOrder==lastOffset*.35f)actionOwner.ComponentCreatureModel.InHandItemOffsetOrder=Vector3.Zero;
                }
                catch(Exception) { /* Owner can already be removed while the world is closing. */ }
            }
            action=null;actionOwner=null;orderOwned=false;
        }
        internal void Draw(Camera camera)
        {
            if(visuals.Count==0)return;
            var batch=renderer.FlatBatch(-1,DepthStencilState.DepthRead,RasterizerState.CullNone,BlendState.AlphaBlend);
            int ghosts=VoidSkillRules.VfxSegments(quality,2,3,3);
            int arcs=VoidSkillRules.VfxSegments(quality,8,12,20);
            foreach(var v in visuals)
            {
                float fade=MathUtils.Saturate((float)((v.Expires-clock.GameTime)/.55));
                Vector3 path=v.To-v.From;path.Y=0;
                Vector3 side=path.LengthSquared()>.001f?Vector3.Normalize(Vector3.Cross(path,Vector3.UnitY)):Vector3.UnitX;
                for(int i=0;i<ghosts;i++)
                {
                    float f=(i+1f)/(ghosts+1f)*.35f;
                    Vector3 c=v.From+(v.To-v.From)*f;
                    float alpha=fade*(1f-f*.45f);
                    Color color=new Color(139,65,205,(int)(150*alpha));
                    Vector3 head=c+Vector3.UnitY*1.7f,shoulder=c+Vector3.UnitY*1.3f;
                    batch.QueueLine(head,shoulder,color);
                    batch.QueueLine(shoulder-side*.32f,shoulder+side*.32f,color);
                    batch.QueueLine(shoulder,c+Vector3.UnitY*.65f,color);
                    batch.QueueLine(c+Vector3.UnitY*.65f,c-side*.23f,color);
                    batch.QueueLine(c+Vector3.UnitY*.65f,c+side*.23f,color);
                }
                if(v.Hunt)
                {
                    float q=1f-fade;
                    float end=q*(float)Math.PI*2f;
                    Vector3 center=v.To+Vector3.UnitY*.75f;
                    for(int i=0;i<arcs;i++)
                    {
                        float a=end-(i+1)*1.5f/arcs,b=end-i*1.5f/arcs;
                        Vector3 p=center+new Vector3((float)Math.Cos(a)*2.1f,0,(float)Math.Sin(a)*2.1f);
                        Vector3 r=center+new Vector3((float)Math.Cos(b)*2.1f,0,(float)Math.Sin(b)*2.1f);
                        batch.QueueLine(p,r,new Color(172,84,226,(int)(220*fade)));
                    }
                }
                for(int endIndex=0;endIndex<2;endIndex++)
                {
                    Vector3 fissure=endIndex==0?v.From:v.To;
                    for(int i=0;i<VoidSkillRules.VfxSegments(quality,3,5,7);i++)
                    {
                        Vector3 a=fissure+Vector3.UnitY*(i*.25f)+side*((i%2==0?-.12f:.12f)*fade);
                        Vector3 b=fissure+Vector3.UnitY*((i+1)*.25f)-side*((i%2==0?-.12f:.12f)*fade);
                        batch.QueueLine(a,b,new Color(144,65,200,(int)(220*fade)));
                    }
                }
            }
            renderer.Flush(camera.ViewProjectionMatrix);
        }
        public void Dispose(){CancelAction();cooldowns.Clear();visuals.Clear();hits.Clear();renderer.Clear();}
    }
}

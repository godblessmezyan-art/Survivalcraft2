using System;
using System.Collections.Generic;
using Engine;
using Game;
using Game.NetWork;
using GameEntitySystem;
namespace SCIEPort.Features.AbyssReaper
{
    // Collision and damage have no renderer, particles, frame visibility or quality setting.
    internal sealed class WorldCutDamage
    {
        private readonly SubsystemTerrain terrain;
        private readonly SubsystemBodies bodies;
        private readonly SubsystemGameInfo info;
        private readonly bool players,passive;
        private readonly float sweepPower,closePower;
        private readonly List<ComponentBody> candidates=new List<ComponentBody>(WorldCutRules.MaxBodies);
        private int rays,checks;
        internal WorldCutDamage(SubsystemTerrain t,SubsystemBodies b,SubsystemGameInfo i,bool allowPlayers,bool allowPassive,float sweep,float close)
        { terrain=t;bodies=b;info=i;players=allowPlayers;passive=allowPassive;sweepPower=sweep;closePower=close; }
        internal void BeginFrame(bool active)
        {
            rays=0;checks=0;candidates.Clear();if(!active)return;
            foreach(var body in bodies.Bodies) { if(candidates.Count>=WorldCutRules.MaxBodies)break;candidates.Add(body); }
        }
        internal bool Loaded(Vector3 p)
        {
            if(!WorldCutRules.Coordinates(p.X,p.Y,p.Z))return false;
            var chunk=terrain.Terrain.GetChunkAtCell(Terrain.ToCell(p.X),Terrain.ToCell(p.Z));
            return chunk!=null && chunk.State>=TerrainChunkState.Valid;
        }
        private bool Protected(Vector3 p)
        {
            Territoriy territory;
            return SubsystemBedrockBlockBehavior.CheckIsInTerritoriy(Terrain.ToCell(p.X),Terrain.ToCell(p.Z),out territory);
        }
        internal WorldCutFailure Step(Vector3 origin,Vector3 direction,Vector3 right,float from,float to,out float safe)
        {
            safe=to;
            if(rays+27>WorldCutRules.MaxRays)return WorldCutFailure.Capacity;
            for(int x=0;x<9;x++)for(int y=-1;y<=1;y++)
            {
                Vector3 a=origin+direction*from+right*(-3+x*.75f)+Vector3.UnitY*(y*.55f);
                Vector3 b=origin+direction*to+right*(-3+x*.75f)+Vector3.UnitY*(y*.55f);
                if(!Loaded(a)||!Loaded(b))return WorldCutFailure.Unloaded;
                if(Protected(a)||Protected(b))return WorldCutFailure.Protected;
                rays++;
                var wall=terrain.Raycast(a,b,false,true,(v,d)=>BlocksManager.Blocks[Terrain.ExtractContents(v)].IsCollidable);
                if(wall.HasValue)safe=Math.Min(safe,Math.Max(from,from+wall.Value.Distance-.025f));
            }
            return safe<to?WorldCutFailure.Wall:WorldCutFailure.None;
        }
        private bool ClearSight(Vector3 a,Vector3 b)
        {
            if(++rays>WorldCutRules.MaxRays)return false;
            return !terrain.Raycast(a,b,false,true,(v,d)=>BlocksManager.Blocks[Terrain.ExtractContents(v)].IsCollidable).HasValue;
        }
        private bool Eligible(WorldCutRuntime.Cast cast,ComponentBody body,out Vector3 center,out Vector3 half)
        {
            center=Vector3.Zero;half=Vector3.Zero;
            if(body==null || body.Entity==cast.Owner.Entity || !body.Entity.IsAddedToProject || body.Project!=cast.Owner.Project)return false;
            if(body.ParentBody==cast.Owner.ComponentBody || cast.Owner.ComponentBody.ParentBody==body)return false;
            var creature=body.Entity.FindComponent<ComponentCreature>(false);
            if(creature==null || creature.ComponentHealth.Health<=0)return false;
            bool player=creature is ComponentPlayer;
            bool predator=creature.Category==CreatureCategory.LandPredator||creature.Category==CreatureCategory.WaterPredator;
            if(player?(!players||!info.WorldSettings.IsFriendlyFireEnabled):(!predator&&!passive))return false;
            center=body.BoundingBox.Center();half=(body.BoundingBox.Max-body.BoundingBox.Min)*.5f;
            if(!Loaded(center)||!Finite(half)||Protected(center))return false;
            for(int x=0;x<2;x++)for(int z=0;z<2;z++)
            {
                Vector3 corner=new Vector3(x==0?body.BoundingBox.Min.X:body.BoundingBox.Max.X,center.Y,z==0?body.BoundingBox.Min.Z:body.BoundingBox.Max.Z);
                if(!Loaded(corner)||Protected(corner))return false;
            }
            return true;
        }
        internal bool Apply(WorldCutRuntime.Cast cast,float from,float to,bool closing)
        {
            // Even a forged presentation event can never enter client-side damage.
            if(CommonLib.WorkType!=WorkType.Local && CommonLib.WorkType!=WorkType.Server)return false;
            if(cast.VisualOnly)return false;
            Vector3 at=cast.Origin+cast.Direction*(closing?to:from);
            bool hit=false;
            foreach(var body in candidates)
            {
                if(++checks>WorldCutRules.MaxBodyChecks || cast.Hits.Count(closing)>=WorldCutRules.MaxPhaseHits)break;
                if(body==null||body.Entity==cast.Owner.Entity||cast.Hits.Contains(body.Entity,closing))continue;
                Vector3 center=body.BoundingBox.Center(),half=(body.BoundingBox.Max-body.BoundingBox.Min)*.5f;
                if(!Finite(center)||!Finite(half))continue;
                Vector3 delta=center-cast.Origin;
                float along=Vector3.Dot(delta,cast.Direction),side=Vector3.Dot(delta,cast.Right);
                float ha=Math.Abs(cast.Direction.X)*half.X+Math.Abs(cast.Direction.Z)*half.Z;
                float hs=Math.Abs(cast.Right.X)*half.X+Math.Abs(cast.Right.Z)*half.Z;
                if(closing?!WorldCutRules.Collapse(along-to,side,delta.Y,ha,hs,half.Y):!WorldCutRules.Swept(along,side,delta.Y,ha,hs,half.Y,from,to))continue;
                if(!Eligible(cast,body,out center,out half)||!ClearSight(at,center)||!cast.Hits.Add(body.Entity,closing))continue;
                // Capture dedup before calling external hooks: exceptions cannot cause retry damage.
                ComponentMiner.AttackBody(body,cast.Owner,center,cast.Direction,closing?closePower:sweepPower,false);
                hit=true;
            }
            return hit;
        }
        internal static bool Finite(Vector3 v) => WorldCutRules.Finite(v.X)&&WorldCutRules.Finite(v.Y)&&WorldCutRules.Finite(v.Z);
        internal void Clear() { candidates.Clear(); }
    }
}

using System;
using System.Collections.Generic;
using Engine;
using Game;
using Game.NetWork;
using GameEntitySystem;
namespace SCIEPort.Features.AbyssReaper
{
    internal sealed class VoidSpaceDamage
    {
        internal sealed class Job
        {
            internal ComponentPlayer Owner;internal Vector3 From,To,Direction;internal byte Skill;
            internal bool VisualOnly=false;internal int Cursor;internal double NextQuery,Expires;
            internal readonly DynamicArray<ComponentBody> Bodies=new DynamicArray<ComponentBody>();
            internal readonly HashSet<Entity> Hits=new HashSet<Entity>();
        }
        private readonly SubsystemTerrain terrain;private readonly SubsystemBodies bodies;
        internal VoidSpaceDamage(SubsystemTerrain terrain,SubsystemBodies bodies){this.terrain=terrain;this.bodies=bodies;}
        internal static bool IsPlayerCarrier(ComponentBody body)
        {
            if(body.ParentBody?.Entity.FindComponent<ComponentPlayer>(false)!=null)return true;
            foreach(var child in body.ChildBodies)if(child.Entity.FindComponent<ComponentPlayer>(false)!=null)return true;
            return false;
        }
        internal bool Eligible(ComponentPlayer owner,ComponentBody body)
        {
            if(body==null||body.Entity==owner.Entity||!body.IsAddedToProject||body.Project!=owner.Project||body.Entity.FindComponent<ComponentPlayer>(false)!=null||IsPlayerCarrier(body))return false;
            var creature=body.Entity.FindComponent<ComponentCreature>(false);if(creature==null||creature.ComponentHealth.Health<=0)return false;
            var box=body.BoundingBox;Vector3 center=box.Center();
            if(!WorldCutDamage.Finite(center)||!WorldCutRules.Coordinates(center.X,center.Y,center.Z))return false;
            for(int i=0;i<5;i++)
            {
                float x=i==4?center.X:i%2==0?box.Min.X:box.Max.X,z=i==4?center.Z:i/2==0?box.Min.Z:box.Max.Z;
                if(!VoidPortalSafety.Ready(terrain,Terrain.ToCell(x),Terrain.ToCell(z))||!VoidPortalSafety.Unprotected(Terrain.ToCell(x),Terrain.ToCell(z)))return false;
            }
            return true;
        }
        private bool Sight(Vector3 from,Vector3 to)
            => Vector3.DistanceSquared(from,to)<.0001f||VoidCombatSafety.ClearPath(terrain,from,to,30);
        private bool Contact(Job job,ComponentBody body,out Vector3 origin)
        {
            Vector3 center=body.BoundingBox.Center();origin=job.From;
            if(job.Skill==VoidSpaceRules.Shatter)return Vector3.DistanceSquared(center,job.From)<=VoidShatterRules.Radius*VoidShatterRules.Radius&&Sight(job.From,center);
            if(job.Skill==VoidSpaceRules.Blink)
            {
                Vector3 a=job.From+Vector3.UnitY,b=job.To+Vector3.UnitY;
                bool hitA=Vector3.DistanceSquared(center,a)<=VoidSpaceRules.BlinkRiftRadius*VoidSpaceRules.BlinkRiftRadius&&Sight(a,center);
                bool hitB=Vector3.DistanceSquared(center,b)<=VoidSpaceRules.BlinkRiftRadius*VoidSpaceRules.BlinkRiftRadius&&Sight(b,center);
                origin=hitA?a:b;return hitA||hitB;
            }
            Vector3 delta=center-job.From,half=(body.BoundingBox.Max-body.BoundingBox.Min)*.5f,right=Vector3.Cross(job.Direction,Vector3.UnitY);
            float along=Vector3.Dot(delta,job.Direction),length=Math.Min(25,Vector3.Distance(job.From,job.To)),side=Vector3.Dot(delta,right);
            float ha=Math.Abs(job.Direction.X)*half.X+Math.Abs(job.Direction.Z)*half.Z,hs=Math.Abs(right.X)*half.X+Math.Abs(right.Z)*half.Z;
            if(!WorldCutRules.Swept(along,side,delta.Y,ha,hs,half.Y,0,length))return false;
            origin=job.From+job.Direction*MathUtils.Clamp(along,0,length);return Sight(origin,center);
        }
        internal void Refresh(Job job)
        {
            job.Bodies.Clear();job.Cursor=0;float radius=job.Skill==VoidSpaceRules.Shatter?10:job.Skill==VoidSpaceRules.Blink?VoidSpaceRules.BlinkRiftRadius:3.5f;
            Vector3 min=Vector3.Min(job.From,job.To)-new Vector3(radius),max=Vector3.Max(job.From,job.To)+new Vector3(radius);
            bodies.FindBodiesInArea(min.XZ,max.XZ,job.Bodies);
        }
        internal void Apply(Job job,double now,ref int budget,int slice)
        {
            if(job.VisualOnly||(CommonLib.WorkType!=WorkType.Local&&CommonLib.WorkType!=WorkType.Server)||budget<=0)return;
            if(job.Cursor>=job.Bodies.Count)
            {
                if(job.Skill==VoidSpaceRules.Shatter||now<job.NextQuery)return;
                Refresh(job);job.NextQuery=now+.1;
            }
            for(int n=0;n<slice&&budget>0&&job.Cursor<job.Bodies.Count;n++,budget--)
            {
                var body=job.Bodies.Array[job.Cursor++];Vector3 origin;
                if(body==null||job.Hits.Contains(body.Entity)||!Eligible(job.Owner,body)||!Contact(job,body,out origin))continue;
                job.Hits.Add(body.Entity);Vector3 delta=body.BoundingBox.Center()-origin;
                float power=job.Skill==VoidSpaceRules.Cut?VoidSpaceRules.CutRiftPower:job.Skill==VoidSpaceRules.Blink?VoidSpaceRules.BlinkPower:VoidShatterRules.Power;
                try{ComponentMiner.AttackBody(body,job.Owner,body.BoundingBox.Center(),delta.LengthSquared()>.001f?Vector3.Normalize(delta):Vector3.UnitY,power,false);}
                catch(Exception e){Log.Warning("[SCIEPort:Space] Target hook failed: "+e.GetType().Name);}
            }
        }
    }
}

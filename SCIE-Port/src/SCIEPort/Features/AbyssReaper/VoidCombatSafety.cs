using System;
using Engine;
using Game;
namespace SCIEPort.Features.AbyssReaper
{
    internal static class VoidCombatSafety
    {
        internal static bool ClearPath(SubsystemTerrain terrain,Vector3 from,Vector3 to,float maxRange)
        {
            if(!WorldCutDamage.Finite(from)||!WorldCutDamage.Finite(to)||!WorldCutRules.Finite(maxRange)||maxRange<=0 || Vector3.DistanceSquared(from,to)>maxRange*maxRange)return false;
            if(Vector3.DistanceSquared(from,to)<.0001f)return false;
            int steps=Math.Max(1,(int)Math.Ceiling(Vector3.Distance(from,to)*2));
            if(steps>128)return false;
            for(int i=0;i<=steps;i++)
            {
                Vector3 at=Vector3.Lerp(from,to,i/(float)steps);int x=Terrain.ToCell(at.X),z=Terrain.ToCell(at.Z);
                if(!WorldCutRules.Coordinates(at.X,at.Y,at.Z)||!VoidPortalSafety.Ready(terrain,x,z)||!VoidPortalSafety.Unprotected(x,z))return false;
            }
            if(terrain.Raycast(from,to,false,true,(v,d)=>BlocksManager.Blocks[Terrain.ExtractContents(v)].IsCollidable).HasValue)return false;
            var moving=terrain.Project.FindSubsystem<SubsystemMovingBlocks>(true);
            if(moving.MovingBlockSets.Count>VoidPortalSafety.MaxCollisionBoxes)return false;
            return !moving.Raycast(from,to,true).HasValue;
        }
    }
}

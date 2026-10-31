using System;
using Engine;
using Game;
using Game.NetWork;
namespace SCIEPort.Features.AbyssReaper
{
    // Native collision boxes, no floor requirement. A swept body volume prevents thin-wall tunnelling.
    internal sealed class VoidBlinkSafety
    {
        private readonly SubsystemTerrain terrain;
        private readonly DynamicArray<ComponentBody.CollisionBox> obstacles=new DynamicArray<ComponentBody.CollisionBox>();
        internal VoidBlinkSafety(SubsystemTerrain terrain){this.terrain=terrain;}
        internal static bool ValidSize(Vector3 size) => WorldCutDamage.Finite(size)&&size.X>.05f&&size.Z>.05f&&size.Y>.1f&&size.X<=2&&size.Z<=2&&size.Y<=3;
        internal static BoundingBox Box(ComponentPlayer p,Vector3 feet)
        {
            Vector3 size=p.ComponentBody.BoxSize;
            return new BoundingBox(feet-new Vector3(size.X*.5f+.05f,-.015f,size.Z*.5f+.05f),feet+new Vector3(size.X*.5f+.05f,size.Y-.015f,size.Z*.5f+.05f));
        }
        internal bool Fits(ComponentPlayer p,Vector3 feet,out string reason) => Clear(p,Box(p,feet),out reason);
        private bool Clear(ComponentPlayer p,BoundingBox box,out string reason)
        {
            reason="前方没有足够容纳身体的空间";
            if(p==null||!p.IsAddedToProject||!ValidSize(p.ComponentBody.BoxSize)||!WorldCutDamage.Finite(box.Min)||!WorldCutDamage.Finite(box.Max)||box.Min.Y<1||box.Max.Y>=255||Math.Abs(box.Min.X)>=999999||Math.Abs(box.Max.X)>=999999||Math.Abs(box.Min.Z)>=999999||Math.Abs(box.Max.Z)>=999999)return false;
            int minX=Terrain.ToCell(box.Min.X),maxX=Terrain.ToCell(box.Max.X),minZ=Terrain.ToCell(box.Min.Z),maxZ=Terrain.ToCell(box.Max.Z);
            int minY=Terrain.ToCell(box.Min.Y),maxY=Terrain.ToCell(box.Max.Y);
            if((maxX-minX+1)*(maxZ-minZ+1)*(maxY-minY+1)>256)return false;
            int checkedBoxes=0;
            for(int x=minX;x<=maxX;x++)for(int z=minZ;z<=maxZ;z++)
            {
                if(!VoidPortalSafety.Ready(terrain,x,z)){reason="前方区块尚未加载";return false;}
                if(!VoidPortalSafety.Unprotected(x,z)){reason="前方领地禁止折跃";return false;}
                for(int y=minY;y<=maxY;y++)
                {
                    int value=terrain.Terrain.GetCellValue(x,y,z);Block block=BlocksManager.Blocks[Terrain.ExtractContents(value)];
                    if(block==null)return false;
                    if(block is MagmaBlock){reason="落点存在岩浆危险";return false;}
                    if(!block.IsCollidable)continue;
                    var boxes=block.GetCustomCollisionBoxes(terrain,value);
                    if(boxes==null||boxes.Length>256)return false;
                    foreach(var collision in boxes)
                    {
                        if(++checkedBoxes>512)return false;
                        var offset=new Vector3(x,y,z);
                        if(box.Intersection(new BoundingBox(collision.Min+offset,collision.Max+offset)))return false;
                    }
                }
            }
            var moving=terrain.Project.FindSubsystem<SubsystemMovingBlocks>(true);
            if(moving.MovingBlockSets.Count>256){reason="附近移动结构过多";return false;}
            foreach(var set in moving.MovingBlockSets)if(box.Intersection(set.BoundingBox(true)))return false;
            obstacles.Clear();p.ComponentBody.FindBodiesCollisionBoxes(box.Center(),obstacles);
            if(obstacles.Count>256){reason="附近碰撞实体过多";return false;}
            for(int i=0;i<obstacles.Count;i++)if(box.Intersection(obstacles.Array[i].Box))return false;
            return true;
        }
        private bool Segment(ComponentPlayer p,Vector3 a,Vector3 b,out string reason)
        {
            var first=Box(p,a);var second=Box(p,b);
            return Clear(p,new BoundingBox(Vector3.Min(first.Min,second.Min),Vector3.Max(first.Max,second.Max)),out reason);
        }
        internal bool TryFind(ComponentPlayer p,Vector3 direction,out Vector3 destination,out string reason)
        {
            destination=p.ComponentBody.Position;reason="无效的折跃方向";
            if(!WorldCutDamage.Finite(direction)||direction.LengthSquared()<.9f||direction.LengthSquared()>1.1f)return false;
            direction=Vector3.Normalize(direction);Vector3 origin=destination;
            if(!Fits(p,origin,out reason))return false;
            for(int i=1;i<=72;i++)
            {
                Vector3 next=origin+direction*(i*.25f);
                if(Segment(p,destination,next,out reason)){destination=next;continue;}
                Vector3 low=destination,high=next;
                for(int j=0;j<6;j++){Vector3 mid=(low+high)*.5f;string unused;if(Segment(p,low,mid,out unused))low=mid;else high=mid;}
                destination=low;return Vector3.DistanceSquared(origin,destination)>=.25f;
            }
            reason="无界折跃";return true;
        }
        internal bool PathClear(ComponentPlayer p,Vector3 from,Vector3 to,out string reason)
        {
            reason="折跃路径已变化";float distance=Vector3.Distance(from,to);
            if(!WorldCutDamage.Finite(from)||!WorldCutDamage.Finite(to)||!WorldCutRules.Finite(distance)||distance>18.01f||!Fits(p,from,out reason))return false;
            int count=Math.Max(1,(int)Math.Ceiling(distance/.25));Vector3 previous=from;
            for(int i=1;i<=count;i++){Vector3 at=Vector3.Lerp(from,to,i/(float)count);if(!Segment(p,previous,at,out reason))return false;previous=at;}
            return true;
        }
        internal static void SetPosition(ComponentPlayer p,Vector3 at)
        {
            var body=p.ComponentBody;body.Position=at;body.Velocity=Vector3.Zero;
            body.netPosition=new NetPosition(at);body.netVelocity=new NetVelocity(Vector3.Zero);
            body.SendPosition=at;body.SendVelocity=Vector3.Zero;
            body.StandingOnValue=null;body.StandingOnBody=null;body.StandingOnVelocity=Vector3.Zero;body.CollisionVelocityChange=Vector3.Zero;
        }
    }
}

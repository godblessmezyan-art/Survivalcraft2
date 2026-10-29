using System;
using Engine;
using Game;
namespace SCIEPort.Features.AbyssReaper
{
    // Strictly read-only checks. Every cell is required to be loaded and outside native territory.
    internal static class VoidPortalSafety
    {
        internal const int MaxCollisionBoxes=256;
        internal static bool Ready(SubsystemTerrain terrain,int x,int z)
        {
            var chunk=terrain.Terrain.GetChunkAtCell(x,z);
            return chunk!=null && chunk.State>=TerrainChunkState.Valid;
        }
        internal static bool Unprotected(int x,int z)
        {
            Territoriy territory;
            return !SubsystemBedrockBlockBehavior.CheckIsInTerritoriy(x,z,out territory)
                && !SubsystemBedrockBlockBehavior.CheckIsInTerritoriyBorder(x,z,out territory);
        }
        private static bool Empty(SubsystemTerrain terrain,int x,int y,int z)
            => y>=1 && y<=254 && Ready(terrain,x,z) && Unprotected(x,z)
               && Terrain.ExtractContents(terrain.Terrain.GetCellValue(x,y,z))==0;
        private static bool StableFloor(SubsystemTerrain terrain,int x,int y,int z)
        {
            if(y<0 || y>253 || !Ready(terrain,x,z) || !Unprotected(x,z))return false;
            int value=terrain.Terrain.GetCellValue(x,y,z);int id=Terrain.ExtractContents(value);
            if(id==0)return false;Block block=BlocksManager.Blocks[id];
            if(block==null || !block.IsCollidable || block.HasCollisionBehavior || block is FluidBlock)return false;
            var boxes=block.GetCustomCollisionBoxes(terrain,value);
            if(boxes==null || boxes.Length>MaxCollisionBoxes)return false;
            foreach(var b in boxes)
                if(b.Min.X<=0f && b.Min.Z<=0f && b.Max.X>=1f && b.Max.Z>=1f && b.Max.Y>=.999f)return true;
            return false;
        }
        // Candidate is feet at a cell center. Full unbroken floor and air only, never liquids/hazard blocks.
        internal static bool SafeLanding(SubsystemTerrain terrain,ComponentPlayer player,Vector3 candidate)
        {
            if(terrain==null || player==null || !WorldCutDamage.Finite(candidate) || !player.IsAddedToProject)return false;
            Vector3 size=player.ComponentBody.BoxSize;
            if(!WorldCutDamage.Finite(size) || size.X<=0 || size.Z<=0 || size.Y<=0 || size.X>2 || size.Z>2 || size.Y>3)return false;
            int y=Terrain.ToCell(candidate.Y);
            if(Math.Abs(candidate.Y-y)>.001f || y<2 || y+size.Y>=255)return false;
            var box=new BoundingBox(candidate-new Vector3(size.X*.5f,0,size.Z*.5f),candidate+new Vector3(size.X*.5f,size.Y,size.Z*.5f));
            int minX=Terrain.ToCell(box.Min.X+.001f),maxX=Terrain.ToCell(box.Max.X-.001f);
            int minZ=Terrain.ToCell(box.Min.Z+.001f),maxZ=Terrain.ToCell(box.Max.Z-.001f);
            int maxY=Terrain.ToCell(box.Max.Y-.001f);
            for(int x=minX;x<=maxX;x++)for(int z=minZ;z<=maxZ;z++)
            {
                if(!StableFloor(terrain,x,y-1,z))return false;
                for(int cy=y;cy<=maxY;cy++)if(!Empty(terrain,x,cy,z))return false;
            }
            box.Min+=new Vector3(.001f);box.Max-=new Vector3(.001f);
            return ClearOfDynamic(player,candidate,box);
        }
        private static bool ClearOfDynamic(ComponentPlayer player,Vector3 center,BoundingBox box)
        {
            var moving=player.Project.FindSubsystem<SubsystemMovingBlocks>(true);
            var bodies=player.Project.FindSubsystem<SubsystemBodies>(true);
            if(moving.MovingBlockSets.Count>MaxCollisionBoxes || bodies.Bodies.Count>512)return false;
            foreach(var set in moving.MovingBlockSets)if(box.Intersection(set.BoundingBox(true)))return false;
            var obstacles=new DynamicArray<ComponentBody.CollisionBox>();
            player.ComponentBody.FindBodiesCollisionBoxes(center,obstacles);
            if(obstacles.Count>MaxCollisionBoxes)return false;
            for(int i=0;i<obstacles.Count;i++)if(box.Intersection(obstacles.Array[i].Box))return false;
            return true;
        }
        // A cardinal portal occupies 3 by 4 air cells and stands on three full floor blocks.
        internal static bool SafePortalSite(SubsystemTerrain terrain,ComponentPlayer player,int centerX,int bottomY,int centerZ,bool alongX)
        {
            if(terrain==null || player==null || !player.IsAddedToProject || bottomY<2 || bottomY+4>255)return false;
            for(int side=-1;side<=1;side++)
            {
                int x=centerX+(alongX?side:0),z=centerZ+(alongX?0:side);
                if(!StableFloor(terrain,x,bottomY-1,z))return false;
                for(int y=bottomY;y<bottomY+4;y++)if(!Empty(terrain,x,y,z))return false;
            }
            var center=new Vector3(centerX+.5f,bottomY,centerZ+.5f);
            var extent=alongX?new Vector3(1.5f,4f,.5f):new Vector3(.5f,4f,1.5f);
            var box=new BoundingBox(center-new Vector3(extent.X,0,extent.Z),center+extent);
            if(box.Intersection(player.ComponentBody.BoundingBox))return false;
            return ClearOfDynamic(player,center,box);
        }
    }
}

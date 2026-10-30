using System.Collections.Generic;
using BuilderAssistant.Core.Blueprints;
using BuilderAssistant.Core.Furniture;

namespace BuilderAssistant.Core.Building
{
    /// <summary>Conservative two-cell-high circulation proof over the actual composed blueprint.</summary>
    internal static class HomeCirculationValidator
    {
        public static BuildingValidationResult Validate(Blueprint bp,FurnishedHomeLayout layout)
        {
            var problems=new List<string>();
            var occupied=new HashSet<CellPos>();
            var floor=new HashSet<CellPos>();
            foreach(BlueprintCell c in bp.Cells)
            {
                occupied.Add(c.Relative);
                if(c.Relative.Y==0 && c.Family==BlockFamily.Cube)floor.Add(c.Relative);
                if(c.Family==BlockFamily.Door)occupied.Add(c.Relative.Add(new CellPos(0,1,0)));
            }
            // Reserve the entire furniture box, including the hollow cells: no path through a chair.
            var reservations=new HashSet<CellPos>();
            foreach(FurniturePlacement p in bp.FurniturePlacements)
            {
                FurnitureTemplate t;
                if(!FurnitureTemplateRegistry.Default.TryGet(p.TemplateId,out t)) { problems.Add("未知家具："+p.TemplateId);continue; }
                int sx=(p.Rotation&1)==0?t.SizeX:t.SizeZ, sz=(p.Rotation&1)==0?t.SizeZ:t.SizeX;
                for(int x=0;x<sx;x++)for(int z=0;z<sz;z++)for(int y=0;y<t.SizeY;y++)
                {
                    CellPos pos=p.Position.Add(new CellPos(x,y,z));
                    if(!reservations.Add(pos))problems.Add("家具净空重叠："+pos);
                    occupied.Add(pos);
                }
            }
            // Detect overlaps even when a caller supplied a manually assembled invalid Blueprint.
            foreach(BlueprintCell c in bp.StructureCells)
                if(reservations.Contains(c.Relative))problems.Add("家具与结构重叠："+c.Relative);

            var reachable=new HashSet<CellPos>();var queue=new Queue<CellPos>();
            if(Walkable(layout.Entrance,layout,occupied,floor)) { reachable.Add(layout.Entrance);queue.Enqueue(layout.Entrance); }
            else problems.Add("入口内侧缺少两格净空或地板。");
            var steps=new[] { new CellPos(1,0,0),new CellPos(-1,0,0),new CellPos(0,0,1),new CellPos(0,0,-1) };
            while(queue.Count>0)
            {
                CellPos p=queue.Dequeue();
                foreach(CellPos step in steps)
                {
                    CellPos next=p.Add(step);
                    if(!reachable.Contains(next) && Walkable(next,layout,occupied,floor)) { reachable.Add(next);queue.Enqueue(next); }
                }
            }
            if(!reachable.Contains(layout.LivingTarget))problems.Add("入口无法通往客厅。");
            if(!reachable.Contains(layout.BedroomTarget))problems.Add("入口无法通往卧室。");
            if(bp.FurniturePlacements.Count!=layout.Furniture.Count)problems.Add("家具配置数量与布局不一致。");
            for(int i=0;i<System.Math.Min(bp.FurniturePlacements.Count,layout.Furniture.Count);i++)
            {
                FurniturePlacement actual=bp.FurniturePlacements[i], expected=layout.Furniture[i];
                if(actual.TemplateId!=expected.TemplateId || actual.Position!=expected.Position || actual.Rotation!=expected.Rotation || actual.MirrorX!=expected.MirrorX)
                    problems.Add("家具配置与房间方案不一致："+expected.TemplateId);
            }
            for(int i=0;i<layout.Access.Count;i++)
                if(!reachable.Contains(layout.Access[i]))problems.Add("家具旁无法通行："+layout.Furniture[i].TemplateId+" "+layout.Access[i]);
            // The partition must really divide two rooms, with one two-high doorway.
            for(int x=1;x<layout.Width-1;x++)
                if(x!=layout.Entrance.X && (!occupied.Contains(new CellPos(x,1,layout.PartitionZ)) ||
                    !occupied.Contains(new CellPos(x,2,layout.PartitionZ))))problems.Add("房间隔墙存在缺口。");
            return new BuildingValidationResult(problems);
        }

        private static bool Walkable(CellPos p,FurnishedHomeLayout l,HashSet<CellPos> occupied,HashSet<CellPos> floor)
        {
            return p.X>0 && p.X<l.Width-1 && p.Z>0 && p.Z<l.Length-1 && floor.Contains(p.Add(new CellPos(0,-1,0))) &&
                !occupied.Contains(p) && !occupied.Contains(p.Add(new CellPos(0,1,0)));
        }
    }
}

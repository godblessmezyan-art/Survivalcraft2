using System;
using System.Collections.Generic;
using System.Linq;
using Engine;
using Game;
using SCIEPort.Features.Blueprints;
using SCIEPort.Features.Blueprints.Core;
using SCIEPort.Features.Blueprints.Core.Blueprints;

internal static partial class BlueprintTests
{
    private static void HomeBlueprintTests()
    {
        var saved=(Block[])BlocksManager.Blocks.Clone();int states=0;
        try
        {
            var cells=new List<BlueprintCell>();
            foreach(string name in HomeBlueprintRules.Types)
            {
                int id=HomeBlueprintRules.Id(name);
                Check(!SCIEPort.Features.Home.HomeRules.IsTall(id)&&!SCIEPort.Features.Home.HomeRules.IsStorage(id)&&!SCIEPort.Features.Home.HomeRules.HasFootprint(id),"authorable furniture has no paired cells or storage entity");
                var type=typeof(HomeBlueprintRules).Assembly.GetType("SCIEPort.Features.Home."+name,true);
                var block=(Block)Activator.CreateInstance(type);block.BlockIndex=id;BlocksManager.Blocks[id]=block;
                VanillaBlockCapability cap;Check(VanillaBlockCapabilities.TryGetByTypeName(name,out cap),"single-cell furniture catalog");
                Check(cap.Family==BlockFamily.Home,"furniture has its own DATA codec");
                for(int data=-1;data<140;data++)
                {
                    Check(cap.IsValidData(data)==HomeBlueprintRules.Valid(name,data),"strict furniture DATA and module boundary");
                    if(!cap.IsValidData(data))continue;
                    states++;cells.Add(new BlueprintCell(new CellPos(states%20,states/20,0),name,BlockFamily.Home,data));
                    int rotated=data;for(int i=0;i<4;i++)rotated=BlockOrientation.Rotate(BlockFamily.Home,rotated,1);
                    Check(rotated==data,"four furniture rotations preserve exact native DATA");
                    int mirrored=BlockOrientation.MirrorX(BlockFamily.Home,data);
                    Check(cap.IsValidData(mirrored)&&BlockOrientation.MirrorX(BlockFamily.Home,mirrored)==data,"canonical furniture facing double mirror");
                }
            }
            VanillaBlockRegistry.Reset();var palette=new VanillaBlockPalette();palette.Reset();
            foreach(string name in HomeBlueprintRules.Types)Check(palette.IsResolvableKey(name),"exact furniture runtime type/id registry activation");
            foreach(string name in new[]{"BookcaseBlock","TallBookshelfBlock","HomeCabinetBlock","HomeUpperBlock","BookcasePartBlock"})
                Check(!palette.IsResolvableKey(name),"paired/hidden/storage furniture remains rejected");
            var bp=SCIEPort.Features.Blueprints.Core.Blueprints.Blueprint.Normalize("家具适配",3,cells);
            foreach(bool mirror in new[]{false,true})for(int r=0;r<4;r++)
            {
                var parsed=BlueprintImportService.FromString(BlueprintJson.Serialize(bp.Reorient(r,mirror)),palette.IsResolvableKey);
                Check(parsed.Success&&parsed.BlockCount==states,"all furniture states round trip and orient");
            }
            var requests=new List<int>();
            foreach(var c in cells){requests.Add(Terrain.MakeBlockValue(HomeBlueprintRules.Id(c.MaterialKey),0,c.Data));requests.Add(0);}
            NativeDirectTests(requests.ToArray());
            Console.WriteLine("Home blueprint codec: "+states+" single-cell states, exact runtime identities, negative multi-cell/storage cases passed.");
        }
        finally {for(int i=0;i<saved.Length;i++)BlocksManager.Blocks[i]=saved[i];VanillaBlockRegistry.Reset();}
    }

    private static void CompactFacingAudit(Blueprint bp)
    {
        var moon=bp.Cells.Where(c=>c.MaterialKey=="AncientWindowBlock"&&(c.Data>>3)>=2).ToArray();
        var panes=bp.Cells.Where(c=>c.MaterialKey=="GlassBlock").ToArray();
        Check(moon.Length==4&&panes.Length>0,"compact tea moon and real glass exist");
        int front=panes.Min(c=>c.Relative.Z);
        float teaX=(float)moon.Average(c=>c.Relative.X+.5),mainX=(float)panes.Where(c=>c.Relative.Z==front).Average(c=>c.Relative.X+.5);
        var eye=new Vector3(bp.SizeX/2f,5,-50);var view=Matrix.CreateLookAt(eye,new Vector3(eye.X,5,0),Vector3.UnitY);
        float teaScreen=Vector3.Transform(new Vector3(teaX,5,front),view).X;
        float mainScreen=Vector3.Transform(new Vector3(mainX,5,front),view).X;
        float platformScreen=Vector3.Transform(new Vector3(.5f,5,front),view).X;
        Check(teaScreen<mainScreen&&mainScreen<platformScreen,"NATIVE ENGINE CAMERA: tea on screen-left, main center, side platform on screen-right");
        Check(!bp.Cells.Any(c=>c.Family==BlockFamily.Door&&c.Relative.Z==front),"main glass front contains no door");
        bool loft=bp.Name.Contains("阁楼版");
        Check(bp.Cells.Count(c=>c.Family==BlockFamily.Door)==(loft?3:2),"shared/tea doors and optional right platform door");
        if(loft)
        {
            Check(bp.SizeX==20&&bp.SizeY==16&&bp.SizeZ==15,"loft preserves approved V2 envelope");
            bool viewLoft=bp.Name.Contains("观景阁楼版");
            if(viewLoft)
            {
                var sideGlass=bp.Cells.Where(c=>c.Relative.X==3&&(c.MaterialKey=="GlassBlock"||(c.MaterialKey=="AncientWindowBlock"&&(c.Data>>3)==0))).ToArray();
                Check(sideGlass.Length==4&&sideGlass.All(c=>c.Relative.Y>=5&&c.Relative.Y<=6&&c.Relative.Z>=5&&c.Relative.Z<=6),"small 2x2 right view window, separate from platform door");
                foreach(var pane in sideGlass)
                    Check(!bp.Cells.Any(c=>c.Relative==new CellPos(4,pane.Relative.Y,pane.Relative.Z)),"right view window not blocked by shelves inside");
                foreach(int x in new[]{9,10})foreach(int z in new[]{8,9})
                {
                    Check(bp.Cells.Any(c=>c.Relative==new CellPos(x,8,z)&&(c.MaterialKey=="PlanksBlock"||c.Family==BlockFamily.Stairs)),"2x2 stair-top turn has supported floor");
                    foreach(int y in new[]{9,10})Check(!bp.Cells.Any(c=>c.Relative==new CellPos(x,y,z)),"loft turn retains two full clear cells");
                }
                foreach(int z in new[]{9,10})
                    Check(bp.Cells.Any(c=>c.Relative==new CellPos(11,9,z)&&c.MaterialKey=="AncientTimberBlock"&&(c.Data>>3)==4),"widened loft edge retains guardrail");
                Check(bp.Cells.Count(c=>c.MaterialKey=="BookshelfBlock"&&c.Relative.X==4&&c.Relative.Y>=4&&c.Relative.Y<=6&&new[]{4,9,10}.Contains(c.Relative.Z))==9,"right shelves retained beside view window and behind door");
                Check(bp.Cells.Any(c=>c.Relative==new CellPos(19,8,5)&&c.MaterialKey=="AncientRoofBlock"),"tea eaves use thin actual tile geometry");
                Check(bp.Cells.Any(c=>c.Relative==new CellPos(8,14,1)&&c.MaterialKey=="WoodenSlabBlock"&&(c.Data&1)==1),"roof leading edge has half-thickness backing");
            }
            else Check(!bp.Cells.Any(c=>c.Relative.X==3&&c.Relative.Z>=4&&c.Relative.Z<=10&&(c.MaterialKey=="GlassBlock"||(c.MaterialKey=="AncientWindowBlock"&&(c.Data>>3)==0))),"V3 right wall has no glass");
            Check(bp.Cells.Any(c=>c.Family==BlockFamily.Door&&c.Relative==new CellPos(3,4,8)),"right door opens onto side platform");
            var stairs=bp.Cells.Where(c=>c.Family==BlockFamily.Stairs).ToArray();
            Check(stairs.Length==10&&stairs.All(c=>c.Relative.X>=10&&c.Relative.X<=11&&c.Relative.Y>=4&&c.Relative.Y<=8&&c.Relative.Z>=4&&c.Relative.Z<=8),"five-rise two-wide staircase remains inside main room");
            Check(bp.Cells.Any(c=>c.MaterialKey=="PlanksBlock"&&c.Relative==new CellPos(9,8,8)),"usable rear loft landing");
        }
        else Check(!bp.Cells.Any(c=>c.Family==BlockFamily.Stairs),"no exterior stairs in V2 blueprint");
        Check(bp.Cells.Count(c=>c.Family==BlockFamily.Home)>0,"actual custom furniture authored");
        Console.WriteLine("Native Engine camera facing audit PASS: tea="+teaScreen+", main="+mainScreen+", right-platform="+platformScreen+" (screen X, ascending left to right). Main front glazed/no door; "+(loft?"right wall/door, loft and internal stairs verified.":"no stairs."));
    }
}

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
        Check(bp.Cells.Count(c=>c.Family==BlockFamily.Door)==2,"shared-wall door plus tea-side entrance only");
        Check(!bp.Cells.Any(c=>c.Family==BlockFamily.Stairs),"no stairs in building-only blueprint");
        Check(bp.Cells.Count(c=>c.Family==BlockFamily.Home)>0,"actual custom furniture authored");
        Console.WriteLine("Native Engine camera facing audit PASS: tea="+teaScreen+", main="+mainScreen+", right-platform="+platformScreen+" (screen X, ascending left to right). Main front glazed/no door; no stairs.");
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Game;
using Game.NetWork;
using SCIEPort.Features.Architecture;
using SCIEPort.Features.Blueprints;
using SCIEPort.Features.Blueprints.Core;
using SCIEPort.Features.Blueprints.Core.Blueprints;
using SCIEPort.Features.Blueprints.Core.BuildEngine;

internal static partial class BlueprintTests
{
    internal static void ValidateBlueprintFile(string path)
    {
        var oldMode=CommonLib.WorkType;var oldNet=CommonLib.Net;var oldTerritories=SubsystemBedrockBlockBehavior.Territories;
        var oldBlocks=(Block[])BlocksManager.Blocks.Clone();int builds=0,maxWrites=0,supports=0;
        try
        {
            SubsystemBedrockBlockBehavior.Territories=new Dictionary<Guid,Territoriy>();
            BlocksManager.Blocks[0]=new AirBlock{BlockIndex=0};
            foreach(var cap in VanillaBlockCapabilities.All)
            {
                Type type=cap.Family==BlockFamily.Detail?typeof(ArchitectureBlock).Assembly.GetType("SCIEPort.Features.Architecture."+cap.TypeName,true):typeof(Block).Assembly.GetType("Game."+cap.TypeName,true);
                int id=(int)type.GetField("Index",BindingFlags.Public|BindingFlags.Static|BindingFlags.FlattenHierarchy).GetRawConstantValue();
                var block=(Block)Activator.CreateInstance(type);block.BlockIndex=id;BlocksManager.Blocks[id]=block;
            }
            var palette=new VanillaBlockPalette();palette.Reset();
            var parsed=BlueprintImportService.FromString(File.ReadAllText(path),palette.IsResolvableKey);
            Check(parsed.Success,parsed.Detail??"actual blueprint import");
            var bp=parsed.Blueprint;var original=BlueprintJson.Serialize(bp);
            var byPosition=new Dictionary<CellPos,BlueprintCell>();foreach(var cell in bp.Cells)byPosition.Add(cell.Relative,cell);
            foreach(var cell in bp.Cells)
            {
                CellPos support;int face;
                if(!AttachmentRules.TryGetSupport(cell.Relative,cell.Family,cell.Data,out support,out face))continue;
                BlueprintCell below;Check(byPosition.TryGetValue(support,out below),"standalone blueprint supplies native attachment support");
                int value;string error;Check(palette.TryResolve(below.MaterialKey,below.Family,below.Data,out value,out error),error??"support resolves");
                Check(VanillaSupportRules.Supports(BlocksManager.Blocks[Terrain.ExtractContents(value)],null,value,cell.Family,face),"actual native support predicate");supports++;
            }
            foreach(var role in new[]{WorkType.Local,WorkType.Server,WorkType.Client})
            {
                for(int rotation=0;rotation<4;rotation++)foreach(bool mirror in new[]{false,true})
                {
                    var oriented=bp.Reorient(rotation,mirror);var reload=BlueprintImportService.FromString(BlueprintJson.Serialize(oriented),palette.IsResolvableKey);
                    Check(reload.Success&&reload.Blueprint.CellCount==bp.CellCount,"all orientations save and reload without lost cells");
                    using(var f=new Fixture())
                    {
                        CommonLib.WorkType=role;var player=f.Player(1);
                        Check(f.Start(player,reload.Blueprint),"whole-house plan and start in "+role);f.Drain();
                        Check(f.Terrain.Occupied==bp.CellCount,"whole-house cell count matches final JSON");
                        maxWrites=Math.Max(maxWrites,f.Terrain.MaxFrameWrites);
                        Check(f.Terrain.MaxFrameWrites<=32,"whole-house per-frame construction budget");
                        f.Host.UndoDirect(player);f.Drain();Check(f.Terrain.Occupied==0,"whole-house undo restores empty terrain");builds++;
                    }
                }
                using(var f=new Fixture())
                {
                    CommonLib.WorkType=role;var player=f.Player(1);
                    Check(f.Start(player,bp),"whole-house cancellable construction starts");f.Tick();f.Tick();f.Tick();
                    f.Host.CancelDirect(player);f.Drain();Check(f.Terrain.Occupied==0,"whole-house cancellation rollback");
                    Check(f.Start(player,bp),"whole-house failure construction starts");f.Terrain.FailAt=f.Terrain.Writes+9;f.Drain();Check(f.Terrain.Occupied==0,"whole-house partial-write failure rollback");
                    CellPos occupied=bp.Cells[0].Relative.Add(new CellPos(0,64,0));f.Terrain.Cells[occupied]=3;int before=f.Terrain.Writes;
                    Check(!f.Start(player,bp)&&f.Terrain.Writes==before,"default non-air protection makes zero writes");
                    Check(f.Start(player,bp,0,true),"explicit region clear can build");f.Drain();f.Host.UndoDirect(player);f.Drain();
                    Check(f.Terrain.Occupied==1&&f.Terrain.Cells[occupied]==3,"clear and construction undo restores original block");
                    var plan=BuildPlanner.Plan(bp,new CellPos(0,250,0),f.Host.Palette,f.Terrain,false,false);Check(!plan.Success,"invalid world height rejected");
                }
            }
            var cycled=bp;for(int i=0;i<4;i++)cycled=cycled.Reorient(1,false);
            Check(BlueprintJson.Serialize(cycled)==original,"four rotations return exact serialized structure");
            Check(BlueprintJson.Serialize(bp.Reorient(0,true).Reorient(0,true))==original,"double mirror returns exact serialized structure");
            Console.WriteLine("Whole-house actual importer/planner/transactions PASS: cells="+bp.CellCount+"; size="+bp.SizeX+"x"+bp.SizeY+"x"+bp.SizeZ+"; oriented builds="+builds+"; native supports="+supports+"; max writes/frame="+maxWrites+"; cancellation/failure/no-overwrite/clear undo/height bounds passed. In-memory terrain, no Android/live room.");
        }
        finally
        {
            for(int i=0;i<oldBlocks.Length;i++)BlocksManager.Blocks[i]=oldBlocks[i];
            VanillaBlockRegistry.Reset();CommonLib.WorkType=oldMode;CommonLib.Net=oldNet;SubsystemBedrockBlockBehavior.Territories=oldTerritories;
        }
    }
}

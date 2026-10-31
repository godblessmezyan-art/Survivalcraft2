using System;
using System.Collections.Generic;
using BuilderAssistant.Core;
using BuilderAssistant.Core.Blueprints;
using BuilderAssistant.Core.BuildEngine;
using BuilderAssistant.Core.Transactions;

namespace BuilderAssistant.Tests
{
    internal sealed class MaterialResolver : IBlockValueResolver
    {
        public readonly Dictionary<string,int> Codes=new Dictionary<string,int>();
        public readonly Dictionary<int,string> Types=new Dictionary<int,string>();
        public MaterialResolver() { foreach(var c in VanillaBlockCapabilities.All) { int n=Codes.Count+1;Codes.Add(c.TypeName,n);Types.Add(n,c.TypeName); } }
        public bool TryResolve(string key,out int value) { return TryResolve(key,0,out value); }
        public bool TryResolve(string key,int data,out int value)
        {
            VanillaBlockCapability c;string type,error;
            if(!VanillaBlockCapabilities.TryGetByMaterialKey(key,out c,out type)) { value=0;return false; }
            return TryResolve(key,c.Family,data,out value,out error);
        }
        public bool TryResolve(string key,BlockFamily family,int data,out int value,out string error)
        {
            VanillaBlockCapability c;string type;value=0;error="unsupported state";
            if(!VanillaBlockCapabilities.TryGetByMaterialKey(key,out c,out type) || c.Family!=family || !c.IsValidData(data))return false;
            value=Codes[type] | (data<<14);error=null;return true;
        }
    }
    internal sealed class MaterialTerrain : ITerrainGateway,IConstructionTerrainGateway
    {
        public readonly FakeTerrain Grid=new FakeTerrain();
        public readonly MaterialResolver Resolver=new MaterialResolver();
        public readonly List<CellPos> WriteOrder=new List<CellPos>();
        public bool IsCellEditable(CellPos p) { return Grid.IsCellEditable(p); }
        public CellEditability InspectCell(CellPos p) { return Grid.InspectCell(p); }
        public bool IsCellAir(CellPos p) { return Grid.ValueAt(p)==0; }
        public bool IsCellSoftObstruction(CellPos p) { return false; }
        public bool TryGetCellValue(CellPos p,out int value) { return Grid.TryGetCellValue(p,out value); }
        public bool SetCellValue(CellPos p,int value) { WriteOrder.Add(p);return Grid.SetCellValue(p,value); }
        public bool ValuesEquivalent(int a,int b) { return a==b; }
        public bool SupportsAttachment(int value,BlockFamily family,int face)
        {
            string type;if(!Resolver.Types.TryGetValue(value&1023,out type))return false;
            bool opaque=type=="PlanksBlock"||type=="DirtBlock"||type=="GrassBlock"||type=="SandBlock"||type=="GravelBlock"||type=="StoneBrickBlock";
            if(family==BlockFamily.Plant)return type=="DirtBlock"||type=="GrassBlock"||type=="SandBlock"||type=="SoilBlock";
            if(family==BlockFamily.Falling)return opaque||type=="SoilBlock";
            if(family==BlockFamily.Snow)return opaque||type=="SpruceLeavesBlock";
            if(family==BlockFamily.Lantern)return opaque||type=="WoodenFenceBlock";
            return opaque;
        }
        public bool ApplyWithJournal(CellPos p,int value,out List<BlockUndoRecord> journal,out string error)
        {
            int old;TryGetCellValue(p,out old);journal=new List<BlockUndoRecord>();error=null;
            if(!SetCellValue(p,value))return false;
            journal.Add(new BlockUndoRecord(p,old,value));return true;
        }
    }
    internal static partial class Program
    {
        private static void TestExpandedMaterials()
        {
            Console.WriteLine("[Expanded terrain/decorative materials and support lifecycle]");
            string[] added={"DirtBlock","IceBlock","SandBlock","GravelBlock","GrassBlock","SoilBlock","SnowBlock","TallGrassBlock","RedFlowerBlock","PurpleFlowerBlock","WhiteFlowerBlock","DryBushBlock","LargeDryBushBlock","FallenLeavesBlock","CarpetBlock","GravestoneBlock","JackOLanternBlock","CairnBlock"};
            var origin=new CellPos(10,40,10);
            var example=BlueprintImportService.FromString(System.IO.File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory,"blueprint-materials-v3.json")),null);
            var showcase=new MaterialTerrain();
            Check(example.Success && BuildPlanner.Plan(example.Blueprint,origin,showcase.Resolver,showcase,false).Success,"published all-material showcase imports and plans with actual support rules");
            foreach(string type in added)
            {
                VanillaBlockCapability c;Check(VanillaBlockCapabilities.TryGetByTypeName(type,out c),"material enabled "+type);
                var cells=new List<BlueprintCell> { new BlueprintCell(new CellPos(0,0,0),"DirtBlock"),new BlueprintCell(new CellPos(0,1,0),type,c.Family,0) };
                var bp=Blueprint.Normalize(type,3,cells);var terrain=new MaterialTerrain();
                var plan=BuildPlanner.Plan(bp,origin,terrain.Resolver,terrain,false);
                Check(plan.Success && terrain.Grid.Writes==0,"supported placement preflight is read-only "+type);
                if(!plan.Success)continue;
                var task=new BuildTask(type,origin,plan.Changes,false);while(!task.IsFinished)task.Advance(terrain,32);
                Check(task.State==BuildTaskState.Completed,"material executes and verifies "+type);
                task.Undo(terrain);while(task.PendingUndoCount>0)task.RetryUndo(terrain);
                Check(terrain.Grid.ValueAt(origin)==0 && terrain.Grid.ValueAt(origin.Add(new CellPos(0,1,0)))==0,"immediate terrain-only undo "+type);
                if(AttachmentRules.RequiresSupport(c.Family))
                {
                    var floating=Blueprint.Normalize("floating",3,new List<BlueprintCell> {new BlueprintCell(new CellPos(0,0,0),type,c.Family,0)});
                    Check(!BuildPlanner.Plan(floating,origin,terrain.Resolver,terrain,true).Success,"clear mode cannot bypass missing support "+type);
                }
            }
            VanillaBlockCapability grass,plant,soil,dry;
            VanillaBlockCapabilities.TryGetByTypeName("GrassBlock",out grass);VanillaBlockCapabilities.TryGetByTypeName("TallGrassBlock",out plant);
            VanillaBlockCapabilities.TryGetByTypeName("SoilBlock",out soil);VanillaBlockCapabilities.TryGetByTypeName("DryBushBlock",out dry);
            Check(grass.IsValidData(1)&&!grass.IsValidData(8)&&plant.IsValidData(8)&&!plant.IsValidData(1)&&!dry.IsValidData(1),"type-qualified natural DATA never aliases paint or a different species");
            Check(soil.IsValidData(7)&&!soil.IsValidData(8)&&!soil.CanUndoAllSubsystems,"soil supports exact moisture/fertility bits with truthful undo scope");
            var orderedCells=new List<BlueprintCell>();
            for(int y=63;y>0;y--)orderedCells.Add(new BlueprintCell(new CellPos(0,y,0),y%2==0?"SandBlock":"GravelBlock",BlockFamily.Falling,0));
            orderedCells.Add(new BlueprintCell(new CellPos(0,0,0),"PlanksBlock"));
            var stack=Blueprint.Normalize("gravity",3,orderedCells);var grid=new MaterialTerrain();
            var stackPlan=BuildPlanner.Plan(stack,origin,grid.Resolver,grid,false);
            Check(stackPlan.Success && stackPlan.Changes[0].Absolute.Y==origin.Y,"gravity columns are topologically ordered from foundation");
            var stackTask=new BuildTask("gravity",origin,stackPlan.Changes,false);bool bounded=true;
            while(!stackTask.IsFinished) { int before=grid.Grid.Writes;stackTask.Advance(grid,32);bounded &= grid.Grid.Writes-before<=32; }
            Check(stackTask.State==BuildTaskState.Completed&&bounded,"gravity stack respects 32-cell budget and postverification");
            stackTask.Undo(grid);while(stackTask.PendingUndoCount>0)stackTask.RetryUndo(grid);
            bool empty=true;foreach(var c in stack.Cells)empty &= grid.Grid.ValueAt(origin.Add(c.Relative))==0;
            Check(empty,"reverse dependency undo removes gravity column before its foundation");
            var floorSand=Blueprint.Normalize("floor",3,new List<BlueprintCell> {new BlueprintCell(new CellPos(0,0,0),"SandBlock",BlockFamily.Falling,0)});
            Check(BuildPlanner.Plan(floorSand,new CellPos(0,0,0),grid.Resolver,grid,false).Success,"native world floor Y=0 is implicit gravity support");

            var baseCells=new List<BlueprintCell> {new BlueprintCell(new CellPos(0,0,0),"PlanksBlock"),new BlueprintCell(new CellPos(0,1,0),"SandBlock",BlockFamily.Falling,0)};
            var two=Blueprint.Normalize("two",3,baseCells);var lost=new MaterialTerrain();
            var p2=BuildPlanner.Plan(two,origin,lost.Resolver,lost,false);var job=new BuildTask("two",origin,p2.Changes,false);
            job.Advance(lost,32);lost.Grid.Occupy(origin,0);job.Advance(lost,32);
            while(job.PendingRollbackCount>0)job.RetryRollback(lost);
            Check(job.State==BuildTaskState.Failed,"postverification detects support removed after construction");
            var plantOverStone=Blueprint.Normalize("badsoil",3,new List<BlueprintCell> {new BlueprintCell(new CellPos(0,0,0),"PlanksBlock"),new BlueprintCell(new CellPos(0,1,0),"RedFlowerBlock",BlockFamily.Plant,0)});
            Check(!BuildPlanner.Plan(plantOverStone,origin,lost.Resolver,lost,false).Success,"plants reject generic building blocks as soil");
            // Natural evolution counts as an external change, never overwrite it on undo.
            var changing=new MaterialTerrain();var changePlan=BuildPlanner.Plan(two,origin,changing.Resolver,changing,false);var changingTask=new BuildTask("changing",origin,changePlan.Changes,false);
            while(!changingTask.IsFinished)changingTask.Advance(changing,32);
            changing.Grid.Occupy(origin.Add(new CellPos(0,1,0)),999);
            changingTask.Undo(changing);while(changingTask.PendingUndoCount>0)changingTask.RetryUndo(changing);
            Check(changing.Grid.ValueAt(origin.Add(new CellPos(0,1,0)))==999,"undo preserves cells changed by nature or another player");
            foreach(string type in new[] {"ChestBlock","WaterBlock","MagmaBlock","FurnitureBlock","SmallGunpowderKegBlock","WoodenFenceGateBlock","WoodenTrapdoorBlock"})
            { VanillaBlockCapability ignored;Check(!VanillaBlockCapabilities.TryGetByTypeName(type,out ignored),"complex systems remain excluded "+type); }
        }
    }
}

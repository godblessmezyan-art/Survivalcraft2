using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BuilderAssistant.Core;
using BuilderAssistant.Core.Blueprints;
using BuilderAssistant.Core.Building;
using BuilderAssistant.Core.BuildEngine;
using BuilderAssistant.Core.Furniture;
using BuilderAssistant.Core.Workbench;
using BuilderAssistant.Core.Json;

namespace BuilderAssistant.Tests
{
    internal static partial class Program
    {
        private static void TestFurnishedHomes()
        {
            Console.WriteLine("[Furnished one-bedroom home / independent circulation QC]");
            foreach(int w in new[] {9,10,13,21})foreach(int l in new[] {13,14,17,25})
            foreach(int seed in new[] {0,1,-1,int.MinValue,int.MaxValue,27})
            foreach(RoofType roof in new[] {RoofType.Hip,RoofType.Gabled,RoofType.Flat})
            {
                var s=BuildingSpec.DefaultFurnishedHome();s.Width=w;s.Length=l;s.Seed=seed;s.Roof=roof;
                s.WallHeight=seed==0?6:4;s.Materials=seed<0?MaterialProfile.House():MaterialProfile.Cottage();
                var result=BuildingComposer.Compose(s,"home");
                Check(result.Success,"furnished home "+w+"x"+l+" seed "+seed+" roof "+roof+(result.Success?"":" "+result.Summarize()));
                if(!result.Success)continue;
                var decoded=BlueprintImportService.FromString(BlueprintJson.Serialize(result.Blueprint),null);
                Check(decoded.Success && decoded.Blueprint.FurniturePlacements.Count==7 && HomeCirculationValidator.Validate(decoded.Blueprint,result.HomeLayout).IsValid,"layout survives real schema3 import");
                var t=new FakeTerrain();
                Check(BuildPlanner.Plan(decoded.Blueprint,new CellPos(0,40,0),new DetailResolver(),t,false).Success && t.Writes==0,"generated furniture uses supported materials and plans read-only");
            }
            var spec=BuildingSpec.DefaultFurnishedHome();var home=BuildingComposer.Compose(spec,"home");
            foreach(int wallHeight in new[] {4,5,6})foreach(int windows in new[] {0,100})
            foreach(RoofType roof in new[] {RoofType.Hip,RoofType.Gabled,RoofType.Flat})
            {
                var variant=BuildingSpec.DefaultFurnishedHome();variant.WallHeight=wallHeight;variant.WindowDensity=windows;variant.Roof=roof;
                Check(BuildingComposer.Compose(variant,"variant").Success,"wall-height and window-density boundary combination");
            }
            Check(home.Success && home.HomeLayout!=null,"default furnished home generates");
            Check(BlueprintJson.Serialize(home.Blueprint)==BlueprintJson.Serialize(BuildingComposer.Compose(spec,"home").Blueprint),"same parameters produce identical JSON");
            string original=BlueprintJson.Serialize(home.Blueprint);spec.Seed=17;
            Check(original!=BlueprintJson.Serialize(BuildingComposer.Compose(spec,"home").Blueprint),"seed changes layout or window composition");
            foreach(int invalid in new[] {0,8,22,int.MaxValue})
            {
                var bad=BuildingSpec.DefaultFurnishedHome();bad.Width=invalid;
                Check(!BuildingComposer.Compose(bad,"bad").Success,"reject furnished width "+invalid);
            }
            foreach(int invalid in new[] {0,12,26,int.MaxValue})
            {
                var bad=BuildingSpec.DefaultFurnishedHome();bad.Length=invalid;
                Check(!BuildingComposer.Compose(bad,"bad").Success,"reject furnished length "+invalid);
            }
            foreach(int h in new[] {2,3,7}) { var bad=BuildingSpec.DefaultFurnishedHome();bad.WallHeight=h;Check(!BuildingComposer.Compose(bad,"bad").Success,"reject insufficient or unsupported wall height"); }
            var multi=BuildingSpec.DefaultFurnishedHome();multi.Floors=2;Check(!BuildingComposer.Compose(multi,"bad").Success,"furnished template refuses extra storeys");
            var chimney=BuildingSpec.DefaultFurnishedHome();chimney.HasChimney=true;Check(!BuildingComposer.Compose(chimney,"bad").Success,"chimney cannot silently intersect bedroom furniture");

            var blockedCells=new List<BlueprintCell>(home.Blueprint.Cells);
            blockedCells.Add(new BlueprintCell(home.HomeLayout.BedroomTarget,"PlanksBlock"));
            var blocked=new Blueprint("blocked",3,home.Blueprint.SizeX,home.Blueprint.SizeY,home.Blueprint.SizeZ,blockedCells,
                new List<BlueprintCell>(home.Blueprint.StructureCells),new List<FurniturePlacement>(home.Blueprint.FurniturePlacements));
            Check(!HomeCirculationValidator.Validate(blocked,home.HomeLayout).IsValid,"QC catches blocked bedroom doorway from actual cells");
            var headCells=new List<BlueprintCell>(home.Blueprint.Cells);
            headCells.Add(new BlueprintCell(home.HomeLayout.Access[0].Add(new CellPos(0,1,0)),"PlanksBlock"));
            var lowCeiling=new Blueprint("head",3,home.Blueprint.SizeX,home.Blueprint.SizeY,home.Blueprint.SizeZ,headCells,
                new List<BlueprintCell>(home.Blueprint.StructureCells),new List<FurniturePlacement>(home.Blueprint.FurniturePlacements));
            Check(!HomeCirculationValidator.Validate(lowCeiling,home.HomeLayout).IsValid,"QC catches head-height block at furniture approach");
            var missingFloor=new List<BlueprintCell>(home.Blueprint.Cells);missingFloor.RemoveAll(c=>c.Relative==home.HomeLayout.Access[0].Add(new CellPos(0,-1,0)));
            var hole=new Blueprint("hole",3,home.Blueprint.SizeX,home.Blueprint.SizeY,home.Blueprint.SizeZ,missingFloor,
                new List<BlueprintCell>(home.Blueprint.StructureCells),new List<FurniturePlacement>(home.Blueprint.FurniturePlacements));
            Check(!HomeCirculationValidator.Validate(hole,home.HomeLayout).IsValid,"QC catches unsupported approach floor");

            for(int r=0;r<4;r++)
            {
                var bp=home.Blueprint.Reorient(r,false);var terrain=new FakeTerrain();var origin=new CellPos(0,40,0);
                var plan=BuildPlanner.Plan(bp,origin,new FakeResolver(),terrain,false);
                Check(plan.Success,"furnished home rotates and plans "+r);
                var task=new BuildTask("home",origin,plan.Changes,false);bool bounded=true;
                while(!task.IsFinished) { int before=terrain.Writes;task.Advance(terrain,32);bounded &= terrain.Writes-before<=32; }
                Check(task.State==BuildTaskState.Completed && bounded,"furnished home completes in bounded batches");
                task.Undo(terrain);while(task.PendingUndoCount>0)task.RetryUndo(terrain);
                bool empty=true;foreach(var c in bp.Cells)empty &= terrain.ValueAt(origin.Add(c.Relative))==0;
                Check(empty,"furnished home structure and furniture undo together");
            }
            foreach(bool cancel in new[] {true,false})
            {
                var terrain=new FakeTerrain();var origin=new CellPos(0,40,0);
                var plan=BuildPlanner.Plan(home.Blueprint,origin,new FakeResolver(),terrain,false);
                var task=new BuildTask("interrupted home",origin,plan.Changes,false);
                task.Advance(terrain,32);
                CellPos obstacle=plan.Changes[32].Absolute;
                if(cancel)task.Cancel(terrain);
                else { terrain.Occupy(obstacle,99);task.Advance(terrain,32); }
                while(task.PendingRollbackCount>0)task.RetryRollback(terrain);
                bool restored=true;
                foreach(var c in home.Blueprint.Cells)
                {
                    CellPos p=origin.Add(c.Relative);
                    restored &= terrain.ValueAt(p)==(!cancel && p==obstacle?99:0);
                }
                Check(restored && task.State==(cancel?BuildTaskState.Cancelled:BuildTaskState.Failed),"new home cancellation/failure rolls back and preserves external edits: "+cancel);
            }
            var service=new BuildingSelectionService();var wb=new BuildingWorkbench(service);
            Check(wb.TryApplySpec("workbench_furnished",BuildingSpec.DefaultFurnishedHome(),0).Accepted && wb.Regenerate().Accepted,"workbench exposes furnished generation");
            for(int i=0;i<8;i++) { wb.CycleWidth();wb.CycleLength();wb.CycleWallHeight();wb.CycleFloors();Check(wb.Draft.Validate().IsValid,"legacy parameter cycling remains within furnished bounds"); }
        }

        private static int DumpFurnishedHomes(string directory)
        {
            Directory.CreateDirectory(directory);var manifest=JsonValue.NewArray();
            int[] widths={9,11,13},lengths={13,15,17},seeds={0,1,-27};
            for(int i=0;i<3;i++)
            {
                var spec=BuildingSpec.DefaultFurnishedHome();spec.Width=widths[i];spec.Length=lengths[i];spec.Seed=seeds[i];
                if(i==1)spec.Materials=MaterialProfile.House();if(i==2)spec.Roof=RoofType.Hip;
                var result=BuildingComposer.Compose(spec,"一室一厅 "+widths[i]+"×"+lengths[i]);
                if(!result.Success)throw new InvalidOperationException(result.Summarize());
                string stem="furnished-home-"+widths[i]+"x"+lengths[i];
                File.WriteAllText(Path.Combine(directory,stem+".json"),BlueprintJson.Serialize(result.Blueprint)+Environment.NewLine,new UTF8Encoding(false));
                var item=JsonValue.NewObject();item.Add("file",JsonValue.NewString(stem+".json"));
                item.Add("name",JsonValue.NewString(result.Blueprint.Name));item.Add("width",JsonValue.NewNumber(spec.Width));item.Add("length",JsonValue.NewNumber(spec.Length));
                item.Add("seed",JsonValue.NewNumber(spec.Seed));item.Add("partitionZ",JsonValue.NewNumber(result.HomeLayout.PartitionZ));
                item.Add("cellCount",JsonValue.NewNumber(result.CellCount));item.Add("wallHeight",JsonValue.NewNumber(spec.WallHeight));
                item.Add("roof",JsonValue.NewString(spec.Roof.ToString()));item.Add("material",JsonValue.NewString(i==1?"石砖":"木板"));
                var furniture=JsonValue.NewArray();
                for(int n=0;n<result.Blueprint.FurniturePlacements.Count;n++)
                {
                    var p=result.Blueprint.FurniturePlacements[n];FurnitureTemplate t;FurnitureTemplateRegistry.Default.TryGet(p.TemplateId,out t);
                    var entry=JsonValue.NewObject();entry.Add("name",JsonValue.NewString(t.Name));
                    entry.Add("x",JsonValue.NewNumber(p.Position.X));entry.Add("z",JsonValue.NewNumber(p.Position.Z));
                    entry.Add("width",JsonValue.NewNumber((p.Rotation&1)==0?t.SizeX:t.SizeZ));entry.Add("length",JsonValue.NewNumber((p.Rotation&1)==0?t.SizeZ:t.SizeX));
                    entry.Add("accessX",JsonValue.NewNumber(result.HomeLayout.Access[n].X));entry.Add("accessZ",JsonValue.NewNumber(result.HomeLayout.Access[n].Z));
                    furniture.Add(entry);
                }
                item.Add("furniture",furniture);manifest.Add(item);
                Console.WriteLine(stem+": "+result.Summarize());
            }
            File.WriteAllText(Path.Combine(directory,"layouts.json"),manifest.ToJson(),new UTF8Encoding(false));return 0;
        }
    }
}

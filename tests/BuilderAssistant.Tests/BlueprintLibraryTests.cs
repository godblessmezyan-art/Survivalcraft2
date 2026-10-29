using System;
using System.IO;
using System.Collections.Generic;
using BuilderAssistant.Core;
using BuilderAssistant.Core.Blueprints;
using BuilderAssistant.Core.Workbench;

namespace BuilderAssistant.Tests
{
    internal static partial class Program
    {
        private static void TestBlueprintLibraryAndPreview()
        {
            Console.WriteLine("[Blueprint library / visual placement read-only contract]");
            foreach(string sample in new[] { "blueprint-minimal-v3.json", "blueprint-states-v3.json", "bookhouse-furniture-v3.json" })
            {
                var result=BlueprintImportService.FromString(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,sample)),null);
                Check(result.Success,"published v0.22 generation example imports: "+sample);
                if(result.Success)
                {
                    var inspection=BlueprintPlacementPreview.Inspect(result.Blueprint,new CellPos(0,30,0),new FakeResolver(),new FakeTerrain(),false);
                    Check(inspection.Ready,"published example passes placement planner: "+sample);
                }
            }
            string root=Path.Combine(Path.GetTempPath(),"builder-library-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var library=new BlueprintFileLibrary(root);
                string error,first,second;
                var bp=Blueprint.Normalize("中文木屋",3,new List<BlueprintCell> {
                    new BlueprintCell(new CellPos(0,0,0),"PlanksBlock"),
                    new BlueprintCell(new CellPos(2,1,3),"PlanksBlock") });
                string json=BlueprintJson.Serialize(bp);
                Check(library.Save(bp.Name,json,out first)==null,"Chinese blueprint saved");
                Check(library.Save(bp.Name,json,out second)==null && first!=second,"repeated save never overwrites");
                string stem=Path.GetFileNameWithoutExtension(first);
                Check(library.ListNames(out error).Count==2 && error==null,"saved entries immediately listed");
                var loaded=BlueprintImportService.FromString(library.Load(stem,out error),null);
                Check(error==null && loaded.Success && loaded.Blueprint.Name==bp.Name,"persistent UTF8 name and JSON round trip");
                Check(library.Delete("../outside")!=null && library.Delete("..\\outside")!=null && library.Delete("C:outside")!=null,"delete rejects traversal paths");
                Check(library.Load("../outside",out error)==null && error!=null,"load rejects traversal paths");
                Check(library.Delete(stem)==null,"selected file deleted");
                Check(library.ListNames(out error).Count==1 && library.Load(stem,out error)==null,"deleted file absent on fresh disk read");
                Check(loaded.Blueprint.CellCount==2,"disk deletion preserves loaded memory blueprint");
                Check(library.Delete(stem)!=null,"missing deletion returns a useful failure");
                Check(Directory.GetFiles(root,"*.tmp").Length==0,"completed saves leave no temporary files");
                File.WriteAllText(Path.Combine(root,"broken.json"),"not json");
                Check(!BlueprintImportService.FromString(library.Load("broken",out error),null).Success,"bad library entry is rejected by the same importer");
                Check(new BlueprintFileLibrary(null).Save("x",json,out first)!=null,"unavailable data directory fails explicitly");
                // Upper transaction ceiling must survive serialized import (previous 256 KiB gate failed).
                var large=new List<BlueprintCell>();
                for(int i=0;i<10000;i++)large.Add(new BlueprintCell(new CellPos(i%25,(i/25)%20,i/500),"PlanksBlock"));
                string big=BlueprintJson.Serialize(Blueprint.Normalize("large",3,large));
                Check(big.Length>256*1024 && BlueprintImportService.FromString(big,null).Success,"10000-cell serialized blueprint fits clipboard import budget");
                Check(!BlueprintImportService.FromString("{"+new string(' ',BlueprintImportService.MaxImportChars)+"}",null).Success,"oversized clipboard rejected");

                var terrain=new FakeTerrain();var resolver=new FakeResolver();var origin=new CellPos(8,30,-6);
                for(int r=0;r<4;r++)
                {
                    Blueprint oriented=bp.Reorient(r,false);
                    var p=BlueprintPlacementPreview.Inspect(oriented,origin,resolver,terrain,false);
                    Check(p.Ready && p.Plan.Changes.Count==oriented.CellCount,"rotation preview and build share cell count");
                    foreach(var c in p.Plan.Changes)Check(p.Snapshot.ContainsKey(c.Absolute),"preview snapshot covers every planned write");
                    Check(p.StillMatches(terrain,(a,b)=>a==b),"unchanged preview can confirm");
                }
                Check(terrain.Writes==0,"preview and confirmation inspection never write terrain");
                var door=Blueprint.Normalize("door",3,new List<BlueprintCell> { new BlueprintCell(new CellPos(0,0,0),"WoodenDoorBlock",BlockFamily.Door,0) });
                var doorPreview=BlueprintPlacementPreview.Inspect(door,origin,resolver,terrain,false);
                Check(doorPreview.Ready && doorPreview.EnvelopeHeight==2 && doorPreview.Snapshot.ContainsKey(origin.Add(new CellPos(0,1,0))),"derived door top belongs to preview height and snapshot");
                terrain.SetCellValue(origin.Add(new CellPos(0,1,0)),33);
                Check(!doorPreview.StillMatches(terrain,(a,b)=>a==b),"door top changes invalidate confirmation");
                terrain.SetCellValue(origin.Add(new CellPos(0,1,0)),0);
                terrain.SetCellValue(origin,77);int before=terrain.Writes;
                var blocked=BlueprintPlacementPreview.Inspect(bp,origin,resolver,terrain,false);
                Check(!blocked.Ready && blocked.OccupiedCount==1 && blocked.Markers.Contains(origin),"occupied target is marked and blocked without clearing");
                var clear=BlueprintPlacementPreview.Inspect(bp,origin,resolver,terrain,true);
                Check(clear.Ready && clear.OccupiedCount==1,"clear preview reports exact overwrite count");
                Check(terrain.Writes==before,"clear preview still performs no writes");
                terrain.SetCellValue(origin,88);
                Check(!clear.StillMatches(terrain,(a,b)=>a==b),"terrain change invalidates confirmation snapshot");
                terrain.Missing.Add(origin);
                var waiting=BlueprintPlacementPreview.Inspect(bp,origin,resolver,terrain,true);
                Check(!waiting.Ready && waiting.Plan.AllEditabilityFailuresRecoverable,"missing chunk cannot confirm and permits bounded retry");
                terrain.Missing.Clear();
                Check(BlueprintPlacementPreview.Inspect(bp,origin,resolver,terrain,true).Ready,"ready chunk can be re-inspected");
                Check(!BlueprintPlacementPreview.Inspect(bp,new CellPos(0,255,0),resolver,terrain,true).Ready,"height overflow rejected before building");
                var many=new List<BlueprintCell>();
                for(int i=0;i<256;i++) { var pos=new CellPos(i%16,20,i/16);terrain.SetCellValue(pos,66);many.Add(new BlueprintCell(new CellPos(i%16,0,i/16),"PlanksBlock")); }
                var crowded=BlueprintPlacementPreview.Inspect(Blueprint.Normalize("many",3,many),new CellPos(0,20,0),resolver,terrain,true);
                Check(crowded.Ready && crowded.OccupiedCount==256 && crowded.Markers.Count==128 && crowded.Snapshot.Count==256,"marker budget never truncates validation or conflict count");
            }
            finally { foreach(string file in Directory.GetFiles(root))File.Delete(file);Directory.Delete(root); }
        }
    }
}

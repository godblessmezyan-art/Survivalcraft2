using System;
using System.Collections.Generic;
using BuilderAssistant.Core;
using BuilderAssistant.Core.Blueprints;
using BuilderAssistant.Core.BuildEngine;
using BuilderAssistant.Core.Furniture;
using BuilderAssistant.Core.Safety;

namespace BuilderAssistant.Tests
{
    internal static partial class Program
    {
        private static string FurnitureJson(string id, int rotation = 0, string extras = "", string cells = "[]")
        {
            return "{\"schemaVersion\":3,\"cells\":" + cells + ",\"furniturePlacements\":[{\"templateId\":\"" + id +
                "\",\"x\":4,\"y\":1,\"z\":4,\"rotation\":" + rotation + extras + "}]}";
        }

        private static Dictionary<CellPos, string> FurnitureSignature(Blueprint b)
        {
            var result = new Dictionary<CellPos, string>();
            foreach (BlueprintCell c in b.Cells) result.Add(c.Relative, c.MaterialKey + ":" + c.Data);
            return result;
        }

        private static bool SameFurnitureCells(Blueprint a, Blueprint b)
        {
            var expected = FurnitureSignature(a);
            if (a.CellCount != b.CellCount) return false;
            foreach (BlueprintCell cell in b.Cells)
            {
                string value;
                if (!expected.TryGetValue(cell.Relative, out value) || value != cell.MaterialKey + ":" + cell.Data) return false;
            }
            return true;
        }

        private static void TestFurnitureTemplates()
        {
            Console.WriteLine("[Furniture templates / schema3 / transactional integration]");
            string example = System.IO.File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "bookhouse-furniture-v3.json"));
            var imported = BlueprintImportService.FromString(example, null);
            Check(imported.Success && imported.Blueprint.FurniturePlacements.Count == 10 && imported.Blueprint.CellCount == 286, "AI example imports through live import service");
            if (imported.Success)
            {
                var terrain = new FakeTerrain();
                var origin = new CellPos(0, 70, 0);
                BuildPlan examplePlan = BuildPlanner.Plan(imported.Blueprint, origin, new FakeResolver(), terrain, false);
                Check(examplePlan.Success, "AI example structure plus furniture preflight");
                if (examplePlan.Success)
                {
                    var task = new BuildTask("AI example", origin, examplePlan.Changes, false);
                    while (!task.IsFinished)
                    {
                        int before = terrain.Writes;
                        task.Advance(terrain, 32);
                        Check(terrain.Writes - before <= 32, "AI example shares 32-cell frame budget");
                    }
                    Check(task.State == BuildTaskState.Completed && task.AppliedCount == 286, "all ten AI templates execute after floor");
                    Check(task.Undo(terrain) == 286, "AI example floor and all furniture undo together");
                }
            }
            int count = 0;
            foreach (FurnitureTemplate t in FurnitureTemplateRegistry.Default.Templates)
            {
                count++;
                FurnitureTemplate found;
                Check(FurnitureTemplateRegistry.Default.TryGet(t.Id, out found) && object.ReferenceEquals(t, found), "registry finds " + t.Id);
                Check(t.SupportedRotations.Count == 4 && t.Cells.Count > 0 && t.Backend == "structure-blocks-v1", "template metadata " + t.Id);
                for (int r = 0; r < 4; r++)
                {
                    var decoded = BlueprintJson.Decode(FurnitureJson(t.Id, r));
                    Check(decoded.Success, "AI JSON imports " + t.Id + " rot " + r);
                    if (!decoded.Success) continue;
                    Blueprint b = decoded.Blueprint;
                    Check(b.CellCount == t.Cells.Count && b.SizeX == ((r & 1) == 0 ? t.SizeX : t.SizeZ), "rotated dimensions " + t.Id);
                    string json = BlueprintJson.Serialize(b);
                    var again = BlueprintJson.Decode(json);
                    Check(again.Success && again.Blueprint.FurniturePlacements.Count == 1 && SameFurnitureCells(b, again.Blueprint), "placement JSON roundtrip " + t.Id);
                    for (int turn = 0; turn < 4; turn++)
                    for (int mirror = 0; mirror < 2; mirror++)
                    {
                        Blueprint transformed = b.Reorient(turn, mirror == 1);
                        var flattened = new Blueprint(b.Name, 2, b.SizeX, b.SizeY, b.SizeZ, new List<BlueprintCell>(b.Cells));
                        Check(SameFurnitureCells(flattened.Reorient(turn, mirror == 1), transformed), "template geometry and DATA transform " + t.Id);
                        var serialized = BlueprintJson.Decode(BlueprintJson.Serialize(transformed));
                        Check(serialized.Success && SameFurnitureCells(transformed, serialized.Blueprint), "oriented template export " + t.Id);
                    }
                    var terrain = new FakeTerrain();
                    var origin = new CellPos(20, 70, 20);
                    var plan = BuildPlanner.Plan(b, origin, new FakeResolver(), terrain, false);
                    Check(plan.Success, "AI furniture plan " + t.Id);
                    if (!plan.Success) continue;
                    var task = new BuildTask(t.Id, origin, plan.Changes, false);
                    while (!task.IsFinished)
                    {
                        int writes = terrain.Writes;
                        task.Advance(terrain, BuildSafetyLimits.MaxBlocksPerBatch);
                        Check(terrain.Writes - writes <= 32, "32-cell budget " + t.Id);
                    }
                    Check(task.State == BuildTaskState.Completed && task.AppliedCount == b.CellCount, "AI placement executes " + t.Id);
                    Check(task.Undo(terrain) == b.CellCount && task.PendingUndoCount == 0, "furniture undo " + t.Id);
                    foreach (BlueprintCell cell in b.Cells) Check(terrain.IsCellAir(origin.Add(cell.Relative)), "undo restores air " + t.Id);
                }
            }
            Check(count == 10, "ten initial furniture templates");
            FurnitureTemplate ignored;
            Check(!FurnitureTemplateRegistry.Default.TryGet("missing", out ignored), "unknown template lookup");
            var registry = new FurnitureTemplateRegistry();
            FurnitureTemplateRegistry.Default.TryGet("WoodChair_A", out ignored);
            registry.Register(ignored);
            bool duplicate = false;
            try { registry.Register(ignored); } catch (ArgumentException) { duplicate = true; }
            Check(duplicate, "duplicate ids rejected");
            Check(!BlueprintJson.Decode(FurnitureJson("missing")).Success, "unknown AI template rejected");
            Check(!BlueprintJson.Decode(FurnitureJson("WoodChair_A", 4)).Success, "invalid rotation rejected");
            Check(!BlueprintJson.Decode(FurnitureJson("WoodChair_A", 0, ",\"color\":\"red\"")).Success, "unsupported color rejected");
            Check(!BlueprintJson.Decode(FurnitureJson("WoodChair_A", 0, ",\"variant\":\"other\"")).Success, "unsupported variant rejected");
            Check(!BlueprintJson.Decode(FurnitureJson("WoodChair_A", 0, ",\"mirrorX\":1")).Success, "invalid mirror rejected");
            Check(!BlueprintJson.Decode(FurnitureJson("WoodChair_A").Replace("\"rotation\":0", "\"rotation\":0.5")).Success, "fractional rotation rejected");
            Check(!BlueprintJson.Decode(FurnitureJson("WoodChair_A").Replace("\"x\":4", "\"x\":2147483647")).Success, "overflow coordinate rejected");
            Check(!BlueprintJson.Decode(FurnitureJson("WoodChair_A").Replace("schemaVersion\":3", "schemaVersion\":2")).Success, "old schema cannot silently ignore furniture");
            Check(!BlueprintJson.Decode("{\"schemaVersion\":3,\"cells\":[],\"furniturePlacements\":[]}").Success, "empty furniture blueprint rejected");
            string collision = FurnitureJson("ReadingDesk_A", 0, "", "[{\"x\":5,\"y\":1,\"z\":4,\"key\":\"PlanksBlock\"}]");
            Check(!BlueprintJson.Decode(collision).Success, "structure in desk clearance box rejected");
            string overlap = FurnitureJson("WoodChair_A").Replace("}]}", "},{\"templateId\":\"WoodChair_A\",\"x\":4,\"y\":1,\"z\":4,\"rotation\":0}]}");
            Check(!BlueprintJson.Decode(overlap).Success, "furniture boxes cannot overlap");
            string combined = FurnitureJson("TallBookshelf_A", 0, "", "[{\"x\":0,\"y\":0,\"z\":0,\"key\":\"PlanksBlock\"}]");
            Blueprint mixed = BlueprintJson.Decode(combined).Blueprint;
            for (int r = 0; r < 4; r++)
            for (int m = 0; m < 2; m++)
            {
                Blueprint first = mixed.Reorient(r, m == 1);
                Blueprint flat = new Blueprint(first.Name, 2, first.SizeX, first.SizeY, first.SizeZ, new List<BlueprintCell>(first.Cells));
                for (int s = 0; s < 4; s++)
                    Check(SameFurnitureCells(first.Reorient(s, true), flat.Reorient(s, true)), "combined layout repeated mirror/rotation");
            }
            var world = new FakeTerrain();
            var anchor = new CellPos(0, 60, 0);
            var mixedPlan = BuildPlanner.Plan(mixed, anchor, new FakeResolver(), world, false);
            Check(mixedPlan.Success && mixedPlan.Changes[0].Absolute == anchor, "structure executes before furniture");
            var running = new BuildTask("mixed", anchor, mixedPlan.Changes, false);
            running.Advance(world, 2);
            running.Cancel(world);
            Check(running.State == BuildTaskState.Cancelled && world.IsCellAir(anchor) && running.PendingRollbackCount == 0, "cancel rolls back structure and furniture");
            world = new FakeTerrain();
            running = new BuildTask("mixed", anchor, mixedPlan.Changes, false);
            running.Advance(world, 1);
            CellPos obstacle = mixedPlan.Changes[1].Absolute;
            world.SetCellValue(obstacle, 777);
            running.Advance(world, 32);
            Check(running.State == BuildTaskState.Failed && running.FailureReason.Contains("TallBookshelf_A") && world.IsCellAir(anchor), "furniture execution collision reports id and rolls structure back");
            int current;
            world.TryGetCellValue(obstacle, out current);
            Check(current == 777, "failure preserves external obstacle");
            var blockedPlan = BuildPlanner.Plan(mixed, anchor, new FakeResolver(), world, false);
            Check(!blockedPlan.Success && blockedPlan.FailureReason.Contains("TallBookshelf_A"), "terrain collision reports furniture id before writes");
            Blueprint desk = BlueprintJson.Decode(FurnitureJson("ReadingDesk_A")).Blueprint;
            var clearanceWorld = new FakeTerrain();
            clearanceWorld.Occupy(anchor.Add(new CellPos(1, 0, 0)), 777);
            Check(!BuildPlanner.Plan(desk, anchor, new FakeResolver(), clearanceWorld, false).Success, "desk empty-space terrain collision");
            var deskOverride = BuildPlanner.Plan(desk, anchor, new FakeResolver(), clearanceWorld, true);
            Check(deskOverride.Success, "explicit override permits obstructed clearance");
            var deskTask = new BuildTask("desk", anchor, deskOverride.Changes, true);
            while (!deskTask.IsFinished) deskTask.Advance(clearanceWorld, 32);
            Check(clearanceWorld.ValueAt(anchor.Add(new CellPos(1, 0, 0))) == 777, "template never clears unwritten bounding-box holes");
            world = new FakeTerrain();
            running = new BuildTask("mixed", anchor, mixedPlan.Changes, false);
            while (!running.IsFinished) running.Advance(world, 32);
            CellPos furnitureCell = mixedPlan.Changes[1].Absolute;
            world.Blocked.Add(furnitureCell);
            running.Undo(world);
            Check(running.PendingUndoCount == 1, "unloaded furniture undo remains pending");
            world.Blocked.Clear();
            Check(running.RetryUndo(world) == 0 && world.IsCellAir(furnitureCell), "pending furniture undo finishes after chunk recovery");
            world = new FakeTerrain();
            running = new BuildTask("mixed", anchor, mixedPlan.Changes, false);
            running.Advance(world, 2);
            world.Blocked.Add(furnitureCell);
            running.Cancel(world);
            Check(running.PendingRollbackCount == 1, "cancel retains furniture rollback when chunk unavailable");
            world.Blocked.Clear();
            Check(running.RetryRollback(world) == 0 && world.IsCellAir(anchor) && world.IsCellAir(furnitureCell), "delayed furniture rollback completes");
            world = new FakeTerrain();
            running = new BuildTask("mixed", anchor, mixedPlan.Changes, false);
            while (!running.IsFinished) running.Advance(world, 32);
            world.Occupy(furnitureCell, 999);
            running.Undo(world);
            Check(world.ValueAt(furnitureCell) == 999, "furniture undo honors later player edit");
            string doorCollision = FurnitureJson("WoodChair_A", 0, "", "[{\"x\":4,\"y\":0,\"z\":4,\"key\":\"WoodenDoorBlock\",\"f\":\"door\"}]");
            Check(!BlueprintJson.Decode(doorCollision).Success, "furniture cannot occupy auto-generated door upper half");
            Check(!BuildPlanner.Plan(mixed, new CellPos(0, 255, 0), new FakeResolver(), new FakeTerrain(), false).Success, "furniture height boundary");
            string far = combined.Replace("\"x\":4", "\"x\":70");
            Check(!BlueprintJson.Decode(far).Success, "combined axis limit");
            var many = new List<FurniturePlacement>();
            for (int i = 0; i < 850; i++) many.Add(new FurniturePlacement("TallBookshelf_A", new CellPos(i * 4, 0, 0)));
            bool capped = false;
            try { FurniturePlacementResolver.Compose("cap", 3, new List<BlueprintCell>(), many); }
            catch (ArgumentException ex) { capped = ex.Message.Contains("上限"); }
            Check(capped, "furniture expansion shares total cell cap");
        }
    }
}

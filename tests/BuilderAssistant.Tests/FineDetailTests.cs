using System;
using System.Collections.Generic;
using BuilderAssistant.Core;
using BuilderAssistant.Core.Blueprints;
using BuilderAssistant.Core.BuildEngine;
using BuilderAssistant.Core.Transactions;
using BuilderAssistant.Core.Safety;

namespace BuilderAssistant.Tests
{
    internal sealed class DetailResolver : IBlockValueResolver
    {
        public bool TryResolve(string key, out int value) { return TryResolve(key, 0, out value); }
        public bool TryResolve(string key, int data, out int value)
        {
            VanillaBlockCapability c; string type;
            if (!VanillaBlockCapabilities.TryGetByMaterialKey(key, out c, out type)) { value = 0; return false; }
            string error;
            return TryResolve(key, c.Family, data, out value, out error);
        }
        public bool TryResolve(string key, BlockFamily family, int data, out int value, out string error)
        {
            value = 0; error = "invalid capability";
            VanillaBlockCapability c; string type;
            if (!VanillaBlockCapabilities.TryGetByMaterialKey(key, out c, out type) || c.Family != family || !c.IsValidData(data)) return false;
            int id = family == BlockFamily.Ladder ? 2 : family == BlockFamily.Torch ? 3 : family == BlockFamily.Door ? 4 : 1;
            value = id | (data << 14); error = null; return true;
        }
    }

    internal sealed class DetailTerrain : ITerrainGateway, IConstructionTerrainGateway
    {
        public readonly FakeTerrain Grid = new FakeTerrain();
        public readonly List<CellPos> WriteOrder = new List<CellPos>();
        public CellPos? FailAt;
        public bool FailAfterMutation;
        public CellPos? DenyRestoreAt;
        public bool IsCellEditable(CellPos p) { return Grid.IsCellEditable(p); }
        public CellEditability InspectCell(CellPos p) { return Grid.InspectCell(p); }
        public bool IsCellAir(CellPos p) { int v; return TryGetCellValue(p, out v) && (v & 1023) == 0; }
        public bool IsCellSoftObstruction(CellPos p) { return false; }
        public bool TryGetCellValue(CellPos p, out int value) { return Grid.TryGetCellValue(p, out value); }
        public bool SetCellValue(CellPos p, int value)
        {
            if (DenyRestoreAt.HasValue && DenyRestoreAt.Value.Equals(p)) return false;
            WriteOrder.Add(p);
            return Grid.SetCellValue(p, value);
        }
        public bool SupportsAttachment(int value, BlockFamily family, int face) { return (value & 1023) == 1; }
        public bool ValuesEquivalent(int current, int expected) { return (current & ~0x3C00) == (expected & ~0x3C00); }
        public bool ApplyWithJournal(CellPos p, int value, out List<BlockUndoRecord> journal, out string error)
        {
            journal = new List<BlockUndoRecord>(); error = null;
            if (FailAt.HasValue && FailAt.Value.Equals(p) && !FailAfterMutation) { error = "injected write denial"; return false; }
            int old; Grid.TryGetCellValue(p, out old);
            // Capture the simulated native upper BEFORE applying the bottom.
            CellPos upper = p.Add(new CellPos(0, 1, 0));
            int oldUpper; Grid.TryGetCellValue(upper, out oldUpper);
            SetCellValue(p, value);
            journal.Add(new BlockUndoRecord(p, old, value));
            if ((value & 1023) == 4)
            {
                Grid.SetCellValue(upper, value);
                journal.Add(new BlockUndoRecord(upper, oldUpper, value));
            }
            if (FailAt.HasValue && FailAt.Value.Equals(p)) { error = "injected callback failure after mutation"; return false; }
            return true;
        }
    }

    internal static partial class Program
    {
        private static int DumpBlockCapabilities(string path)
        {
            var root = BuilderAssistant.Core.Json.JsonValue.NewObject();
            root.Add("modVersion", BuilderAssistant.Core.Json.JsonValue.NewString("0.19.0"));
            root.Add("maxSchemaVersion", BuilderAssistant.Core.Json.JsonValue.NewNumber(3));
            var furniture = BuilderAssistant.Core.Json.JsonValue.NewArray();
            foreach (var template in BuilderAssistant.Core.Furniture.FurnitureTemplateRegistry.Default.Templates)
            {
                var item = BuilderAssistant.Core.Json.JsonValue.NewObject();
                item.Add("id", BuilderAssistant.Core.Json.JsonValue.NewString(template.Id));
                item.Add("backend", BuilderAssistant.Core.Json.JsonValue.NewString(template.Backend));
                item.Add("sizeX", BuilderAssistant.Core.Json.JsonValue.NewNumber(template.SizeX));
                item.Add("sizeY", BuilderAssistant.Core.Json.JsonValue.NewNumber(template.SizeY));
                item.Add("sizeZ", BuilderAssistant.Core.Json.JsonValue.NewNumber(template.SizeZ));
                furniture.Add(item);
            }
            root.Add("furnitureTemplates", furniture);
            root.Add("targetEvidence", BuilderAssistant.Core.Json.JsonValue.NewString("Windows x25.01.17 SHA256 3D14B8D698546F13E66FBF49DBCFAC45A2CDDFC97709D738050DF2E1CC74A7AD"));
            root.Add("android7723", BuilderAssistant.Core.Json.JsonValue.NewString("待验证"));
            root.Add("undoScope", BuilderAssistant.Core.Json.JsonValue.NewString("terrain snapshots with conflict protection; full subsystem undo is a separate capability"));
            var blocks = BuilderAssistant.Core.Json.JsonValue.NewArray();
            foreach (VanillaBlockCapability c in VanillaBlockCapabilities.All)
            {
                var row = BuilderAssistant.Core.Json.JsonValue.NewObject();
                row.Add("type", BuilderAssistant.Core.Json.JsonValue.NewString(c.TypeName));
                row.Add("family", BuilderAssistant.Core.Json.JsonValue.NewString(BlockOrientation.FamilyToTag(c.Family)));
                row.Add("kind", BuilderAssistant.Core.Json.JsonValue.NewString(c.Kind.ToString()));
                row.Add("place", BuilderAssistant.Core.Json.JsonValue.NewBool(c.CanPlace));
                row.Add("copy", BuilderAssistant.Core.Json.JsonValue.NewBool(c.CanCopy));
                row.Add("rotate", BuilderAssistant.Core.Json.JsonValue.NewBool(c.CanRotate));
                row.Add("mirror", BuilderAssistant.Core.Json.JsonValue.NewBool(c.CanMirror));
                row.Add("preserveData", BuilderAssistant.Core.Json.JsonValue.NewBool(c.CanSaveState));
                row.Add("undoTerrain", BuilderAssistant.Core.Json.JsonValue.NewBool(c.CanUndoTerrain));
                row.Add("undoAllSubsystems", BuilderAssistant.Core.Json.JsonValue.NewBool(c.CanUndoAllSubsystems));
                var data = BuilderAssistant.Core.Json.JsonValue.NewArray();
                for (int d = 0; d < 1024; d++) if (c.IsValidData(d)) data.Add(BuilderAssistant.Core.Json.JsonValue.NewNumber(d));
                row.Add("allowedData", data);
                blocks.Add(row);
            }
            root.Add("blocks", blocks);
            System.IO.File.WriteAllText(path, root.ToJson() + Environment.NewLine, new System.Text.UTF8Encoding(false));
            return 0;
        }

        private static void TestFineDetailStates()
        {
            Console.WriteLine("[FineDetailStates]");
            // Exhaust every canonical DATA state: the same decoder is used by the live copier.
            foreach (VanillaBlockCapability c in VanillaBlockCapabilities.All)
            {
                bool roundTrip = true, transforms = true, strict = true;
                int legal = 0;
                for (int d = 0; d < 2048; d++)
                {
                    int captured;
                    bool valid = c.IsValidData(d);
                    if (c.TryDecodeTerrainData(d, out captured) != valid) strict = false;
                    if (!valid) continue;
                    legal++;
                    var b = new BlueprintBuilder();
                    b.SetOriented(0, 0, 0, c.TypeName, c.Family, captured);
                    Blueprint original = b.ToBlueprint("state", 2);
                    BlueprintJsonDecodeResult decoded = BlueprintJson.Decode(BlueprintJson.Serialize(original));
                    if (!decoded.Success || !SameCellSet(original, decoded.Blueprint)) roundTrip = false;
                    Blueprint transformed = original;
                    for (int turn = 0; turn < 4; turn++) transformed = transformed.Reorient(1, false);
                    if (!SameCellSet(original, transformed) || !SameCellSet(original, original.Reorient(0, true).Reorient(0, true))) transforms = false;
                    // Dihedral relation M R M = R^-1 catches handedness mistakes missed by identity-only checks.
                    if (BlockOrientation.MirrorX(c.Family, BlockOrientation.Rotate(c.Family, BlockOrientation.MirrorX(c.Family, d), 1)) !=
                        BlockOrientation.Rotate(c.Family, d, -1)) transforms = false;
                }
                Check(legal > 0 && roundTrip, c.TypeName + " every canonical state copies and JSON round-trips");
                Check(transforms && strict, c.TypeName + " exhaustive transform laws and rejecting decode");
            }
            Check(BlockOrientation.Rotate(BlockFamily.Log, 1, 1) == 2, "Z-axis log rotates to X-axis");
            Check(BlockOrientation.MirrorX(BlockFamily.Stairs, 8) == 9 && BlockOrientation.MirrorX(BlockFamily.Stairs, 16) == 17,
                "stair corner collision quadrants reflect correctly");
            Check(BlockOrientation.Rotate(BlockFamily.Ladder, 0, 1) == 3 && BlockOrientation.MirrorX(BlockFamily.Ladder, 1) == 3,
                "ladder face vectors follow transformed support positions");
            foreach (BlockFamily f in new[] { BlockFamily.Trapdoor, BlockFamily.FenceGate })
            {
                bool laws = true;
                for (int d = 0; d < 512; d++)
                {
                    if (!BlockOrientation.IsValidAuthoredData(f, d)) continue;
                    int v = d;
                    for (int i = 0; i < 4; i++) v = BlockOrientation.Rotate(f, v, 1);
                    if (v != d || BlockOrientation.MirrorX(f, BlockOrientation.MirrorX(f, d)) != d) laws = false;
                }
                Check(laws, f + " researched direction codec returns to original");
            }
            foreach (string type in new[] { "WoodenTrapdoorBlock", "CellTrapdoorBlock", "WoodenFenceGateBlock", "IronFenceGateBlock", "CarpetBlock", "IvyBlock" })
            {
                VanillaBlockCapability ignored;
                Check(!VanillaBlockCapabilities.TryGetByTypeName(type, out ignored), type + " stays unsupported until subsystem rollback is proven");
            }
            foreach (string type in new[] { "WindowBlock", "FramedGlassBlock", "GlassBlock", "IronBlock", "LimestoneBlock" })
            {
                VanillaBlockCapability c; VanillaBlockCapabilities.TryGetByTypeName(type, out c);
                Check(c.IsValidData(0) && !c.IsValidData(1), type + " rejects fabricated paint state");
            }
            foreach (int version in new[] { 1, 2 })
            {
                var decoded = BlueprintJson.Decode("{\"schemaVersion\":" + version + ",\"cells\":[{\"x\":0,\"y\":0,\"z\":0,\"key\":\"StoneBase\"}]}");
                Check(decoded.Success && decoded.Blueprint.Cells[0].Data == 0, "legacy schema " + version + " remains readable");
            }
            foreach (string field in new[] { "null", "123", "{}" })
                Check(!BlueprintJson.Decode("{\"schemaVersion\":2,\"cells\":[{\"x\":0,\"y\":0,\"z\":0,\"key\":\"StoneBase\",\"f\":" + field + "}]}").Success,
                    "non-string family rejected: " + field);
            foreach (BlockFamily f in new[] { BlockFamily.Cube, BlockFamily.Stairs, BlockFamily.Slab, BlockFamily.Fence })
            {
                int encoded;
                Check(!BlockOrientation.TryEncodePaint(f,0,-1,out encoded) && !BlockOrientation.TryEncodePaint(f,0,16,out encoded),
                    f + " rejects out-of-range colors without truncating");
            }
            TestAttachedConstruction();
            TestRelatedJournal();
            TestLarge4419();
            TestLargeRollbackBudget();
        }

        private static Blueprint AttachedBlueprint()
        {
            // Face3 ladder is at x=0, support x=1: coordinate order alone would put the ladder first.
            var b = new BlueprintBuilder();
            b.SetOriented(0, 0, 0, "WoodenLadderBlock", BlockFamily.Ladder, 3);
            b.Set(1, 0, 0, "BricksBlock");
            b.SetOriented(1, 1, 0, "TorchBlock", BlockFamily.Torch, 4);
            return b.ToBlueprint("attached", 2);
        }

        private static void TestAttachedConstruction()
        {
            var terrain = new DetailTerrain();
            var resolver = new DetailResolver();
            var origin = new CellPos(0, 20, 0);
            Blueprint bp = BlueprintJson.Decode(BlueprintJson.Serialize(AttachedBlueprint())).Blueprint;
            BuildPlan plan = BuildPlanner.Plan(bp, origin, resolver, terrain, false);
            Check(plan.Success && plan.Changes[0].Absolute.Equals(new CellPos(1, 20, 0)), "dependency sort places support before same-level ladder");
            var task = new BuildTask(bp.Name, origin, plan.Changes, false);
            while (!task.IsFinished) task.Advance(terrain, 32);
            Check(task.State == BuildTaskState.Completed && terrain.WriteOrder[0].Equals(new CellPos(1, 20, 0)), "attachment writes follow dependency order and pass next-frame verification");
            terrain.WriteOrder.Clear();
            task.Undo(terrain);
            Check(terrain.IsCellAir(origin) && terrain.IsCellAir(new CellPos(1,20,0)) && terrain.IsCellAir(new CellPos(1,21,0)), "undo restores the entire attachment fixture");
            Check(terrain.WriteOrder[terrain.WriteOrder.Count - 1].Equals(new CellPos(1,20,0)), "undo removes attachments before their support");
            var floating = new BlueprintBuilder(); floating.SetOriented(0,0,0,"IronLadderBlock",BlockFamily.Ladder,0);
            Check(!BuildPlanner.Plan(floating.ToBlueprint("unsupported",2),origin,resolver,new DetailTerrain(),false).Success,
                "missing external support is rejected before writes");
            terrain = new DetailTerrain();
            plan = BuildPlanner.Plan(bp, origin, resolver, terrain, false);
            task = new BuildTask("support-lost", origin, plan.Changes, false);
            task.Advance(terrain, 1);
            terrain.Grid.SetCellValue(new CellPos(1,20,0),0);
            task.Advance(terrain, 32);
            Check(task.State == BuildTaskState.Failed && terrain.IsCellAir(origin), "support loss during construction fails safely");
            terrain = new DetailTerrain(); plan = BuildPlanner.Plan(bp,origin,resolver,terrain,false);
            task = new BuildTask("postcheck",origin,plan.Changes,false); task.Advance(terrain,32);
            terrain.Grid.SetCellValue(origin,0); task.Advance(terrain,32);
            Check(task.State == BuildTaskState.Failed && terrain.IsCellAir(new CellPos(1,20,0)), "delayed attachment deletion fails postcheck and rolls back support");
            terrain = new DetailTerrain(); plan = BuildPlanner.Plan(bp,origin,resolver,terrain,false);
            task = new BuildTask("partial",origin,plan.Changes,false); while (!task.IsFinished) task.Advance(terrain,32);
            terrain.DenyRestoreAt = new CellPos(1,21,0); task.Undo(terrain);
            Check(task.PendingUndoCount == 3 && !terrain.IsCellAir(new CellPos(1,20,0)), "failed undo stops before removing supporting structure");
            terrain.DenyRestoreAt = null; task.RetryUndo(terrain);
            Check(task.PendingUndoCount == 0 && terrain.IsCellAir(new CellPos(1,20,0)), "retry preserves reverse dependency order");
        }

        private static void TestRelatedJournal()
        {
            var origin = new CellPos(0,20,0);
            var upper = origin.Add(new CellPos(0,1,0));
            foreach (bool failAfter in new[] { false, true })
            {
                var terrain = new DetailTerrain { FailAt = origin, FailAfterMutation = failAfter };
                var changes = new List<PlannedChange> { new PlannedChange(origin,4,BlockFamily.Door) };
                var task = new BuildTask("callback",origin,changes,false);
                task.Advance(terrain,32);
                Check(task.State == BuildTaskState.Failed && terrain.IsCellAir(origin) && terrain.IsCellAir(upper),
                    "journal restores direct and generated cells after callback failure=" + failAfter);
            }
            var world = new DetailTerrain();
            var build = new BuildTask("derived",origin,new List<PlannedChange> { new PlannedChange(origin,4,BlockFamily.Door) },false);
            build.Advance(world,32); build.Undo(world);
            Check(world.IsCellAir(origin) && world.IsCellAir(upper), "generated upper has a pre-mutation snapshot and is undone");
            var b = new BlueprintBuilder(); b.SetOriented(0,0,0,"WoodenDoorBlock",BlockFamily.Door,0); b.Set(0,1,0,"BricksBlock");
            Check(!BuildPlanner.Plan(b.ToBlueprint("collision",2),origin,new DetailResolver(),new DetailTerrain(),false).Success,
                "planned blocks cannot occupy generated door upper");
        }

        private static void TestLargeRollbackBudget()
        {
            var b = new BlueprintBuilder();
            for (int i = 0; i < 4419; i++) b.Set(i % 21,i / 441,(i / 21) % 21,"BricksBlock");
            Blueprint bp = b.ToBlueprint("large-failure",2);
            var origin = new CellPos(0,20,0);
            foreach (bool cancel in new[] { false, true })
            {
                var terrain = new DetailTerrain();
                BuildPlan plan = BuildPlanner.Plan(bp,origin,new DetailResolver(),terrain,false);
                if (!cancel) terrain.FailAt = plan.Changes[40].Absolute;
                var task = new BuildTask("large-failure",origin,plan.Changes,false);
                bool budget = true;
                int guard = 0;
                while (!task.IsFinished && guard++ < 500)
                {
                    int before = terrain.Grid.Writes;
                    task.Advance(terrain,32);
                    if (cancel && task.AppliedCount >= 64)
                    {
                        // Cancel is another update action, with its own bounded restoration call.
                        if (terrain.Grid.Writes - before > 32) budget = false;
                        before = terrain.Grid.Writes;
                        task.Cancel(terrain);
                    }
                    if (terrain.Grid.Writes - before > 32) budget = false;
                }
                while (task.PendingRollbackCount > 0 && guard++ < 1000)
                {
                    int before = terrain.Grid.Writes;
                    task.RetryRollback(terrain);
                    if (terrain.Grid.Writes - before > 32) budget = false;
                }
                bool clean = task.PendingRollbackCount == 0;
                foreach (PlannedChange c in plan.Changes) if (!terrain.IsCellAir(c.Absolute)) clean = false;
                Check(clean && budget && task.State == (cancel ? BuildTaskState.Cancelled : BuildTaskState.Failed),
                    "large failure/cancel restores exact world with at most 32 writes per call, cancel=" + cancel);
            }
            var cap = new BlueprintBuilder();
            for (int i = 0; i < 9999; i++) cap.Set(i % 64,i / 4096,(i / 64) % 64,"BricksBlock");
            cap.SetOriented(0,3,0,"WoodenDoorBlock",BlockFamily.Door,0);
            var result = BuildPlanner.Plan(cap.ToBlueprint("derived-cap",2),origin,new DetailResolver(),new DetailTerrain(),false);
            Check(!result.Success && result.FailureReason.Contains("事务超过上限"), "generated door upper counts toward 10000-cell transaction cap");
        }

        private static void TestLarge4419()
        {
            var b = new BlueprintBuilder();
            for (int i = 0; i < 4419; i++) b.Set(i % 21, i / 441, (i / 21) % 21, "BricksBlock");
            var imported = BlueprintJson.Decode(BlueprintJson.Serialize(b.ToBlueprint("4419",2)));
            Check(imported.Success && imported.Blueprint.CellCount == 4419, "4419-cell JSON import regression");
            var terrain = new DetailTerrain(); var origin = new CellPos(0,20,0);
            var plan = BuildPlanner.Plan(imported.Blueprint,origin,new DetailResolver(),terrain,false);
            var task = new BuildTask("4419",origin,plan.Changes,false);
            int calls = 0; bool budget = true;
            while (!task.IsFinished && calls < 1000) { if (task.Advance(terrain,32000) > 32) budget = false; calls++; }
            Check(budget && calls == 139 && task.AppliedCount == 4419 && task.State == BuildTaskState.Completed,
                "4419 cells use 139 bounded batches even when caller requests an excessive budget");
            int undoCalls = 1, restored = task.Undo(terrain);
            bool undoBudget = restored <= 32;
            while (task.PendingUndoCount > 0 && undoCalls < 1000)
            {
                int before = terrain.Grid.Writes;
                task.RetryUndo(terrain);
                if (terrain.Grid.Writes - before > 32) undoBudget = false;
                restored += task.LastRestoreCount;
                undoCalls++;
            }
            Check(restored == 4419 && undoCalls == 139 && undoBudget && terrain.IsCellAir(origin), "4419-cell undo uses 139 bounded batches");
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using Engine;
using Game;
using Game.NetWork;
using SCIEPort.Features.Architecture;
using SCIEPort.Features.Blueprints;
using SCIEPort.Features.Blueprints.Core;
using SCIEPort.Features.Blueprints.Core.Blueprints;
using SCIEPort.Features.Blueprints.Core.BuildEngine;

internal static partial class BlueprintTests
{
    private static void ArchitectureTests()
    {
        var previous = new Dictionary<int, Block>();
        int forms = 0, meshes = 0, maxTriangles = 0;
        var cells = new List<BlueprintCell>();
        try
        {
            foreach (string type in ArchitectureRules.Types)
            {
                int id = ArchitectureRules.Id(type); previous[id] = BlocksManager.Blocks[id];
                var block = (ArchitectureBlock)Activator.CreateInstance(typeof(ArchitectureBlock).Assembly.GetType("SCIEPort.Features.Architecture." + type, true));
                block.BlockIndex = id; BlocksManager.Blocks[id] = block;
                int variants = 0;
                foreach (int value in block.GetCreativeValues())
                {
                    Check(Terrain.ExtractContents(value) == id && Terrain.ExtractData(value) == variants * 8, "creative architectural DATA"); variants++;
                }
                Check(variants == ArchitectureRules.Variants(id), "all architectural forms visible");
                Check(block.GetEmittedLightAmount(Terrain.MakeBlockValue(id)) == (id == 1017 ? 12 : 0), "native lantern light only");
                for (int data = -1; data <= 80; data++)
                {
                    VanillaBlockCapability cap; Check(VanillaBlockCapabilities.TryGetByTypeName(type, out cap), "owned authoring catalog");
                    Check(cap.IsValidData(data) == ArchitectureRules.Valid(id, data), "type-qualified DATA rejects unused variants");
                }
                for (int variant = 0; variant < variants; variant++)
                {
                    forms++;
                    for (int state = 0; state < 8; state++)
                    {
                        int data = (variant << 3) | state;
                        var shape = ArchitectureGeometry.Build(id, data); meshes++;
                        Check(shape.Bounds.Min.X >= -.0001 && shape.Bounds.Min.Y >= -.0001 && shape.Bounds.Min.Z >= -.0001 && shape.Bounds.Max.X <= 1.0001 && shape.Bounds.Max.Y <= 1.0001 && shape.Bounds.Max.Z <= 1.0001, "geometry stays inside one journalled cell " + id + "/" + data + " " + shape.Bounds);
                        Check(shape.Collision.Length <= 128 && shape.Interaction.Length == 1, "bounded collision and touch selection");
                        foreach (BoundingBox box in shape.Collision) Check(box.Min.X >= -.0001 && box.Min.Y >= -.0001 && box.Min.Z >= -.0001 && box.Max.X <= 1.0001 && box.Max.Y <= 1.0001 && box.Max.Z <= 1.0001 && box.Max.X > box.Min.X && box.Max.Y > box.Min.Y && box.Max.Z > box.Min.Z, "nonempty bounded collision");
                        int triangles = (shape.Opaque.Indices.Count + shape.Glass.Indices.Count) / 3;
                        maxTriangles = Math.Max(maxTriangles, triangles); Check(triangles > 0 && triangles <= 1600, "Android per-part triangle budget");
                        foreach (BlockMesh mesh in new[] { shape.Opaque, shape.Glass })
                        {
                            Check(mesh.Vertices.Count < ushort.MaxValue && mesh.Indices.Count % 3 == 0, "16-bit mesh indices");
                            for (int i = 0; i < mesh.Indices.Count; i += 3)
                            {
                                int a = mesh.Indices.Array[i], b = mesh.Indices.Array[i + 1], c = mesh.Indices.Array[i + 2];
                                Check(a < mesh.Vertices.Count && b < mesh.Vertices.Count && c < mesh.Vertices.Count, "indices reference vertices");
                                Vector3 p = mesh.Vertices.Array[a].Position, q = mesh.Vertices.Array[b].Position, r = mesh.Vertices.Array[c].Position;
                                Vector3 cross = Vector3.Cross(q - p, r - p);
                                Check(cross.Length() > .0000001f, "nondegenerate authored triangle");
                                Check(Vector3.Dot(cross, CellFace.FaceToVector3(mesh.Vertices.Array[a].Face)) < 0, "native clockwise winding survives mirror " + id + "/" + data + "/" + i);
                            }
                            for (int i = 0; i < mesh.Vertices.Count; i++)
                            {
                                var vertex = mesh.Vertices.Array[i];
                                Check(!float.IsNaN(vertex.Position.X) && !float.IsInfinity(vertex.Position.Y) && vertex.Face <= 5, "finite vertex and native lighting face");
                                Check(vertex.TextureCoordinates.X == .5f && vertex.TextureCoordinates.Y == .5f, "white texel UV");
                            }
                        }
                        Check(shape.Glass.Vertices.Count > 0 == (id == 1016 && variant == 0), "glass goes only into transparent mesh");
                        bool glow = false; foreach (var vertex in shape.Opaque.Vertices) glow |= vertex.IsEmissive;
                        Check(glow == (id == 1017), "lantern emissive panels");
                        for (int turn = 0; turn < 4; turn++) foreach (bool mirrored in new[] { false, true })
                        {
                            int transformed = BlockOrientation.Rotate(BlockFamily.Detail, data, turn);
                            if (mirrored) transformed = BlockOrientation.MirrorX(BlockFamily.Detail, transformed);
                            var other = ArchitectureGeometry.Build(id, transformed);
                            Check(other.Opaque.Vertices.Count == shape.Opaque.Vertices.Count, "orientation preserves mesh topology");
                            // Sample every part, including its off-centre/chiral features.
                            for (int i = 0; i < shape.Opaque.Vertices.Count; i += 17)
                            {
                                Vector3 p = shape.Opaque.Vertices.Array[i].Position;
                                for (int j = 0; j < turn; j++) p = new Vector3(1 - p.Z, p.Y, p.X);
                                if (mirrored) p.X = 1 - p.X;
                                Check((p - other.Opaque.Vertices.Array[i].Position).Length() < .00001f, "DATA transform matches blueprint geometric transform");
                            }
                        }
                        cells.Add(new BlueprintCell(new CellPos(cells.Count % 24, cells.Count / 24, 0), type, BlockFamily.Detail, data));
                    }
                }
            }
            VanillaBlockRegistry.Reset();
            var palette = new VanillaBlockPalette();
            var bp = SCIEPort.Features.Blueprints.Core.Blueprints.Blueprint.Normalize("古建细节套件", 3, cells);
            var imported = BlueprintImportService.FromString(BlueprintJson.Serialize(bp), null);
            Check(imported.Success && imported.Blueprint.CellCount == cells.Count, "detail JSON serialization and import");
            Check(!BlueprintImportService.FromString("{\"schemaVersion\":3,\"cells\":[{\"x\":0,\"y\":0,\"z\":0,\"key\":\"AncientRoofBlock\",\"f\":\"detail\",\"d\":48}]}", null).Success, "undefined roof variant rejected at import");
            foreach (BlueprintCell cell in cells) { string error; Check(palette.TryValidateCell(cell, out error), "exact owned CLR type activates material"); }
            foreach (var role in new[] { WorkType.Local, WorkType.Server, WorkType.Client })
            {
                CommonLib.WorkType = role;
                using (var f = new Fixture())
                {
                    CommonLib.WorkType = role;
                    var player = f.Player(1); Check(f.Start(player, imported.Blueprint), "detail construction starts per role"); f.Drain();
                    Check(f.Terrain.Cells.Count == cells.Count, "all architectural states placed");
                    f.Host.UndoDirect(player); f.Drain();
                    foreach (var value in f.Terrain.Cells.Values) Check(Terrain.ExtractContents(value) == 0, "detail undo restores terrain");
                    Check(f.Start(player, imported.Blueprint), "detail restart for cancel"); f.Tick(); f.Host.CancelDirect(player); f.Drain();
                    Check(f.Terrain.Occupied == 0, "partial architectural construction rolls back on cancel");
                    Check(f.Start(player, imported.Blueprint), "detail restart for exception"); f.Terrain.FailAt = f.Terrain.Writes + 9; f.Drain();
                    Check(f.Terrain.Occupied == 0, "partial architectural construction rolls back on write failure");
                }
            }
            foreach(string template in new[]{"AncientMoonWindow_A","AncientTeaCorner_A","AncientObservatory_A"})
            {
                string json="{\"schemaVersion\":3,\"cells\":[],\"furniturePlacements\":[{\"templateId\":\""+template+"\",\"x\":0,\"y\":0,\"z\":0,\"rotation\":0}]}";
                var result=BlueprintImportService.FromString(json,null); Check(result.Success,"architectural assembly template imports");
                for(int r=0;r<4;r++)foreach(bool m in new[]{false,true})
                {
                    var transformed=result.Blueprint.Reorient(r,m); Check(BlueprintImportService.FromString(BlueprintJson.Serialize(transformed),null).Success,"assembly template rotation mirror and save");
                    using(var f=new Fixture()){var player=f.Player(1);Check(f.Start(player,transformed),"assembly builds");f.Drain();f.Host.UndoDirect(player);f.Drain();Check(f.Terrain.Occupied==0,"assembly undo");}
                }
            }
            foreach(string name in new[]{"ancient-detail-kit.json","ancient-bookhouse-corners.json"})
            {
                var result=BlueprintImportService.FromString(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ArchitectureExamples",name)),null);
                Check(result.Success,"delivered example parses: "+name);
                for(int r=0;r<4;r++)foreach(bool m in new[]{false,true})
                {
                    using(var f=new Fixture()){var player=f.Player(1);Check(f.Start(player,result.Blueprint.Reorient(r,m)),"delivered example plans");f.Drain();f.Host.UndoDirect(player);f.Drain();Check(f.Terrain.Occupied==0,"delivered example undo");}
                }
            }
            // Native ID7 journal/write/undo (actual terrain fixture, no live room).
            var requests = new List<int>();
            foreach (string type in ArchitectureRules.Types)
            {
                int id = ArchitectureRules.Id(type);
                requests.Add(Terrain.MakeBlockValue(id, 0, ((ArchitectureRules.Variants(id)-1)<<3)|7)); requests.Add(0);
            }
            NativeDirectTests(requests.ToArray());
            var native = new Terrain();
            try
            {
                native.AllocateChunk(0,0).State = TerrainChunkState.Valid;
                CommonLib.WorkType = WorkType.Local;
                var terrain = NativeTerrain(native);
                var gateway = new GameTerrainGateway(terrain);
                var copyGrid = native;
                for (int i=0;i<cells.Count;i++) copyGrid.SetCellValueFast(i % 12, 64 + i / 12, 0, Terrain.MakeBlockValue(ArchitectureRules.Id(cells[i].MaterialKey), 0, cells[i].Data));
                var job = new BlueprintCopier.CopyJob(gateway, new CellPos(0,64,0),new CellPos(11,64+(cells.Count-1)/12,0),"古建复制");
                while(!job.Advance(32)) { }
                Check(!job.Result.HasFailure && job.Result.Copied == cells.Count, "native copier preserves detail states and mirrors");
            }
            finally { native.Dispose(); }
            Console.WriteLine("Architecture: " + forms + " forms / " + meshes + " states; max " + maxTriangles + " triangles; codec, geometry, copy, Local/Server/Client and undo passed.");
        }
        finally { foreach (var pair in previous) BlocksManager.Blocks[pair.Key] = pair.Value; VanillaBlockRegistry.Reset(); }
    }
}

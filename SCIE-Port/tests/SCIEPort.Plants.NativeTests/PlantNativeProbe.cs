using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Engine;
using Game;
using Game.NetWork;
using GameEntitySystem;

// Exact Windows target assemblies and memory-only terrain. No player world or game connection.
public static class PlantNativeProbe
{
    const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
    static int checks;
    static void Check(bool ok,string why){checks++;if(!ok)throw new Exception("Plants native: "+why);}
    static void Put(object o,string name,object value)
    {
        for(Type t=o.GetType();t!=null;t=t.BaseType)
        {
            FieldInfo f=t.GetField(name,F)??t.GetField("<"+name+">k__BackingField",F);
            if(f!=null){f.SetValue(o,value);return;}
        }
        throw new Exception(name);
    }
    static object Call(object o,string name,params object[] args){return o.GetType().GetMethod(name,F).Invoke(o,args);}
    public static void Run(Assembly mod)
    {
        Geometry(mod);
        var saved=CommonLib.WorkType;var savedNet=CommonLib.Net;var savedHooks=ModsManager.ModHooks;
        var savedBlocks=(Block[])BlocksManager.Blocks.Clone();
        try
        {
            ModsManager.ModHooks=new Dictionary<string,ModsManager.ModHook>();
            CommonLib.Net=(NetNode)FormatterServices.GetUninitializedObject(typeof(NetNode));
            Put(CommonLib.Net,"PendingPackages",new List<IPackage>());CommonLib.Net.Clients=new Dictionary<int,Client>();
            Generation(mod);
            foreach(WorkType mode in new[]{WorkType.Local,WorkType.Server,WorkType.Client}) Runtime(mod,mode);
        }
        finally{CommonLib.WorkType=saved;CommonLib.Net=savedNet;ModsManager.ModHooks=savedHooks;Array.Copy(savedBlocks,BlocksManager.Blocks,savedBlocks.Length);}
        Console.WriteLine("Plants exact-target geometry/growth/authority/native harvest checks passed: "+checks);
    }
    static void Geometry(Assembly mod)
    {
        var g=mod.GetType("SCIEPort.Features.Plants.PlantGeometry",true);
        var build=g.GetMethod("Build",BindingFlags.Static|BindingFlags.NonPublic);
        var visual=g.GetMethod("VisualData",BindingFlags.Static|BindingFlags.NonPublic);
        int[] ids={903,904,905,906,907,908,909,910,911,912,913,914,915,916,929,930,931,932,934,935,936,937,939,940};
        float appleWidth = MeshWidth(build, 940);
        Check(appleWidth > 1.65f * MeshWidth(build, 904) && appleWidth > 1.65f * MeshWidth(build, 916),
            "apple is visibly broader than a berry bundle");
        Check(appleWidth * .3f >= .12f && appleWidth * .3f <= .14f, "native drop size corresponds to a full-size apple");
        foreach(int id in ids)
        {
            var words=new List<int>();
            for(int d=0;d<16;d++)words.Add(d);
            if (id == 909 || id == 913) for (int stage = 0; stage < 4; stage++) words.Add((stage << 6) | 32);
            if(id==939)
            {
                for(int h=1;h<=2;h++)words.Add(32|7|(1<<6)|(1<<8)|(h<<10));
                words.Add(16|7);
                for(int x=-1;x<=1;x++)for(int z=-1;z<=1;z++)for(int y=2;y<=4;y++)for(int s=0;s<16;s++)
                    words.Add(48|((x+1)<<6)|((z+1)<<8)|(y<<10)|s);
            }
            var keys=new HashSet<int>();
            foreach(int data in words)
            {
                int key=(int)visual.Invoke(null,new object[]{id,data});
                Check(key==(int)visual.Invoke(null,new object[]{id,key}),"canonical visual data is stable");
                if(!keys.Add(key))continue;
                var mesh=(BlockMesh)build.Invoke(null,new object[]{id,key});
                Check(mesh.Vertices.Count>0&&mesh.Indices.Count%3==0&&mesh.Indices.Count/3<=1800,"finite mobile mesh budget "+id+"/"+key);
                for(int i=0;i<mesh.Vertices.Count;i++)
                {
                    var v=mesh.Vertices.Array[i];Vector3 p=v.Position;
                    Check(!float.IsNaN(p.X)&&!float.IsNaN(p.Y)&&!float.IsNaN(p.Z)&&p.X>=-.001f&&p.X<=1.001f&&p.Y>=-.001f&&p.Y<=1.001f&&p.Z>=-.001f&&p.Z<=1.001f,"mesh stays in its occupied cell "+id+"/"+key+": "+p);
                    Check(v.Color.A==255&&v.Face<=5,"opaque shaded faces use the native lighting schema");
                }
                for(int i=0;i<mesh.Indices.Count;i+=3)
                {
                    int a=mesh.Indices.Array[i],b=mesh.Indices.Array[i+1],c=mesh.Indices.Array[i+2];
                    Check(a<mesh.Vertices.Count&&b<mesh.Vertices.Count&&c<mesh.Vertices.Count,"index bounds");
                    Vector3 pa=mesh.Vertices.Array[a].Position,pb=mesh.Vertices.Array[b].Position,pc=mesh.Vertices.Array[c].Position;
                    Check(Vector3.Cross(pb-pa,pc-pa).LengthSquared()>1e-15f,"no degenerate triangles "+id+"/"+key);
                }
            }
            if(id==939)Check(keys.Count<=160,"tree mesh cache stays bounded across ownership/harvest states");
        }
        foreach(string name in new[]{"Apple.ApplePlantBlock","Crops.CornStalkBlock","Flax.FlaxPlantBlock","Sunflower.SunflowerPlantBlock","Bamboo.BambooPlantBlock","KellyPlants.WildBerryPlantBlock","KellyPlants.CloudberryPlantBlock"})
        {
            var b=(Block)Activator.CreateInstance(mod.GetType("SCIEPort.Features."+name,true));
            b.BlockIndex=(int)b.GetType().GetField("Index").GetRawConstantValue();
            for(int d=0;d<16;d++)
            {
                var boxes=b.GetCustomCollisionBoxes(null,Terrain.MakeBlockValue(b.BlockIndex,0,d));
                Check(boxes.Length>0,"native raycast always receives a nonempty box array");
                foreach(var box in boxes)Check(box.Min.X<=box.Max.X&&box.Min.Y<box.Max.Y&&box.Min.Z<=box.Max.Z,"nonzero pickable height");
            }
        }
        // Native DATA is signed after shifting: all newly used role/offset/harvest bits must round-trip.
        for(int data=0;data<16384;data++)Check(Terrain.ExtractData(Terrain.MakeBlockValue(939,15,data))==data,"native DATA/lighting round trip");
        var concurrent=(Block)Activator.CreateInstance(mod.GetType("SCIEPort.Features.Apple.ApplePlantBlock",true));concurrent.BlockIndex=939;
        var results=new BoundingBox[1024][];
        Parallel.For(0,results.Length,i=>results[i]=concurrent.GetCustomCollisionBoxes(null,Terrain.MakeBlockValue(939,0,48|((i%3)<<6)|((i%3)<<8)|(3<<10)|(i%8))));
        for(int i=0;i<results.Length;i++)Check(results[i].Length==1&&results[i][0].Min.Y<results[i][0].Max.Y,"concurrent cold collision/model cache access is safe");
        var food=(Block)Activator.CreateInstance(mod.GetType("SCIEPort.Features.Apple.AppleBlock",true));food.BlockIndex=940;
        var oldFood=BlocksManager.Blocks[940];
        try
        {
            var ground=new GraniteBlock();BlocksManager.Blocks[940]=food;
            for(int face=0;face<6;face++) Check(ground.ShouldGenerateFace(null,face,Terrain.MakeBlockValue(3),Terrain.MakeBlockValue(940)),"legacy placed apple never culls any neighboring cube face");
            Check(food.GetPlacementValue(null,null,Terrain.MakeBlockValue(940),new TerrainRaycastResult()).Value==0,"edible apple rejects placement independently of CSV");
            var recovered=new List<BlockDropValue>();bool debris;
            food.GetDropValues(null,Terrain.MakeBlockValue(940),0,0,recovered,out debris);
            Check(recovered.Count==1&&recovered[0].Value==Terrain.MakeBlockValue(940)&&recovered[0].Count==1,"legacy placed apple can be recovered as one food item");
            BlocksManager.Blocks[940]=new OpaqueFoodFixture();
            Check(!ground.ShouldGenerateFace(null,4,Terrain.MakeBlockValue(3),Terrain.MakeBlockValue(940)),"negative control reproduces the old opaque-food xray face hole");
        }
        finally{BlocksManager.Blocks[940]=oldFood;}
    }
    static float MeshWidth(MethodInfo build, int id)
    {
        var mesh = (BlockMesh)build.Invoke(null, new object[] { id, 0 });
        float min = 1f, max = 0f;
        for (int i = 0; i < mesh.Vertices.Count; i++)
        {
            float x = mesh.Vertices.Array[i].Position.X;
            min = Math.Min(min, x);
            max = Math.Max(max, x);
        }
        return max - min;
    }

    static Block MakeBlock(Assembly mod, string name)
    {
        var block = (Block)Activator.CreateInstance(mod.GetType("SCIEPort.Features." + name, true));
        block.BlockIndex = (int)block.GetType().GetField("Index").GetRawConstantValue();
        BlocksManager.Blocks[block.BlockIndex] = block;
        return block;
    }

    static SubsystemPollableBlockBehavior MakeBehavior(Assembly mod, SubsystemTerrain terrain, string name)
    {
        var behavior = (SubsystemPollableBlockBehavior)Activator.CreateInstance(mod.GetType("SCIEPort.Features." + name, true));
        Put(behavior, "SubsystemTerrain", terrain);
        return behavior;
    }

    static void PlantLifecycle(Assembly mod, SubsystemTerrain subsystem)
    {
        Terrain terrain = subsystem.Terrain;
        const int x = 2, y = 64, z = 3;
        var corn = MakeBlock(mod, "Crops.CornStalkBlock");
        var crop = MakeBehavior(mod, subsystem, "Crops.SubsystemCornBlockBehavior");
        terrain.SetCellValueFast(x, y - 1, z, DirtBlock.Index);
        terrain.SetCellValueFast(x, y + 1, z, Terrain.MakeBlockValue(0, 15, 0));
        terrain.SetCellValueFast(x, y, z, Terrain.MakeBlockValue(corn.BlockIndex, 0, 8 | 6));
        crop.OnPoll(0, x, y, z, 0);
        int data = Terrain.ExtractData(terrain.GetCellValue(x, y, z));
        Check(data == (CommonLib.WorkType == WorkType.Client ? 14 : 15), "wild corn matures on dirt on the authoritative side");
        if (CommonLib.WorkType == WorkType.Client) return;

        terrain.SetCellValueFast(x, y, z, Terrain.MakeBlockValue(corn.BlockIndex, 0, 6));
        crop.OnPoll(0, x, y, z, 0);
        Check(Terrain.ExtractData(terrain.GetCellValue(x, y, z)) == 6, "cultivated corn needs tilled soil");
        terrain.SetCellValueFast(x, y, z, Terrain.MakeBlockValue(corn.BlockIndex, 0, 14));
        terrain.SeasonTemperature = 0;
        crop.OnPoll(0, x, y, z, 0);
        Check(Terrain.ExtractData(terrain.GetCellValue(x, y, z)) == 14, "frost pauses warm crops");
        terrain.SeasonTemperature = 12;

        var cactus = MakeBlock(mod, "KellyPlants.CactusFruitBlock");
        var fruit = MakeBehavior(mod, subsystem, "KellyPlants.SubsystemKellyFruitBlockBehavior");
        terrain.SetCellValueFast(x, y - 1, z, CactusBlock.Index);
        terrain.SetCellValueFast(x, y, z, Terrain.MakeBlockValue(cactus.BlockIndex));
        AssertHarvestCycle(subsystem, cactus, fruit, x, y, z, cactus.BlockIndex, 3);

        var cattail = MakeBlock(mod, "KellyPlants.CattailBlock");
        BlocksManager.Blocks[WaterBlock.Index] = new WaterBlock();
        terrain.SetCellValueFast(x, y - 1, z, DirtBlock.Index);
        terrain.SetCellValueFast(x + 1, y - 1, z, WaterBlock.Index);
        terrain.SetCellValueFast(x, y, z, Terrain.MakeBlockValue(cattail.BlockIndex));
        AssertHarvestCycle(subsystem, cattail, fruit, x, y, z, 906, 3);

        var fungus = MakeBlock(mod, "KellyPlants.MushroomPatchBlock");
        var mushroom = MakeBehavior(mod, subsystem, "KellyPlants.SubsystemKellyMushroomBlockBehavior");
        terrain.SetCellValueFast(x, y + 1, z, Terrain.MakeBlockValue(0, 8, 0));
        terrain.SetCellValueFast(x, y, z, Terrain.MakeBlockValue(fungus.BlockIndex, 0, 32));
        AssertHarvestCycle(subsystem, fungus, mushroom, x, y, z, 910, 3);

        var berry = MakeBlock(mod, "KellyPlants.WildBerryPlantBlock");
        var berries = MakeBehavior(mod, subsystem, "KellyPlants.SubsystemWildBerryBlockBehavior");
        terrain.SetCellValueFast(x, y + 1, z, Terrain.MakeBlockValue(0, 15, 0));
        terrain.SetCellValueFast(x, y, z, Terrain.MakeBlockValue(berry.BlockIndex, 0, 1));
        AssertHarvestCycle(subsystem, berry, berries, x, y, z, 904, 4);
        terrain.SetCellValueFast(x, y, z, 0);
    }

    static void AssertHarvestCycle(SubsystemTerrain subsystem, Block plant, SubsystemBlockBehavior behavior,
        int x, int y, int z, int fruit, int growthSteps)
    {
        Terrain terrain = subsystem.Terrain;
        int ripe = terrain.GetCellValue(x, y, z);
        var drops = new List<BlockDropValue>();
        bool debris;
        plant.GetDropValues(subsystem, ripe, 0, 0, drops, out debris);
        Check(drops.Count == 1 && Terrain.ExtractContents(drops[0].Value) == fruit, "ripe harvest " + plant.BlockIndex);
        int replacement = 0;
        var drop = drops[0];
        behavior.OnItemHarvested(x, y, z, ripe, ref drop, ref replacement);
        Check(Terrain.ExtractContents(replacement) == plant.BlockIndex && replacement != ripe, "harvest keeps a dormant plant " + plant.BlockIndex);
        terrain.SetCellValueFast(x, y, z, replacement);
        drops.Clear();
        plant.GetDropValues(subsystem, replacement, 0, 0, drops, out debris);
        Check(drops.Count == 0, "empty plant yields no second harvest " + plant.BlockIndex);
        for (int tick = 0; tick < growthSteps; tick++)
            ((SubsystemPollableBlockBehavior)behavior).OnPoll(0, x, y, z, 0);
        plant.GetDropValues(subsystem, terrain.GetCellValue(x, y, z), 0, 0, drops, out debris);
        Check(drops.Count == 1 && Terrain.ExtractContents(drops[0].Value) == fruit, "native polling restores the crop " + plant.BlockIndex);
    }

    static void Generation(Assembly mod)
    {
        string[] names = { "Apple.ApplePlantBlock", "Bamboo.BambooPlantBlock", "Crops.CornStalkBlock", "Flax.FlaxPlantBlock",
            "Sunflower.SunflowerPlantBlock", "KellyPlants.SoybeanBlock", "KellyPlants.WildBerryPlantBlock", "KellyPlants.CloudberryPlantBlock",
            "KellyPlants.CattailBlock", "KellyPlants.CactusFruitBlock", "KellyPlants.MushroomPatchBlock", "KellyPlants.FlyAgaricPatchBlock" };
        foreach (string name in names) MakeBlock(mod, name);
        var wood = MakeBlock(mod, "Apple.AppleWoodBlock");
        var leaves = MakeBlock(mod, "Apple.AppleLeavesBlock");
        var fruitingLeaves = MakeBlock(mod, "Apple.AppleFruitingLeavesBlock");
        Check(wood is WoodBlock && wood.BlockIndex == 978, "apple logs use the native orientable wood type");
        Check(leaves is LeavesBlock24 && leaves.BlockIndex == 979, "ordinary foliage uses the native leaf type");
        Check(fruitingLeaves is LeavesBlock24 && fruitingLeaves.BlockIndex == 980, "ripe foliage has its own native leaf block");
        Check(wood.GetFaceTextureSlot(4, Terrain.MakeBlockValue(978)) == 17 &&
            wood.GetFaceTextureSlot(0, Terrain.MakeBlockValue(978)) == 16, "upright log has end grain at the top");
        for (int face = 0; face < 6; face++)
        {
            int aligned = Terrain.MakeBlockValue(978, 0, WoodBlock.SetCutFace(0, face));
            Check(wood.GetFaceTextureSlot(face, aligned) == 17 &&
                wood.GetFaceTextureSlot(CellFace.OppositeFace(face), aligned) == 17, "native axes rotate the apple log's cut faces");
        }
        Check(leaves.GetFaceTextureSlot(0, Terrain.MakeBlockValue(979)) == 18 &&
            leaves.GetFaceTextureSlot(0, Terrain.MakeBlockValue(979, 0, 1)) == 19 &&
            leaves.GetFaceTextureSlot(0, Terrain.MakeBlockValue(979, 0, 4)) == 20 &&
            fruitingLeaves.GetFaceTextureSlot(0, Terrain.MakeBlockValue(980)) == 21, "each canopy phase is visibly distinct");
        var placement = fruitingLeaves.GetPlacementValue(null, null, Terrain.MakeBlockValue(980, 0, 48 | 8192), new TerrainRaycastResult());
        Check(placement.Value == Terrain.MakeBlockValue(980), "player-placed fruiting leaves clear tree ownership");
        BlocksManager.Blocks[OakLeavesBlock.Index] = new OakLeavesBlock();
        BlocksManager.Blocks[WaterBlock.Index] = new WaterBlock();
        var type = mod.GetType("SCIEPort.Features.Plants.PlantTerrainGenerator", true);
        var constructor = type.GetConstructor(F, null, new[] { typeof(ITerrainContentsGenerator), typeof(SubsystemGameInfo) }, null);
        var settings = (WorldSettings)FormatterServices.GetUninitializedObject(typeof(WorldSettings));
        settings.TimeOfYear = .35f;
        settings.AreSeasonsChanging = true;
        var info = new SubsystemGameInfo { WorldSeed = 987651, WorldSettings = settings };
        foreach (int scenario in new[] { 0, 1, 2, 3, 4 })
        {
            var generator = (ITerrainContentsGenerator)constructor.Invoke(new object[] { new HabitatFixture(scenario), info });
            int[] totals = new int[1024];
            for (int seed = 0; seed < 128; seed++)
            {
                using (var terrain = new Terrain())
                {
                    var chunk = terrain.AllocateChunk(seed, seed % 7);
                    CommonLib.WorkType = WorkType.Server;
                    generator.GenerateChunkContentsPass4(chunk);
                    int[] count = CountPlants(chunk);
                    Check(count[939] == 0 && (count[978] == 0 && count[979] + count[980] == 0 ||
                        count[978] == 3 && count[979] == 14 && count[980] == 4), "natural apple tree has three logs and the correct autumn leaves");
                    Check(count[932] <= 3 && count[929] <= 2 && count[934] <= 3 && count[936] <= 3 &&
                        count[911] <= 2 && count[903] <= 3 && count[915] <= 3 && count[905] <= 4 &&
                        count[907] <= 2 && count[909] <= 2 && count[913] <= 1, "per-chunk limits");
                    for (int id = 0; id < count.Length; id++) totals[id] += count[id];
                }
            }
            string evidence = " scenario " + scenario;
            if (scenario == 0) {
                foreach (int id in new[] { 978, 932, 929, 934, 936, 911, 903 })
                    Check(totals[id] > 0, "viable meadow habitat for " + id);
                Check(totals[915] + totals[905] + totals[907] == 0, "meadow excludes bog and desert species");
            }
            if (scenario == 1) Check(totals[915] > 0 && totals[978] == 0, "cool bog only supports cold species" + evidence);
            if (scenario == 2) Check(totals[905] > 0, "inland shoreline supports cattails" + evidence);
            if (scenario == 3) Check(totals[907] > 0 && totals[929] == 0, "dry cactus tops support cactus fruit" + evidence);
            if (scenario == 4) Check(totals[909] > 0 && totals[913] > 0 && totals[978] == 0, "fungi seed after vanilla canopy generation" + evidence);
            var selected = new[] {978,979,980,932,929,934,936,911,903,915,905,907,909,913};
            Console.WriteLine("Habitat " + scenario + " over 128 chunks, " + string.Join(", ", Array.ConvertAll(selected, id => id + "=" + totals[id])));
        }

        var wrapper = (ITerrainContentsGenerator)constructor.Invoke(new object[] { new HabitatFixture(0), info });
        using (var first = new Terrain())
        using (var second = new Terrain())
        using (var client = new Terrain())
        {
            var a = first.AllocateChunk(8, 2);
            var b = second.AllocateChunk(8, 2);
            var c = client.AllocateChunk(8, 2);
            wrapper.GenerateChunkContentsPass4(a);
            wrapper.GenerateChunkContentsPass4(b);
            for (int i = 0; i < a.Cells.Length; i++) Check(a.Cells[i] == b.Cells[i], "same seed creates identical terrain");
            CommonLib.WorkType = WorkType.Client;
            wrapper.GenerateChunkContentsPass4(c);
            Check(CountPlants(c)[978] + CountPlants(c)[979] + CountPlants(c)[980] + CountPlants(c)[932] + CountPlants(c)[903] + CountPlants(c)[934] == 0 &&
                Terrain.ExtractContents(c.GetCellValueFast(0, 65, 0)) == GrassBlock.Index, "client delegates original generation without adding mod plants");
        }
    }

    static int[] CountPlants(TerrainChunk chunk)
    {
        var counts = new int[1024];
        for (int x = 0; x < 16; x++)
        for (int z = 0; z < 16; z++)
        for (int y = 64; y <= 80; y++) counts[Terrain.ExtractContents(chunk.GetCellValueFast(x, y, z))]++;
        return counts;
    }

    private sealed class HabitatFixture : ITerrainContentsGenerator
    {
        private readonly int kind;
        public HabitatFixture(int kind) { this.kind = kind; }
        public int OceanLevel { get { return 64; } }
        public Vector3 FindCoarseSpawnPosition() { return Vector3.Zero; }
        public float CalculateOceanShoreDistance(float x, float z) { return 40f; }
        public float CalculateHeight(float x, float z) { return 65f; }
        public int CalculateTemperature(float x, float z) { return kind == 1 ? 3 : kind == 3 ? 14 : 10; }
        public int CalculateHumidity(float x, float z) { return kind == 3 ? 3 : 10; }
        public float CalculateMountainRangeFactor(float x, float z) { return 0f; }
        public void GenerateChunkContentsPass1(TerrainChunk chunk) { }
        public void GenerateChunkContentsPass2(TerrainChunk chunk) { }
        public void GenerateChunkContentsPass3(TerrainChunk chunk) { }
        public void GenerateChunkContentsPass4(TerrainChunk chunk)
        {
            for (int x = 0; x < 16; x++)
            for (int z = 0; z < 16; z++)
            {
                chunk.SetHumidityFast(x, z, CalculateHumidity(x, z));
                chunk.SetTemperatureFast(x, z, CalculateTemperature(x, z));
                chunk.SetCellValueFast(x, 65, z, kind == 3 ? SandBlock.Index : GrassBlock.Index);
                if (kind == 2 && z < 8) chunk.SetCellValueFast(x, 65, z, WaterBlock.Index);
                if (kind == 3 && (x + z) % 5 == 0) chunk.SetCellValueFast(x, 66, z, CactusBlock.Index);
                if (kind == 4)
                {
                    chunk.SetCellValueFast(x, 70, z, OakLeavesBlock.Index);
                    chunk.SetCellValueFast(x, 71, z, OakLeavesBlock.Index);
                }
            }
        }
    }

    private sealed class OpaqueFoodFixture:FlatBlock { }
    static void Runtime(Assembly mod,WorkType mode)
    {
        CommonLib.WorkType=mode;
        using(var cells=new Terrain())
        {
            TerrainChunk chunk=cells.AllocateChunk(0,0);chunk.State=TerrainChunkState.Valid;
            cells.SeasonTemperature=12;
            cells.SeasonHumidity=11;
            var terrain=(SubsystemTerrain)FormatterServices.GetUninitializedObject(typeof(SubsystemTerrain));Put(terrain,"Terrain",cells);
            terrain.m_modifiedCells=new Dictionary<Point3,bool>();
            var updater=(TerrainUpdater)FormatterServices.GetUninitializedObject(typeof(TerrainUpdater));Put(updater,"m_terrain",cells);Put(terrain,"TerrainUpdater",updater);
            var behaviors=new SubsystemBlockBehaviors();behaviors.m_blockBehaviorsByContents=new SubsystemBlockBehavior[BlocksManager.Blocks.Length][];
            for(int i=0;i<behaviors.m_blockBehaviorsByContents.Length;i++)behaviors.m_blockBehaviorsByContents[i]=new SubsystemBlockBehavior[0];
            Put(terrain,"m_subsystemBlockBehaviors",behaviors);
            var runtime=(SubsystemPollableBlockBehavior)Activator.CreateInstance(mod.GetType("SCIEPort.Features.Apple.SubsystemAppleBlockBehavior",true));
            Put(runtime,"SubsystemTerrain",terrain);var seasons=new SubsystemSeasons{Season=Season.Spring};Put(runtime,"m_seasons",seasons);
            foreach (int id in new[] { 939, 940, 978, 979, 980 })
                behaviors.m_blockBehaviorsByContents[id] = new[] { runtime };
            var apple=(Block)Activator.CreateInstance(mod.GetType("SCIEPort.Features.Apple.ApplePlantBlock",true));apple.BlockIndex=939;BlocksManager.Blocks[939]=apple;
            PlantLifecycle(mod, terrain);
            const int y=64;cells.SetCellValueFast(8,y-1,8,Terrain.MakeBlockValue(DirtBlock.Index));
            cells.SetCellValueFast(8,y,8,Terrain.MakeBlockValue(939,0,0));cells.SetCellValueFast(8,y+1,8,Terrain.MakeBlockValue(0,15,0));
            for(int s=0;s<6;s++){Call(runtime,"Update",.016f);runtime.OnPoll(Terrain.MakeBlockValue(939),8,y,8,0);}
            int grown=cells.GetCellValue(8,y,8);
            Check((Terrain.ExtractData(grown)&7)==(mode==WorkType.Client?0:6),"native poll grows only on authoritative sides "+mode);
            if(mode==WorkType.Client)
            {
                var clientDrop=new BlockDropValue{Value=Terrain.MakeBlockValue(940),Count=2};int replacement=0;
                runtime.OnItemHarvested(8,66,8,Terrain.MakeBlockValue(939,0,48|15),ref clientDrop,ref replacement);
                Check(replacement==0,"client cannot mutate harvest state");
                cells.SetCellValueFast(5,y,8,Terrain.MakeBlockValue(939,0,16|7));
                runtime.OnPoll(0,5,y,8,0);
                Check(cells.GetCellContents(5,y,8)==939,"client cannot locally migrate legacy tree cells");
                return;
            }
            // Obstruction and an unloaded adjoining chunk both leave the original sapling unchanged.
            cells.SetCellValueFast(9,y+2,8,3);Call(runtime,"Update",.016f);runtime.OnPoll(grown,8,y,8,0);
            Check(Terrain.ExtractData(cells.GetCellValue(8,y,8))==6&&cells.GetCellValue(9,y+2,8)==3,"occupied canopy cancels the entire expansion");
            cells.SetCellValueFast(9,y+2,8,0);
            Call(runtime,"Update",.016f);runtime.OnPoll(grown,8,y,8,0);
            Check(cells.GetCellContents(8,y,8)==978 && Terrain.ExtractData(cells.GetCellValue(8,y,8))==4,"sapling becomes independently identified wood");
            int logCount=0, leafCount=0;
            for(int x=7;x<=9;x++)
            for(int h=0;h<5;h++)
            for(int z=7;z<=9;z++)
            {
                int content = cells.GetCellContents(x,y+h,z);
                if (content == 978) logCount++;
                if (content == 979) leafCount++;
            }
            Check(logCount==3 && leafCount==18,"three logs and eighteen leaf cells are committed");
            cells.SetCellValueFast(12,y-1,8,Terrain.MakeBlockValue(DirtBlock.Index));cells.SetCellValueFast(12,y,8,Terrain.MakeBlockValue(939,0,6));cells.SetCellValueFast(12,y+1,8,Terrain.MakeBlockValue(0,15,0));
            runtime.OnPoll(0,12,y,8,0);Check(Terrain.ExtractData(cells.GetCellValue(12,y,8))==6,"second tree waits when the frame budget is exhausted");
            Call(runtime,"Update",.016f);runtime.OnPoll(0,12,y,8,0);Check(cells.GetCellContents(12,y,8)==978,"new frame resumes deferred growth");
            int fertile=SoilBlock.SetNitrogen(SoilBlock.SetHydration(0,true),2);
            cells.SetCellValueFast(4,y-1,12,Terrain.MakeBlockValue(SoilBlock.Index,15,fertile));
            cells.SetCellValueFast(4,y,12,Terrain.MakeBlockValue(939,15,6));cells.SetCellValueFast(4,y+1,12,Terrain.MakeBlockValue(0,15,0));
            Call(runtime,"Update",.016f);runtime.OnPoll(0,4,y,12,0);
            Check(cells.GetCellContents(4,y,12)==978,"fertile illuminated soil commits without a false light-signature mismatch");
            Check(SoilBlock.GetNitrogen(Terrain.ExtractData(cells.GetCellValue(4,y-1,12)))==1,"tree completion consumes one nitrogen exactly once");
            Check(!(bool)runtime.GetType().GetField("m_faulted",F).GetValue(runtime),"normal native light reset does not halt growth");
            cells.SetCellValueFast(15,y-1,8,Terrain.MakeBlockValue(DirtBlock.Index));cells.SetCellValueFast(15,y,8,Terrain.MakeBlockValue(939,0,6));cells.SetCellValueFast(15,y+1,8,Terrain.MakeBlockValue(0,15,0));
            Call(runtime,"Update",.016f);runtime.OnPoll(0,15,y,8,0);Check(Terrain.ExtractData(cells.GetCellValue(15,y,8))==6,"unloaded cross-chunk canopy defers without partial writes");
            // Real GetDropValues -> OnItemHarvested -> ChangeCell lifecycle, with native DATA and network queue.
            var wood = BlocksManager.Blocks[978];
            var leaves = BlocksManager.Blocks[979];
            var fruiting = BlocksManager.Blocks[980];
            int leafValue = cells.GetCellValue(9,y+2,8);
            int data = Terrain.ExtractData(leafValue);
            seasons.Season = Season.Autumn;
            cells.SetCellValueFast(9,y+2,8,Terrain.MakeBlockValue(979,0,(data&~15)|6));
            cells.SetCellValueFast(10,y+2,8,Terrain.MakeBlockValue(0,15,0));
            Call(runtime,"Update",.016f);
            runtime.OnPoll(0,9,y+2,8,0);
            leafValue = cells.GetCellValue(9,y+2,8);
            Check(Terrain.ExtractContents(leafValue)==980,"ripe phase becomes the fruiting block");
            var drops=new List<BlockDropValue>();bool debris;
            fruiting.GetDropValues(terrain,leafValue,0,0,drops,out debris);
            Check(drops.Count==1 && Terrain.ExtractContents(drops[0].Value)==940 && drops[0].Count==2,"ripe canopy yields two apples");
            int next=0;var drop=drops[0];runtime.OnItemHarvested(9,y+2,8,leafValue,ref drop,ref next);
            Check(Terrain.ExtractContents(next)==979 && (Terrain.ExtractData(next)&8192)!=0 && (Terrain.ExtractData(next)&8)==0,"harvest turns fruiting leaves into ordinary leaves and locks the branch");
            terrain.ChangeCell(9,y+2,8,next,true,null);
            runtime.OnPoll(0,9,y+2,8,0);
            Check(cells.GetCellContents(9,y+2,8)==979,"harvested branch cannot fruit twice in autumn");
            seasons.Season = Season.Spring;
            runtime.OnPoll(0,9,y+2,8,0);
            Check((Terrain.ExtractData(cells.GetCellValue(9,y+2,8))&8192)==0,"next spring clears the previous harvest lock");
            drops.Clear();
            leaves.GetDropValues(terrain,next,0,0,drops,out debris);
            Check(drops.TrueForAll(item => Terrain.ExtractContents(item.Value)!=940),"plain leaves cannot drop apples");
            drops.Clear();
            wood.GetDropValues(terrain,cells.GetCellValue(8,y,8),0,0,drops,out debris);
            Check(drops.Count==2 && drops[0].Value==Terrain.MakeBlockValue(978) &&
                drops[1].Value==Terrain.MakeBlockValue(939),"grown base yields native wood and a sapling, clearing ownership from the log");
            drops.Clear();
            wood.GetDropValues(terrain,Terrain.MakeBlockValue(978,0,2),0,0,drops,out debris);
            Check(drops.Count==1 && drops[0].Value==Terrain.MakeBlockValue(978),"manual logs drop their upright inventory value without saplings");

            cells.SetCellValueFast(8,y+1,8,0);
            Call(runtime,"Update",.016f);runtime.OnPoll(0,9,y+2,8,0);
            Check(cells.GetCellValue(9,y+2,8)==Terrain.MakeBlockValue(979),"lost trunk detaches a fruit branch for vanilla decay");
            var info = (SubsystemGameInfo)FormatterServices.GetUninitializedObject(typeof(SubsystemGameInfo));
            info.WorldSettings = (WorldSettings)FormatterServices.GetUninitializedObject(typeof(WorldSettings));
            var decay = new SubsystemWoodBlockBehavior();
            Put(decay,"SubsystemTerrain",terrain);
            decay.m_subsystemGameInfo=info;
            var isolatedLeaf=new Point3(2,90,7);
            cells.SetCellValueFast(isolatedLeaf.X,isolatedLeaf.Y,isolatedLeaf.Z,Terrain.MakeBlockValue(979));
            decay.OnBlockRemoved(Terrain.MakeBlockValue(978),0,2,89,7);
            Check(decay.m_leavesToCheck.Contains(isolatedLeaf),"wood removal queues apple leaves through vanilla behavior");
            decay.DecayLeavesIfNeeded(isolatedLeaf);
            Check(cells.GetCellContents(isolatedLeaf.X,isolatedLeaf.Y,isolatedLeaf.Z)==0,"vanilla calculates and removes unsupported apple leaves");
            Check(cells.GetCellContents(12,y,8)==978,"decay does not touch another tree");

            var legacyRoot = new Point3(3,82,4);
            int legacyLeaf = Terrain.MakeBlockValue(939,0,48|(2<<6)|(1<<8)|(2<<10)|15);
            cells.SetCellValueFast(3,82,4,Terrain.MakeBlockValue(939,0,16|7));
            cells.SetCellValueFast(3,83,4,Terrain.MakeBlockValue(939,0,32|7|(1<<6)|(1<<8)|(1<<10)));
            cells.SetCellValueFast(3,84,4,Terrain.MakeBlockValue(939,0,32|7|(1<<6)|(1<<8)|(2<<10)));
            cells.SetCellValueFast(4,84,4,legacyLeaf);
            Call(runtime,"Update",.016f);
            // Migrate the leaf first to exercise a partially converted save.
            runtime.OnPoll(0,4,84,4,0);
            Check(cells.GetCellContents(4,84,4)==980 &&
                Terrain.ExtractData(cells.GetCellValue(4,84,4))==Terrain.ExtractData(legacyLeaf), "legacy fruit and branch ownership survive migration");
            for(int h=0;h<3;h++)runtime.OnPoll(0,legacyRoot.X,legacyRoot.Y+h,legacyRoot.Z,0);
            Check(cells.GetCellValue(3,82,4)==Terrain.MakeBlockValue(978,0,4) &&
                cells.GetCellContents(3,83,4)==978 && cells.GetCellContents(3,84,4)==978,"legacy tree stem migrates in place");
            cells.SetCellValueFast(2,84,4,Terrain.MakeBlockValue(939,0,48|(1<<8)|(2<<10)|8192));
            runtime.OnPoll(0,2,84,4,0);
            Check(cells.GetCellContents(2,84,4)==979 && (Terrain.ExtractData(cells.GetCellValue(2,84,4))&8192)!=0,
                "legacy harvest lock survives wood-first migration");
            Call(runtime,"Update",.016f);runtime.OnPoll(0,0,0,0,0);runtime.OnPoll(0,0,255,0,0);runtime.OnPoll(0,int.MaxValue,64,0,0);
            Check(true,"world boundary polls safely return");
        }
    }
}

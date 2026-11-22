using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using Engine;
using Game;
using Game.NetWork;
using GameEntitySystem;
using SCIEPort.Features.Scenery;

// Exact Windows target assemblies and memory-only terrain. No player world or game connection.
public static class SceneryNativeProbe
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    const BindingFlags S = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    const int OceanLevel = 64;
    const int WorldSeed = 20261122;
    static int checks;

    static void Check(bool ok, string why)
    {
        checks++;
        if (!ok) throw new Exception("Scenery native: " + why);
    }

    public static void Run(Assembly mod)
    {
        var savedWork = CommonLib.WorkType;
        var savedBlocks = (Block[])BlocksManager.Blocks.Clone();
        try
        {
            RegisterBlocks(mod);
            Geometry();
            var settings = (WorldSettings)FormatterServices.GetUninitializedObject(typeof(WorldSettings));
            settings.TimeOfYear = .35f;
            settings.AreSeasonsChanging = true;
            var info = new SubsystemGameInfo { WorldSeed = WorldSeed, WorldSettings = settings };

            CommonLib.WorkType = WorkType.Server;
            TestBudgets(OceanHeight, "ocean");
            TestBudgets(LandHeight, "land");
            TestOceanShipwreck(Wrapper(mod, new Fixture(true), info));
            TestLandLandmarks(Wrapper(mod, new Fixture(false), info));
            TestAuthority(mod, info);
            TestDeterminism(mod, info);
            TestObstacleGuard(mod, info);
            TestExceptionIsolation(mod, info);
        }
        finally
        {
            CommonLib.WorkType = savedWork;
            Array.Copy(savedBlocks, BlocksManager.Blocks, savedBlocks.Length);
        }
        Console.WriteLine("Scenery exact-target selection/generation/safety checks passed: " + checks);
    }

    static void RegisterBlocks(Assembly mod)
    {
        string[] names = { "Cloudstone", "Hull", "Envelope", "Crystal", "Brass", "Sail" };
        int[] ids = { 880, 881, 882, 883, 884, 885 };
        for (int i = 0; i < names.Length; i++)
        {
            Type type = mod.GetType("SCIEPort.Features.Scenery.Scenery" + names[i] + "Block", true);
            Block block = (Block)Activator.CreateInstance(type);
            int id = (int)type.GetField("Index").GetRawConstantValue();
            Check(id == ids[i], "declared index for Scenery" + names[i] + "Block");
            block.BlockIndex = id;
            BlocksManager.Blocks[id] = block;
            Check(BlocksManager.Blocks[id] is SceneryCubeBlock, "registered scenery cube " + id);
        }
    }

    static void Geometry()
    {
        var cloudstone = (SceneryCubeBlock)BlocksManager.Blocks[880];
        var hull = (SceneryCubeBlock)BlocksManager.Blocks[881];
        var envelope = (SceneryCubeBlock)BlocksManager.Blocks[882];
        var crystal = (SceneryCubeBlock)BlocksManager.Blocks[883];
        var brass = (SceneryCubeBlock)BlocksManager.Blocks[884];
        var sail = (SceneryCubeBlock)BlocksManager.Blocks[885];

        for (int face = 0; face < 6; face++)
        {
            Check(cloudstone.GetFaceTextureSlot(face, Terrain.MakeBlockValue(880)) == 5, "cloudstone atlas slot");
            Check(brass.GetFaceTextureSlot(face, Terrain.MakeBlockValue(884)) == 8, "brass atlas slot");
            Check(sail.GetFaceTextureSlot(face, Terrain.MakeBlockValue(885)) == 9, "sail atlas slot");
            Check(hull.GetFaceTextureSlot(face, Terrain.MakeBlockValue(881)) == (face == 4 ? 1 : 0), "hull top/side atlas slot");
        }
        for (int data = 0; data <= 2; data++)
        {
            Check(envelope.GetFaceTextureSlot(0, Terrain.MakeBlockValue(882, 0, data)) == 2 + data, "envelope skin variant " + data);
        }
        Check(envelope.GetFaceTextureSlot(0, Terrain.MakeBlockValue(882, 0, 7)) == 4, "out-of-range envelope data clamps");
        Check(crystal.GetFaceTextureSlot(0, Terrain.MakeBlockValue(883, 0, 0)) == 6, "cyan crystal atlas slot");
        Check(crystal.GetFaceTextureSlot(0, Terrain.MakeBlockValue(883, 0, 1)) == 7, "violet crystal atlas slot");
        Check(crystal.GetEmittedLightAmount(Terrain.MakeBlockValue(883, 0, 0)) == 12, "cyan crystal light");
        Check(crystal.GetEmittedLightAmount(Terrain.MakeBlockValue(883, 0, 1)) == 10, "violet crystal light");

        int variants = 0;
        foreach (int value in envelope.GetCreativeValues())
        {
            variants++;
            Check(Terrain.ExtractContents(value) == 882, "envelope creative content");
            Check(Terrain.ExtractData(value) >= 0 && Terrain.ExtractData(value) <= 2, "envelope creative data");
        }
        Check(variants == 3, "envelope exposes three skins");

        var drops = new List<BlockDropValue>();
        bool debris;
        envelope.GetDropValues(null, Terrain.MakeBlockValue(882, 0, 2), 0, 0, drops, out debris);
        Check(drops.Count == 1 && Terrain.ExtractContents(drops[0].Value) == 882 && Terrain.ExtractData(drops[0].Value) == 2,
            "envelope drops keep their skin");
        drops.Clear();
        crystal.GetDropValues(null, Terrain.MakeBlockValue(883, 0, 1), 0, 0, drops, out debris);
        Check(drops.Count == 1 && Terrain.ExtractContents(drops[0].Value) == 883 && Terrain.ExtractData(drops[0].Value) == 1,
            "crystal drops keep their colour");
    }

    static void TestBudgets(Func<int, int, float> height, string name)
    {
        for (int chunkZ = -40; chunkZ <= 40; chunkZ++)
        {
            for (int chunkX = -40; chunkX <= 40; chunkX++)
            {
                var sites = new List<ScenerySite>();
                SceneryRules.VisitSites(WorldSeed, chunkX, chunkZ, OceanLevel, height, sites);
                Check(sites.Count <= SceneryRules.MaxSitesPerChunk, name + " site cap");
                for (int i = 0; i < sites.Count; i++)
                {
                    Check(sites[i].Height > 0 && sites[i].Height <= 64, name + " landmark height budget");
                    Check(sites[i].MaxY <= SceneryRules.MaxWorldHeight, name + " landmark build ceiling");
                }
            }
        }
    }

    static void TestOceanShipwreck(ITerrainContentsGenerator generator)
    {
        ScenerySite site = FindSite(SceneryKind.Shipwreck, true);
        using (var terrain = new Terrain())
        {
            var chunks = GenerateSite(terrain, generator, site, true);
            int hull = CountBlock(chunks, 881);
            int planks = CountBlock(chunks, 21);
            int wood = CountBlock(chunks, 9);
            Check(hull > 0, "shipwreck hull is written");
            Check(planks > 0, "shipwreck deck is written");
            Check(wood > 0, "shipwreck mast is written");

            int customs = 0;
            int water = 0;
            int wetHull = 0;
            foreach (TerrainChunk chunk in chunks.Values)
            {
                for (int x = 0; x < 16; x++)
                {
                    for (int z = 0; z < 16; z++)
                    {
                        Check(Terrain.ExtractContents(chunk.GetCellValueFast(x, 50, z)) == SandBlock.Index,
                            "solid sea floor is never replaced");
                        for (int y = 0; y < 256; y++)
                        {
                            int contents = Terrain.ExtractContents(chunk.GetCellValueFast(x, y, z));
                            if (contents == WaterBlock.Index) water++;
                            if (contents == 881 && y >= 51 && y <= 63) wetHull++;
                            if (contents < 880 || contents > 885) continue;
                            customs++;
                            Check(y >= 1, "shipwreck scenery stays above the world floor");
                            Check(y <= OceanLevel - 1, "shipwreck scenery stays under the ocean surface");
                        }
                    }
                }
            }
            Check(customs > 0, "shipwreck uses authored scenery blocks");
            Check(wetHull > 0, "hull replaces water cells");
            Check(water > 0, "untouched sea water remains");
            Check(customs < 20000, "shipwreck stays inside the per-chunk budget");
        }
    }

    static void TestLandLandmarks(ITerrainContentsGenerator generator)
    {
        ScenerySite airship = FindSite(SceneryKind.Airship, false);
        using (var terrain = new Terrain())
        {
            var chunks = GenerateSite(terrain, generator, airship, true);
            int envelope = CountBlock(chunks, 882);
            Check(envelope > 0, "airship envelope is written");
            Check(CountBlockWithData(chunks, 882, airship.Variant) == envelope, "airship keeps its skin variant");
            Check(CountBlock(chunks, 884) > 0, "airship brass fittings are written");
            Check(CountBlock(chunks, 883) > 0, "airship crystal lamp is written");
            Check(CountBlock(chunks, 15) > 0, "airship windows are written");
            Check(CountBlock(chunks, 21) > 0, "airship deck and tail are written");
            Check(CountBlock(chunks, 954) == 1, "airship carries exactly one prize display");
            AssertAirClearance(chunks, "airship");
            AssertLandSoil(chunks, "airship");
        }

        ScenerySite island = FindSite(SceneryKind.SkyIsland, false);
        using (var terrain = new Terrain())
        {
            var chunks = GenerateSite(terrain, generator, island, true);
            Check(CountBlock(chunks, 880) > 0, "sky island cloudstone is written");
            Check(CountBlock(chunks, 8) > 0, "sky island grass cap is written");
            Check(CountBlock(chunks, 2) > 0, "sky island soil is written");
            Check(CountBlock(chunks, 9) > 0, "sky island trunk is written");
            Check(CountBlock(chunks, 12) > 0, "sky island canopy is written");
            Check(CountBlock(chunks, 884) > 0, "sky island prize altar is written");
            Check(CountBlock(chunks, 954) == 1, "sky island carries exactly one prize display");
            AssertAirClearance(chunks, "sky island");
            AssertLandSoil(chunks, "sky island");
        }
    }

    static void TestAuthority(Assembly mod, SubsystemGameInfo info)
    {
        ScenerySite site = FindSite(SceneryKind.Airship, false);
        int chunkX = SceneryRules.FloorDiv(site.X, 16);
        int chunkZ = SceneryRules.FloorDiv(site.Z, 16);
        foreach (WorkType mode in new[] { WorkType.Client, WorkType.Server, WorkType.Local })
        {
            CommonLib.WorkType = mode;
            using (var terrain = new Terrain())
            {
                TerrainChunk chunk = terrain.AllocateChunk(chunkX, chunkZ);
                Wrapper(mod, new Fixture(false), info).GenerateChunkContentsPass4(chunk);
                if (mode == WorkType.Client)
                {
                    Check(CountCustom(chunk) == 0, "clients never generate landmarks");
                    Check(Terrain.ExtractContents(chunk.GetCellValueFast(0, 72, 0)) == GrassBlock.Index,
                        "clients still receive native terrain");
                }
                else
                {
                    Check(CountCustom(chunk) > 0, "authoritative side generates landmarks: " + mode);
                }
            }
        }

        var saved = info.WorldSettings.TerrainGenerationMode;
        try
        {
            info.WorldSettings.TerrainGenerationMode = TerrainGenerationMode.Nihility;
            CommonLib.WorkType = WorkType.Server;
            using (var terrain = new Terrain())
            {
                TerrainChunk chunk = terrain.AllocateChunk(chunkX, chunkZ);
                Wrapper(mod, new Fixture(false), info).GenerateChunkContentsPass4(chunk);
                Check(CountCustom(chunk) == 0, "nihility worlds stay empty");
            }
        }
        finally
        {
            info.WorldSettings.TerrainGenerationMode = saved;
            CommonLib.WorkType = WorkType.Server;
        }
    }

    static void TestDeterminism(Assembly mod, SubsystemGameInfo info)
    {
        ScenerySite site = FindSite(SceneryKind.SkyIsland, false);
        int centerX = SceneryRules.FloorDiv(site.X, 16);
        int centerZ = SceneryRules.FloorDiv(site.Z, 16);
        CommonLib.WorkType = WorkType.Server;
        using (var first = new Terrain())
        using (var second = new Terrain())
        {
            var forward = GenerateRegion(first, Wrapper(mod, new Fixture(false), info),
                centerX - 1, centerX + 1, centerZ - 1, centerZ + 1, true);
            var reverse = GenerateRegion(second, Wrapper(mod, new Fixture(false), info),
                centerX - 1, centerX + 1, centerZ - 1, centerZ + 1, false);
            Check(forward.Count == 9 && reverse.Count == 9, "nine chunks generated");
            foreach (KeyValuePair<long, TerrainChunk> pair in forward)
            {
                TerrainChunk other;
                Check(reverse.TryGetValue(pair.Key, out other), "reverse order produced the same chunk");
                Check(pair.Value.Cells.Length == other.Cells.Length, "chunk cell array size");
                for (int i = 0; i < pair.Value.Cells.Length; i++)
                {
                    if (pair.Value.Cells[i] == other.Cells[i]) continue;
                    throw new Exception("Scenery native: generation order changes chunk " + pair.Key);
                }
                checks++;
            }
        }
    }

    static void TestObstacleGuard(Assembly mod, SubsystemGameInfo info)
    {
        int chunkX;
        int chunkZ;
        ScenerySite site;
        Check(FindSingleSiteChunk(LandHeight, out chunkX, out chunkZ, out site), "single-site chunk exists");
        MethodInfo generate = GeneratorMethod(mod);
        CommonLib.WorkType = WorkType.Local;
        using (var terrain = new Terrain())
        {
            TerrainChunk chunk = terrain.AllocateChunk(chunkX, chunkZ);
            var fixture = new Fixture(false);
            fixture.GenerateChunkContentsPass4(chunk);

            var recorder = new CellRecorder(chunkX, chunkZ);
            SceneryRules.Paint(site, recorder, BlockSet());
            Check(recorder.Cells.Count > 0, "the landmark emits cells into its chunk");
            foreach (KeyValuePair<long, int> pair in recorder.Cells)
            {
                int x;
                int y;
                int z;
                DecodeKey(pair.Key, out x, out y, out z);
                chunk.SetCellValueFast(x - chunk.Origin.X, y, z - chunk.Origin.Y, GraniteBlock.Index);
            }

            int written = (int)generate.Invoke(null, new object[] { chunk, fixture, WorldSeed });
            Check(written == 0, "existing solid cells are never overwritten, got " + written);

            long key = 0;
            int expected = 0;
            foreach (KeyValuePair<long, int> pair in recorder.Cells)
            {
                key = pair.Key;
                expected = pair.Value;
                break;
            }
            int px;
            int py;
            int pz;
            DecodeKey(key, out px, out py, out pz);
            chunk.SetCellValueFast(px - chunk.Origin.X, py, pz - chunk.Origin.Y, 0);
            written = (int)generate.Invoke(null, new object[] { chunk, fixture, WorldSeed });
            Check(written == 1, "one cleared cell is filled exactly once, got " + written);
            Check(Terrain.ExtractContents(chunk.GetCellValueFast(px - chunk.Origin.X, py, pz - chunk.Origin.Y)) == expected,
                "the cleared cell receives the landmark block");
        }
    }

    static void TestExceptionIsolation(Assembly mod, SubsystemGameInfo info)
    {
        CommonLib.WorkType = WorkType.Server;
        var fixture = new Fixture(true, true);
        using (var terrain = new Terrain())
        {
            TerrainChunk chunk = terrain.AllocateChunk(7, -5);
            Wrapper(mod, fixture, info).GenerateChunkContentsPass4(chunk);
            Check(Terrain.ExtractContents(chunk.GetCellValueFast(0, 50, 0)) == SandBlock.Index,
                "native terrain survives a scenery failure");
            Check(Terrain.ExtractContents(chunk.GetCellValueFast(0, 63, 0)) == WaterBlock.Index,
                "native water survives a scenery failure");
            Check(CountCustom(chunk) == 0, "a failed landmark paints nothing");
        }
    }

    static void AssertAirClearance(Dictionary<long, TerrainChunk> chunks, string name)
    {
        foreach (TerrainChunk chunk in chunks.Values)
        {
            for (int x = 0; x < 16; x++)
            {
                for (int z = 0; z < 16; z++)
                {
                    for (int y = 0; y < 256; y++)
                    {
                        int contents = Terrain.ExtractContents(chunk.GetCellValueFast(x, y, z));
                        if (contents < 880 || contents > 885) continue;
                        Check(y >= 98, name + " keeps air clearance above terrain");
                        Check(y <= SceneryRules.MaxWorldHeight, name + " stays below the build ceiling");
                    }
                }
            }
        }
    }

    static void AssertLandSoil(Dictionary<long, TerrainChunk> chunks, string name)
    {
        foreach (TerrainChunk chunk in chunks.Values)
        {
            for (int x = 0; x < 16; x++)
            {
                for (int z = 0; z < 16; z++)
                {
                    Check(Terrain.ExtractContents(chunk.GetCellValueFast(x, 71, z)) == DirtBlock.Index, name + " keeps native dirt");
                    Check(Terrain.ExtractContents(chunk.GetCellValueFast(x, 72, z)) == GrassBlock.Index, name + " keeps native grass");
                }
            }
        }
    }

    static ScenerySite FindSite(SceneryKind kind, bool ocean)
    {
        Func<int, int, float> height = LandHeight;
        if (ocean) height = OceanHeight;
        for (int chunkZ = -96; chunkZ <= 96; chunkZ++)
        {
            for (int chunkX = -96; chunkX <= 96; chunkX++)
            {
                var sites = new List<ScenerySite>();
                SceneryRules.VisitSites(WorldSeed, chunkX, chunkZ, OceanLevel, height, sites);
                for (int i = 0; i < sites.Count; i++)
                {
                    if (sites[i].Kind == kind) return sites[i];
                }
            }
        }
        throw new Exception("Scenery native: no " + kind + " site in the sampled area");
    }

    static bool FindSingleSiteChunk(Func<int, int, float> height, out int chunkX, out int chunkZ, out ScenerySite site)
    {
        for (int z = -40; z <= 40; z++)
        {
            for (int x = -40; x <= 40; x++)
            {
                var sites = new List<ScenerySite>();
                SceneryRules.VisitSites(WorldSeed, x, z, OceanLevel, height, sites);
                if (sites.Count != 1) continue;
                var recorder = new CellRecorder(x, z);
                SceneryRules.Paint(sites[0], recorder, BlockSet());
                if (recorder.Cells.Count == 0) continue;
                chunkX = x;
                chunkZ = z;
                site = sites[0];
                return true;
            }
        }
        chunkX = 0;
        chunkZ = 0;
        site = new ScenerySite();
        return false;
    }

    static Dictionary<long, TerrainChunk> GenerateSite(
        Terrain terrain,
        ITerrainContentsGenerator generator,
        ScenerySite site,
        bool forward)
    {
        int minX = SceneryRules.FloorDiv(site.MinX, 16);
        int maxX = SceneryRules.FloorDiv(site.MaxX, 16);
        int minZ = SceneryRules.FloorDiv(site.MinZ, 16);
        int maxZ = SceneryRules.FloorDiv(site.MaxZ, 16);
        return GenerateRegion(terrain, generator, minX, maxX, minZ, maxZ, forward);
    }

    static Dictionary<long, TerrainChunk> GenerateRegion(
        Terrain terrain,
        ITerrainContentsGenerator generator,
        int minChunkX,
        int maxChunkX,
        int minChunkZ,
        int maxChunkZ,
        bool forward)
    {
        var chunks = new Dictionary<long, TerrainChunk>();
        int width = maxChunkX - minChunkX + 1;
        int total = width * (maxChunkZ - minChunkZ + 1);
        for (int i = 0; i < total; i++)
        {
            int index = forward ? i : total - 1 - i;
            int chunkX = minChunkX + index % width;
            int chunkZ = minChunkZ + index / width;
            TerrainChunk chunk = terrain.AllocateChunk(chunkX, chunkZ);
            generator.GenerateChunkContentsPass4(chunk);
            chunks[ChunkKey(chunkX, chunkZ)] = chunk;
        }
        return chunks;
    }

    static ITerrainContentsGenerator Wrapper(Assembly mod, ITerrainContentsGenerator source, SubsystemGameInfo info)
    {
        Type type = mod.GetType("SCIEPort.Features.Plants.PlantTerrainGenerator", true);
        ConstructorInfo constructor = type.GetConstructor(F, null,
            new[] { typeof(ITerrainContentsGenerator), typeof(SubsystemGameInfo) }, null);
        Check(constructor != null, "terrain wrapper constructor");
        return (ITerrainContentsGenerator)constructor.Invoke(new object[] { source, info });
    }

    static MethodInfo GeneratorMethod(Assembly mod)
    {
        Type type = mod.GetType("SCIEPort.Features.Scenery.SceneryGenerator", true);
        MethodInfo method = type.GetMethod("Generate", S, null,
            new[] { typeof(TerrainChunk), typeof(ITerrainContentsGenerator), typeof(int) }, null);
        Check(method != null, "scenery generator entry point");
        return method;
    }

    static int CountBlock(Dictionary<long, TerrainChunk> chunks, int id)
    {
        int count = 0;
        foreach (TerrainChunk chunk in chunks.Values)
        {
            for (int x = 0; x < 16; x++)
            {
                for (int z = 0; z < 16; z++)
                {
                    for (int y = 0; y < 256; y++)
                    {
                        if (Terrain.ExtractContents(chunk.GetCellValueFast(x, y, z)) == id) count++;
                    }
                }
            }
        }
        return count;
    }

    static int CountBlockWithData(Dictionary<long, TerrainChunk> chunks, int id, int data)
    {
        int count = 0;
        foreach (TerrainChunk chunk in chunks.Values)
        {
            for (int x = 0; x < 16; x++)
            {
                for (int z = 0; z < 16; z++)
                {
                    for (int y = 0; y < 256; y++)
                    {
                        int value = chunk.GetCellValueFast(x, y, z);
                        if (Terrain.ExtractContents(value) == id && Terrain.ExtractData(value) == data) count++;
                    }
                }
            }
        }
        return count;
    }

    static int CountCustom(TerrainChunk chunk)
    {
        int count = 0;
        for (int x = 0; x < 16; x++)
        {
            for (int z = 0; z < 16; z++)
            {
                for (int y = 0; y < 256; y++)
                {
                    int contents = Terrain.ExtractContents(chunk.GetCellValueFast(x, y, z));
                    if (contents >= 880 && contents <= 885) count++;
                }
            }
        }
        return count;
    }

    static SceneryBlockSet BlockSet()
    {
        return new SceneryBlockSet
        {
            Hull = 881,
            Envelope = 882,
            Cloudstone = 880,
            Crystal = 883,
            Brass = 884,
            Sail = 885,
            Wood = OakWoodBlock.Index,
            Planks = PlanksBlock.Index,
            Glass = GlassBlock.Index,
            Treasure = TreasureGeneratorBlock.Index,
            Leaves = OakLeavesBlock.Index,
            Dirt = DirtBlock.Index,
            Grass = GrassBlock.Index,
            Display = 954
        };
    }

    static float OceanHeight(int x, int z)
    {
        return 50f;
    }

    static float LandHeight(int x, int z)
    {
        return 72f;
    }

    static long ChunkKey(int x, int z)
    {
        return ((long)x << 32) ^ (uint)z;
    }

    static long CellKey(int x, int y, int z)
    {
        return ((long)(x + 1048576) << 42) | ((long)(y + 1024) << 21) | (uint)(z + 1048576);
    }

    static void DecodeKey(long key, out int x, out int y, out int z)
    {
        x = (int)((key >> 42) & 0x1FFFFF) - 1048576;
        y = (int)((key >> 21) & 0x7FF) - 1024;
        z = (int)(key & 0x1FFFFF) - 1048576;
    }

    sealed class Fixture : ITerrainContentsGenerator
    {
        readonly bool m_ocean;
        readonly bool m_throwOnHeight;

        public Fixture(bool ocean)
            : this(ocean, false)
        {
        }

        public Fixture(bool ocean, bool throwOnHeight)
        {
            m_ocean = ocean;
            m_throwOnHeight = throwOnHeight;
        }

        public int OceanLevel
        {
            get { return SceneryNativeProbe.OceanLevel; }
        }

        public Vector3 FindCoarseSpawnPosition()
        {
            return Vector3.Zero;
        }

        public float CalculateOceanShoreDistance(float x, float z)
        {
            return m_ocean ? 200f : 40f;
        }

        public float CalculateHeight(float x, float z)
        {
            if (m_throwOnHeight) throw new Exception("scenery fixture height failure");
            return m_ocean ? 50f : 72f;
        }

        public int CalculateTemperature(float x, float z)
        {
            return 10;
        }

        public int CalculateHumidity(float x, float z)
        {
            return 10;
        }

        public float CalculateMountainRangeFactor(float x, float z)
        {
            return 0f;
        }

        public void GenerateChunkContentsPass1(TerrainChunk chunk) { }
        public void GenerateChunkContentsPass2(TerrainChunk chunk) { }
        public void GenerateChunkContentsPass3(TerrainChunk chunk) { }

        public void GenerateChunkContentsPass4(TerrainChunk chunk)
        {
            for (int x = 0; x < 16; x++)
            {
                for (int z = 0; z < 16; z++)
                {
                    if (m_ocean)
                    {
                        chunk.SetCellValueFast(x, 50, z, SandBlock.Index);
                        for (int y = 51; y <= SceneryNativeProbe.OceanLevel - 1; y++)
                        {
                            chunk.SetCellValueFast(x, y, z, WaterBlock.Index);
                        }
                    }
                    else
                    {
                        for (int y = 0; y <= 71; y++) chunk.SetCellValueFast(x, y, z, DirtBlock.Index);
                        chunk.SetCellValueFast(x, 72, z, GrassBlock.Index);
                    }
                }
            }
        }
    }

    sealed class CellRecorder : IScenerySink
    {
        readonly int m_originX;
        readonly int m_originZ;

        public readonly Dictionary<long, int> Cells = new Dictionary<long, int>();

        public CellRecorder(int chunkX, int chunkZ)
        {
            m_originX = chunkX * 16;
            m_originZ = chunkZ * 16;
        }

        public void Set(int x, int y, int z, int blockIndex, int data, bool replaceFluid)
        {
            if (x < m_originX || x > m_originX + 15 || z < m_originZ || z > m_originZ + 15) return;
            if (y < 1 || y > 254) return;
            Cells[CellKey(x, y, z)] = blockIndex;
        }
    }
}

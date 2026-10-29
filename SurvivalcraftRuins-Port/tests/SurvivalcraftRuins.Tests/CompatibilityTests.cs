using System.Reflection;
using Game;
using Game.NetWork;
using SimpleJson;
using SurvivalcraftRuins.Core;
using Xunit;

namespace SurvivalcraftRuins.Tests;

public sealed class CompatibilityTests
{
    private static string OutputDirectory => AppDomain.CurrentDomain.BaseDirectory;

    [Fact]
    public void TargetHookSignaturesMatchX250117()
    {
        MethodInfo? terrainHook = typeof(ModLoader).GetMethod(
            "OnTerrainContentsGenerated",
            new[] { typeof(TerrainChunk) });
        MethodInfo? updateHook = typeof(ModLoader).GetMethod("SubsystemUpdate", new[] { typeof(float) });

        Assert.NotNull(terrainHook);
        Assert.Equal(typeof(void), terrainHook!.ReturnType);
        Assert.NotNull(updateHook);
        Assert.Equal(typeof(void), updateHook!.ReturnType);
        Assert.Equal(typeof(List<string>), typeof(ModInfo).GetField("Dependencies")!.FieldType);
    }

    [Fact]
    public void AuthorityGateAllowsOnlyLocalAndServer()
    {
        Assert.True(RuntimeCompat.IsAuthoritativeRole(WorkType.Local, roleIsValid: true));
        Assert.True(RuntimeCompat.IsAuthoritativeRole(WorkType.Server, roleIsValid: true));
        Assert.False(RuntimeCompat.IsAuthoritativeRole(WorkType.Client, roleIsValid: true));
        Assert.False(RuntimeCompat.IsAuthoritativeRole(WorkType.Local, roleIsValid: false));
        Assert.False(RuntimeCompat.IsAuthoritativeRole(WorkType.Server, roleIsValid: false));
    }

    [Fact]
    public void MetadataMatchesTargetLoaderSchema()
    {
        var metadata = (JsonObject)SimpleJson.SimpleJson.DeserializeObject(
            File.ReadAllText(Path.Combine(OutputDirectory, "modinfo.json")))!;

        Assert.Equal("1.42", metadata["ApiVersion"]);
        Assert.Equal("2.4.10.6", metadata["ScVersion"]);
        Assert.IsType<JsonArray>(metadata["Dependencies"]);
        Assert.False(metadata.ContainsKey("LoadOrder"));
        Assert.False(metadata.ContainsKey("Settings"));
    }

    [Fact]
    public void AllTwentyThreeVariantsUseRealTargetBlocksAndBoundedData()
    {
        var templates = LoadTemplates();
        Dictionary<string, LootPool> pools = LoadLootPools();
        IReadOnlyList<StructureDef> definitions = StructureRegistry.CreateDefs(templates);
        int variants = 0;
        int maximumCells = 0;
        int maximumChunkCells = 0;

        foreach (StructureDef definition in definitions)
        {
            foreach (StructureVariant variant in definition.Variants)
            {
                variants++;
                StructureData data = variant.Provider(0x250117);
                maximumCells = Math.Max(maximumCells, data.Cells.Count);
                Assert.InRange(data.Cells.Count, 1, WorldGenerationLimits.MaxCellsPerStructure);
                foreach (PaletteEntry palette in data.Palette)
                {
                    AssertTargetBlock(palette.BlockName, palette.Data, variant.Name);
                }
                maximumChunkCells = Math.Max(maximumChunkCells, MaximumCellsInAnyChunk(data));
            }
        }
        foreach (LootPool pool in pools.Values)
        {
            foreach (LootEntry entry in pool.Entries.Where(e => !string.IsNullOrEmpty(e.BlockName)))
            {
                AssertTargetBlock(entry.BlockName, entry.Data, "loot:" + pool.Name);
            }
        }

        Assert.Equal(13, templates.Count);
        Assert.Equal(AssetValidator.ExpectedVariantCount, variants);
        Assert.InRange(maximumChunkCells, 1, WorldGenerationLimits.MaxCellWritesPerChunk);
        Assert.InRange(maximumCells, 1, WorldGenerationLimits.MaxCellsPerStructure);
    }

    [Fact]
    public void AndroidSafePoolExpansionPreservesOriginalOrderAndCounts()
    {
        var variants = StructureRegistry.CreateDefs(LoadTemplates())
            .SelectMany(definition => definition.Variants)
            .ToDictionary(variant => variant.Name, StringComparer.Ordinal);

        AssertPoolRuns(variants["sky_fortress"].ChestPools, "sky_cache", 12, "vault", 4);
        AssertPoolRuns(variants["ruined_temple"].ChestPools, "offering", 6, "vault", 2);
        AssertPoolRuns(variants["bathhouse"].ChestPools, "trader_goods", 14, "offering", 4);
        AssertPoolRuns(variants["galley"].ChestPools, "ship_supply", 8, "ship_treasure", 3);
        AssertPoolRuns(variants["windmill"].ChestPools, "homestead_cellar", 8, "homestead", 8);
    }

    [Fact]
    public void AssetLoaderUsesExactManifestInsteadOfExtensionEnumeration()
    {
        var entity = new DirectoryModEntity(OutputDirectory);

        RuinsRuntime.LoadAssets(entity);

        var templates = (IReadOnlyDictionary<string, StructureData>)typeof(RuinsRuntime)
            .GetField("s_templates", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;
        var pools = (IReadOnlyDictionary<string, LootPool>)typeof(RuinsRuntime)
            .GetField("s_lootPools", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;
        Assert.Equal(13, templates.Count);
        Assert.Contains("sky_fortress", templates.Keys);
        Assert.NotEmpty(pools);
        Assert.Equal(14, entity.RequestedPaths.Count);
        Assert.Contains("Assets/Structures/sky_fortress.json", entity.RequestedPaths);
        Assert.Contains("Assets/Loot/pools.json", entity.RequestedPaths);
    }

    [Theory]
    [InlineData("WoodenStairsBlock", 8)]
    [InlineData("WoodenSlabBlock", 2)]
    [InlineData("OakWoodBlock", 3)]
    [InlineData("WoodenLadderBlock", 4)]
    [InlineData("TorchBlock", 5)]
    [InlineData("CarpetBlock", 16)]
    [InlineData("DetonatorBlock", 6)]
    [InlineData("PressurePlateBlock", 12)]
    [InlineData("DispenserBlock", 14)]
    [InlineData("SwitchBlock", 12)]
    [InlineData("WireThroughStoneBlock", 3)]
    [InlineData("PistonBlock", 75)]
    public void UnsafeDirectionalOrColorDataIsRejected(string blockName, int data)
    {
        Assert.False(BlockDataPolicy.IsSupported(blockName, data, out string reason));
        Assert.False(string.IsNullOrWhiteSpace(reason));
    }

    private static Dictionary<string, StructureData> LoadTemplates()
    {
        var result = new Dictionary<string, StructureData>(StringComparer.OrdinalIgnoreCase);
        string directory = Path.Combine(OutputDirectory, "Assets", "Structures");
        foreach (string file in Directory.GetFiles(directory, "*.json"))
        {
            StructureData data = StructureTemplate.Parse(File.ReadAllText(file));
            result.Add(data.Name, data);
        }
        return result;
    }

    private static Dictionary<string, LootPool> LoadLootPools()
    {
        return LootConfig.Parse(File.ReadAllText(
            Path.Combine(OutputDirectory, "Assets", "Loot", "pools.json")));
    }

    private static void AssertTargetBlock(string blockName, int data, string source)
    {
        Assert.True(BlockDataPolicy.IsSupported(blockName, data, out string reason),
            $"{source}: {blockName} data={data}: {reason}");
        Type? blockType = typeof(Block).Assembly.GetType("Game." + blockName, throwOnError: false);
        Assert.True(blockType != null && typeof(Block).IsAssignableFrom(blockType),
            $"{source}: target assembly has no Game.{blockName}");
        FieldInfo? index = blockType!.GetField("Index", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(index);
        Assert.InRange((int)index!.GetRawConstantValue()!, 0, 1023);
    }

    private static int MaximumCellsInAnyChunk(StructureData data)
    {
        int maximum = 0;
        for (int offsetX = 0; offsetX < 16; offsetX++)
        {
            for (int offsetZ = 0; offsetZ < 16; offsetZ++)
            {
                var counts = new Dictionary<(int X, int Z), int>();
                foreach (Cell cell in data.Cells)
                {
                    var key = (
                        PlacementEngine.FloorDiv(cell.X + offsetX, 16),
                        PlacementEngine.FloorDiv(cell.Z + offsetZ, 16));
                    counts.TryGetValue(key, out int count);
                    counts[key] = count + 1;
                }
                if (counts.Count > 0)
                {
                    maximum = Math.Max(maximum, counts.Values.Max());
                }
            }
        }
        return maximum;
    }

    private static void AssertPoolRuns(
        IReadOnlyList<string> actual,
        string first,
        int firstCount,
        string second,
        int secondCount)
    {
        Assert.Equal(firstCount + secondCount, actual.Count);
        for (int i = 0; i < firstCount; i++)
        {
            Assert.Equal(first, actual[i]);
        }
        for (int i = 0; i < secondCount; i++)
        {
            Assert.Equal(second, actual[firstCount + i]);
        }
    }

    private sealed class DirectoryModEntity : ModEntity
    {
        private readonly string m_root;

        public DirectoryModEntity(string root)
        {
            m_root = root;
        }

        public List<string> RequestedPaths { get; } = new();

        public override void GetFiles(string extension, Action<string, Stream> action)
        {
            throw new InvalidOperationException("Extension enumeration is forbidden by this compatibility test.");
        }

        public override bool GetFile(string filename, Action<Stream> action)
        {
            RequestedPaths.Add(filename);
            string path = Path.Combine(m_root, filename.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
            {
                return false;
            }
            using Stream stream = File.OpenRead(path);
            action(stream);
            return true;
        }
    }
}

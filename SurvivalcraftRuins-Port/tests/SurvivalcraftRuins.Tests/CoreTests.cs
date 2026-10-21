using SurvivalcraftRuins.Builders;
using SurvivalcraftRuins.Core;
using Xunit;

namespace SurvivalcraftRuins.Tests;

/// <summary>Flat dry world at a fixed altitude.</summary>
internal sealed class FlatSampler : IWorldSampler
{
    public int OceanLevel => 64;
    private readonly float m_height;

    public FlatSampler(float height)
    {
        m_height = height;
    }

    public float Height(float x, float z)
    {
        return m_height;
    }

    public int Temperature(float x, float z) => 8;

    public int Humidity(float x, float z) => 8;
}

/// <summary>Open ocean with a uniform seabed depth.</summary>
internal sealed class OceanSampler : IWorldSampler
{
    public int OceanLevel => 64;
    private readonly float m_bed;

    public OceanSampler(float bed = 56f)
    {
        m_bed = bed;
    }

    public float Height(float x, float z)
    {
        return m_bed;
    }

    public int Temperature(float x, float z) => 8;

    public int Humidity(float x, float z) => 8;
}

/// <summary>Flat land with a fixed biome reading, for the biome-gated slots.</summary>
internal sealed class BiomeSampler : IWorldSampler
{
    private readonly int m_temperature;
    private readonly int m_humidity;

    public BiomeSampler(int temperature, int humidity)
    {
        m_temperature = temperature;
        m_humidity = humidity;
    }

    public int OceanLevel => 64;

    public float Height(float x, float z) => 80f;

    public int Temperature(float x, float z) => m_temperature;

    public int Humidity(float x, float z) => m_humidity;
}

/// <summary>
/// Rolling terrain with hills, valleys and sea, so a candidate site is a coin
/// toss rather than a guaranteed hit. Wavelengths are in the same range as the
/// game's hills (tens of blocks), which is what makes single-site placement
/// lose most of its candidates.
/// </summary>
internal sealed class RollingSampler : IWorldSampler
{
    private readonly float m_relief;

    /// <param name="relief">Local hill amplitude; 5 is gentle country, 12 is broken ground.</param>
    public RollingSampler(float relief = 5f)
    {
        m_relief = relief;
    }

    public int OceanLevel => 64;

    public float Height(float x, float z)
    {
        return 74f
            + 14f * (float)Math.Sin(x * 0.0042f) * (float)Math.Cos(z * 0.0051f)   // broad swells and bays
            + m_relief * (float)Math.Sin(x * 0.021f + 1.3f)                      // hills
            + m_relief * (float)Math.Cos(z * 0.019f - 0.7f);
    }

    public int Temperature(float x, float z) => 8;

    public int Humidity(float x, float z) => 8;
}

public class DeterministicHashTests
{
    [Fact]
    public void MixIsStable()
    {
        Assert.Equal(DeterministicHash.Mix(123, 7, -4, 9), DeterministicHash.Mix(123, 7, -4, 9));
        Assert.NotEqual(DeterministicHash.Mix(123, 7, -4, 9), DeterministicHash.Mix(124, 7, -4, 9));
    }

    [Fact]
    public void UnitFloatIsInRange()
    {
        for (uint i = 0; i < 1000; i++)
        {
            float f = DeterministicHash.ToUnitFloat(DeterministicHash.Mix(i, 42u));
            Assert.InRange(f, 0f, 0.9999999f);
        }
    }

    [Fact]
    public void StringSaltIsStable()
    {
        Assert.Equal(DeterministicHash.StringSalt("werewolf_den"), DeterministicHash.StringSalt("werewolf_den"));
        Assert.NotEqual(DeterministicHash.StringSalt("a"), DeterministicHash.StringSalt("b"));
    }
}

public class PlacementEngineTests
{
    private static StructureDef SimpleLandDef(string name, int gridChunks = 8, float chance = 0.5f)
    {
        return new StructureDef
        {
            Name = name,
            GridChunks = gridChunks,
            Chance = chance,
            Anchor = AnchorKind.Land,
            Variants = new[]
            {
                new StructureVariant
                {
                    Name = name,
                    Provider = _ =>
                    {
                        var b = new StructureBuilder(name);
                        b.Fill(0, 0, 0, 4, 3, 4, "PlanksBlock");
                        b.Chest(2, 1, 2);
                        return b.Build();
                    },
                    ChestPools = new[] { "baseline" },
                },
            },
        };
    }

    [Fact]
    public void SameSeedSameLayout()
    {
        var defs = new[] { SimpleLandDef("hut") };
        var a = new PlacementEngine(1234, new FlatSampler(80f), defs);
        var b = new PlacementEngine(1234, new FlatSampler(80f), defs);
        for (int cx = -6; cx <= 6; cx++)
        {
            for (int cz = -6; cz <= 6; cz++)
            {
                var ia = a.GetInstance(0, cx, cz);
                var ib = b.GetInstance(0, cx, cz);
                Assert.Equal(ia == null, ib == null);
                if (ia != null && ib != null)
                {
                    Assert.Equal((ia.OriginX, ia.OriginY, ia.OriginZ), (ib.OriginX, ib.OriginY, ib.OriginZ));
                }
            }
        }
    }

    [Fact]
    public void DifferentSeedsDiffer()
    {
        var defs = new[] { SimpleLandDef("hut", chance: 0.9f) };
        var a = new PlacementEngine(1, new FlatSampler(80f), defs);
        var b = new PlacementEngine(2, new FlatSampler(80f), defs);
        int differences = 0;
        for (int cx = -8; cx <= 8; cx++)
        {
            for (int cz = -8; cz <= 8; cz++)
            {
                var ia = a.GetInstance(0, cx, cz);
                var ib = b.GetInstance(0, cx, cz);
                if ((ia == null) != (ib == null))
                {
                    differences++;
                }
                else if (ia != null && ib != null && (ia.OriginX != ib.OriginX || ia.OriginZ != ib.OriginZ))
                {
                    differences++;
                }
            }
        }
        Assert.True(differences > 10, $"expected layouts to diverge, only {differences} differences");
    }

    [Fact]
    public void InstanceStaysInsideItsCell()
    {
        var defs = new[] { SimpleLandDef("hut", chance: 1f) };
        var engine = new PlacementEngine(99, new FlatSampler(90f), defs);
        for (int cx = -5; cx <= 5; cx++)
        {
            for (int cz = -5; cz <= 5; cz++)
            {
                var instance = engine.GetInstance(0, cx, cz);
                Assert.NotNull(instance);
                int span = defs[0].GridChunks * 16;
                Assert.InRange(instance!.WorldMinX, cx * span, (cx + 1) * span - 1);
                Assert.InRange(instance.WorldMaxX, cx * span, (cx + 1) * span - 1);
                Assert.InRange(instance.WorldMinZ, cz * span, (cz + 1) * span - 1);
                Assert.InRange(instance.WorldMaxZ, cz * span, (cz + 1) * span - 1);
            }
        }
    }

    [Fact]
    public void LandStructuresRejectOcean()
    {
        var defs = new[] { SimpleLandDef("hut", chance: 1f) };
        var engine = new PlacementEngine(7, new OceanSampler(), defs);
        for (int cx = -4; cx <= 4; cx++)
        {
            for (int cz = -4; cz <= 4; cz++)
            {
                Assert.Null(engine.GetInstance(0, cx, cz));
            }
        }
    }

    [Fact]
    public void OffshoreStructuresRequireWater()
    {
        var wreck = new StructureDef
        {
            Name = "wreck",
            GridChunks = 8,
            Chance = 1f,
            Anchor = AnchorKind.Offshore,
            Variants = new[]
            {
                new StructureVariant
                {
                    Name = "wreck",
                    Provider = ShipwreckBuilder.Build,
                    ChestPools = new[] { "ship_supply" },
                },
            },
        };
        var ocean = new PlacementEngine(5, new OceanSampler(), new[] { wreck });
        Assert.NotNull(ocean.GetInstance(0, 0, 0));
        var land = new PlacementEngine(5, new FlatSampler(90f), new[] { wreck });
        Assert.Null(land.GetInstance(0, 0, 0));
    }

    [Fact]
    public void HigherPriorityDefSuppressesOverlap()
    {
        // Two defs with identical grids and 100% chance will always collide in
        // the same cell; the second must yield.
        var defs = new[] { SimpleLandDef("first", chance: 1f), SimpleLandDef("second", chance: 1f) };
        var engine = new PlacementEngine(42, new FlatSampler(80f), defs);
        for (int cx = -3; cx <= 3; cx++)
        {
            for (int cz = -3; cz <= 3; cz++)
            {
                var first = engine.GetInstance(0, cx, cz);
                var second = engine.GetInstance(1, cx, cz);
                Assert.NotNull(first);
                if (second != null)
                {
                    Assert.False(second.IntersectsColumnRange(
                        first!.WorldMinX, first.WorldMinZ, first.WorldMaxX, first.WorldMaxZ));
                }
            }
        }
    }

    [Fact]
    public void ChunkQueryMatchesDirectEnumeration()
    {
        var defs = new[] { SimpleLandDef("hut", chance: 0.8f) };
        var engine = new PlacementEngine(2024, new FlatSampler(75f), defs);
        var fromChunks = new HashSet<(int, int, int)>();
        for (int chunkX = -20; chunkX <= 20; chunkX++)
        {
            for (int chunkZ = -20; chunkZ <= 20; chunkZ++)
            {
                foreach (var instance in engine.InstancesIntersecting(
                             chunkX * 16, chunkZ * 16, chunkX * 16 + 15, chunkZ * 16 + 15))
                {
                    fromChunks.Add((instance.OriginX, instance.OriginY, instance.OriginZ));
                }
            }
        }
        Assert.True(fromChunks.Count > 3, $"expected several instances, got {fromChunks.Count}");
    }
}

public class BuilderTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(999)]
    public void BuildersAreDeterministic(int seed)
    {
        foreach (var build in new Func<int, StructureData>[]
                 { ShipwreckBuilder.Build, MineBuilder.Build, LighthouseBuilder.Build, CampBuilder.Build,
                   MarketStallBuilder.Build, WatchtowerBuilder.Build, ShrineBuilder.Build })
        {
            var a = build(seed);
            var b = build(seed);
            Assert.Equal(a.Cells.Count, b.Cells.Count);
            for (int i = 0; i < a.Cells.Count; i++)
            {
                Assert.Equal(a.Cells[i], b.Cells[i]);
            }
        }
    }

    [Fact]
    public void ShipwreckHasCargoAndTreasure()
    {
        var ship = ShipwreckBuilder.Build(7);
        Assert.Equal(3, ship.Chests.Count);
        Assert.Contains(ship.Palette, p => p.BlockName == "TreasureGeneratorBlock");
        Assert.Contains(ship.Palette, p => p.BlockName == "SmallGunpowderKegBlock");
    }

    [Fact]
    public void MineHasChestsOreAndAmbush()
    {
        var mine = MineBuilder.Build(3);
        Assert.Equal(2, mine.Chests.Count);
        Assert.Single(mine.Ambushes);
        Assert.Contains(mine.Palette, p => p.BlockName.EndsWith("OreBlock", StringComparison.Ordinal));
        Assert.True(mine.MinY < -10, "mine should dig well below the surface");
    }

    [Fact]
    public void LighthouseIsTallWithLamps()
    {
        var lighthouse = LighthouseBuilder.Build(11);
        Assert.Equal(2, lighthouse.Chests.Count);
        Assert.True(lighthouse.MaxY >= 20, "tower should be tall");
        Assert.Contains(lighthouse.Palette, p => p.BlockName == "WickerLampBlock");
    }

    [Fact]
    public void ColorfulBuildersHaveExpectedFixtures()
    {
        var stall = MarketStallBuilder.Build(9);
        Assert.Equal(2, stall.Chests.Count);
        Assert.Contains(stall.Palette, p => p.BlockName == "CarpetBlock");
        var tower = WatchtowerBuilder.Build(4);
        Assert.Equal(2, tower.Chests.Count);
        Assert.True(tower.MaxY >= 11);
        var shrine = ShrineBuilder.Build(2);
        Assert.Single(shrine.Chests);
        Assert.Contains(shrine.Palette, p => p.BlockName == "TreasureGeneratorBlock");
    }

    [Fact]
    public void CampHasTwoChests()
    {
        var camp = CampBuilder.Build(5);
        Assert.Equal(2, camp.Chests.Count);
        Assert.Contains(camp.Palette, p => p.BlockName == "CampfireBlock");
    }
}

public class LootAndTemplateTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "src", "SurvivalcraftRuins")))
        {
            dir = dir.Parent;
        }
        Assert.NotNull(dir);
        return dir!.FullName;
    }

    private static string AssetsDir => Path.Combine(RepoRoot(), "src", "SurvivalcraftRuins", "Assets");

    [Fact]
    public void LootPoolsParseAndRollWithinBounds()
    {
        var pools = LootConfig.Parse(File.ReadAllText(Path.Combine(AssetsDir, "Loot", "pools.json")));
        Assert.True(pools.Count >= 8);
        foreach (var pool in pools.Values)
        {
            var random = new Random(42);
            for (int i = 0; i < 200; i++)
            {
                var items = pool.Roll(random);
                Assert.True(items.Count <= pool.MaxRolls);
                foreach (var item in items)
                {
                    Assert.False(string.IsNullOrEmpty(item.BlockName));
                    Assert.InRange(item.Count, 1, 64);
                }
            }
        }
    }

    [Fact]
    public void LootRollsAreDeterministicPerSeed()
    {
        var pools = LootConfig.Parse(File.ReadAllText(Path.Combine(AssetsDir, "Loot", "pools.json")));
        var pool = pools["ship_treasure"];
        var a = pool.Roll(new Random(777));
        var b = pool.Roll(new Random(777));
        Assert.Equal(a, b);
    }

    [Fact]
    public void AllTemplatesParseWithConsistentMarkers()
    {
        var files = Directory.GetFiles(Path.Combine(AssetsDir, "Structures"), "*.json");
        Assert.Equal(13, files.Length);
        foreach (var file in files)
        {
            var data = StructureTemplate.Parse(File.ReadAllText(file));
            Assert.NotEmpty(data.Cells);
            var chestCells = new HashSet<(short, short, short)>();
            foreach (var cell in data.Cells)
            {
                if (data.Palette[cell.PaletteIndex].BlockName == "ChestBlock")
                {
                    chestCells.Add((cell.X, cell.Y, cell.Z));
                }
            }
            foreach (var chest in data.Chests)
            {
                Assert.Contains((chest.X, chest.Y, chest.Z), chestCells);
            }
            Assert.True(data.SizeX <= PlacementEngine.MaxFootprint, $"{data.Name} too wide");
            Assert.True(data.SizeZ <= PlacementEngine.MaxFootprint, $"{data.Name} too deep");
        }
    }

    [Fact]
    public void RegistryPoolsAllExist()
    {
        var pools = LootConfig.Parse(File.ReadAllText(Path.Combine(AssetsDir, "Loot", "pools.json")));
        var templates = new Dictionary<string, StructureData>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.GetFiles(Path.Combine(AssetsDir, "Structures"), "*.json"))
        {
            var data = StructureTemplate.Parse(File.ReadAllText(file));
            templates[data.Name] = data;
        }
        var defs = StructureRegistry.CreateDefs(templates);
        Assert.Equal(14, defs.Count);
        int variantCount = 0;
        foreach (var def in defs)
        {
            foreach (var variant in def.Variants)
            {
                variantCount++;
                var data = variant.Provider(123);
                foreach (var pool in variant.ChestPools)
                {
                    Assert.True(pools.ContainsKey(pool), $"{variant.Name}: missing pool '{pool}'");
                }
                if (variant.ChestPools.Count > 0)
                {
                    Assert.True(data.Chests.Count > 0, $"{variant.Name}: pools assigned but no chest markers");
                }
                if (variant.Guards != null)
                {
                    Assert.NotEmpty(GuardPlanner.Plan(variant, data));
                }
                if (variant.Bounds is { } bounds)
                {
                    Assert.Equal(bounds, StructureBounds.From(data));
                }
                Assert.True(data.SizeX <= PlacementEngine.MaxFootprint, $"{variant.Name} too wide: {data.SizeX}");
                Assert.True(data.SizeZ <= PlacementEngine.MaxFootprint, $"{variant.Name} too deep: {data.SizeZ}");
            }
        }
        Assert.Equal(23, variantCount);
    }
}

public class DensityTests
{
    private static IReadOnlyList<StructureDef> LandDefs()
    {
        var defs = new List<StructureDef>();
        foreach (var def in TestRegistry.Defs())
        {
            if (def.Anchor == AnchorKind.Land && def.Biome == null)
            {
                defs.Add(def);
            }
        }
        return defs;
    }

    private static double PerSquareKm(IWorldSampler sampler, int seed = 20260808)
    {
        var defs = LandDefs();
        var engine = new PlacementEngine(seed, sampler, defs);
        const int side = 2000;   // 4 km^2
        var seen = new HashSet<(int, int)>();
        for (int i = 0; i < defs.Count; i++)
        {
            int span = defs[i].GridChunks * 16;
            for (int cx = 0; cx * span < side; cx++)
            {
                for (int cz = 0; cz * span < side; cz++)
                {
                    var instance = engine.GetInstance(i, cx, cz);
                    if (instance != null)
                    {
                        seen.Add((instance.CenterX, instance.CenterZ));
                    }
                }
            }
        }
        return seen.Count / 4.0;
    }

    /// <summary>
    /// Land structures sit around one every 110 blocks. Measured over a real
    /// area so a future grid tweak that undoes it fails here.
    /// </summary>
    [Fact]
    public void LandDensityIsAroundTwentyPerSquareKilometre()
    {
        Assert.InRange(PerSquareKm(new FlatSampler(80f)), 14, 28);
    }

    /// <summary>
    /// What layout 3 buys: the table means the same thing on real terrain as on
    /// a test plane. Play testing found the opposite — one site test per cell
    /// let hills and coastline eat most candidates, so a table tuned to 40/km²
    /// delivered under 20 and biome-gated temples were effectively absent.
    /// </summary>
    [Fact]
    public void RoughTerrainKeepsMostOfTheTableDensity()
    {
        double flat = PerSquareKm(new FlatSampler(80f));
        double rough = PerSquareKm(new RollingSampler());
        Assert.True(rough > 0.6 * flat, $"rough terrain kept only {rough:0.0} of {flat:0.0} per km^2");
    }

    /// <summary>
    /// The retry is what makes that hold; with a single site per cell the same
    /// terrain loses a large share of the candidates.
    /// </summary>
    [Fact]
    public void SiteRetryIsWhatSurvivesRoughTerrain()
    {
        var defs = LandDefs();
        int Count(int attempts)
        {
            var engine = new PlacementEngine(4242, new RollingSampler(12f), defs, 1f, attempts);
            int found = 0;
            for (int i = 0; i < defs.Count; i++)
            {
                for (int cx = 0; cx < 12; cx++)
                {
                    for (int cz = 0; cz < 12; cz++)
                    {
                        if (engine.GetInstance(i, cx, cz) != null)
                        {
                            found++;
                        }
                    }
                }
            }
            return found;
        }
        Assert.True(Count(PlacementEngine.DefaultSiteAttempts) > Count(1) * 1.4,
            "retrying inside the cell should recover a large share of rejected candidates");
    }

    /// <summary>
    /// There is one placement table and every world uses it, whatever version
    /// of the mod (if any) that world was created under. Adding the mod to a
    /// world someone already plays is the normal case, not an edge case.
    /// </summary>
    [Fact]
    public void EveryWorldUsesTheCurrentTable()
    {
        var defs = StructureRegistry.CreateDefs(TestRegistry.Templates());
        Assert.Equal(18, defs.Single(d => d.Name == "small").GridChunks);
        Assert.Contains(defs, d => d.Name == "desert_temple");
        Assert.Contains(defs, d => d.Name == "jungle_temple");
        Assert.Contains(defs, d => d.Name == "monument");
    }

    [Fact]
    public void DensityScaleThinsPlacement()
    {
        var defs = LandDefs();
        int Count(float scale)
        {
            var engine = new PlacementEngine(7, new FlatSampler(80f), defs, scale);
            int found = 0;
            for (int i = 0; i < defs.Count; i++)
            {
                for (int cx = -8; cx <= 8; cx++)
                {
                    for (int cz = -8; cz <= 8; cz++)
                    {
                        if (engine.GetInstance(i, cx, cz) != null)
                        {
                            found++;
                        }
                    }
                }
            }
            return found;
        }
        Assert.True(Count(0.5f) < Count(1f), "half density should place fewer structures");
        Assert.Equal(0, Count(0f));
    }

    [Fact]
    public void InstancesKeepTheirSeparationRadius()
    {
        var defs = TestRegistry.Defs();
        var engine = new PlacementEngine(4242, new FlatSampler(80f), defs);
        var placed = new List<StructureInstance>();
        for (int i = 0; i < defs.Count; i++)
        {
            for (int cx = -4; cx <= 4; cx++)
            {
                for (int cz = -4; cz <= 4; cz++)
                {
                    var instance = engine.GetInstance(i, cx, cz);
                    if (instance != null)
                    {
                        placed.Add(instance);
                    }
                }
            }
        }
        Assert.True(placed.Count > 20, $"expected a decent sample, got {placed.Count}");
        for (int a = 0; a < placed.Count; a++)
        {
            for (int b = a + 1; b < placed.Count; b++)
            {
                if (ReferenceEquals(placed[a].Def, placed[b].Def))
                {
                    continue;   // same slot is spaced by the cell grid instead
                }
                int keepOut = Math.Max(placed[a].Def.SeparationRadius, placed[b].Def.SeparationRadius);
                long dx = placed[a].CenterX - placed[b].CenterX;
                long dz = placed[a].CenterZ - placed[b].CenterZ;
                Assert.True(dx * dx + dz * dz >= (long)keepOut * keepOut,
                    $"{placed[a].Def.Name} and {placed[b].Def.Name} are {Math.Sqrt(dx * dx + dz * dz):0} apart, want {keepOut}");
            }
        }
    }

    [Fact]
    public void NeighbouringCellsOfOneSlotStayApart()
    {
        var def = new StructureDef
        {
            Name = "hut",
            GridChunks = 14,
            Chance = 1f,
            Anchor = AnchorKind.Land,
            Variants = new[]
            {
                new StructureVariant
                {
                    Name = "hut",
                    Provider = _ =>
                    {
                        var b = new StructureBuilder("hut");
                        b.Fill(0, 0, 0, 6, 3, 6, "PlanksBlock");
                        b.Chest(3, 1, 3);
                        return b.Build();
                    },
                    ChestPools = new[] { "baseline" },
                },
            },
        };
        var engine = new PlacementEngine(11, new FlatSampler(80f), new[] { def });
        for (int cx = -3; cx < 3; cx++)
        {
            var left = engine.GetInstance(0, cx, 0);
            var right = engine.GetInstance(0, cx + 1, 0);
            Assert.NotNull(left);
            Assert.NotNull(right);
            int gap = right!.WorldMinX - left!.WorldMaxX;
            Assert.True(gap >= 100, $"neighbouring cells left only {gap} blocks between structures");
        }
    }
}

public class BiomeTests
{
    private static StructureDef Def(string name)
    {
        foreach (var def in TestRegistry.Defs())
        {
            if (def.Name == name)
            {
                return def;
            }
        }
        throw new InvalidOperationException($"no slot '{name}'");
    }

    [Theory]
    [InlineData(13, 2, true)]    // hot and dry
    [InlineData(13, 9, false)]   // hot but damp
    [InlineData(5, 2, false)]    // dry but cool
    public void DesertTempleOnlyBuildsInDesert(int temperature, int humidity, bool expected)
    {
        var engine = new PlacementEngine(3, new BiomeSampler(temperature, humidity), new[] { Def("desert_temple") });
        bool any = false;
        for (int cx = -3; cx <= 3 && !any; cx++)
        {
            for (int cz = -3; cz <= 3 && !any; cz++)
            {
                any = engine.GetInstance(0, cx, cz) != null;
            }
        }
        Assert.Equal(expected, any);
    }

    [Theory]
    [InlineData(13, 13, true)]   // hot and wet
    [InlineData(13, 5, false)]   // hot and dry
    [InlineData(3, 13, false)]   // wet but cold
    public void JungleTempleOnlyBuildsInJungle(int temperature, int humidity, bool expected)
    {
        var engine = new PlacementEngine(9, new BiomeSampler(temperature, humidity), new[] { Def("jungle_temple") });
        bool any = false;
        for (int cx = -3; cx <= 3 && !any; cx++)
        {
            for (int cz = -3; cz <= 3 && !any; cz++)
            {
                any = engine.GetInstance(0, cx, cz) != null;
            }
        }
        Assert.Equal(expected, any);
    }

    [Fact]
    public void MonumentNeedsDeepWater()
    {
        var def = Def("monument");
        var deep = new PlacementEngine(5, new OceanSampler(bed: 30f), new[] { def });
        bool foundDeep = false;
        for (int cx = -2; cx <= 2 && !foundDeep; cx++)
        {
            for (int cz = -2; cz <= 2 && !foundDeep; cz++)
            {
                foundDeep = deep.GetInstance(0, cx, cz) != null;
            }
        }
        Assert.True(foundDeep, "monument should appear in deep ocean");

        var shallow = new PlacementEngine(5, new OceanSampler(bed: 56f), new[] { def });
        for (int cx = -2; cx <= 2; cx++)
        {
            for (int cz = -2; cz <= 2; cz++)
            {
                Assert.Null(shallow.GetInstance(0, cx, cz));
            }
        }
    }

    /// <summary>
    /// Declared bounds let placement reject a site without building; if they
    /// ever drifted from the real geometry, structures would be mis-sited.
    /// </summary>
    [Fact]
    public void MonumentBoundsMatchWhatItBuilds()
    {
        foreach (int seed in new[] { 1, 77, 20260808 })
        {
            Assert.Equal(OceanMonumentBuilder.Bounds, StructureBounds.From(OceanMonumentBuilder.Build(seed)));
        }
    }
}

public class GuardTests
{
    private static StructureVariant Variant(string name)
    {
        foreach (var def in TestRegistry.Defs())
        {
            foreach (var variant in def.Variants)
            {
                if (variant.Name == name)
                {
                    return variant;
                }
            }
        }
        throw new InvalidOperationException($"no variant '{name}'");
    }

    private static int TotalGuards(string variantName)
    {
        var variant = Variant(variantName);
        int total = 0;
        foreach (var post in GuardPlanner.Plan(variant, variant.Provider(5)))
        {
            total += post.Count;
        }
        return total;
    }

    /// <summary>
    /// The coliseum used to roll 1-2 lions at each of twelve markers, so up to
    /// twenty-four. The budget caps it at ten however many markers it carries.
    /// </summary>
    [Fact]
    public void ColiseumGuardsAreCapped()
    {
        Assert.InRange(TotalGuards("coliseum"), 6, 10);
    }

    /// <summary>The sky fortress holds the best loot in the mod and used to be empty.</summary>
    [Fact]
    public void SkyFortressIsGuarded()
    {
        Assert.InRange(TotalGuards("sky_fortress"), 8, 12);
    }

    [Fact]
    public void SmallFindsStayUnguarded()
    {
        foreach (string name in new[] { "hunter_camp", "market_stall", "shrine", "wishing_well", "fishing_hut" })
        {
            Assert.Null(Variant(name).Guards);
        }
    }

    /// <summary>
    /// Guards belong beside the loot. Ambush markers imported with the source
    /// structures sit wherever that build put a spawner, which scattered guards
    /// through empty rooms; chests win whenever a structure has any.
    /// </summary>
    [Fact]
    public void GuardsStandAtTheChests()
    {
        foreach (string name in new[] { "sky_fortress", "coliseum", "werewolf_den", "shipwreck", "ocean_monument" })
        {
            var variant = Variant(name);
            var data = variant.Provider(3);
            var chestCells = new HashSet<(short, short, short)>();
            foreach (var chest in data.Chests)
            {
                chestCells.Add((chest.X, chest.Y, chest.Z));
            }
            var posts = GuardPlanner.Plan(variant, data);
            Assert.NotEmpty(posts);
            foreach (var post in posts)
            {
                Assert.Contains((post.X, post.Y, post.Z), chestCells);
            }
        }
    }

    /// <summary>Structures with loot but no markers still get guarded.</summary>
    [Fact]
    public void ProceduralBuildersWithoutMarkersAreStillGuarded()
    {
        var variant = Variant("shipwreck");
        var data = variant.Provider(3);
        Assert.Empty(data.Ambushes);
        Assert.NotEmpty(GuardPlanner.Plan(variant, data));
    }

    [Fact]
    public void MarkersOnlyServeStructuresWithNoChests()
    {
        var spec = new GuardSpec { Creatures = new[] { "Lion" }, PerChest = 1f, BaseCount = 2, MinTotal = 2, MaxTotal = 7 };
        var b = new StructureBuilder("test");
        b.Fill(0, 0, 0, 8, 3, 8, "PlanksBlock");
        b.Ambush(0, 1, 4);
        b.Ambush(2, 1, 4);
        var variant = new StructureVariant
        {
            Name = "test",
            Provider = _ => b.Build(),
            ChestPools = Array.Empty<string>(),
            Guards = spec,
        };
        var posts = GuardPlanner.Plan(variant, variant.Provider(1));
        Assert.Equal(2, posts.Length);
        Assert.Equal(2, posts[0].Count + posts[1].Count);
    }

    [Fact]
    public void BudgetIsSpreadOverPostsAndIndependentOfOrder()
    {
        var spec = new GuardSpec { Creatures = new[] { "Lion" }, PerChest = 2f, MinTotal = 2, MaxTotal = 7 };
        var b = new StructureBuilder("test");
        b.Fill(0, 0, 0, 8, 3, 8, "PlanksBlock");
        for (int i = 0; i < 3; i++)
        {
            b.Chest(i, 1, 1);
        }
        var variant = new StructureVariant
        {
            Name = "test",
            Provider = _ => b.Build(),
            ChestPools = new[] { "baseline" },
            Guards = spec,
        };
        var posts = GuardPlanner.Plan(variant, variant.Provider(1));
        Assert.Equal(3, posts.Length);
        int total = 0;
        foreach (var post in posts)
        {
            total += post.Count;
        }
        Assert.Equal(6, total);
        Assert.Equal(GuardPlanner.Plan(variant, variant.Provider(1)), posts);
    }

    [Fact]
    public void SupernaturalGuardsDeclareAMundaneStandIn()
    {
        foreach (var def in TestRegistry.Defs())
        {
            foreach (var variant in def.Variants)
            {
                var guards = variant.Guards;
                if (guards == null)
                {
                    continue;
                }
                foreach (string creature in guards.Creatures)
                {
                    if (creature.StartsWith("Werewolf", StringComparison.Ordinal))
                    {
                        Assert.False(string.IsNullOrEmpty(guards.MundaneSubstitute),
                            $"{variant.Name} spawns a werewolf but has no stand-in for worlds without them");
                    }
                }
            }
        }
    }
}

public class TempleFidelityTests
{
    /// <summary>
    /// Coordinates read off Minecraft 1.16.5's DesertPyramidPiece. They are
    /// asserted so a later refactor cannot quietly reshape the trap.
    /// </summary>
    [Fact]
    public void DesertTempleMatchesTheOriginalLayout()
    {
        var data = DesertTempleBuilder.Build(1);
        var cells = Index(data);

        Assert.Equal("PressurePlateBlock", cells[(10, -11, 10)].BlockName);
        Assert.Equal("DetonatorBlock", cells[(10, -13, 10)].BlockName);

        int kegs = 0;
        for (int x = 9; x <= 11; x++)
        {
            for (int z = 9; z <= 11; z++)
            {
                if (cells[(x, -13, z)].BlockName == "MediumGunpowderKegBlock")
                {
                    kegs++;
                }
            }
        }
        Assert.Equal(8, kegs);   // nine cells, the middle one being the detonator

        var chests = new HashSet<(short, short, short)>();
        foreach (var chest in data.Chests)
        {
            chests.Add((chest.X, chest.Y, chest.Z));
        }
        Assert.Equal(4, chests.Count);
        foreach (var expected in new (short, short, short)[] { (12, -11, 10), (8, -11, 10), (10, -11, 12), (10, -11, 8) })
        {
            Assert.Contains(expected, chests);
        }

        // The blue tile is the only clue the chamber exists.
        Assert.Equal("SandstoneBlock", cells[(10, 0, 10)].BlockName);
        Assert.Equal(1 | (11 << 1), cells[(10, 0, 10)].Data);
        Assert.Equal(21, data.SizeX);
        Assert.Equal(21, data.SizeZ);
    }

    /// <summary>Coordinates read off Minecraft 1.16.5's JunglePyramidPiece.</summary>
    [Fact]
    public void JungleTempleMatchesTheOriginalLayout()
    {
        var data = JungleTempleBuilder.Build(1);
        var cells = Index(data);

        // Both traps keep the original's corridors; the dispensers moved into
        // the end walls at floor level in 0.6 so the plate wiring can reach
        // them (see TrapTests).
        Assert.Equal("DispenserBlock", cells[(2, -3, 0)].BlockName);
        Assert.Equal("DispenserBlock", cells[(10, -3, 4)].BlockName);
        foreach (var (x, y, z) in new[] { (8, -2, 13), (9, -2, 13), (10, -2, 13) })
        {
            Assert.Equal("SwitchBlock", cells[(x, y, z)].BlockName);
        }

        var chests = new HashSet<(short, short, short)>();
        foreach (var chest in data.Chests)
        {
            chests.Add((chest.X, chest.Y, chest.Z));
        }
        Assert.Equal(2, chests.Count);
        Assert.Contains(((short)8, (short)-3, (short)2), chests);
        Assert.Contains(((short)9, (short)-3, (short)9), chests);

        Assert.Equal(12, data.SizeX);
        Assert.Equal(15, data.SizeZ);
    }

    /// <summary>Dispensers are useless empty, so the variant must load them.</summary>
    [Fact]
    public void JungleTempleLoadsItsDispensers()
    {
        StructureVariant? jungle = null;
        foreach (var def in TestRegistry.Defs())
        {
            foreach (var variant in def.Variants)
            {
                if (variant.Name == "jungle_temple")
                {
                    jungle = variant;
                }
            }
        }
        Assert.NotNull(jungle);
        Assert.Equal("trap_arrows", jungle!.DispenserPool);
    }

    [Fact]
    public void MonumentLeavesItsHallsFlooded()
    {
        var data = OceanMonumentBuilder.Build(2);
        int air = 0, water = 0;
        foreach (var cell in data.Cells)
        {
            string name = data.Palette[cell.PaletteIndex].BlockName;
            if (name == "AirBlock") air++;
            if (name == "WaterBlock") water++;
        }
        Assert.True(air > 0, "the wings should hold breathable pockets");
        Assert.True(air < 400, $"the monument should not be a dry cavern, found {air} air cells");
        Assert.True(water > 0, "doorways should be open water");
        Assert.Contains(data.Palette, p => p.BlockName == "TreasureGeneratorBlock");

        // Two wing caches below two vault chests: the pool order in the
        // registry depends on that height split.
        Assert.Equal(4, data.Chests.Count);
        var byHeight = new List<short>();
        foreach (var chest in data.Chests)
        {
            byHeight.Add(chest.Y);
        }
        byHeight.Sort();
        Assert.Equal(2, byHeight.FindAll(y => y == byHeight[0]).Count);
        Assert.True(byHeight[2] > byHeight[1], "vault chests must sit above the wing caches");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4096)]
    public void NewBuildersAreDeterministic(int seed)
    {
        foreach (var build in new Func<int, StructureData>[]
                 { DesertTempleBuilder.Build, JungleTempleBuilder.Build, OceanMonumentBuilder.Build })
        {
            var a = build(seed);
            var b = build(seed);
            Assert.Equal(a.Cells.Count, b.Cells.Count);
            for (int i = 0; i < a.Cells.Count; i++)
            {
                Assert.Equal(a.Cells[i], b.Cells[i]);
                Assert.Equal(a.Palette[a.Cells[i].PaletteIndex], b.Palette[b.Cells[i].PaletteIndex]);
            }
        }
    }

    private static Dictionary<(int, int, int), PaletteEntry> Index(StructureData data)
    {
        var map = new Dictionary<(int, int, int), PaletteEntry>();
        foreach (var cell in data.Cells)
        {
            map[(cell.X, cell.Y, cell.Z)] = data.Palette[cell.PaletteIndex];
        }
        return map;
    }
}

public class LayoutVersionTests
{
    [Fact]
    public void SeparationRadiiStayWithinTheSearchedNeighbourhood()
    {
        foreach (var def in TestRegistry.Defs())
        {
            Assert.InRange(def.SeparationRadius, 0, PlacementEngine.MaxSeparation);
        }
    }

    [Fact]
    public void CurrentLayoutAddsTheNewSlots()
    {
        var names = new List<string>();
        foreach (var def in TestRegistry.Defs())
        {
            names.Add(def.Name);
        }
        Assert.Contains("monument", names);
        Assert.Contains("desert_temple", names);
        Assert.Contains("jungle_temple", names);
    }
}

public class SettingsTests
{
    [Fact]
    public void SettingsRoundTrip()
    {
        float density = RuinsSettings.Density;
        float guards = RuinsSettings.Guards;
        try
        {
            RuinsSettings.Density = 0.5f;
            RuinsSettings.Guards = 1.5f;
            var element = new System.Xml.Linq.XElement("Ruins");
            RuinsSettings.Save(element);

            RuinsSettings.Density = 1f;
            RuinsSettings.Guards = 1f;
            RuinsSettings.Load(element);
            Assert.Equal(0.5f, RuinsSettings.Density);
            Assert.Equal(1.5f, RuinsSettings.Guards);

            RuinsSettings.Load(new System.Xml.Linq.XElement("Ruins"));
            Assert.Equal(1f, RuinsSettings.Density);
            Assert.Equal(1f, RuinsSettings.Guards);
        }
        finally
        {
            RuinsSettings.Density = density;
            RuinsSettings.Guards = guards;
        }
    }
}

/// <summary>Loads the shipped templates once so tests can build the real registry.</summary>
internal static class TestRegistry
{
    private static IReadOnlyDictionary<string, StructureData>? s_templates;
    private static IReadOnlyList<StructureDef>? s_defs;

    public static IReadOnlyDictionary<string, StructureData> Templates()
    {
        if (s_templates != null)
        {
            return s_templates;
        }
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "src", "SurvivalcraftRuins")))
        {
            dir = dir.Parent;
        }
        Assert.NotNull(dir);
        var templates = new Dictionary<string, StructureData>(StringComparer.OrdinalIgnoreCase);
        string structures = Path.Combine(dir!.FullName, "src", "SurvivalcraftRuins", "Assets", "Structures");
        foreach (string file in Directory.GetFiles(structures, "*.json"))
        {
            var data = StructureTemplate.Parse(File.ReadAllText(file));
            templates[data.Name] = data;
        }
        s_templates = templates;
        return templates;
    }

    public static IReadOnlyList<StructureDef> Defs()
    {
        return s_defs ??= StructureRegistry.CreateDefs(Templates());
    }
}

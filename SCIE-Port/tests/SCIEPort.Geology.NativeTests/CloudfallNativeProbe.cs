using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Engine;
using Game;

public static class CloudfallNativeProbe
{
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static MethodInfo choose, geology, landmarks, profile, planColumn, finish;
    private static int checks;

    public static void Run(Assembly mod, Func<TerrainGenerationMode, int, SubsystemTerrain> fixture)
    {
        choose = mod.GetType("SCIEPort.Features.Geology.GeologySites", true).GetMethod("Choose", Static);
        geology = mod.GetType("SCIEPort.Features.Geology.GeologyTerrain", true).GetMethod("Generate", Static);
        landmarks = mod.GetType("SCIEPort.Features.Landmarks.NaturalLandmarks", true).GetMethod("Generate", Static);
        profile = mod.GetType("SCIEPort.Features.Geology.CloudfallProfiles", true).GetMethod("At", Static);
        planColumn = mod.GetType("SCIEPort.Features.Geology.GeologyTerrain", true).GetMethod("PlanColumn", Static);
        finish = mod.GetType("SCIEPort.Features.Geology.GeologyTerrain", true).GetMethod("Finish", Static);
        int seed = WorldSeed("CloudfallPeaks");
        foreach (var mode in new[] { TerrainGenerationMode.Continent, TerrainGenerationMode.Better, TerrainGenerationMode.Island })
        {
            ITerrainContentsGenerator generator;
            using (Terrain terrain = TestWorld(fixture(mode, seed), mode, out generator))
            {
                SurveyPeaks(terrain, generator, seed, mode);
                SurveyWaterfalls(terrain, generator, seed, mode);
            }
        }
        CheckStairs();
        Console.WriteLine("Cloudfall and waterfall fixed target: " + checks + " checks passed.");
    }

    private static int WorldSeed(string text)
    {
        int seed = 0;
        for (int i = 0; i < text.Length; i++) seed += text[i] * (1 + 29 * i);
        return seed;
    }

    private static Terrain TestWorld(SubsystemTerrain world, TerrainGenerationMode mode, out ITerrainContentsGenerator generator)
    {
        world.SubsystemGameInfo = world.Project.FindSubsystem<SubsystemGameInfo>(true);
        world.SubsystemGameInfo.WorldSettings.StartingPositionMode = StartingPositionMode.Easy;
        if (mode == TerrainGenerationMode.Island)
            world.SubsystemGameInfo.WorldSettings.IslandSize = new Vector2(768, 768);
        generator = mode == TerrainGenerationMode.Better ? (ITerrainContentsGenerator)new TerrainContentsGeneratorBetter(world)
            : new TerrainContentsGenerator24(world);
        return world.Terrain;
    }

    private static void SurveyPeaks(Terrain terrain, ITerrainContentsGenerator generator, int seed, TerrainGenerationMode mode)
    {
        Vector3 spawn = generator.FindCoarseSpawnPosition();
        int rx = (int)Math.Floor(spawn.X / 192), rz = (int)Math.Floor(spawn.Z / 192);
        var sites = new List<object>();
        for (int z = rz - 4; z <= rz + 4; z++)
        for (int x = rx - 4; x <= rx + 4; x++)
        {
            object site = choose.Invoke(null, new object[] { generator, seed, new Point2(x * 192, z * 192) });
            if (site != null && Field<object>(site, "Kind").ToString() == "CloudfallPeaks") sites.Add(site);
        }
        int complete = 0;
        object nearest = null;
        foreach (object site in sites.OrderBy(site => Distance(site, spawn)))
        {
            Point2 center = Position(site, 0, 0);
            TerrainChunk chunk = terrain.AllocateChunk(center.X >> 4, center.Y >> 4);
            Prepare(generator, chunk);
            geology.Invoke(null, new object[] { chunk, generator, seed, mode });
            if (chunk.ModificationCounter == 0) Console.WriteLine(RejectedColumn(chunk, site));
            generator.GenerateChunkContentsPass4(chunk);
            finish.Invoke(null, new object[] { chunk, generator, seed, mode });
            if (HasThreeFalls(terrain, site))
            {
                complete++;
                if (nearest == null) nearest = site;
            }
            else Console.WriteLine(mode + " incomplete core at " + center + " changes=" + chunk.ModificationCounter +
                ": " + InspectFall(terrain, site));
            terrain.FreeChunk(chunk);
        }
        Console.WriteLine(mode + " CloudfallPeaks seed CloudfallPeaks: " + complete + "/" + sites.Count +
            " selected sites with three complete falls in 81 nearby regions, spawn " + spawn + ".");
        Check(nearest != null, mode + " has a reachable nearby cloudfall with all three native waterfalls and the shared pool");
        VerifySite(terrain, generator, seed, mode, nearest);
    }

    private static string RejectedColumn(TerrainChunk chunk, object site)
    {
        object writes = Activator.CreateInstance(planColumn.GetParameters()[5].ParameterType);
        for (int z = -6; z <= 6; z++)
        for (int x = -6; x <= 6; x++)
        {
            int localX = x + 8, localZ = z + 8;
            object shape = profile.Invoke(null, new object[] { site, x, z });
            if ((bool)planColumn.Invoke(null, new[] { chunk, (object)localX, localZ, shape, Field<object>(site, "Kind"), writes }))
                continue;
            int top = chunk.CalculateTopmostCellHeight(localX, localZ);
            return "Rejected core column " + localX + "," + localZ + " native top=" + top + " IDs=" +
                string.Join(",", Enumerable.Range(Math.Max(3, top - 20), 21).Select(y => chunk.GetCellContentsFast(localX, y, localZ)).Distinct());
        }
        return "All individual core columns passed the preflight.";
    }

    private static void VerifySite(Terrain terrain, ITerrainContentsGenerator generator, int seed, TerrainGenerationMode mode, object site)
    {
        Point2 center = Position(site, 0, 0);
        Point2 lookout = Position(site, 0, 26);
        int pool = Field<int>(site, "Base") - 2;
        for (int dz = -3; dz <= 3; dz++)
        for (int dx = -3; dx <= 3; dx++)
            CreateChunk(terrain, generator, center + new Point2(dx * 16, dz * 16), mode, seed, false);
        Check(HasThreeFalls(terrain, site), "native fourth pass leaves the complete exhibit intact");
        CheckNativeWaterUpdate(terrain, site);
        int peaks = terrain.AllocatedChunks.Count(chunk => chunk.Cells.Any(value => Terrain.ExtractContents(value) == 893));
        Check(peaks >= 6, "moss and peaks spread over several chunks");
        for (int v = 6; v <= 29; v++)
        {
            Point2 point = Position(site, 0, v);
            int y = pool + 1 + Math.Min((v - 6) / 2, 9);
            int floor = terrain.GetCellContents(point.X, y, point.Y);
            int above = terrain.GetCellContents(point.X, y + 1, point.Y), head = terrain.GetCellContents(point.X, y + 2, point.Y);
            Check(floor == 892 && (above == 0 || above == SnowBlock.Index) && head == 0,
                "unobstructed grade at " + point + " Y=" + y + " path step=" + v +
                " actual=" + floor + ", " + above + ", " + head + " native height=" + generator.CalculateHeight(point.X, point.Y));
        }
        Check(terrain.GetCellContents(lookout.X, pool + 10, lookout.Y) == 892, "lookout platform is present");
        foreach (TerrainChunk chunk in terrain.AllocatedChunks.ToArray()) terrain.FreeChunk(chunk);

        var blocked = terrain.AllocateChunk(center.X >> 4, center.Y >> 4);
        Prepare(generator, blocked);
        blocked.IsLoaded = true;
        int[] before = (int[])blocked.Cells.Clone();
        geology.Invoke(null, new object[] { blocked, generator, seed, mode });
        Check(blocked.Cells.SequenceEqual(before), "saved core remains byte-for-byte intact");
        blocked.IsLoaded = false;
        blocked.SetCellValueFast(center.X & 15, pool, center.Y & 15, Terrain.MakeBlockValue(ChestBlock.Index));
        before = (int[])blocked.Cells.Clone();
        geology.Invoke(null, new object[] { blocked, generator, seed, mode });
        Check(blocked.Cells.SequenceEqual(before), "one obstructed core cell cancels the whole hydraulic footprint");
        terrain.FreeChunk(blocked);
        Console.WriteLine(mode + " cloudfall center X=" + center.X + " Z=" + center.Y + " pool Y=" + pool +
            ", lookout X=" + lookout.X + " Y=" + (pool + 11) + " Z=" + lookout.Y + ".");
    }

    private static void CheckNativeWaterUpdate(Terrain terrain, object site)
    {
        var water = new SubsystemWaterBlockBehavior();
        var subsystem = new SubsystemTerrain { Terrain = terrain };
        typeof(SubsystemBlockBehavior).GetProperty("SubsystemTerrain").SetValue(water, subsystem, null);
        var updater = (TerrainUpdater)FormatterServices.GetUninitializedObject(typeof(TerrainUpdater));
        typeof(TerrainUpdater).GetField("m_terrain", BindingFlags.Public | Instance).SetValue(updater, terrain);
        typeof(SubsystemTerrain).GetField("<TerrainUpdater>k__BackingField", Instance).SetValue(subsystem, updater);
        var behaviors = new SubsystemBlockBehaviors();
        behaviors.m_blockBehaviorsByContents = new SubsystemBlockBehavior[BlocksManager.Blocks.Length][];
        for (int i = 0; i < behaviors.m_blockBehaviorsByContents.Length; i++)
            behaviors.m_blockBehaviorsByContents[i] = new SubsystemBlockBehavior[0];
        behaviors.m_blockBehaviorsByContents[WaterBlock.Index] = new SubsystemBlockBehavior[] { water };
        subsystem.m_subsystemBlockBehaviors = behaviors;
        int pool = Field<int>(site, "Base") - 2;
        for (int v = -2; v <= 4; v++)
        for (int u = -5; u <= 5; u++)
        {
            Point2 point = Position(site, u, v);
            for (int y = pool - 2; y <= pool + 30; y++)
                if (terrain.GetCellContents(point.X, y, point.Y) == WaterBlock.Index)
                    water.m_toUpdate[new Point3(point.X, y, point.Y)] = true;
        }
        for (int tick = 0; tick < 4; tick++) water.SpreadFluid();
        Check(HasThreeFalls(terrain, site), "native fluid updates retain all three falls: " + InspectFall(terrain, site));
        for (int u = -6; u <= 6; u++)
        {
            Point2 lip = Position(site, u, 5);
            Check(terrain.GetCellContents(lip.X, pool, lip.Y) == 892 &&
                terrain.GetCellContents(lip.X, pool + 1, lip.Y) == 892, "native water stays within the pool wall");
        }
    }

    private static bool HasThreeFalls(Terrain terrain, object site)
    {
        int pool = Field<int>(site, "Base") - 2;
        for (int i = 0; i < 3; i++)
        {
            int top = pool + 14 + i * 8;
            Point2 source = Position(site, -4 + 4 * i, -2);
            Point2 fall = Position(site, -4 + 4 * i, -1);
            if (!Water(terrain.GetCellValue(source.X, top, source.Y), 0) ||
                !FluidBlock.GetIsTop(Terrain.ExtractData(terrain.GetCellValue(fall.X, top, fall.Y)))) return false;
            for (int y = pool + 1; y <= top; y++)
                if (!Water(terrain.GetCellValue(fall.X, y, fall.Y), 1)) return false;
        }
        for (int v = -1; v <= 4; v++)
        for (int u = -5; u <= 5; u++)
        {
            Point2 point = Position(site, u, v);
            if (!Water(terrain.GetCellValue(point.X, pool, point.Y), 0) ||
                terrain.GetCellContents(point.X, pool - 3, point.Y) != 892) return false;
        }
        return true;
    }

    private static string InspectFall(Terrain terrain, object site)
    {
        int pool = Field<int>(site, "Base") - 2;
        return string.Join(" | ", Enumerable.Range(0, 3).Select(i =>
        {
            int top = pool + 14 + i * 8;
            Point2 source = Position(site, -4 + 4 * i, -2), fall = Position(site, -4 + 4 * i, -1);
            return "Y=" + top + " src=" + terrain.GetCellContents(source.X, top, source.Y) +
                " fall=" + terrain.GetCellContents(fall.X, top, fall.Y) +
                " water=" + Enumerable.Range(pool, top - pool + 1).Count(y => terrain.GetCellContents(fall.X, y, fall.Y) == 18);
        }));
    }

    private static void SurveyWaterfalls(Terrain terrain, ITerrainContentsGenerator generator, int seed, TerrainGenerationMode mode)
    {
        Vector3 spawn = generator.FindCoarseSpawnPosition();
        int cx = (int)Math.Floor(spawn.X / 16), cz = (int)Math.Floor(spawn.Z / 16);
        var candidates = new List<Point2>();
        for (int dz = -16; dz < 16; dz++)
        for (int dx = -16; dx < 16; dx++)
        {
            int x = (cx + dx) * 16 + 8, z = (cz + dz) * 16 + 8;
            if (generator.CalculateHeight(x, z) >= generator.OceanLevel - 1 && generator.CalculateOceanShoreDistance(x, z) >= 0)
                candidates.Add(new Point2(x, z));
        }
        int count = 0;
        Point3? first = null;
        foreach (Point2 point in candidates.OrderBy(p => (p.X - spawn.X) * (p.X - spawn.X) + (p.Y - spawn.Z) * (p.Y - spawn.Z)))
        {
            TerrainChunk chunk = CreateChunk(terrain, generator, point, mode, seed, true);
            Point3? fall = FindSmallFall(chunk, generator.OceanLevel);
            if (fall.HasValue)
            {
                count++;
                if (!first.HasValue) first = fall;
            }
            terrain.FreeChunk(chunk);
        }
        Console.WriteLine(mode + " small waterfalls: " + count + "/" + candidates.Count + " inland chunks, first " + first + ".");
        Check(first.HasValue, mode + " native terrain contains a real small waterfall in the spawn survey");
    }

    private static Point3? FindSmallFall(TerrainChunk chunk, int sea)
    {
        for (int z = 2; z <= 13; z++)
        for (int x = 2; x <= 13; x++)
        for (int y = sea + 4; y <= chunk.CalculateTopmostCellHeight(x, z); y++)
        {
            if (!Water(chunk.GetCellValueFast(x, y, z), 1)) continue;
            bool source = Water(chunk.GetCellValueFast(x - 1, y, z), 0) || Water(chunk.GetCellValueFast(x + 1, y, z), 0) ||
                Water(chunk.GetCellValueFast(x, y, z - 1), 0) || Water(chunk.GetCellValueFast(x, y, z + 1), 0);
            if (!source) continue;
            int bottom = y;
            while (bottom > sea && Water(chunk.GetCellValueFast(x, bottom, z), 1)) bottom--;
            if (y - bottom >= 4 && Water(chunk.GetCellValueFast(x, bottom, z), 0))
                return new Point3(chunk.Origin.X + x, y, chunk.Origin.Y + z);
        }
        return null;
    }

    private static bool Water(int value, int level)
    {
        return Terrain.ExtractContents(value) == WaterBlock.Index && FluidBlock.GetLevel(Terrain.ExtractData(value)) == level;
    }

    private static TerrainChunk CreateChunk(Terrain terrain, ITerrainContentsGenerator generator, Point2 point,
        TerrainGenerationMode mode, int seed, bool small)
    {
        var chunk = terrain.AllocateChunk(point.X >> 4, point.Y >> 4);
        Prepare(generator, chunk);
        if (!small) geology.Invoke(null, new object[] { chunk, generator, seed, mode });
        generator.GenerateChunkContentsPass4(chunk);
        if (!small) finish.Invoke(null, new object[] { chunk, generator, seed, mode });
        if (small) landmarks.Invoke(null, new object[] { chunk, generator, seed, mode });
        return chunk;
    }

    private static void Prepare(ITerrainContentsGenerator generator, TerrainChunk chunk)
    {
        generator.GenerateChunkContentsPass1(chunk);
        generator.GenerateChunkContentsPass2(chunk);
        generator.GenerateChunkContentsPass3(chunk);
    }

    private static Point2 Position(object site, int u, int v)
    {
        Point2 facing = Field<Point2>(site, "Facing");
        return new Point2(Field<int>(site, "X") + facing.X * v - facing.Y * u,
            Field<int>(site, "Z") + facing.Y * v + facing.X * u);
    }

    private static float Distance(object site, Vector3 spawn)
    {
        Point2 point = Position(site, 0, 0);
        float x = point.X - spawn.X, z = point.Y - spawn.Z;
        return x * x + z * z;
    }

    private static T Field<T>(object site, string name)
    {
        return (T)site.GetType().GetField(name, Instance).GetValue(site);
    }

    private static void CheckStairs()
    {
        var stairs = BlocksManager.Blocks[894] as StairsBlock;
        Check(stairs != null && stairs.IsPlaceable && stairs.DefaultCategory == "SCIEPort.Structures", "registered native stairs");
        CraftingRecipe recipe = stairs.GetProceduralCraftingRecipes().Single();
        var rock = BlocksManager.Blocks[892].CraftingId + ":0";
        string[] actual = { rock, null, null, rock, rock, null, rock, rock, rock };
        Check(recipe.ResultValue == Terrain.MakeBlockValue(894) && recipe.ResultCount == 4 &&
            CraftingRecipesManager.MatchRecipe(recipe.Ingredients, actual), "native workbench creates four peak stairs");
    }

    private static void Check(bool condition, string label)
    {
        checks++;
        if (!condition) throw new InvalidOperationException("Cloudfall: " + label);
    }
}

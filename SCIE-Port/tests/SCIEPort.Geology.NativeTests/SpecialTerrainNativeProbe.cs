using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Engine;
using Game;

public static class SpecialTerrainNativeProbe
{
    private const BindingFlags Static = BindingFlags.NonPublic | BindingFlags.Static;
    private const BindingFlags Instance = BindingFlags.NonPublic | BindingFlags.Instance;
    private static MethodInfo chooseLandmarks, generateLandmark, chooseGeology, generateGeology;
    private static int checks;

    private static void Check(bool condition, string description)
    {
        checks++;
        if (!condition) throw new InvalidOperationException("Special terrain: " + description);
    }

    public static void Run(Assembly mod, Func<TerrainGenerationMode, int, SubsystemTerrain> fixture)
    {
        const string landmarks = "SCIEPort.Features.Landmarks.";
        const string geology = "SCIEPort.Features.Geology.";
        chooseLandmarks = mod.GetType(landmarks + "LandmarkDeck", true).GetMethod("Choose", Static);
        generateLandmark = mod.GetType(landmarks + "NaturalLandmarks", true).GetMethod("Generate", Static);
        chooseGeology = mod.GetType(geology + "GeologySites", true).GetMethod("Choose", Static);
        generateGeology = mod.GetType(geology + "GeologyTerrain", true).GetMethod("Generate", Static);
        Deck();
        Local("StoneCircle", new Habitat(80, 10, 8, 45f, GraniteBlock.Index, GrassBlock.Index), LimestoneBlock.Index);
        Local("Hoodoo", new Habitat(72, 11, 4, 45f, GraniteBlock.Index, SandBlock.Index), SandstoneBlock.Index);
        Local("Serac", new Habitat(104, 0, 7, 40f, GraniteBlock.Index, GraniteBlock.Index), 1011);
        Local("DuneRidge", new Habitat(71, 12, 3, 50f, SandstoneBlock.Index, SandBlock.Index), SandBlock.Index);
        Local("SeaStacks", new Habitat(58, 10, 6, -10f, BasaltBlock.Index, BasaltBlock.Index), BasaltBlock.Index);
        Local("Sinkhole", new Habitat(82, 11, 10, 45f, LimestoneBlock.Index, GrassBlock.Index), GravelBlock.Index);
        Local("ScreeFan", new Habitat(89, 10, 6, 45f, GraniteBlock.Index, GraniteBlock.Index, 1), GravelBlock.Index);
        Local("MeadowHummocks", new Habitat(80, 10, 10, 45f, GraniteBlock.Index, GrassBlock.Index), GraniteBlock.Index);
        Local("FrostPolygons", new Habitat(105, 0, 6, 40f, GraniteBlock.Index, GraniteBlock.Index), SnowBlock.Index);
        Fallback();
        SharedHabitat("warm inland", new Habitat(88, 12, 8, 44f, LimestoneBlock.Index, GrassBlock.Index),
            new[] { "KarstBridges", "PaintedHills", "Pinnacles", "RiverCave", "ColumnarBasalt", "GraniteDomes", "DryCanyon", "ErodedBadlands" });
        SharedHabitat("warm coastal dunes", new Habitat(73, 12, 8, 39f, SandstoneBlock.Index, SandBlock.Index),
            new[] { "DuneLagoons", "Terraces", "GraniteDomes", "RiverCave" });
        SharedHabitat("shallow sea", new Habitat(58, 10, 8, -17f, LimestoneBlock.Index, SandBlock.Index),
            new[] { "BlueHole", "BasaltCoast" });
        SharedHabitat("cold upland", new Habitat(106, 0, 7, 44f, GraniteBlock.Index, GraniteBlock.Index),
            new[] { "AlpineGlacier", "IceCave", "GlacialCirque" });
        WonderGeometry();
        NativeLandmarks(fixture, 167537);
        Regional(fixture, TerrainGenerationMode.Continent, 167537);
        Regional(fixture, TerrainGenerationMode.Better, 167537);
        Console.WriteLine("Special terrain fixed target: " + checks + " checks passed.");
    }

    private static string[] Draw(int seed, Point2 chunk)
    {
        object[] args = { seed, chunk, null, null };
        bool selected = (bool)chooseLandmarks.Invoke(null, args);
        Check(selected == (args[2] != null || args[3] != null), "selection reflects candidate slots");
        return new[] { Convert.ToString(args[2]), Convert.ToString(args[3]) };
    }

    private static void Deck()
    {
        foreach (int seed in new[] { 167537, -20167805 })
        {
            const int side = 97;
            var choices = new Dictionary<Point2, string[]>();
            var all = new HashSet<string>();
            int selected = 0, totalChoices = 0;
            for (int z = -48; z < 49; z++)
            for (int x = -48; x < 49; x++)
            {
                var position = new Point2(x, z);
                string[] draw = Draw(seed, position);
                choices.Add(position, draw);
                if (draw.Any(s => s.Length != 0)) selected++;
                foreach (string kind in draw.Where(s => s.Length != 0))
                {
                    all.Add(kind);
                    totalChoices++;
                }
                Check(draw[0].Length == 0 || draw[0] != draw[1], "distinct primary and fallback");
            }
            foreach (var pair in choices)
            {
                for (int dz = 0; dz <= 2; dz++)
                for (int dx = dz == 0 ? 1 : -2; dx <= 2; dx++)
                {
                    string[] adjacent;
                    if (!choices.TryGetValue(pair.Key + new Point2(dx, dz), out adjacent)) continue;
                    Check(!pair.Value.Intersect(adjacent).Any(s => s.Length != 0), "candidates differ within two chunks, including corners");
                }
            }
            Check(all.Count == 14 && selected > side * side / 3 && selected <= side * side * 2 / 3,
                "all fourteen choices with bounded participation for seed " + seed);
            var negative = new Point2(-19, -23);
            Check(choices[negative].SequenceEqual(Draw(seed, negative)), "repeatable negative chunk");
            Console.WriteLine("Landmark draws " + seed + ": " + selected + "/" + side * side +
                " eligible chunks, " + totalChoices + " candidate slots, all " + all.Count + " kinds.");
        }
    }

    private static void Generate(MethodInfo method, TerrainChunk chunk, ITerrainContentsGenerator generator,
        int seed, TerrainGenerationMode mode)
    {
        method.Invoke(null, new object[] { chunk, generator, seed, mode });
    }

    private static void Local(string kind, Habitat habitat, int material)
    {
        var position = new Point2(-13, 8);
        int seed = Enumerable.Range(1, 1000).First(value => Draw(value, position)[0] == kind);
        using (var terrain = new Terrain())
        {
            var chunk = terrain.AllocateChunk(position.X, position.Y);
            habitat.GenerateChunkContentsPass4(chunk);
            int[] original = (int[])chunk.Cells.Clone();
            chunk.IsLoaded = true;
            Generate(generateLandmark, chunk, habitat, seed, TerrainGenerationMode.Continent);
            Check(chunk.Cells.SequenceEqual(original) && chunk.ModificationCounter == 0, kind + " skips stored chunks");
            chunk.IsLoaded = false;
            Generate(generateLandmark, chunk, habitat, seed, TerrainGenerationMode.Nihility);
            Check(chunk.Cells.SequenceEqual(original), kind + " skips Nihility");
            Generate(generateLandmark, chunk, habitat, seed, TerrainGenerationMode.Continent);
            Check(chunk.ModificationCounter == 1, kind + " persists a generated landmark");

            int changes = 0, added = 0;
            for (int z = 0; z < 16; z++)
            for (int x = 0; x < 16; x++)
            for (int y = 0; y < 256; y++)
            {
                int index = TerrainChunk.CalculateCellIndex(x, y, z);
                if (chunk.Cells[index] == original[index]) continue;
                Check(x >= 1 && x <= 14 && z >= 1 && z <= 14 && y >= 3 && y <= 250, kind + " write bounds");
                changes++;
                if (Terrain.ExtractContents(chunk.Cells[index]) == material) added++;
            }
            Check(changes >= 10 && changes <= 640 && added > 0, kind + " material and write budget");
            if (kind == "SeaStacks")
            {
                int pillars = 0, water = 0;
                for (int z = 1; z <= 14; z++)
                for (int x = 1; x <= 14; x++)
                {
                    if (chunk.GetCellContentsFast(x, 65, z) == BasaltBlock.Index) pillars++;
                    if (chunk.GetCellContentsFast(x, 62, z) == WaterBlock.Index) water++;
                }
                Check(pillars >= 3 && water >= 190, "stacks rise above the sea with open gaps");
            }
            if (kind == "Sinkhole") Check(SinkholeExit(chunk, habitat.Height), "sinkhole has a climbable stair route");
            Console.WriteLine(kind + " built through production selection: " + changes + " edited cells.");
        }
    }

    private static bool SinkholeExit(TerrainChunk chunk, int rim)
    {
        var floor = new int[16, 16];
        Point2 low = new Point2(1, 1);
        for (int z = 1; z <= 14; z++)
        for (int x = 1; x <= 14; x++)
        {
            floor[x, z] = chunk.CalculateTopmostCellHeight(x, z);
            if (floor[x, z] < floor[low.X, low.Y]) low = new Point2(x, z);
        }
        if (rim - floor[low.X, low.Y] < 5) return false;
        var seen = new HashSet<Point2> { low };
        var queue = new Queue<Point2>();
        queue.Enqueue(low);
        while (queue.Count > 0)
        {
            Point2 current = queue.Dequeue();
            int level = floor[current.X, current.Y];
            if (level >= rim) return true;
            foreach (Point2 offset in new[] { new Point2(1, 0), new Point2(0, 1), new Point2(-1, 0), new Point2(0, -1) })
            {
                Point2 next = current + offset;
                int step = floor[next.X, next.Y];
                if (step > 0 && Math.Abs(step - level) <= 1 && seen.Add(next)) queue.Enqueue(next);
            }
        }
        return false;
    }

    private static void Fallback()
    {
        var position = new Point2(7, -9);
        int seed = Enumerable.Range(1, 10000).First(value => Draw(value, position).SequenceEqual(new[] { "Serac", "DuneRidge" }));
        var habitat = new Habitat(73, 12, 3, 50f, SandstoneBlock.Index, SandBlock.Index);
        using (var terrain = new Terrain())
        {
            var chunk = terrain.AllocateChunk(position.X, position.Y);
            habitat.GenerateChunkContentsPass4(chunk);
            Generate(generateLandmark, chunk, habitat, seed, TerrainGenerationMode.Continent);
            Check(chunk.ModificationCounter == 1 && chunk.Cells.Any(c => Terrain.ExtractContents(c) == SandBlock.Index),
                "unsuitable ice candidate falls back to dune in hot sand");
        }
        using (var terrain = new Terrain())
        {
            var chunk = terrain.AllocateChunk(position.X, position.Y);
            habitat.GenerateChunkContentsPass4(chunk);
            for (int z = 0; z < 16; z++)
            for (int x = 0; x < 16; x++)
                if (x % 3 == 0) chunk.SetCellValueFast(x, 74, z, ChestBlock.Index);
            int[] original = (int[])chunk.Cells.Clone();
            Generate(generateLandmark, chunk, habitat, seed, TerrainGenerationMode.Continent);
            Check(chunk.Cells.SequenceEqual(original) && chunk.ModificationCounter == 0,
                "obstructed site aborts without partial writes");
        }
    }

    private static T Field<T>(object value, string name)
    {
        return (T)value.GetType().GetField(name, Instance).GetValue(value);
    }

    private static void SharedHabitat(string name, Habitat habitat, string[] expected)
    {
        var counts = new Dictionary<string, int>();
        int total = 0;
        for (int seed = 1; seed <= 400; seed++)
        {
            object first = chooseGeology.Invoke(null, new object[] { habitat, seed, new Point2(-192, 0) });
            object other = chooseGeology.Invoke(null, new object[] { habitat, seed, new Point2(-16, 176) });
            Check((first == null) == (other == null), name + " shares selection availability across the region");
            if (first == null) continue;
            string kind = Field<object>(first, "Kind").ToString();
            Check(kind == Field<object>(other, "Kind").ToString() && Field<int>(first, "X") == Field<int>(other, "X") &&
                Field<int>(first, "Z") == Field<int>(other, "Z") && Field<int>(first, "Seed") == Field<int>(other, "Seed"),
                name + " selection is deterministic across the region");
            int frequency;
            counts.TryGetValue(kind, out frequency);
            counts[kind] = frequency + 1;
            total++;
        }
        Check(expected.All(counts.ContainsKey), name + " offers all expected alternatives: " + string.Join(", ", counts.Keys));
        Check(counts.Values.Max() * expected.Length <= 1.9 * total, name + " no type dominates the eligible alternatives");
        Console.WriteLine("Shared " + name + ": " + counts.Count + " types in " + total + " sites, largest share " +
            counts.Values.Max() + "/" + total + ".");
    }

    private static object FindSite(Habitat habitat, string kind, out int seed)
    {
        for (seed = 1; seed <= 400; seed++)
        {
            object site = chooseGeology.Invoke(null, new object[] { habitat, seed, new Point2(-192, 0) });
            if (site != null && Field<object>(site, "Kind").ToString() == kind) return site;
        }
        throw new InvalidOperationException("No test site for " + kind);
    }

    private static Point2 Offset(object site, int side, int forward)
    {
        Point2 facing = Field<Point2>(site, "Facing");
        return new Point2(Field<int>(site, "X") + forward * facing.X - side * facing.Y,
            Field<int>(site, "Z") + forward * facing.Y + side * facing.X);
    }

    private static TerrainChunk FixtureChunk(Terrain terrain, Point2 point, Habitat habitat, int seed)
    {
        TerrainChunk chunk = terrain.GetChunkAtCell(point.X, point.Y);
        if (chunk != null) return chunk;
        chunk = terrain.AllocateChunk(point.X >> 4, point.Y >> 4);
        habitat.GenerateChunkContentsPass4(chunk);
        Generate(generateGeology, chunk, habitat, seed, TerrainGenerationMode.Continent);
        return chunk;
    }

    private static void WonderGeometry()
    {
        int seed;
        var sand = new Habitat(73, 12, 8, 39f, SandstoneBlock.Index, SandBlock.Index);
        object lagoons = FindSite(sand, "DuneLagoons", out seed);
        int level = Field<int>(lagoons, "Base");
        Point2 pond = Offset(lagoons, -16, 0);
        using (var terrain = new Terrain())
        {
            TerrainChunk chunk = FixtureChunk(terrain, pond, sand, seed);
            int wet = 0;
            for (int z = 2; z <= 14; z++)
            for (int x = 2; x <= 14; x++)
            {
                int value = chunk.GetCellValueFast(x, level, z);
                if (Terrain.ExtractContents(value) != 1013) continue;
                wet++;
                Check(FluidBlock.GetIsTop(Terrain.ExtractData(value)), "lagoon surface uses native top flags");
                Check(x > 2 && x < 14 && z > 2 && z < 14, "lagoon stays inside its solid rim");
                foreach (Point2 dir in new[] { new Point2(1, 0), new Point2(-1, 0), new Point2(0, 1), new Point2(0, -1) })
                {
                    int neighbor = chunk.GetCellContentsFast(x + dir.X, level, z + dir.Y);
                    Check(neighbor == 1013 || BlocksManager.Blocks[neighbor].IsFluidBlocker, "lagoon rim holds water");
                }
            }
            Check(wet >= 30, "lagoon pool generated through the production path");
        }
        using (var terrain = new Terrain())
        {
            TerrainChunk chunk = terrain.AllocateChunk(pond.X >> 4, pond.Y >> 4);
            sand.GenerateChunkContentsPass4(chunk);
            chunk.SetCellValueFast(2, level, 8, ChestBlock.Index);
            Generate(generateGeology, chunk, sand, seed, TerrainGenerationMode.Continent);
            Check(chunk.GetCellContentsFast(2, level, 8) == ChestBlock.Index &&
                !chunk.Cells.Any(c => Terrain.ExtractContents(c) == 1013), "foreign rim blocks cancel the complete lagoon");
        }

        var sea = new Habitat(58, 10, 8, -17f, LimestoneBlock.Index, SandBlock.Index);
        object hole = FindSite(sea, "BlueHole", out seed);
        using (var terrain = new Terrain())
        {
            Point2 center = Offset(hole, 0, 0);
            Point2 rim = Offset(hole, 0, 18);
            FixtureChunk(terrain, center, sea, seed);
            FixtureChunk(terrain, rim, sea, seed);
            Check(terrain.GetCellContents(center.X, 42, center.Y) == WaterBlock.Index &&
                terrain.GetCellContents(center.X, 37, center.Y) == 1010, "blue hole is filled from the sea to its deep limestone floor");
            Check(terrain.GetCellContents(rim.X, 60, rim.Y) == 1008 &&
                terrain.GetCellContents(rim.X, 64, rim.Y) == WaterBlock.Index, "blue hole has a submerged pale reef rim");
        }

        var rock = new Habitat(88, 12, 8, 44f, LimestoneBlock.Index, GrassBlock.Index);
        object gorge = FindSite(rock, "KarstBridges", out seed);
        using (var terrain = new Terrain())
        {
            int bend = 2 + Field<int>(gorge, "Seed") % 5;
            foreach (int along in new[] { -18, 0, 18 })
            {
                int side = (int)Math.Round(bend * Math.Sin(along * .12));
                int floor = rock.Height - 12 + Math.Max(0, Math.Abs(along) - 15) * 2 / 3;
                for (int dx = -2; dx <= 2; dx++)
                {
                    Point2 cell = Offset(gorge, side + dx, along);
                    FixtureChunk(terrain, cell, rock, seed);
                    Check(terrain.GetCellContents(cell.X, floor, cell.Y) == 1010, "gorge bridge has a solid walking floor");
                    for (int y = floor + 1; y <= floor + 5; y++)
                        Check(terrain.GetCellContents(cell.X, y, cell.Y) == 0, "gorge bridge clear walking space");
                    Check(terrain.GetCellContents(cell.X, rock.Height + 3, cell.Y) == 1010, "gorge bridge spans the opening");
                }
            }
        }
    }

    private static void NativeLandmarks(Func<TerrainGenerationMode, int, SubsystemTerrain> fixture, int seed)
    {
        var mode = TerrainGenerationMode.Continent;
        var world = fixture(mode, seed);
        var generator = new TerrainContentsGenerator24(world);
        var remaining = new HashSet<string> { "StoneCircle", "Hoodoo", "Serac", "DuneRidge", "SeaStacks", "Sinkhole",
            "ScreeFan", "MeadowHummocks", "FrostPolygons" };
        for (int z = -96; z < 192 && remaining.Count > 0; z++)
        for (int x = -96; x < 192 && remaining.Count > 0; x++)
        {
            int px = x * 16 + 8, pz = z * 16 + 8;
            int height = (int)Math.Round(generator.CalculateHeight(px, pz));
            int sea = generator.OceanLevel;
            int heat = generator.CalculateTemperature(px, pz) + SubsystemWeather.GetTemperatureAdjustmentAtHeight(height);
            int humidity = generator.CalculateHumidity(px, pz);
            float shore = generator.CalculateOceanShoreDistance(px, pz);
            string[] choices = Draw(seed, new Point2(x, z));
            if ((choices[0].Length == 0) == (choices[1].Length == 0)) continue;
            string kind = choices[0].Length == 0 ? choices[1] : choices[0];
            if (!remaining.Contains(kind) || !LikelyHabitat(kind, height - sea, heat, humidity, shore)) continue;

            var chunk = world.Terrain.AllocateChunk(x, z);
            generator.GenerateChunkContentsPass1(chunk);
            generator.GenerateChunkContentsPass2(chunk);
            generator.GenerateChunkContentsPass3(chunk);
            generator.GenerateChunkContentsPass4(chunk);
            int before = chunk.ModificationCounter;
            Generate(generateLandmark, chunk, generator, seed, mode);
            if (chunk.ModificationCounter == before + 1)
            {
                remaining.Remove(kind);
                Console.WriteLine("Continent " + kind + " generated near X=" + px + ", Z=" + pz + " (HighVolcano).");
            }
            world.Terrain.FreeChunk(chunk);
        }
        Check(remaining.Count == 0, "native chunk generation covered all local formations. Missing: " + string.Join(", ", remaining));
    }

    private static bool LikelyHabitat(string kind, int altitude, int heat, int humidity, float shore)
    {
        switch (kind)
        {
            case "StoneCircle": return altitude >= 2 && heat >= 1 && humidity >= 3 && shore >= 24;
            case "Hoodoo": return altitude >= 2 && heat >= 6 && humidity <= 6 && shore >= 18;
            case "Serac": return altitude >= 8 && heat <= 0 && humidity >= 4;
            case "DuneRidge": return altitude >= 1 && heat >= 7 && humidity <= 5 && shore >= 22;
            case "SeaStacks": return shore >= -44 && shore <= 11;
            case "Sinkhole": return altitude >= 8 && heat >= 2 && humidity >= 7 && shore >= 18;
            case "ScreeFan": return altitude >= 8 && shore >= 20;
            case "MeadowHummocks": return altitude >= 3 && heat >= 4 && humidity >= 8 && shore >= 18;
            case "FrostPolygons": return altitude >= 10 && heat <= 0 && shore >= 16;
            default: return false;
        }
    }

    private static void Regional(Func<TerrainGenerationMode, int, SubsystemTerrain> fixture, TerrainGenerationMode mode, int seed)
    {
        var world = fixture(mode, seed);
        ITerrainContentsGenerator generator = mode == TerrainGenerationMode.Better
            ? (ITerrainContentsGenerator)new TerrainContentsGeneratorBetter(world) : new TerrainContentsGenerator24(world);
        var sites = new Dictionary<string, List<Point2>>();
        const int min = -4, max = 26;
        for (int z = min; z < max; z++)
        for (int x = min; x < max; x++)
        {
            object site = chooseGeology.Invoke(null, new object[] { generator, seed, new Point2(x * 192, z * 192) });
            if (site == null) continue;
            string kind = Field<object>(site, "Kind").ToString();
            List<Point2> positions;
            if (!sites.TryGetValue(kind, out positions)) sites[kind] = positions = new List<Point2>();
            positions.Add(new Point2(Field<int>(site, "X"), Field<int>(site, "Z")));
        }
        foreach (string kind in new[] { "ColumnarBasalt", "DryCanyon", "GraniteDomes", "ErodedBadlands", "GlacialCirque",
            "KarstBridges", "DuneLagoons", "BlueHole", "AlpineGlacier" })
        {
            Check(sites.ContainsKey(kind), mode + " native terrain selects " + kind);
            var center = sites[kind].OrderBy(p => p.X * p.X + p.Y * p.Y).First();
            int cx = center.X >> 4, cz = center.Y >> 4;
            int distinctive = kind == "ColumnarBasalt" ? 1006 : kind == "GraniteDomes" ? GraniteBlock.Index :
                kind == "GlacialCirque" || kind == "AlpineGlacier" ? 1011 :
                kind == "KarstBridges" ? 1010 : kind == "DuneLagoons" ? SandBlock.Index :
                kind == "BlueHole" ? 1010 : 1009;
            for (int dx = 0; dx < 2; dx++)
            {
                var chunk = world.Terrain.AllocateChunk(cx + dx, cz);
                generator.GenerateChunkContentsPass1(chunk);
                generator.GenerateChunkContentsPass2(chunk);
                generator.GenerateChunkContentsPass3(chunk);
                int[] original = (int[])chunk.Cells.Clone();
                chunk.IsLoaded = true;
                Generate(generateGeology, chunk, generator, seed, mode);
                Check(chunk.Cells.SequenceEqual(original), kind + " skips stored region slices");
                chunk.IsLoaded = false;
                Generate(generateGeology, chunk, generator, seed, mode);
                int edited = 0, material = 0;
                for (int index = 0; index < original.Length; index++)
                {
                    if (chunk.Cells[index] == original[index]) continue;
                    edited++;
                    if (Terrain.ExtractContents(chunk.Cells[index]) == distinctive) material++;
                }
                Check(chunk.ModificationCounter > 0 && edited > 0 &&
                    (dx == 1 && (kind == "GlacialCirque" || kind == "AlpineGlacier" || kind == "BlueHole") || material > 0),
                    kind + " appears on both sides of a native chunk border");
            }
            Console.WriteLine(mode + " " + kind + ": " + sites[kind].Count + "/" + (max - min) * (max - min) +
                " regions, center " + center + ".");
        }
        Check(sites.ContainsKey("PaintedHills") && sites.ContainsKey("Pinnacles"), mode + " keeps earlier dry-land variants");
    }

    private sealed class Habitat : ITerrainContentsGenerator
    {
        internal readonly int Height;
        private readonly int temperature, humidity, ground, surface, slope;
        private readonly float shore;

        internal Habitat(int height, int temperature, int humidity, float shore, int ground, int surface, int slope = 0)
        {
            Height = height;
            this.temperature = temperature;
            this.humidity = humidity;
            this.shore = shore;
            this.ground = ground;
            this.surface = surface;
            this.slope = slope;
        }

        public int OceanLevel { get { return 64; } }
        public Vector3 FindCoarseSpawnPosition() { return Vector3.Zero; }
        public float CalculateOceanShoreDistance(float x, float z) { return shore; }
        public float CalculateHeight(float x, float z) { return Height - slope * ((int)x & 15); }
        public int CalculateTemperature(float x, float z) { return temperature; }
        public int CalculateHumidity(float x, float z) { return humidity; }
        public float CalculateMountainRangeFactor(float x, float z) { return 0f; }
        public void GenerateChunkContentsPass1(TerrainChunk chunk) { }
        public void GenerateChunkContentsPass2(TerrainChunk chunk) { }
        public void GenerateChunkContentsPass3(TerrainChunk chunk) { }
        public void GenerateChunkContentsPass4(TerrainChunk chunk)
        {
            for (int z = 0; z < 16; z++)
            for (int x = 0; x < 16; x++)
            {
                chunk.SetTemperatureFast(x, z, temperature);
                chunk.SetHumidityFast(x, z, humidity);
                int height = Height - slope * x;
                for (int y = height - 16; y < height; y++) chunk.SetCellValueFast(x, y, z, ground);
                chunk.SetCellValueFast(x, height, z, surface);
                for (int y = height + 1; y <= OceanLevel; y++)
                    chunk.SetCellValueFast(x, y, z, Terrain.MakeBlockValue(WaterBlock.Index, 0, FluidBlock.SetIsTop(0, y == OceanLevel)));
            }
        }
    }
}

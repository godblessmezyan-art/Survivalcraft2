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
        Fallback();
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
                foreach (Point2 step in new[] { new Point2(1, 0), new Point2(0, 1) })
                {
                    string[] adjacent;
                    if (!choices.TryGetValue(pair.Key + step, out adjacent)) continue;
                    Check(!pair.Value.Intersect(adjacent).Any(s => s.Length != 0), "adjacent candidates differ");
                }
            }
            Check(all.Count == 11 && selected > side * side / 2 && selected <= side * side * 2 / 3,
                "all eleven choices with bounded participation for seed " + seed);
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
        int seed = Enumerable.Range(1, 1000).First(value => Draw(value, position).SequenceEqual(new[] { "Serac", "DuneRidge" }));
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

    private static void NativeLandmarks(Func<TerrainGenerationMode, int, SubsystemTerrain> fixture, int seed)
    {
        var mode = TerrainGenerationMode.Continent;
        var world = fixture(mode, seed);
        var generator = new TerrainContentsGenerator24(world);
        var remaining = new HashSet<string> { "StoneCircle", "Hoodoo", "Serac", "DuneRidge", "SeaStacks", "Sinkhole" };
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
            default: return false;
        }
    }

    private static void Regional(Func<TerrainGenerationMode, int, SubsystemTerrain> fixture, TerrainGenerationMode mode, int seed)
    {
        var world = fixture(mode, seed);
        ITerrainContentsGenerator generator = mode == TerrainGenerationMode.Better
            ? (ITerrainContentsGenerator)new TerrainContentsGeneratorBetter(world) : new TerrainContentsGenerator24(world);
        var sites = new Dictionary<string, List<Point2>>();
        for (int z = -4; z < 16; z++)
        for (int x = -4; x < 16; x++)
        {
            object site = chooseGeology.Invoke(null, new object[] { generator, seed, new Point2(x * 192, z * 192) });
            if (site == null) continue;
            string kind = Field<object>(site, "Kind").ToString();
            List<Point2> positions;
            if (!sites.TryGetValue(kind, out positions)) sites[kind] = positions = new List<Point2>();
            positions.Add(new Point2(Field<int>(site, "X"), Field<int>(site, "Z")));
        }
        foreach (string kind in new[] { "ColumnarBasalt", "DryCanyon" })
        {
            Check(sites.ContainsKey(kind), mode + " native terrain selects " + kind);
            var center = sites[kind].OrderBy(p => p.X * p.X + p.Y * p.Y).First();
            int cx = center.X >> 4, cz = center.Y >> 4;
            int distinctive = kind == "ColumnarBasalt" ? 1006 : 1009;
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
                Check(chunk.ModificationCounter > 0 && chunk.Cells.Any(c => Terrain.ExtractContents(c) == distinctive),
                    kind + " appears on both sides of a native chunk border");
            }
            Console.WriteLine(mode + " " + kind + ": " + sites[kind].Count + "/400 regions, center " + center + ".");
        }
        Check(sites.ContainsKey("PaintedHills") && sites.ContainsKey("Pinnacles"), mode + " keeps earlier dry-land variants");
    }

    private sealed class Habitat : ITerrainContentsGenerator
    {
        internal readonly int Height;
        private readonly int temperature, humidity, ground, surface;
        private readonly float shore;

        internal Habitat(int height, int temperature, int humidity, float shore, int ground, int surface)
        {
            Height = height;
            this.temperature = temperature;
            this.humidity = humidity;
            this.shore = shore;
            this.ground = ground;
            this.surface = surface;
        }

        public int OceanLevel { get { return 64; } }
        public Vector3 FindCoarseSpawnPosition() { return Vector3.Zero; }
        public float CalculateOceanShoreDistance(float x, float z) { return shore; }
        public float CalculateHeight(float x, float z) { return Height; }
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
                for (int y = Height - 16; y < Height; y++) chunk.SetCellValueFast(x, y, z, ground);
                chunk.SetCellValueFast(x, Height, z, surface);
                for (int y = Height + 1; y <= OceanLevel; y++)
                    chunk.SetCellValueFast(x, y, z, Terrain.MakeBlockValue(WaterBlock.Index, 0, FluidBlock.SetIsTop(0, y == OceanLevel)));
            }
        }
    }
}

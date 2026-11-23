using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Engine;
using Game;
using Game.NetWork;

public static class VolcanoNativeProbe
{
    private const BindingFlags Static = BindingFlags.NonPublic | BindingFlags.Static;
    private const BindingFlags Field = BindingFlags.NonPublic | BindingFlags.Instance;
    private const string SeedText = "HighVolcano";
    private const string Active = "MagmaCrater";
    private const string Cold = "Crater";
    private const string Lakes = "CraterLakes";
    private static readonly Point2[] Steps = { new Point2(1, 0), new Point2(-1, 0), new Point2(0, 1), new Point2(0, -1) };
    private static MethodInfo choose;
    private static Action<TerrainChunk, ITerrainContentsGenerator, int, TerrainGenerationMode> generate;
    private static int checks;

    public static void Run(Assembly mod, Func<TerrainGenerationMode, int, SubsystemTerrain> fixture)
    {
        choose = mod.GetType("SCIEPort.Features.Geology.GeologySites", true).GetMethod("Choose", Static);
        generate = (Action<TerrainChunk, ITerrainContentsGenerator, int, TerrainGenerationMode>)mod
            .GetType("SCIEPort.Features.Geology.GeologyTerrain", true).GetMethod("Generate", Static)
            .CreateDelegate(typeof(Action<TerrainChunk, ITerrainContentsGenerator, int, TerrainGenerationMode>));
        int seed = WorldSeed(SeedText);
        Survey(fixture, TerrainGenerationMode.Continent, seed);
        Survey(fixture, TerrainGenerationMode.Better, seed);
        Survey(fixture, TerrainGenerationMode.Island, WorldSeed("VolcanoIsland"));
        Console.WriteLine("Volcano fixed target: " + checks + " checks passed.");
    }

    private static int WorldSeed(string seed)
    {
        int value = 0;
        for (int i = 0; i < seed.Length; i++) value += seed[i] * (1 + 29 * i);
        return value;
    }

    private static void Check(bool success, string message)
    {
        checks++;
        if (!success) throw new InvalidOperationException("Volcano: " + message);
    }

    private static T Read<T>(object target, string name)
    {
        return (T)target.GetType().GetField(name, Field).GetValue(target);
    }

    private sealed class Site
    {
        internal readonly string Kind;
        internal readonly int X, Z, Base, Radius, Sea;

        internal Site(object site)
        {
            Kind = Read<object>(site, "Kind").ToString();
            X = Read<int>(site, "X");
            Z = Read<int>(site, "Z");
            Base = Read<int>(site, "Base");
            Radius = Read<int>(site, "Radius");
            Sea = Read<int>(site, "Sea");
        }

        internal Point2 Crater { get { return new Point2(X, Z); } }
        internal int Floor { get { return Base + (Kind == Lakes ? 4 : -4); } }
        public override string ToString() { return Kind + " (" + X + "," + Z + ") Y=" + Floor; }
    }

    private static ITerrainContentsGenerator Generator(SubsystemTerrain terrain, TerrainGenerationMode mode)
    {
        if (mode == TerrainGenerationMode.Island)
            terrain.Project.FindSubsystem<SubsystemGameInfo>(true).WorldSettings.IslandSize = new Vector2(3072f, 3072f);
        return mode == TerrainGenerationMode.Better
            ? (ITerrainContentsGenerator)new TerrainContentsGeneratorBetter(terrain)
            : new TerrainContentsGenerator24(terrain);
    }

    private static void Survey(Func<TerrainGenerationMode, int, SubsystemTerrain> fixture, TerrainGenerationMode mode, int seed)
    {
        var terrain = fixture(mode, seed);
        ITerrainContentsGenerator generator = Generator(terrain, mode);
        var sites = new List<Site>();
        for (int z = -4; z < 16; z++)
        for (int x = -4; x < 16; x++)
        {
            object raw = choose.Invoke(null, new object[] { generator, seed, new Point2(x * 192, z * 192) });
            if (raw == null) continue;
            var site = new Site(raw);
            if (site.Kind != Active && site.Kind != Cold && site.Kind != Lakes) continue;
            Check(site.Base >= generator.OceanLevel + 25 && site.Floor >= generator.OceanLevel + 21,
                "selected site's center and crater floor are inland uplands");
            Check(generator.CalculateHeight(site.X, site.Z) >= generator.OceanLevel + 13.5f, "qualifying natural height");
            Check((site.X & 15) == 8 && (site.Z & 15) == 8, "central bowl stays inside its chunk, including negatives");
            sites.Add(site);
        }
        foreach (var pair in sites.GroupBy(s => s.Kind))
            Console.WriteLine(mode + " " + pair.Key + ": " + pair.Count() + " / 400 regions");
        foreach (string kind in new[] { Active, Cold, Lakes })
        {
            Site[] sample = sites.Where(s => s.Kind == kind).OrderBy(s => s.X * s.X + s.Z * s.Z).Take(kind == Active ? 3 : 2).ToArray();
            Check(sample.Length > 0 || (kind == Lakes && mode != TerrainGenerationMode.Continent), mode + " sampled " + kind);
            if (sample.Length == 0) continue;
            Console.WriteLine(mode + " first " + sample[0] + " for WorldSeed " + seed);
            foreach (Site site in sample)
            {
                var world = fixture(mode, seed);
                ITerrainContentsGenerator contents = Generator(world, mode);
                var chunks = Build(world.Terrain, contents, site, seed, mode, false);
                Ring(world.Terrain, site);
                if (site.Kind != Active) continue;
                Lava(world.Terrain, site);
                if (site != sample[0]) continue;
                var reordered = fixture(mode, seed);
                var reverse = Build(reordered.Terrain, Generator(reordered, mode), site, seed, mode, true);
                foreach (var chunk in chunks)
                    Check(chunk.Cells.SequenceEqual(reverse.Single(c => c.Coords == chunk.Coords).Cells), "generation order preserves every cell");
                RejectBasin(fixture, mode, seed, site);
            }
        }
    }

    private static List<TerrainChunk> Build(Terrain terrain, ITerrainContentsGenerator generator, Site site,
        int seed, TerrainGenerationMode mode, bool reverse)
    {
        var chunks = new List<TerrainChunk>();
        int radius = site.Radius + 8;
        for (int cz = (site.Z - radius) >> 4; cz <= (site.Z + radius) >> 4; cz++)
        for (int cx = (site.X - radius) >> 4; cx <= (site.X + radius) >> 4; cx++)
            chunks.Add(terrain.AllocateChunk(cx, cz));
        foreach (var chunk in chunks) NativeTerrain(generator, chunk);
        // Compare only the order of the mod's generation pass. Native extras can read
        // surrounding terrain, so their order must be identical in both fixtures.
        IEnumerable<TerrainChunk> planned = reverse ? chunks.AsEnumerable().Reverse() : chunks;
        foreach (var chunk in planned)
            generate(chunk, generator, seed, mode);
        foreach (var chunk in chunks)
            generator.GenerateChunkContentsPass4(chunk);
        return chunks;
    }

    private static void NativeTerrain(ITerrainContentsGenerator generator, TerrainChunk chunk)
    {
        generator.GenerateChunkContentsPass1(chunk);
        generator.GenerateChunkContentsPass2(chunk);
        generator.GenerateChunkContentsPass3(chunk);
    }

    private static int Ground(Terrain terrain, Point2 point)
    {
        var chunk = terrain.GetChunkAtCell(point.X, point.Y);
        int top = chunk.CalculateTopmostCellHeight(point.X & 15, point.Y & 15);
        while (top > 0)
        {
            int contents = terrain.GetCellContents(point.X, top, point.Y);
            Block block = BlocksManager.Blocks[contents];
            if (block is FluidBlock || (block.IsCollidable && !(block is WoodBlock) && !(block is LeavesBlock) &&
                !(block is LeavesBlock24) && contents != SnowBlock.Index))
                return top;
            top--;
        }
        return 0;
    }

    private static void Ring(Terrain terrain, Site site)
    {
        // Any low breach would connect the open exterior to the crater at this elevation.
        // Cardinal angles cover the old entrance and rear tunnel, with all other directions sampled too.
        double radius = site.Radius * .57;
        for (int bearing = 0; bearing < 360; bearing++)
        {
            double angle = bearing * Math.PI / 180;
            var point = new Point2(site.X + (int)Math.Round(Math.Cos(angle) * radius),
                site.Z + (int)Math.Round(Math.Sin(angle) * radius));
            int height = Ground(terrain, point);
            Check(height >= site.Floor + 18, "unbroken crater rim at " + point + " " + site + " height " + height);
            for (int y = height - 3; y <= height; y++)
                Check(BlocksManager.Blocks[terrain.GetCellContents(point.X, y, point.Y)].IsCollidable,
                    "solid rock under crest at " + point);
            for (int y = site.Floor + 3; y <= site.Floor + 8; y++)
                Check(BlocksManager.Blocks[terrain.GetCellContents(point.X, y, point.Y)].IsCollidable,
                    "no old radial passage under the crest at " + point);
        }
    }

    private static void Lava(Terrain terrain, Site site)
    {
        int visible = 0, level = site.Floor - 2;
        for (int dz = -7; dz <= 7; dz++)
        for (int dx = -7; dx <= 7; dx++)
        {
            int x = site.X + dx, z = site.Z + dz;
            int value = terrain.GetCellValue(x, level, z);
            if (dx * dx + dz * dz > 41)
            {
                Check(Terrain.ExtractContents(value) != MagmaBlock.Index, "magma does not extend beyond the pool");
                continue;
            }
            Check(Terrain.ExtractContents(value) == MagmaBlock.Index &&
                FluidBlock.GetIsTop(Terrain.ExtractData(value)) &&
                FluidBlock.GetLevel(Terrain.ExtractData(value)) == 0, "native source visible at the planned lava level " + site);
            Check(terrain.GetCellContents(x, level + 1, z) == 0, "open air over the lava lake");
            foreach (var step in Steps)
            {
                int neighbor = terrain.GetCellContents(x + step.X, level, z + step.Y);
                Check(neighbor == MagmaBlock.Index || BlocksManager.Blocks[neighbor].IsFluidBlocker, "sealed pool walls");
            }
            int below = level - 1;
            while (terrain.GetCellContents(x, below, z) == MagmaBlock.Index) below--;
            Check(level - below <= 3 && BlocksManager.Blocks[terrain.GetCellContents(x, below, z)].IsFluidBlocker, "solid basin bed");
            visible++;
        }
        Check(visible == 137, "visible lava surface size, got " + visible);
    }

    private static void RejectBasin(Func<TerrainGenerationMode, int, SubsystemTerrain> fixture, TerrainGenerationMode mode,
        int seed, Site site)
    {
        var world = fixture(mode, seed);
        var generator = Generator(world, mode);
        var chunk = world.Terrain.AllocateChunk(site.X >> 4, site.Z >> 4);
        NativeTerrain(generator, chunk);
        int floor = site.Floor;
        int foreign = Terrain.MakeBlockValue(1022);
        chunk.SetCellValueFast(15, floor, 8, foreign); // Last-side obstruction near the liquid rim.
        var initial = (int[])chunk.Cells.Clone();
        chunk.IsLoaded = true;
        generate(chunk, generator, seed, mode);
        Check(chunk.ModificationCounter == 0 && chunk.Cells.SequenceEqual(initial), "saved terrain untouched");
        chunk.IsLoaded = false;
        generate(chunk, generator, seed, mode);
        for (int x = 1; x <= 15; x++)
        for (int z = 1; z <= 15; z++)
        for (int y = floor - 5; y <= floor + 1; y++)
        {
            int index = TerrainChunk.CalculateCellIndex(x, y, z);
            Check(chunk.Cells[index] == initial[index], "foreign rim cell cancels the complete basin write");
        }
    }
}

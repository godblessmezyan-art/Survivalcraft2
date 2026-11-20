using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Engine;
using Game;
using Game.NetWork;
using GameEntitySystem;
using TemplatesDatabase;

public static class PyramidNativeProbe
{
    public delegate SubsystemTerrain Fixture(GameDatabase db, TerrainGenerationMode mode, out Project project,
        out SubsystemBlockBehavior palace, int seed);

    private const BindingFlags Instance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private const BindingFlags Static = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    private const int MechanismId = 1023, Full = (1 << 25) - 1;
    private const string SeedText = "SunPyramid";
    private static readonly Point3[] SealPoints =
    {
        new Point3(11, 2, 25), new Point3(67, 2, 37), new Point3(39, 19, 25)
    };
    private static readonly Point2[] Directions =
    {
        new Point2(1, 0), new Point2(-1, 0), new Point2(0, 1), new Point2(0, -1)
    };
    private static Type siteType, layoutType, stateType, runtimeType, packetType;
    private static int checks;

    public static void Run(Assembly mod, GameDatabase database, Fixture fixture)
    {
        CommonLib.WorkType = WorkType.Local;
        siteType = TypeOf(mod, "PyramidSite");
        layoutType = TypeOf(mod, "PyramidLayout");
        stateType = TypeOf(mod, "PyramidState");
        runtimeType = TypeOf(mod, "SubsystemPyramid");
        packetType = TypeOf(mod, "PyramidPackage");
        Validation(mod, database);
        StateCases(mod);
        var site = Create(siteType, new Point2(0, 0), 72, 0, 953);
        LayoutRoutes(site);
        int seed = SeedText.Select((c, i) => c * (1 + 29 * i)).Sum();
        GenerateWorlds(database, fixture, seed);
        Reward(database, fixture, seed, WorkType.Local);
        Reward(database, fixture, seed, WorkType.Server);
        Console.WriteLine("Pyramid fixed target: " + checks + " checks passed.");
    }

    private static Type TypeOf(Assembly mod, string name) { return mod.GetType("SCIEPort.Features.Pyramid." + name, true); }
    private static object Create(Type type, params object[] args) { return Activator.CreateInstance(type, Instance, null, args, null); }
    private static T Field<T>(object target, string name) { return (T)target.GetType().GetField(name, Instance).GetValue(target); }
    private static T Property<T>(object target, string name) { return (T)target.GetType().GetProperty(name, Instance).GetValue(target); }
    private static object Call(object target, string name, params object[] args)
    {
        return target.GetType().GetMethod(name, Instance).Invoke(target, args);
    }
    private static int Value(int data) { return Terrain.MakeBlockValue(MechanismId, 0, data); }
    private static void Check(bool condition, string reason)
    {
        checks++;
        if (!condition) throw new InvalidOperationException("Pyramid: " + reason);
    }

    private static void Validation(Assembly mod, GameDatabase database)
    {
        var block = BlocksManager.Blocks[MechanismId];
        Check(block.GetType() == TypeOf(mod, "PyramidMechanismBlock") && !block.GetCreativeValues().Any() &&
            block.GetCategory(Value(0)) == "SCIEPort.Structures", "registered world-only mechanism");
        var unique = mod.GetTypes().Concat(typeof(Block).Assembly.GetTypes()).Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(Block)))
            .Select(t => t.GetField("Index", Static | BindingFlags.DeclaredOnly))
            .Count(f => f != null && f.IsLiteral && (int)f.GetRawConstantValue() == MechanismId);
        Check(unique == 1, "contents ID is unique in both native and mod assemblies");
        for (int data = 0; data <= 12; data++)
        {
            int value = Terrain.ReplaceLight(Value(data), 15);
            Check(Terrain.ExtractContents(value) == MechanismId && Terrain.ExtractData(value) == data &&
                block.GetFaceTextureSlot(0, value) == data && block.IsInteractive(null, value) == (data < 3),
                "native 10-bit contents mask and intended DATA " + data);
        }
        foreach (string project in new[] { "Project", "GameProject" })
        {
            var values = new ValuesDictionary();
            values.PopulateFromDatabaseObject(database.Database.FindDatabaseObject(project, database.ProjectTemplateType, true));
            var runtime = values.GetValue<ValuesDictionary>("SCIEPortPyramid");
            Check(runtime.GetValue<string>("Class") == runtimeType.FullName &&
                values.ContainsKey("BlockEntities") && values.ContainsKey("Terrain"), project + " real XDB member");
        }
        var packet = (IPackage)Create(packetType);
        var allIds = typeof(IPackage).Assembly.GetTypes().Concat(mod.GetTypes())
            .Where(t => !t.IsAbstract && !t.IsInterface && typeof(IPackage).IsAssignableFrom(t))
            .Select(t => ((IPackage)FormatterServices.GetUninitializedObject(t)).ID);
        Check(packet.ID == 234 && allIds.Count(id => id == packet.ID) == 1, "unique packet ID");
        var expected = new Point3(-113, 77, 175);
        TidePalaceProbe.Put(packet, "Point", expected);
        using (var writer = new PackageStreamWriter())
        {
            packet.WriteData(writer);
            Check(writer.BaseStream.Length == 12, "fixed native point payload");
            var copy = (IPackage)Create(packetType);
            using (var reader = new PackageStreamReader(writer.Data())) copy.ReadData(reader);
            Check(Field<Point3>(copy, "Point") == expected, "native signed coordinates round trip");
        }
    }

    private static void StateCases(Assembly mod)
    {
        var loot = TypeOf(mod, "PyramidLoot");
        var draw = (Func<Game.Random, int>)loot.GetMethod("Draw", Static).CreateDelegate(typeof(Func<Game.Random, int>));
        var eligible = (Func<int, bool>)loot.GetMethod("IsEligible", Static).CreateDelegate(typeof(Func<int, bool>));
        var valid = (Func<int, bool>)loot.GetMethod("IsRegistered", Static).CreateDelegate(typeof(Func<int, bool>));
        var pool = Enumerable.Range(0, BlocksManager.Blocks.Length)
            .Where(id => id != 951 && BlocksManager.Blocks[id].DefaultCategory == "SCIEPort.Artifacts").ToArray();
        Check(pool.SequenceEqual(Enumerable.Range(0, BlocksManager.Blocks.Length).Where(eligible)) &&
            pool.Length == 9 && !eligible(951), "pool is every registered artifact except Poseidon");
        Check(pool.All(valid), "all rewards validate the actual registered block and single-slot support");
        var random = new Game.Random(43512);
        var counts = new Dictionary<int, int>();
        for (int i = 0; i < 9000; i++)
        {
            int id = draw(random);
            Check(eligible(id), "draw excludes Poseidon and non-artifact blocks");
            counts[id] = counts.ContainsKey(id) ? counts[id] + 1 : 1;
        }
        Check(counts.Count == 9 && counts.Values.All(count => count >= 850 && count <= 1150), "balanced fixed-seed sample");
        for (int order = 0; order < 6; order++)
        {
            var site = Create(siteType, new Point2(-80, -80), 72, order, 952);
            var state = Create(stateType, site, Full - 1, 0, false);
            int[] pattern = Enumerable.Range(0, 3).Select(step => (int)Call(site, "SealAt", step)).ToArray();
            Check(pattern.Distinct().Count() == 3 && !(bool)Call(state, "Press", pattern[0]), "incomplete site cannot progress");
            TidePalaceProbe.Put(state, "Slices", Full);
            Check((bool)Call(state, "Press", pattern[0]) && (bool)Call(state, "Press", pattern[2]) &&
                Property<int>(state, "Progress") == 0, "wrong next seal resets the sequence");
            foreach (int seal in pattern) Check((bool)Call(state, "Press", seal), "correct seal advances");
            Check(Property<bool>(state, "Solved") && !(bool)Call(state, "Press", pattern[0]), "completion latches");
            Call(state, "MarkStocked");
            Check(Property<bool>(state, "Stocked"), "once-only stocking ledger");
        }
    }

    private static void LayoutRoutes(object site)
    {
        var slices = new Func<int, int, int, int>[5, 5];
        for (int z = 0; z < 5; z++)
        for (int x = 0; x < 5; x++)
        {
            var layout = Create(layoutType, site, new Point2(x * 16, z * 16));
            slices[x, z] = (Func<int, int, int, int>)layoutType.GetMethod("At", Instance)
                .CreateDelegate(typeof(Func<int, int, int, int>), layout);
        }
        Func<Point3, int> at = point =>
        {
            if (point.X < 0 || point.X >= 80 || point.Z < 0 || point.Z >= 80 || point.Y < -14) return SandstoneBlock.Index;
            if (point.Y > 36) return 0;
            int value = slices[point.X >> 4, point.Z >> 4](point.X & 15, point.Y, point.Z & 15);
            return value == -2 || (value == -1 && point.Y < 0) ? SandstoneBlock.Index : Math.Max(0, value);
        };
        Check(at(new Point3(39, 36, 39)) == Value(12), "36-block sandstone pyramid summit");
        for (int index = 0; index < 3; index++)
            Check(at(SealPoints[index]) == Value(index) && at(new Point3(36 + index * 3, 6, 43)) == Value(6 + index),
                "matching seal and mural symbol");

        var entry = new Point3(40, 1, 8);
        Check(Distance(at, entry, new Point3(40, 1, 21), false, false, 36) >= 1, "front gate connects to the main hall");
        var bridge = new Point3(26, 1, 26);
        var west = new Point3(12, 1, 25);
        Check(Distance(at, bridge, west, false, false, 8) < 0, "west seal cannot be reached by just walking across gaps");
        Check(Distance(at, bridge, west, true, false, 8) >= 4, "west seal uses short jumps across bridge pillars");
        Check(Distance(at, new Point3(20, -4, 29), bridge, false, false, 8) >= 0, "bridge pit has walkable recovery steps");
        Check(Distance(at, entry, new Point3(67, 1, 35), false, false, 9) >= 55, "east labyrinth requires navigating alternating corridors");
        Check(Distance(at, entry, new Point3(41, 18, 26), false, false, 24) >= 30, "two flights reach the upper seal");
        var altar = new Point3(40, -9, 64);
        Check(Distance(at, entry, altar, false, false, 11) < 0, "closed gate blocks the underground chamber");
        Check(Distance(at, entry, altar, false, true, 11) >= 40, "opening the gate exposes a continuous staircase to the altar");
    }

    private static int Distance(Func<Point3, int> at, Point3 from, Point3 to, bool jump, bool open, int ceiling)
    {
        Func<int, bool> isAir = value => value == 0 || (open && value == Value(9));
        Func<Point3, bool> hasRoom = p => isAir(at(p)) && isAir(at(p + new Point3(0, 1, 0)));
        Func<Point3, bool> stand = p => p.Y >= -13 && p.Y <= ceiling &&
            !isAir(at(p + new Point3(0, -1, 0))) && hasRoom(p);
        var seen = new Dictionary<Point3, int> { { from, 0 } };
        var queue = new Queue<Point3>();
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            Point3 point = queue.Dequeue();
            if (point == to) return seen[point];
            foreach (var step in Directions)
            {
                var horizontal = new Point3(step.X, 0, step.Y);
                for (int y = -1; y <= 1; y++)
                {
                    var next = point + horizontal + new Point3(0, y, 0);
                    if (stand(next) && !seen.ContainsKey(next))
                    {
                        seen.Add(next, seen[point] + 1);
                        queue.Enqueue(next);
                    }
                }
                Point3 landing = point + horizontal + horizontal;
                if (jump && hasRoom(point + horizontal) && stand(landing) && !seen.ContainsKey(landing))
                {
                    seen.Add(landing, seen[point] + 2);
                    queue.Enqueue(landing);
                }
            }
        }
        return -1;
    }

    private static SubsystemBlockBehavior AddRuntime(SubsystemTerrain terrain)
    {
        var runtime = (SubsystemBlockBehavior)Create(runtimeType);
        TidePalaceProbe.Add(terrain.Project, runtime);
        runtime.Load(new ValuesDictionary());
        return runtime;
    }

    private static ITerrainContentsGenerator Generator(SubsystemTerrain terrain, TerrainGenerationMode mode)
    {
        if (mode == TerrainGenerationMode.Island)
            terrain.Project.FindSubsystem<SubsystemGameInfo>(true).WorldSettings.IslandSize = new Vector2(3072f, 3072f);
        return mode == TerrainGenerationMode.Better
            ? (ITerrainContentsGenerator)new TerrainContentsGeneratorBetter(terrain) : new TerrainContentsGenerator24(terrain);
    }

    private static void GenerateWorlds(GameDatabase database, Fixture fixture, int seed)
    {
        foreach (TerrainGenerationMode mode in new[] { TerrainGenerationMode.Continent, TerrainGenerationMode.Island, TerrainGenerationMode.Better })
        {
            Project project;
            SubsystemBlockBehavior palace;
            var terrain = fixture(database, mode, out project, out palace, seed);
            var runtime = AddRuntime(terrain);
            var generator = Generator(terrain, mode);
            var wrapper = (ITerrainContentsGenerator)Create(runtimeType.Assembly.GetType("SCIEPort.Features.Plants.PlantTerrainGenerator", true),
                generator, project.FindSubsystem<SubsystemGameInfo>(true));
            var choose = siteType.GetMethod("Choose", Static);
            var sites = new List<object>();
            for (int z = -4; z < 12; z++)
            for (int x = -4; x < 12; x++)
            {
                object result = choose.Invoke(null, new object[] { generator, seed, new Point2(x, z) });
                if (result != null) sites.Add(result);
            }
            if (sites.Count == 0)
            {
                int climate = 0, ground = 0, free = 0;
                var checkGround = siteType.GetMethod("TryBase", Static);
                var checkGeology = siteType.GetMethod("OverlapsGeology", Static);
                for (int z = -4; z < 12; z++)
                for (int x = -4; x < 12; x++)
                for (int candidate = 0; candidate < 9; candidate++)
                {
                    int px = x * 384 + 64 + (candidate % 3) * 80, pz = z * 384 + 64 + (candidate / 3) * 80;
                    if (generator.CalculateTemperature(px + 40, pz + 40) >= 8 &&
                        generator.CalculateHumidity(px + 40, pz + 40) <= 6) climate++;
                    if (!(bool)checkGround.Invoke(null, new object[] { generator, px, pz, 0 })) continue;
                    ground++;
                    if (!(bool)checkGeology.Invoke(null, new object[] { generator, seed, new Point2(px, pz) })) free++;
                }
                Check(false, mode + " has no site. Climate " + climate + ", ground " + ground + ", free " + free);
            }
            object site = sites.OrderBy(s => Field<Point2>(s, "Origin").X * Field<Point2>(s, "Origin").X +
                Field<Point2>(s, "Origin").Y * Field<Point2>(s, "Origin").Y).First();
            Point2 origin = Field<Point2>(site, "Origin");
            int b = Field<int>(site, "BaseY");
            Console.WriteLine(mode + " Pyramid " + sites.Count + "/256, " + SeedText + " origin " + origin + ", deck " + b);
            var chunks = new List<TerrainChunk>();
            for (int z = 0; z < 5; z++)
            for (int x = 0; x < 5; x++)
            {
                var chunk = terrain.Terrain.AllocateChunk((origin.X >> 4) + x, (origin.Y >> 4) + z);
                generator.GenerateChunkContentsPass1(chunk);
                generator.GenerateChunkContentsPass2(chunk);
                generator.GenerateChunkContentsPass3(chunk);
                chunks.Add(chunk);
            }
            CommonLib.WorkType = WorkType.Client;
            Check(!(bool)Call(runtime, "Generate", chunks[0], generator, seed, mode), "pure clients cannot generate");
            CommonLib.WorkType = WorkType.Local;
            chunks[0].IsLoaded = true;
            Check(!(bool)Call(runtime, "Generate", chunks[0], generator, seed, mode) &&
                chunks[0].ModificationCounter == 0, "already loaded chunk untouched");
            chunks[0].IsLoaded = false;
            var stored = (HashSet<Point2>)TidePalaceProbeStorage(terrain, "Existing");
            stored.Add(chunks[24].Coords);
            var blocker = (SubsystemBlockBehavior)Create(runtimeType);
            TidePalaceProbe.Add(project, blocker);
            blocker.Load(new ValuesDictionary());
            Check(!(bool)Call(blocker, "Generate", chunks[0], generator, seed, mode), "saved neighbor rejects the footprint");
            stored.Clear();
            foreach (var chunk in chunks.AsEnumerable().Reverse()) wrapper.GenerateChunkContentsPass4(chunk);
            var records = Field<IDictionary>(runtime, "sites");
            int written = records.Count == 1 ? Field<int>(records.Values.Cast<object>().Single(), "Slices") : 0;
            Check(written == Full, mode + " wrote all 25 native slices through the real entry point. " +
                (written == Full ? string.Empty : RejectedSlices(site, b, written, chunks)));
            Check(terrain.Terrain.GetCellValue(origin.X + 39, b + 36, origin.Y + 39) == Value(12), "crown written");
            var display = (Point3)Call(site, "World", new Point3(40, -9, 65));
            Check(terrain.Terrain.GetCellValue(display.X, display.Y, display.Z) == Value(10), "deep altar still sealed after generation");
            Check(project.FindSubsystem<SubsystemBlockEntities>(true).GetBlockEntity(display.X, display.Y, display.Z) == null,
                "generation alone cannot create a reward inventory");
        }
    }

    private static string RejectedSlices(object site, int baseY, int written, IList<TerrainChunk> chunks)
    {
        var natural = (Func<int, bool>)TypeOf(runtimeType.Assembly, "PyramidTerrain").GetMethod("Natural", Static)
            .CreateDelegate(typeof(Func<int, bool>));
        var rejected = new List<string>();
        for (int i = 0; i < chunks.Count; i++)
        {
            if ((written & (1 << i)) != 0) continue;
            TerrainChunk chunk = chunks[i];
            object layout = Create(layoutType, site, chunk.Origin);
            var at = (Func<int, int, int, int>)layoutType.GetMethod("At", Instance)
                .CreateDelegate(typeof(Func<int, int, int, int>), layout);
            var types = new HashSet<string>();
            for (int x = 0; x < 16; x++)
            for (int z = 0; z < 16; z++)
            for (int y = -14; y <= 36; y++)
            {
                if (at(x, y, z) < 0) continue;
                int old = Terrain.ExtractContents(chunk.GetCellValueFast(x, baseY + y, z));
                if (!natural(old)) types.Add(old + "/" + BlocksManager.Blocks[old].GetType().Name);
            }
            rejected.Add(chunk.Origin + "=" + string.Join(",", types));
        }
        return string.Join(" ", rejected);
    }

    private static object TidePalaceProbeStorage(SubsystemTerrain terrain, string name)
    {
        return terrain.TerrainSerializer.m_storage.GetType().GetField(name, Instance).GetValue(terrain.TerrainSerializer.m_storage);
    }

    private static void Reward(GameDatabase database, Fixture fixture, int seed, WorkType mode)
    {
        CommonLib.WorkType = mode;
        Project project;
        SubsystemBlockBehavior palace;
        var terrain = fixture(database, TerrainGenerationMode.Continent, out project, out palace, seed);
        var runtime = AddRuntime(terrain);
        terrain.m_subsystemBlockBehaviors = new SubsystemBlockBehaviors
        {
            m_blockBehaviorsByContents = Enumerable.Range(0, BlocksManager.Blocks.Length).Select(n => new SubsystemBlockBehavior[0]).ToArray()
        };
        var updater = (TerrainUpdater)FormatterServices.GetUninitializedObject(typeof(TerrainUpdater));
        TidePalaceProbe.Put(updater, "m_terrain", terrain.Terrain);
        TidePalaceProbe.Put(terrain, "<TerrainUpdater>k__BackingField", updater);
        var site = Create(siteType, new Point2(0, 0), 72, 0, mode == WorkType.Local ? 948 : 971);
        var state = Create(stateType, site, Full, 0, false);
        Field<IDictionary>(runtime, "sites").Add(new Point2(0, 0), state);
        var entities = project.FindSubsystem<SubsystemBlockEntities>(true);
        var display = (Point3)Call(site, "World", new Point3(40, -9, 65));
        Write(terrain.Terrain, display, Value(10));
        var door = (Point3)Call(site, "World", new Point3(38, 1, 45));
        Write(terrain.Terrain, door, Value(9));
        var player = TidePalaceProbe.MakePlayer(project);
        var miner = new ComponentMiner { m_entity = player.Entity, ComponentPlayer = player };
        var networkProject = (ProjectNet)FormatterServices.GetUninitializedObject(typeof(ProjectNet));
        TidePalaceProbe.Put(networkProject, "m_subsystems", project.m_subsystems);
        var players = project.FindSubsystem<SubsystemPlayers>(true);
        var client = new Client(player.PlayerGuid, null) { State = ClientState.ProjectLoaded };
        TidePalaceProbe.Put(client, "<SubsystemPlayers>k__BackingField", players);
        TidePalaceProbe.Put(client, "<ID>k__BackingField", (byte)7);
        CommonLib.Net.Clients[7] = client;
        players.m_playersData.Add(player.PlayerData);
        var seals = SealPoints.Select(p => (Point3)Call(site, "World", p)).ToArray();
        for (int i = 0; i < seals.Length; i++) Write(terrain.Terrain, seals[i], Value(i));
        Action<int> click = index =>
        {
            Point3 p = seals[index];
            player.ComponentCreatureModel.m_eyePosition = new Vector3(p.X + .5f, p.Y + .5f, p.Z - 1.5f);
            if (mode == WorkType.Local)
            {
                var hit = new TerrainRaycastResult { CellFace = new CellFace(p.X, p.Y, p.Z, 2), Value = Value(index) };
                Check(runtime.OnInteract(hit, miner), "native local interaction accepted");
            }
            else
            {
                Check(client.PlayerData == player.PlayerData, "native client identity resolves current player");
                var packet = (IPackage)Create(packetType);
                TidePalaceProbe.Put(packet, "Point", p);
                packet.From = client;
                packet.Handle(networkProject, CommonLib.Net, true);
            }
            ((IUpdateable)runtime).Update(0f);
        };
        var wall = seals[0] + new Point3(0, 0, -1);
        Write(terrain.Terrain, wall, GraniteBlock.Index);
        click(0);
        Check(Property<int>(state, "Progress") == 0, "through-wall input rejected");
        Write(terrain.Terrain, wall, 0);
        click(0);
        click(2);
        Check(Property<int>(state, "Progress") == 0 && entities.GetBlockEntity(display.X, display.Y, display.Z) == null,
            "wrong seal resets, core still empty");
        click(0);
        click(1);
        Check(terrain.Terrain.GetCellValue(door.X, door.Y, door.Z) == Value(9) &&
            entities.GetBlockEntity(display.X, display.Y, display.Z) == null, "two seals leave the gate and empty core locked");
        click(2);
        var entity = entities.GetBlockEntity(display.X, display.Y, display.Z);
        Check(entity != null && terrain.Terrain.GetCellValue(door.X, door.Y, door.Z) == 0 && Property<bool>(state, "Stocked"),
            "third seal opens the gate and creates a real native display");
        var chest = entity.Entity.FindComponent<ComponentChest>(true);
        Check(chest.SlotsCount == 1 && chest.GetSlotCount(0) == 1 &&
            chest.GetSlotValue(0) == Terrain.MakeBlockValue(Field<int>(site, "RewardId")), "one real fixed artifact");
        chest.RemoveSlotItems(0, 1);
        Call(runtime, "Synchronize", state);
        Check(chest.GetSlotCount(0) == 0, "empty chest cannot restock");
        project.RemoveEntity(entity.Entity, true);
        Write(terrain.Terrain, display, 0);
        var save = new ValuesDictionary();
        runtime.Save(save);
        var reloaded = (SubsystemBlockBehavior)Create(runtimeType);
        TidePalaceProbe.Add(project, reloaded);
        reloaded.Load(save);
        var restored = Field<IDictionary>(reloaded, "sites")[new Point2(0, 0)];
        Call(reloaded, "Synchronize", restored);
        Check(Property<bool>(restored, "Stocked") && Field<int>(Field<object>(restored, "Site"), "RewardId") == Field<int>(site, "RewardId") &&
            entities.GetBlockEntity(display.X, display.Y, display.Z) == null, "restored claim and stable prize survive removal and reload");
    }

    private static void Write(Terrain terrain, Point3 point, int value)
    {
        var chunk = terrain.GetChunkAtCell(point.X, point.Z) ?? terrain.AllocateChunk(point.X >> 4, point.Z >> 4);
        chunk.State = TerrainChunkState.Valid;
        terrain.SetCellValueFast(point.X, point.Y, point.Z, value);
    }
}

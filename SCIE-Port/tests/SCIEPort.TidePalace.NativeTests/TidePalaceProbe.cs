using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml.Linq;
using Engine;
using Game;
using Game.NetWork;
using GameEntitySystem;
using TemplatesDatabase;

public static class TidePalaceProbe
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const int Seed = 602131;
    private const int PalaceId = 1022;
    private static int checks;
    private static Type siteType, layoutType, stateType, behaviorType;
    private static readonly Point3[] Sides =
    {
        new Point3(1, 0, 0), new Point3(-1, 0, 0), new Point3(0, 0, 1),
        new Point3(0, 0, -1), new Point3(0, -1, 0), new Point3(0, 1, 0)
    };

    private static void Check(bool condition, string name)
    {
        checks++;
        if (!condition) throw new InvalidOperationException("Tide Palace: " + name);
    }

    private static FieldInfo Field(Type type, string name)
    {
        while (type != null)
        {
            var field = type.GetField(name, Instance);
            if (field != null) return field;
            type = type.BaseType;
        }
        throw new MissingFieldException(name);
    }

    private static void Put(object target, string name, object value) { Field(target.GetType(), name).SetValue(target, value); }
    private static T Get<T>(object target, string name) { return (T)Field(target.GetType(), name).GetValue(target); }
    private static T Property<T>(object target, string name) { return (T)target.GetType().GetProperty(name, Instance).GetValue(target); }
    private static object Call(object target, string name, params object[] args)
    {
        return target.GetType().GetMethod(name, Instance).Invoke(target, args);
    }
    private static object Make(Type type, params object[] args)
    {
        return Activator.CreateInstance(type, Instance, null, args, null);
    }
    private static void Add(Project project, Subsystem subsystem)
    {
        Put(subsystem, "m_project", project);
        project.m_subsystems.Add(subsystem);
    }

    public static void Run(Assembly mod, XElement xml, string nativeBlocks, string geologyBlocks, string palaceBlocks)
    {
        siteType = mod.GetType("SCIEPort.Features.TidePalace.PalaceSite", true);
        layoutType = mod.GetType("SCIEPort.Features.TidePalace.PalaceLayout", true);
        stateType = mod.GetType("SCIEPort.Features.TidePalace.TidePalaceState", true);
        behaviorType = mod.GetType("SCIEPort.Features.TidePalace.SubsystemTidePalace", true);
        CommonLib.WorkType = WorkType.Local;
        ModsManager.ModHooks = new Dictionary<string, ModsManager.ModHook>();
        CommonLib.Net = (NetNode)FormatterServices.GetUninitializedObject(typeof(NetNode));
        Put(CommonLib.Net, "PendingPackages", new List<IPackage>());
        CommonLib.Net.Clients = new Dictionary<int, Client>();
        RegisterBlocks(mod, nativeBlocks, geologyBlocks, palaceBlocks);
        CheckRegistration(mod, xml);
        var site = Make(siteType, new Point2(0, 0), 32, 17);
        Layout(site);
        Puzzle();

        var game = new GameDatabase(XmlDatabaseSerializer.LoadDatabase(xml));
        foreach (TerrainGenerationMode mode in new[] { TerrainGenerationMode.Continent, TerrainGenerationMode.Island, TerrainGenerationMode.Better })
            Generate(game, mode);
        Reward(game, WorkType.Local);
        Reward(game, WorkType.Server);
        Console.WriteLine("Tide Palace fixed target: " + checks + " checks passed.");
    }

    private static void RegisterBlocks(Assembly mod, string native, string geology, string palace)
    {
        for (int i = 0; i < BlocksManager.Blocks.Length; i++) BlocksManager.Blocks[i] = new AirBlock();
        foreach (var assembly in new[] { typeof(Block).Assembly, mod })
        foreach (var type in assembly.GetTypes())
        {
            if (type.IsAbstract || !type.IsSubclassOf(typeof(Block))) continue;
            var index = type.GetField("Index", Static | BindingFlags.DeclaredOnly);
            if (index == null || !index.IsLiteral || type.GetConstructor(Type.EmptyTypes) == null) continue;
            int id = (int)index.GetRawConstantValue();
            if (assembly == mod && id != 951 && id != 954 && (id < 1005 || id > 1013) && id != PalaceId) continue;
            var block = (Block)Activator.CreateInstance(type);
            block.BlockIndex = id;
            BlocksManager.Blocks[id] = block;
            var fluid = block as FluidBlock;
            if (fluid != null) BlocksManager.FluidBlocks[id] = fluid;
        }
        BlocksManager.LoadBlocksData(native);
        BlocksManager.LoadBlocksData(geology);
        BlocksManager.LoadBlocksData(palace);
        BlocksManager.FluidBlocks[WaterBlock.Index].Initialize();
        BlocksManager.Blocks[951].MaxStacking = 1;
    }

    private static void CheckRegistration(Assembly mod, XElement xml)
    {
        var block = BlocksManager.Blocks[PalaceId];
        Check(block.GetType().Assembly == mod && block.DefaultCategory == "SCIEPort.Structures" &&
            !block.GetCreativeValues().Any() && block.IsFluidBlocker, "registered mechanism category and hidden variants");
        for (int data = 0; data <= 18; data++)
        {
            int value = Terrain.MakeBlockValue(PalaceId, 0, data);
            Check(Terrain.ExtractContents(value) == PalaceId && Terrain.ExtractData(value) == data, "native DATA round trip");
            Check(block.GetFaceTextureSlot(0, value) < 14 && block.IsInteractive(null, value) == (data <= 9), "texture and interaction roles");
        }
        byte[] ids = typeof(IPackage).Assembly.GetTypes().Concat(mod.GetTypes())
            .Where(type => !type.IsAbstract && !type.IsInterface && typeof(IPackage).IsAssignableFrom(type))
            .Select(type => ((IPackage)FormatterServices.GetUninitializedObject(type)).ID).ToArray();
        var packet = (IPackage)Activator.CreateInstance(mod.GetType("SCIEPort.Features.TidePalace.TidePalacePackage", true));
        Check(packet.ID == 235 && ids.Count(id => id == packet.ID) == 1, "unique package number");
        Put(packet, "Point", new Point3(-224, 23, 16));
        Put(packet, "ExpectedData", (byte)7);
        using (var writer = new PackageStreamWriter())
        {
            packet.WriteData(writer);
            byte[] bytes = writer.Data();
            Check(writer.BaseStream.Length == 13 && bytes.Length == 14, "fixed 13-byte payload and native stream padding");
            var copy = (IPackage)Activator.CreateInstance(packet.GetType());
            using (var reader = new PackageStreamReader(bytes)) copy.ReadData(reader);
            Check(Get<Point3>(copy, "Point") == new Point3(-224, 23, 16) && Get<byte>(copy, "ExpectedData") == 7, "signed packet coordinates round trip");
        }
        var db = XmlDatabaseSerializer.LoadDatabase(xml);
        var gd = new GameDatabase(db);
        foreach (string name in new[] { "Project", "GameProject" })
        {
            var values = new ValuesDictionary();
            values.PopulateFromDatabaseObject(db.FindDatabaseObject(name, gd.ProjectTemplateType, true));
            var configured = values.GetValue<ValuesDictionary>("SCIEPortTidePalace");
            Check(configured.GetValue<string>("Class") == behaviorType.FullName &&
                values.ContainsKey("BlockEntities") && values.ContainsKey("Terrain"), name + " world loading dependency names");
            var project = new Project { m_gameDatabase = gd };
            var behavior = (SubsystemBlockBehavior)Activator.CreateInstance(behaviorType);
            behavior.Initialize(project, configured);
            project.m_subsystems.Add(behavior);
            var registry = new SubsystemBlockBehaviors();
            registry.Initialize(project, values.GetValue<ValuesDictionary>("BlockBehaviors"));
            var otherBindings = BlocksManager.Blocks.Select(b => b.Behaviors).ToArray();
            try
            {
                for (int i = 0; i < otherBindings.Length; i++)
                    if (i != PalaceId) BlocksManager.Blocks[i].Behaviors = string.Empty;
                registry.Load(new ValuesDictionary());
                SubsystemBlockBehavior[] bindings = registry.GetBlockBehaviors(PalaceId);
                Check(bindings.Length == 1 && bindings[0] == behavior, name + " uses native named registry exactly once");
            }
            finally
            {
                for (int i = 0; i < otherBindings.Length; i++) BlocksManager.Blocks[i].Behaviors = otherBindings[i];
            }
        }
    }

    private static void Layout(object site)
    {
        var slices = new Func<int, int, int, int>[4, 3];
        for (int z = 0; z < 3; z++)
        for (int x = 0; x < 4; x++)
        {
            object layout = Make(layoutType, site, new Point2(x * 16, z * 16));
            slices[x, z] = (Func<int, int, int, int>)layoutType.GetMethod("At", Instance).CreateDelegate(typeof(Func<int, int, int, int>), layout);
        }
        Func<Point3, int> at = p =>
        {
            if (p.X < 0 || p.X >= 64 || p.Z < 0 || p.Z >= 48 || p.Y < -21 || p.Y > 18) return WaterBlock.Index;
            int value = slices[p.X >> 4, p.Z >> 4](p.X & 15, p.Y, p.Z & 15);
            return value < 0 ? WaterBlock.Index : value;
        };
        var gate = new Point3(29, 7, 30);
        Check(Terrain.ExtractData(at(gate)) == 13, "sealed gate remains after building staircase");
        int air = 0;
        var leaks = new List<string>();
        for (int x = 0; x < 64; x++)
        for (int z = 0; z < 48; z++)
        for (int y = -20; y <= 18; y++)
        {
            var p = new Point3(x, y, z);
            if (at(p) != 0) continue;
            air++;
            // Water can flow level or down, and can fill a neighbor after moving down.
            for (int i = 0; i < Sides.Length; i++)
            {
                if (i == 4) continue;
                if (Terrain.ExtractContents(at(p + Sides[i])) == WaterBlock.Index)
                    leaks.Add(p + " from " + Sides[i]);
            }
        }
        Check(leaks.Count == 0, "sealed air, leaks: " + string.Join("; ", leaks.Take(12)));
        Check(air >= 1000, "palace includes full height chambers and distinct air bells");
        var entry = new Point3(11, 11, 8);
        Check(Reach(at, new Point3(11, 17, 4), entry, true, false), "upper opening reaches first air bell");
        var hub = new Point3(29, 6, 27);
        Check(Reach(at, entry, hub, true, false), "first air bell reaches dry ante-room");
        var east = new Point3(46, 10, 18);
        Check(Reach(at, east, new Point3(47, 3, 23), true, false), "east air overlook to the middle dial");
        Check(Reach(at, new Point3(10, 4, 25), new Point3(12, -5, 25), true, false), "west bell to the submerged counterweight");
        var altar = new Point3(29, -4, 41);
        Check(!Reach(at, hub, altar, false, false), "sealed gate cuts the dry path");
        Check(Reach(at, hub, altar, false, true), "open gate reaches altar by traversable descending stairs");
        Check(Reach(at, altar, new Point3(51, 8, 40), true, false), "sump and return shaft connect altar to sea");
    }

    private static bool Reach(Func<Point3, int> at, Point3 from, Point3 to, bool swim, bool gateOpen)
    {
        Func<Point3, bool> pass = p =>
        {
            int value = at(p), id = Terrain.ExtractContents(value);
            return id == 0 || (swim && id == WaterBlock.Index) || (gateOpen && id == PalaceId && Terrain.ExtractData(value) == 13);
        };
        var visited = new HashSet<Point3> { from };
        var queue = new Queue<Point3>();
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            Point3 p = queue.Dequeue();
            if (p == to) return true;
            foreach (var side in Sides)
            {
                Point3 next = p + side;
                if (next.X < -1 || next.X > 64 || next.Z < -1 || next.Z > 48 || next.Y < -17 || next.Y > 20) continue;
                if (pass(next) && pass(next + new Point3(0, 1, 0)) && visited.Add(next)) queue.Enqueue(next);
            }
        }
        return false;
    }

    private static void Puzzle()
    {
        for (int solution = 1; solution <= 26; solution++)
        {
            var candidate = Make(siteType, new Point2(0, 0), 32, solution);
            var state = Make(stateType, candidate, 4094, 0, 0);
            Check(!(bool)Call(state, "Lift") && !(bool)Call(state, "Turn", 0), "partial ruin cannot grant progress");
            Put(state, "Slices", 4095);
            Check((bool)Call(state, "Lift") && !Property<bool>(state, "CanStock"), "counterweight alone cannot unlock");
            for (int dial = 0, n = solution; dial < 3; dial++, n /= 3)
                for (int steps = 0; steps < n % 3; steps++) Check((bool)Call(state, "Turn", dial), "rotate target digit");
            Check(Property<bool>(state, "CanStock") && !(bool)Call(state, "Lift"), "solved puzzle authorizes one stock");
            Call(state, "MarkStocked");
            var reloaded = Make(stateType, candidate, 4095, solution, Property<int>(state, "SaveFlags"));
            Check(!Property<bool>(reloaded, "CanStock"), "save reload cannot stock a second item");
        }
    }

    private sealed class Storage : TerrainSerializer23.IStorage
    {
        internal readonly HashSet<Point2> Existing = new HashSet<Point2>();
        public bool Exists(Point2 p) { return Existing.Contains(p); }
        public void Open(string directoryName, string suffix) { }
        public int Load(Point2 p, byte[] buffer) { return -1; }
        public void Save(Point2 p, byte[] buffer, int bytes) { Existing.Add(p); }
        public void Dispose() { }
    }

    private static SubsystemTerrain Fixture(GameDatabase db, TerrainGenerationMode mode, out Project p, out SubsystemBlockBehavior behavior)
    {
        p = new Project();
        p.m_gameDatabase = db;
        p.m_projectTemplate = db.Database.FindDatabaseObject("Project", db.ProjectTemplateType, true);
        var settings = (WorldSettings)FormatterServices.GetUninitializedObject(typeof(WorldSettings));
        settings.TerrainGenerationMode = mode;
        settings.BiomeSize = 1f;
        settings.IslandSize = new Vector2(400f, 400f);
        settings.DisableBlocks = string.Empty;
        Add(p, new SubsystemGameInfo { WorldSeed = Seed, WorldSettings = settings });
        Add(p, new SubsystemInventories());
        Add(p, new SubsystemPlayers());
        Add(p, new SubsystemBottomSuckerBlockBehavior());
        Add(p, new SubsystemBlockEntities());
        var terrain = new SubsystemTerrain { Terrain = new Terrain(), TerrainSerializer = new TerrainSerializer23 { m_storage = new Storage() } };
        Add(p, terrain);
        behavior = (SubsystemBlockBehavior)Activator.CreateInstance(behaviorType);
        Add(p, behavior);
        behavior.Load(new ValuesDictionary());
        return terrain;
    }

    private static void Generate(GameDatabase db, TerrainGenerationMode mode)
    {
        Project project;
        SubsystemBlockBehavior behavior;
        var world = Fixture(db, mode, out project, out behavior);
        ITerrainContentsGenerator generator = mode == TerrainGenerationMode.Better
            ? (ITerrainContentsGenerator)new TerrainContentsGeneratorBetter(world) : new TerrainContentsGenerator24(world);
        var choose = siteType.GetMethod("Choose", Static);
        var candidates = new List<object>();
        for (int z = -7; z < 3; z++)
        for (int x = -7; x < 3; x++)
        {
            var region = new Point2(x, z);
            object selected = choose.Invoke(null, new object[] { generator, Seed, region });
            if (selected != null) candidates.Add(selected);
        }
        Check(candidates.Count > 0 && candidates.Count < 50, mode + " native seed produces rare offshore sites");
        int completed = 0;
        foreach (object selected in candidates.OrderBy(s => Get<Point2>(s, "Origin").X * Get<Point2>(s, "Origin").X +
            Get<Point2>(s, "Origin").Y * Get<Point2>(s, "Origin").Y).Take(3))
        {
            Point2 origin = Get<Point2>(selected, "Origin");
            int b = Get<int>(selected, "BaseY");
            Check(b + 18 <= generator.OceanLevel - 6, "towers fully submerged");
            var storage = (Storage)world.TerrainSerializer.m_storage;
            var slices = new List<TerrainChunk>();
            for (int z = 0; z < 3; z++)
            for (int x = 0; x < 4; x++)
            {
                var chunk = world.Terrain.AllocateChunk((origin.X >> 4) + x, (origin.Y >> 4) + z);
                generator.GenerateChunkContentsPass1(chunk);
                generator.GenerateChunkContentsPass2(chunk);
                generator.GenerateChunkContentsPass3(chunk);
                generator.GenerateChunkContentsPass4(chunk);
                slices.Add(chunk);
            }
            if (completed == 0)
            {
                storage.Existing.Add(slices[3].Coords);
                var negative = (SubsystemBlockBehavior)Activator.CreateInstance(behaviorType);
                Put(negative, "m_project", project);
                negative.Load(new ValuesDictionary());
                Check(!(bool)Call(negative, "Generate", slices[0], generator, Seed, mode), "a serialized neighbor rejects footprint");
                storage.Existing.Clear();
                CommonLib.WorkType = WorkType.Client;
                Check(!(bool)Call(behavior, "Generate", slices[0], generator, Seed, mode) &&
                    slices[0].ModificationCounter == 0, "pure client cannot generate or dirty terrain");
                CommonLib.WorkType = WorkType.Local;
                Console.WriteLine(mode + " sample palace for numeric WorldSeed " + Seed + ": " + origin +
                    ", deck " + b + ", sea " + generator.OceanLevel + ".");
            }
            foreach (TerrainChunk chunk in slices.AsEnumerable().Reverse())
            {
                bool applied = (bool)Call(behavior, "Generate", chunk, generator, Seed, mode);
                if (!applied)
                {
                    object layout = Make(layoutType, selected, chunk.Origin);
                    var at = (Func<int, int, int, int>)layoutType.GetMethod("At", Instance).CreateDelegate(typeof(Func<int, int, int, int>), layout);
                    var natural = (Func<int, bool>)behaviorType.Assembly.GetType("SCIEPort.Features.TidePalace.PalaceTerrain")
                        .GetMethod("Natural", Static).CreateDelegate(typeof(Func<int, bool>));
                    var rejected = new HashSet<string>();
                    for (int x = 0; x < 16; x++)
                    for (int z = 0; z < 16; z++)
                    for (int y = -12; y <= 18; y++)
                    {
                        int id = Terrain.ExtractContents(chunk.GetCellValueFast(x, b + y, z));
                        if (at(x, y, z) >= 0 && !natural(id)) rejected.Add(id + "=" + BlocksManager.Blocks[id].GetType().Name);
                    }
                    throw new InvalidOperationException("native slice rejected " + mode + " origin " + chunk.Origin + ": " + string.Join(", ", rejected));
                }
                storage.Existing.Add(chunk.Coords);
                Check(chunk.ModificationCounter == 1, "reserved slice persists");
            }
            completed++;
        }
        Console.WriteLine(mode + ": " + candidates.Count + "/100 eligible regions, " + completed + " full native palaces.");
    }

    private static void Reward(GameDatabase db, WorkType mode)
    {
        CommonLib.WorkType = mode;
        Project project;
        SubsystemBlockBehavior behavior;
        var terrain = Fixture(db, TerrainGenerationMode.Continent, out project, out behavior);
        var blockBehaviors = new SubsystemBlockBehaviors();
        Add(project, blockBehaviors);
        blockBehaviors.m_blockBehaviorsByContents = new SubsystemBlockBehavior[BlocksManager.Blocks.Length][];
        for (int i = 0; i < blockBehaviors.m_blockBehaviorsByContents.Length; i++)
            blockBehaviors.m_blockBehaviorsByContents[i] = new SubsystemBlockBehavior[0];
        terrain.m_subsystemBlockBehaviors = blockBehaviors;
        var updater = (TerrainUpdater)FormatterServices.GetUninitializedObject(typeof(TerrainUpdater));
        Put(updater, "m_terrain", terrain.Terrain);
        Put(terrain, "<TerrainUpdater>k__BackingField", updater);
        var site = Make(siteType, new Point2(0, 0), 32, 17);
        var p = new Point3(29, 28, 43);
        terrain.Terrain.AllocateChunk(p.X >> 4, p.Z >> 4).State = TerrainChunkState.Valid;
        terrain.Terrain.SetCellValueFast(p.X, p.Y, p.Z, Terrain.MakeBlockValue(PalaceId, 0, 17));
        var state = Make(stateType, site, 4095, 17, 1);
        ((IDictionary)Get<object>(behavior, "sites")).Add(new Point2(0, 0), state);
        var blockEntities = project.FindSubsystem<SubsystemBlockEntities>(true);
        Call(behavior, "Synchronize", state);
        Check(blockEntities.GetBlockEntity(p.X, p.Y, p.Z) == null, "one wing leaves real display empty");
        var counterweight = new Point3(11, 27, 25);
        terrain.Terrain.AllocateChunk(counterweight.X >> 4, counterweight.Z >> 4).State = TerrainChunkState.Valid;
        terrain.Terrain.SetCellValueFast(counterweight.X, counterweight.Y, counterweight.Z, Terrain.MakeBlockValue(PalaceId, 0, 9));
        var player = MakePlayer(project);
        player.ComponentCreatureModel.m_eyePosition = new Vector3(11.5f, 27.5f, 23.5f);
        var fixturePlayer = new ComponentMiner { m_entity = player.Entity, ComponentPlayer = player };
        var hit = new TerrainRaycastResult { CellFace = new CellFace(11, 27, 25, 2), Value = Terrain.MakeBlockValue(PalaceId, 0, 9) };
        var projectNet = (ProjectNet)FormatterServices.GetUninitializedObject(typeof(ProjectNet));
        Put(projectNet, "m_subsystems", project.m_subsystems);
        Client client = null;
        if (mode == WorkType.Server)
        {
            var players = project.FindSubsystem<SubsystemPlayers>(true);
            client = new Client(player.PlayerGuid, null) { State = ClientState.ProjectLoaded };
            Put(client, "<SubsystemPlayers>k__BackingField", players);
            Put(client, "<ID>k__BackingField", (byte)5);
            CommonLib.Net.Clients[5] = client;
            players.m_playersData.Add(player.PlayerData);
            Check(client.PlayerData == player.PlayerData, "native client ID resolves the ordinary player");
        }
        Action click = () =>
        {
            if (mode == WorkType.Local)
                Check(behavior.OnInteract(hit, fixturePlayer), "native local interaction handled");
            else
            {
                var packet = (IPackage)Make(behaviorType.Assembly.GetType("SCIEPort.Features.TidePalace.TidePalacePackage"));
                Put(packet, "Point", counterweight);
                Put(packet, "ExpectedData", (byte)9);
                packet.From = client;
                packet.Handle(projectNet, CommonLib.Net, true);
            }
            ((IUpdateable)behavior).Update(0f);
        };
        terrain.Terrain.SetCellValueFast(11, 27, 24, GraniteBlock.Index);
        click();
        Check(!Property<bool>(state, "West"), mode + " blocked view cannot operate counterweight");
        terrain.Terrain.SetCellValueFast(11, 27, 24, WaterBlock.Index);
        click();
        Check(Property<bool>(state, "West"), mode + " second wing completed by real player interaction");
        var entity = blockEntities.GetBlockEntity(p.X, p.Y, p.Z);
        Check(entity != null && terrain.Terrain.GetCellContents(p.X, p.Y, p.Z) == 954, "native display created at altar");
        var inventory = entity.Entity.FindComponent<ComponentChest>(true);
        Check(inventory.SlotsCount == 1 && inventory.GetSlotCount(0) == 1 && Terrain.ExtractContents(inventory.GetSlotValue(0)) == 951,
            "native display contains one actual unchanged trident");
        inventory.RemoveSlotItems(0, 1);
        Call(behavior, "Synchronize", state);
        Check(inventory.GetSlotCount(0) == 0, "taking the trident leaves the existing display empty");
        project.RemoveEntity(entity.Entity, true);
        terrain.Terrain.SetCellValueFast(p.X, p.Y, p.Z, 0);
        var save = new ValuesDictionary();
        behavior.Save(save);
        var reloaded = (SubsystemBlockBehavior)Activator.CreateInstance(behaviorType);
        Put(reloaded, "m_project", project);
        reloaded.Load(save);
        object restored = ((IDictionary)Get<object>(reloaded, "sites"))[new Point2(0, 0)];
        Call(reloaded, "Synchronize", restored);
        Check(blockEntities.GetBlockEntity(p.X, p.Y, p.Z) == null, "destroying and reloading a claimed display leaves no second trident");
    }

    private static ComponentPlayer MakePlayer(Project project)
    {
        var entity = (Entity)FormatterServices.GetUninitializedObject(typeof(Entity));
        Put(entity, "m_project", project);
        Put(entity, "m_isAddedToProject", true);
        var player = new ComponentPlayer
        {
            m_entity = entity,
            PlayerGuid = Guid.NewGuid(),
            ComponentHealth = new ComponentHealth { m_entity = entity, Health = 1f },
            ComponentCreatureModel = new ComponentCreatureModel { m_entity = entity }
        };
        var playerData = (PlayerData)FormatterServices.GetUninitializedObject(typeof(PlayerData));
        playerData.PlayerGUID = player.PlayerGuid;
        playerData.ComponentPlayer = player;
        player.PlayerData = playerData;
        Put(entity, "m_components", new List<Component> { player, player.ComponentHealth, player.ComponentCreatureModel });
        return player;
    }
}

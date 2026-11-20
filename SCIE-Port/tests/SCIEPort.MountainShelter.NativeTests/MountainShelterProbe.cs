using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using Engine;
using Game;
using Game.NetWork;
using Game.NetWork.Packages;
using GameEntitySystem;
using SCIEPort.Features.MountainShelter;
using SCIEPort.Mod;

public static class MountainShelterProbe
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static int checks;

    private static void Check(bool ok, string detail)
    {
        checks++;
        if (!ok) throw new Exception("Mountain shelter: " + detail);
    }

    private static T Bare<T>() { return (T)FormatterServices.GetUninitializedObject(typeof(T)); }

    private static void Put(object value, string field, object data)
    {
        for (var type = value.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo f = type.GetField(field, Instance) ?? type.GetField("<" + field + ">k__BackingField", Instance);
            if (f != null) { f.SetValue(value, data); return; }
        }
        throw new MissingFieldException(value.GetType().FullName, field);
    }

    private static void Add(Project project, Subsystem subsystem)
    {
        Put(subsystem, "m_project", project);
        project.m_subsystems.Add(subsystem);
    }

    private static Entity NewEntity(Project project)
    {
        var entity = Bare<Entity>();
        Put(entity, "m_project", project);
        Put(entity, "m_isAddedToProject", true);
        return entity;
    }

    private static ComponentPlayer NewPlayer(Project project, SubsystemTerrain terrain, SubsystemGameInfo info)
    {
        Entity entity = NewEntity(project);
        var p = new ComponentPlayer { m_entity = entity, PlayerGuid = Guid.NewGuid() };
        var body = new ComponentBody { m_entity = entity, BoxSize = new Vector3(.6f, 1.8f, .6f) };
        p.ComponentBody = body;
        p.ComponentCreatureModel = new ComponentHumanModel { m_entity = entity };
        p.ComponentHealth = new ComponentHealth { m_entity = entity, Health = 1f };
        p.ComponentVitalStats = new ComponentVitalStats { m_entity = entity, Sleep = .55f };
        p.ComponentSleep = new ComponentSleep { m_entity = entity, m_componentPlayer = p,
            m_subsystemTerrain = terrain, m_subsystemGameInfo = info };
        p.m_subsystemPlayerStats = new SubsystemPlayerStats();
        var data = Bare<PlayerData>();
        data.PlayerGUID = p.PlayerGuid;
        data.ComponentPlayer = p;
        data.m_stateMachine = new StateMachine();
        data.m_stateMachine.m_currentState = new StateMachine.State { Name = "Playing" };
        p.PlayerData = data;
        var inventory = new ComponentInventory { m_entity = entity };
        inventory.m_slots.Add(new ComponentInventoryBase.Slot());
        p.ComponentMiner = new ComponentMiner { m_entity = entity, ComponentPlayer = p, Inventory = inventory };
        Put(entity, "m_components", new List<Component> { p, body, p.ComponentHealth, p.ComponentCreatureModel,
            p.ComponentSleep, p.ComponentVitalStats, p.ComponentMiner, inventory });
        project.FindSubsystem<SubsystemPlayers>(true).m_componentPlayers.Add(p);
        project.FindSubsystem<SubsystemBodies>(true).OnEntityAdded(entity);
        return p;
    }

    private static void InterfacePoints(Block shelter)
    {
        int index = MountainShelterBlock.Index;
        int n = 0;
        foreach (int value in shelter.GetCreativeValues()) { Check(value == index, "folded creative item"); n++; }
        Check(n == 1, "parts hidden in creative");
        for (int r = 0; r < 4; r++)
        {
            for (int part = 0; part < 16; part++)
            {
                var value = Terrain.MakeBlockValue(index, 0, 1 + 16 * part + r);
                BoundingBox[] boxes = shelter.GetCustomCollisionBoxes(null, value);
                Check(boxes.Length >= 1, "native raycast gets nonempty boxes");
                foreach (var box in boxes)
                    Check(box.Min.X >= 0 && box.Min.Y >= 0 && box.Min.Z >= 0 && box.Max.X <= 1 && box.Max.Y <= 1 && box.Max.Z <= 1
                        && box.Min.X < box.Max.X && box.Min.Y < box.Max.Y && box.Min.Z < box.Max.Z, "collision stays within its own cell");
                Check(shelter.GetEmittedLightAmount(value) == 0, "new parts start dark");
            }
            Check(shelter.GetEmittedLightAmount(Terrain.MakeBlockValue(index, 0, 105 + r)) == 12, "lit lantern is native level 12");
            Check(shelter.GetEmittedLightAmount(Terrain.MakeBlockValue(index, 0, 121 + r)) == 0, "second control shares one source");
        }
    }

    private static void Move(ComponentPlayer player, Vector3 point, int standing)
    {
        player.ComponentBody.Position = point;
        player.ComponentBody.StandingOnValue = standing;
        player.ComponentCreatureModel.m_eyePosition = point + new Vector3(0, 1.55f, 0);
    }

    private static void Press(SubsystemMountainShelter tent, ComponentPlayer p, Terrain cells, Point3 point)
    {
        var hit = new TerrainRaycastResult { Value = cells.GetCellValue(point.X,point.Y,point.Z),
            CellFace = new CellFace(point.X,point.Y,point.Z,4) };
        Check(tent.OnInteract(hit, p.ComponentMiner), "native interaction routed to shelter");
        tent.Update(.05f);
    }

    private static int Cell(Terrain cells, Point3 point) { return cells.GetCellValue(point.X, point.Y, point.Z); }
    private static void Set(Terrain cells, Point3 point, int value) { cells.SetCellValueFast(point.X, point.Y, point.Z, value); }

    private sealed class InterruptedWrite : SubsystemBlockBehavior
    {
        public override int[] HandledBlocks { get { return new[] { 947 }; } }
        public override void OnBlockAdded(int value, int oldValue, int x, int y, int z)
        {
            if (x == 16 && y == 64 && z == 7)
                throw new InvalidOperationException("Injected interruption after the second shelter write.");
        }
    }

    private static void NativeLight(SubsystemTerrain terrain, TerrainUpdater updater, Point3 light, int expected)
    {
        var chunk = terrain.Terrain.GetChunkAtCell(light.X, light.Z);
        for (int x = 0; x < 16; x++) for (int z = 0; z < 16; z++)
        {
            chunk.SetTopHeightFast(x, z, 68);
            chunk.SetBottomHeightFast(x, z, 63);
        }
        var sources = updater.GetType().GetField("m_lightSources", Instance);
        sources.SetValue(updater, Activator.CreateInstance(sources.FieldType));
        updater.GetType().GetMethod("GenerateChunkLightSources", Instance).Invoke(updater, new object[] { chunk });
        Check(Terrain.ExtractLight(Cell(terrain.Terrain, light)) == expected, "target TerrainUpdater collects emitted light");
    }

    public static void Run(int defaultEmission)
    {
        var oldBlocks = (Block[])BlocksManager.Blocks.Clone();
        var oldNet = CommonLib.Net;
        var oldMode = CommonLib.WorkType;
        var oldHooks = ModsManager.ModHooks;
        try
        {
            CommonLib.WorkType = WorkType.Local;
            CommonLib.Net = Bare<NetNode>();
            var pending = new List<IPackage>();
            Put(CommonLib.Net, "PendingPackages", pending);
            CommonLib.Net.Clients = new Dictionary<int, Client>();
            ModsManager.ModHooks = new Dictionary<string, ModsManager.ModHook>();
            var shelter = new MountainShelterBlock { BlockIndex = 947, MaxStacking = 1, IsCollidable = true,
                IsTransparent = true, DefaultEmittedLightAmount = defaultEmission };
            BlocksManager.Blocks[0] = new AirBlock();
            BlocksManager.Blocks[3] = new GraniteBlock { BlockIndex = 3, IsCollidable = true };
            BlocksManager.Blocks[947] = shelter;
            InterfacePoints(shelter);
            using (var cells = new Terrain())
            {
                for (int x = -1; x <= 2; x++) for (int z = -1; z <= 1; z++)
                    cells.AllocateChunk(x, z).State = TerrainChunkState.Valid;
                var project = new Project();
                var terrain = Bare<SubsystemTerrain>();
                var updater = Bare<TerrainUpdater>();
                Put(updater, "m_terrain", cells);
                Put(terrain, "Terrain", cells);
                Put(terrain, "TerrainUpdater", updater);
                terrain.m_modifiedCells = new Dictionary<Point3, bool>();
                terrain.m_subsystemBlockBehaviors = new SubsystemBlockBehaviors();
                terrain.m_subsystemBlockBehaviors.m_blockBehaviorsByContents = new SubsystemBlockBehavior[BlocksManager.Blocks.Length][];
                for (int i = 0; i < BlocksManager.Blocks.Length; i++)
                    terrain.m_subsystemBlockBehaviors.m_blockBehaviorsByContents[i] = new SubsystemBlockBehavior[0];
                var tent = new SubsystemMountainShelter();
                terrain.m_subsystemBlockBehaviors.m_blockBehaviorsByContents[947] = new SubsystemBlockBehavior[] { tent };
                Add(project, terrain);
                Add(project, new SubsystemBodies());
                Add(project, new SubsystemPlayers());
                var info = new SubsystemGameInfo { WorldSettings = Bare<WorldSettings>() };
                info.WorldSettings.DisableBlocks = string.Empty;
                Add(project, info);
                Add(project, new SubsystemPickables { m_subsystemGameInfo = info });
                Add(project, tent);
                tent.Load(new TemplatesDatabase.ValuesDictionary());
                var left = NewPlayer(project, terrain, info);
                var right = NewPlayer(project, terrain, info);
                var extra = NewPlayer(project, terrain, info);
                Point3 root = new Point3(15,64,7);
                Point3 support = root - new Point3(0,1,0);
                Point3 obstacle = root + new Point3(1,1,1);
                // The footprint crosses a chunk boundary, with one corner lower by one cell.
                for (int x = 15; x <= 16; x++) for (int z = 6; z <= 8; z++)
                    cells.SetCellValueFast(x, x == 16 && z == 8 ? 62 : 63, z, 3);
                var bag = (ComponentInventory)left.ComponentMiner.Inventory;
                bag.m_slots[0].Value = 947;
                bag.m_slots[0].Count = 1;
                Move(left, new Vector3(15.5f,64,5.5f), 3);
                var placement = new MountainShelterPackage();
                Put(placement, "Point", support);
                Put(placement, "Expected", 3);
                Put(placement, "Place", true);
                var enqueue = typeof(SubsystemMountainShelter).GetMethod("Enqueue", Instance);
                Set(cells, obstacle, 3);
                enqueue.Invoke(tent, new object[] { left, placement, null });
                tent.Update(.05f);
                Check(Cell(cells, root) == 0 && bag.GetSlotCount(0) == 1 && Cell(cells, obstacle) == 3, "obstruction leaves world and inventory intact");
                Set(cells, obstacle, 0);
                CommonLib.WorkType = WorkType.Client;
                enqueue.Invoke(tent, new object[] { left, placement, null });
                tent.Update(.05f);
                Check(Cell(cells, root) == 0, "ordinary client makes no local world edits");
                CommonLib.WorkType = WorkType.Local;
                TerrainChunk adjacent = cells.GetChunkAtCell(16,8);
                adjacent.State = (TerrainChunkState)((int)TerrainChunkState.InvalidLight - 1);
                enqueue.Invoke(tent, new object[] { left, placement, null });
                tent.Update(.05f);
                Check(Cell(cells, root) == 0 && bag.GetSlotCount(0) == 1, "unfinished adjacent chunk rejects deployment");
                adjacent.State = TerrainChunkState.Valid;
                terrain.m_subsystemBlockBehaviors.m_blockBehaviorsByContents[947] = new SubsystemBlockBehavior[] { tent, new InterruptedWrite() };
                enqueue.Invoke(tent, new object[] { left, placement, null });
                tent.Update(.05f);
                Check(Cell(cells, root) == 0 && cells.GetCellValue(16,64,7) == 0 && bag.GetSlotCount(0) == 1,
                    "interruption after a native write restores both cells and preserves inventory");
                terrain.m_subsystemBlockBehaviors.m_blockBehaviorsByContents[947] = new SubsystemBlockBehavior[] { tent };
                enqueue.Invoke(tent, new object[] { left, placement, null });
                tent.Update(.05f);
                Check(Terrain.ExtractContents(Cell(cells, root)) == 947 && bag.GetSlotCount(0) == 0, "successful deployment consumes one item");
                Point3 leg = new Point3(16,63,8);
                Check(Terrain.ExtractContents(Cell(cells,leg)) == 947 && cells.GetCellValue(16,62,8) == 3, "one-cell step uses leg");
                Point3 lamp = new Point3(15,65,8);
                Point3 lampRight = lamp + new Point3(1,0,0);
                Move(left, new Vector3(15.5f,64.18f,7.5f), Cell(cells,root));
                Press(tent, left, cells, new Point3(15,65,7));
                Press(tent, left, cells, lamp);
                NativeLight(terrain, updater, lamp, 12);
                Check(shelter.GetEmittedLightAmount(Cell(cells,lampRight)) == 0, "two controls operate one light source across chunk seam");
                var animalEntity = NewEntity(project);
                var animal = new ComponentCreature { m_entity = animalEntity, Category = CreatureCategory.LandPredator };
                Put(animalEntity, "m_components", new List<Component> { animal });
                var loader = new SCIEPortLoader();
                float power = 3f, impulse = 2f, stun = 1f;
                bool recalc = false, skip;
                loader.AttackBody(left.ComponentBody, animal, Vector3.Zero, Vector3.UnitZ, ref power, true);
                loader.AttackPowerParameter(left.ComponentBody, animal, Vector3.Zero, Vector3.UnitZ, ref impulse, ref stun, ref recalc);
                loader.OnCreatureInjure(left.ComponentHealth, .5f, animal, false, "bite", out skip);
                Check(power == 0f && impulse == 0f && stun == 0f && skip, "closed berth cancels animal damage and knockback");
                loader.OnCreatureInjure(left.ComponentHealth, .5f, right, false, "PVP", out skip);
                Check(!skip, "player attacks retain native handling");
                loader.OnCreatureInjure(left.ComponentHealth, .5f, null, false, "fall", out skip);
                Check(!skip, "environment damage retains native handling");
                Move(right, new Vector3(16.5f,64.18f,7.5f), cells.GetCellValue(16,64,7));
                loader.OnCreatureInjure(right.ComponentHealth, .5f, animal, false, "bite", out skip);
                Check(!skip, "open right berth has no protection");
                Press(tent, right, cells, new Point3(16,65,7));
                Press(tent, right, cells, lampRight);
                NativeLight(terrain, updater, lamp, 0);
                CommonLib.WorkType = WorkType.Server;
                var client = new Client(right.PlayerGuid, null) { State = ClientState.ProjectLoaded };
                var players = project.FindSubsystem<SubsystemPlayers>(true);
                players.m_playersData.Add(right.PlayerData);
                Put(client, "SubsystemPlayers", players);
                Put(client, "ID", (byte)7);
                CommonLib.Net.Clients[7] = client;
                var netProject = Bare<ProjectNet>();
                Put(netProject, "m_subsystems", project.m_subsystems);
                var lightRequest = new MountainShelterPackage();
                Put(lightRequest, "Point", lampRight);
                Put(lightRequest, "Expected", Terrain.ReplaceLight(Cell(cells, lampRight), 0));
                MountainShelterPackage received;
                using (var writer = new PackageStreamWriter())
                {
                    lightRequest.WriteData(writer);
                    using (var reader = new PackageStreamReader(writer.Data()))
                    {
                        received = new MountainShelterPackage();
                        received.ReadData(reader);
                    }
                }
                received.Handle(netProject, CommonLib.Net, true);
                tent.Update(.05f);
                Check(shelter.GetEmittedLightAmount(Cell(cells, lamp)) == 0, "server ignores a request without a native sender");
                received.From = client;
                received.Handle(netProject, CommonLib.Net, true);
                tent.Update(.05f);
                Check(shelter.GetEmittedLightAmount(Cell(cells, lamp)) == 12 && pending.Exists(p => p is SubsystemTerrainPackage),
                    "authenticated remote control changes the lamp and queues native terrain packets");
                Press(tent, left, cells, new Point3(15,64,8));
                Move(extra, new Vector3(15.5f,64.18f,8.5f), Cell(cells,new Point3(15,64,8)));
                Press(tent, extra, cells, new Point3(15,64,8));
                Check(!extra.ComponentSleep.IsSleeping, "two players cannot claim the same berth");
                Press(tent, right, cells, new Point3(16,64,8));
                Check(left.ComponentSleep.IsSleeping && right.ComponentSleep.IsSleeping, "distinct berths invoke real native sleep");
                Check(pending.Exists(p => p is ComponentSleepPackage), "native sleep packets queued");
                terrain.ChangeCellNet(15,66,8,0);
                tent.Update(.05f);
                Check(!left.ComponentSleep.IsSleeping && !right.ComponentSleep.IsSleeping && Cell(cells,root) == 0 && Cell(cells,leg) == 0,
                    "damage wakes both players and cleans only the owned structure");
                CommonLib.WorkType = WorkType.Local;
                Move(left, new Vector3(15.5f,64,5.5f), 3);
                Move(right, new Vector3(17.5f,64,5.5f), 3);
                Move(extra, new Vector3(13.5f,64,5.5f), 3);
                bag.m_slots[0].Value = 947;
                bag.m_slots[0].Count = 1;
                enqueue.Invoke(tent, new object[] { left, placement, null });
                tent.Update(.05f);
                Point3 canopy = new Point3(15,66,7);
                Move(right, new Vector3(16.5f,64.18f,7.5f), Cell(cells,new Point3(16,64,7)));
                Press(tent, left, cells, canopy);
                Check(Terrain.ExtractContents(Cell(cells, root)) == 947 && bag.GetSlotCount(0) == 0, "occupant blocks roof recovery");
                Move(right, new Vector3(17.5f,64,5.5f), 3);
                Press(tent, left, cells, canopy);
                Check(Cell(cells, root) == 0 && Cell(cells, leg) == 0 && bag.GetSlotCount(0) == 1, "empty recovery returns exactly one folded shelter");
            }
            Console.WriteLine("Mountain shelter native checks passed: " + checks);
        }
        finally
        {
            CommonLib.Net = oldNet;
            CommonLib.WorkType = oldMode;
            ModsManager.ModHooks = oldHooks;
            Array.Copy(oldBlocks, BlocksManager.Blocks, oldBlocks.Length);
        }
    }
}

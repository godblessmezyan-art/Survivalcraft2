using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Engine;
using Game;
using Game.NetWork;
using Game.NetWork.Packages;
using GameEntitySystem;

public static class FishingNativeProbe
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly Vector3 Eye = new Vector3(8.5f, 66f, 4.5f);
    private const int Rod = 972;
    private static int s_checks;

    private sealed class FixedRandom : System.Random
    {
        public override int Next(int maxValue) { return maxValue == 100 ? 0 : maxValue / 2; }
    }

    private sealed class Fixture : IDisposable
    {
        public readonly Terrain Cells = new Terrain();
        public readonly SubsystemPlayers Players = new SubsystemPlayers();
        public readonly SubsystemTime Clock = new SubsystemTime();
        public readonly SubsystemInventories Inventories = new SubsystemInventories();
        public readonly SubsystemPickables Pickables = new SubsystemPickables();
        public readonly ProjectNet Project = Bare<ProjectNet>();
        public readonly SubsystemBlockBehavior Runtime;
        public readonly ComponentPlayer Player;
        public readonly ComponentInventory Inventory = new ComponentInventory();
        public readonly Client Peer;

        public Fixture(Assembly mod, Guid playerId, bool isMain)
        {
            Cells.AllocateChunk(0, 0).State = TerrainChunkState.Valid;
            Cells.AllocateChunk(0, 1).State = TerrainChunkState.Valid;
            for (int x = 2; x <= 14; x++)
                for (int z = 7; z <= 20; z++)
                {
                    Cells.SetCellValueFast(x, 63, z, WaterBlock.Index);
                    Cells.SetCellValueFast(x, 64, z, WaterBlock.Index);
                }

            var nativeTerrain = Bare<SubsystemTerrain>();
            Put(nativeTerrain, "Terrain", Cells);
            Pickables.m_subsystemGameInfo = new SubsystemGameInfo();
            Runtime = (SubsystemBlockBehavior)Activator.CreateInstance(mod.GetType("SCIEPort.Features.Fishing.SubsystemFishing", true));
            Put(Project, "m_subsystems", new List<Subsystem> { Runtime, Players, nativeTerrain, Pickables, Inventories, Clock });
            Put(Runtime, "m_project", Project);
            Put(Runtime, "m_terrain", nativeTerrain);
            Put(Runtime, "m_pickables", Pickables);
            Put(Runtime, "m_players", Players);
            Put(Runtime, "m_time", Clock);
            Put(Runtime, "m_random", new FixedRandom());
            object water = Activator.CreateInstance(mod.GetType("SCIEPort.Features.Fishing.FishingWater", true),
                Members, null, new object[] { nativeTerrain }, null);
            Put(Runtime, "m_water", water);

            var entity = Bare<Entity>();
            entity.EntityId = 33;
            Put(entity, "m_project", Project);
            Put(entity, "m_isAddedToProject", true);
            Put(entity, "m_components", new List<Component>());

            Player = new ComponentPlayer();
            Player.m_entity = entity;
            Player.ComponentMiner = new ComponentMiner { ComponentPlayer = Player, Inventory = Inventory };
            Player.ComponentBody = new ComponentBody { Position = Eye - new Vector3(0, 1.5f, 0) };
            Player.ComponentHealth = new ComponentHealth { Health = 1 };
            Player.ComponentCreatureModel = new ComponentCreatureModel { m_eyePosition = Eye, m_eyeRotation = Quaternion.Identity };
            Player.ComponentInput = new ComponentInput();
            Player.PlayerData = Bare<PlayerData>();
            Player.PlayerData.PlayerGUID = playerId;
            Player.PlayerData.ComponentPlayer = Player;
            Put(Player.PlayerData, "m_isMainPlayer", isMain);
            Players.m_componentPlayers.Add(Player);
            Players.m_playersData.Add(Player.PlayerData);

            for (int i = 0; i < 3; i++) Inventory.m_slots.Add(new ComponentInventoryBase.Slot());
            Inventory.AddSlotItems(0, Terrain.MakeBlockValue(Rod), 1);
            Put(Inventory, "Id", 101);
            Inventories.RegisterInventory(Inventory);
            Peer = new Client(playerId, Project) { State = ClientState.ProjectLoaded };
            Put(Peer, "ID", (byte)1);
            Peer.SetProjet(Project);
        }

        public void Advance(double time)
        {
            Clock.m_gameTime = time;
            ((IUpdateable)Runtime).Update(0.02f);
        }

        public int Sessions { get { return ((IDictionary)Get(Runtime, "m_sessions")).Count; } }

        public void Dispose() { Cells.Dispose(); }
    }

    public static void Run(Assembly mod)
    {
        var originalMode = CommonLib.WorkType;
        var originalNode = CommonLib.Net;
        var originalWater = BlocksManager.Blocks[WaterBlock.Index];
        var originalFish = BlocksManager.Blocks[RawFishBlock.Index];
        var originalRod = BlocksManager.Blocks[Rod];
        var originalSeed = BlocksManager.Blocks[SeedsBlock.Index];
        var originalStone = BlocksManager.Blocks[GraniteBlock.Index];

        try
        {
            CommonLib.WorkType = WorkType.Local;
            BlocksManager.Blocks[WaterBlock.Index] = new WaterBlock();
            BlocksManager.Blocks[RawFishBlock.Index] = new RawFishBlock { MaxStacking = 12 };
            BlocksManager.Blocks[Rod] = (Block)Activator.CreateInstance(mod.GetType("SCIEPort.Features.Fishing.FishingRodBlock", true));
            BlocksManager.Blocks[SeedsBlock.Index] = new SeedsBlock();
            BlocksManager.Blocks[GraniteBlock.Index] = new GraniteBlock();
            CommonLib.Net = Bare<NetNode>();
            Put(CommonLib.Net, "PendingPackages", new List<IPackage>());
            CommonLib.Net.Clients = new Dictionary<int, Client>();
            Client hostIdentity = new Client(Guid.NewGuid(), null);
            Put(hostIdentity, "ID", (byte)0);
            CommonLib.Net.Server = hostIdentity;

            var playerId = Guid.NewGuid();
            using (var host = new Fixture(mod, playerId, false))
            using (var guest = new Fixture(mod, playerId, true))
            {
                CommonLib.Net.Clients.Add(1, host.Peer);
                Check((int)UpdateOrder.Input < (int)((IUpdateable)host.Runtime).UpdateOrder
                    && (int)((IUpdateable)host.Runtime).UpdateOrder < (int)UpdateOrder.Default,
                    "input samples run between native input and player");
                ValidateWater(host, mod);
                SimulateRoom(host, guest, hostIdentity, mod);
                TestLocal(host, mod);
            }
        }
        finally
        {
            SubsystemInventories.flushSyncItems();
            CommonLib.WorkType = originalMode;
            CommonLib.Net = originalNode;
            BlocksManager.Blocks[WaterBlock.Index] = originalWater;
            BlocksManager.Blocks[RawFishBlock.Index] = originalFish;
            BlocksManager.Blocks[Rod] = originalRod;
            BlocksManager.Blocks[SeedsBlock.Index] = originalSeed;
            BlocksManager.Blocks[GraniteBlock.Index] = originalStone;
        }
        Console.WriteLine("Fishing exact-target terrain, packet, native inventory and pickable checks passed (" + s_checks + ").");
    }

    private static void ValidateWater(Fixture host, Assembly mod)
    {
        object water = Get(host.Runtime, "m_water");
        Point3? nearCell, farCell, highCell, sideCell;
        Vector3 near = Trace(mod, water, Vector3.UnitZ, 0f, out nearCell);
        Vector3 far = Trace(mod, water, Vector3.UnitZ, 1f, out farCell);
        Vector3 high = Trace(mod, water, Vector3.Normalize(new Vector3(0, 0.35f, 1f)), 1f, out highCell);
        Vector3 side = Trace(mod, water, Vector3.Normalize(new Vector3(0.24f, 0, 1f)), 1f, out sideCell);
        Check(nearCell.HasValue && farCell.HasValue && highCell.HasValue && sideCell.HasValue, "different casts reach real native water");
        Check(far.Z > near.Z + 4f, "charge determines horizontal reach");
        Check(high.Z > far.Z + 2f, "elevation changes range");
        Check(side.X > far.X + 1.5f, "yaw changes lateral landing point");
        Check(Math.Abs(far.Y - 64.875f) < 0.01f && Math.Abs(far.X - farCell.Value.X - 0.5f) > .1f,
            "surface height and fractional landing are preserved");
        Console.WriteLine("Native cast distances from shore: near {0:F2}, full {1:F2}, lofted {2:F2} blocks.",
            near.Z - Eye.Z, far.Z - Eye.Z, high.Z - Eye.Z);

        for (int y = 64; y <= 68; y++) host.Cells.SetCellValueFast(8, y, 6, GraniteBlock.Index);
        Point3? blocked;
        Vector3 stop = Trace(mod, water, Vector3.UnitZ, 1f, out blocked);
        Check(!blocked.HasValue && stop.Z < 6.05f, "first solid contact stops the flight");
        for (int y = 64; y <= 68; y++) host.Cells.SetCellValueFast(8, y, 6, 0);

        Point3 cell = nearCell.Value;
        host.Cells.SetCellValueFast(cell.X, 63, cell.Z, 0);
        Check(!(bool)Call(water, "IsFishable", cell), "one block of water is too shallow");
        host.Cells.SetCellValueFast(cell.X, 63, cell.Z, WaterBlock.Index);
        host.Cells.SetCellValueFast(cell.X, 65, cell.Z, GraniteBlock.Index);
        Check(!(bool)Call(water, "IsFishable", cell), "covered water cannot be used");
        host.Cells.SetCellValueFast(cell.X, 65, cell.Z, 0);
        Point3 tiny = new Point3(0, 64, 4);
        host.Cells.SetCellValueFast(0, 63, 4, WaterBlock.Index);
        host.Cells.SetCellValueFast(0, 64, 4, WaterBlock.Index);
        Check(!(bool)Call(water, "IsFishable", tiny), "isolated column is too small");
    }

    private static Vector3 Trace(Assembly mod, object water, Vector3 direction, float force, out Point3? cell)
    {
        Type type = mod.GetType("SCIEPort.Features.Fishing.FishingCastArc", true);
        object arc = type.GetMethod("Launch", BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, new object[] { Eye, direction, force });
        object[] args = { Eye, arc, null };
        object traced = Call(water, "Trace", args);
        cell = (Point3?)args[2];
        return (Vector3)type.GetProperty("End", Members).GetValue(traced, null);
    }

    private static void SimulateRoom(Fixture host, Fixture guest, Client hostIdentity, Assembly mod)
    {
        // Seat the seed beside the rod and leave capacity for exactly one of two portions.
        int fish = Terrain.MakeBlockValue(RawFishBlock.Index);
        int bait = Terrain.MakeBlockValue(SeedsBlock.Index, 0, (int)SeedsBlock.SeedType.WildRye);
        foreach (Fixture side in new[] { host, guest })
        {
            side.Inventory.AddSlotItems(1, bait, 2);
            side.Inventory.AddSlotItems(2, fish, 11);
        }

        CommonLib.WorkType = WorkType.Server;
        host.Advance(48);
        Deliver(Input(mod, 17, 1), host, host.Peer, true);
        Deliver(Input(mod, 2, 1), host, host.Peer, true);
        Check(host.Sessions == 0 && Pending().Count == 0, "a release requires a matching host charge");

        CommonLib.WorkType = WorkType.Client;
        guest.Advance(50);
        var ray = new Ray3(Eye, Vector3.UnitZ);
        Check(guest.Runtime.OnUse(ray, guest.Player.ComponentMiner) && Pending().Count == 0,
            "tap alone does not cast the rod");
        guest.Player.ComponentInput.m_playerInput.Aim = ray;
        guest.Player.ComponentInput.m_playerInput.Dig = ray;
        guest.Advance(50);
        IPackage charge = Pending().Single();
        Check((byte)Get(charge, "Kind") == 16 && guest.Sessions == 0 &&
            !guest.Player.ComponentInput.m_playerInput.Dig.HasValue, "native hold begins charging and consumes mining");
        Pending().Clear();
        guest.Advance(50.45);
        Check(guest.Player.ComponentCreatureModel.AimHandAngleOrder > 1f,
            "hold visibly pulls the native arm back");
        guest.Player.ComponentInput.m_playerInput.Aim = null;
        guest.Advance(50.65);
        IPackage cast = Pending().Single();
        Check((byte)Get(cast, "Kind") == 17, "release sends aim direction");
        Pending().Clear();
        guest.Advance(50.80);
        Check(guest.Player.ComponentCreatureModel.AimHandAngleOrder < 0f,
            "release moves the native arm into follow-through");

        CommonLib.WorkType = WorkType.Server;
        host.Advance(50.04);
        Deliver(charge, host, host.Peer, true);
        Check(host.Sessions == 0 && Pending().Any(p => p.ID == 236 && (byte)Get(p, "Kind") == 19),
            "host broadcasts wind-up before any projectile exists");
        host.Advance(50.69);
        Deliver(cast, host, host.Peer, true);
        Check(host.Sessions == 1 && host.Inventory.GetSlotCount(1) == 2, "host launches without spending bait");
        Check(Pending().Any(p => p.ID == 236 && (byte)Get(p, "Kind") == 20) &&
              Pending().Any(p => p.ID == 236 && (byte)Get(p, "Kind") == 21),
            "the host publishes origin, velocity and time of flight");
        Check(!Pending().Any(p => p.ID == 236 && (byte)Get(p, "Kind") == 7),
            "waiting starts only after water impact");
        host.Advance(52);
        Check(Pending().Any(p => p.ID == 236 && (byte)Get(p, "Kind") == 7),
            "flight into water transitions into waiting");
        host.Advance(64.5);
        Check(host.Inventory.GetSlotCount(1) == 2, "bait stays during approach");
        host.Advance(65);
        Check(host.Inventory.GetSlotCount(1) == 1, "only bite spends bait");
        host.Advance(65.04);
        Check(host.Inventory.GetSlotCount(1) == 1, "bite ticks do not spend more bait");

        // The guest fixture bypasses the HUD, while using the actual packet and player-input handlers.
        var phasePackets = Pending().ToArray();
        Pending().Clear();
        CommonLib.WorkType = WorkType.Client;
        Put(guest.Player.PlayerData, "m_isMainPlayer", false);
        foreach (IPackage packet in phasePackets) Deliver(packet, guest, hostIdentity, false);
        Put(guest.Player.PlayerData, "m_isMainPlayer", true);
        guest.Player.ComponentInput.m_playerInput.Interact = ray;
        guest.Advance(65.25);
        Check(!guest.Player.ComponentInput.m_playerInput.Interact.HasValue, "fishing intercepts tap before native item use");
        IPackage hook = Pending().Single();
        Check((byte)Get(hook, "Kind") == 3, "guest bite tap sends a hook action");
        Pending().Clear();

        CommonLib.WorkType = WorkType.Server;
        host.Advance(65.25);
        Deliver(hook, host, host.Peer, true);
        IPackage started = Pending().Single(p => p.ID == 236 && (byte)Get(p, "Kind") == 10);
        Check(host.Inventory.GetSlotCount(2) == 11, "hook alone grants no fish");
        Pending().Clear();

        CommonLib.WorkType = WorkType.Client;
        Put(guest.Player.PlayerData, "m_isMainPlayer", false);
        Deliver(started, guest, hostIdentity, false);
        Put(guest.Player.PlayerData, "m_isMainPlayer", true);
        guest.Player.ComponentInput.m_playerInput.Aim = ray;
        guest.Player.ComponentInput.m_playerInput.Dig = ray;
        guest.Advance(65.5);
        IPackage hold = Pending().Single();
        Check((byte)Get(hold, "Kind") == 4 && !guest.Player.ComponentInput.m_playerInput.Dig.HasValue,
            "native touch hold drives reel and suppresses mining");
        Pending().Clear();

        CommonLib.WorkType = WorkType.Server;
        host.Advance(65.5);
        Deliver(hold, host, host.Peer, true);
        uint sequence = (uint)Get(cast, "Sequence");
        // Simulate the guest's held keepalive and release at each of the two tension transitions.
        int pauses = 0;
        bool tenseBefore = false;
        double releaseUntil = 0;
        for (int i = 1; i <= 130 && host.Sessions > 0; i++)
        {
            double now = 65.5 + i * 0.1;
            host.Advance(now);
            var snapshot = Pending().LastOrDefault(p => p.ID == 236 && (byte)Get(p, "Kind") == 10);
            bool tense = snapshot != null && ((byte)Get(snapshot, "Count") & 128) != 0;
            if (tense && !tenseBefore) { pauses++; releaseUntil = now + 0.8; }
            tenseBefore = tense;
            Deliver(Input(mod, now < releaseUntil ? (byte)5 : (byte)4, sequence), host, host.Peer, true);
        }
        Check(pauses == 2 && host.Sessions == 0, "remote large fish resolves after two releases");
        Check(host.Inventory.GetSlotCount(2) == 12, "one fish uses last bit of native inventory capacity");
        Check(host.Pickables.Pickables.Count == 1 && host.Pickables.Pickables[0].Value == fish
            && host.Pickables.Pickables[0].Count == 1, "only overflow becomes a native raw fish pickable");
        Check(Pending().OfType<PickablePackage>().Count(p => p.m_type == PickablePackage.PickType.Create) == 1, "world drop schedules native sync once");
        Check(Pending().Count(p => p.ID == 236 && (byte)Get(p, "Kind") == 11 && (byte)Get(p, "Count") == 0x12) == 1,
            "one authoritative landing result reports stored and dropped counts");
        Deliver(cast, host, host.Peer, true);
        Deliver(hook, host, host.Peer, true);
        Deliver(hold, host, host.Peer, true);
        Check(host.Sessions == 0 && host.Pickables.Pickables.Count == 1, "duplicate actions cannot reopen a settled cast");

        SubsystemInventories.flushSyncItems();
        IPackage inventorySync = Pending().OfType<ComponentInventoryPackage>().Single();
        Check(guest.Inventory.GetSlotCount(2) == 11, "guest inventory changes only on native item delivery");
        CommonLib.WorkType = WorkType.Client;
        Deliver(inventorySync, guest, hostIdentity, false);
        Check(guest.Inventory.GetSlotCount(1) == 1 && guest.Inventory.GetSlotCount(2) == 12
            && guest.Inventory.GetSlotValue(2) == fish, "native packet delivers the real raw fish and bait cost");
        Pending().Clear();

        CommonLib.WorkType = WorkType.Server;
        host.Advance(100);
        Deliver(Input(mod, 16, sequence + 1), host, host.Peer, true);
        host.Advance(100.45);
        Deliver(Input(mod, 17, sequence + 1), host, host.Peer, true);
        Check(host.Sessions == 1, "second cast is independent");
        host.Runtime.OnEntityRemoved(host.Player.Entity);
        host.Advance(145);
        Check(host.Sessions == 0 && host.Pickables.Pickables.Count == 1, "disconnect cleans up without a reward");
    }

    private static void TestLocal(Fixture world, Assembly mod)
    {
        CommonLib.WorkType = WorkType.Local;
        world.Inventory.RemoveSlotItems(1, 1);
        world.Inventory.RemoveSlotItems(2, 4);
        world.Advance(200);
        Call(world.Runtime, "AcceptInput", world.Player, Input(mod, 16, 1), null);
        world.Advance(200.1);
        Call(world.Runtime, "AcceptInput", world.Player, Input(mod, 18, 1), null);
        Check(world.Sessions == 0, "interrupted wind-up leaves no flight");
        world.Advance(201);
        Call(world.Runtime, "AcceptInput", world.Player, Input(mod, 16, 2), null);
        world.Advance(201.65);
        Call(world.Runtime, "AcceptInput", world.Player, Input(mod, 17, 2), null);
        world.Advance(203);
        world.Advance(220);
        Call(world.Runtime, "AcceptInput", world.Player, Input(mod, 3, 2, 1), null);
        world.Advance(224.1);
        Check(world.Sessions == 0 && world.Inventory.GetSlotCount(2) == 9, "same native release and fishing loop grants one fish offline");
    }

    private static IPackage Input(Assembly mod, byte kind, uint sequence, byte count = 0)
    {
        var packet = (IPackage)Activator.CreateInstance(mod.GetType("SCIEPort.Features.Fishing.FishingCatchPackage", true));
        Put(packet, "Kind", kind);
        Put(packet, "Caster", (ushort)33);
        Put(packet, "Cell", new Point3(0, 0, 256));
        Put(packet, "Sequence", sequence);
        Put(packet, "Count", count);
        return packet;
    }

    private static void Deliver(IPackage packet, Fixture target, Client from, bool server)
    {
        using (var writer = new PackageStreamWriter())
        {
            packet.WriteData(writer);
            var copy = (IPackage)Activator.CreateInstance(packet.GetType());
            using (var reader = new PackageStreamReader(writer.Data())) copy.ReadData(reader);
            copy.From = from;
            copy.Handle(target.Project, CommonLib.Net, server);
        }
    }

    private static List<IPackage> Pending() { return (List<IPackage>)Get(CommonLib.Net, "PendingPackages"); }
    private static T Bare<T>() { return (T)FormatterServices.GetUninitializedObject(typeof(T)); }
    private static object Call(object target, string name, params object[] args)
    { return target.GetType().GetMethod(name, Members).Invoke(target, args); }

    private static object Get(object target, string name) { return Field(target, name).GetValue(target); }
    private static void Put(object target, string name, object value) { Field(target, name).SetValue(target, value); }

    private static FieldInfo Field(object target, string name)
    {
        for (Type type = target.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo field = type.GetField(name, Members) ?? type.GetField("<" + name + ">k__BackingField", Members);
            if (field != null) return field;
        }
        throw new MissingFieldException(target.GetType().FullName, name);
    }

    private static void Check(bool passed, string name)
    {
        if (!passed) throw new InvalidOperationException("Fishing native: " + name);
        s_checks++;
    }
}

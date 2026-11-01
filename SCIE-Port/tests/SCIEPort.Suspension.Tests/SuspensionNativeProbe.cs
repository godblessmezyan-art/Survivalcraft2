using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml.Linq;
using Engine;
using Game;
using Game.NetWork;
using Game.NetWork.Packages;
using GameEntitySystem;
using TemplatesDatabase;

// Exact-target isolated components: real entity load/save, inventory transfer and removal.
public static class SuspensionNativeProbe
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static int checks;
    static void Check(bool value, string message) { checks++; if (!value) throw new Exception("Suspension: " + message); }
    static void Put(object o, string name, object value)
    {
        for (var t = o.GetType(); t != null; t = t.BaseType) { var f = t.GetField(name, F); if (f != null) { f.SetValue(o, value); return; } }
        throw new Exception(name);
    }
    static object Call(object o, string name, params object[] args)
    {
        foreach (var m in o.GetType().GetMethods(F)) if (m.Name == name && m.GetParameters().Length == args.Length) return m.Invoke(o, args);
        throw new Exception(name);
    }
    static void Add(Project p, Subsystem s) { Put(s, "m_project", p); p.m_subsystems.Add(s); }
    static Entity BareEntity(Project p)
    {
        var e = (Entity)FormatterServices.GetUninitializedObject(typeof(Entity)); Put(e, "m_project", p); Put(e, "m_isAddedToProject", true); Put(e, "m_components", new List<Component>()); return e;
    }
    public static void Run(Assembly mod, XElement xml)
    {
        var blockType = mod.GetType("SCIEPort.Features.Suspension.SuspendedItemBlock", true);
        var itemType = mod.GetType("SCIEPort.Features.Suspension.ComponentSuspendedItem", true);
        var runtimeType = mod.GetType("SCIEPort.Features.Suspension.SubsystemSuspendedItem", true);
        foreach (var a in new[] { typeof(Block).Assembly, mod }) foreach (var t in a.GetTypes())
        {
            if (!t.IsSubclassOf(typeof(Block)) || t == blockType) continue;
            var f = t.GetField("Index", BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy);
            if (f != null && f.IsLiteral) Check((int)f.GetRawConstantValue() != 954, "954 vacant against native and all mod blocks");
        }
        Check(typeof(ComponentChest).IsAssignableFrom(itemType), "native chest protocol identity");
        var before = CommonLib.WorkType; var oldNet = CommonLib.Net; var oldHooks = ModsManager.ModHooks;
        var oldBlocks = (Block[])BlocksManager.Blocks.Clone();
        try
        {
            CommonLib.WorkType = WorkType.Local; ModsManager.ModHooks = new Dictionary<string, ModsManager.ModHook>();
            CommonLib.Net = (NetNode)FormatterServices.GetUninitializedObject(typeof(NetNode));
            Put(CommonLib.Net, "PendingPackages", new List<IPackage>()); CommonLib.Net.Clients = new Dictionary<int, Client>();
            var db = XmlDatabaseSerializer.LoadDatabase(xml); var gd = new GameDatabase(db);
            var p = new Project(); p.m_gameDatabase = gd; p.m_projectTemplate = db.FindDatabaseObject("Project", gd.ProjectTemplateType, true);
            var inventories = new SubsystemInventories(); Add(p, inventories);
            var blockEntities = new SubsystemBlockEntities(); Add(p, blockEntities);
            Add(p, new SubsystemPlayers());
            var info = new SubsystemGameInfo { WorldSettings = (WorldSettings)FormatterServices.GetUninitializedObject(typeof(WorldSettings)) }; Add(p, info);
            info.WorldSettings.DisableBlocks = string.Empty;
            var pickables = new SubsystemPickables(); pickables.m_subsystemGameInfo = info; Add(p, pickables);
            var runtime = (Subsystem)Activator.CreateInstance(runtimeType); Add(p, runtime);
            Put(runtime, "m_subsystemBlockEntities", blockEntities);
            var template = db.FindDatabaseObject("SCIEPortSuspendedItem", gd.EntityTemplateType, true);
            var initial = new ValuesDictionary(); initial.PopulateFromDatabaseObject(template);
            var chest = initial.GetValue<ValuesDictionary>("Chest");
            Check(chest.GetValue<int>("SlotsCount") == 1 && chest.GetValue<string>("Class") == itemType.FullName, "real merged DB overrides Chest class and slot count");
            Check(initial.GetValue<ValuesDictionary>("BlockEntity").GetValue<string>("Class") == typeof(ComponentBlockEntity).FullName, "native block-entity inheritance");
            initial.GetValue<ValuesDictionary>("BlockEntity").SetValue("Coordinates", new Point3(8, 65, 8));
            var entity = p.CreateEntity(initial); p.AddEntity(entity);
            var item = entity.FindComponent<ComponentChest>(true);
            Check(item.SlotsCount == 1 && item.Id > 0 && inventories.GetInventoryById(item.Id) == item, "real entity has registered native single-slot inventory");
            Check(blockEntities.GetBlockEntity(8, 65, 8).Entity == entity, "real block-entity coordinate registration");
            var value = Terrain.MakeBlockValue(953, 0, 173);
            BlocksManager.Blocks[953] = new GraniteBlock { BlockIndex = 953, MaxStacking = 1 };
            BlocksManager.Blocks[3] = new GraniteBlock { BlockIndex = 3, MaxStacking = 40 };
            var inventory = new ComponentInventory(); inventory.m_entity = BareEntity(p);
            inventory.m_slots.Add(new ComponentInventoryBase.Slot { Value = value, Count = 1 }); inventory.m_slots.Add(new ComponentInventoryBase.Slot());
            Check(InventorySlotWidget.HandleMoveItem(inventory, 0, item, 0, 1), "native transfer deposits artifact");
            Check(inventory.GetSlotCount(0) == 0 && item.GetSlotCount(0) == 1 && item.GetSlotValue(0) == value, "deposit conserves exact DATA/durability value");
            Check(!item.AddNetSlotItems(0, value, 1) && item.GetSlotCount(0) == 1, "full single-item slot rejects additional item");
            Check(item.RemoveNetSlotItems(0, -5) == 0 && item.GetSlotCount(0) == 1, "negative removal cannot manufacture items");
            Check(item.GetSlotCapacity(-1, value) == 0 && item.GetSlotCapacity(1, value) == 0 && item.GetSlotCapacity(0, 0) == 0, "invalid slots and air rejected");
            Check(item.GetSlotProcessCapacity(0, Terrain.MakeBlockValue(3)) == 0, "displayed item cannot be processed or used as crafting input");
            Check(InventorySlotWidget.HandleMoveItem(item, 0, inventory, 1, 1) && item.GetSlotCount(0) == 0 && inventory.GetSlotValue(1) == value, "native take-back preserves complete value");
            inventory.m_slots[0] = new ComponentInventoryBase.Slot { Value = Terrain.MakeBlockValue(3, 0, 12), Count = 40 };
            Check(InventorySlotWidget.HandleDragDrop(inventory, 0, DragMode.AllItems, item, 0, false), "native stack drag accepted");
            Check(inventory.GetSlotCount(0) == 39 && item.GetSlotCount(0) == 1, "stack drag stores one and leaves 39 with player");
            Check(!InventorySlotWidget.HandleMoveItem(inventory, 0, item, 0, 40) && inventory.GetSlotCount(0) == 39, "repeated/full transfer conserves counts");
            item.RemoveSlotItems(0, 1); item.AddSlotItems(0, value, 1);
            var saved = p.SaveEntities(new[] { entity });
            var saveXml = new XElement("Entities"); saved.Save(saveXml);
            saved = new EntityDataList(gd, XElement.Parse(saveXml.ToString()), false);
            p.RemoveEntity(entity, true);
            Check(inventories.GetInventoryById(item.Id) == null && blockEntities.GetBlockEntity(8, 65, 8) == null, "native removal releases registrations");
            var loaded = p.LoadEntities(saved); p.AddEntities(loaded);
            entity = loaded[0]; item = entity.FindComponent<ComponentChest>(true);
            Check(item.GetSlotCount(0) == 1 && item.GetSlotValue(0) == value && item.SlotsCount == 1, "native entity save/load preserves exact artifact");
            Check(entity.FindComponent<ComponentBlockEntity>(true).Coordinates == new Point3(8, 65, 8), "native save/load preserves display position");
            CommonLib.WorkType = WorkType.Client;
            Check(!item.AddNetSlotItems(0, Terrain.MakeBlockValue(3), 1) && item.RemoveNetSlotItems(0, 1) == 0 && item.GetSlotValue(0) == value, "Client cannot finalize local inventory mutations");
            var sync = new ComponentInventoryPackage(new Dictionary<IInventory, List<int>> { { item, new List<int> { 0 } } });
            using (var writer = new PackageStreamWriter())
            {
                sync.WriteData(writer); var copy = new ComponentInventoryPackage();
                using (var reader = new PackageStreamReader(writer.Data())) copy.ReadData(reader);
                item.SetSlotValue(0, new ComponentInventoryBase.Slot());
                var clientProject = (ProjectNet)FormatterServices.GetUninitializedObject(typeof(ProjectNet)); Put(clientProject, "m_subsystems", p.m_subsystems);
                copy.Handle(clientProject, CommonLib.Net, false);
                Check(item.GetSlotCount(0) == 1 && item.GetSlotValue(0) == value, "actual native inventory packet roundtrip restores client display value");
            }
            Call(runtime, "OnBlockAdded", Terrain.MakeBlockValue(954), 0, 8, 65, 8, null);
            Check(blockEntities.GetBlockEntity(8, 65, 8).Entity == entity, "Client cannot create a second block entity");
            Call(runtime, "OnBlockRemoved", Terrain.MakeBlockValue(954), 0, 8, 65, 8);
            Check(pickables.Pickables.Count == 0 && blockEntities.GetBlockEntity(8, 65, 8) == null, "Client removal never creates duplicate drops");
            CommonLib.WorkType = WorkType.Server;
            loaded = p.LoadEntities(saved); p.AddEntities(loaded); entity = loaded[0];
            item = entity.FindComponent<ComponentChest>(true);
            Call(runtime, "OnBlockRemoved", Terrain.MakeBlockValue(954), 0, 8, 65, 8);
            Call(runtime, "OnBlockRemoved", Terrain.MakeBlockValue(954), 0, 8, 65, 8);
            Check(pickables.Pickables.Count == 1 && pickables.Pickables[0].Value == value && pickables.Pickables[0].Count == 1, "Server destruction returns exactly one stored artifact, even on repeated callback");
            Check(!entity.IsAddedToProject && inventories.GetInventoryById(item.Id) == null, "destroyed display is removed and unregistered");
            Check(p.SaveEntities(p.Entities).EntitiesData.Count == 0, "destroyed display is absent from next save");
            CommonLib.WorkType = WorkType.Local;
            var miner = new ComponentMiner { ComponentPlayer = new ComponentPlayer { PlayerGuid = Guid.NewGuid() } };
            Call(runtime, "OnBlockAdded", Terrain.MakeBlockValue(954), 0, 10, 65, 10, miner);
            var placed = blockEntities.GetBlockEntity(10, 65, 10);
            Check(placed != null && placed.Entity.FindComponent<ComponentChest>(true).SlotsCount == 1 && placed.Owner == miner.ComponentPlayer.PlayerGuid, "actual player placement creates correct template and owner");
            Call(runtime, "OnBlockAdded", Terrain.MakeBlockValue(954), 0, 10, 65, 10, miner);
            Check(p.Entities.Count == 1, "repeated placement callback does not create another inventory");
            Call(runtime, "OnBlockRemoved", Terrain.MakeBlockValue(954), 0, 10, 65, 10);
            Check(pickables.Pickables.Count == 1 && p.Entities.Count == 0, "removing an empty display creates no content drops");
            var block = (Block)Activator.CreateInstance(blockType);
            Check(block.GetCustomCollisionBoxes(null, 0).Length == 0 && block.GetCustomInteractionBoxes(null, 0).Length == 1, "walk-through but selectable");
            var mesh = new BlockMesh(); blockType.GetMethod("BuildMarker", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { mesh });
            Check(mesh.Indices.Count / 3 == 72, "empty-marker mesh bounded to 72 triangles");
            var displayMatrix = runtimeType.GetMethod("DisplayMatrix", BindingFlags.Static | BindingFlags.NonPublic);
            var point = new Point3(-20, 50, 31); var center = new Vector3(-19.5f, 50.5f, 31.5f);
            for (int i = 0; i <= 300; i++)
            {
                var m = (Matrix)displayMatrix.Invoke(null, new object[] { point, i / 10f, Vector3.Zero });
                Check(Math.Abs(m.Translation.X - center.X) < .001f && Math.Abs(m.Translation.Z - center.Z) < .001f && Math.Abs(m.Translation.Y - center.Y) <= .0651f, "rotation never drifts from selected cell");
            }
            var beforeWrap = (Matrix)displayMatrix.Invoke(null, new object[] { point, 119.999f, Vector3.Zero });
            var afterWrap = (Matrix)displayMatrix.Invoke(null, new object[] { point, .001f, Vector3.Zero });
            Check(Vector3.Distance(beforeWrap.Translation, afterWrap.Translation) < .001f && Vector3.Distance(beforeWrap.Forward, afterWrap.Forward) < .001f, "long-running timer wrap has no rotation or hover jump");
            runtime.Dispose();
        }
        finally
        {
            CommonLib.WorkType = WorkType.Local; SubsystemInventories.flushSyncItems(); CommonLib.WorkType = before;
            CommonLib.Net = oldNet; ModsManager.ModHooks = oldHooks; Array.Copy(oldBlocks, BlocksManager.Blocks, oldBlocks.Length);
        }
        Console.WriteLine("Suspension target-native database/entity/save/inventory/Client-Server/drop/pose: " + checks + " checks passed (isolated fixtures, not gameplay).");
    }
}

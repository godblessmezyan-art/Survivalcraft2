using System;
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

// Exact-target, in-memory entities and inventories. Never loads a player world.
public static class HomeNativeProbe
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    static int checks;
    static void Check(bool ok, string why) { checks++; if (!ok) throw new Exception("Home native: " + why); }
    static void Put(object obj, string name, object value)
    {
        for (Type t = obj.GetType(); t != null; t = t.BaseType)
        { FieldInfo field = t.GetField(name, F); if (field != null) { field.SetValue(obj, value); return; } }
        throw new Exception(name);
    }
    static void Add(Project project, Subsystem subsystem) { Put(subsystem, "m_project", project); project.m_subsystems.Add(subsystem); }
    static Entity BareEntity(Project project)
    {
        var entity = (Entity)FormatterServices.GetUninitializedObject(typeof(Entity));
        Put(entity, "m_project", project); Put(entity, "m_components", new List<Component>()); return entity;
    }
    static bool Contains(BoundingBox[] boxes, Vector3 p)
    {
        foreach (BoundingBox b in boxes) if (p.X >= b.Min.X-.0001f && p.X <= b.Max.X+.0001f
            && p.Y >= b.Min.Y-.0001f && p.Y <= b.Max.Y+.0001f && p.Z >= b.Min.Z-.0001f && p.Z <= b.Max.Z+.0001f) return true;
        return false;
    }
    static void Geometry(Assembly mod)
    {
        Type geometry = mod.GetType("SCIEPort.Features.Home.HomeExtensionGeometry", true);
        MethodInfo offset = geometry.GetMethod("Offset", BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo getBoxes = geometry.GetMethod("Boxes", BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo cell = geometry.GetMethod("Cell", BindingFlags.Static | BindingFlags.NonPublic);
        foreach (int id in new[] { 965, 966 }) for (int rotation = 0; rotation < 4; rotation++)
        {
            var offsets = new List<Point3>(); var boxes = new List<BoundingBox[]>();
            for (int part = 0; part < 4; part++)
            {
                Point3 d = (Point3)offset.Invoke(null, new object[] { rotation, part });
                Check(!offsets.Contains(d), "footprint cells stay distinct in every direction"); offsets.Add(d);
                var b = (BoundingBox[])getBoxes.Invoke(null, new object[] { id, rotation, part }); boxes.Add(b);
                Check(b.Length > 0, "each footprint cell has actual collision");
                foreach (BoundingBox bound in b)
                    Check(bound.Min.X >= -.0001f && bound.Min.Z >= -.0001f && bound.Min.Y >= 0 && bound.Max.X <= 1.0001f
                        && bound.Max.Z <= 1.0001f && bound.Max.Y <= 1 && bound.Min.X < bound.Max.X && bound.Min.Y < bound.Max.Y && bound.Min.Z < bound.Max.Z,
                        "collision clipped inside its own cell");
            }
            Vector3[] sample = id == 966 ? new[] { new Vector3(.2f,.62f,.2f),new Vector3(1.8f,.62f,.2f),new Vector3(.2f,.62f,1.8f),new Vector3(1.8f,.9f,1.9f) }
                : new[] { new Vector3(.6f,.75f,.5f),new Vector3(1.4f,.75f,1.5f),new Vector3(.3f,.42f,1.1f),new Vector3(1.7f,.42f,1.1f) };
            Matrix turn = Matrix.CreateTranslation(-.5f,0,-.5f)*Matrix.CreateRotationY(rotation*MathUtils.PI/2)*Matrix.CreateTranslation(.5f,0,.5f);
            foreach (Vector3 p in sample)
            {
                Vector3 rotated = Vector3.Transform(p, turn); bool found = false;
                for (int i = 0; i < 4; i++) if (Contains(boxes[i], rotated-new Vector3(offsets[i].X,0,offsets[i].Z))) found = true;
                Check(found, "mattress/headboard/table/chair contacts survive all four rotations");
            }
        }
        object[] args = { new Point3(int.MaxValue,64,0),0,1,new Point3() };
        Check(!(bool)cell.Invoke(null,args), "positive coordinate overflow rejected");
        args = new object[] { new Point3(int.MinValue,64,0),2,1,new Point3() };
        Check(!(bool)cell.Invoke(null,args), "negative coordinate overflow rejected");
    }
    public static void Run(Assembly mod, XElement xml)
    {
        Geometry(mod);
        WorkType previous = CommonLib.WorkType; var oldNet = CommonLib.Net; var oldHooks = ModsManager.ModHooks;
        var oldBlocks = (Block[])BlocksManager.Blocks.Clone();
        try
        {
            CommonLib.WorkType = WorkType.Local; ModsManager.ModHooks = new Dictionary<string, ModsManager.ModHook>();
            CommonLib.Net = (NetNode)FormatterServices.GetUninitializedObject(typeof(NetNode));
            Put(CommonLib.Net,"PendingPackages",new List<IPackage>()); CommonLib.Net.Clients=new Dictionary<int,Client>();
            BlocksManager.Blocks[3] = new GraniteBlock { BlockIndex=3, MaxStacking=40 };
            var db=XmlDatabaseSerializer.LoadDatabase(xml); var gd=new GameDatabase(db); var project=new Project();
            project.m_gameDatabase=gd; project.m_projectTemplate=db.FindDatabaseObject("Project",gd.ProjectTemplateType,true);
            var inventories=new SubsystemInventories(); Add(project,inventories);
            var entities=new SubsystemBlockEntities(); Add(project,entities); Add(project,new SubsystemPlayers());
            var info=new SubsystemGameInfo { WorldSettings=(WorldSettings)FormatterServices.GetUninitializedObject(typeof(WorldSettings)) };
            info.WorldSettings.DisableBlocks=string.Empty; Add(project,info);
            var pickables=new SubsystemPickables { m_subsystemGameInfo=info }; Add(project,pickables);
            var runtime=(Subsystem)Activator.CreateInstance(mod.GetType("SCIEPort.Features.Home.SubsystemHomeFurniture",true)); Add(project,runtime); Put(runtime,"m_subsystemBlockEntities",entities);
            var initial=new ValuesDictionary(); initial.PopulateFromDatabaseObject(db.FindDatabaseObject("SCIEPortHomeStorage",gd.EntityTemplateType,true));
            Check(initial.GetValue<ValuesDictionary>("Chest").GetValue<int>("SlotsCount")==16,"merged template retains all sixteen physical slots");
            initial.GetValue<ValuesDictionary>("BlockEntity").SetValue("Coordinates",new Point3(8,65,8));
            Entity entity=project.CreateEntity(initial); project.AddEntity(entity); var home=entity.FindComponent<ComponentChest>(true);
            Check(home.GetType().FullName=="SCIEPort.Features.Home.ComponentHomeStorage" && home.SlotsCount==16,"real template loads the owned inventory");
            // Raw fixture initialization represents a V1 sixteen-slot save, before the new cap.
            for(int i=0;i<16;i++) { home.m_slots[i].Value=Terrain.MakeBlockValue(3,0,100+i); home.m_slots[i].Count=i+1; }
            var saved=project.SaveEntities(new[]{entity}); var saveXml=new XElement("Entities"); saved.Save(saveXml);
            saved=new EntityDataList(gd,XElement.Parse(saveXml.ToString()),false); project.RemoveEntity(entity,true);
            var loaded=project.LoadEntities(saved); project.AddEntities(loaded); entity=loaded[0]; home=entity.FindComponent<ComponentChest>(true);
            for(int i=0;i<16;i++) Check(home.GetSlotCount(i)==i+1 && home.GetSlotValue(i)==Terrain.MakeBlockValue(3,0,100+i),"native reload keeps every legacy value/count");
            int value=home.GetSlotValue(15);
            for(int i=0;i<16;i++) Check(home.GetSlotCapacity(i,value)==(i<8?40:0),"exactly eight deposit slots");
            for(int i=8;i<16;i++)
            {
                Check(!home.AddNetSlotItems(i,home.GetSlotValue(i),1) && home.GetSlotCount(i)==i+1,"occupied legacy slot cannot grow");
                Check(home.RemoveNetSlotItems(i,-5)==0 && home.GetSlotCount(i)==i+1,"negative withdrawal cannot grow a legacy stack");
                Check(home.GetSlotProcessCapacity(i,value)==0,"legacy slots cannot be processing targets");
            }
            Check(!home.AddNetSlotItems(0,home.GetSlotValue(0),int.MaxValue),"deposit arithmetic cannot overflow");
            var player=new ComponentInventory(); player.m_entity=BareEntity(project);
            for(int i=0;i<20;i++) player.m_slots.Add(new ComponentInventoryBase.Slot());
            player.m_slots[0].Value=value; player.m_slots[0].Count=40;
            foreach(WorkType mode in new[]{WorkType.Local,WorkType.Server})
            {
                CommonLib.WorkType=mode;
                Check(!InventorySlotWidget.HandleMoveItem(player,0,home,15,1),"native move rejects deposit into occupied overflow on host");
                Check(!InventorySlotWidget.HandleDragDrop(player,0,DragMode.AllItems,home,15,false),"native drag cannot bypass the host capacity limit");
                Check(player.GetSlotCount(0)==40 && home.GetSlotCount(15)==16,"rejected native writes conserve both inventories");
            }
            CommonLib.WorkType=WorkType.Local;
            Check(!InventorySlotWidget.HandleMoveItem(home,15,player,0,16) && home.GetSlotCount(15)==16,"full backpack target does not consume old items");
            player.m_slots[0].Count=39;
            Check(InventorySlotWidget.HandleMoveItem(home,15,player,0,16) && player.GetSlotCount(0)==40 && home.GetSlotCount(15)==15,"partial legacy extraction conserves remainder");
            Check(InventorySlotWidget.HandleMoveItem(home,15,player,1,40) && player.GetSlotCount(1)==15 && home.GetSlotCount(15)==0,"legacy slot can be completely drained");
            Check(!home.AddNetSlotItems(15,value,1),"drained overflow cannot be refilled");
            CommonLib.WorkType=WorkType.Server;
            home.SetSlotValue(15,new ComponentInventoryBase.Slot { Value=value,Count=1 });
            Check(home.GetSlotCount(15)==0,"server direct snapshot cannot manufacture overflow");
            CommonLib.WorkType=WorkType.Client;
            var sync=new ComponentInventoryPackage(new Dictionary<IInventory,List<int>>{{home,new List<int>{14}}});
            using(var writer=new PackageStreamWriter())
            {
                sync.WriteData(writer); var copy=new ComponentInventoryPackage(); using(var reader=new PackageStreamReader(writer.Data())) copy.ReadData(reader);
                home.SetSlotValue(14,new ComponentInventoryBase.Slot());
                var client=(ProjectNet)FormatterServices.GetUninitializedObject(typeof(ProjectNet)); Put(client,"m_subsystems",project.m_subsystems); copy.Handle(client,CommonLib.Net,false);
                Check(home.GetSlotCount(14)==15 && home.GetSlotValue(14)==Terrain.MakeBlockValue(3,0,114),"real native sync restores take-only legacy data on client");
            }
            CommonLib.WorkType=WorkType.Local;
            saved=project.SaveEntities(new[]{entity}); saveXml=new XElement("Entities"); saved.Save(saveXml); saved=new EntityDataList(gd,XElement.Parse(saveXml.ToString()),false);
            project.RemoveEntity(entity,true); loaded=project.LoadEntities(saved); project.AddEntities(loaded); entity=loaded[0]; home=entity.FindComponent<ComponentChest>(true);
            Check(home.SlotsCount==16 && home.GetSlotCount(15)==0 && home.GetSlotCount(14)==15,"save/reload after extraction preserves both drained and remaining overflow");
            for(int i=8;i<16;i++)
            {
                if(home.GetSlotCount(i)>0) { int target=ComponentInventoryBase.FindAcquireSlotForItem(player,home.GetSlotValue(i)); Check(target>=0 && InventorySlotWidget.HandleMoveItem(home,i,player,target,40),"all remaining legacy stacks can be recovered"); }
                Check(home.GetSlotCount(i)==0 && !home.AddNetSlotItems(i,value,1),"all drained legacy slots remain take-only");
            }
            Check(home.AddNetSlotItems(0,home.GetSlotValue(0),1),"ordinary deposit still works in first half");
            var vanilla=new ComponentChest(); for(int i=0;i<16;i++) vanilla.m_slots.Add(new ComponentInventoryBase.Slot());
            Check(vanilla.AddNetSlotItems(15,value,40),"original container capacity is unaffected");
            for(int i=0;i<16;i++) { home.m_slots[i].Value=Terrain.MakeBlockValue(3,0,100+i); home.m_slots[i].Count=i+1; }
            CommonLib.WorkType=WorkType.Server;
            MethodInfo removed=runtime.GetType().GetMethod("OnBlockRemoved",new[]{typeof(int),typeof(int),typeof(int),typeof(int),typeof(int)});
            removed.Invoke(runtime,new object[]{Terrain.MakeBlockValue(967),0,8,65,8}); removed.Invoke(runtime,new object[]{Terrain.MakeBlockValue(967),0,8,65,8});
            int total=0; foreach(Pickable item in pickables.Pickables) total+=item.Count;
            Check(total==136 && pickables.Pickables.Count==16,"destruction drops all sixteen stored stacks once, including overflow");
            Check(entities.GetBlockEntity(8,65,8)==null && !entity.IsAddedToProject,"destroyed storage registrations are released");
            runtime.Dispose();
        }
        finally
        {
            CommonLib.WorkType=WorkType.Local; SubsystemInventories.flushSyncItems(); CommonLib.WorkType=previous;
            CommonLib.Net=oldNet; ModsManager.ModHooks=oldHooks; Array.Copy(oldBlocks,BlocksManager.Blocks,oldBlocks.Length);
        }
        Console.WriteLine("Home target-native entity/save/legacy-capacity/transfer/Client-Server/drop/footprint: "+checks+" checks passed (isolated fixtures, not gameplay).");
    }
}

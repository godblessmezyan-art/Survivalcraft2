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
        { FieldInfo field = t.GetField(name, F) ?? t.GetField("<"+name+">k__BackingField", F); if (field != null) { field.SetValue(obj, value); return; } }
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
    static void SleepAndFire(Assembly mod, Project project, Subsystem runtime, SubsystemGameInfo info)
    {
        // Exact native regression: the original zero-length array crashes on a miss.
        var ray=new Ray3(new Vector3(.5f,-1,.5f),Vector3.UnitY);int boxIndex;BoundingBox hitBox;
        bool nativeBug=false;try{new EmptyCollisionBlock().Raycast(ray,null,0,false,out boxIndex,out hitBox);}catch(IndexOutOfRangeException){nativeBug=true;}
        Check(nativeBug,"negative control reproduces native empty-box raycast crash");
        foreach(string typeName in new[]{"HomeCurtainBlock","HomeUpperBlock","HomeDiningPartBlock","HomeBedPartBlock"})
        {
            var b=(Block)Activator.CreateInstance(mod.GetType("SCIEPort.Features.Home."+typeName,true));
            int id=typeName=="HomeCurtainBlock"?962:typeName=="HomeUpperBlock"?963:typeName=="HomeDiningPartBlock"?969:970;
            b.BlockIndex=id;BlocksManager.Blocks[id]=b;
            if(id==962)
            {
                var dataBoxes=new Dictionary<int,BoundingBox[]>();
                MethodInfo shape=b.GetType().BaseType.GetMethod("Boxes",BindingFlags.Static|BindingFlags.NonPublic);
                for(int d=0;d<8;d++)dataBoxes[d]=(BoundingBox[])shape.Invoke(null,new object[]{962,0,d&3,false,(d&4)!=0});
                Put(b,"collisions",dataBoxes);
            }
            foreach(int rotation in new[]{0,1,2,3})foreach(int state in new[]{0,4})
            {
                int value=Terrain.MakeBlockValue(id,0,rotation|state|(id==963?64:0));
                // Occupancy DATA0 is invalid; valid dining/bed DATA4 has real collision.
                b.Raycast(ray,null,value,false,out boxIndex,out hitBox);
                Check(b.GetCustomCollisionBoxes(null,value).Length>0,"native raycast safely handles curtain/upper/proxy DATA");
            }
        }
        var suspended=(Block)Activator.CreateInstance(mod.GetType("SCIEPort.Features.Suspension.SuspendedItemBlock",true));
        Check(!suspended.Raycast(ray,null,954,false,out boxIndex,out hitBox).HasValue&&!suspended.IsCollidable_(954),"suspended display stays traversable and native rays do not crash");
        var fireplace=(Block)Activator.CreateInstance(mod.GetType("SCIEPort.Features.Home.HomeFireplaceBlock",true));
        fireplace.BlockIndex=964; BlocksManager.Blocks[964]=fireplace;
        var collisionData=new Dictionary<int,BoundingBox[]>();
        MethodInfo boxes=mod.GetType("SCIEPort.Features.Home.HomeExtensionGeometry",true).GetMethod("Boxes",BindingFlags.Static|BindingFlags.NonPublic);
        for(int d=0;d<8;d++)collisionData[d]=(BoundingBox[])boxes.Invoke(null,new object[]{964,d&3,0});
        Put(fireplace,"collisions",collisionData);
        foreach(int rotation in new[]{0,1,2,3})
        {
            Check(SubsystemMetersBlockBehavior.GetHeat(Terrain.MakeBlockValue(964,0,rotation))==0,"native meter sees extinguished fireplace as cold");
            Check(SubsystemMetersBlockBehavior.GetHeat(Terrain.MakeBlockValue(964,0,rotation|4))==1,"native meter sees lit fireplace as a full warmth source");
            Check(fireplace.GetEmittedLightAmount(Terrain.MakeBlockValue(964,0,rotation|4))==8,"lit fireplace keeps native light");
            Check(fireplace.GetEmittedLightAmount(Terrain.MakeBlockValue(964,0,rotation))==0,"extinguishing removes light");
        }
        MethodInfo prepare=fireplace.GetType().GetMethod("PrepareSection",F);
        var assetRoot=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(mod.Location),"Assets/Models");
        foreach(int state in new[]{0,4})
        {
            string path=System.IO.Path.Combine(assetRoot,state==0?"HomeFireplace.dae":"HomeFireplaceOn.dae");
            Engine.Media.ModelData imported;using(var stream=System.IO.File.OpenRead(path))imported=Engine.Media.ModelData.Load(stream);
            var dae=XElement.Load(path);XNamespace ns="http://www.collada.org/2005/11/COLLADASchema";
            var geometry=new List<XElement>(dae.Element(ns+"library_geometries").Elements(ns+"geometry"));
            Check(imported.Meshes.Count==geometry.Count&&geometry.Count==(state==0?21:36),"target Engine imports expected fireplace section order/count");
            for(int index=0;index<geometry.Count;index++)
            {
                string name=(string)geometry[index].Attribute("name");
                var coordinates=geometry[index].Element(ns+"mesh").Element(ns+"source").Element(ns+"float_array").Value.Split((char[])null,StringSplitOptions.RemoveEmptyEntries);
                Vector3 minimum=new Vector3(float.MaxValue),maximum=new Vector3(float.MinValue);
                for(int vindex=0;vindex<coordinates.Length;vindex+=3)
                {
                    var position=new Vector3(float.Parse(coordinates[vindex],System.Globalization.CultureInfo.InvariantCulture),float.Parse(coordinates[vindex+1],System.Globalization.CultureInfo.InvariantCulture),float.Parse(coordinates[vindex+2],System.Globalization.CultureInfo.InvariantCulture));
                    minimum=Vector3.Min(minimum,position);maximum=Vector3.Max(maximum,position);
                }
                Check(Vector3.DistanceSquared(minimum,imported.Meshes[index].BoundingBox.Min)<.000001f
                    &&Vector3.DistanceSquared(maximum,imported.Meshes[index].BoundingBox.Max)<.000001f,"actual Engine mesh order matches each named source geometry's bounds");
                bool warm=name.Contains("flame")||name.Contains("ember");
                Check(warm==(state==4&&index>=19&&index<=33),"fixed material ordinals match actual packaged DAE geometry");
                var mesh=new BlockMesh();mesh.Vertices.Add(new BlockMeshVertex{Color=Color.White,TextureCoordinates=new Vector2(.3f,.3f)});
                prepare.Invoke(fireplace,new object[]{state,index,mesh}); var v=mesh.Vertices.Array[0];
                if(!warm) Check(v.Color==Color.White&&!v.IsEmissive&&v.TextureCoordinates==new Vector2(.3f,.3f),"housing/control/off model retains original material");
                else Check(v.IsEmissive&&v.Color.R>v.Color.G&&v.Color.G>v.Color.B&&v.TextureCoordinates==new Vector2(.125f,.875f),"flame/core/ember uses neutral UV and warm emissive color");
            }
        }
        using(var cells=new Terrain())
        {
            cells.AllocateChunk(0,0).State=TerrainChunkState.Valid;
            var terrain=(SubsystemTerrain)FormatterServices.GetUninitializedObject(typeof(SubsystemTerrain));Put(terrain,"Terrain",cells);
            Put(runtime,"SubsystemTerrain",terrain);
            BlocksManager.Blocks[0]=new AirBlock();
            BlocksManager.Blocks[3]=new GraniteBlock{BlockIndex=3,IsCollidable=true};
            BlocksManager.Blocks[966]=new GraniteBlock{BlockIndex=966,SleepSuitability=1,IsCollidable=true};
            BlocksManager.Blocks[970]=new GraniteBlock{BlockIndex=970,SleepSuitability=1,IsCollidable=true};
            for(int x=4;x<=12;x++)for(int z=4;z<=12;z++)cells.SetCellValueFast(x,68,z,3);
            // Each shelter ray encounters one of the previously empty-box blocks
            // before reaching the roof. This reproduces the bed's real CanSleep path.
            BlocksManager.Blocks[954]=suspended;
            cells.SetCellValueFast(8,67,8,Terrain.MakeBlockValue(962));
            cells.SetCellValueFast(9,67,8,Terrain.MakeBlockValue(963,0,64));
            cells.SetCellValueFast(8,67,9,Terrain.MakeBlockValue(954));
            Entity entity=BareEntity(project);Put(entity,"m_isAddedToProject",true);
            var p=new ComponentPlayer();p.m_entity=entity;
            p.ComponentBody=new ComponentBody();p.ComponentBody.m_entity=entity;
            p.ComponentMiner=new ComponentMiner();p.ComponentMiner.m_entity=entity;
            var held=new ComponentInventory();held.m_slots.Add(new ComponentInventoryBase.Slot());p.ComponentMiner.Inventory=held;
            p.ComponentHealth=new ComponentHealth();p.ComponentHealth.m_entity=entity;p.ComponentHealth.Health=1;
            p.ComponentVitalStats=new ComponentVitalStats();p.ComponentVitalStats.Sleep=.5f;
            p.ComponentSleep=new ComponentSleep();p.ComponentSleep.m_entity=entity;
            p.m_subsystemPlayerStats=new SubsystemPlayerStats();
            var data=(PlayerData)FormatterServices.GetUninitializedObject(typeof(PlayerData));
            data.m_stateMachine=new StateMachine();data.m_stateMachine.m_currentState=new StateMachine.State{Name="Playing"};p.PlayerData=data;
            var sleep=p.ComponentSleep;sleep.m_componentPlayer=p;sleep.m_subsystemTerrain=terrain;sleep.m_subsystemGameInfo=info;
            Put(entity,"m_components",new List<Component>{p,p.ComponentHealth,p.ComponentBody,p.ComponentSleep,p.ComponentVitalStats});
            var players=project.FindSubsystem<SubsystemPlayers>(true);players.m_componentPlayers.Add(p);
            var loader=(ModLoader)Activator.CreateInstance(mod.GetType("SCIEPort.Mod.SCIEPortLoader",true));
            MethodInfo comfort=runtime.GetType().GetMethod("UpdateBedInsulation",F);
            var early=(IUpdateable)Activator.CreateInstance(runtime.GetType().GetNestedType("BedInsulationUpdate",BindingFlags.NonPublic),F,null,new object[]{runtime},null);
            Check(early.UpdateOrder<sleep.UpdateOrder,"bed comfort is scheduled before native default updates, including accelerated substeps");
            MethodInfo attempt=runtime.GetType().GetMethod("TrySleep",F);
            MethodInfo offset=mod.GetType("SCIEPort.Features.Home.HomeExtensionGeometry",true).GetMethod("Offset",BindingFlags.Static|BindingFlags.NonPublic);
            foreach(WorkType role in new[]{WorkType.Local,WorkType.Server})
            {
                CommonLib.WorkType=role;
                for(int rotation=0;rotation<4;rotation++)for(int part=0;part<4;part++)
                {
                    for(int x=7;x<=9;x++)for(int z=7;z<=9;z++)cells.SetCellValueFast(x,64,z,0);
                    for(int n=0;n<4;n++)
                    {
                        Point3 relative=(Point3)offset.Invoke(null,new object[]{rotation,n});
                        cells.SetCellValueFast(8+relative.X,64,8+relative.Z,Terrain.MakeBlockValue(n==0?966:970,0,rotation|(n<<2)));
                    }
                    Point3 d=(Point3)offset.Invoke(null,new object[]{rotation,part});
                    p.ComponentBody.Position=new Vector3(8.5f+d.X,64.65f,8.5f+d.Z);
                    p.ComponentBody.StandingOnValue=Terrain.MakeBlockValue(part==0?966:970);
                    attempt.Invoke(runtime,new object[]{p,new Point3(8,64,8),rotation,null});
                    Check(sleep.IsSleeping,"native Local/Server sleep starts on all four bed cells and rotations");
                    double? started=sleep.m_sleepStartTime;
                    attempt.Invoke(runtime,new object[]{p,new Point3(8,64,8),rotation,null});
                    Check(sleep.m_sleepStartTime==started,"duplicate queued sleep tap is harmless");
                    foreach(float ambient in new[]{-1000f,1000f})
                    {
                        p.ComponentVitalStats.Temperature=ambient<0?0:24;p.ComponentVitalStats.m_environmentTemperature=ambient;
                        early.Update(.05f);comfort.Invoke(runtime,null);
                        Check(p.ComponentVitalStats.Temperature==12&&p.ComponentVitalStats.m_environmentTemperature==ambient,"sleep on each bed cell keeps comfort at extreme temperatures without changing native ambient cache");
                    }
                    foreach(int causeId in new[]{25,26,35})
                    { bool skip;loader.OnCreatureInjure(p.ComponentHealth,.05f,null,false,LanguageControl.Get(ComponentVitalStats.fName,causeId),out skip);Check(skip,"only native cold/heat injuries are blocked during bed sleep"); }
                    bool otherSkip;loader.OnCreatureInjure(p.ComponentHealth,.1f,null,false,"lava",out otherSkip);Check(!otherSkip,"bed is not general environmental invulnerability");
                    sleep.NetWakeUp();Check(!sleep.IsSleeping,"native wake clears sleeping state");
                    p.ComponentVitalStats.Temperature=0;early.Update(.05f);
                    Check(p.ComponentVitalStats.Temperature==0,"waking immediately releases bed temperature override");
                    loader.OnCreatureInjure(p.ComponentHealth,.05f,null,false,LanguageControl.Get(ComponentVitalStats.fName,25),out otherSkip);Check(!otherSkip,"cold injury is native again after waking");
                }
                p.ComponentBody.Position=new Vector3(8.5f,64.65f,8.5f);
                sleep.m_subsystemTerrain=null; // Inject a failure from inside native CanSleep.
                bool failed=false;try{string reason;sleep.CanSleep(out reason);}catch(NullReferenceException){failed=true;}
                Check(failed,"injected exception reaches native shelter validation");
                attempt.Invoke(runtime,new object[]{p,new Point3(8,64,8),0,null});
                Check(!sleep.IsSleeping,"native validation exception stays out of the update loop");sleep.m_subsystemTerrain=terrain;
                p.ComponentHealth.Health=0;attempt.Invoke(runtime,new object[]{p,new Point3(8,64,8),0,null});
                Check(!sleep.IsSleeping,"queued tap after death does not start sleep");p.ComponentHealth.Health=1;
            }
            sleep.NetSleep(true);cells.SetCellValueFast(8,64,8,Terrain.MakeBlockValue(966));p.ComponentVitalStats.Temperature=0;
            cells.SetCellValueFast(8,64,8,0);early.Update(.05f);Check(p.ComponentVitalStats.Temperature==0,"removing bed immediately ends insulation");
            cells.SetCellValueFast(8,64,8,Terrain.MakeBlockValue(966));
            CommonLib.WorkType=WorkType.Client;Put(data,"m_isMainPlayer",true);early.Update(.05f);Check(p.ComponentVitalStats.Temperature==12,"ordinary client applies comfort to its own sleeping player");
            Put(data,"m_isMainPlayer",false);p.ComponentVitalStats.Temperature=0;early.Update(.05f);Check(p.ComponentVitalStats.Temperature==0,"client never edits remote player's temperature");
            sleep.NetWakeUp();players.m_componentPlayers.Remove(p);
            CommonLib.WorkType=WorkType.Local;
            // An enclosed cold fixture avoids weather services and exercises the actual
            // temperature solver, rather than only checking our GetHeat override.
            for(int x=7;x<=10;x++)for(int y=64;y<=66;y++)for(int z=7;z<=9;z++)cells.SetCellValueFast(x,y,z,3);
            cells.SetCellValueFast(8,65,8,0);cells.SetCellValueFast(9,65,8,Terrain.MakeBlockValue(964));
            cells.GetChunkAtCell(8,8).SetTopHeightFast(8,8,68);
            var meter=new SubsystemMetersBlockBehavior();Put(meter,"SubsystemTerrain",terrain);
            float cold,hot,cooled,flux;meter.CalculateTemperature(8,65,8,12,0,out cold,out flux);
            cells.SetCellValueFast(9,65,8,Terrain.MakeBlockValue(964,0,4));
            meter.CalculateTemperature(8,65,8,12,0,out hot,out flux);
            cells.SetCellValueFast(9,65,8,Terrain.MakeBlockValue(964));
            meter.CalculateTemperature(8,65,8,12,0,out cooled,out flux);
            Check(hot>cold+1&&Math.Abs(cooled-cold)<.001f,"native temperature rises beside lit fireplace and returns after extinguishing");
        }
    }
    sealed class EmptyCollisionBlock : GraniteBlock
    {
        public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain,int value) { return new BoundingBox[0]; }
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
            SleepAndFire(mod,project,runtime,info);
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

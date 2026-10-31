using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml.Linq;
using TemplatesDatabase;
using Engine;
using Game;
using Game.NetWork;
public static class WorldCutNativeProbe
{
    private static int checks;
    private static void Check(bool value,string text) { checks++;if(!value)throw new Exception(text); }
    private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
    private static void Put(object value,string field,object data) { value.GetType().GetField(field,Flags).SetValue(value,data); }
    private static object Get(object value,string field) { return value.GetType().GetField(field,Flags).GetValue(value); }
    public static void Run(Assembly mod)
    {
        Type type=mod.GetType("SCIEPort.Features.AbyssReaper.WorldCutPackage",true);
        int minBytes=int.MaxValue,maxBytes=0;
        for(int i=0;i<80;i++)
        {
            var packet=(IPackage)Activator.CreateInstance(type);Check(packet.ID==247,"dedicated packet ID");
            Put(packet,"Kind",(byte)(i%9));Put(packet,"Reason",(byte)(i%10));Put(packet,"Caster",(ushort)(i+100));Put(packet,"Sequence",i>=48?uint.MaxValue-(uint)i:(uint)(i+1));
            Put(packet,"Origin",i>=48?new Vector3((i-48)*31249.5f,63.375f,-(i-48)*31249.5f):new Vector3(i*.5f,64,-i*.25f));Put(packet,"Direction",i>=48?new Vector3(.6f,0,.8f):Vector3.UnitZ);Put(packet,"Distance",i>=48?25-(i-48)*.3333333f:i*.5f);Put(packet,"Cooldown",10-i*.125f);
            var writer=new PackageStreamWriter();packet.WriteData(writer);byte[] bytes=writer.Data();
            minBytes=Math.Min(minBytes,bytes.Length);maxBytes=Math.Max(maxBytes,bytes.Length);
            Check(bytes.Length<=64,"bounded real native packet layout");
            var decoded=(IPackage)Activator.CreateInstance(type);decoded.ReadData(new PackageStreamReader(bytes));
            foreach(string field in new[]{"Kind","Reason","Caster","Sequence","Origin","Direction","Distance","Cooldown"})Check(Get(packet,field).Equals(Get(decoded,field)),"native packet roundtrip "+field);
        }
        Console.WriteLine("WorldCut actual native packet bytes for 80 fixtures: "+minBytes+".."+maxBytes);
        int ids=0;
        foreach(Type native in typeof(IPackage).Assembly.GetTypes())
        {
            if(native.IsAbstract || !native.IsClass || !typeof(IPackage).IsAssignableFrom(native))continue;
            try { var packet=(IPackage)FormatterServices.GetUninitializedObject(native);Check(packet.ID!=247,"native packet collision "+native.Name);ids++; }
            catch(SerializationException) { }
        }
        Check(ids>=30,"complete native package surface");
        // Real native hit-test transforms without fonts, textures or a display/device.
        var root=new CanvasWidget { Size=new Vector2(640,480),IsHitTestVisible=false };
        var area=new CanvasWidget { Size=new Vector2(64,62),IsHitTestVisible=false };
        var click=new ClickableWidget { HorizontalAlignment=WidgetAlignment.Stretch,VerticalAlignment=WidgetAlignment.Stretch,IsHitTestVisible=true };
        area.Children.Add(click);root.Children.Add(area);root.SetWidgetPosition(area,new Vector2(174,38));
        root.Measure(new Vector2(640,480));root.Arrange(Vector2.Zero,new Vector2(640,480));
        Check(click.ActualSize.X==64 && click.ActualSize.Y==62,"native touch slot minimum size");
        Vector2 at=click.WidgetToScreen(new Vector2(32,31));
        Check(ReferenceEquals(Widget.HitTestGlobal(root,at,w=>w.IsHitTestVisible),click),"slot two owns only its hit region through transparent parent");
        Vector2 back=click.ScreenToWidget(at);Check((back-new Vector2(32,31)).LengthSquared()<.0001f,"touch screen/widget coordinate roundtrip");
        Check(Widget.HitTestGlobal(root,new Vector2(20,20),w=>w.IsHitTestVisible)==null,"world touch outside slot is not swallowed");
        // Verify the actual mod circular widget under the native UI layout transform.
        var circle=(Widget)Activator.CreateInstance(mod.GetType("SCIEPort.Features.AbyssReaper.AbyssSkillHitTarget",true),true);
        circle.HorizontalAlignment=WidgetAlignment.Stretch;circle.VerticalAlignment=WidgetAlignment.Stretch;
        area.Children.Clear();area.Children.Add(circle);
        root.Measure(new Vector2(640,480));root.Arrange(Vector2.Zero,new Vector2(640,480));
        Check(ReferenceEquals(Widget.HitTestGlobal(root,circle.WidgetToScreen(new Vector2(32,31)),w=>w.IsHitTestVisible),circle),"native circular skill center clickable");
        Check(Widget.HitTestGlobal(root,circle.WidgetToScreen(new Vector2(1,1)),w=>w.IsHitTestVisible)==null,"transparent icon corner leaves world input available");
        Check(Widget.HitTestGlobal(root,circle.WidgetToScreen(new Vector2(63,61)),w=>w.IsHitTestVisible)==null,"opposite transparent corner does not intercept touch");
        Check(circle.HitTest(circle.WidgetToScreen(new Vector2(32,4))),"circular edge remains reachable");
        // Exercise the real queue and Dispose with no world/player or graphics objects.
        Type runtime=mod.GetType("SCIEPort.Features.AbyssReaper.WorldCutRuntime",true);
        object instance=FormatterServices.GetUninitializedObject(runtime);
        foreach(string name in new[]{"queue","casts","peers","retired"})
        {
            var f=runtime.GetField(name,Flags);f.SetValue(instance,Activator.CreateInstance(f.FieldType));
        }
        Type damage=mod.GetType("SCIEPort.Features.AbyssReaper.WorldCutDamage",true);
        Put(instance,"damage",Activator.CreateInstance(damage,Flags,null,new object[]{null,null,null,false,false,100f,40f},null));
        Type scene=mod.GetType("SCIEPort.Features.AbyssReaper.WorldCutScene",true);
        Put(instance,"scene",Activator.CreateInstance(scene,Flags,null,new object[]{false,true},null));
        object source=Activator.CreateInstance(type);Put(source,"Kind",(byte)1);
        MethodInfo enqueue=runtime.GetMethod("Enqueue",Flags);
        for(int i=0;i<100;i++)enqueue.Invoke(instance,new object[]{null,null,source});
        object queue=Get(instance,"queue");Check((int)queue.GetType().GetProperty("Count").GetValue(queue,null)==32,"queue flood cap");
        Put(source,"Kind",(byte)5);
        object first=queue.GetType().GetMethod("Peek").Invoke(queue,null);
        Check((byte)Get(Get(first,"Packet"),"Kind")==1,"network callback snapshots immutable packet data");
        object castList=Get(instance,"casts");Type castType=castList.GetType().GetGenericArguments()[0];
        castList.GetType().GetMethod("Add").Invoke(castList,new[]{Activator.CreateInstance(castType,true)});
        ((IDisposable)instance).Dispose();
        Check((int)castList.GetType().GetProperty("Count").GetValue(castList,null)==0,"retiring-owner failure still releases all casts");
        Check((int)queue.GetType().GetProperty("Count").GetValue(queue,null)==0,"Dispose drains queued references");
        enqueue.Invoke(instance,new object[]{null,null,source});
        Check((int)queue.GetType().GetProperty("Count").GetValue(queue,null)==0,"late callback after Dispose is ignored");
        TerrainFixture(mod);
        PortalPersistenceFixture(mod);
        ChunkLeaseFixture(mod);
        Console.WriteLine("WorldCut native packet, ID, touch hit-test, queue/flood/Dispose: "+checks+" checks passed (not device gameplay).");
    }
    private static void ChunkLeaseFixture(Assembly mod)
    {
        var work=CommonLib.WorkType;CommonLib.WorkType=WorkType.Local;
        try
        {
            var updater=(TerrainUpdater)FormatterServices.GetUninitializedObject(typeof(TerrainUpdater));
            var updaterType=typeof(TerrainUpdater);
            var paramsField=updaterType.GetField("m_updateParameters",Flags);
            object parameters=Activator.CreateInstance(paramsField.FieldType);
            var active=new System.Collections.Generic.Dictionary<int,TerrainUpdater.UpdateLocation>();
            paramsField.FieldType.GetField("Locations").SetValue(parameters,active);paramsField.SetValue(updater,parameters);
            var pending=new System.Collections.Generic.Dictionary<int,TerrainUpdater.UpdateLocation?>();
            updaterType.GetField("m_pendingLocations",Flags).SetValue(updater,pending);
            Type leaseType=mod.GetType("SCIEPort.Features.AbyssReaper.VoidChunkLease",true);
            MethodInfo create=leaseType.GetMethod("TryCreate",BindingFlags.Static|BindingFlags.NonPublic);
            object[] args={updater,new Vector2(128,96),null};
            Check((bool)create.Invoke(null,args),"native updater lease created");
            object lease=args[2];int id=(int)Get(lease,"id");
            Check(id < -1 && pending.ContainsKey(id) && pending[id].HasValue,"native updater queued owned location");
            ((IDisposable)lease).Dispose();
            Check(pending.ContainsKey(id)&&!pending[id].HasValue&&!active.ContainsKey(id),"cancel before update queues removal");
            pending.Clear();object[] second={updater,new Vector2(128,96),null};
            Check((bool)create.Invoke(null,second),"native updater second lease");
            lease=second[2];id=(int)Get(lease,"id");active[id]=pending[id].Value;pending.Remove(id);
            ((IDisposable)lease).Dispose();
            Check(!active.ContainsKey(id)&&pending.ContainsKey(id)&&!pending[id].HasValue,"cancel after apply forces allocation refresh");
            pending.Clear();object[] third={updater,new Vector2(128,96),null};
            Check((bool)create.Invoke(null,third),"native updater third lease");
            lease=third[2];id=(int)Get(lease,"id");var foreign=pending[id].Value;foreign.Center=new Vector2(900,900);pending[id]=foreign;
            ((IDisposable)lease).Dispose();
            Check(pending[id].HasValue&&pending[id].Value.Center==new Vector2(900,900),"foreign update is not removed");
            pending.Clear();object[] fourth={updater,new Vector2(128,96),null};
            Check((bool)create.Invoke(null,fourth),"lease for destination camera handoff");
            lease=fourth[2];id=(int)Get(lease,"id");Guid actor=Guid.NewGuid();
            Type travelType=mod.GetType("SCIEPort.Features.AbyssReaper.VoidPortalLocalTravel",true);
            object travel=FormatterServices.GetUninitializedObject(travelType);
            var clock=(SubsystemTime)FormatterServices.GetUninitializedObject(typeof(SubsystemTime));clock.m_gameTime=1;
            Put(travel,"clock",clock);
            Put(travel,"alreadyLoaded",new System.Collections.Generic.HashSet<Guid>());
            var leases=Activator.CreateInstance(travelType.GetField("leases",Flags).FieldType);
            leases.GetType().GetMethod("Add").Invoke(leases,new object[]{actor,lease});Put(travel,"leases",leases);
            var delay=new System.Collections.Generic.Dictionary<Guid,double>();delay.Add(actor,4);Put(travel,"releaseAfter",delay);
            var end=travelType.GetMethod("EndLoading",Flags);end.Invoke(travel,new object[]{actor});
            Check(pending[id].HasValue,"arrival grace retains destination while camera catches up");
            clock.m_gameTime=4;end.Invoke(travel,new object[]{actor});
            Check(!pending[id].HasValue,"destination lease released after grace");
            CommonLib.WorkType=WorkType.Client;object[] client={updater,new Vector2(0,0),null};
            Check(!(bool)create.Invoke(null,client),"client never creates authoritative chunk lease");
        }
        finally { CommonLib.WorkType=work; }
    }
    private static void PortalPersistenceFixture(Assembly mod)
    {
        Type registry=mod.GetType("SCIEPort.Features.AbyssReaper.VoidPortalRegistry",true);
        object instance=Activator.CreateInstance(registry,Flags,null,new object[]{Guid.NewGuid(),12},null);
        Type subsystem=mod.GetType("SCIEPort.Features.AbyssReaper.SubsystemAbyssReaper",true);
        object healthy=FormatterServices.GetUninitializedObject(subsystem);
        Put(healthy,"<Portals>k__BackingField",instance);
        var emptySaved=new ValuesDictionary();subsystem.GetMethod("Save").Invoke(healthy,new object[]{emptySaved});
        object[] emptyParsed={emptySaved.GetValue<string>("VoidPortalRecords"),null};
        Check((bool)registry.GetMethod("TryDeserialize",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,emptyParsed),"new world saves an empty portal registry");
        Check((int)registry.GetProperty("Count",Flags).GetValue(emptyParsed[1],null)==0,"empty save retains zero portals");
        Guid id=Guid.NewGuid(),owner=Guid.NewGuid();
        object[] create={id,owner,"测试 & Portal",8.5f,64f,8.5f,0f};
        Check((bool)registry.GetMethod("TryAdd",Flags).Invoke(instance,create),"portal fixture create");
        string original=(string)registry.GetMethod("Serialize",Flags).Invoke(instance,null);
        var healthySaved=new ValuesDictionary();subsystem.GetMethod("Save").Invoke(healthy,new object[]{healthySaved});
        Check(healthySaved.GetValue<string>("VoidPortalRecords")==original,"subsystem saves healthy portal records through the actual Save entry");
        var dictionary=new ValuesDictionary();dictionary.SetValue("VoidPortalRecords",original);
        var xml=new XElement("Subsystem");dictionary.Save(xml);
        var loaded=new ValuesDictionary();loaded.ApplyOverrides(XElement.Parse(xml.ToString()));
        Check(loaded.GetValue<string>("VoidPortalRecords")==original,"native ValuesDictionary preserves escaped portal records");
        object[] parsed={loaded.GetValue<string>("VoidPortalRecords"),null};
        Check((bool)registry.GetMethod("TryDeserialize",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,parsed),"native saved record parses");
        object degraded=FormatterServices.GetUninitializedObject(subsystem);
        Put(degraded,"portalLoadFailure","<broken>");
        var saved=new ValuesDictionary();subsystem.GetMethod("Save").Invoke(degraded,new object[]{saved});
        Check(saved.GetValue<string>("VoidPortalRecords")=="<broken>","save preserves failed input unchanged");
    }
    private static void TerrainFixture(Assembly mod)
    {
        var terrain=new Terrain();var chunk=terrain.AllocateChunk(0,0);chunk.State=TerrainChunkState.Valid;
        var native=(SubsystemTerrain)FormatterServices.GetUninitializedObject(typeof(SubsystemTerrain));
        typeof(SubsystemTerrain).GetField("<Terrain>k__BackingField",Flags).SetValue(native,terrain);
        Block air=BlocksManager.Blocks[0],stone=BlocksManager.Blocks[3];
        var territoryField=typeof(SubsystemBedrockBlockBehavior).GetField("Territories",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic);
        object territories=territoryField.GetValue(null);
        try
        {
            BlocksManager.Blocks[0]=new AirBlock { BlockIndex=0,IsCollidable=false };
            BlocksManager.Blocks[3]=new GraniteBlock { BlockIndex=3,IsCollidable=true };
            territoryField.SetValue(null,Activator.CreateInstance(territoryField.FieldType));
            terrain.SetCellValueFast(8,64,9,3);
            Type safety=mod.GetType("SCIEPort.Features.AbyssReaper.VoidPortalSafety",true);
            var safetyFlags=BindingFlags.Static|BindingFlags.NonPublic;
            MethodInfo ready=safety.GetMethod("Ready",safetyFlags),empty=safety.GetMethod("Empty",safetyFlags),floor=safety.GetMethod("StableFloor",safetyFlags),unprotected=safety.GetMethod("Unprotected",safetyFlags);
            Check((bool)ready.Invoke(null,new object[]{native,8,8}),"portal accepts valid loaded chunk");
            Type registryType=mod.GetType("SCIEPort.Features.AbyssReaper.VoidPortalRegistry",true);
            object registry=Activator.CreateInstance(registryType,Flags,null,new object[]{Guid.NewGuid(),12},null);
            Guid portalId=Guid.NewGuid();
            Check((bool)registryType.GetMethod("TryAdd",Flags).Invoke(registry,new object[]{portalId,Guid.NewGuid(),"Loaded",8.5f,64f,8.5f,0f}),"loaded-destination fixture record");
            object[] lookup={portalId,null};registryType.GetMethod("TryGet",Flags).Invoke(registry,lookup);
            Type travelType=mod.GetType("SCIEPort.Features.AbyssReaper.VoidPortalLocalTravel",true);
            object travel=Activator.CreateInstance(travelType,Flags,null,new object[]{native,null,null,registry},null);
            Guid actor=Guid.NewGuid();var oldWork=CommonLib.WorkType;CommonLib.WorkType=WorkType.Local;
            try {
                Check((bool)travelType.GetMethod("BeginLoading",Flags).Invoke(travel,new object[]{actor,lookup[1]}),"already loaded destination does not require private updater ABI");
                Check((bool)travelType.GetMethod("ChunksReady",Flags).Invoke(travel,new object[]{lookup[1]}),"loaded destination chunks remain ready before actor handshake");
                chunk.State=TerrainChunkState.InvalidContents1;
                Check(!(bool)travelType.GetMethod("ChunksReady",Flags).Invoke(travel,new object[]{lookup[1]}),"chunk downgrade blocks movement");
                chunk.State=TerrainChunkState.Valid;
            } finally {CommonLib.WorkType=oldWork;}

            Check(!(bool)ready.Invoke(null,new object[]{native,40,40}),"portal rejects missing chunk");
            Check((bool)empty.Invoke(null,new object[]{native,8,64,8}),"portal clear air");
            Check(!(bool)empty.Invoke(null,new object[]{native,8,64,9}),"portal rejects wall");
            Check(!(bool)empty.Invoke(null,new object[]{native,8,0,8}),"portal rejects world boundary");
            terrain.SetCellValueFast(8,63,8,3);
            Check((bool)floor.Invoke(null,new object[]{native,8,63,8}),"portal accepts full safe floor");
            BlocksManager.Blocks[3].HasCollisionBehavior=true;
            Check(!(bool)floor.Invoke(null,new object[]{native,8,63,8}),"portal rejects collision hazard floor");
            BlocksManager.Blocks[3].HasCollisionBehavior=false;
            terrain.SetCellValueFast(8,63,8,0);
            Type damage=mod.GetType("SCIEPort.Features.AbyssReaper.WorldCutDamage",true);
            object checker=Activator.CreateInstance(damage,Flags,null,new object[]{native,null,null,false,false,100f,40f},null);
            MethodInfo step=damage.GetMethod("Step",Flags);
            object[] args={new Vector3(8.5f,64.5f,7.5f),Vector3.UnitZ,-Vector3.UnitX,1f,2f,0f};
            object blocked=step.Invoke(checker,args);
            Check(blocked.ToString()=="Wall","actual native ray clips wide sweep at wall");
            Check((float)args[5]>1.4f&&(float)args[5]<1.5f,"safe prefix stops before physical wall");
            object[] unknown={new Vector3(30.5f,64.5f,7.5f),Vector3.UnitZ,-Vector3.UnitX,0f,.2f,0f};
            Check(step.Invoke(checker,unknown).ToString()=="Unloaded","missing chunk fails closed");
            damage.GetMethod("BeginFrame",Flags).Invoke(checker,new object[]{false});
            string last="";
            for(int i=0;i<10;i++)
            {
                object[] clear={new Vector3(8.5f,64.5f,7.5f),Vector3.UnitZ,-Vector3.UnitX,0f,.1f,0f};
                last=step.Invoke(checker,clear).ToString();
            }
            Check(last=="Capacity","real ray budget stops unchecked advancement");
            damage.GetMethod("BeginFrame",Flags).Invoke(checker,new object[]{false});
            var protectedArea=new Territoriy { Origin=new Point2(8,7),OwnChunkCoord=new Point3(0,64,0),OwnerGuid=Guid.NewGuid(),
                BoundBox=new BoundingBox(new Vector3(0,0,0),new Vector3(16,256,16)) };
            object dict=territoryField.GetValue(null);dict.GetType().GetMethod("Add").Invoke(dict,new object[]{protectedArea.OwnerGuid,protectedArea});
            Check(!(bool)unprotected.Invoke(null,new object[]{8,7}),"portal rejects protected native territory");
            Check(!(bool)empty.Invoke(null,new object[]{native,8,64,8}),"portal rejects protected air");
            object[] protectedArgs={new Vector3(8.5f,64.5f,7.5f),Vector3.UnitZ,-Vector3.UnitX,0f,.1f,0f};
            Check(step.Invoke(checker,protectedArgs).ToString()=="Protected","native protected territory stops the wave");
            Check(terrain.GetCellValue(8,64,9)==3,"skill collision did not modify isolated test terrain");
        }
        finally { territoryField.SetValue(null,territories);BlocksManager.Blocks[0]=air;BlocksManager.Blocks[3]=stone;terrain.Dispose(); }
    }

}

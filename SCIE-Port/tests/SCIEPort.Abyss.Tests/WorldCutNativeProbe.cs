using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
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
            Put(packet,"Kind",(byte)(i%6));Put(packet,"Reason",(byte)(i%10));Put(packet,"Caster",(ushort)(i+100));Put(packet,"Sequence",i>=48?uint.MaxValue-(uint)i:(uint)(i+1));
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
        Console.WriteLine("WorldCut native packet, ID, touch hit-test, queue/flood/Dispose: "+checks+" checks passed (not device gameplay).");
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
            object[] protectedArgs={new Vector3(8.5f,64.5f,7.5f),Vector3.UnitZ,-Vector3.UnitX,0f,.1f,0f};
            Check(step.Invoke(checker,protectedArgs).ToString()=="Protected","native protected territory stops the wave");
            Check(terrain.GetCellValue(8,64,9)==3,"skill collision did not modify isolated test terrain");
        }
        finally { territoryField.SetValue(null,territories);BlocksManager.Blocks[0]=air;BlocksManager.Blocks[3]=stone;terrain.Dispose(); }
    }

}

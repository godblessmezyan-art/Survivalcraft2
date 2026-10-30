using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using Engine;
using Engine.Graphics;
using Game;
using Game.NetWork;
using GameEntitySystem;
public static class VoidHorizonNativeProbe
{
    private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
    private static int checks;
    private static void Check(bool value,string text){checks++;if(!value)throw new Exception("Horizon native: "+text);}
    private static FieldInfo Field(Type type,string name)
    {
        for(;type!=null;type=type.BaseType){var field=type.GetField(name,Flags);if(field!=null)return field;}
        throw new Exception("No field "+name);
    }
    private static void Put(object obj,string name,object value){Field(obj.GetType(),name).SetValue(obj,value);}
    private static object Get(object obj,string name){return Field(obj.GetType(),name).GetValue(obj);}
    private static object Call(object obj,string method,params object[] args){return obj.GetType().GetMethod(method,Flags).Invoke(obj,args);}
    private static object New(Type type){return Activator.CreateInstance(type,true);}
    private static T Bare<T>(){return (T)FormatterServices.GetUninitializedObject(typeof(T));}
    public static void Run(Assembly mod)
    {
        var packageType=mod.GetType("SCIEPort.Features.AbyssReaper.VoidHorizonPackage",true);
        int min=int.MaxValue,max=0;
        for(int i=0;i<48;i++)
        {
            var packet=(IPackage)New(packageType);
            Put(packet,"Kind",(byte)(i%5));Put(packet,"Reason",(byte)(i%3));Put(packet,"Caster",(ushort)(60000+i));
            Put(packet,"Sequence",i<24?(uint)(i+1):uint.MaxValue-(uint)i);Put(packet,"Revision",(uint)(300+i));
            Put(packet,"Origin",new Vector3(i*1234.5f,64.125f,-i*120.5f));Put(packet,"Direction",new Vector3(.6f,0,.8f));
            Put(packet,"Elapsed",i*.35f);Put(packet,"Cooldown",180-i*.35f);
            var writer=new PackageStreamWriter();packet.WriteData(writer);byte[] bytes=writer.Data();
            min=Math.Min(min,bytes.Length);max=Math.Max(max,bytes.Length);
            Check(packet.ID==249&&bytes.Length<=64,"dedicated bounded packet");
            var read=(IPackage)New(packageType);read.ReadData(new PackageStreamReader(bytes));
            foreach(string field in new[]{"Kind","Reason","Caster","Sequence","Revision","Origin","Direction","Elapsed","Cooldown"})
                Check(Get(packet,field).Equals(Get(read,field)),"native serialization "+field);
        }
        int ids=0;
        foreach(Assembly assembly in new[]{typeof(IPackage).Assembly,mod})foreach(Type type in assembly.GetTypes())
        {
            if(type.IsAbstract||!type.IsClass||!typeof(IPackage).IsAssignableFrom(type)||type==packageType)continue;
            var packet=(IPackage)FormatterServices.GetUninitializedObject(type);Check(packet.ID!=249,"package collision "+type.Name);ids++;
        }
        Check(ids>=38,"native and mod package inventory");
        Console.WriteLine("Horizon actual native wire bytes: "+min+".."+max+", checked peer package IDs: "+ids);
        Fixture(mod,packageType);
        Geometry(mod);
        Console.WriteLine("Void horizon native protocol/lifecycle/immunity/geometry: "+checks+" checks passed (not gameplay/device proof).");
    }
    private static ComponentPlayer Player(Project project,int index)
    {
        var entity=Bare<Entity>();Put(entity,"m_project",project);Put(entity,"m_isAddedToProject",true);Put(entity,"m_components",new List<Component>());
        var player=new ComponentPlayer();player.m_entity=entity;player.PlayerGuid=Guid.NewGuid();
        player.ComponentHealth=new ComponentHealth();player.ComponentHealth.m_entity=entity;player.ComponentHealth.Health=1;
        player.ComponentBody=new ComponentBody();player.ComponentBody.m_entity=entity;
        player.ComponentSleep=new ComponentSleep();player.ComponentSleep.m_entity=entity;
        player.ComponentRider=new ComponentRider();player.ComponentRider.m_entity=entity;player.ComponentRider.ComponentCreature=player;
        player.ComponentMiner=new ComponentMiner();player.ComponentMiner.m_entity=entity;
        player.ComponentInput=new ComponentInput();player.ComponentInput.m_entity=entity;
        player.ComponentLocomotion=new ComponentLocomotion();player.ComponentLocomotion.m_entity=entity;
        var inventory=new ComponentInventory();inventory.m_entity=entity;
        inventory.m_slots.Add(new ComponentInventoryBase.Slot{Value=index,Count=1});
        player.ComponentMiner.Inventory=inventory;
        var data=Bare<PlayerData>();data.m_stateMachine=new StateMachine();data.m_stateMachine.m_currentState=new StateMachine.State{Name="Playing"};
        Put(data,"m_isMainPlayer",true);data.ComponentPlayer=player;player.PlayerData=data;
        ((List<Component>)Get(entity,"m_components")).AddRange(new Component[]{player,player.ComponentHealth,player.ComponentBody,player.ComponentMiner});
        return player;
    }
    private static void Fixture(Assembly mod,Type packetType)
    {
        WorkType previous=CommonLib.WorkType;CommonLib.WorkType=WorkType.Local;
        try
        {
            var project=new Project();var terrain=Bare<SubsystemTerrain>();Put(terrain,"m_project",project);
            var clock=new SubsystemTime();clock.m_gameTime=100;
            var runtimeType=mod.GetType("SCIEPort.Features.AbyssReaper.VoidHorizonRuntime",true);
            object runtime=Activator.CreateInstance(runtimeType,Flags,null,new object[]{terrain,null,clock,null,null,true,0},null);
            var subsystem=(Subsystem)New(mod.GetType("SCIEPort.Features.AbyssReaper.SubsystemAbyssReaper",true));
            Put(subsystem,"m_project",project);Put(subsystem,"<Horizon>k__BackingField",runtime);project.m_subsystems.Add(subsystem);
            int index=(int)mod.GetType("SCIEPort.Features.AbyssReaper.AbyssReaperBlock",true).GetField("Index").GetRawConstantValue();
            var player=Player(project,index);var teammate=Player(project,index);
            object list=Get(runtime,"casts");Type castType=list.GetType().GetGenericArguments()[0];
            object cast=New(castType);Put(cast,"Owner",player);Put(cast,"OwnerGuid",player.PlayerGuid);Put(cast,"Center",Vector3.Zero);Put(cast,"Direction",Vector3.UnitZ);
            Type timelineType=mod.GetType("SCIEPort.Features.AbyssReaper.HorizonTimeline`1",true).MakeGenericType(typeof(Entity));
            Put(cast,"Timeline",Activator.CreateInstance(timelineType,Flags,null,new object[]{100d},null));
            ((IList)list).Add(cast);
            Check((bool)Call(runtime,"Protects",player.ComponentHealth),"accepted caster protected at time zero");
            Check(!(bool)Call(runtime,"Protects",teammate.ComponentHealth),"immunity not granted to bystander");
            var loader=(ModLoader)New(mod.GetType("SCIEPort.Mod.SCIEPortLoader",true));
            foreach(bool ignore in new[]{false,true})foreach(string cause in new[]{"fire","magma","fall","suffocation","void","multiplayer",""})
            {
                bool skip;loader.OnCreatureInjure(player.ComponentHealth,100000,teammate,ignore,cause,out skip);
                Check(skip,"unconditional native injury veto: "+cause+" bypass="+ignore);
            }
            float damage=999;loader.AttackBody(player.ComponentBody,teammate,Vector3.Zero,Vector3.UnitX,ref damage,true);
            Check(damage==0,"armor damage blocked before native ApplyArmorProtection");
            float impulse=1,stun=1;bool recalculate=true;
            loader.AttackPowerParameter(player.ComponentBody,teammate,Vector3.Zero,Vector3.UnitX,ref impulse,ref stun,ref recalculate);
            Check(impulse==0&&stun==0,"native attack impulse/stun suppressed");
            clock.m_gameTime=116.999;Check((bool)Call(runtime,"Protects",player.ComponentHealth),"immune through end of domain");
            clock.m_gameTime=117;Check(!(bool)Call(runtime,"Protects",player.ComponentHealth),"immunity ends even before Update cleanup");
            clock.m_gameTime=99;Check(!(bool)Call(runtime,"Protects",player.ComponentHealth),"clock regression never extends immunity");
            clock.m_gameTime=101;Put(cast,"VisualOnly",true);Put(cast,"LeaseUntil",101d);
            Check(!(bool)Call(runtime,"Protects",player.ComponentHealth),"expired client lease grants no immunity");
            Put(cast,"LeaseUntil",102d);Check((bool)Call(runtime,"Protects",player.ComponentHealth),"accepted client presentation lease");
            Put(cast,"VisualOnly",false);
            var originalGuid=player.PlayerGuid;player.PlayerGuid=Guid.NewGuid();
            Check(!(bool)Call(runtime,"Protects",player.ComponentHealth),"entity ownership reuse revokes protection");player.PlayerGuid=originalGuid;
            ((ComponentInventory)player.ComponentMiner.Inventory).m_slots[0].Value=0;
            Check(!(bool)Call(runtime,"Protects",player.ComponentHealth),"weapon switch revokes before Update");
            ((ComponentInventory)player.ComponentMiner.Inventory).m_slots[0].Value=index;
            Put(player.Entity,"m_isAddedToProject",false);Check(!(bool)Call(runtime,"Protects",player.ComponentHealth),"removed owner protected by no stale cast");Put(player.Entity,"m_isAddedToProject",true);
            teammate.Category=CreatureCategory.LandPredator;
            Check(!(bool)Call(runtime,"Target",cast,teammate.ComponentBody),"even predator-tagged player excluded before LOS or pull");
            Check(!(bool)Call(runtime,"Target",cast,player.ComponentBody),"caster never targets itself");
            var input=player.ComponentInput;
            input.m_playerInput.Move=Vector3.One;input.m_playerInput.Jump=true;input.m_playerInput.Look=new Vector2(.3f,.7f);
            runtimeType.GetMethod("LockInput",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{player});
            Check(input.m_playerInput.Move==Vector3.Zero&&!input.m_playerInput.Jump&&input.m_playerInput.Look==new Vector2(.3f,.7f),"channel blocks movement while preserving look");
            var source=New(packetType);Put(source,"Kind",(byte)1);
            for(int i=0;i<100;i++)Call(runtime,"Enqueue",null,null,source);
            var queue=Get(runtime,"queue");Check((int)queue.GetType().GetProperty("Count").GetValue(queue,null)==32,"bounded callback queue");
            Put(source,"Kind",(byte)4);object queued=queue.GetType().GetMethod("Peek").Invoke(queue,null);
            Check((byte)Get(Get(queued,"Packet"),"Kind")==1,"callback snapshots do not alias input packet");
            Call(runtime,"Cancel",player);Check(!(bool)Call(runtime,"Protects",player.ComponentHealth)&&((IList)list).Count==0,"cancel removes cast and immunity immediately");
            // A client can finish locally before its final network event arrives.
            var peer=Call(runtime,"Get",player,true);Put(peer,"Sent",1u);Put(cast,"Sequence",1u);Put(cast,"VisualOnly",true);
            ((IList)list).Add(cast);Call(runtime,"Stop",cast,false);
            Check((uint)Get(peer,"Cancelled")==1u,"natural client expiry leaves a tombstone");
            Check(!(bool)Call(peer,"Receive",1u,10u,(byte)1),"late newer snapshot cannot resurrect locally completed cast");
            ((IDisposable)runtime).Dispose();
            Check((int)queue.GetType().GetProperty("Count").GetValue(queue,null)==0,"world disposal drains callback references");
            Call(runtime,"Enqueue",null,null,source);Check((int)queue.GetType().GetProperty("Count").GetValue(queue,null)==0,"late callback ignored after disposal");
            Check(!(bool)Call(runtime,"Protects",player.ComponentHealth),"disposed runtime never protects");
        }
        finally {CommonLib.WorkType=previous;}
    }
    private static void Geometry(Assembly mod)
    {
        var scene=New(mod.GetType("SCIEPort.Features.AbyssReaper.VoidHorizonScene",true));
        var budget=mod.GetType("SCIEPort.Features.AbyssReaper.VoidHorizonVisualRules",true).GetMethod("PrimitiveBudget",BindingFlags.Static|BindingFlags.NonPublic);
        foreach(int quality in new[]{0,1,2})foreach(float time in new[]{0f,1f,2f,2.5f,3.99f,4f,5f,8f,14f,15.99f,16f,16.5f,17f})
        {
            int count=(int)Call(scene,"Queue",Vector3.Zero,Vector3.UnitZ,time,quality);
            Check(count>=0&&count<=(int)budget.Invoke(null,new object[]{quality}),"bounded actual queued geometry q="+quality+" t="+time);
            if(time>=4&&time<16)Check(count>100,"full domain contains opaque background geometry");
            var renderer=(PrimitivesRenderer3D)Get(scene,"renderer");int actual=0;bool finite=true;
            foreach(BaseBatch item in renderer.m_allBatches)
            {
                var batch=item as FlatBatch3D;if(batch==null)continue;
                actual+=batch.TriangleIndices.Count/3;
                foreach(var vertex in batch.TriangleVertices)
                    finite &= !float.IsNaN(vertex.Position.X)&&!float.IsInfinity(vertex.Position.X)
                        &&!float.IsNaN(vertex.Position.Y)&&!float.IsInfinity(vertex.Position.Y)
                        &&!float.IsNaN(vertex.Position.Z)&&!float.IsInfinity(vertex.Position.Z);
            }
            Check(actual==count,"actual engine mesh agrees with declared budget");
            Check(finite,"all vertices finite, including fractional-power rift tips");
            ((IDisposable)scene).Dispose();
        }
    }
}

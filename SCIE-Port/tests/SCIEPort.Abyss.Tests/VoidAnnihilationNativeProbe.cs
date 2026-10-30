using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using Engine;
using Game;
using Game.NetWork;
using GameEntitySystem;
public static class VoidAnnihilationNativeProbe
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    static int checks;
    static void Check(bool value,string text){checks++;if(!value)throw new Exception("Annihilation: "+text);}
    static T Bare<T>(){return (T)FormatterServices.GetUninitializedObject(typeof(T));}
    static FieldInfo Field(Type t,string name){for(;t!=null;t=t.BaseType){var f=t.GetField(name,Flags);if(f!=null)return f;}throw new Exception(name);}
    static void Put(object obj,string name,object value){Field(obj.GetType(),name).SetValue(obj,value);}
    static object Get(object obj,string name){return Field(obj.GetType(),name).GetValue(obj);}
    static object Call(object obj,string name,params object[] args){return obj.GetType().GetMethod(name,Flags).Invoke(obj,args);}
    static ComponentCreature Creature(Project project,SubsystemBodies bodies,Vector3 at,bool passive)
    {
        var entity=Bare<Entity>();Put(entity,"m_project",project);Put(entity,"m_isAddedToProject",true);
        var creature=new ComponentCreature();creature.m_entity=entity;creature.Category=passive?CreatureCategory.LandOther:CreatureCategory.LandPredator;
        creature.ComponentBody=new ComponentBody();creature.ComponentBody.m_entity=entity;creature.ComponentBody.Position=at;creature.ComponentBody.BoxSize=new Vector3(.6f,1.6f,.6f);
        creature.ComponentHealth=new ComponentHealth();creature.ComponentHealth.m_entity=entity;creature.ComponentHealth.m_componentCreature=creature;
        creature.ComponentHealth.Health=1;creature.ComponentHealth.AttackResilience=1000000;creature.ComponentHealth.IsInvulnerable=true;
        Put(entity,"m_components",new List<Component>{creature,creature.ComponentBody,creature.ComponentHealth});bodies.AddBody(creature.ComponentBody);return creature;
    }
    public static void Run(Assembly mod)
    {
        var previous=CommonLib.WorkType;var net=CommonLib.Net;var territories=SubsystemBedrockBlockBehavior.Territories;var hooks=ModsManager.ModHooks;
        var cells=new Terrain();
        try
        {
            CommonLib.WorkType=WorkType.Server;CommonLib.Net=Bare<NetNode>();Put(CommonLib.Net,"PendingPackages",new List<IPackage>());
            SubsystemBedrockBlockBehavior.Territories=new Dictionary<Guid,Territoriy>();ModsManager.ModHooks=new Dictionary<string,ModsManager.ModHook>();
            cells.AllocateChunk(0,0).State=TerrainChunkState.Valid;cells.AllocateChunk(7,0).State=TerrainChunkState.Valid;cells.AllocateChunk(8,0).State=TerrainChunkState.Valid;
            var project=new Project();var terrain=Bare<SubsystemTerrain>();Put(terrain,"m_project",project);Put(terrain,"<Terrain>k__BackingField",cells);
            var bodies=new SubsystemBodies();Put(bodies,"m_project",project);var clock=new SubsystemTime();var players=new SubsystemPlayers();
            var owner=VoidHorizonNativeProbe.Player(project,952);owner.ComponentBody.Position=new Vector3(8.5f,64,8.5f);
            var teammate=VoidHorizonNativeProbe.Player(project,952);teammate.ComponentBody.Position=new Vector3(9,64,8.5f);teammate.Category=CreatureCategory.LandPredator;
            players.m_componentPlayers.Add(owner);players.m_componentPlayers.Add(teammate);bodies.AddBody(owner.ComponentBody);bodies.AddBody(teammate.ComponentBody);
            var creatures=new List<ComponentCreature>();for(int i=0;i<600;i++)creatures.Add(Creature(project,bodies,new Vector3(10+(i%5)*.1f,64+(i%3),9+(i%7)*.1f),i%2==0));
            var distant=Creature(project,bodies,new Vector3(120,64,8.5f),true);
            var boundary=Creature(project,bodies,new Vector3(136.5f,64,8.5f),false);
            var outside=Creature(project,bodies,new Vector3(136.6f,64,8.5f),false);
            var high=Creature(project,bodies,new Vector3(11,210,8.5f),true);
            var carrier=Creature(project,bodies,new Vector3(11,64,10),false);carrier.ComponentBody.m_childBodies.Add(teammate.ComponentBody);
            var runtimeType=mod.GetType("SCIEPort.Features.AbyssReaper.VoidHorizonRuntime",true);
            var runtime=Activator.CreateInstance(runtimeType,Flags,null,new object[]{terrain,bodies,clock,null,players,false,0},null);
            var castType=runtimeType.GetNestedType("Cast",BindingFlags.NonPublic);var cast=Activator.CreateInstance(castType,true);
            Put(cast,"Owner",owner);Put(cast,"OwnerGuid",owner.PlayerGuid);Put(cast,"Center",owner.ComponentBody.Position);Put(cast,"Direction",Vector3.UnitZ);
            var timeline=mod.GetType("SCIEPort.Features.AbyssReaper.HorizonTimeline`1",true).MakeGenericType(typeof(Entity));
            Put(cast,"Timeline",Activator.CreateInstance(timeline,Flags,null,new object[]{0d},null));((IList)Get(runtime,"casts")).Add(cast);
            clock.m_gameTime=4.199;Call(runtime,"Update");Check(creatures.TrueForAll(c=>c.ComponentHealth.Health==1),"no gradual damage before coverage");
            clock.m_gameTime=4.2;Call(runtime,"Update");
            int killed=creatures.FindAll(c=>c.ComponentHealth.Health==0).Count;
            Check(killed>0&&killed<=128,"settlement begins immediately with bounded frame work");
            Check(((IList)Get(runtime,"casts")).Count==0&&!(bool)Call(runtime,"Protects",owner.ComponentHealth),"domain and invulnerability vanish at coverage");
            for(int frame=0;frame<10&&((IList)Get(runtime,"settlements")).Count>0;frame++)Call(runtime,"Update");
            Check(creatures.TrueForAll(c=>c.ComponentHealth.Health==0),"all 600 predators and passive animals die, beyond old 24/512 caps");
            Check(distant.ComponentHealth.Health==0&&boundary.ComponentHealth.Health==0&&high.ComponentHealth.Health==0,"128-block boundary, high targets and beyond old 40-block radius");
            Check(outside.ComponentHealth.Health==1,"outside radius survives");
            Check(owner.ComponentHealth.Health==1&&teammate.ComponentHealth.Health==1&&carrier.ComponentHealth.Health==1,"caster, all players and occupied player carrier survive");
            Check(((IList)Get(runtime,"settlements")).Count==0,"all queued references retire");
            var execute=runtimeType.GetMethod("Execute",BindingFlags.NonPublic|BindingFlags.Static);
            execute.Invoke(null,new object[]{teammate.ComponentHealth});Check(teammate.ComponentHealth.Health==1,"player exclusion is also enforced at injury boundary");
            CommonLib.WorkType=WorkType.Client;execute.Invoke(null,new object[]{outside.ComponentHealth});
            Check(outside.ComponentHealth.Health==1,"ordinary client cannot execute damage");CommonLib.WorkType=WorkType.Server;
            Put(cast,"VisualOnly",true);Call(runtime,"StartSettlement",cast);Check(((IList)Get(runtime,"settlements")).Count==0,"presentation casts never execute damage");
            ((IDisposable)runtime).Dispose();Check(((IList)Get(runtime,"settlements")).Count==0,"dispose clears delayed settlement");
        }
        finally{cells.Dispose();CommonLib.Net=net;CommonLib.WorkType=previous;SubsystemBedrockBlockBehavior.Territories=territories;ModsManager.ModHooks=hooks;}
        Console.WriteLine("Void annihilation real native HP/authority/large-area/budget: "+checks+" checks passed (600 NPC fixture, no live world/device).");
    }
}

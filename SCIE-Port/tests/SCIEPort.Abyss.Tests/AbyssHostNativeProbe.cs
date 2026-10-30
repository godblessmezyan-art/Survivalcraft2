using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using Engine;
using Game;
using Game.NetWork;
using GameEntitySystem;

// Exercises real host targeting, native attack dispatch and portal operations without a display.
// The recording hook stops damage before audio/particles/network side effects: this is not gameplay.
public static class AbyssHostNativeProbe
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static int checks;
    static void Check(bool b,string label){checks++;if(!b)throw new Exception("Abyss host: "+label);}
    static void Put(object o,string name,object value)
    {
        for(Type t=o.GetType();t!=null;t=t.BaseType){var f=t.GetField(name,Flags);if(f!=null){f.SetValue(o,value);return;}}
        throw new Exception("Missing fixture field "+name);
    }
    static T Bare<T>(){return (T)FormatterServices.GetUninitializedObject(typeof(T));}
    static Entity Entity(Project p)
    {
        var e=Bare<Entity>();Put(e,"m_project",p);Put(e,"m_isAddedToProject",true);Put(e,"m_components",new List<Component>());return e;
    }
    internal static ComponentPlayer Player(Project p,SubsystemBodies bodies,int index)
    {
        var e=Entity(p);var player=new ComponentPlayer();player.m_entity=e;player.PlayerGuid=Guid.NewGuid();
        player.ComponentHealth=new ComponentHealth();player.ComponentHealth.m_entity=e;player.ComponentHealth.Health=1;
        player.ComponentBody=new ComponentBody();player.ComponentBody.m_entity=e;player.ComponentBody.Position=new Vector3(8.5f,64,5.5f);
        player.ComponentBody.BoxSize=new Vector3(.6f,1.8f,.6f);player.ComponentBody.m_subsystemBodies=bodies;
        player.ComponentSleep=new ComponentSleep();player.ComponentSleep.m_entity=e;
        player.ComponentRider=new ComponentRider();player.ComponentRider.m_entity=e;player.ComponentRider.ComponentCreature=player;
        player.ComponentMiner=new ComponentMiner();player.ComponentMiner.m_entity=e;
        player.ComponentCreatureModel=new ComponentCreatureModel();player.ComponentCreatureModel.m_entity=e;
        player.ComponentCreatureModel.m_eyePosition=new Vector3(8.5f,65.5f,5.5f);player.ComponentCreatureModel.m_eyeRotation=Quaternion.Identity;
        var inventory=new ComponentInventory();inventory.m_entity=e;inventory.m_slots.Add(new ComponentInventoryBase.Slot{Value=index,Count=1});player.ComponentMiner.Inventory=inventory;
        var data=Bare<PlayerData>();data.m_stateMachine=new StateMachine();data.m_stateMachine.m_currentState=new StateMachine.State{Name="Playing"};
        Put(data,"m_isMainPlayer",true);data.ComponentPlayer=player;player.PlayerData=data;
        Put(e,"m_components",new List<Component>{player,player.ComponentHealth,player.ComponentBody,player.ComponentMiner});
        return player;
    }
    sealed class AttackRecorder:ModLoader
    {
        internal readonly List<ComponentBody> Targets=new List<ComponentBody>();
        internal readonly List<float> Powers=new List<float>();
        public override bool AttackBody(ComponentBody target,ComponentCreature attacker,Vector3 point,Vector3 direction,ref float power,bool melee)
        { Targets.Add(target);Powers.Add(power);power=0;return true; }
    }
    public static void Run(Assembly mod)
    {
        var previous=CommonLib.WorkType;var hooks=ModsManager.ModHooks;
        var territory=SubsystemBedrockBlockBehavior.Territories;
        Block air=BlocksManager.Blocks[0],stone=BlocksManager.Blocks[3];var cells=new Terrain();
        try
        {
            CommonLib.WorkType=WorkType.Server;ModsManager.ModHooks=new Dictionary<string,ModsManager.ModHook>();
            SubsystemBedrockBlockBehavior.Territories=new Dictionary<Guid,Territoriy>();
            BlocksManager.Blocks[0]=new AirBlock{BlockIndex=0,IsCollidable=false};BlocksManager.Blocks[3]=new GraniteBlock{BlockIndex=3,IsCollidable=true};
            cells.AllocateChunk(0,0).State=TerrainChunkState.Valid;
            var project=new Project();var terrain=Bare<SubsystemTerrain>();Put(terrain,"m_project",project);Put(terrain,"<Terrain>k__BackingField",cells);
            var bodies=new SubsystemBodies();Put(bodies,"m_project",project);
            var moving=Bare<SubsystemMovingBlocks>();Put(moving,"m_project",project);moving.m_movingBlockSets=new List<SubsystemMovingBlocks.MovingBlockSet>();moving.m_result=new DynamicArray<IMovingBlockSet>();
            project.m_subsystems.Add(terrain);project.m_subsystems.Add(bodies);project.m_subsystems.Add(moving);
            var type=mod.GetType("SCIEPort.Features.AbyssReaper.SubsystemAbyssReaper",true);
            var runtime=(Subsystem)Activator.CreateInstance(type);Put(runtime,"m_project",project);Put(runtime,"terrain",terrain);Put(runtime,"bodies",bodies);
            int index=(int)mod.GetType("SCIEPort.Features.AbyssReaper.AbyssReaperBlock").GetField("Index").GetRawConstantValue();
            var owner=Player(project,bodies,index);
            var npcEntity=Entity(project);var creature=new ComponentCreature();creature.m_entity=npcEntity;
            creature.ComponentHealth=new ComponentHealth();creature.ComponentHealth.m_entity=npcEntity;creature.ComponentHealth.Health=1;
            creature.ComponentBody=new ComponentBody();creature.ComponentBody.m_entity=npcEntity;creature.ComponentBody.Mass=1;
            creature.ComponentBody.Position=new Vector3(10.5f,64.5f,7.5f);creature.ComponentBody.BoxSize=new Vector3(.8f,1.5f,.8f);
            Put(npcEntity,"m_components",new List<Component>{creature,creature.ComponentHealth,creature.ComponentBody});bodies.AddBody(creature.ComponentBody);
            var recorder=new AttackRecorder();ModsManager.RegisterHook("AttackBody",recorder);
            var state=Activator.CreateInstance(type.GetNestedType("State",BindingFlags.NonPublic),true);Put(state,"SwingDirection",Vector3.Normalize(new Vector3(1,0,1)));
            type.GetMethod("Strike",Flags).Invoke(runtime,new object[]{owner,state});
            Check(recorder.Targets.Count==1&&recorder.Targets[0]==creature.ComponentBody&&recorder.Powers[0]==22,"host directs positive normal damage to aimed non-predator");
            var allow=type.GetMethod("MeleeTargetAllowed",BindingFlags.Static|BindingFlags.NonPublic);
            Check(!(bool)allow.Invoke(null,new object[]{false,false,false,false,false,false}),"passive collateral stays disabled");
            Check(!(bool)allow.Invoke(null,new object[]{true,false,true,false,false,true}),"default excludes teammate even with friendly fire");
            var sample=new PlayerInput{Hit=new Ray3(Vector3.Zero,Vector3.UnitZ),Interact=new Ray3(Vector3.Zero,Vector3.UnitZ)};
            var interact=type.GetMethod("WantsPortalInteraction",BindingFlags.Static|BindingFlags.NonPublic);
            Check((bool)interact.Invoke(null,new object[]{sample,false,false}),"native phone Hit+Interact tap can manage portal");
            Check(!(bool)interact.Invoke(null,new object[]{sample,true,false})&&!(bool)interact.Invoke(null,new object[]{sample,false,true}),"melee/channel locks still block portal interaction");
            bodies.RemoveBody(creature.ComponentBody);
            var registryType=mod.GetType("SCIEPort.Features.AbyssReaper.VoidPortalRegistry",true);
            var registry=Activator.CreateInstance(registryType,Flags,null,new object[]{Guid.NewGuid(),12},null);Put(runtime,"<Portals>k__BackingField",registry);
            var ops=Activator.CreateInstance(mod.GetType("SCIEPort.Features.AbyssReaper.VoidPortalOperations",true),Flags,null,new object[]{runtime,terrain},null);
            for(int x=7;x<=13;x++)cells.SetCellValueFast(x,63,9,3);
            var create=ops.GetType().GetMethod("TryCreate",Flags);object[] first={owner,8,64,9,true,"Source",Guid.Empty};object[] second={owner,12,64,9,true,"Destination",Guid.Empty};
            Check((bool)create.Invoke(ops,first),"host creates portal on real loaded native terrain");
            var lowerAim=Vector3.Normalize(new Vector3(0,-1.25f,4));
            Check((Guid)type.GetMethod("FindPortal",Flags).Invoke(runtime,new object[]{owner,lowerAim})==(Guid)first[6],"phone can select visible lower portal edge");
            cells.SetCellValueFast(8,64,7,3);
            Check((Guid)type.GetMethod("FindPortal",Flags).Invoke(runtime,new object[]{owner,lowerAim})==Guid.Empty,"portal selection cannot cross wall");
            cells.SetCellValueFast(8,64,7,0);
            Check((bool)create.Invoke(ops,second),"host creates second portal");
            Check((bool)ops.GetType().GetMethod("Link",Flags).Invoke(ops,new object[]{owner,first[6],second[6]}),"host links portal destination");
            Check((bool)ops.GetType().GetMethod("Rename",Flags).Invoke(ops,new object[]{owner,first[6],"Renamed"}),"host portal management owns nearby record");
            Check(cells.GetCellValue(8,63,9)==3&&cells.GetCellValue(8,64,9)==0,"portal creation does not change terrain");
            CommonLib.WorkType=WorkType.Client;object[] denied={owner,10,64,9,true,"Client",Guid.Empty};
            Check(!(bool)create.Invoke(ops,denied),"client cannot mutate authoritative portal registry");
            Console.WriteLine("Abyss host native targeting/input/portal operations: "+checks+" checks passed (attack hook recorded; no actual gameplay).");
        }
        finally {CommonLib.WorkType=previous;ModsManager.ModHooks=hooks;SubsystemBedrockBlockBehavior.Territories=territory;BlocksManager.Blocks[0]=air;BlocksManager.Blocks[3]=stone;cells.Dispose();}
    }
}

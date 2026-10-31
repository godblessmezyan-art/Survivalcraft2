using System;
using System.IO;
using System.Xml.Linq;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using Engine;
using Engine.Graphics;
using Game;
using Game.NetWork;
using GameEntitySystem;
using TemplatesDatabase;

// Real target components/health/path orders and isolated network queues, not live game/device proof.
public static class DeathAuthorityNativeProbe
{
    const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;static Assembly mod;static int checks;static Database database;static GameDatabase game;
    static void Check(bool v,string message){checks++;if(!v)throw new Exception("Death: "+message);}
    static T Bare<T>(){return (T)FormatterServices.GetUninitializedObject(typeof(T));}
    static FieldInfo Field(Type t,string n){for(;t!=null;t=t.BaseType){var f=t.GetField(n,F);if(f!=null)return f;}throw new Exception(n);}
    static object Get(object o,string n){return Field(o.GetType(),n).GetValue(o);}static void Put(object o,string n,object v){Field(o.GetType(),n).SetValue(o,v);}
    static object Call(object o,string n,params object[] args){return o.GetType().GetMethods(F).Single(x=>x.Name==n&&x.GetParameters().Length==args.Length).Invoke(o,args);}
    static object New(string name,params object[] args){return Activator.CreateInstance(mod.GetType("SCIEPort.Features.AbyssReaper."+name,true),F,null,args,null);}
    sealed class Fixture:IDisposable
    {
        internal object Base,Death,Owner;internal Project Project;internal ComponentPlayer Player;internal SubsystemTime Clock;internal SubsystemBodies Bodies;internal Client Client;
        internal Fixture(bool main=true)
        {
            var t=typeof(VoidSpaceNativeProbe).GetNestedType("Fixture",BindingFlags.NonPublic);Base=Activator.CreateInstance(t,F,null,new object[]{main,(byte)2},null);
            Project=(Project)Get(Base,"Project");Project.m_gameDatabase=game;Player=(ComponentPlayer)Get(Base,"Player");Clock=(SubsystemTime)Get(Base,"Clock");Bodies=(SubsystemBodies)Get(Base,"Bodies");Owner=Get(Base,"Owner");Client=(Client)Get(Base,"Client");
            Death=New("DeathAuthorityRuntime",Owner,Get(Base,"Terrain"),Bodies,Clock,Get(Base,"Players"));Put(Owner,"<Death>k__BackingField",Death);
        }
        internal ComponentCreature Creature(ushort id,Vector3 position,bool predator=false)
        {
            var c=(ComponentCreature)Call(Base,"Creature",position,false);c.Entity.EntityId=id;c.Category=predator?CreatureCategory.LandPredator:CreatureCategory.LandOther;c.DisplayName="Fixture Wolf";
            c.ComponentHealth.m_componentCreature=c;c.ComponentHealth.AttackResilience=100;c.ComponentSpawn=new ComponentSpawn{m_entity=c.Entity,SpawnTime=id,AutoDespawn=true};c.ComponentCreatureModel=new ComponentCreatureModel{m_entity=c.Entity,m_eyePosition=position+Vector3.UnitY};
            var selector=new ComponentBehaviorSelector{m_entity=c.Entity};var path=new ComponentPathfinding{m_entity=c.Entity,m_componentPilot=new ComponentPilot()};var miner=new ComponentMiner{m_entity=c.Entity,ComponentCreature=c,AttackPower=8};
            ((List<Component>)Get(c.Entity,"m_components")).AddRange(new Component[]{selector,path,miner,c.ComponentCreatureModel,c.ComponentSpawn});
            var values=new ValuesDictionary{DatabaseObject=database.FindDatabaseObject("Wolf_Gray",game.EntityTemplateType,true)};Put(c.Entity,"m_valuesDictionary",values);return c;
        }
        internal void Request(byte action,Vector3 direction){Check((bool)Call(Death,"Request",Player,action,direction,(ushort)0),"local command queued");Call(Death,"Update");}
        public void Dispose(){((IDisposable)Death).Dispose();((IDisposable)Base).Dispose();}
    }
    static void Combat()
    {
        CommonLib.WorkType=WorkType.Local;
        using(var f=new Fixture())
        {
            f.Request(1,-Vector3.UnitZ);Check((bool)Call(f.Death,"IsDeath",f.Player),"toggle enters death authority");
            f.Clock.m_gameTime+=.2;f.Request(2,-Vector3.UnitZ);Check((bool)Call(f.Death,"Empowered",f.Player),"empower is active for twelve seconds");
            var c=f.Creature(11,new Vector3(8.5f,64,2));f.Clock.m_gameTime+=.2;f.Request(5,-Vector3.UnitZ);
            Check(c.ComponentHealth.Health>0&&Math.Abs(c.ComponentHealth.Health-.01f)<.0001f,"first real native hit leaves one resilience-scaled health point");
            f.Clock.m_gameTime+=.5;f.Request(5,-Vector3.UnitZ);
            Check(c.ComponentHealth.Health==1&&(bool)Call(f.Death,"IsFriendly",c),"second hit converts and heals without applying the old normal damage");
            Check(c.Entity.FindComponent<ComponentBehaviorSelector>(true).IsDisableBehavior&&!c.ComponentSpawn.AutoDespawn,"native selector is taken over and natural despawn is disabled");
            Check((bool)Call(f.Death,"FriendlyDamage",f.Player.ComponentBody,c)&&(bool)Call(f.Death,"FriendlyDamage",c.ComponentBody,f.Player),"summons cannot damage players and player area attacks cannot damage friendly summons");
            var s=Call(f.Death,"Get",f.Player);f.Clock.m_gameTime+=1.1;f.Request(1,-Vector3.UnitZ);
            Check(!(bool)Call(f.Death,"IsDeath",f.Player)&&!(bool)Call(f.Death,"Empowered",f.Player)&&(bool)Call(f.Death,"IsFriendly",c),"switching to void ends empowerment while retaining army");
            Check((int)Call(f.Death,"Count",f.Player.PlayerGuid)==1,"one converted creature consumes one common slot");
            var data=(ValuesDictionary)Call(f.Death,"Save");Check(data.GetValue<int>("Count")==1&&data.GetValue<int>("Version")==1,"contract save contains owner/entity/template/spawn fingerprint");
            Call(f.Death,"Dismiss",f.Player,(ushort)11);Check(!(bool)Call(f.Death,"IsFriendly",c)&&!c.Entity.FindComponent<ComponentBehaviorSelector>(true).IsDisableBehavior&&c.ComponentSpawn.AutoDespawn,"dismiss restores original native AI/despawn settings");
            var replacement=New("DeathAuthorityRuntime",f.Owner,Get(f.Base,"Terrain"),f.Bodies,f.Clock,Get(f.Base,"Players"));Call(replacement,"Load",data);Call(replacement,"EntityAdded",c.Entity);
            Check((bool)Call(replacement,"IsFriendly",c),"saved native entity ID and fingerprint rebind the contract");((IDisposable)replacement).Dispose();
            Put(s,"Death",true);Put(s,"EmpowerUntil",f.Clock.GameTime+12);f.Clock.m_gameTime+=12.01;
            Check(!(bool)Call(f.Death,"Empowered",f.Player),"empower lease ends at twelve seconds");
        }
        using(var f=new Fixture())
        {
            f.Request(1,-Vector3.UnitZ);f.Clock.m_gameTime+=.2;f.Request(2,-Vector3.UnitZ);
            for(ushort id=20;id<26;id++)Check((bool)Call(f.Death,"Adopt",f.Creature(id,new Vector3(12+(id-20),64,8)),f.Player.PlayerGuid,false),"adopt up to six");
            Check(!(bool)Call(f.Death,"Adopt",f.Creature(27,new Vector3(19,64,8)),f.Player.PlayerGuid,false),"seventh common/special slot is rejected");
            var waiting=f.Creature(28,new Vector3(8.5f,64,2));f.Clock.m_gameTime+=.2;f.Request(5,-Vector3.UnitZ);f.Clock.m_gameTime+=.5;f.Request(5,-Vector3.UnitZ);
            Check(waiting.ComponentHealth.Health>0&&waiting.ComponentHealth.Health<.02f&&!(bool)Call(f.Death,"IsFriendly",waiting),"full army does not kill the target on attempted second-hit conversion");
        }
        using(var f=new Fixture())
        {
            f.Request(1,-Vector3.UnitZ);var friend=f.Creature(30,new Vector3(10,64,8));Call(f.Death,"Adopt",friend,f.Player.PlayerGuid,false);
            var hostile=f.Creature(31,new Vector3(11,64,10),true);var neutral=f.Creature(32,new Vector3(13,64,10));
            f.Clock.m_gameTime+=.2;f.Request(3,-Vector3.UnitZ);f.Clock.m_gameTime+=1.249;Call(f.Death,"Update");Check(hostile.ComponentHealth.Health==1,"wave does not execute before full expansion");
            f.Clock.m_gameTime+=.002;Call(f.Death,"Update");Check(hostile.ComponentHealth.Health==0&&neutral.ComponentHealth.Health==1&&friend.ComponentHealth.Health==1&&f.Player.ComponentHealth.Health==1,"wave executes hostile NPC while preserving neutral/friendly/player");
            Check((double)Call(f.Death,"Remaining",f.Player,(byte)3)>34,"wave uses the independent 36-second cooldown");
        }
        using(var f=new Fixture())
        {
            var unknown=new ValuesDictionary();unknown.SetValue("Version",999);Call(f.Death,"Load",unknown);Check(ReferenceEquals(Call(f.Death,"Save"),unknown),"future save data retained unchanged");
        }
    }
    static object Packet(Fixture f,byte action,uint sequence){var q=New("DeathAuthorityPackage");Put(q,"Action",action);Put(q,"Caster",f.Player.Entity.EntityId);Put(q,"Sequence",sequence);Put(q,"World",Get(f.Death,"worldId"));Put(q,"Direction",-Vector3.UnitZ);return q;}
    sealed class AttackRecorder:ModLoader
    {
        internal readonly List<ComponentBody> Targets=new List<ComponentBody>();
        public override bool AttackBody(ComponentBody target,ComponentCreature attacker,Vector3 point,Vector3 direction,ref float power,bool melee){Targets.Add(target);power=0;return true;}
    }
    static void ArmyAndBudget()
    {
        CommonLib.WorkType=WorkType.Local;
        using(var f=new Fixture())
        {
            var friend=f.Creature(50,new Vector3(10,64,8));Call(f.Death,"Adopt",friend,f.Player.PlayerGuid,false);
            var enemy=f.Creature(51,new Vector3(12.4f,64,8),true);Call(f.Death,"Update");
            Check(friend.Entity.FindComponent<ComponentPathfinding>(true).Destination==enemy.ComponentBody.Position,"guard advances beyond three blocks into native attack reach");
            var recorder=new AttackRecorder();ModsManager.RegisterHook("AttackBody",recorder);friend.ComponentBody.Position=new Vector3(11,64,8);f.Clock.m_gameTime+=.6;Call(f.Death,"Update");
            Check(recorder.Targets.Contains(enemy.ComponentBody),"native guard melee chooses hostile NPC and dispatches through real AttackBody hook");
            Check(!recorder.Targets.Contains(f.Player.ComponentBody),"owner never selected for allied melee");
            f.Player.ComponentMiner.Inventory.RemoveSlotItems(0,1);f.Clock.m_gameTime+=.6;Call(f.Death,"Update");Check((bool)Call(f.Death,"IsFriendly",friend),"army survives weapon unequip");
            ModsManager.ModHooks.Clear();
            var saved=(ValuesDictionary)Call(f.Death,"Save");Call(f.Death,"Dismiss",f.Player,(ushort)50);Call(f.Death,"Load",saved);f.Clock.m_gameTime+=20;Call(f.Death,"Update");
            Check(((ValuesDictionary)Call(f.Death,"Save")).GetValue<int>("Count")==1,"unloaded contract is not silently discarded after a timeout");
            var list=(IList)Call(f.Death,"Friends",f.Player.PlayerGuid);Check(list.Count==1,"pending saved contract remains manageable");Call(f.Death,"Dismiss",f.Player,(ushort)50);
            Check((int)Call(f.Death,"Count",f.Player.PlayerGuid)==0,"owner may release unresolved saved contract");
        }
        using(var f=new Fixture())
        {
            f.Request(1,-Vector3.UnitZ);var creatures=new List<ComponentCreature>();
            for(ushort i=0;i<260;i++)creatures.Add(f.Creature((ushort)(100+i),new Vector3(10+i%13*.15f,64,10+i/13*.15f),true));
            f.Clock.m_gameTime+=.2;f.Request(3,-Vector3.UnitZ);f.Clock.m_gameTime+=1.26;Call(f.Death,"Update");
            int dead=creatures.Count(c=>c.ComponentHealth.Health==0);Check(dead==128,"first wave settlement frame respects 128-candidate global budget");
            var settled=creatures.First(c=>c.ComponentHealth.Health==0);settled.ComponentHealth.Health=1;Call(f.Death,"Update");Check(settled.ComponentHealth.Health==1,"wave ledger never executes a settled entity twice");
            Call(f.Death,"Update");Check(creatures.Count(c=>c.ComponentHealth.Health==0)==259,"remaining wave candidates settle over bounded frames");
        }
    }
    static void Network()
    {
        CommonLib.WorkType=WorkType.Server;
        using(var f=new Fixture(false))
        {
            var q=Packet(f,1,1);Call(f.Death,"Enqueue",f.Player,f.Client,q);Call(f.Death,"Update");Check((bool)Call(f.Death,"IsDeath",f.Player),"ordinary player toggles via authenticated host request");
            f.Clock.m_gameTime+=2;Call(f.Death,"Enqueue",f.Player,f.Client,q);Call(f.Death,"Update");Check((bool)Call(f.Death,"IsDeath",f.Player),"replayed toggle does not toggle again");
            q=Packet(f,1,2);Put(q,"World",Guid.NewGuid());Call(f.Death,"Enqueue",f.Player,f.Client,q);Call(f.Death,"Update");Check((bool)Call(f.Death,"IsDeath",f.Player),"cross-world stale command rejected");
            q=Packet(f,1,3);Call(f.Death,"Enqueue",f.Player,f.Client,q);Put(f.Client,"<TokenId>k__BackingField",Guid.NewGuid());Call(f.Death,"Update");Check((bool)Call(f.Death,"IsDeath",f.Player),"queued old connection token rejected");
        }
        var type=mod.GetType("SCIEPort.Features.AbyssReaper.DeathAuthorityPackage",true);
        foreach(var assembly in new[]{typeof(IPackage).Assembly,mod})foreach(var t in assembly.GetTypes())if(t.IsClass&&!t.IsAbstract&&typeof(IPackage).IsAssignableFrom(t)&&t!=type)Check(((IPackage)FormatterServices.GetUninitializedObject(t)).ID!=252,"252 audited unique");
        for(byte kind=0;kind<8;kind++)
        {
            var q=New("DeathAuthorityPackage");Put(q,"Kind",kind);Put(q,"Action",(byte)5);Put(q,"Owner",Guid.NewGuid());Put(q,"World",Guid.NewGuid());Put(q,"Stamp",123.5);Put(q,"Echo",456.75);Put(q,"Name","冥界猎犬");
            var writer=new PackageStreamWriter();((IPackage)q).WriteData(writer);var bytes=writer.Data();Check(bytes.Length<256,"bounded real native wire");var copy=New("DeathAuthorityPackage");((IPackage)copy).ReadData(new PackageStreamReader(bytes));
            foreach(var name in new[]{"Kind","Action","Owner","World","Stamp","Echo","Name"})Check(Get(q,name).Equals(Get(copy,name)),"native field roundtrip "+name);
        }
    }
    static void ClientAndInput()
    {
        CommonLib.WorkType=WorkType.Local;
        using(var f=new Fixture())
        {
            Call(f.Base,"EnableCut");f.Request(1,-Vector3.UnitZ);f.Clock.m_gameTime+=.2;f.Request(2,-Vector3.UnitZ);
            var state=Activator.CreateInstance(f.Owner.GetType().GetNestedType("State",BindingFlags.NonPublic),true);Put(state,"Ray",new Ray3(f.Player.ComponentCreatureModel.EyePosition,-Vector3.UnitZ));Put(state,"CombatTime",20d);
            Call(f.Owner,"UpdateMeleeInput",f.Player,state,true,0f);Put(state,"CombatTime",22d);Call(f.Owner,"UpdateMeleeInput",f.Player,state,false,0f);
            Check((int)Get(Get(state,"Combo"),"<Stage>k__BackingField")<3,"empowered long hold resolves to normal attack instead of charged heavy");
            var creature=f.Creature(80,new Vector3(10,64,8));Call(f.Death,"Adopt",creature,f.Player.PlayerGuid,false);var stranger=Guid.NewGuid();var original=f.Player.PlayerGuid;f.Player.PlayerGuid=stranger;
            Call(f.Death,"Dismiss",f.Player,(ushort)80);Check((bool)Call(f.Death,"IsFriendly",creature),"another player cannot dismiss this owner's contract");f.Player.PlayerGuid=original;
        }
        CommonLib.WorkType=WorkType.Client;
        using(var f=new Fixture())
        {
            CommonLib.Net.Server=f.Client;Put(f.Death,"serverToken",f.Client.TokenId);Put(f.Death,"worldKnown",true);Put(f.Death,"hasClock",true);var state=Call(f.Death,"Get",f.Player);
            var q=Packet(f,2,1);Put(q,"Kind",(byte)1);Put(q,"Owner",f.Player.PlayerGuid);Put(q,"Revision",2u);Put(q,"Flags",(byte)1);Put(q,"A",12f);Put(q,"B",3f);Put(q,"Stamp",f.Clock.GameTime-2);Call(f.Death,"Receive",q);
            Check((bool)Call(f.Death,"IsDeath",f.Player)&&Math.Abs((double)Call(f.Death,"EmpowerRemaining",f.Player)-10)<.01,"client ages authoritative empowerment instead of restarting twelve seconds on arrival");
            Put(q,"Revision",1u);Put(q,"Flags",(byte)0);Call(f.Death,"Receive",q);Check((bool)Call(f.Death,"IsDeath",f.Player),"stale state cannot overwrite current authority");
            var victim=f.Creature(81,new Vector3(8.5f,64,2));Call(f.Death,"Strike",f.Player,-Vector3.UnitZ);Check(victim.ComponentHealth.Health==1,"client visual/runtime path cannot directly injure real native target");
            var wave=Packet(f,3,3);Put(wave,"Kind",(byte)2);Put(wave,"Owner",f.Player.PlayerGuid);Put(wave,"Position",f.Player.ComponentBody.Position);Put(wave,"Stamp",f.Clock.GameTime);Call(f.Death,"Receive",wave);Call(f.Death,"Receive",wave);
            Check(((IList)Get(f.Death,"waves")).Count==1,"duplicated wave event yields one client visual");f.Clock.m_gameTime+=2;Call(f.Death,"Update");Call(f.Death,"Receive",wave);Check(((IList)Get(f.Death,"waves")).Count==0,"expired wave event does not replay after visual end");
            Func<byte,uint,object> friend=(kind,rev)=>{var p=Packet(f,0,0);Put(p,"Kind",kind);Put(p,"Revision",rev);Put(p,"Stamp",f.Clock.GameTime);return p;};
            q=friend(6,10);Put(q,"Target",(ushort)1);Call(f.Death,"Receive",q);q=friend(3,11);Put(q,"Sequence",10u);Put(q,"Target",(ushort)81);Put(q,"Owner",f.Player.PlayerGuid);Put(q,"Name","契约生物");Call(f.Death,"Receive",q);
            q=friend(4,12);Put(q,"Target",(ushort)81);Call(f.Death,"Receive",q);q=friend(7,13);Put(q,"Sequence",10u);Call(f.Death,"Receive",q);Check((int)Call(f.Death,"Count",f.Player.PlayerGuid)==0,"newer removal wins over in-flight full army snapshot");
            var attacker=new Client(Guid.NewGuid(),null);q=friend(1,20);Put(q,"Caster",f.Player.Entity.EntityId);Put(q,"Owner",f.Player.PlayerGuid);Call(f.Death,"Enqueue",null,attacker,q);Call(f.Death,"Update");Check((bool)Call(f.Death,"IsDeath",f.Player),"state from a non-server client is rejected");CommonLib.Net.Server=null;
        }
    }
    public static void Run(Assembly assembly,XElement xml)
    {
        mod=assembly;database=XmlDatabaseSerializer.LoadDatabase(xml);game=new GameDatabase(database);typeof(VoidSpaceNativeProbe).GetField("mod",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,assembly);
        var work=CommonLib.WorkType;var net=CommonLib.Net;var hooks=ModsManager.ModHooks;var territory=SubsystemBedrockBlockBehavior.Territories;var air=BlocksManager.Blocks[0];var stone=BlocksManager.Blocks[3];
        try
        {
            CommonLib.Net=Bare<NetNode>();Put(CommonLib.Net,"PendingPackages",new List<IPackage>());CommonLib.Net.Clients=new Dictionary<int,Client>();ModsManager.ModHooks=new Dictionary<string,ModsManager.ModHook>();SubsystemBedrockBlockBehavior.Territories=new Dictionary<Guid,Territoriy>();
            BlocksManager.Blocks[0]=new AirBlock{BlockIndex=0,IsCollidable=false};BlocksManager.Blocks[3]=new GraniteBlock{BlockIndex=3,IsCollidable=true};Combat();ArmyAndBudget();Network();ClientAndInput();
        }
        finally{CommonLib.WorkType=work;CommonLib.Net=net;ModsManager.ModHooks=hooks;SubsystemBedrockBlockBehavior.Territories=territory;BlocksManager.Blocks[0]=air;BlocksManager.Blocks[3]=stone;}
        Console.WriteLine("Death authority native health/conversion/army/ownership/persistence/authentication checks: "+checks+" passed (isolated; not live network/Android).");
    }
}

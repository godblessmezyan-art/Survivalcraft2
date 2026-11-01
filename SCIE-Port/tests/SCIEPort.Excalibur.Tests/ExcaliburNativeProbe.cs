using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using Engine;
using Game;
using Game.NetWork;
using GameEntitySystem;
public static class ExcaliburNativeProbe
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static int checks;static void Check(bool ok,string label){checks++;if(!ok)throw new Exception("Excalibur native: "+label);}
    static object Get(object o,string n){return o.GetType().GetField(n,F).GetValue(o);}
    static void Put(object o,string n,object v){for(var t=o.GetType();t!=null;t=t.BaseType){var f=t.GetField(n,F);if(f!=null){f.SetValue(o,v);return;}}throw new Exception("fixture field "+n);}
    static T Bare<T>(){return (T)FormatterServices.GetUninitializedObject(typeof(T));}
    static object Call(object o,string n,params object[] args){return o.GetType().GetMethod(n,F).Invoke(o,args);}
    static Entity Entity(Project p){var e=Bare<Entity>();Put(e,"m_project",p);Put(e,"m_isAddedToProject",true);Put(e,"m_components",new List<Component>());return e;}
    static ComponentBody Npc(Project p,SubsystemBodies bodies,Vector3 pos)
    {
        var e=Entity(p);var health=new ComponentHealth();health.m_entity=e;health.Health=1;
        var body=new ComponentBody();body.m_entity=e;body.Position=pos;body.BoxSize=new Vector3(.6f,1,.6f);body.Mass=1;
        Put(e,"m_components",new List<Component>{body,health});bodies.AddBody(body);return body;
    }
    sealed class Recorder:ModLoader
    {
        internal readonly List<float> Powers=new List<float>();internal bool Fail;
        public override bool AttackBody(ComponentBody b,ComponentCreature a,Vector3 point,Vector3 dir,ref float power,bool melee)
        {Powers.Add(power);if(Fail)throw new Exception("injected external damage-hook failure");power=0;return true;}
    }
    sealed class InjuryRecorder:ModLoader
    {
        internal int Count;internal float Amount;
        public override void OnCreatureInjure(ComponentHealth h,float amount,ComponentCreature a,bool ignore,string cause,out bool skip)
        {Count++;Amount=amount;skip=true;}
    }
    static object Cast(Type rt,ComponentPlayer owner,byte skill)
    {var c=Activator.CreateInstance(rt.GetNestedType("Cast",BindingFlags.NonPublic),true);Put(c,"Player",owner);Put(c,"Skill",skill);Put(c,"Origin",owner.ComponentCreatureModel.EyePosition);Put(c,"Direction",Vector3.UnitZ);return c;}
    public static void Run(Assembly mod)
    {
        var packetType=mod.GetType("SCIEPort.Features.Excalibur.ExcaliburPackage",true);var rt=mod.GetType("SCIEPort.Features.Excalibur.SubsystemExcalibur",true);
        foreach(var asm in new[]{typeof(IPackage).Assembly,mod})foreach(var t in asm.GetTypes())
        {
            if(t.IsAbstract||!typeof(IPackage).IsAssignableFrom(t)||t==packetType)continue;
            var p=(IPackage)FormatterServices.GetUninitializedObject(t);Check(p.ID!=253,"253 unique against native and all existing mod packages");
        }
        foreach(var t in typeof(Block).Assembly.GetTypes())if(t.IsSubclassOf(typeof(Block)))
        {var f=t.GetField("Index",BindingFlags.Public|BindingFlags.Static|BindingFlags.FlattenHierarchy);if(f!=null&&f.IsLiteral)Check((int)f.GetRawConstantValue()!=953,"953 native ID vacant");}
        foreach(var t in mod.GetTypes())if(t.IsSubclassOf(typeof(Block))&&t.Name!="ExcaliburBlock")
        {var f=t.GetField("Index",BindingFlags.Public|BindingFlags.Static|BindingFlags.FlattenHierarchy);if(f!=null&&f.IsLiteral)Check((int)f.GetRawConstantValue()!=953,"953 mod ID vacant");}
        var packet=(IPackage)Activator.CreateInstance(packetType);
        foreach(uint seq in new[]{1u,127u,128u,uint.MaxValue})
        {
            var values=new Dictionary<string,object>{{"Kind",(byte)1},{"Skill",(byte)3},{"Caster",(ushort)65534},{"Sequence",seq},{"CastId",uint.MaxValue},{"Origin",new Vector3(1,65,-30)},{"Direction",Vector3.UnitZ},{"Cooldowns",new Vector3(10,35,65)},{"Age",2.9f},{"Blessing",8f}};
            foreach(var pair in values)Put(packet,pair.Key,pair.Value);
            using(var writer=new PackageStreamWriter())
            {
                packet.WriteData(writer);Check(writer.BaseStream.Length<=64,"bounded compressed native payload");
                using(var reader=new PackageStreamReader(writer.Data())){var copy=(IPackage)Activator.CreateInstance(packetType);copy.ReadData(reader);foreach(var pair in values)Check(Get(copy,pair.Key).Equals(pair.Value),"native compressed roundtrip "+pair.Key);Check(reader.BaseStream.Position==reader.BaseStream.Length,"all fields consumed");}
            }
        }
        var valid=rt.GetMethod("Valid",BindingFlags.Static|BindingFlags.NonPublic);Check((bool)valid.Invoke(null,new object[]{packet}),"full snapshot accepted");
        foreach(var pair in new Dictionary<string,object>{{"Age",float.NaN},{"Blessing",8.01f},{"Cooldowns",new Vector3(25,35,90.1f)},{"Direction",Vector3.Zero},{"Skill",(byte)4}})
        {var old=Get(packet,pair.Key);Put(packet,pair.Key,pair.Value);Check(!(bool)valid.Invoke(null,new object[]{packet}),"invalid snapshot "+pair.Key);Put(packet,pair.Key,old);}
        Put(packet,"Kind",(byte)3);Check(!(bool)valid.Invoke(null,new object[]{packet}),"impact event cannot impersonate a sanctuary cast");
        Put(packet,"Skill",(byte)0);Put(packet,"CastId",0u);Put(packet,"Age",0f);Check((bool)valid.Invoke(null,new object[]{packet}),"swing confirmation event shape");
        Put(packet,"Age",1f);Check((bool)valid.Invoke(null,new object[]{packet}),"separate holy-proc event shape");Put(packet,"Age",2f);Check(!(bool)valid.Invoke(null,new object[]{packet}),"reject unknown impact phase");Put(packet,"Age",0f);
        var runtime=Activator.CreateInstance(rt);
        for(int i=0;i<100;i++)Call(runtime,"Enqueue",null,packet);
        Check(((ICollection)Get(runtime,"pending")).Count==32,"bounded packet queue");((IDisposable)runtime).Dispose();Check(((ICollection)Get(runtime,"pending")).Count==0,"dispose clears");Call(runtime,"Enqueue",null,packet);Check(((ICollection)Get(runtime,"pending")).Count==0,"late callbacks ignored");
        var previous=CommonLib.WorkType;var oldNet=CommonLib.Net;var hooks=ModsManager.ModHooks;var territories=SubsystemBedrockBlockBehavior.Territories;var air=BlocksManager.Blocks[0];var stone=BlocksManager.Blocks[3];var sword=BlocksManager.Blocks[953];var cells=new Terrain();
        try
        {
            CommonLib.WorkType=WorkType.Local;ModsManager.ModHooks=new Dictionary<string,ModsManager.ModHook>();SubsystemBedrockBlockBehavior.Territories=new Dictionary<Guid,Territoriy>();
            CommonLib.Net=Bare<NetNode>();Put(CommonLib.Net,"PendingPackages",new List<IPackage>());CommonLib.Net.Clients=new Dictionary<int,Client>();
            BlocksManager.Blocks[0]=new AirBlock{BlockIndex=0,IsCollidable=false};BlocksManager.Blocks[3]=new GraniteBlock{BlockIndex=3,IsCollidable=true};
            for(int z=0;z<=32;z+=16)cells.AllocateChunk(0,z).State=TerrainChunkState.Valid;
            var project=new Project();var terrain=Bare<SubsystemTerrain>();Put(terrain,"m_project",project);Put(terrain,"<Terrain>k__BackingField",cells);
            var bodies=new SubsystemBodies();Put(bodies,"m_project",project);project.m_subsystems.Add(terrain);project.m_subsystems.Add(bodies);
            var clock=new SubsystemTime();var info=new SubsystemGameInfo();info.WorldSettings=Bare<WorldSettings>();
            runtime=Activator.CreateInstance(rt);Put(runtime,"m_project",project);Put(runtime,"terrain",terrain);Put(runtime,"bodies",bodies);Put(runtime,"clock",clock);Put(runtime,"info",info);
            var owner=AbyssHostNativeProbe.Player(project,bodies,953);owner.ComponentLocomotion=new ComponentLocomotion();owner.ComponentLocomotion.WalkSpeed=5;
            var recorder=new Recorder();ModsManager.RegisterHook("AttackBody",recorder);
            var core=Npc(project,bodies,new Vector3(8.5f,65,12));var edge=Npc(project,bodies,new Vector3(12,65,12));var miss=Npc(project,bodies,new Vector3(13,65,12));
            var cast=Cast(rt,owner,2);Call(runtime,"Strike",cast);Check(recorder.Powers.Count==2&&recorder.Powers.Contains(55)&&recorder.Powers.Contains(25),"Local one core55 and edge25, width outsider untouched");
            Call(runtime,"Strike",cast);Check(recorder.Powers.Count==2,"same cast cannot hit twice");
            CommonLib.WorkType=WorkType.Client;Call(runtime,"Strike",Cast(rt,owner,2));Check(recorder.Powers.Count==2,"ordinary Client damage forbidden");
            CommonLib.WorkType=WorkType.Server;Call(runtime,"Strike",Cast(rt,owner,2));Check(recorder.Powers.Count==4&&recorder.Powers.Contains(55),"Server second-skill beam 55");
            // Place opaque wall cells across both rays; terrain must remain exactly as the fixture set it.
            for(int x=8;x<=12;x++)cells.SetCellValueFast(x,65,9,3);
            Call(runtime,"Strike",Cast(rt,owner,2));Check(recorder.Powers.Count==4,"wall blocks holy flood damage");Check(cells.GetCellValue(8,65,9)==3,"no terrain mutation");
            for(int x=8;x<=12;x++)cells.SetCellValueFast(x,65,9,0);
            bodies.RemoveBody(edge);bodies.RemoveBody(miss);recorder.Fail=true;cast=Cast(rt,owner,2);
            try{Call(runtime,"Strike",cast);}catch(TargetInvocationException){}
            int count=recorder.Powers.Count;recorder.Fail=false;Call(runtime,"Strike",cast);Check(recorder.Powers.Count==count,"external hook exception cannot retry target damage");
            CommonLib.WorkType=WorkType.Local;var request=Activator.CreateInstance(packetType);Put(request,"Skill",(byte)2);Put(request,"Sequence",1u);Put(request,"Direction",Vector3.UnitZ);
            Call(runtime,"Accept",owner,request);var state=Call(runtime,"Get",owner);Check(Get(state,"Active")!=null&&((double[])Get(state,"Ready"))[1]==35,"native actor accepts and starts second-skill 35s cooldown");
            rt.GetMethod("Pose",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{Get(state,"Active"),1.2f});
            Check(owner.ComponentCreatureModel.AimHandAngleOrder>2&&owner.ComponentCreatureModel.InHandItemOffsetOrder.Y>0,"native third-person arms and blade raised together");
            Call(runtime,"Accept",owner,request);Check(Get(state,"NextCast").Equals(1u),"duplicate request does not consume/recast");
            Call(runtime,"ApplySpeed",state,true);Check(owner.ComponentLocomotion.WalkSpeed==6,"owned movement bonus");for(int i=0;i<100;i++)Call(runtime,"ApplySpeed",state,true);Check(owner.ComponentLocomotion.WalkSpeed==6,"no repeated speed drift");
            Call(runtime,"ApplySpeed",state,false);Check(owner.ComponentLocomotion.WalkSpeed==5,"exact speed restoration");
            var clear=rt.GetMethod("ClearCast",BindingFlags.Static|BindingFlags.NonPublic);clear.Invoke(null,new[]{state});Put(request,"Sequence",2u);Call(runtime,"Accept",owner,request);Check(Get(state,"Active")==null,"cooldown blocks fresh sequence");
            Check(owner.ComponentCreatureModel.AimHandAngleOrder==0&&owner.ComponentCreatureModel.InHandItemOffsetOrder==Vector3.Zero,"native arms and item orders restored on cancellation");
            Put(request,"Skill",(byte)1);Put(request,"Sequence",3u);Call(runtime,"Accept",owner,request);Check((double)Get(state,"BlessingEnd")==8&&((double[])Get(state,"Ready"))[0]==25,"enchantment duration8 cooldown25");
            clear.Invoke(null,new[]{state});
            core.Position=new Vector3(8.5f,65,8.0f);core.Entity.FindComponent<ComponentHealth>(true).AttackResilience=100;
            object[] targetArgs={owner,Vector3.UnitZ,Vector3.Zero};
            Check(ReferenceEquals(Call(runtime,"EnchantedTarget",targetArgs),core),"enchanted ray reaches body beyond native two blocks");
            core.Position=new Vector3(8.5f,65,8.81f);targetArgs[2]=Vector3.Zero;
            Check(Call(runtime,"EnchantedTarget",targetArgs)==null,"hard three-block reach, no collider padding beyond limit");
            core.Position=new Vector3(8.5f,65,8.0f);cells.SetCellValueFast(8,65,7,3);targetArgs[2]=Vector3.Zero;
            Check(Call(runtime,"EnchantedTarget",targetArgs)==null,"enchanted melee does not cross walls");cells.SetCellValueFast(8,65,7,0);
            BlocksManager.Blocks[953]=new GraniteBlock{BlockIndex=953,DefaultMeleePower=24,DefaultMeleeHitProbability=1,Durability=-1};info.WorldSettings.GameMode=GameMode.Creative;
            owner.ComponentMiner.ComponentCreature=owner;owner.ComponentMiner.AttackPower=1;owner.ComponentMiner.m_subsystemTime=clock;owner.ComponentMiner.m_subsystemGameInfo=info;
            owner.m_subsystemPlayerStats=new SubsystemPlayerStats();
            clock.m_gameTime=1;Put(request,"Kind",(byte)2);Put(request,"Skill",(byte)0);Put(request,"Sequence",4u);
            CommonLib.WorkType=WorkType.Server;int hitsBefore=recorder.Powers.Count;Call(runtime,"Accept",owner,request);
            Check(recorder.Powers.Count==hitsBefore+1&&recorder.Powers[recorder.Powers.Count-1]>0,"authoritative extended-melee request enters actual native Miner.Hit and AttackBody");
            Call(runtime,"Accept",owner,request);Check(recorder.Powers.Count==hitsBefore+1,"duplicate melee sequence cannot replay physical attack");
            Put(request,"Sequence",5u);Call(runtime,"Accept",owner,request);Check(recorder.Powers.Count==hitsBefore+1,"fresh melee packet cannot bypass native/server attack cadence");
            Check(((IList)Get(CommonLib.Net,"PendingPackages")).Count==1,"server confirms one extended-melee swing for clients");
            clock.m_gameTime=0;
            var fp=new ComponentFirstPersonModel();fp.m_entity=owner.Entity;((List<Component>)Get(owner.Entity,"m_components")).Add(fp);
            Check((bool)Call(runtime,"EnchantedDraw",fp.m_drawBlockEnvironmentData)&&!(bool)Call(runtime,"EnchantedDraw",new DrawBlockEnvironmentData()),"gold blade emission bound to actual held-model owner, not inventory");
            var injury=new InjuryRecorder();ModsManager.RegisterHook("OnCreatureInjure",injury);
            Call(runtime,"PrepareMelee",core,owner,24f,true);Call(runtime,"PrepareMelee",core,owner,24f,true);
            Check(((ICollection)Get(runtime,"addons")).Count==1,"one pending add-on for repeated hook");
            core.Entity.FindComponent<ComponentHealth>(true).Health=.8f;clock.m_gameTime=.1;
            Call(runtime,"ApplyAddons");Call(runtime,"ApplyAddons");
            Check(injury.Count==1&&Math.Abs(injury.Amount-.15f)<.0001f,"Server: one separate 15/resilience holy injury after observed physical damage");
            var events=(List<IPackage>)Get(CommonLib.Net,"PendingPackages");Check(events.Count==3,"server emits separate proc flash and native gold 15 display");
            Check((float)Get(events[1],"Age")==1,"proc confirmation does not replay the swing animation");
            clock.m_gameTime=1;Call(runtime,"PrepareMelee",core,owner,24f,true);clock.m_gameTime=1.1;Call(runtime,"ApplyAddons");
            Check(injury.Count==1,"blocked or zero-health-loss physical hit does not proc");
            Call(runtime,"PrepareMelee",core,owner,55f,false);Check(((ICollection)Get(runtime,"addons")).Count==0,"holy beam cannot recursively proc enchantment");
            CommonLib.WorkType=WorkType.Client;Call(runtime,"PrepareMelee",core,owner,24f,true);Check(((ICollection)Get(runtime,"addons")).Count==0,"Client never schedules holy damage");CommonLib.WorkType=WorkType.Local;
            var avalon=Cast(rt,owner,3);Put(avalon,"Start",2d);Put(avalon,"HealMaximum",1f);Put(state,"Active",avalon);owner.ComponentHealth.Health=.5f;
            clock.m_gameTime=2.59;Check(!(bool)Call(runtime,"Protects",owner.ComponentHealth),"Avalon windup remains vulnerable");
            clock.m_gameTime=2.6;Check((bool)Call(runtime,"Protects",owner.ComponentHealth)&&!owner.ComponentHealth.IsInvulnerable,"Avalon shields via scoped hook without mutating global invulnerability");
            project.m_subsystems.Add((Subsystem)runtime);
            var loader=Activator.CreateInstance(mod.GetType("SCIEPort.Mod.SCIEPortLoader",true));object[] injuryArgs={owner.ComponentHealth,1f,null,true,"fixture direct injury",false};Call(loader,"OnCreatureInjure",injuryArgs);
            Check((bool)injuryArgs[5],"actual loader blocks direct injury, including ignoreInvulnerability, during sanctuary");
            object[] attackArgs={owner.ComponentBody,owner,owner.ComponentBody.Position,Vector3.UnitZ,55f,false};
            Check((bool)Call(loader,"AttackBody",attackArgs)&&(float)attackArgs[4]==0,"actual loader blocks native attack before armor/impulse");
            clock.m_gameTime=5.6;Check(!(bool)Call(runtime,"Protects",owner.ComponentHealth),"three-second invulnerability expires");Call(runtime,"HealAvalon",avalon,3.6f);
            Check(Math.Abs(owner.ComponentHealth.Health-.62f)<.0001f,"Avalon returns 12 percent normalized maximum health");
            clock.m_gameTime=6;Call(runtime,"HealAvalon",avalon,4.2f);Check(Math.Abs(owner.ComponentHealth.Health-.62f)<.0001f,"repeated/catch-up healing does not exceed budget");
            owner.ComponentHealth.Health=.98f;Put(avalon,"Healed",0f);clock.m_gameTime=7;Call(runtime,"HealAvalon",avalon,3.6f);Check(owner.ComponentHealth.Health==1,"healing clamps to current equipment maximum");
            CommonLib.WorkType=WorkType.Client;Put(state,"LastSnapshot",0d);clock.m_gameTime=3;
            Check(!(bool)Call(runtime,"Protects",owner.ComponentHealth),"stale Client lease cannot keep immunity");CommonLib.WorkType=WorkType.Local;
            ((ComponentInventory)owner.ComponentMiner.Inventory).m_slots[0]=new ComponentInventoryBase.Slot{Value=952,Count=1};
            owner.ComponentCreatureModel.InHandItemOffsetOrder=new Vector3(.1f,.2f,.3f);
            clear.Invoke(null,new[]{state});Check(Get(state,"Active")==null,"cast/pose cleanup");
            Check(owner.ComponentCreatureModel.InHandItemOffsetOrder==new Vector3(.1f,.2f,.3f),"switching to another artifact preserves that artifact's freshly submitted pose");
            bodies.RemoveBody(core);
        }
        finally{CommonLib.WorkType=previous;CommonLib.Net=oldNet;ModsManager.ModHooks=hooks;SubsystemBedrockBlockBehavior.Territories=territories;BlocksManager.Blocks[0]=air;BlocksManager.Blocks[3]=stone;BlocksManager.Blocks[953]=sword;cells.Dispose();}
        Console.WriteLine("Excalibur target-native protocol/IDs/Local-Server-Client damage/once-only/wall/cooldown/passive: "+checks+" assertions passed; recorded AttackBody only, not gameplay.");
    }
}

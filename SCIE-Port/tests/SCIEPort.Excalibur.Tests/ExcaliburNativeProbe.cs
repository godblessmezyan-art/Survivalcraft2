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
        foreach(var pair in new Dictionary<string,object>{{"Age",float.NaN},{"Blessing",8.01f},{"Cooldowns",new Vector3(10,35,65.1f)},{"Direction",Vector3.Zero},{"Skill",(byte)4}})
        {var old=Get(packet,pair.Key);Put(packet,pair.Key,pair.Value);Check(!(bool)valid.Invoke(null,new object[]{packet}),"invalid snapshot "+pair.Key);Put(packet,pair.Key,old);}
        var runtime=Activator.CreateInstance(rt);
        for(int i=0;i<100;i++)Call(runtime,"Enqueue",null,packet);
        Check(((ICollection)Get(runtime,"pending")).Count==32,"bounded packet queue");((IDisposable)runtime).Dispose();Check(((ICollection)Get(runtime,"pending")).Count==0,"dispose clears");Call(runtime,"Enqueue",null,packet);Check(((ICollection)Get(runtime,"pending")).Count==0,"late callbacks ignored");
        var previous=CommonLib.WorkType;var hooks=ModsManager.ModHooks;var territories=SubsystemBedrockBlockBehavior.Territories;var air=BlocksManager.Blocks[0];var stone=BlocksManager.Blocks[3];var cells=new Terrain();
        try
        {
            CommonLib.WorkType=WorkType.Local;ModsManager.ModHooks=new Dictionary<string,ModsManager.ModHook>();SubsystemBedrockBlockBehavior.Territories=new Dictionary<Guid,Territoriy>();
            BlocksManager.Blocks[0]=new AirBlock{BlockIndex=0,IsCollidable=false};BlocksManager.Blocks[3]=new GraniteBlock{BlockIndex=3,IsCollidable=true};
            for(int z=0;z<=32;z+=16)cells.AllocateChunk(0,z).State=TerrainChunkState.Valid;
            var project=new Project();var terrain=Bare<SubsystemTerrain>();Put(terrain,"m_project",project);Put(terrain,"<Terrain>k__BackingField",cells);
            var bodies=new SubsystemBodies();Put(bodies,"m_project",project);project.m_subsystems.Add(terrain);project.m_subsystems.Add(bodies);
            var clock=new SubsystemTime();var info=new SubsystemGameInfo();info.WorldSettings=Bare<WorldSettings>();
            runtime=Activator.CreateInstance(rt);Put(runtime,"m_project",project);Put(runtime,"terrain",terrain);Put(runtime,"bodies",bodies);Put(runtime,"clock",clock);Put(runtime,"info",info);
            var owner=AbyssHostNativeProbe.Player(project,bodies,953);owner.ComponentLocomotion=new ComponentLocomotion();owner.ComponentLocomotion.WalkSpeed=5;
            var recorder=new Recorder();ModsManager.RegisterHook("AttackBody",recorder);
            var core=Npc(project,bodies,new Vector3(8.5f,65,12));var edge=Npc(project,bodies,new Vector3(12,65,12));var miss=Npc(project,bodies,new Vector3(13,65,12));
            var cast=Cast(rt,owner,3);Call(runtime,"Strike",cast);Check(recorder.Powers.Count==2&&recorder.Powers.Contains(55)&&recorder.Powers.Contains(25),"Local one core55 and edge25, width outsider untouched");
            Call(runtime,"Strike",cast);Check(recorder.Powers.Count==2,"same cast cannot hit twice");
            CommonLib.WorkType=WorkType.Client;Call(runtime,"Strike",Cast(rt,owner,3));Check(recorder.Powers.Count==2,"ordinary Client damage forbidden");
            CommonLib.WorkType=WorkType.Server;Call(runtime,"Strike",Cast(rt,owner,1));Check(recorder.Powers.Count==3&&recorder.Powers[2]==18,"Server lake strike 18");
            // Place opaque wall cells across both rays; terrain must remain exactly as the fixture set it.
            for(int x=8;x<=12;x++)cells.SetCellValueFast(x,65,9,3);
            Call(runtime,"Strike",Cast(rt,owner,3));Check(recorder.Powers.Count==3,"wall blocks holy flood damage");Check(cells.GetCellValue(8,65,9)==3,"no terrain mutation");
            for(int x=8;x<=12;x++)cells.SetCellValueFast(x,65,9,0);
            bodies.RemoveBody(edge);bodies.RemoveBody(miss);recorder.Fail=true;cast=Cast(rt,owner,1);
            try{Call(runtime,"Strike",cast);}catch(TargetInvocationException){}
            int count=recorder.Powers.Count;recorder.Fail=false;Call(runtime,"Strike",cast);Check(recorder.Powers.Count==count,"external hook exception cannot retry target damage");
            CommonLib.WorkType=WorkType.Local;var request=Activator.CreateInstance(packetType);Put(request,"Skill",(byte)3);Put(request,"Sequence",1u);Put(request,"Direction",Vector3.UnitZ);
            Call(runtime,"Accept",owner,request);var state=Call(runtime,"Get",owner);Check(Get(state,"Active")!=null&&((double[])Get(state,"Ready"))[2]==65,"native actor accepts and starts 65s cooldown");
            rt.GetMethod("Pose",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{Get(state,"Active"),1.2f});
            Check(owner.ComponentCreatureModel.AimHandAngleOrder>2&&owner.ComponentCreatureModel.InHandItemOffsetOrder.Y>0,"native third-person arms and blade raised together");
            Call(runtime,"Accept",owner,request);Check(Get(state,"NextCast").Equals(1u),"duplicate request does not consume/recast");
            Call(runtime,"ApplySpeed",state,true);Check(owner.ComponentLocomotion.WalkSpeed==6,"owned movement bonus");for(int i=0;i<100;i++)Call(runtime,"ApplySpeed",state,true);Check(owner.ComponentLocomotion.WalkSpeed==6,"no repeated speed drift");
            Call(runtime,"ApplySpeed",state,false);Check(owner.ComponentLocomotion.WalkSpeed==5,"exact speed restoration");
            var clear=rt.GetMethod("ClearCast",BindingFlags.Static|BindingFlags.NonPublic);clear.Invoke(null,new[]{state});Put(request,"Sequence",2u);Call(runtime,"Accept",owner,request);Check(Get(state,"Active")==null,"cooldown blocks fresh sequence");
            Check(owner.ComponentCreatureModel.AimHandAngleOrder==0&&owner.ComponentCreatureModel.InHandItemOffsetOrder==Vector3.Zero,"native arms and item orders restored on cancellation");
            Put(request,"Skill",(byte)2);Put(request,"Sequence",3u);Call(runtime,"Accept",owner,request);Check((double)Get(state,"BlessingEnd")==8&&((double[])Get(state,"Ready"))[1]==35,"blessing duration8 cooldown35");
            ((ComponentInventory)owner.ComponentMiner.Inventory).m_slots[0]=new ComponentInventoryBase.Slot{Value=952,Count=1};
            owner.ComponentCreatureModel.InHandItemOffsetOrder=new Vector3(.1f,.2f,.3f);
            clear.Invoke(null,new[]{state});Check(Get(state,"Active")==null,"cast/pose cleanup");
            Check(owner.ComponentCreatureModel.InHandItemOffsetOrder==new Vector3(.1f,.2f,.3f),"switching to another artifact preserves that artifact's freshly submitted pose");
            bodies.RemoveBody(core);
        }
        finally{CommonLib.WorkType=previous;ModsManager.ModHooks=hooks;SubsystemBedrockBlockBehavior.Territories=territories;BlocksManager.Blocks[0]=air;BlocksManager.Blocks[3]=stone;cells.Dispose();}
        Console.WriteLine("Excalibur target-native protocol/IDs/Local-Server-Client damage/once-only/wall/cooldown/passive: "+checks+" assertions passed; recorded AttackBody only, not gameplay.");
    }
}

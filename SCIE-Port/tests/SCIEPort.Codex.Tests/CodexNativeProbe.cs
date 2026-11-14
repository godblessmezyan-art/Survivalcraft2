using System;using System.Collections.Generic;using System.Collections;using System.Reflection;using System.Runtime.Serialization;using System.Xml.Linq;
using Engine;using Game;using Game.NetWork;using GameEntitySystem;using TemplatesDatabase;
public static class CodexNativeProbe
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;static int checks;
    static void Check(bool b,string label){checks++;if(!b)throw new Exception("Codex: "+label);}
    static void Put(object o,string n,object v){for(var t=o.GetType();t!=null;t=t.BaseType){var f=t.GetField(n,F);if(f!=null){f.SetValue(o,v);return;}}throw new Exception(n);}
    static object Get(object o,string n){return o.GetType().GetField(n,F).GetValue(o);}
    static object Call(object o,string n,params object[] a){return o.GetType().GetMethod(n,F).Invoke(o,a);}
    public static void Run(Assembly mod,XElement xml)
    {
        var type=mod.GetType("SCIEPort.Features.Codex.SubsystemArtifactCodex",true);var packetType=mod.GetType("SCIEPort.Features.Codex.CodexPackage",true);var bookType=mod.GetType("SCIEPort.Features.Codex.ArtifactCodexBlock",true);
        foreach(var asm in new[]{typeof(Block).Assembly,mod})foreach(var t in asm.GetTypes())
        {
            if(!t.IsAbstract&&typeof(IPackage).IsAssignableFrom(t)&&t!=packetType)Check(((IPackage)FormatterServices.GetUninitializedObject(t)).ID!=254,"254 unique against native and mod packages");
            if(t.IsSubclassOf(typeof(Block))&&t!=bookType){var f=t.GetField("Index",BindingFlags.Public|BindingFlags.Static|BindingFlags.FlattenHierarchy);if(f!=null&&f.IsLiteral)Check((int)f.GetRawConstantValue()!=955,"955 vacant");}
        }
        var db=XmlDatabaseSerializer.LoadDatabase(xml);var gd=new GameDatabase(db);var values=new ValuesDictionary();values.PopulateFromDatabaseObject(db.FindDatabaseObject("SCIEPortArtifactCodex",gd.SubsystemTemplateType,true));
        Check(values.GetValue<string>("Class")==type.FullName,"real merged database registers required world subsystem");
        var prior=CommonLib.WorkType;var oldNet=CommonLib.Net;
        try
        {
            CommonLib.WorkType=WorkType.Local;var project=new Project();var players=new SubsystemPlayers();Put(players,"m_project",project);project.m_subsystems.Add(players);
            var clock=new SubsystemTime();clock.m_gameTimeDelta=.05f;Put(clock,"m_project",project);project.m_subsystems.Add(clock);
            var bodies=new SubsystemBodies();Put(bodies,"m_project",project);project.m_subsystems.Add(bodies);
            var terrain=(SubsystemTerrain)FormatterServices.GetUninitializedObject(typeof(SubsystemTerrain));Put(terrain,"m_project",project);project.m_subsystems.Add(terrain);
            var p=AbyssHostNativeProbe.Player(project,bodies,955);players.m_componentPlayers.Add(p);p.ComponentLocomotion=new ComponentLocomotion{WalkSpeed=5};p.ComponentInput=new ComponentInput();p.ComponentInput.m_entity=p.Entity;p.ComponentInput.m_componentPlayer=p;
            Put(p.PlayerData,"m_isMainPlayer",false);p.ComponentMiner.ComponentCreature=p;p.ComponentMiner.ComponentPlayer=p;
            var runtime=project.FindSubsystem(type,null,true);Call(runtime,"Load",values);
            var records=(Dictionary<Guid,uint>)Get(runtime,"Records");records.Clear();
            var allowed=type.GetMethod("Allowed",BindingFlags.Static|BindingFlags.NonPublic);
            int[] ids={942,943,948,949,950,951,952,953};
            foreach(int id in ids)Check((bool)allowed.Invoke(null,new object[]{p,id}),"artifact usable without true name "+id);
            Check((bool)allowed.Invoke(null,new object[]{p,955})&&(bool)allowed.Invoke(null,new object[]{p,3}),"book and ordinary items remain usable");
            var inv=(ComponentInventory)p.ComponentMiner.Inventory;
            inv.m_slots[0].Value=953;p.ComponentInput.m_playerInput.Hit=new Ray3(Vector3.Zero,Vector3.UnitZ);p.ComponentInput.m_playerInput.Dig=p.ComponentInput.m_playerInput.Hit;p.ComponentInput.m_playerInput.Aim=p.ComponentInput.m_playerInput.Hit;
            Call(runtime,"Update",.05f);Check(p.ComponentInput.m_playerInput.Hit.HasValue&&p.ComponentInput.m_playerInput.Dig.HasValue&&p.ComponentInput.m_playerInput.Aim.HasValue,"artifact input passes through without true name");
            var loader=Activator.CreateInstance(mod.GetType("SCIEPort.Mod.SCIEPortLoader"));object[] hit={p.ComponentMiner,null,Vector3.Zero,Vector3.UnitZ,99f,1f,1f,false};Call(loader,"OnMinerHit",hit);Check((float)hit[4]==99&&!(bool)hit[7],"actual miner hook keeps artifact attack power without true name");
            object[] dig={p.ComponentMiner,new TerrainRaycastResult(),2f,false};Call(loader,"OnMinerDig",dig);Check((float)dig[2]==2f&&!(bool)dig[3],"actual native dig hook leaves artifact digging untouched without true name");
            var q=Activator.CreateInstance(packetType);Put(q,"Owner",p.PlayerGuid);Put(q,"Caster",p.Entity.EntityId);Put(q,"Sequence",1u);Put(q,"Kind",(byte)2);Put(q,"Entry",(byte)0);Put(q,"Mask",255u);
            Call(runtime,"Accept",p,q);Check(records.Count==0,"unlock rejected without held book");
            inv.m_slots[0].Value=955;Put(q,"Owner",Guid.NewGuid());Call(runtime,"Accept",p,q);Check(records.Count==0,"spoofed player GUID rejected");Put(q,"Owner",p.PlayerGuid);
            Put(q,"Entry",(byte)9);Call(runtime,"Accept",p,q);Check(records.Count==0,"unknown catalog index rejected");Put(q,"Entry",(byte)0);
            CommonLib.WorkType=WorkType.Client;Call(runtime,"Accept",p,q);Check(records.Count==0,"Client cannot grant itself unlocks");CommonLib.WorkType=WorkType.Local;
            for(byte i=0;i<8;i++){clock.m_gameTime=i;Put(q,"Entry",i);Put(q,"Sequence",(uint)i+1);Call(runtime,"Accept",p,q);Check((bool)allowed.Invoke(null,new object[]{p,ids[i]}),"authority grants selected true name "+ids[i]);Check(records[p.PlayerGuid]==(uint)((1<<(i+1))-1),"packet mask cannot unlock unrelated entries");}
            var other=AbyssHostNativeProbe.Player(project,bodies,955);records.Remove(other.PlayerGuid);Check((bool)allowed.Invoke(null,new object[]{other,953}),"every player can use artifacts without per-player unlock");Check(!records.ContainsKey(other.PlayerGuid),"unlock records stay per player");
            var save=new ValuesDictionary();Call(runtime,"Save",save);var saveXml=new XElement("Values");save.Save(saveXml);var reloaded=new ValuesDictionary();reloaded.ApplyOverrides(XElement.Parse(saveXml.ToString()));
            var fresh=Activator.CreateInstance(type);Call(fresh,"ReadRecords",reloaded.GetValue<ValuesDictionary>("TrueNames"));Check(((Dictionary<Guid,uint>)Get(fresh,"Records"))[p.PlayerGuid]==255,"native ValuesDictionary XML save/load preserves all true names");
            var malformed=new ValuesDictionary();malformed.SetValue("not-guid","255");malformed.SetValue(Guid.NewGuid().ToString("N"),"-1");Call(fresh,"ReadRecords",malformed);Check(((Dictionary<Guid,uint>)Get(fresh,"Records")).Count==1,"malformed progress never grants access");
            var loaded=Activator.CreateInstance(type);Put(loaded,"m_project",project);Call(loaded,"Load",values);
            Check(((Dictionary<Guid,uint>)Get(loaded,"Records")).Count==0,"real subsystem Load accepts an old world without TrueNames");
            values.SetValue("TrueNames",reloaded.GetValue<ValuesDictionary>("TrueNames"));Call(loaded,"Load",values);
            Check(((Dictionary<Guid,uint>)Get(loaded,"Records"))[p.PlayerGuid]==255,"real subsystem Load restores existing true names without changing save format");
            var mixed=new ValuesDictionary();var valid=Guid.NewGuid();mixed.SetValue("invalid-id","255");mixed.SetValue(Guid.Empty.ToString("N"),"255");mixed.SetValue(valid.ToString("D"),"128");
            values.SetValue("TrueNames",mixed);var mixedLoad=Activator.CreateInstance(type);Put(mixedLoad,"m_project",project);Call(mixedLoad,"Load",values);
            var mixedRecords=(Dictionary<Guid,uint>)Get(mixedLoad,"Records");Check(mixedRecords.Count==1&&mixedRecords[valid]==128,"mixed malformed save skips invalid/empty IDs and preserves the following valid player");
            foreach(string maskText in new[]{"0","128","255","4294967295","-1","4294967296","not-number"})
            {
                var data=new ValuesDictionary();data.SetValue(valid.ToString("N"),maskText);values.SetValue("TrueNames",data);var loadMask=Activator.CreateInstance(type);Put(loadMask,"m_project",project);Call(loadMask,"Load",values);
                var loadedRecords=(Dictionary<Guid,uint>)Get(loadMask,"Records");uint expected;bool ok=uint.TryParse(maskText,out expected);
                Check(ok?loadedRecords.Count==1&&loadedRecords[valid]==(expected&511):loadedRecords.Count==0,"actual world Load preserves/filters permission mask: "+maskText);
            }
            inv.m_slots[0].Value=955;for(int i=0;i<20;i++)Call(runtime,"Update",.05f);Check(p.ComponentLocomotion.WalkSpeed==6,"book movement bonus no accumulation");inv.m_slots[0].Value=3;Call(runtime,"Update",.05f);Check(p.ComponentLocomotion.WalkSpeed==5,"book movement bonus fully restored on switch");
            p.ComponentHealth.Health=0;Check(records[p.PlayerGuid]==255,"death does not erase world progress");p.ComponentHealth.Health=1;
            inv.m_slots[0].Value=949;Call(runtime,"Update",.05f);Check(p.ComponentLocomotion.WalkSpeed==6,"unsealed utility eye receives default movement bonus");
            p.ComponentHealth.Health=0;Call(runtime,"Update",.05f);Check(p.ComponentLocomotion.WalkSpeed==5,"death restores utility-eye movement increment");p.ComponentHealth.Health=1;
            CommonLib.Net=(NetNode)FormatterServices.GetUninitializedObject(typeof(NetNode));Put(CommonLib.Net,"PendingPackages",new List<IPackage>());CommonLib.Net.Clients=new Dictionary<int,Client>();
            CommonLib.WorkType=WorkType.Server;records[p.PlayerGuid]=0;inv.m_slots[0].Value=955;clock.m_gameTime=20;Put(q,"Sequence",100u);Put(q,"Entry",(byte)7);Call(runtime,"Accept",p,q);
            var snapshots=(List<IPackage>)Get(CommonLib.Net,"PendingPackages");Check(records[p.PlayerGuid]==128&&snapshots.Count==1&&(uint)Get(snapshots[0],"Mask")==128,"Server grants one entry and publishes authoritative mask");
            CommonLib.WorkType=WorkType.Client;records.Clear();Call(runtime,"Receive",snapshots[0]);Check(records[p.PlayerGuid]==128,"late Client snapshot restores progress without local granting");
            var snapshot=Activator.CreateInstance(packetType);Put(snapshot,"Kind",(byte)1);Put(snapshot,"Owner",p.PlayerGuid);Put(snapshot,"Caster",p.Entity.EntityId);Put(snapshot,"Sequence",200u);Put(snapshot,"Mask",512u);Call(runtime,"Receive",snapshot);Check(records[p.PlayerGuid]==128,"Client rejects unknown permission bits");
            Put(snapshot,"Mask",255u);Put(snapshot,"Owner",Guid.NewGuid());Call(runtime,"Receive",snapshot);Check(records[p.PlayerGuid]==128,"Client ignores state for an unbound player identity");
            Put(snapshot,"Owner",p.PlayerGuid);Put(snapshot,"Sequence",1u);Put(snapshot,"Mask",0u);Call(runtime,"Receive",snapshot);Check(records[p.PlayerGuid]==128,"replayed snapshots cannot roll back progress");
            var writer=new PackageStreamWriter();((IPackage)q).WriteData(writer);Check(writer.BaseStream.Length<=40,"bounded native compressed payload");var copy=(IPackage)Activator.CreateInstance(packetType);using(var reader=new PackageStreamReader(writer.Data()))copy.ReadData(reader);foreach(string field in new[]{"Kind","Entry","Caster","Owner","Sequence","Mask"})Check(Get(copy,field).Equals(Get(q,field)),"native package roundtrip "+field);writer.Dispose();
            var mesh=new BlockMesh();bookType.GetMethod("Build",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{mesh});Check(mesh.Indices.Count/3>300&&mesh.Indices.Count/3<700,"independent detailed book model within fixed budget");
            // Enter through the real packet handler, with the same GUID on the
            // connection, PlayerData and entity (rather than calling Accept).
            CommonLib.WorkType=WorkType.Server;records.Clear();snapshots.Clear();
            p.Entity.EntityId=37;p.PlayerData.PlayerGUID=p.PlayerGuid;
            var remote=new Client(p.PlayerGuid,null);Put(remote,"<ID>k__BackingField",(byte)2);Put(remote,"<SubsystemPlayers>k__BackingField",players);remote.State=ClientState.Playing;
            players.m_playersData.Add(p.PlayerData);CommonLib.Net.Clients[2]=remote;
            var proxy=(ProjectNet)FormatterServices.GetUninitializedObject(typeof(ProjectNet));Put(proxy,"m_subsystems",project.m_subsystems);
            inv.m_slots[0].Value=955;clock.m_gameTime=30;Put(q,"Owner",p.PlayerGuid);Put(q,"Caster",p.Entity.EntityId);Put(q,"Sequence",201u);Put(q,"Entry",(byte)6);((IPackage)q).From=remote;
            ((IPackage)q).Handle(proxy,CommonLib.Net,true);Call(runtime,"Update",.05f);
            Check(records.ContainsKey(p.PlayerGuid)&&records[p.PlayerGuid]==64,"real remote handler unseals only its authenticated player");
            CommonLib.WorkType=WorkType.Client;records.Clear();snapshots.Clear();
            var server=NativeServerSession.Install(CommonLib.Net);
            Check(!ReferenceEquals(server,CommonLib.Net.Server)&&ReferenceEquals(server.Peer,CommonLib.Net.Server.Peer),"native SyncList creates distinct server Clients for the same transport");
            Put(snapshot,"Caster",p.Entity.EntityId);Put(snapshot,"Sequence",300u);Put(snapshot,"Mask",128u);((IPackage)snapshot).From=server;
            ((IPackage)snapshot).Handle(proxy,CommonLib.Net,false);Call(runtime,"Update",.05f);
            Check(records.ContainsKey(p.PlayerGuid)&&records[p.PlayerGuid]==128,"native bootstrap split still delivers authoritative true-name reply");
            var impostor=new Client(server.Peer,server.ID,server.TokenId,server.GUID,null,"-1","impostor");
            Put(snapshot,"Sequence",301u);Put(snapshot,"Mask",255u);((IPackage)snapshot).From=impostor;
            ((IPackage)snapshot).Handle(proxy,CommonLib.Net,false);Call(runtime,"Update",.05f);
            Check(records[p.PlayerGuid]==128,"same ID, GUID, token and peer cannot substitute an unregistered sender");
            ((IPackage)snapshot).From=server;((IPackage)snapshot).Handle(proxy,CommonLib.Net,false);
            var nextServer=NativeServerSession.Install(CommonLib.Net);Call(runtime,"Update",.05f);
            Check(records.Count==0,"reconnect drops old queued masks and permission revision");
            Put(snapshot,"Sequence",1u);Put(snapshot,"Mask",64u);((IPackage)snapshot).From=nextServer;
            ((IPackage)snapshot).Handle(proxy,CommonLib.Net,false);Call(runtime,"Update",.05f);
            Check(records.ContainsKey(p.PlayerGuid)&&records[p.PlayerGuid]==64,"new session can start permission revisions at one");
            ((IDisposable)runtime).Dispose();Check(records.Count==0,"world exit clears runtime permissions");
        }
        finally{CommonLib.WorkType=prior;CommonLib.Net=oldNet;}
        Console.WriteLine("Codex native identity/database/permissions/authentication/input/passive/save/model: "+checks+" checks passed; no live world/device.");
    }
}

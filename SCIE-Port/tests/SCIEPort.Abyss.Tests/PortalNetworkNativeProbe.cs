using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using Engine;
using Game;
using Game.NetWork;
using GameEntitySystem;
public static class PortalNetworkNativeProbe
{
    const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
    static int checks;
    static void Check(bool b,string s){checks++;if(!b)throw new Exception("Portal network: "+s);}
    static T Bare<T>(){return (T)FormatterServices.GetUninitializedObject(typeof(T));}
    static FieldInfo Field(Type t,string n){for(;t!=null;t=t.BaseType){var f=t.GetField(n,Flags);if(f!=null)return f;}throw new Exception(n);}
    static void Put(object o,string n,object v){Field(o.GetType(),n).SetValue(o,v);}
    static object Get(object o,string n){return Field(o.GetType(),n).GetValue(o);}
    static object Call(object o,string n,params object[] a){return o.GetType().GetMethod(n,Flags).Invoke(o,a);}
    static object New(Type t,params object[] a){return Activator.CreateInstance(t,Flags,null,a,null);}
    static object Packet(Type t,byte command,ComponentPlayer p,uint sequence,Guid source,Guid target,string text)
    {var x=New(t);Put(x,"Kind",(byte)0);Put(x,"Command",command);Put(x,"Caster",(ushort)(p.Entity==null?0:p.Entity.EntityId));Put(x,"Sequence",sequence);Put(x,"Portal",source);Put(x,"Target",target);Put(x,"Text",text);return x;}
    sealed class Fixture:IDisposable
    {
        internal Project Project=new Project();internal Terrain Cells=new Terrain();internal SubsystemTerrain Terrain;
        internal SubsystemBodies Bodies=new SubsystemBodies();internal SubsystemTime Clock=new SubsystemTime();
        internal SubsystemPlayers Players=new SubsystemPlayers();internal ComponentPlayer Player;internal Client Client;
        internal object Owner,Registry,Net,Travel;internal Type PacketType;
        internal Fixture(Assembly mod,bool main)
        {
            PacketType=mod.GetType("SCIEPort.Features.AbyssReaper.VoidPortalPackage",true);
            Terrain=Bare<SubsystemTerrain>();Put(Terrain,"m_project",Project);Put(Terrain,"<Terrain>k__BackingField",Cells);
            var updater=Bare<TerrainUpdater>();var param=Field(typeof(TerrainUpdater),"m_updateParameters");var data=New(param.FieldType);
            data.GetType().GetField("Locations").SetValue(data,new Dictionary<int,TerrainUpdater.UpdateLocation>());param.SetValue(updater,data);
            Put(updater,"m_pendingLocations",new Dictionary<int,TerrainUpdater.UpdateLocation?>());Put(Terrain,"<TerrainUpdater>k__BackingField",updater);
            Cells.AllocateChunk(0,0).State=TerrainChunkState.Valid;
            for(int x=0;x<16;x++)for(int z=0;z<16;z++)Cells.SetCellValueFast(x,63,z,3);
            Put(Bodies,"m_project",Project);Put(Players,"m_project",Project);
            var moving=Bare<SubsystemMovingBlocks>();Put(moving,"m_project",Project);moving.m_movingBlockSets=new List<SubsystemMovingBlocks.MovingBlockSet>();moving.m_result=new DynamicArray<IMovingBlockSet>();
            Project.m_subsystems.Add(Terrain);Project.m_subsystems.Add(Bodies);Project.m_subsystems.Add(Players);Project.m_subsystems.Add(moving);
            Owner=New(mod.GetType("SCIEPort.Features.AbyssReaper.SubsystemAbyssReaper",true));Put(Owner,"m_project",Project);Put(Owner,"terrain",Terrain);Put(Owner,"bodies",Bodies);Put(Owner,"clock",Clock);
            Project.m_subsystems.Add((Subsystem)Owner);
            Registry=New(mod.GetType("SCIEPort.Features.AbyssReaper.VoidPortalRegistry",true),Guid.NewGuid(),12);Put(Owner,"<Portals>k__BackingField",Registry);
            int index=(int)mod.GetType("SCIEPort.Features.AbyssReaper.AbyssReaperBlock").GetField("Index").GetRawConstantValue();
            Player=AbyssHostNativeProbe.Player(Project,Bodies,index);Put(Player.PlayerData,"m_isMainPlayer",main);Player.ComponentInput=new ComponentInput();Player.ComponentInput.m_entity=Player.Entity;
            Client=NewClient(Players,Player,main?(byte)1:(byte)2);
            Net=New(mod.GetType("SCIEPort.Features.AbyssReaper.VoidPortalNetwork",true),Owner,Players,Terrain,Clock);
            Travel=New(mod.GetType("SCIEPort.Features.AbyssReaper.VoidPortalLocalTravel",true),Terrain,Players,Clock,Registry);
            Net.GetType().GetProperty("Travel",Flags).SetValue(Net,Travel,null);Travel.GetType().GetProperty("Network",Flags).SetValue(Travel,Net,null);
            Put(Owner,"<Gateways>k__BackingField",Net);Put(Owner,"portalTravel",Travel);
            Put(Owner,"<PortalOperations>k__BackingField",New(mod.GetType("SCIEPort.Features.AbyssReaper.VoidPortalOperations",true),Owner,Terrain));
            Clock.m_gameTime=10;
        }
        internal Guid Gate(string name,int x)
        {
            Guid id=Guid.NewGuid();Check((bool)Call(Registry,"TryAdd",id,Guid.NewGuid(),name,x+.5f,64f,9.5f,0f),"permanent fixture gate");return id;
        }
        public void Dispose(){((IDisposable)Travel).Dispose();((IDisposable)Net).Dispose();Cells.Dispose();}
    }
    static Client NewClient(SubsystemPlayers players,ComponentPlayer player,byte id)
    {
        var client=new Client(Guid.NewGuid(),null);Put(client,"<ID>k__BackingField",id);Put(client,"<SubsystemPlayers>k__BackingField",players);client.State=ClientState.ProjectLoaded;
        player.PlayerData.PlayerGUID=client.GUID;players.m_playersData.Add(player.PlayerData);players.m_componentPlayers.Add(player);CommonLib.Net.Clients[id]=client;return client;
    }
    static List<IPackage> Pending(){return (List<IPackage>)Get(CommonLib.Net,"PendingPackages");}
    static int Count(Fixture f){return (int)f.Registry.GetType().GetProperty("Count",Flags).GetValue(f.Registry,null);}
    static string Feedback(Fixture f)
    {
        if(f.Player.PlayerData.IsMainPlayer)return (string)f.Net.GetType().GetProperty("Feedback",Flags).GetValue(f.Net,null);
        string result="";foreach(var packet in Pending())if(packet.GetType()==f.PacketType&&(byte)Get(packet,"Kind")==4)result=(string)Get(packet,"Text");return result;
    }
    static void Creation(Assembly mod,WorkType mode,bool main)
    {
        CommonLib.WorkType=mode;
        using(var f=new Fixture(mod,main))
        {
            uint sequence=0;
            Action<byte,string> request=(command,text)=>
            {
                if(main){Call(f.Net,"Request",f.Player,command,Guid.Empty,Guid.Empty,text);return;}
                var packet=Packet(f.PacketType,command,f.Player,++sequence,Guid.Empty,Guid.Empty,text);
                Put(packet,"Direction",f.Player.ComponentCreatureModel.EyeRotation.GetForwardVector());Call(f.Net,"Enqueue",f.Player,f.Client,packet);
            };
            // The real dialog sends Sync on open, then Create with a level eye direction.
            request(0,"");request(1,"身前星门");
            Call(f.Net,"Update");
            Check(Count(f)==1,"open-list Sync does not swallow immediate level-view creation: "+mode);
            object[] gate={(Guid)f.Net.GetType().GetProperty("LastCreated",Flags).GetValue(f.Net,null),null};
            if(main)
            {
                Check((bool)f.Registry.GetType().GetMethod("TryGet",Flags).Invoke(f.Registry,gate),"local success returns actual new gate identity");
                Check((string)Get(gate[1],"Name")=="身前星门"&&Feedback(f).Contains("已建立"),"requested name and visible success survive automatic placement");
            }
            request(1,"快速重试");Call(f.Net,"Update");
            Check(Count(f)==1&&Feedback(f).Contains("频繁"),"throttled click returns explicit feedback instead of processing forever");
            f.Clock.m_gameTime+=1;
            request(1,"");Call(f.Net,"Update");
            Check(Count(f)==1&&Feedback(f).Contains("名称"),"invalid create name is reported without modifying registry");
            f.Clock.m_gameTime+=1;
            request(0,"");request(0,"");Call(f.Net,"Update");
            Check(Feedback(f).Contains("刷新过于频繁"),"snapshot refresh retains its own bounded rate limit");
            object[] second={(Guid)Guid.NewGuid(),null};
            var aimed=Vector3.Normalize(new Vector3(2,-1.5f,4));object[] args={f.Player,aimed,"瞄准门",Guid.Empty};
            Check((bool)f.Net.GetType().GetMethod("CreateAimed",Flags).Invoke(f.Net,args)&&Count(f)==2,"explicit ground aim still creates at the requested site");
            second[0]=args[3];f.Registry.GetType().GetMethod("TryGet",Flags).Invoke(f.Registry,second);
            Check((float)Get(second[1],"X")==10.5f&&(float)Get(second[1],"Z")==9.5f,"ground aim does not silently relocate the chosen site");
            object[] duplicate={f.Player,aimed,"重复",Guid.Empty};
            Check(!(bool)f.Net.GetType().GetMethod("CreateAimed",Flags).Invoke(f.Net,duplicate)&&Count(f)==2,"repeated aimed creation cannot overlap an existing permanent gate");
            f.Clock.m_gameTime+=1;
            for(int x=0;x<16;x++)for(int z=0;z<16;z++)for(int y=64;y<69;y++)f.Cells.SetCellValueFast(x,y,z,3);
            request(1,"无空地");Call(f.Net,"Update");
            Check(Count(f)==2&&Feedback(f).Contains("建立失败"),"no safe space gives actionable failure and no partial gate");
            Check(f.Cells.GetCellValue(8,63,9)==3,"creation does not edit native floor");
        }
    }
    public static void Run(Assembly mod)
    {
        var work=CommonLib.WorkType;var node=CommonLib.Net;var territories=SubsystemBedrockBlockBehavior.Territories;
        Block air=BlocksManager.Blocks[0],stone=BlocksManager.Blocks[3];
        try
        {
            CommonLib.Net=Bare<NetNode>();Put(CommonLib.Net,"PendingPackages",new List<IPackage>());CommonLib.Net.Clients=new Dictionary<int,Client>();
            SubsystemBedrockBlockBehavior.Territories=new Dictionary<Guid,Territoriy>();BlocksManager.Blocks[0]=new AirBlock{BlockIndex=0,IsCollidable=false};BlocksManager.Blocks[3]=new GraniteBlock{BlockIndex=3,IsCollidable=true};
            var packetType=mod.GetType("SCIEPort.Features.AbyssReaper.VoidPortalPackage",true);
            foreach(var assembly in new[]{typeof(IPackage).Assembly,mod})foreach(var t in assembly.GetTypes())
            {if(t.IsClass&&!t.IsAbstract&&typeof(IPackage).IsAssignableFrom(t)&&t!=packetType)Check(((IPackage)BareObject(t)).ID!=250,"packet 250 is unoccupied");}
            for(int i=0;i<32;i++)
            {
                var original=Packet(packetType,(byte)(i%8),new ComponentPlayer(),(uint)(i+1),Guid.NewGuid(),Guid.NewGuid(),"共享星门 & <"+i+"> 名称");
                Put(original,"Kind",(byte)(i%8));Put(original,"Owner",Guid.NewGuid());Put(original,"Point",new Vector3(i+.5f,64,-i-.5f));Put(original,"Direction",Vector3.UnitZ);Put(original,"Facing",90f);Put(original,"Revision",uint.MaxValue-(uint)i);Put(original,"Limit",(byte)64);Put(original,"Count",(byte)i);
                var writer=new PackageStreamWriter();((IPackage)original).WriteData(writer);var bytes=writer.Data();Check(bytes.Length<700,"bounded wire size");
                var copy=New(packetType);((IPackage)copy).ReadData(new PackageStreamReader(bytes));
                foreach(string f in new[]{"Kind","Command","Caster","Sequence","Revision","Portal","Target","Owner","Point","Direction","Facing","Text","Limit","Count"})Check(Get(original,f).Equals(Get(copy,f)),"native package roundtrip "+f);
            }
            var empty=Packet(packetType,0,new ComponentPlayer(),1,Guid.Empty,Guid.Empty,"");
            var overlong=new PackageStreamWriter();((IPackage)empty).WriteData(overlong);overlong.BaseStream.Position-=2;overlong.Write((ushort)385);
            bool rejected=false;try{((IPackage)New(packetType)).ReadData(new PackageStreamReader(overlong.Data()));}catch(InvalidDataException){rejected=true;}
            Check(rejected,"oversized text length rejected before allocation");
            var invalidUtf8=new PackageStreamWriter();((IPackage)empty).WriteData(invalidUtf8);invalidUtf8.BaseStream.Position-=2;invalidUtf8.Write((ushort)2);invalidUtf8.Write(new byte[]{255,255});
            rejected=false;try{((IPackage)New(packetType)).ReadData(new PackageStreamReader(invalidUtf8.Data()));}catch(System.Text.DecoderFallbackException){rejected=true;}
            Check(rejected,"invalid UTF8 rejected");
            Creation(mod,WorkType.Local,true);Creation(mod,WorkType.Server,true);Creation(mod,WorkType.Server,false);
            CommonLib.WorkType=WorkType.Server;
            using(var server=new Fixture(mod,false))
            {
                Guid source=server.Gate("Source",8),a=server.Gate("A",4),b=server.Gate("B",12);
                var ops=Get(server.Owner,"<PortalOperations>k__BackingField");
                Check((bool)Call(ops,"Rename",server.Player,a,"Anyone can rename"),"ordinary non-owner remote player manages name");
                var request=Packet(packetType,2,server.Player,1,b,Guid.Empty,"Renamed by RPC");
                Call(server.Net,"Enqueue",server.Player,server.Client,request);Call(server.Net,"Update");
                object[] lookup={b,null};server.Registry.GetType().GetMethod("TryGet",Flags).Invoke(server.Registry,lookup);
                Check((string)Get(lookup[1],"Name")=="Renamed by RPC","authenticated remote mutation processed on update");
                Put(request,"Text","Replay must not win");Call(server.Net,"Enqueue",server.Player,server.Client,request);server.Clock.m_gameTime+=1;Call(server.Net,"Update");
                Check((string)Get(lookup[1],"Name")=="Renamed by RPC","replayed mutation ignored");
                var oldToken=server.Client.TokenId;
                var oldConnection=Packet(packetType,2,server.Player,2,b,Guid.Empty,"Old connection");Call(server.Net,"Enqueue",server.Player,server.Client,oldConnection);
                Put(server.Client,"<TokenId>k__BackingField",Guid.NewGuid());Call(server.Net,"Update");
                Check((string)Get(lookup[1],"Name")=="Renamed by RPC","queued request retains original connection token");Put(server.Client,"<TokenId>k__BackingField",oldToken);
                var stranger=AbyssHostNativeProbe.Player(server.Project,server.Bodies,952);Put(stranger.PlayerData,"m_isMainPlayer",false);
                var spoof=Packet(packetType,3,stranger,2,b,Guid.Empty,"");Call(server.Net,"Enqueue",stranger,server.Client,spoof);Call(server.Net,"Update");
                Check((int)server.Registry.GetType().GetProperty("Count",Flags).GetValue(server.Registry,null)==3,"queued connection ownership is revalidated");
                // Real world/client gate records are atomically replaced only at the matching end marker.
                Pending().Clear();Call(server.Net,"Enqueue",server.Player,server.Client,Packet(packetType,0,server.Player,3,Guid.Empty,Guid.Empty,""));Call(server.Net,"Update");
                var snapshot=new List<IPackage>(Pending());Check(snapshot.Count>=5,"server shared rows emitted");
                CommonLib.WorkType=WorkType.Client;
                using(var client=new Fixture(mod,true))
                {
                    foreach(var packet in snapshot)
                    {
                        byte kind=(byte)Get(packet,"Kind");if(kind<1||kind>3)continue;
                        Put(packet,"Caster",client.Player.Entity.EntityId);Call(client.Net,"Enqueue",null,null,packet);Call(client.Net,"Update");
                        object current=Get(client.Owner,"<Portals>k__BackingField");
                        if(kind!=3)Check((int)current.GetType().GetProperty("Count",Flags).GetValue(current,null)==0,"partial rows never expose half a snapshot");
                    }
                    Call(client.Net,"Update");object replica=Get(client.Owner,"<Portals>k__BackingField");
                    Check((int)replica.GetType().GetProperty("Count",Flags).GetValue(replica,null)==3,"client receives all shared permanent gates");
                    var created=Packet(packetType,0,client.Player,0,a,Guid.Empty,"永久星门已建立并记录");Put(created,"Kind",(byte)4);Call(client.Net,"Receive",created);
                    foreach(var row in snapshot)if((byte)Get(row,"Kind")>=1&&(byte)Get(row,"Kind")<=3)Call(client.Net,"Receive",row);
                    Check(Feedback(client).Contains("已建立"),"automatic shared-list snapshot does not erase the create result");
                    // The authenticated packet handler rejects peers pretending to be the host.
                    var malicious=New(packetType);Put(malicious,"Kind",(byte)1);((IPackage)malicious).From=server.Client;
                    CommonLib.Net.Server=client.Client;int before=((ICollection)Get(client.Net,"queue")).Count;
                    var proxy=Bare<ProjectNet>();foreach(var field in typeof(Project).GetFields(Flags))field.SetValue(proxy,field.GetValue(client.Project));
                    ((IPackage)malicious).Handle(proxy,CommonLib.Net,false);
                    Check(((ICollection)Get(client.Net,"queue")).Count==before,"peer cannot inject server snapshot");
                    var sameId=new Client(Guid.NewGuid(),null);Put(sameId,"<ID>k__BackingField",client.Client.ID);((IPackage)malicious).From=sameId;
                    ((IPackage)malicious).Handle(proxy,CommonLib.Net,false);
                    Check(((ICollection)Get(client.Net,"queue")).Count==before,"server provenance uses object identity, not native ID-only equality");
                    // Test the complete owning-client half: binding, preload, commit, abort, cleanup.
                    Call(client.Net,"Request",client.Player,(byte)4,source,a,"");
                    uint clientSequence=(uint)client.Net.GetType().GetProperty("ChoiceSequence",Flags).GetValue(client.Net,null);
                    Vector3 clientOrigin=client.Player.ComponentBody.Position,clientLanding=new Vector3(6.5f,64,9.5f);
                    var prepare=Packet(packetType,0,client.Player,clientSequence+1,source,a,"");Put(prepare,"Kind",(byte)5);Put(prepare,"Point",clientLanding);
                    Call(client.Travel,"Receive",client.Player,prepare);Check(Get(client.Travel,"clientTransfer")==null,"stale prepare cannot revive another selection");
                    Put(prepare,"Sequence",clientSequence);Call(client.Travel,"Receive",client.Player,prepare);Call(client.Travel,"Update");
                    Check((bool)Call(client.Travel,"InputLocked",client.Player),"client input locks only for active handoff");
                    var commit=Packet(packetType,0,client.Player,clientSequence,source,a,"");Put(commit,"Kind",(byte)6);Put(commit,"Point",clientLanding+Vector3.UnitX);
                    Call(client.Travel,"Receive",client.Player,commit);Check(client.Player.ComponentBody.Position==clientOrigin,"commit cannot change preflight landing");
                    Put(commit,"Point",clientLanding);Call(client.Travel,"Receive",client.Player,commit);Call(client.Travel,"Receive",client.Player,commit);
                    Check(client.Player.ComponentBody.Position==clientLanding,"valid repeated commit moves only to authorized landing");
                    var finish=Packet(packetType,7,client.Player,clientSequence,source,a,"");Put(finish,"Kind",(byte)7);Put(finish,"Point",clientOrigin);
                    Call(client.Travel,"Receive",client.Player,finish);
                    Check(client.Player.ComponentBody.Position==clientOrigin&&!(bool)Call(client.Travel,"InputLocked",client.Player),"server abort restores source and releases input");
                    client.Clock.m_gameTime+=2;Call(client.Travel,"Update");Check(Get(client.Travel,"clientTransfer")==null,"client lease retires after camera handoff grace");
                }
                CommonLib.WorkType=WorkType.Server;Pending().Clear();
                server.Clock.m_gameTime+=1;Call(server.Net,"Enqueue",server.Player,server.Client,Packet(packetType,4,server.Player,4,source,a,""));Call(server.Net,"Update");
                var choice=Get(server.Travel,"choices");Check((int)choice.GetType().GetProperty("Count").GetValue(choice,null)==1,"remote player has own target selection");
                server.Player.ComponentBody.Position=new Vector3(8.5f,64,9.5f);Call(server.Travel,"Update");
                var transfers=Get(server.Travel,"transfers");Check((int)transfers.GetType().GetProperty("Count").GetValue(transfers,null)==1,"remote entry starts destination preflight");
                object transfer=transfers.GetType().GetProperty("Item").GetValue(transfers,new object[]{server.Player.PlayerGuid});
                object selection=Get(transfer,"Choice");uint sequence=(uint)Get(selection,"Sequence");
                Vector3 origin=(Vector3)Get(transfer,"Origin"),landing=(Vector3)Get(transfer,"Landing");
                var secondPlayer=AbyssHostNativeProbe.Player(server.Project,server.Bodies,952);
                var actors=(Dictionary<Guid,ComponentPlayer>)Get(server.Travel,"actors");actors.Add(secondPlayer.PlayerGuid,secondPlayer);
                object[] targetLookup={a,null};server.Registry.GetType().GetMethod("TryGet",Flags).Invoke(server.Registry,targetLookup);
                object[] alternative={secondPlayer.PlayerGuid,targetLookup[1],0f,0f,0f};
                Check((bool)server.Travel.GetType().GetMethod("TryFindLanding",Flags).Invoke(server.Travel,alternative)&&new Vector3((float)alternative[2],(float)alternative[3],(float)alternative[4])!=landing,"concurrent travellers reserve distinct landing boxes");actors.Remove(secondPlayer.PlayerGuid);
                var ready=Packet(packetType,5,server.Player,sequence,source,a,"");Call(server.Travel,"Signal",server.Player,ready);Call(server.Travel,"Update");
                Check((bool)Get(transfer,"Committed")&&server.Player.ComponentBody.Position==origin,"server waits for owner ack before authoritative move");
                server.Player.ComponentBody.netPosition=new NetPosition(new Vector3(999,64,999));
                var ack=Packet(packetType,6,server.Player,sequence,source,a,"");Call(server.Travel,"Signal",server.Player,ack);
                Check(server.Player.ComponentBody.Position==landing&&server.Player.ComponentBody.netPosition.Get(.1f)==landing,"ack resets stale native position interpolation");
                Call(server.Travel,"Signal",server.Player,ack);Check(server.Player.ComponentBody.Position==landing,"repeated ack is idempotent");
                server.Cells.AllocateChunk(1,0).State=TerrainChunkState.Valid;
                server.Clock.m_gameTime+=1;server.Player.ComponentBody.Position=new Vector3(8.5f,64,9.5f);
                Call(server.Net,"Enqueue",server.Player,server.Client,Packet(packetType,4,server.Player,5,source,b,""));Call(server.Net,"Update");Call(server.Travel,"Update");
                string messages="";foreach(var msg in Pending())if(msg.GetType()==packetType)messages+=" ["+Get(msg,"Text")+"]";
                Check((int)transfers.GetType().GetProperty("Count").GetValue(transfers,null)==1,"second remote handoff begins independently"+messages);
                Check((bool)Call(ops,"Delete",server.Player,b),"all remote players may delete another creator's gate");Call(server.Travel,"Update");
                Check((int)transfers.GetType().GetProperty("Count").GetValue(transfers,null)==0,"destination deletion cancels outstanding remote handoff");
                // Client preload uses a private owned location, without advertising false player coordinates.
                CommonLib.WorkType=WorkType.Client;Pending().Clear();
                var leaseType=mod.GetType("SCIEPort.Features.AbyssReaper.VoidChunkLease",true);object[] args={server.Terrain.TerrainUpdater,new Vector2(100,100),null};
                Check((bool)leaseType.GetMethod("TryCreateClient",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args),"client preload lease creates bounded owned update location");
                Check(Pending().Count==0,"client preload does not relocate native player update location on server");((IDisposable)args[2]).Dispose();
            }
        }
        finally{CommonLib.Net=node;CommonLib.WorkType=work;SubsystemBedrockBlockBehavior.Territories=territories;BlocksManager.Blocks[0]=air;BlocksManager.Blocks[3]=stone;}
        Console.WriteLine("Shared portal native wire/permissions/snapshot/replay/teleport checks: "+checks+" passed (isolated fixtures, not live multiplayer).");
    }
    static object BareObject(Type t){return FormatterServices.GetUninitializedObject(t);}
}

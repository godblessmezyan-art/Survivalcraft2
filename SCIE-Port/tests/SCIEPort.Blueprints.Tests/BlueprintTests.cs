using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using Engine;
using Game;
using Game.NetWork;
using GameEntitySystem;
using SCIEPort.Features.Blueprints;
using SCIEPort.Features.Blueprints.Core;
using SCIEPort.Features.Blueprints.Core.Blueprints;
using SCIEPort.Features.Blueprints.Core.BuildEngine;
using SCIEPort.Features.Blueprints.Core.Transactions;
using SCIEPort.Features.Codex;

internal static partial class BlueprintTests
{
    private sealed class Sink:ILogSink { public void Log(LogType type,string message){Console.WriteLine(type+": "+message);} }
    private static int checks;
    private const BindingFlags Fields=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    private static void Check(bool ok,string message) { checks++;if(!ok)throw new Exception(message); }
    private static void Throws(Action action,string message) { bool threw=false;try{action();}catch{threw=true;}Check(threw,message); }
    private static T Bare<T>() { return (T)FormatterServices.GetUninitializedObject(typeof(T)); }
    private static void Put(object instance,string name,object value)
    {
        for(Type type=instance.GetType();type!=null;type=type.BaseType)
        { var field=type.GetField(name,Fields)??type.GetField("<"+name+">k__BackingField",Fields);if(field!=null){field.SetValue(instance,value);return;} }
        throw new Exception("Fixture field not found: "+name);
    }
    private static List<IPackage> Pending { get { return (List<IPackage>)typeof(NetNode).GetField("PendingPackages",Fields).GetValue(CommonLib.Net); } }
    private sealed class Grid : ITerrainGateway,IConstructionTerrainGateway
    {
        internal readonly Dictionary<CellPos,int> Cells=new Dictionary<CellPos,int>();
        internal readonly HashSet<CellPos> Blocked=new HashSet<CellPos>();
        internal int Writes,FrameWrites,MaxFrameWrites,FailAt=-1;
        public CellEditability InspectCell(CellPos p) { return new CellEditability(p,p.Y<0||p.Y>=256?CellEditabilityStatus.InvalidY:Blocked.Contains(p)?CellEditabilityStatus.ChunkMissing:CellEditabilityStatus.Editable); }
        public bool IsCellEditable(CellPos p) { return InspectCell(p).IsEditable; }
        public bool IsCellAir(CellPos p) { int value;return TryGetCellValue(p,out value)&&Terrain.ExtractContents(value)==0; }
        public bool IsCellSoftObstruction(CellPos p) { return false; }
        public bool TryGetCellValue(CellPos p,out int value) { Cells.TryGetValue(p,out value);return IsCellEditable(p); }
        public bool SetCellValue(CellPos p,int value)
        { if(!IsCellEditable(p))return false;Writes++;FrameWrites++;MaxFrameWrites=Math.Max(MaxFrameWrites,FrameWrites);Cells[p]=value;return Writes!=FailAt; }
        public bool SupportsAttachment(int value,BlockFamily family,int face) { return value!=0; }
        public bool ValuesEquivalent(int a,int b) { return Terrain.ReplaceLight(a,0)==Terrain.ReplaceLight(b,0); }
        public bool ApplyWithJournal(CellPos p,int value,out List<BlockUndoRecord> journal,out string error)
        { int old;TryGetCellValue(p,out old);journal=new List<BlockUndoRecord>{new BlockUndoRecord(p,old,value)};error=null;return SetCellValue(p,value); }
        internal int Occupied { get { return Cells.Count(x=>Terrain.ExtractContents(x.Value)!=0); } }
    }
    private sealed class Fixture : IDisposable
    {
        internal readonly Project Project=new Project();
        internal readonly SubsystemPlayers Players=new SubsystemPlayers();
        internal readonly SubsystemTime Clock=new SubsystemTime();
        internal readonly SubsystemBlueprintArtifact Host=new SubsystemBlueprintArtifact();
        internal readonly SubsystemArtifactCodex Codex=new SubsystemArtifactCodex();
        internal readonly Grid Terrain=new Grid();
        internal readonly Dictionary<Guid,uint> Sequences=new Dictionary<Guid,uint>();
        internal Fixture()
        {
            CommonLib.WorkType=WorkType.Server;CommonLib.Net=Bare<NetNode>();Put(CommonLib.Net,"PendingPackages",new List<IPackage>());CommonLib.Net.Clients=new Dictionary<int,Client>();CommonLib.Net.Clients[0]=new Client(Guid.NewGuid(),null);
            Put(Players,"m_project",Project);Put(Host,"m_project",Project);Put(Codex,"m_project",Project);
            Project.m_subsystems.Add(Players);Project.m_subsystems.Add(Host);Project.m_subsystems.Add(Codex);
            Put(Host,"players",Players);Put(Host,"clock",Clock);Put(Host,"terrain",Terrain);
            var info=new SubsystemGameInfo();var settings=Bare<WorldSettings>();settings.GameMode=GameMode.Creative;Put(info,"WorldSettings",settings);Put(Host,"info",info);
            Host.TerrainForPlayer=p=>Terrain;Clock.m_gameTime=10;Clock.m_gameTimeDelta=.05f;Host.Palette.Reset();
        }
        internal ComponentPlayer Player(byte id)
        {
            var entity=Bare<Entity>();Put(entity,"m_project",Project);Put(entity,"m_isAddedToProject",true);Put(entity,"m_components",new List<Component>());Put(entity,"EntityId",(ushort)id);
            var player=new ComponentPlayer { m_entity=entity,PlayerGuid=Guid.NewGuid() };
            player.ComponentHealth=new ComponentHealth { m_entity=entity,Health=1 };
            player.ComponentLocomotion=new ComponentLocomotion { m_entity=entity,WalkSpeed=5 };
            player.ComponentBody=new ComponentBody { m_entity=entity,Position=new Vector3(0,64,0) };
            player.ComponentMiner=new ComponentMiner { m_entity=entity,ComponentPlayer=player,ComponentCreature=player };
            var inventory=new ComponentInventory { m_entity=entity };inventory.m_slots.Add(new ComponentInventoryBase.Slot { Value=957,Count=1 });player.ComponentMiner.Inventory=inventory;
            var data=Bare<PlayerData>();data.ComponentPlayer=player;data.GroupKey="";player.PlayerData=data;Put(data,"m_isMainPlayer",false);
            var client=new Client(Guid.NewGuid(),null);Put(client,"ID",id);Put(client,"SubsystemPlayers",Players);client.State=ClientState.ProjectLoaded;
            data.PlayerGUID=client.GUID;CommonLib.Net.Clients[id]=client;Players.m_componentPlayers.Add(player);Players.m_playersData.Add(data);
            Codex.Records[player.PlayerGuid]=511;return player;
        }
        internal void Tick() { Terrain.FrameWrites=0;Clock.m_gameTime+=.05;Host.Update(.05f);Check(Terrain.FrameWrites<=32,"world frame write budget"); }
        internal BlueprintPackage Command(ComponentPlayer p,byte kind,Guid request)
        { uint sequence;Sequences.TryGetValue(p.PlayerGuid,out sequence);Sequences[p.PlayerGuid]=++sequence;return new BlueprintPackage { Kind=kind,Request=request,Sequence=sequence,Owner=p.PlayerGuid,Caster=p.Entity.EntityId }; }
        internal BlueprintPackage Send(ComponentPlayer p,BlueprintPackage command)
        { Pending.Clear();Host.Enqueue(p,p.PlayerData.Client,command);Tick();return Pending.OfType<BlueprintPackage>().LastOrDefault(x=>x.Owner==p.PlayerGuid); }
        internal BlueprintPackage Preview(ComponentPlayer p,Blueprint blueprint,int x=0,bool clear=false)
        {
            byte[] json=Encoding.UTF8.GetBytes(BlueprintJson.Serialize(blueprint));Guid request=Guid.NewGuid();
            var begin=Command(p,BlueprintPackage.Begin,request);begin.Total=json.Length;begin.X=x;begin.Y=64;begin.Clear=clear;
            var reply=Send(p,begin);Check(reply!=null&&reply.Kind==BlueprintPackage.Ack,"ordinary remote player begin acknowledged");
            for(int offset=0;offset<json.Length;offset+=BlueprintTransfer.ChunkBytes)
            {
                var chunk=Command(p,BlueprintPackage.Chunk,request);chunk.Offset=offset;chunk.Payload=json.Skip(offset).Take(BlueprintTransfer.ChunkBytes).ToArray();
                reply=Send(p,chunk);Check(reply.Kind==BlueprintPackage.Ack&&reply.Offset==offset+chunk.Payload.Length,"chunk acknowledged at exact offset");
                if(offset==0){int count=Terrain.Writes;var replay=Send(p,chunk);Check(replay.Offset==reply.Offset&&Terrain.Writes==count,"repeated chunk has no append/write side effects");}
            }
            return Send(p,Command(p,BlueprintPackage.Finish,request));
        }
        internal void Drain() { for(int i=0;i<1200&&Host.Busy;i++)Tick();Check(!Host.Busy,"operation terminates"); }
        public void Dispose() { Host.Dispose(); }
    }
    private static Blueprint Blueprint(int count=1)
    {
        var builder=new BlueprintBuilder();for(int i=0;i<count;i++)builder.Set(new CellPos(i%32,i/1024,(i/32)%32),"GraniteBlock");return builder.ToBlueprint("联机蓝图",3);
    }
    private static void TransferTests()
    {
        Throws(()=>new BlueprintTransfer(Guid.Empty,1,0),"empty transfer ID");Throws(()=>new BlueprintTransfer(Guid.NewGuid(),0,0),"empty transfer");Throws(()=>new BlueprintTransfer(Guid.NewGuid(),BlueprintTransfer.MaxBytes+1,0),"oversized transfer");
        var t=new BlueprintTransfer(Guid.NewGuid(),3,0);Check(!t.Append(1,new byte[]{1},0),"out of order chunk rejected");Check(!t.Append(0,new byte[4097],0),"oversized chunk rejected");
        Check(t.Append(0,Encoding.UTF8.GetBytes("abc"),0)&&t.Finish(0)=="abc","UTF8 upload assembled");Check(!t.Append(0,new byte[]{1},0),"duplicate append rejected");
        var invalid=new BlueprintTransfer(Guid.NewGuid(),1,0);invalid.Append(0,new byte[]{255},0);Throws(()=>invalid.Finish(0),"invalid UTF8 refused");
        var expired=new BlueprintTransfer(Guid.NewGuid(),1,0);Check(!expired.Append(0,new byte[]{1},181),"expired upload rejected");Throws(()=>expired.Finish(0),"truncated upload refused");
        Check(BlueprintTransfer.Newer(1,uint.MaxValue)&&!BlueprintTransfer.Newer(uint.MaxValue,1),"sequence wrap and replay");Check(!BlueprintTransfer.ValidOrigin(int.MaxValue,64,0)&&!BlueprintTransfer.ValidOrigin(0,256,0),"coordinate bounds");
    }
    private static void NetworkTests()
    {
        using(var f=new Fixture())
        {
            var alice=f.Player(1);var bob=f.Player(2);
            Check(alice.PlayerData.Client!=null&&!alice.PlayerData.IsMainPlayer&&!alice.PlayerData.ServerMaster&&!alice.PlayerData.ServerManager,"fixture is ordinary remote player");
            int before=f.Terrain.Writes;var ready=f.Preview(alice,Blueprint(120));Check(ready.Kind==BlueprintPackage.Ready&&f.Terrain.Writes==before,"prepare never writes terrain: "+ready.Kind+" "+Encoding.UTF8.GetString(ready.Payload));
            var confirm=f.Command(alice,BlueprintPackage.Confirm,ready.Request);f.Send(alice,confirm);Check(f.Host.Busy,"remote confirm starts authoritative task");
            var busy=f.Send(bob,f.Command(bob,BlueprintPackage.Undo,Guid.NewGuid()));Check(busy.Kind==BlueprintPackage.Status,"other player cannot interleave undo");
            f.Drain();Check(f.Terrain.Occupied==120,"remote build applied to authoritative terrain");before=f.Terrain.Writes;f.Send(alice,confirm);Check(f.Terrain.Writes==before,"replayed confirmation cannot build twice");
            f.Send(bob,f.Command(bob,BlueprintPackage.Undo,Guid.NewGuid()));Check(f.Terrain.Occupied==120,"other player has no access to Alice history");
            var changed=new CellPos(0,64,0);f.Terrain.Cells[changed]=21;f.Send(alice,f.Command(alice,BlueprintPackage.Undo,Guid.NewGuid()));f.Drain();Check(f.Terrain.Occupied==1&&f.Terrain.Cells[changed]==21,"undo preserves later edits and restores only own build");
            ready=f.Preview(bob,Blueprint(2),40);Check(ready.Kind==BlueprintPackage.Ready,"Bob has same build permission");f.Terrain.Cells[new CellPos(40,64,0)]=3;before=f.Terrain.Writes;
            f.Send(bob,f.Command(bob,BlueprintPackage.Confirm,ready.Request));Check(f.Terrain.Writes==before&&!f.Host.Busy,"changed server snapshot refused");
            f.Clock.m_gameTime+=1;ready=f.Preview(alice,Blueprint(200),-40);f.Send(alice,f.Command(alice,BlueprintPackage.Confirm,ready.Request));f.Tick();
            f.Send(bob,f.Command(bob,BlueprintPackage.Cancel,Guid.NewGuid()));Check(f.Host.Busy,"Bob cancel cannot cancel Alice");
            f.Send(alice,f.Command(alice,BlueprintPackage.Cancel,Guid.NewGuid()));f.Drain();Check(f.Terrain.Occupied==2,"cancel rolls back partial build only");
            f.Clock.m_gameTime+=1;ready=f.Preview(alice,Blueprint(100),-40);f.Send(alice,f.Command(alice,BlueprintPackage.Confirm,ready.Request));f.Players.m_componentPlayers.Remove(alice);f.Drain();Check(f.Terrain.Occupied==2,"disconnect cancels and rolls back");
        }
        using(var f=new Fixture())
        {
            var p=f.Player(1);var ready=f.Preview(p,Blueprint(100));f.Terrain.FailAt=10;f.Send(p,f.Command(p,BlueprintPackage.Confirm,ready.Request));f.Drain();Check(f.Terrain.Occupied==0,"failed native-style partial write is journaled and rolled back");
            f.Clock.m_gameTime+=1;ready=f.Preview(p,Blueprint(100));f.Send(p,f.Command(p,BlueprintPackage.Confirm,ready.Request));f.Drain();
            var block=new CellPos(3,64,0);f.Terrain.Blocked.Add(block);f.Send(p,f.Command(p,BlueprintPackage.Undo,Guid.NewGuid()));for(int i=0;i<5;i++)f.Tick();Check(f.Host.Busy,"unloaded undo cell pauses future restores");
            f.Terrain.Blocked.Clear();f.Drain();Check(f.Terrain.Occupied==0,"deferred undo finishes after chunks return");
        }
    }
    private static void SafetyTests()
    {
        using(var f=new Fixture())
        {
            var p=f.Player(1);var other=f.Player(2);int before=f.Terrain.Writes;
            var forged=f.Command(p,BlueprintPackage.Undo,Guid.NewGuid());forged.Owner=other.PlayerGuid;
            Check(f.Send(p,forged)==null&&f.Terrain.Writes==before,"spoofed GUID refused");
            var wrong=f.Command(p,BlueprintPackage.Undo,Guid.NewGuid());wrong.Caster=other.Entity.EntityId;
            Check(f.Send(p,wrong)==null,"spoofed entity refused");
            Pending.Clear();f.Host.Enqueue(p,other.PlayerData.Client,f.Command(p,BlueprintPackage.Undo,Guid.NewGuid()));f.Tick();Check(Pending.Count==0,"spoofed connection refused");
            f.Codex.Records[p.PlayerGuid]=255;var denied=f.Command(p,BlueprintPackage.Begin,Guid.NewGuid());denied.Total=1;denied.Y=64;
            Check(f.Send(p,denied).Kind==BlueprintPackage.Status&&!f.Host.Busy,"sealed ninth artifact refused");f.Codex.Records[p.PlayerGuid]=511;
            var bad=f.Command(p,BlueprintPackage.Begin,Guid.NewGuid());bad.Y=64;bad.Total=BlueprintTransfer.MaxBytes+1;Check(f.Send(p,bad).Kind==BlueprintPackage.Status,"oversized network declaration refused");
            f.Clock.m_gameTime+=1;var ready=f.Preview(p,Blueprint());Check(ready.Kind==BlueprintPackage.Ready,"server preview available to ordinary player");f.Clock.m_gameTime+=31;
            before=f.Terrain.Writes;f.Send(p,f.Command(p,BlueprintPackage.Confirm,ready.Request));Check(f.Terrain.Writes==before&&!f.Host.Busy,"expired ticket cannot mutate terrain");
            f.Terrain.Cells[new CellPos(0,64,0)]=3;f.Clock.m_gameTime+=1;ready=f.Preview(p,Blueprint());Check(ready.Kind==BlueprintPackage.Status,"default no-overwrite refuses occupied cell");
            f.Clock.m_gameTime+=1;ready=f.Preview(p,Blueprint(),0,true);Check(ready.Kind==BlueprintPackage.Ready&&ready.Offset==1,"explicit clear returns occupied count for second confirmation");
            f.Terrain.Cells[new CellPos(0,64,0)]=45;f.Clock.m_gameTime+=1;ready=f.Preview(p,Blueprint(),0,true);Check(ready.Kind==BlueprintPackage.Status,"unknown container/state protected from clear");
            f.Terrain.Cells.Clear();SubsystemBedrockBlockBehavior.Territories[Guid.NewGuid()]=new Territoriy { Origin=new Point2(0,0),OwnerGuid=other.PlayerData.PlayerGUID };
            f.Clock.m_gameTime+=1;ready=f.Preview(p,Blueprint());Check(ready.Kind==BlueprintPackage.Status,"native territory denies non-owner non-admin remote player");
            SubsystemBedrockBlockBehavior.Territories.Clear();
            for(int i=0;i<40;i++)f.Tick();Check(Math.Abs(p.ComponentLocomotion.WalkSpeed-6)<.0001,"held movement +20 percent has no accumulation");
            ((ComponentInventory)p.ComponentMiner.Inventory).m_slots[0].Value=3;f.Tick();Check(p.ComponentLocomotion.WalkSpeed==5,"unequip returns only own movement increment");
            ((ComponentInventory)p.ComponentMiner.Inventory).m_slots[0].Value=957;
            var loader=new SCIEPort.Mod.SCIEPortLoader();p.ComponentMiner.m_lastHitTime=5;float attack=1,probability=1,creature=1;bool hitted;
            loader.OnMinerHit(p.ComponentMiner,null,Vector3.Zero,Vector3.UnitZ,ref attack,ref probability,ref creature,out hitted);
            Check(Math.Abs(p.ComponentMiner.m_lastHitTime-(5-.66*(1-1/1.1)))<.000001,"native melee clock applies 10 percent artifact frequency");
        }
        using(var f=new Fixture())
        {
            var p=f.Player(1);var ready=f.Preview(p,Blueprint(100));f.Send(p,f.Command(p,BlueprintPackage.Confirm,ready.Request));f.Drain();
            var other=f.Player(2);var pendingTicket=f.Preview(other,Blueprint(),40);Check(pendingTicket.Kind==BlueprintPackage.Ready,"second player can prepare before undo");
            f.Terrain.Blocked.Add(new CellPos(3,64,0));f.Send(p,f.Command(p,BlueprintPackage.Undo,Guid.NewGuid()));
            f.Send(p,f.Command(p,BlueprintPackage.Cancel,Guid.NewGuid()));Check(!f.Host.Busy,"user can pause an unfinished undo");
            int writes=f.Terrain.Writes;f.Send(other,f.Command(other,BlueprintPackage.Confirm,pendingTicket.Request));Check(f.Terrain.Writes==writes&&!f.Host.Busy,"previously prepared tickets cannot bypass pending undo lock");
            f.Clock.m_gameTime+=1;var begin=f.Command(p,BlueprintPackage.Begin,Guid.NewGuid());begin.Y=64;begin.Total=1;
            Check(f.Send(p,begin).Kind==BlueprintPackage.Status&&!f.Host.Busy,"pending undo blocks later construction");
            f.Terrain.Blocked.Clear();f.Send(p,f.Command(p,BlueprintPackage.Undo,Guid.NewGuid()));f.Drain();Check(f.Terrain.Occupied==0,"paused undo resumes without dropping journal");
        }
    }
    private static void LibraryAndCopyTests()
    {
        string root=Path.Combine(Path.GetTempPath(),"scie-blueprints-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var library=new BlueprintFileLibrary(root);string first,second,error;string json=BlueprintJson.Serialize(Blueprint());
            Check(library.Save("绘卷",json,out first)==null&&library.Save("绘卷",json,out second)==null&&first!=second,"duplicate saves never overwrite");
            Check(library.ListNames(out error).Count==2&&error==null,"local library persistence");
            var stem=Path.GetFileNameWithoutExtension(first);Check(BlueprintImportService.FromString(library.Load(stem,out error),null).Success,"file import/export roundtrip");
            Check(library.Delete("../outside")!=null&&library.Load("../outside",out error)==null,"path traversal refused");
            Check(library.Delete(stem)==null&&library.ListNames(out error).Count==1,"explicit selected-file deletion");
            var cells=new Terrain();try
            {
                cells.AllocateChunk(0,0).State=TerrainChunkState.Valid;for(int x=0;x<10;x++)for(int z=0;z<10;z++)cells.SetCellValueFast(x,64,z,3);
                var subsystem=Bare<SubsystemTerrain>();Put(subsystem,"Terrain",cells);var gateway=new GameTerrainGateway(subsystem);
                var copy=new BlueprintCopier.CopyJob(gateway,new CellPos(0,64,0),new CellPos(9,64,9),"复制");int frames=0;
                while(!copy.Advance(16)){frames++;Check(frames<20,"copy makes bounded progress");}
                Check(frames>=6&&copy.Result.Copied==100&&copy.Result.Blueprint.CellCount==100,"native read-only copy is spread across scan batches");
                var bad=new BlueprintCopier.CopyJob(gateway,new CellPos(int.MinValue,64,0),new CellPos(int.MaxValue,64,0),"越界");bad.Advance(1);Check(bad.Result.HasFailure,"copy coordinate subtraction cannot overflow caps");
                var unloaded=new BlueprintCopier.CopyJob(gateway,new CellPos(15,64,0),new CellPos(16,64,0),"未加载");while(!unloaded.Advance(8)){}Check(unloaded.Result.HasFailure&&unloaded.Result.Blueprint==null,"copy does not silently save partial unloaded chunks");
            }
            finally{cells.Dispose();}
        }
        finally{if(Path.GetFullPath(root).StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase))Directory.Delete(root,true);}
        using(var stream=File.OpenRead(Path.Combine(Path.GetDirectoryName(typeof(BlueprintToolBlock).Assembly.Location),"Assets/Textures/TiangongBlueprintSkill.png")))
        {var image=Engine.Media.Image.Load(stream);Check(image.Width==256&&image.Height==256&&image.GetPixel(0,0).A==0&&image.GetPixel(128,128).A>230,"target Engine decodes final generated icon and alpha");}
        var mesh=new BlockMesh();BlueprintToolBlock.Build(mesh);Check(mesh.Vertices.Count>0&&mesh.Indices.Count/3<1000,"native 3D scroll geometry stays within mesh budget");
        foreach(float width in new[]{640f,854f,1280f,1920f})foreach(bool left in new[]{false,true})
        {
            var obstacles=new List<SCIEPort.Features.Artifacts.ArtifactSkillLayout.Rect>{new SCIEPort.Features.Artifacts.ArtifactSkillLayout.Rect{X=left?width-180:0,Y=220,W=180,H=200},new SCIEPort.Features.Artifacts.ArtifactSkillLayout.Rect{X=left?0:width-180,Y=220,W=180,H=200}};
            SCIEPort.Features.Artifacts.ArtifactSkillLayout.Rect rect;
            if(BlueprintPreviewLayout.Place(width,480,obstacles,out rect))foreach(var obstacle in obstacles)Check(!rect.Overlaps(obstacle),"preview touch panel avoids left/right native movement/look controls");
        }
    }
    private static void IdentityAndCodecTests()
    {
        foreach(var asm in new[]{typeof(Block).Assembly,typeof(BlueprintPackage).Assembly})foreach(var type in asm.GetTypes())
        {
            if(!type.IsAbstract&&typeof(IPackage).IsAssignableFrom(type)&&type!=typeof(BlueprintPackage))Check(((IPackage)FormatterServices.GetUninitializedObject(type)).ID!=239,"239 vacant against actual native/mod packets");
            if(type.IsSubclassOf(typeof(Block))&&type!=typeof(BlueprintToolBlock)){var id=type.GetField("Index",BindingFlags.Public|BindingFlags.Static|BindingFlags.FlattenHierarchy);if(id!=null&&id.IsLiteral)Check((int)id.GetRawConstantValue()!=957,"957 vacant against actual native/mod blocks");}
        }
        var packet=new BlueprintPackage{Kind=BlueprintPackage.Chunk,Owner=Guid.NewGuid(),Request=Guid.NewGuid(),Caster=7,Sequence=uint.MaxValue,X=-10,Y=250,Z=42,Total=5000,Offset=4096,Clear=true,Payload=Encoding.UTF8.GetBytes("绘卷")};
        using(var writer=new PackageStreamWriter())
        {
            packet.WriteData(writer);var bytes=writer.Data();using(var reader=new PackageStreamReader(bytes))
            { var decoded=new BlueprintPackage();decoded.ReadData(reader);Check(decoded.Kind==packet.Kind&&decoded.Owner==packet.Owner&&decoded.Request==packet.Request&&decoded.Sequence==packet.Sequence&&decoded.X==-10&&decoded.Y==250&&decoded.Z==42&&decoded.Clear&&decoded.Total==5000&&decoded.Offset==4096&&decoded.Payload.SequenceEqual(packet.Payload),"actual native packet codec roundtrip"); }
        }
        packet.Payload=new byte[4097];Throws(()=>{using(var writer=new PackageStreamWriter())packet.WriteData(writer);},"native writer refuses oversized payload");
        Check(CodexRules.Bit(957)==256&&CodexRules.Bit(953)==128&&CodexRules.Bit(942)==1,"existing codex bits preserved and ninth appended");
        Check(!CodexRules.Unlocked(255,957)&&CodexRules.Unlocked(511,957),"old worlds preserve locks until new artifact unsealed");
        var json=BlueprintJson.Serialize(Blueprint(123));var bp=BlueprintImportService.FromString(json,null);Check(bp.Success&&bp.Blueprint.CellCount==123,"schema3 import export");
        var rotated=bp.Blueprint;for(int i=0;i<4;i++)rotated=rotated.Reorient(1,false);Check(BlueprintJson.Serialize(rotated)==json,"four rotations preserve blueprint");
        rotated=bp.Blueprint.Reorient(0,true).Reorient(0,true);Check(BlueprintJson.Serialize(rotated)==json,"double mirror preserves blueprint");
    }
    internal static void Run()
    {
        var oldMode=CommonLib.WorkType;var oldNet=CommonLib.Net;var oldTerritories=SubsystemBedrockBlockBehavior.Territories;
        try
        {
            SubsystemBedrockBlockBehavior.Territories=new Dictionary<Guid,Territoriy>();
            BlocksManager.Blocks[0]=new AirBlock{BlockIndex=0};BlocksManager.Blocks[3]=new GraniteBlock{BlockIndex=3};BlocksManager.Blocks[21]=new PlanksBlock{BlockIndex=21};
            Engine.Log.AddLogSink(new Sink());TransferTests();IdentityAndCodecTests();NetworkTests();SafetyTests();LibraryAndCopyTests();TestCloudBlueprints().GetAwaiter().GetResult();TestBlueprintWebRequest().GetAwaiter().GetResult();
            Console.WriteLine("Blueprint native API/codec + simulated authoritative multiplayer: "+checks+" checks passed. No actual game room or Android device.");
        }
        finally { CommonLib.WorkType=oldMode;CommonLib.Net=oldNet;SubsystemBedrockBlockBehavior.Territories=oldTerritories; }
    }
}

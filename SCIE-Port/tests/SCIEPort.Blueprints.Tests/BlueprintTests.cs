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
            Put(Host,"players",Players);Put(Host,"clock",Clock);Host.Feedback=(p,text)=>{};
            var info=new SubsystemGameInfo();var settings=Bare<WorldSettings>();settings.GameMode=GameMode.Creative;Put(info,"WorldSettings",settings);
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
            var inventory=new ComponentInventory { m_entity=entity };inventory.m_slots.Add(new ComponentInventoryBase.Slot { Value=971,Count=1 });player.ComponentMiner.Inventory=inventory;
            var data=Bare<PlayerData>();data.ComponentPlayer=player;data.GroupKey="";player.PlayerData=data;Put(data,"m_isMainPlayer",false);
            var client=new Client(Guid.NewGuid(),null);Put(client,"ID",id);Put(client,"SubsystemPlayers",Players);client.State=ClientState.ProjectLoaded;
            data.PlayerGUID=client.GUID;CommonLib.Net.Clients[id]=client;Players.m_componentPlayers.Add(player);Players.m_playersData.Add(data);
            Codex.Records[player.PlayerGuid]=511;return player;
        }
        internal void Tick() { Terrain.FrameWrites=0;Clock.m_gameTime+=.05;Host.Update(.05f);Check(Terrain.FrameWrites<=32,"world frame write budget"); }
        internal bool Start(ComponentPlayer p,Blueprint bp,int x=0,bool clear=false)
        {
            var origin=new CellPos(x,64,0);var plan=BuildPlanner.Plan(bp,origin,Host.Palette,Terrain,clear,false);
            return Host.StartDirect(p,plan,bp,origin,clear,null);
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
    private static void DirectTests()
    {
        foreach(var role in new[]{WorkType.Local,WorkType.Server,WorkType.Client})using(var f=new Fixture())
        {
            CommonLib.WorkType=role;var alice=f.Player(1);var bob=f.Player(2);f.Codex.Records.Clear();
            Check(f.Host.CanOperate(alice),"drawing works without a true-name record in "+role);
            Check(f.Start(alice,Blueprint(120)),"direct build starts immediately in "+role);
            Check(Pending.Count==0,"no custom host request or acknowledgement in "+role);
            f.Drain();Check(f.Terrain.Occupied==120,"direct scheduled build completes in "+role);
            f.Host.UndoDirect(bob);Check(!f.Host.Busy,"other player cannot undo own build");
            f.Terrain.Cells[new CellPos(0,64,0)]=21;f.Host.UndoDirect(alice);f.Drain();
            Check(f.Terrain.Occupied==1&&f.Terrain.Cells[new CellPos(0,64,0)]==21,"undo preserves subsequent edits in "+role);
            Check(f.Start(alice,Blueprint(200),-40),"start cancellable direct build");f.Tick();f.Host.CancelDirect(bob);Check(f.Host.Busy,"other player cannot cancel");
            f.Host.CancelDirect(alice);f.Drain();Check(f.Terrain.Occupied==1,"cancel restores partial direct build");
            Check(f.Start(alice,Blueprint(100),-40),"start failure test");f.Terrain.FailAt=f.Terrain.Writes+10;f.Drain();Check(f.Terrain.Occupied==1,"failed partial writes are journaled and restored");
            Check(f.Start(alice,Blueprint(100),-40),"start disconnect test");f.Tick();f.Players.m_componentPlayers.Remove(alice);f.Drain();Check(f.Terrain.Occupied==1,"disconnect restores partial build");
            f.Players.m_componentPlayers.Add(alice);Check(f.Start(alice,Blueprint(100),-40),"start undo pause test");f.Drain();
            var blocked=new CellPos(-37,64,0);f.Terrain.Blocked.Add(blocked);f.Host.UndoDirect(alice);for(int i=0;i<5;i++)f.Tick();Check(f.Host.Busy,"unloaded undo cell keeps restoration pending");
            f.Host.CancelDirect(alice);Check(!f.Host.Busy,"own undo can pause");Check(!f.Start(bob,Blueprint(),40),"pending restore prevents losing the undo journal");
            f.Terrain.Blocked.Clear();f.Host.UndoDirect(alice);f.Drain();Check(f.Terrain.Occupied==1,"resume restores remaining journal");
            f.Terrain.Cells[new CellPos(40,64,0)]=3;Check(!f.Start(alice,Blueprint(),40),"default no-overwrite remains");Check(f.Start(alice,Blueprint(),40,true),"explicit clear can start directly");f.Drain();
            for(int i=0;i<40;i++)f.Tick();Check(Math.Abs(alice.ComponentLocomotion.WalkSpeed-6)<.0001,"held movement stays +20 percent");
            ((ComponentInventory)alice.ComponentMiner.Inventory).m_slots[0].Value=3;f.Tick();Check(alice.ComponentLocomotion.WalkSpeed==5,"unequip restores increment");
            Check(Pending.Count==0,"direct scheduler never generates a custom packet");
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
        foreach(var asm in new[]{typeof(Block).Assembly,typeof(BlueprintToolBlock).Assembly})foreach(var type in asm.GetTypes())
        {
            if(!type.IsAbstract&&typeof(IPackage).IsAssignableFrom(type))Check(((IPackage)FormatterServices.GetUninitializedObject(type)).ID!=237,"237 vacant against actual native/mod packets");
            if(type.IsSubclassOf(typeof(Block))&&type!=typeof(BlueprintToolBlock)){var id=type.GetField("Index",BindingFlags.Public|BindingFlags.Static|BindingFlags.FlattenHierarchy);if(id!=null&&id.IsLiteral)Check((int)id.GetRawConstantValue()!=971,"971 vacant against actual native/mod blocks");}
        }
        Check(CodexRules.Bit(971)==256&&CodexRules.Bit(953)==128&&CodexRules.Bit(942)==1,"existing codex bits preserved and ninth appended");
        Check(CodexRules.Unlocked(0,971)&&CodexRules.Unlocked(511,971),"drawing is directly usable without true-name unlock");
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
            SCIEPort.Blueprints.Tests.BlueprintHttpTests.Run().GetAwaiter().GetResult();Engine.Log.AddLogSink(new Sink());TransferTests();IdentityAndCodecTests();DirectTests();NativeDirectTests();LibraryAndCopyTests();TestCloudBlueprints().GetAwaiter().GetResult();
            Console.WriteLine("Blueprint direct Local/Server/Client, API/codec and terrain: "+checks+" checks passed. No actual game room or Android device.");
        }
        finally { CommonLib.WorkType=oldMode;CommonLib.Net=oldNet;SubsystemBedrockBlockBehavior.Territories=oldTerritories; }
    }
}

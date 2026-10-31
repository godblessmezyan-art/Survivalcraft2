using System;
using System.Collections.Generic;
using Engine;
using Game;
using Game.NetWork;
namespace SCIEPort.Features.AbyssReaper
{
    // All registry writes and player movement are performed from the subsystem update thread.
    internal sealed class VoidPortalNetwork:IDisposable
    {
        internal const byte Sync=0,Create=1,Rename=2,Delete=3,Choose=4,Ready=5,Ack=6,Cancel=7;
        internal const byte BeginRows=1,Row=2,EndRows=3,Reply=4,Prepare=5,Commit=6,Finish=7;
        private sealed class Peer
        {
            internal ComponentPlayer Player;internal Client Client;internal uint Processed,SentRevision;
            internal double NextCommand,NextSync;internal ushort Entity;internal Guid Token;
        }
        private struct Pending{internal ComponentPlayer Player;internal Client Client;internal Guid Token;internal VoidPortalPackage Packet;}
        private readonly SubsystemAbyssReaper owner;
        private readonly SubsystemPlayers players;
        private readonly SubsystemTerrain terrain;
        private readonly SubsystemTime clock;
        private readonly Queue<Pending> queue=new Queue<Pending>();
        private readonly Dictionary<Guid,Peer> peers=new Dictionary<Guid,Peer>();
        private readonly Dictionary<ComponentPlayer,VoidPortalDialog> dialogs=new Dictionary<ComponentPlayer,VoidPortalDialog>();
        private readonly List<ComponentPlayer> closed=new List<ComponentPlayer>();
        private readonly List<Guid> retired=new List<Guid>();
        private uint revision=1,sent,received;private double nextSync;private Guid replicaConnection;
        private VoidPortalRegistry staging;private uint stagingRevision;private int expected;private double stagingUntil;
        private bool disposed;
        internal string Feedback{get;private set;}="";
        internal Guid LastCreated{get;private set;}
        internal uint ChoiceSequence{get;private set;}
        private Guid choiceSource,choiceTarget;
        private ComponentPlayer choicePlayer;private Guid serverToken;
        internal bool MatchesChoice(ComponentPlayer p,VoidPortalPackage packet) => choicePlayer==p&&serverToken==(CommonLib.Net?.Server?.TokenId??Guid.Empty)
            &&ChoiceSequence!=0&&packet.Sequence==ChoiceSequence&&packet.Portal==choiceSource&&packet.Target==choiceTarget;
        internal VoidPortalLocalTravel Travel{get;set;}
        internal bool Authority => CommonLib.WorkType==WorkType.Local||CommonLib.WorkType==WorkType.Server;
        internal VoidPortalNetwork(SubsystemAbyssReaper owner,SubsystemPlayers players,SubsystemTerrain terrain,SubsystemTime clock)
        {this.owner=owner;this.players=players;this.terrain=terrain;this.clock=clock;}
        internal void Enqueue(ComponentPlayer player,Client client,VoidPortalPackage packet)
        {lock(queue)if(!disposed&&queue.Count<256)queue.Enqueue(new Pending{Player=player,Client=client,Token=client?.TokenId??packet.From?.TokenId??Guid.Empty,Packet=packet.Copy()});}
        internal bool Actor(ComponentPlayer p)
            => p!=null&&p.Project==owner.Project&&p.IsAddedToProject&&p.PlayerGuid!=Guid.Empty&&p.PlayerData.IsReadyForPlaying
                &&p.ComponentHealth.Health>0&&p.ComponentSleep.SleepFactor<=0&&p.ComponentRider.Mount==null
                &&p.ComponentBody.ParentBody==null&&p.ComponentBody.ChildBodies.Count==0
                &&p.Project.FindSubsystem<SCIEPort.Features.FourSaints.SubsystemFourSaints>(false)?.IsActionLocked(p)!=true;
        internal Client Connection(ComponentPlayer p)=>p?.PlayerData.Client;
        private bool Access(ComponentPlayer p)
        {
            if(!Actor(p)||owner.Portals==null)return false;
            if(Terrain.ExtractContents(p.ComponentMiner.ActiveBlockValue)==AbyssReaperBlock.Index)return true;
            foreach(var gate in owner.Portals.Records)if(Near(p,gate.Id))return true;
            return false;
        }
        internal bool Near(ComponentPlayer p,Guid id)
        {
            VoidPortalRegistry.Portal gate;if(!Actor(p)||owner.Portals==null||!owner.Portals.TryGet(id,out gate))return false;
            Vector3 eye=p.ComponentCreatureModel.EyePosition,at=new Vector3(gate.X,gate.Y+1.5f,gate.Z);
            return gate.Active&&WorldCutDamage.Finite(eye)&&Vector3.DistanceSquared(eye,at)<=36
                &&VoidCombatSafety.ClearPath(terrain,eye,at,6);
        }
        internal bool Open(ComponentPlayer p,Guid source)
        {
            if(!Actor(p)||dialogs.ContainsKey(p))return false;
            var dialog=new VoidPortalDialog(owner,p,source);dialogs.Add(p,dialog);
            DialogsManager.ShowDialog(p.GuiWidget,dialog);Request(p,Sync,Guid.Empty,Guid.Empty,"");return true;
        }
        internal void Close(ComponentPlayer p,VoidPortalDialog dialog)
        {dialogs.Remove(p);dialog.Release();DialogsManager.HideDialog(dialog);}
        internal bool HasDialog(ComponentPlayer p)=>dialogs.ContainsKey(p);
        internal void Request(ComponentPlayer p,byte command,Guid source,Guid target,string name)
        {
            if(disposed||p==null)return;
            if(!Authority&&CommonLib.Net?.Server==null){Feedback="未连接主机，请重新连接后重试";return;}
            if(unchecked(++sent)==0)++sent;
            var packet=new VoidPortalPackage{Kind=0,Command=command,Caster=p.Entity.EntityId,Sequence=sent,Portal=source,Target=target,Text=name??"",Direction=p.ComponentCreatureModel.EyeRotation.GetForwardVector()};
            if(command==Choose){ChoiceSequence=sent;choiceSource=source;choiceTarget=target;choicePlayer=p;serverToken=CommonLib.Net?.Server?.TokenId??Guid.Empty;}
            Feedback=command==Sync?"正在读取共享星门…":"正在处理…";
            if(Authority)Enqueue(p,null,packet);else CommonLib.Net?.QueuePackage(packet);
        }
        internal void Signal(ComponentPlayer p,byte command,uint sequence,Guid source,Guid target)
        {
            var packet=new VoidPortalPackage{Kind=0,Command=command,Caster=p.Entity.EntityId,Sequence=sequence,Portal=source,Target=target};
            if(Authority)Enqueue(p,null,packet);else CommonLib.Net?.QueuePackage(packet);
        }
        internal void Send(ComponentPlayer p,VoidPortalPackage packet)
        {
            packet.Caster=p.Entity.EntityId;
            if(p.PlayerData.IsMainPlayer){Receive(packet);return;}
            packet.To=Connection(p);if(packet.To!=null&&CommonLib.WorkType==WorkType.Server)CommonLib.Net?.QueuePackage(packet);
        }
        internal void Message(ComponentPlayer p,string text)
            => Send(p,new VoidPortalPackage{Kind=Reply,Text=text});
        private Peer Bind(ComponentPlayer p,Client client)
        {
            Peer peer;if(!peers.TryGetValue(p.PlayerGuid,out peer))
            {if(peers.Count>=64)return null;peer=new Peer();peers.Add(p.PlayerGuid,peer);}
            Guid token=client?.TokenId??Guid.Empty;
            if(peer.Player!=p||peer.Entity!=p.Entity.EntityId||peer.Token!=token)
            {peer.Processed=0;peer.SentRevision=0;peer.NextCommand=peer.NextSync=0;}
            peer.Player=p;peer.Entity=p.Entity.EntityId;peer.Token=token;peer.Client=client;return peer;
        }
        private void Process(Pending pending)
        {
            var p=pending.Player;var packet=pending.Packet;
            if(!Actor(p)||packet.Caster!=p.Entity.EntityId||packet.Sequence==0)return;
            if(pending.Client!=null&&(pending.Client.State<ClientState.ProjectLoaded||pending.Client.SubsystemPlayers==null||pending.Token!=pending.Client.TokenId||!ReferenceEquals(Connection(p),pending.Client)||pending.Client.PlayerData?.ComponentPlayer!=p))return;
            if(packet.Command==Ready||packet.Command==Ack||packet.Command==Cancel)
            {Travel?.Signal(p,packet);return;}
            var peer=Bind(p,pending.Client);if(peer==null||!WorldCutRules.Newer(packet.Sequence,peer.Processed))return;
            peer.Processed=packet.Sequence;
            if(packet.Command>Choose)return;
            // Opening/refreshing the list must not consume the mutation cooldown.
            if(packet.Command==Sync)
            {
                if(clock.GameTime<peer.NextSync){Message(p,"刷新过于频繁，请稍后重试");return;}
                peer.NextSync=clock.GameTime+.25;Snapshot(p,peer);return;
            }
            if(clock.GameTime<peer.NextCommand){Message(p,"操作过于频繁，请稍后重试");return;}
            peer.NextCommand=clock.GameTime+.15;
            if(owner.Portals==null){Message(p,"星门存档不可用，原始记录已保留");return;}
            if(!Access(p)){Message(p,"请持有终焉之镰或靠近任意星门");return;}
            if(owner.PortalBusy(p)){Message(p,"请先结束当前攻击或技能");return;}
            if(packet.Command==Create)
            {
                if(!VoidPortalRegistry.ValidName(packet.Text)){Message(p,"星门名称无效：请输入1至32个有效字符");return;}
                if(owner.Portals.Count>=owner.Portals.Limit){Message(p,"永久星门数量已满（"+owner.Portals.Count+"/"+owner.Portals.Limit+"），请先删除不需要的星门");return;}
                Guid id;if(!CreateAimed(p,packet.Direction,packet.Text,out id)){Message(p,"建立失败：需3格宽实心地面、4格高净空；请避开领地、生物和已有星门后重试");return;}
                Changed();Send(p,new VoidPortalPackage{Kind=Reply,Portal=id,Text="永久星门已建立并记录，关闭面板即可查看；可选择另一座门作为目的地"});
            }
            else if(packet.Command==Rename||packet.Command==Delete)
            {
                bool ok=packet.Command==Rename?owner.PortalOperations.Rename(p,packet.Portal,packet.Text):owner.PortalOperations.Delete(p,packet.Portal);
                if(ok){Changed();Message(p,packet.Command==Rename?"名称已保存":"星门已删除，所有玩家的列表将同步");}
                else Message(p,"操作失败：星门已被删除或名称无效");
            }
            else
            {
                VoidPortalRegistry.Portal target;
                if(!owner.Portals.TryGet(packet.Target,out target)||!target.Active){Message(p,"目的地已删除，请重新选择");return;}
                Guid source=packet.Portal;
                if(source==Guid.Empty)
                {
                    if(owner.Portals.Count>=owner.Portals.Limit){Message(p,"星门数量已满，请使用已有星门或删除一座后再开门");return;}
                    if(Terrain.ExtractContents(p.ComponentMiner.ActiveBlockValue)!=AbyssReaperBlock.Index){Message(p,"随处开门需要持有终焉之镰");return;}
                    if(!CreateEntrance(p,out source)){Message(p,"附近没有可建立永久星门的空地，请换个位置");return;}
                    Changed();
                }
                if(source==target.Id||!Near(p,source)){Message(p,"请选择另一座目的地，并靠近出发星门");return;}
                Travel?.Choose(p,source,target.Id,packet.Sequence);
                Send(p,new VoidPortalPackage{Kind=Reply,Command=Choose,Sequence=packet.Sequence,Portal=source,Target=target.Id,Text="目的地已选择，走入出发星门即可传送"});
            }
        }
        private bool CreateAimed(ComponentPlayer p,Vector3 direction,string name,out Guid id)
        {
            id=Guid.Empty;if(!VoidPortalRegistry.ValidName(name)||!WorldCutDamage.Finite(direction)||direction.LengthSquared()<.9f||direction.LengthSquared()>1.1f)return false;
            Vector3 eye=p.ComponentCreatureModel.EyePosition;direction=Vector3.Normalize(direction);
            var hit=terrain.Raycast(eye,eye+direction*6,false,true,(v,d)=>BlocksManager.Blocks[Terrain.ExtractContents(v)].IsCollidable);
            // The modal list freezes world aiming. Level/sky views use a bounded search in front
            // of the player; an explicitly aimed ground site still has to pass its own checks.
            if(!hit.HasValue||hit.Value.CellFace.Face!=4)return CreateEntrance(p,direction,name,out id);
            var f=hit.Value.CellFace;return owner.PortalOperations.TryCreate(p,f.X,f.Y+1,f.Z,Math.Abs(direction.Z)>=Math.Abs(direction.X),name,out id);
        }
        private bool CreateEntrance(ComponentPlayer p,out Guid id)
            => CreateEntrance(p,p.ComponentCreatureModel.EyeRotation.GetForwardVector(),"星门 "+(owner.Portals.Count+1),out id);
        private bool CreateEntrance(ComponentPlayer p,Vector3 direction,string name,out Guid id)
        {
            id=Guid.Empty;Vector3 forward=direction;forward.Y=0;
            if(WorldCutDamage.Finite(forward)&&forward.LengthSquared()<.01f){forward=p.ComponentBody.Rotation.GetForwardVector();forward.Y=0;}
            if(!WorldCutDamage.Finite(forward)||forward.LengthSquared()<.01f)return false;forward=Vector3.Normalize(forward);
            Vector3 right=Vector3.Cross(forward,Vector3.UnitY);
            for(int i=0;i<6;i++)
            {
                Vector3 at=p.ComponentBody.Position+forward*(i<3?3:4)+right*((i%3)-1)*1.5f;
                if(!VoidPortalSafety.Ready(terrain,Terrain.ToCell(at.X),Terrain.ToCell(at.Z)))continue;
                var hit=terrain.Raycast(at+Vector3.UnitY*2,at-Vector3.UnitY*4,false,true,(v,d)=>BlocksManager.Blocks[Terrain.ExtractContents(v)].IsCollidable);
                if(!hit.HasValue||hit.Value.CellFace.Face!=4)continue;
                var f=hit.Value.CellFace;
                if(owner.PortalOperations.TryCreate(p,f.X,f.Y+1,f.Z,Math.Abs(forward.Z)>=Math.Abs(forward.X),name,out id))return true;
            }
            return false;
        }
        private void Changed(){if(unchecked(++revision)==0)++revision;}
        private void Snapshot(ComponentPlayer p,Peer peer)
        {
            var registry=owner.Portals;if(registry==null){Message(p,"星门存档不可用，原记录已保留");return;}
            Send(p,new VoidPortalPackage{Kind=BeginRows,Revision=revision,Owner=registry.WorldId,Count=(byte)registry.Count,Limit=(byte)registry.Limit});
            foreach(var gate in registry.Records)Send(p,new VoidPortalPackage{Kind=Row,Command=(byte)(gate.Active?1:0),Revision=revision,Portal=gate.Id,Owner=gate.Owner,Point=new Vector3(gate.X,gate.Y,gate.Z),Facing=gate.Facing,Text=gate.Name});
            Send(p,new VoidPortalPackage{Kind=EndRows,Revision=revision});peer.SentRevision=revision;
        }
        private ComponentPlayer Local(ushort entity)
        {foreach(var p in players.ComponentPlayers)if(p.PlayerData.IsMainPlayer&&p.Entity.EntityId==entity)return p;return null;}
        private void Receive(VoidPortalPackage packet)
        {
            var p=Local(packet.Caster);if(p==null)return;
            if(packet.Kind==Reply)
            {
                Feedback=packet.Text;
                if(packet.Portal!=Guid.Empty)LastCreated=packet.Portal;
                if(packet.Command==Choose&&choicePlayer==p&&packet.Sequence==ChoiceSequence&&packet.Target==choiceTarget)
                {
                    choiceSource=packet.Portal;
                    VoidPortalDialog d;if(dialogs.TryGetValue(p,out d))Close(p,d);
                }
                // A corrupt/unsupported world record is already preserved and logged at load.
                // Keep its error in the portal dialog, but do not show an unsolicited HUD toast
                // when the initial background sync runs without an open dialog.
                if(packet.Text!="星门存档不可用，原记录已保留" || dialogs.ContainsKey(p))
                    p.ComponentGui?.DisplaySmallMessage(packet.Text,Color.White,false,false);
                return;
            }
            if(packet.Kind>=Prepare){Travel?.Receive(p,packet);return;}
            if(Authority)return;
            if(packet.Kind==BeginRows)
            {
                if(packet.Revision==0||(received!=0&&packet.Revision!=received&&!WorldCutRules.Newer(packet.Revision,received))||packet.Owner==Guid.Empty||packet.Count>64||packet.Limit<1||packet.Limit>64||packet.Count>packet.Limit)return;
                staging=new VoidPortalRegistry(packet.Owner,packet.Limit);stagingRevision=packet.Revision;expected=packet.Count;stagingUntil=clock.GameTime+5;
            }
            else if(staging!=null&&packet.Revision==stagingRevision)
            {
                if(packet.Kind==Row)
                {
                    if(packet.Command>1||!staging.TryAdd(packet.Portal,packet.Owner,packet.Text,packet.Point.X,packet.Point.Y,packet.Point.Z,packet.Facing))staging=null;
                    else{VoidPortalRegistry.Portal gate;staging.TryGet(packet.Portal,out gate);gate.Active=packet.Command==1;}
                }
                else if(packet.Kind==EndRows)
                {
                    if(staging.Count==expected)
                    {
                        owner.AcceptPortalSnapshot(staging);received=stagingRevision;
                        if(string.IsNullOrEmpty(Feedback)||Feedback=="正在读取共享星门…")Feedback="共享星门已同步";
                    }
                    staging=null;
                }
            }
        }
        internal void Update()
        {
            if(disposed)return;
            if(!Authority&&replicaConnection!=(CommonLib.Net?.Server?.TokenId??Guid.Empty))
            {
                replicaConnection=CommonLib.Net?.Server?.TokenId??Guid.Empty;received=0;staging=null;
                if(serverToken!=replicaConnection)ChoiceSequence=0;
                if(CommonLib.Net?.Server!=null)foreach(var p in players.ComponentPlayers)if(p.PlayerData.IsMainPlayer&&Actor(p)){Request(p,Sync,Guid.Empty,Guid.Empty,"");break;}
            }
            for(int i=0;i<32;i++)
            {
                Pending pending;lock(queue){if(queue.Count==0)break;pending=queue.Dequeue();}
                try{if(Authority)Process(pending);else if(pending.Token==(CommonLib.Net?.Server?.TokenId??Guid.Empty))Receive(pending.Packet);}
                catch(Exception e){Log.Warning("[SCIEPort:Portals] Command failed: "+e.GetType().Name);if(pending.Player!=null)Message(pending.Player,"星门操作失败，请重试");}
            }
            if(staging!=null&&clock.GameTime>stagingUntil)staging=null;
            closed.Clear();foreach(var pair in dialogs)if(!Actor(pair.Key)||pair.Key.GameWidget==null)closed.Add(pair.Key);
            foreach(var p in closed)Close(p,dialogs[p]);
            if(!Authority)return;
            retired.Clear();foreach(var pair in peers)if(!Actor(pair.Value.Player))retired.Add(pair.Key);
            foreach(var id in retired)peers.Remove(id);
            // A failed save load intentionally leaves Portals null and the raw record intact.
            // Do not re-issue a failed background snapshot every 0.25 game seconds.
            // Explicit Sync still goes through Snapshot and reports the problem to the player.
            if(owner.Portals==null)return;
            if(clock.GameTime<nextSync)return;nextSync=clock.GameTime+.25;
            int budget=2;
            foreach(var p in players.ComponentPlayers)
            {
                if(!Actor(p))continue;
                Client connection=p.PlayerData.IsMainPlayer?null:Connection(p);
                if(!p.PlayerData.IsMainPlayer&&(connection==null||connection.State<ClientState.ProjectLoaded))continue;
                var peer=Bind(p,connection);if(peer!=null&&peer.SentRevision!=revision){Snapshot(p,peer);if(--budget<=0)break;}
            }
        }
        public void Dispose()
        {disposed=true;lock(queue)queue.Clear();closed.Clear();closed.AddRange(dialogs.Keys);foreach(var p in closed)Close(p,dialogs[p]);peers.Clear();staging=null;}
    }
}

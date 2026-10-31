using System;
using System.Collections.Generic;
using Engine;
using Game;
using Game.NetWork;
using GameEntitySystem;
using TemplatesDatabase;
namespace SCIEPort.Features.AbyssReaper
{
    internal sealed partial class DeathAuthorityRuntime:IDisposable
    {
        internal sealed class State
        {
            internal ComponentPlayer Player;internal Guid Token;internal ushort Entity;internal uint Sent,Processed,Revision,LastWave,LastSwing;
            internal bool Death;internal double EmpowerUntil,EmpowerReady,WaveReady,SummonReady,ToggleReady,PendingUntil,NextRequest,NextStrike,LastStrike=-100,NextSync;
            internal int Combo;internal string Feedback;internal double FeedbackUntil,NextManagementRequest;
        }
        internal sealed class WaveCast{internal ComponentPlayer Owner;internal Guid Guid;internal uint Sequence;internal Vector3 Center;internal double Started;internal bool Impacted,Visual;internal int Cursor;internal readonly DynamicArray<ComponentBody> Targets=new DynamicArray<ComponentBody>();internal readonly HashSet<Entity> Seen=new HashSet<Entity>();}
        private struct Pending{internal ComponentPlayer Player;internal Client Client;internal Guid Token;internal DeathAuthorityPackage Packet;}
        private sealed class Mark{internal Guid Owner;internal double Until;}
        private readonly SubsystemAbyssReaper parent;private readonly SubsystemTerrain terrain;private readonly SubsystemTime clock;private readonly SubsystemBodies bodies;private readonly SubsystemPlayers players;
        private readonly Dictionary<Guid,State> states=new Dictionary<Guid,State>();
        private readonly Queue<Pending> queue=new Queue<Pending>();
        private readonly Dictionary<ComponentCreature,Mark> marks=new Dictionary<ComponentCreature,Mark>();
        private readonly List<ComponentCreature> retiredMarks=new List<ComponentCreature>();
        private readonly List<WaveCast> waves=new List<WaveCast>();
        private readonly DynamicArray<ComponentBody> candidates=new DynamicArray<ComponentBody>();
        private readonly HashSet<Entity> swingHits=new HashSet<Entity>();
        private readonly DeathAuthorityScene scene=new DeathAuthorityScene();
        private readonly Dictionary<DrawBlockEnvironmentData,ComponentPlayer> drawOwners=new Dictionary<DrawBlockEnvironmentData,ComponentPlayer>();
        private uint revision;private bool disposed;private double nextDrawBind;private Guid serverToken;
        private bool hasClock;private double clockOffset,lastServerTime;private readonly List<DeathAuthorityPackage> deferredWaves=new List<DeathAuthorityPackage>();
        private Guid worldId=Guid.NewGuid();private bool worldKnown;private readonly List<double> syncEchoes=new List<double>();
        private double Now=>clock.GameTime;
        internal int Quality=1;
        internal bool Authority=>CommonLib.WorkType==WorkType.Local||CommonLib.WorkType==WorkType.Server;
        internal DeathAuthorityRuntime(SubsystemAbyssReaper parent,SubsystemTerrain terrain,SubsystemBodies bodies,SubsystemTime clock,SubsystemPlayers players)
        {this.parent=parent;this.terrain=terrain;this.bodies=bodies;this.clock=clock;this.players=players;}
        private bool Actor(ComponentPlayer p,bool held=true)=>p!=null&&p.Project==terrain.Project&&p.IsAddedToProject&&p.PlayerGuid!=Guid.Empty&&p.PlayerData.IsReadyForPlaying&&p.ComponentHealth.Health>0&&(!held||Terrain.ExtractContents(p.ComponentMiner.ActiveBlockValue)==952&&p.ComponentSleep.SleepFactor<=0&&p.ComponentRider.Mount==null&&p.ComponentBody.ParentBody==null&&p.ComponentBody.ChildBodies.Count==0);
        private static bool Management(byte action)=>action==DeathAuthorityRules.Summon||action==DeathAuthorityRules.Sync||action==DeathAuthorityRules.Dismiss||action==DeathAuthorityRules.Recall||action==DeathAuthorityRules.Stow||action==DeathAuthorityRules.Manifest;
        private bool CommandActor(ComponentPlayer p,byte action)=>Management(action)?Actor(p,false)&&(action==DeathAuthorityRules.Sync||Terrain.ExtractContents(p.ComponentMiner.ActiveBlockValue)==952&&p.ComponentSleep.SleepFactor<=0):Actor(p);
        private State Get(ComponentPlayer p)
        {
            if(p==null)return null;State s;if(!states.TryGetValue(p.PlayerGuid,out s)){if(states.Count>=64)return null;states.Add(p.PlayerGuid,s=new State());}
            Guid token=CommonLib.WorkType==WorkType.Server&&!p.PlayerData.IsMainPlayer?p.PlayerData.Client?.TokenId??Guid.Empty:!Authority?CommonLib.Net?.Server?.TokenId??Guid.Empty:Guid.Empty;
            if(s.Player!=p||s.Token!=token||s.Entity!=p.Entity.EntityId){s.Player=p;s.Token=token;s.Entity=p.Entity.EntityId;s.Processed=0;s.Revision=s.LastWave=s.LastSwing=0;s.PendingUntil=0;s.EmpowerUntil=0;}
            return s;
        }
        internal bool IsDeath(ComponentPlayer p)=>Get(p)?.Death==true;
        internal bool Empowered(ComponentPlayer p)=>Actor(p)&&IsDeath(p)&&(Get(p)?.EmpowerUntil??0)>Now;
        internal double EmpowerRemaining(ComponentPlayer p)=>Empowered(p)?Math.Max(0,Get(p).EmpowerUntil-Now):0;
        internal bool Busy(ComponentPlayer p){if((Get(p)?.PendingUntil??0)>Now)return true;foreach(var w in waves)if(w.Owner==p&&Now<w.Started+DeathAuthorityRules.WaveDuration)return true;return false;}
        internal double Remaining(ComponentPlayer p,byte action){var s=Get(p);return s==null?0:Math.Max(0,(action==1?s.ToggleReady:action==2?s.EmpowerReady:action==3?s.WaveReady:s.SummonReady)-Now);}
        internal string Feedback(ComponentPlayer p){var s=Get(p);return s!=null&&Now<s.FeedbackUntil?s.Feedback:null;}
        private void Message(ComponentPlayer p,string value){var s=Get(p);if(s==null)return;s.Feedback=value;s.FeedbackUntil=Now+3;}
        private bool Locked(ComponentPlayer p)=>parent.WorldCut?.Busy(p)==true||parent.Space?.Busy(p)==true||parent.Horizon?.Busy(p)==true||parent.PortalTransferLocked(p)||p.Project.FindSubsystem<SCIEPort.Features.FourSaints.SubsystemFourSaints>(false)?.IsActionLocked(p)==true;
        internal bool Request(ComponentPlayer p,byte action,Vector3 direction,ushort target=0)
        {
            if(disposed||p==null||!p.PlayerData.IsMainPlayer)return false;
            if(!CommandActor(p,action)){Message(p,Management(action)?"请保持持有终焉之镰，清醒且存活时管理军势":"当前角色状态不能使用死亡权柄");return false;}
            var s=Get(p);if(s==null)return false;
            if(!Authority&&!worldKnown&&action!=DeathAuthorityRules.Sync){Message(p,"正在同步死亡权柄，请稍候");return false;}
            if(action!=DeathAuthorityRules.Sync&&(Locked(p)||Busy(p)&&action!=DeathAuthorityRules.Strike)){Message(p,"请先结束当前动作");return false;}
            if(unchecked(++s.Sent)==0)++s.Sent;
            var packet=new DeathAuthorityPackage{Kind=0,Action=action,Caster=p.Entity.EntityId,Sequence=s.Sent,Direction=direction,Target=target,Echo=action==6?Time.RealTime:0,World=worldId};
            if(!Authority&&action==6){if(syncEchoes.Count>=4)syncEchoes.RemoveAt(0);syncEchoes.Add(packet.Echo);}
            if(action!=DeathAuthorityRules.Strike&&action!=DeathAuthorityRules.Sync){s.PendingUntil=Now+2;Message(p,Authority?"正在处理军势操作…":"已发送军势操作，等待房主响应…");}
            if(Authority){if(!Enqueue(p,null,packet)){s.PendingUntil=0;Message(p,"操作队列已满，请稍后重试");return false;}}
            else if(CommonLib.Net?.Server!=null)CommonLib.Net.QueuePackage(packet);else{s.PendingUntil=0;Message(p,"尚未连接房主");return false;}return true;
        }
        internal bool Enqueue(ComponentPlayer p,Client from,DeathAuthorityPackage packet)
        {lock(queue){if(disposed||queue.Count>=DeathAuthorityRules.QueueLimit)return false;queue.Enqueue(new Pending{Player=p,Client=from,Token=from?.TokenId??Guid.Empty,Packet=packet.Copy()});return true;}}
        private uint Revision(){if(unchecked(++revision)==0)++revision;return revision;}
        private void Send(DeathAuthorityPackage packet,Client to=null)
        {if(CommonLib.WorkType!=WorkType.Server)return;packet.To=to;packet.Revision=Revision();packet.Stamp=Now;packet.World=worldId;CommonLib.Net?.QueuePackage(packet);}
        private void StatePacket(ComponentPlayer p,string text=null,double echo=0)
        {
            var s=Get(p);if(s==null)return;if(text!=null)Message(p,text);s.PendingUntil=0;
            Send(new DeathAuthorityPackage{Kind=1,Caster=p.Entity.EntityId,Owner=p.PlayerGuid,Flags=(byte)(s.Death?1:0),Sequence=s.Processed,A=(float)Math.Max(0,s.EmpowerUntil-Now),B=(float)Math.Max(0,s.EmpowerReady-Now),C=(float)Math.Max(0,s.WaveReady-Now),D=(float)Math.Max(0,s.SummonReady-Now),Name=text??"",Echo=echo});
        }
        private void Process(Pending pending)
        {
            var p=pending.Player;var q=pending.Packet;
            if(p==null||q.Caster!=p.Entity.EntityId||q.Sequence==0)return;
            if(q.Action!=DeathAuthorityRules.Sync&&q.World!=worldId)return;
            if(pending.Client!=null&&(pending.Client.TokenId!=pending.Token||pending.Client.State<ClientState.ProjectLoaded||!ReferenceEquals(p.PlayerData.Client,pending.Client)||pending.Client.PlayerData?.ComponentPlayer!=p))return;
            if(!CommandActor(p,q.Action)){StatePacket(p,"角色状态变化，请保持持有终焉之镰后重试");return;}
            var s=Get(p);if(s==null||!WorldCutRules.Newer(q.Sequence,s.Processed))return;s.Processed=q.Sequence;
            if(q.Action==DeathAuthorityRules.Sync){if(Now>=s.NextSync){s.NextSync=Now+1;StatePacket(p,null,q.Echo);SyncFriends(pending.Client);}return;}
            if(loadFailure!=null){StatePacket(p,"死亡契约存档版本不受支持，已保留原数据");return;}
            if(!Management(q.Action)&&clock.GameTimeDelta<=0||Locked(p)){StatePacket(p,"当前动作尚未结束，请稍后管理军势");return;}
            if(q.Action==DeathAuthorityRules.Strike){Strike(p,q.Direction);return;}
            bool management=Management(q.Action);double requestTime=management?Time.RealTime:Now;
            if(requestTime<(management?s.NextManagementRequest:s.NextRequest)||BusyWave(p)){StatePacket(p,"操作过快或领域尚未结束");return;}
            if(management)s.NextManagementRequest=requestTime+.15;else s.NextRequest=Now+.15;
            switch(q.Action)
            {
                case DeathAuthorityRules.Toggle:
                    if(Now<s.ToggleReady){StatePacket(p,"权柄切换冷却中");return;}
                    s.Death=!s.Death;s.EmpowerUntil=0;s.ToggleReady=Now+1;ClearMarks(p.PlayerGuid);parent.WorldCut?.Cancel(p);StatePacket(p,s.Death?"死亡权柄 · 夺魂与统御":"虚空权柄 · 裂界与湮灭");break;
                case DeathAuthorityRules.Empower:
                    if(!s.Death||Now<s.EmpowerReady||Now<s.EmpowerUntil){StatePacket(p,"赋能尚未就绪或正在持续");return;}
                    ClearMarks(p.PlayerGuid);s.EmpowerUntil=Now+DeathAuthorityRules.EmpowerDuration;s.EmpowerReady=Now+DeathAuthorityRules.EmpowerCooldown;parent.WorldCut?.Cancel(p);StatePacket(p,"夺魂赋刃 · 12秒 · 首击濒死，再击转化");break;
                case DeathAuthorityRules.Wave:
                    if(!s.Death||Now<s.WaveReady||waves.Count>=4){StatePacket(p,"死亡蔓延冷却中或容量已满");return;}
                    var cast=new WaveCast{Owner=p,Guid=p.PlayerGuid,Sequence=q.Sequence,Center=p.ComponentBody.Position,Started=Now};waves.Add(cast);s.WaveReady=Now+36;
                    Send(new DeathAuthorityPackage{Kind=2,Caster=p.Entity.EntityId,Owner=p.PlayerGuid,Sequence=q.Sequence,Position=cast.Center});StatePacket(p,"死亡领域 · 扩散完成湮灭敌人、回满军势");break;
                case DeathAuthorityRules.Summon:
                    if(!s.Death){StatePacket(p,"请先切换死亡权柄");return;}
                    if(Now<s.SummonReady){StatePacket(p,"召唤冷却中，还需"+Math.Ceiling(s.SummonReady-Now).ToString("0")+"秒");return;}
                    string reason;if(Summon(p,q.Target,out reason))s.SummonReady=Now+DeathAuthorityRules.SummonCooldown;StatePacket(p,reason);break;
                case DeathAuthorityRules.Dismiss:StatePacket(p,Dismiss(p,q.Target));break;
                case DeathAuthorityRules.Recall:Recall(p);StatePacket(p,"友军正在归队");break;
                case DeathAuthorityRules.Stow:StatePacket(p,StowArmy(p));break;
                case DeathAuthorityRules.Manifest:StatePacket(p,ManifestArmy(p));break;
                default:StatePacket(p,"无效的死亡权柄操作");break;
            }
        }
        private bool BusyWave(ComponentPlayer p){foreach(var w in waves)if(w.Owner==p&&Now<w.Started+DeathAuthorityRules.WaveDuration)return true;return false;}
        private void Receive(DeathAuthorityPackage q)
        {
            if(q.Name==null||q.Name.Length>96||!DeathAuthorityRules.Finite(q.Stamp)||q.Stamp<0)return;
            if(q.Kind==1&&syncEchoes.Contains(q.Echo))
            {
                double rtt=Time.RealTime-q.Echo;if(DeathAuthorityRules.Finite(rtt)&&rtt>=0&&rtt<8&&q.World!=Guid.Empty){worldId=q.World;worldKnown=true;hasClock=true;clockOffset=q.Stamp+rtt*.5-Now;syncEchoes.Remove(q.Echo);}
            }
            if(!worldKnown||q.World!=worldId)return;
            if(q.Kind==3||q.Kind==4||q.Kind==6||q.Kind==7){ReceiveFriend(q);return;}
            ComponentPlayer p=null;foreach(var candidate in players.ComponentPlayers)if(candidate.Entity.EntityId==q.Caster&&candidate.PlayerGuid==q.Owner){p=candidate;break;}if(p==null)return;
            var s=Get(p);if(s==null)return;
            if(q.Kind==1)
            {
                if(!WorldCutRules.Newer(q.Revision,s.Revision)||!WorldCutRules.Finite(q.A)||!WorldCutRules.Finite(q.B)||!WorldCutRules.Finite(q.C)||!WorldCutRules.Finite(q.D)||q.A<0||q.A>12.1f||q.B<0||q.B>3.1f||q.C<0||q.C>36.1f||q.D<0||q.D>30.1f)return;
                double rtt=Time.RealTime-q.Echo;if(p.PlayerData.IsMainPlayer&&q.Echo>0&&DeathAuthorityRules.Finite(rtt)&&rtt>=0&&rtt<8){hasClock=true;clockOffset=q.Stamp+rtt*.5-Now;}
                double age=hasClock?Math.Max(0,Math.Max(lastServerTime,Now+clockOffset)-q.Stamp):0;
                s.Revision=q.Revision;s.Death=(q.Flags&1)!=0;s.EmpowerUntil=Now+Math.Max(0,q.A-age);s.EmpowerReady=Now+Math.Max(0,q.B-age);s.WaveReady=Now+Math.Max(0,q.C-age);s.SummonReady=Now+Math.Max(0,q.D-age);s.PendingUntil=0;if(q.Name.Length>0)Message(p,q.Name);
                if(hasClock&&deferredWaves.Count>0){var waiting=deferredWaves.ToArray();deferredWaves.Clear();foreach(var packet in waiting)Receive(packet);}
            }
            else if(q.Kind==2&&WorldCutRules.Coordinates(q.Position.X,q.Position.Y,q.Position.Z)&&WorldCutRules.Finite(q.A)&&q.A>=0&&q.A<1.6f)
            {if(!hasClock){if(deferredWaves.Count<16)deferredWaves.Add(q);return;}double age=Math.Max(0,Math.Max(lastServerTime,Now+clockOffset)-q.Stamp)+q.A;if(age>=1.6||!WorldCutRules.Newer(q.Sequence,s.LastWave))return;s.LastWave=q.Sequence;if(waves.Count<4)waves.Add(new WaveCast{Owner=p,Guid=p.PlayerGuid,Sequence=q.Sequence,Center=q.Position,Started=Now-age,Visual=true});}
            else if(q.Kind==5&&q.Action<=2&&WorldCutDamage.Finite(q.Direction)&&WorldCutRules.Newer(q.Sequence,s.LastSwing)){s.LastSwing=q.Sequence;scene.Swing(p,q.Action,q.Direction,Now);}
        }
        internal void Update()
        {
            if(disposed)return;
            if(!Authority)
            {
                var token=CommonLib.Net?.Server?.TokenId??Guid.Empty;if(token!=serverToken){serverToken=token;clientFriends.Clear();friendRevisions.Clear();snapshotRows.Clear();snapshotId=lastSnapshot=0;hasClock=worldKnown=false;syncEchoes.Clear();lastServerTime=0;deferredWaves.Clear();waves.Clear();scene.Clear();foreach(var s in states.Values){s.Revision=0;s.EmpowerUntil=0;s.PendingUntil=0;s.NextSync=0;}}
                if(hasClock)lastServerTime=Math.Max(lastServerTime,Now+clockOffset);
                foreach(var p in players.ComponentPlayers)if(p.PlayerData.IsMainPlayer&&Actor(p,false)){var s=Get(p);if(Now>=s.NextSync){s.NextSync=Now+2;Request(p,DeathAuthorityRules.Sync,Vector3.UnitZ);}break;}
            }
            for(int i=0;i<8;i++){Pending v;lock(queue){if(queue.Count==0)break;v=queue.Dequeue();}try{if(Authority)Process(v);else if(ReferenceEquals(v.Client,CommonLib.Net?.Server)&&v.Token==serverToken)Receive(v.Packet);}catch(Exception e){Log.Warning("[SCIEPort:Death] Request failed: "+e.GetType().Name);if(v.Player!=null)StatePacket(v.Player,"死亡权柄操作失败");}}
            if(!Authority&&hasClock&&deferredWaves.Count>0){var waiting=deferredWaves.ToArray();deferredWaves.Clear();foreach(var packet in waiting)Receive(packet);}
            foreach(var s in states.Values){if(s.PendingUntil>0&&Now>=s.PendingUntil){s.PendingUntil=0;Message(s.Player,"房主响应超时，请重试");}if(!Actor(s.Player)||!s.Death){if(s.EmpowerUntil>0)ClearMarks(s.Player.PlayerGuid);s.EmpowerUntil=0;}}
            retiredMarks.Clear();foreach(var item in marks)if(!item.Key.IsAddedToProject||item.Key.ComponentHealth.Health<=0||Now>=item.Value.Until)retiredMarks.Add(item.Key);foreach(var c in retiredMarks)marks.Remove(c);
            if(Authority){UpdateDiscarded();UpdateGuards();UpdateWaves();}
            else{UpdateClientMounts();for(int i=waves.Count-1;i>=0;i--)if(Now>=waves[i].Started+1.6||!Actor(waves[i].Owner,false))waves.RemoveAt(i);}
            if(Now>=nextDrawBind){nextDrawBind=Now+.5;drawOwners.Clear();foreach(var p in players.ComponentPlayers){var fp=p.Entity.FindComponent<ComponentFirstPersonModel>(false);if(fp!=null)drawOwners[fp.m_drawBlockEnvironmentData]=p;var human=p.Entity.FindComponent<ComponentHumanModel>(false);if(human!=null)drawOwners[human.m_drawBlockEnvironmentData]=p;}}
            scene.Update(Now);
        }
        internal bool EmpoweredDraw(DrawBlockEnvironmentData e){ComponentPlayer p;return e!=null&&drawOwners.TryGetValue(e,out p)&&Empowered(p);}
        private void ClearMarks(Guid id){retiredMarks.Clear();foreach(var pair in marks)if(pair.Value.Owner==id)retiredMarks.Add(pair.Key);foreach(var c in retiredMarks)marks.Remove(c);}
        internal void Draw(Camera camera)=>scene.Draw(camera,waves,Now,Quality);
        public void Dispose(){if(disposed)return;disposed=true;ClearClientMounts();lock(queue)queue.Clear();marks.Clear();waves.Clear();states.Clear();guards.Clear();hidden.Clear();discardedEntities.Clear();clientFriends.Clear();friendRevisions.Clear();snapshotRows.Clear();deferredWaves.Clear();threats.Clear();saved.Clear();drawOwners.Clear();scene.Dispose();}
    }
}

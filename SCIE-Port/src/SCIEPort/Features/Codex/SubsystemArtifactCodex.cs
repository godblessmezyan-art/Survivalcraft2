using System;
using System.Collections.Generic;
using Engine;
using Game;
using Game.NetWork;
using TemplatesDatabase;
namespace SCIEPort.Features.Codex
{
    /// <summary>World-owned, GUID-keyed true-name permissions. Requests are authenticated and applied on Update.</summary>
    public sealed class SubsystemArtifactCodex:SubsystemBlockBehavior,IUpdateable
    {
        internal readonly Dictionary<Guid,uint> Records=new Dictionary<Guid,uint>();
        private readonly Dictionary<Guid,uint> received=new Dictionary<Guid,uint>(),accepted=new Dictionary<Guid,uint>(),sent=new Dictionary<Guid,uint>();
        private readonly Dictionary<Guid,double> lastRequest=new Dictionary<Guid,double>();
        private readonly Dictionary<Guid,Client> connections=new Dictionary<Guid,Client>();
        private readonly Dictionary<ComponentPlayer,float> walk=new Dictionary<ComponentPlayer,float>();
        private readonly List<ComponentPlayer> retired=new List<ComponentPlayer>();
        private readonly Queue<Pending> pending=new Queue<Pending>();
        private sealed class Pending{internal ComponentPlayer Player;internal Client Connection;internal Guid Token;internal CodexPackage Packet;}
        private SubsystemPlayers players;private SubsystemTime clock;private double nextSync;private uint revision;private bool disposed;private Guid serverToken;
        internal bool Authority=>CommonLib.WorkType==WorkType.Local||CommonLib.WorkType==WorkType.Server;
        /// <inheritdoc/>
        public override int[] HandledBlocks=>new[]{CodexRules.Book};
        /// <inheritdoc/>
        public UpdateOrder UpdateOrder=>(UpdateOrder)(-8);
        /// <inheritdoc/>
        public override void Load(ValuesDictionary values)
        {
            base.Load(values);players=Project.FindSubsystem<SubsystemPlayers>(true);clock=Project.FindSubsystem<SubsystemTime>(true);
            if(Authority)ReadRecords(values.GetValue<ValuesDictionary>("TrueNames",null));
        }
        internal void ReadRecords(ValuesDictionary values)
        {
            if(values==null)return;
            foreach(var pair in values){Guid guid;uint mask;if(Records.Count>=4096)break;if(SCIEPort.Compatibility.LegacyGuid.TryParse(pair.Key,out guid)&&guid!=Guid.Empty&&SCIEPort.Compatibility.LegacyParsing.TryUInt32(Convert.ToString(pair.Value,System.Globalization.CultureInfo.InvariantCulture),out mask))Records[guid]=mask&255;}
        }
        /// <inheritdoc/>
        public override void Save(ValuesDictionary values)
        {if(!Authority)return;var data=new ValuesDictionary();foreach(var pair in Records)data.SetValue(pair.Key.ToString("N"),pair.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));values.SetValue("TrueNames",data);}
        internal uint Mask(ComponentPlayer p)=>p!=null&&Records.TryGetValue(p.PlayerGuid,out var mask)?mask:0;
        internal static bool Held(ComponentPlayer p,int id)=>p!=null&&p.IsAddedToProject&&p.ComponentHealth.Health>0&&Terrain.ExtractContents(p.ComponentMiner.ActiveBlockValue)==id;
        internal static bool Allowed(ComponentPlayer p,int id)
        {
            if(CodexRules.Bit(id)==0)return true;
            return p!=null&&p.Project?.FindSubsystem<SubsystemArtifactCodex>(false)?.Has(p,id)==true;
        }
        internal bool Has(ComponentPlayer p,int id)=>CodexRules.Unlocked(Mask(p),id);
        internal static bool MayHold(ComponentPlayer p)=>p!=null&&Allowed(p,Terrain.ExtractContents(p.ComponentMiner.ActiveBlockValue));
        /// <inheritdoc/>
        public override bool OnUse(Ray3 ray,ComponentMiner miner)
        {
            var p=miner.ComponentPlayer;if(!Held(p,CodexRules.Book))return false;
            if(p.GameWidget!=null&&p.PlayerData.IsMainPlayer&&!DialogsManager.HasDialogs(p.GuiWidget)){DialogsManager.ShowDialog(p.GuiWidget,new CodexDialog(this,p));Request(p,0,0);}
            return true;
        }
        internal void Request(ComponentPlayer p,byte kind,byte entry)
        {
            if(!Held(p,CodexRules.Book))return;sent.TryGetValue(p.PlayerGuid,out var n);sent[p.PlayerGuid]=++n;
            var packet=new CodexPackage{Kind=kind,Entry=entry,Caster=p.Entity.EntityId,Owner=p.PlayerGuid,Sequence=n};
            if(Authority)Enqueue(p,packet);else if(CommonLib.WorkType==WorkType.Client)CommonLib.Net.QueuePackage(packet);
        }
        internal void Enqueue(ComponentPlayer p,CodexPackage packet)
        {
            var connection=p!=null?p.PlayerData.Client:packet.From;
            lock(pending){if(!disposed&&pending.Count<32)pending.Enqueue(new Pending{Player=p,Connection=connection,Token=connection?.TokenId??Guid.Empty,Packet=packet});}
        }
        internal void Accept(ComponentPlayer p,CodexPackage packet)
        {
            if(!Authority||!Held(p,CodexRules.Book)||packet.Owner!=p.PlayerGuid||packet.Caster!=p.Entity.EntityId||(packet.Kind!=0&&packet.Kind!=2)||packet.Entry>=CodexRules.Items.Length)return;
            if(!players.ComponentPlayers.Contains(p))return;
            if(!connections.TryGetValue(p.PlayerGuid,out var connection)||!ReferenceEquals(connection,p.PlayerData.Client)){connections[p.PlayerGuid]=p.PlayerData.Client;accepted.Remove(p.PlayerGuid);lastRequest.Remove(p.PlayerGuid);}
            accepted.TryGetValue(p.PlayerGuid,out var old);if(!CodexRules.Newer(packet.Sequence,old))return;
            accepted[p.PlayerGuid]=packet.Sequence;
            if(packet.Kind==2){if(lastRequest.TryGetValue(p.PlayerGuid,out var last)&&clock.GameTime-last<.2)return;lastRequest[p.PlayerGuid]=clock.GameTime;}
            if(packet.Kind==2&&(Records.ContainsKey(p.PlayerGuid)||Records.Count<4096))Records[p.PlayerGuid]=Mask(p)|CodexRules.Bit(CodexRules.Items[packet.Entry]);
            Publish(p);
        }
        private void Publish(ComponentPlayer p)
        {if(CommonLib.WorkType==WorkType.Server)CommonLib.Net.QueuePackage(new CodexPackage{Kind=1,Caster=p.Entity.EntityId,Owner=p.PlayerGuid,Sequence=++revision,Mask=Mask(p)});}
        internal void Receive(CodexPackage packet)
        {
            if(Authority||packet.Kind!=1||packet.Mask>255||packet.Owner==Guid.Empty)return;
            foreach(var p in players.ComponentPlayers)if(p.PlayerGuid==packet.Owner&&p.Entity.EntityId==packet.Caster){received.TryGetValue(packet.Owner,out var old);if(CodexRules.Newer(packet.Sequence,old)){received[packet.Owner]=packet.Sequence;Records[packet.Owner]=packet.Mask;}return;}
        }
        /// <inheritdoc/>
        public void Update(float dt)
        {
            if(clock.GameTimeDelta<=0)return;
            if(!Authority)
            {
                var token=SCIEPort.Compatibility.ServerConnection.Token(CommonLib.Net);
                if(token!=serverToken){serverToken=token;received.Clear();Records.Clear();}
            }
            for(int i=0;i<8;i++){Pending item;lock(pending){if(pending.Count==0)break;item=pending.Dequeue();}if(item.Player!=null){if(ReferenceEquals(item.Connection,item.Player.PlayerData.Client)&&(item.Connection?.TokenId??Guid.Empty)==item.Token)Accept(item.Player,item.Packet);}else if(SCIEPort.Compatibility.ServerConnection.IsServer(CommonLib.Net,item.Connection)&&item.Token==serverToken)Receive(item.Packet);}
            retired.Clear();foreach(var pair in walk){pair.Key.ComponentLocomotion.WalkSpeed-=pair.Value;retired.Add(pair.Key);}foreach(var p in retired)walk.Remove(p);
            foreach(var p in players.ComponentPlayers)
            {
                bool book=Held(p,CodexRules.Book),locked=!MayHold(p);
                if(book||((Held(p,949)||Held(p,950))&&!locked)){float delta=p.ComponentLocomotion.WalkSpeed*.2f;if(!float.IsNaN(delta)&&!float.IsInfinity(delta)){walk[p]=delta;p.ComponentLocomotion.WalkSpeed+=delta;}}
                var input=p.ComponentInput;if(input==null)continue;
                if(book) {input.m_playerInput.Interact=input.m_playerInput.Interact??input.m_playerInput.Hit??input.m_playerInput.Dig;input.m_playerInput.Hit=null;input.m_playerInput.Dig=null;input.m_playerInput.Aim=null;}
                else if(locked){if((input.m_playerInput.Hit.HasValue||input.m_playerInput.Aim.HasValue||input.m_playerInput.Interact.HasValue||input.m_playerInput.Dig.HasValue)&&p.PlayerData.IsMainPlayer&&clock.GameTime>=nextNotice){nextNotice=clock.GameTime+2;p.ComponentGui.DisplaySmallMessage("尚未解放真名，请先使用神器图谱。",Color.Yellow,false,false);}input.m_playerInput.Hit=null;input.m_playerInput.Dig=null;input.m_playerInput.Aim=null;input.m_playerInput.Interact=null;}
            }
            if(Authority&&clock.GameTime>=nextSync){nextSync=clock.GameTime+2;foreach(var p in players.ComponentPlayers)Publish(p);}
        }
        private double nextNotice;
        /// <inheritdoc/>
        public override void Dispose(){lock(pending){disposed=true;pending.Clear();}foreach(var pair in walk)pair.Key.ComponentLocomotion.WalkSpeed-=pair.Value;walk.Clear();Records.Clear();received.Clear();accepted.Clear();sent.Clear();lastRequest.Clear();connections.Clear();base.Dispose();}
    }
}

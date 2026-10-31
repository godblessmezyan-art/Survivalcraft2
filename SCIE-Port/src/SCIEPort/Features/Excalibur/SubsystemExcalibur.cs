using System;
using System.Collections.Generic;
using Engine;
using Game;
using Game.NetWork;
using GameEntitySystem;
using SCIEPort.Features.FourSaints;
using TemplatesDatabase;
namespace SCIEPort.Features.Excalibur
{
    /// <summary>Lake sword: bounded main-thread casts; only Local/Server may enter native damage.</summary>
    public sealed class SubsystemExcalibur:SubsystemBlockBehavior,IUpdateable,IDrawable
    {
        internal sealed class State
        {
            internal ComponentPlayer Player;
            internal uint Sent,Accepted,Revision,Received,NextCast,LastVisual;
            internal readonly double[] Ready=new double[3];
            internal double BlessingEnd,NextSnapshot,LastSnapshot=-100,LastRequest=-100;
            internal float WalkDelta;
            internal Cast Active;
        }
        internal sealed class Cast
        {
            internal ComponentPlayer Player;internal byte Skill;internal uint Id;
            internal Vector3 Origin,Direction;internal double Start;internal bool Settled,Released;
            internal readonly ExcaliburRules.HitLedger<Entity> Hits=new ExcaliburRules.HitLedger<Entity>();
        }
        private sealed class Pending{internal ComponentPlayer Player;internal ExcaliburPackage Packet;}
        private readonly Queue<Pending> pending=new Queue<Pending>();
        private readonly Dictionary<ushort,State> states=new Dictionary<ushort,State>();
        private readonly List<ushort> retired=new List<ushort>();
        private readonly DynamicArray<ComponentBody> candidates=new DynamicArray<ComponentBody>();
        private SubsystemTime clock;private SubsystemPlayers players;private SubsystemTerrain terrain;private SubsystemBodies bodies;
        private SubsystemGameInfo info;private ExcaliburHud hud;private ExcaliburScene scene;private bool disposed;
        internal double Now=>clock.GameTime;
        internal bool Authority=>CommonLib.WorkType==WorkType.Local||CommonLib.WorkType==WorkType.Server;
        internal IEnumerable<State> States=>states.Values;
        /// <inheritdoc/>
        public override int[] HandledBlocks=>new[]{ExcaliburBlock.Index};
        /// <inheritdoc/>
        public UpdateOrder UpdateOrder=>(UpdateOrder)(-4);
        /// <inheritdoc/>
        public int[] DrawOrders{get;}=new[]{107};
        /// <inheritdoc/>
        public override void Load(ValuesDictionary values)
        {
            base.Load(values);clock=Project.FindSubsystem<SubsystemTime>(true);players=Project.FindSubsystem<SubsystemPlayers>(true);
            terrain=Project.FindSubsystem<SubsystemTerrain>(true);bodies=Project.FindSubsystem<SubsystemBodies>(true);info=Project.FindSubsystem<SubsystemGameInfo>(true);
            hud=new ExcaliburHud(this);scene=new ExcaliburScene(this);
        }
        internal static bool Equipped(ComponentPlayer p)=>p!=null&&p.IsAddedToProject&&p.ComponentHealth.Health>0&&Terrain.ExtractContents(p.ComponentMiner.ActiveBlockValue)==ExcaliburBlock.Index;
        private bool Actor(ComponentPlayer p)=>Equipped(p)&&p.PlayerData.IsReadyForPlaying&&p.ComponentSleep.SleepFactor<=0&&p.ComponentRider.Mount==null&&p.ComponentBody.ParentBody==null
            &&p.ComponentGui?.ModalPanelWidget==null
            &&Project.FindSubsystem<SubsystemFourSaints>(false)?.IsActionLocked(p)!=true;
        internal State Get(ComponentPlayer p)
        {
            State s;
            if(states.TryGetValue(p.Entity.EntityId,out s)&&!ReferenceEquals(s.Player,p)){Restore(s);hud?.Remove(s.Player);states.Remove(p.Entity.EntityId);s=null;}
            if(s==null){s=new State{Player=p};states.Add(p.Entity.EntityId,s);}return s;
        }
        internal bool Fresh(State s)=>Authority||Now-s.LastSnapshot<1.6;
        internal bool Busy(ComponentPlayer p)=>p!=null&&states.TryGetValue(p.Entity.EntityId,out var s)&&s.Active!=null&&Fresh(s);
        internal float Frequency(ComponentPlayer p)
        {State s;return ExcaliburRules.AttackFrequency(p!=null&&states.TryGetValue(p.Entity.EntityId,out s)&&Fresh(s)&&Now<s.BlessingEnd);}
        internal void TickGui(ComponentGui gui)=>hud.Update(gui);
        internal void Request(ComponentPlayer p,byte skill,Vector3 direction)
        {
            if(!Actor(p)||!ExcaliburRules.Skill(skill)||clock.GameTimeDelta<=0)return;
            State s=Get(p);if(Now-s.LastRequest<.2||s.Active!=null)return;s.LastRequest=Now;
            var packet=new ExcaliburPackage{Caster=p.Entity.EntityId,Sequence=++s.Sent,Skill=skill,Direction=direction};
            if(Authority)Enqueue(p,packet);else if(CommonLib.WorkType==WorkType.Client)CommonLib.Net.QueuePackage(packet);
        }
        internal void Enqueue(ComponentPlayer p,ExcaliburPackage packet)
        {lock(pending){if(!disposed&&pending.Count<32)pending.Enqueue(new Pending{Player=p,Packet=packet});}}
        private int ActiveCount(){int n=0;foreach(var s in states.Values)if(s.Active!=null)n++;return n;}
        internal void Accept(ComponentPlayer p,ExcaliburPackage packet)
        {
            if(!Authority||!Actor(p)||!ExcaliburRules.Skill(packet.Skill)||!Finite(packet.Direction))return;
            State s=Get(p);if(!ExcaliburRules.Newer(packet.Sequence,s.Accepted))return;s.Accepted=packet.Sequence;
            if(s.Active!=null||Now<s.Ready[packet.Skill-1]||ActiveCount()>=ExcaliburRules.MaxCasts)return;
            float length=packet.Direction.Length();if(length<.95f||length>1.05f)return;
            var direction=Vector3.Normalize(packet.Direction);var origin=p.ComponentCreatureModel.EyePosition;
            if(!Loaded(origin)||Protected(origin))return;
            // Lock the release origin/direction at server acceptance: clients cannot teleport the beam.
            s.Ready[packet.Skill-1]=Now+ExcaliburRules.Cooldown(packet.Skill);
            if(packet.Skill==2)s.BlessingEnd=Now+8;
            s.Active=new Cast{Player=p,Id=++s.NextCast,Skill=packet.Skill,Origin=origin,Direction=direction,Start=Now};
            Present(s,s.Active);Publish(s);
        }
        /// <inheritdoc/>
        public void Update(float dt)
        {
            if(clock.GameTimeDelta<=0)return;
            for(int budget=0;budget<16;budget++)
            {Pending item;lock(pending){if(pending.Count==0)break;item=pending.Dequeue();}if(item.Player!=null)Accept(item.Player,item.Packet);else if(!Authority)Receive(item.Packet);}
            foreach(var p in players.ComponentPlayers)
            {
                if(!Equipped(p)&&!states.ContainsKey(p.Entity.EntityId))continue;
                var s=Get(p);ApplySpeed(s,Equipped(p));
                var c=s.Active;
                bool cancel=!Actor(p)||!Fresh(s)||(c!=null&&Vector3.DistanceSquared(p.ComponentCreatureModel.EyePosition,c.Origin)>9);
                if(cancel){if(c!=null)ClearCast(s);s.BlessingEnd=0;}
                else if(c!=null)
                {
                    float age=(float)(Now-c.Start);
                    try
                    {
                        // Input has already been assembled (-10); consume held attack/dig before
                        // ComponentPlayer (0), so the cast cannot also mine or start a native swing.
                        var input=p.ComponentInput;
                        if(input!=null){input.m_playerInput.Hit=null;input.m_playerInput.Dig=null;input.m_playerInput.Aim=null;input.m_playerInput.Interact=null;}
                        Pose(c,age);
                        if(age>=ExcaliburRules.Impact(c.Skill)&&!c.Released){c.Released=true;Release(c);}
                        if(age>=ExcaliburRules.Impact(c.Skill)&&!c.Settled){c.Settled=true;if(c.Skill!=2)Strike(c);}
                    }
                    catch(Exception error){Log.Warning("[SCIEPort:Excalibur] Cast stopped: "+error);ClearCast(s);}
                    if(age>=ExcaliburRules.Length(c.Skill)||age<0)ClearCast(s);
                }
                if(Authority&&Now>=s.NextSnapshot){Publish(s);s.NextSnapshot=Now+.4;}
            }
            retired.Clear();foreach(var pair in states)if(!pair.Value.Player.IsAddedToProject||!players.ComponentPlayers.Contains(pair.Value.Player)){Restore(pair.Value);retired.Add(pair.Key);}
            foreach(var id in retired){hud.Remove(states[id].Player);states.Remove(id);}
        }
        private static void Pose(Cast c,float age)
        {
            if(c.Skill!=3)return;
            float pitch,yaw,lift;ExcaliburRules.Pose(age,out pitch,out yaw,out lift);
            var rotation=new Vector3(pitch,yaw,-.12f);var offset=new Vector3(-.12f,lift,0);
            var fp=c.Player.Entity.FindComponent<ComponentFirstPersonModel>(false);
            if(fp!=null){fp.ItemRotationOrder=rotation;fp.ItemOffsetOrder=offset;}
            float raise=MathUtils.Saturate(age/.65f),swing=MathUtils.Saturate((age-2.65f)/.35f);
            c.Player.ComponentCreatureModel.AimHandAngleOrder=2.55f*raise*(1-swing)+.5f*swing;
            c.Player.ComponentCreatureModel.InHandItemRotationOrder=rotation;c.Player.ComponentCreatureModel.InHandItemOffsetOrder=offset*.4f;
        }
        private static void ClearCast(State s)
        {
            // Native models consume and reset these orders every update. If another item is
            // already equipped, leave its newly submitted pose untouched during this handoff.
            if(s.Active!=null&&Equipped(s.Player))
            {
                var fp=s.Player.Entity.FindComponent<ComponentFirstPersonModel>(false);if(fp!=null){fp.ItemRotationOrder=Vector3.Zero;fp.ItemOffsetOrder=Vector3.Zero;}
                s.Player.ComponentCreatureModel.InHandItemRotationOrder=Vector3.Zero;s.Player.ComponentCreatureModel.InHandItemOffsetOrder=Vector3.Zero;
                s.Player.ComponentCreatureModel.AimHandAngleOrder=0;
            }
            s.Active=null;s.NextSnapshot=0;
        }
        private void ApplySpeed(State s,bool active)
        {
            var move=s.Player.ComponentLocomotion;move.WalkSpeed-=s.WalkDelta;s.WalkDelta=0;
            if(active&&ExcaliburRules.Finite(move.WalkSpeed)){s.WalkDelta=move.WalkSpeed*.2f;move.WalkSpeed+=s.WalkDelta;}
        }
        private void Restore(State s){ApplySpeed(s,false);ClearCast(s);}
        private void Present(State s,Cast c)
        {
            if(s.LastVisual==c.Id)return;s.LastVisual=c.Id;
            Project.FindSubsystem<SubsystemAudio>(false)?.PlaySound("Audio/Excalibur/Charge",.85f,c.Skill==3?-.12f:.12f,c.Origin,24,false);
        }
        private void Release(Cast c)
        {
            if(c.Skill==2)return;
            c.Player.ComponentMiner.Poke(true);
            Project.FindSubsystem<SubsystemAudio>(false)?.PlaySound("Audio/Excalibur/Release",c.Skill==3?1f:.55f,c.Skill==3?-.08f:.2f,c.Origin,40,false);
        }
        private static bool Related(ComponentBody a,ComponentBody b)=>a==b||a.ParentBody==b||b.ParentBody==a||a.ChildBodies.Contains(b)||b.ChildBodies.Contains(a);
        private bool Protected(Vector3 p)=>!SCIEPort.Features.AbyssReaper.VoidPortalSafety.Unprotected(Terrain.ToCell(p.X),Terrain.ToCell(p.Z));
        internal bool Loaded(Vector3 p)
        {if(!Finite(p)||Math.Abs(p.X)>1000000||Math.Abs(p.Z)>1000000||p.Y<1||p.Y>254)return false;var c=terrain.Terrain.GetChunkAtCell(Terrain.ToCell(p.X),Terrain.ToCell(p.Z));return c!=null&&c.State>=TerrainChunkState.Valid;}
        private bool Sight(Vector3 a,Vector3 b)
        {
            int steps=(int)Math.Ceiling(Vector3.Distance(a,b));
            if(steps>40)return false;
            for(int i=0;i<=steps;i++){var p=Vector3.Lerp(a,b,i/(float)Math.Max(1,steps));if(!Loaded(p)||Protected(p))return false;}
            return !terrain.Raycast(a,b,false,true,(v,d)=>BlocksManager.Blocks[Terrain.ExtractContents(v)].IsCollidable).HasValue;
        }
        private void Strike(Cast c)
        {
            if(!Authority||!Actor(c.Player)||!Loaded(c.Origin)||Protected(c.Origin))return;
            float range=c.Skill==3?35:16;Vector3 side=Vector3.Cross(c.Direction,Vector3.UnitY);if(side.LengthSquared()<.001f)side=Vector3.UnitX;else side=Vector3.Normalize(side);
            Vector3 up=Vector3.Normalize(Vector3.Cross(side,c.Direction));
            Vector3 end=c.Origin+c.Direction*range,min=Vector3.Min(c.Origin,end)-new Vector3(6),max=Vector3.Max(c.Origin,end)+new Vector3(6);
            candidates.Clear();bodies.FindBodiesInArea(min.XZ,max.XZ,candidates);
            for(int i=0;i<Math.Min(candidates.Count,ExcaliburRules.MaxCandidates);i++)
            {
                var b=candidates.Array[i];if(b==null||!b.Entity.IsAddedToProject||Related(c.Player.ComponentBody,b)||b.Project!=Project)continue;
                if(b.Player!=null&&!info.WorldSettings.IsFriendlyFireEnabled)continue;
                var health=b.Entity.FindComponent<ComponentHealth>(false);if(health==null||health.Health<=0)continue;
                var center=b.BoundingBox.Center();if(!Loaded(center)||Protected(center))continue;
                var delta=center-c.Origin;float power=ExcaliburRules.Power(c.Skill,Vector3.Dot(delta,c.Direction),Vector3.Dot(delta,side),Vector3.Dot(delta,up));
                if(power<=0||!Sight(c.Origin,center)||!c.Hits.Add(b.Entity))continue;
                ComponentMiner.AttackBody(b,c.Player,center,c.Direction,power,false);
            }
        }
        private void Publish(State s)
        {
            if(CommonLib.WorkType!=WorkType.Server)return;var c=s.Active;
            CommonLib.Net.QueuePackage(new ExcaliburPackage{Kind=1,Caster=s.Player.Entity.EntityId,Sequence=++s.Revision,CastId=c?.Id??0,Skill=c?.Skill??0,
                Origin=c?.Origin??Vector3.Zero,Direction=c?.Direction??Vector3.Zero,Age=c==null?0:(float)Math.Max(0,Now-c.Start),
                Cooldowns=new Vector3((float)Math.Max(0,s.Ready[0]-Now),(float)Math.Max(0,s.Ready[1]-Now),(float)Math.Max(0,s.Ready[2]-Now)),Blessing=(float)Math.Max(0,s.BlessingEnd-Now)});
        }
        internal static bool Finite(Vector3 p)=>ExcaliburRules.Finite(p.X)&&ExcaliburRules.Finite(p.Y)&&ExcaliburRules.Finite(p.Z);
        internal static bool Valid(ExcaliburPackage p)=>p.Kind==1&&Finite(p.Origin)&&Finite(p.Direction)&&Finite(p.Cooldowns)&&ExcaliburRules.Finite(p.Age)&&ExcaliburRules.Finite(p.Blessing)
            &&p.Blessing>=0&&p.Blessing<=8&&p.Cooldowns.X>=0&&p.Cooldowns.X<=10&&p.Cooldowns.Y>=0&&p.Cooldowns.Y<=35&&p.Cooldowns.Z>=0&&p.Cooldowns.Z<=65
            &&(p.Skill==0?p.CastId==0:ExcaliburRules.Skill(p.Skill)&&p.CastId!=0&&p.Age>=0&&p.Age<=ExcaliburRules.Length(p.Skill)&&p.Direction.LengthSquared()>.98f&&p.Direction.LengthSquared()<1.02f);
        internal void Receive(ExcaliburPackage packet)
        {
            if(Authority||!Valid(packet))return;
            foreach(var p in players.ComponentPlayers)if(p.Entity.EntityId==packet.Caster)
            {
                var s=Get(p);if(!ExcaliburRules.Newer(packet.Sequence,s.Received))return;s.Received=packet.Sequence;s.LastSnapshot=Now;
                s.Ready[0]=Now+packet.Cooldowns.X;s.Ready[1]=Now+packet.Cooldowns.Y;s.Ready[2]=Now+packet.Cooldowns.Z;s.BlessingEnd=Now+packet.Blessing;
                if(packet.Skill==0){ClearCast(s);return;}
                if(s.Active==null||s.Active.Id!=packet.CastId)
                {
                    if(ActiveCount()>=ExcaliburRules.MaxCasts)return;
                    ClearCast(s);s.Active=new Cast{Player=p,Skill=packet.Skill,Id=packet.CastId,Origin=packet.Origin,Direction=packet.Direction,Settled=true,Released=packet.Age>=ExcaliburRules.Impact(packet.Skill)};Present(s,s.Active);
                }
                s.Active.Start=Now-packet.Age;return;
            }
        }
        /// <inheritdoc/>
        public void Draw(Camera camera,int order)=>scene.Draw(camera);
        /// <inheritdoc/>
        public override void Dispose()
        {lock(pending){disposed=true;pending.Clear();}foreach(var s in states.Values)Restore(s);hud?.Dispose();scene?.Dispose();states.Clear();candidates.Clear();base.Dispose();}
    }
}

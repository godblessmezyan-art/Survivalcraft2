using System;
using System.Collections.Generic;
using Engine;
using Game;
using Game.NetWork;
using GameEntitySystem;
using SCIEPort.Features.FourSaints;
using TemplatesDatabase;
namespace SCIEPort.Features.Poseidon
{
    /// <summary>Authoritative tide shield, directional wave and twenty-second ocean domain.</summary>
    public sealed class SubsystemPoseidon:SubsystemBlockBehavior,IUpdateable,IDrawable
    {
        internal sealed class State
        {
            internal ComponentPlayer Player;
            internal uint Sent,Accepted,Revision,Received;
            internal double ShieldReady,WaveReady,DomainReady,DomainEnd,NextSnapshot,LastRequest=-100,LastSnapshot=-100;
            internal Vector3 DomainOrigin,DomainDirection;
            internal bool DomainWater;
            internal float Shield,WalkDelta,SwimDelta;
            internal double Ready(byte skill)=>skill==1?ShieldReady:skill==2?WaveReady:DomainReady;
        }
        internal sealed class Cast
        {
            internal ComponentPlayer Player;
            internal byte Skill;
            internal Vector3 Origin,Direction;
            internal double Start,NextQuery;
            internal float Distance;
            internal bool Water;
            internal readonly HashSet<Entity> Hit=new HashSet<Entity>();
            // Waterspout grind keeps a per-target cooldown, so the ring ticks instead of hitting once.
            internal readonly Dictionary<int,double> Ticks=new Dictionary<int,double>();
        }
        private readonly Dictionary<ushort,State> m_states=new Dictionary<ushort,State>();
        internal readonly List<Cast> Casts=new List<Cast>(PoseidonRules.MaxCasts);
        internal IEnumerable<State> States=>m_states.Values;
        private readonly DynamicArray<ComponentBody> m_candidates=new DynamicArray<ComponentBody>();
        private readonly List<int> m_stale=new List<int>();
        private readonly List<ushort> m_remove=new List<ushort>();
        private sealed class Pending {internal ComponentPlayer Player;internal PoseidonV2Package Packet;}
        private readonly Queue<Pending> m_pending=new Queue<Pending>();
        private bool m_disposed;
        private SubsystemTime m_time;private SubsystemPlayers m_players;private SubsystemTerrain m_terrain;
        private SubsystemBodies m_bodies;private SubsystemGameInfo m_info;private PoseidonScene m_scene;private PoseidonHud m_hud;
        internal double Now=>m_time.GameTime;
        private bool Authority=>CommonLib.WorkType!=WorkType.Client;
        /// <inheritdoc/>
        public override int[] HandledBlocks=>new[]{PoseidonBlock.Index};
        /// <inheritdoc/>
        public UpdateOrder UpdateOrder=>(UpdateOrder)(-4);
        /// <inheritdoc/>
        public int[] DrawOrders{get;}=new[]{106,1100};
        /// <inheritdoc/>
        public override void Load(ValuesDictionary values)
        {
            base.Load(values);m_time=Project.FindSubsystem<SubsystemTime>(true);m_players=Project.FindSubsystem<SubsystemPlayers>(true);
            m_terrain=Project.FindSubsystem<SubsystemTerrain>(true);m_bodies=Project.FindSubsystem<SubsystemBodies>(true);m_info=Project.FindSubsystem<SubsystemGameInfo>(true);
            m_scene=new PoseidonScene(this);m_hud=new PoseidonHud(this);
        }
        internal static bool Equipped(ComponentPlayer p)=>p!=null&&p.IsAddedToProject&&p.ComponentHealth.Health>0&&Terrain.ExtractContents(p.ComponentMiner.ActiveBlockValue)==PoseidonBlock.Index;
        private static bool Actor(ComponentPlayer p)=>Equipped(p)&&p.PlayerData.IsReadyForPlaying&&p.ComponentSleep.SleepFactor<=0&&p.ComponentBody.ParentBody==null&&p.ComponentRider.Mount==null&&!p.ComponentLocomotion.IsCreativeFlyEnabled;
        internal State Get(ComponentPlayer p)
        {
            State s;
            if(m_states.TryGetValue(p.Entity.EntityId,out s)&&!ReferenceEquals(s.Player,p)){ApplyPassive(s,false);m_hud?.Remove(s.Player);m_states.Remove(p.Entity.EntityId);s=null;}
            if(s==null){s=new State{Player=p};m_states.Add(p.Entity.EntityId,s);}return s;
        }
        internal void TickGui(ComponentGui gui)=>m_hud.Update(gui);
        internal static bool InWater(ComponentPlayer p)=>p.ComponentBody.ImmersionFactor>.25f&&p.ComponentBody.ImmersionFluidBlock is WaterBlock;
        // Native players never receive a SwimOrder, so only deeply wading/swimming bodies take the speed passive.
        internal static bool Swimming(ComponentPlayer p)=>p.ComponentBody.ImmersionFactor>PoseidonRules.SwimImmersion&&p.ComponentBody.ImmersionFluidBlock is WaterBlock;
        internal static bool BlocksDrowning(ComponentHealth health,ComponentCreature attacker,string cause)
        {
            var p=health?.Entity.FindComponent<ComponentPlayer>(false);if(!Equipped(p)||attacker!=null||cause!=LanguageControl.Get(ComponentHealth.fName,7))return false;
            var t=p.Project.FindSubsystem<SubsystemTerrain>(true);var eye=p.ComponentCreatureModel.EyePosition;
            return BlocksManager.Blocks[t.Terrain.GetCellContents(Terrain.ToCell(eye.X),Terrain.ToCell(eye.Y),Terrain.ToCell(eye.Z))] is WaterBlock;
        }
        // Called AFTER Four Saints immunities/reduction and BEFORE its lethal-rebirth check.
        // Replay stays inside Four Saints' existing guard, so a partial hit consumes the shield once.
        internal float AbsorbInjury(ComponentHealth health,float amount,bool ignoreInvulnerability)
        {
            if(!Authority||health==null||ignoreInvulnerability||health.IsInvulnerable||!PoseidonRules.Finite(amount)||amount<=0)return amount;
            var p=health.Entity.FindComponent<ComponentPlayer>(false);State s;
            if(!Equipped(p)||!m_states.TryGetValue(p.Entity.EntityId,out s)||!ReferenceEquals(s.Player,p))return amount;
            float left=PoseidonRules.Absorb(ref s.Shield,amount,InWater(p));if(left<amount)s.NextSnapshot=0;return left;
        }
        internal float ShieldPoints(State s)=>PoseidonRules.ShieldPoints(s.Shield,InWater(s.Player));
        internal bool Fresh(State s)=>Authority||Now-s.LastSnapshot<2;
        internal bool InDomain(State s)=>Actor(s.Player)&&Fresh(s)&&Now<s.DomainEnd;
        internal float DomainBlend(Vector3 position,GameWidget viewer=null)
        {
            float blend=0;
            foreach(var s in m_states.Values)if(InDomain(s)&&(s.Player.GameWidget==viewer||Vector3.DistanceSquared(position,s.DomainOrigin)<=24*24))
                blend=Math.Max(blend,PoseidonRules.DomainBlend((float)(s.DomainEnd-Now)));
            return blend;
        }
        internal float LocalDomainBlend
        {
            get {float blend=0;foreach(var p in m_players.ComponentPlayers)if(p.PlayerData.IsMainPlayer&&p.GameWidget!=null)blend=Math.Max(blend,DomainBlend(p.ComponentBody.Position,p.GameWidget));return blend;}
        }
        internal void Request(ComponentPlayer p,byte skill,Vector3 direction)
        {
            if(!Actor(p)||!PoseidonRules.Skill(skill)||m_time.GameTimeDelta<=0)return;
            State s=Get(p);if(Now-s.LastRequest<.2)return;s.LastRequest=Now;
            var packet=new PoseidonV2Package{Caster=p.Entity.EntityId,Skill=skill,Direction=direction,Sequence=++s.Sent};
            if(Authority)Enqueue(p,packet);else CommonLib.Net.QueuePackage(packet);
        }
        internal void Enqueue(ComponentPlayer player,PoseidonV2Package packet)
        {lock(m_pending){if(!m_disposed&&m_pending.Count<32)m_pending.Enqueue(new Pending{Player=player,Packet=packet});}}
        internal void Accept(ComponentPlayer p,PoseidonV2Package packet)
        {
            if(!Authority||!Actor(p)||!PoseidonRules.Skill(packet.Skill)||!Finite(packet.Direction))return;
            State s=Get(p);if(!PoseidonRules.Newer(packet.Sequence,s.Accepted))return;s.Accepted=packet.Sequence;
            if(Project.FindSubsystem<SubsystemFourSaints>(false)?.IsActionLocked(p)==true||s.Ready(packet.Skill)>Now||Casts.Count>=PoseidonRules.MaxCasts)return;
            foreach(var c in Casts)if(c.Player==p&&c.Skill!=3)return;
            Vector3 direction=packet.Direction;float length=direction.Length();if(length<.95f||length>1.05f)return;
            direction.Y=0;
            if(direction.LengthSquared()<.01f){if(packet.Skill!=1)return;direction=Vector3.UnitZ;}
            direction=Vector3.Normalize(direction);Vector3 origin=p.ComponentBody.Position;
            if(!Loaded(origin))return;
            bool water=InWater(p),domain=InDomain(s);
            if(packet.Skill==1){s.ShieldReady=Now+PoseidonRules.Cooldown(1,domain);s.Shield=5;}
            else if(packet.Skill==2)s.WaveReady=Now+PoseidonRules.Cooldown(2,domain);
            else
            {
                s.DomainReady=Now+60;s.DomainEnd=Now+20;s.DomainOrigin=origin;s.DomainDirection=direction;s.DomainWater=water;s.Shield=5;
                s.ShieldReady=Math.Min(s.ShieldReady,Now+7);s.WaveReady=Math.Min(s.WaveReady,Now+5);
            }
            var cast=new Cast{Player=p,Skill=packet.Skill,Origin=origin,Direction=direction,Start=Now,Water=water};
            Casts.Add(cast);Present(cast);Publish(s,1,cast);
        }
        private void Present(Cast c)
        {
            c.Player.ComponentMiner.Poke(true);
            Project.FindSubsystem<SubsystemAudio>(true).PlaySound(c.Skill==1?"Audio/Poseidon/Surge":"Audio/Poseidon/Tsunami",c.Skill==3?.95f:.8f,c.Skill==3?-.22f:0,c.Origin,20,false);
            if(c.Player.PlayerData.IsMainPlayer)c.Player.ComponentBody.ApplyShaking(c.Skill==3?.3f:.10f);
        }
        /// <inheritdoc/>
        public void Update(float dt)
        {
            if(m_time.GameTimeDelta<=0)return;
            for(int budget=0;budget<16;budget++)
            {
                Pending p;lock(m_pending){if(m_pending.Count==0)break;p=m_pending.Dequeue();}
                if(p.Player!=null)Accept(p.Player,p.Packet);else if(!Authority)Receive(p.Packet);
            }
            foreach(var p in m_players.ComponentPlayers)
            {
                if(!Equipped(p)&&!m_states.ContainsKey(p.Entity.EntityId))continue;
                State s=Get(p);bool active=Equipped(p);
                if(!Actor(p)||!Fresh(s)){s.Shield=0;s.DomainEnd=0;}
                ApplyPassive(s,active);
                if(Authority&&Now>=s.NextSnapshot){Publish(s,2,null);s.NextSnapshot=Now+.5;}
            }
            m_remove.Clear();foreach(var pair in m_states)if(!pair.Value.Player.IsAddedToProject||!m_players.ComponentPlayers.Contains(pair.Value.Player)){ApplyPassive(pair.Value,false);m_remove.Add(pair.Key);}
            foreach(var id in m_remove){m_hud.Remove(m_states[id].Player);m_states.Remove(id);}
            for(int i=Casts.Count-1;i>=0;i--)
            {
                var c=Casts[i];float age=(float)(Now-c.Start);
                if(!Actor(c.Player)||age<0||(!Authority&&!Fresh(Get(c.Player)))){Casts.RemoveAt(i);continue;}
                bool alive=PoseidonRules.Alive(c.Skill,age);
                if(Now>=c.NextQuery||!alive){Strike(c,Math.Min(age,PoseidonRules.Duration(c.Skill)));c.NextQuery=Now+.08;}
                if(!alive)Casts.RemoveAt(i);
            }
        }
        private void ApplyPassive(State s,bool active)
        {
            var p=s.Player;var locomotion=p.ComponentLocomotion;
            locomotion.WalkSpeed-=s.WalkDelta;locomotion.SwimSpeed-=s.SwimDelta;s.WalkDelta=0;s.SwimDelta=0;
            if(!active)return;
            if(InWater(p))p.ComponentHealth.Air=1;
            // Native player movement reads WalkSpeed (SwimSpeed is only consumed by AI SwimOrder):
            // +20% on the ground, the much larger water bonus only while actually swimming.
            bool swimming=Swimming(p);
            s.WalkDelta=PoseidonRules.SpeedDelta(locomotion.WalkSpeed,swimming);locomotion.WalkSpeed+=s.WalkDelta;
            if(!swimming)return;
            s.SwimDelta=PoseidonRules.SpeedDelta(locomotion.SwimSpeed,true);locomotion.SwimSpeed+=s.SwimDelta;
        }
        private void Strike(Cast c,float age)
        {
            bool domain=c.Skill==3,sweep=c.Skill==2;
            if(sweep&&age<PoseidonRules.WaveWindup)return;
            float next=sweep?PoseidonRules.WaveDistance(age):PoseidonRules.ShieldRadius*MathUtils.Saturate(age/PoseidonRules.ShieldDuration);
            Vector3 sight=c.Origin+Vector3.UnitY*.8f;
            if(!Loaded(sight)){c.Start=-100;return;}
            if(sweep&&c.Distance>=0)
            {
                var a=sight+c.Direction*c.Distance;var b=sight+c.Direction*next;
                if(!Loaded(b)||Wall(a,b)){c.Distance=-1;c.Start=-100;return;}
            }
            var side=Vector3.Cross(c.Direction,Vector3.UnitY);
            if (Authority) Query(c,c.Origin,domain?PoseidonRules.DomainRingOuter+1:sweep?19:7,body=>
            {
                Vector3 delta=body.BoundingBox.Center()-c.Origin;float flat=new Vector2(delta.X,delta.Z).Length();
                // Only the blue ring of waterspouts grinds; every bearing at the same distance is equivalent.
                if(domain)return PoseidonRules.InVortex(flat,delta.Y);
                if(sweep)
                {
                    float along=Vector3.Dot(delta,c.Direction),across=Vector3.Dot(delta,side);
                    return PoseidonRules.InSweep(along,across,delta.Y,c.Distance,next,.5f);
                }
                return flat<=next+.5f&&delta.Y>=-.5f&&delta.Y<=3;
            },sight);
            if(!domain&&c.Distance>=0)c.Distance=next;
        }
        private void Query(Cast c,Vector3 center,float radius,Func<ComponentBody,bool> overlaps,Vector3 sightOrigin)
        {
            bool repeat=c.Skill==3;
            m_candidates.Clear();m_bodies.FindBodiesAroundPoint(new Vector2(center.X,center.Z),radius,m_candidates);
            for(int i=0;i<Math.Min(m_candidates.Count,PoseidonRules.MaxCandidates);i++)
            {
                var b=m_candidates.Array[i];
                if(b==c.Player.ComponentBody||!b.Entity.IsAddedToProject||Related(c.Player.ComponentBody,b))continue;
                if(b.Player!=null&&!m_info.WorldSettings.IsFriendlyFireEnabled)continue;
                if(repeat?Cooling(c,b):c.Hit.Contains(b.Entity))continue;
                var health=b.Entity.FindComponent<ComponentHealth>(false);
                if(health==null||health.Health<=0||!overlaps(b)||!Loaded(b.Position)||Wall(sightOrigin,b.BoundingBox.Center()))continue;
                if(repeat){if(c.Ticks.Count>=PoseidonRules.MaxTickTargets&&!Prune(c))continue;c.Ticks[b.Entity.EntityId]=Now+PoseidonRules.DomainTickInterval;}
                else{if(c.Hit.Count>=PoseidonRules.MaxHits)break;c.Hit.Add(b.Entity);}
                Vector3 direction=c.Direction;
                if(c.Skill==1||repeat)
                {
                    direction=b.BoundingBox.Center()-c.Origin;direction.Y=0;
                    direction=direction.LengthSquared()<.01f?Vector3.UnitZ:Vector3.Normalize(direction);
                    // The whirlpool drags its victims back toward the eye instead of throwing them out of the ring.
                    if(repeat)direction=-direction;
                }
                ComponentMiner.AttackBody(b,c.Player,b.BoundingBox.Center(),direction,repeat?PoseidonRules.TickDamage(c.Water):PoseidonRules.Damage(c.Skill,c.Water),false);
                b.ApplyImpulse(direction*(repeat?4:c.Skill==1?7:c.Skill==2?8:10)+Vector3.UnitY*(repeat?1.2f:c.Skill==3?3:1.5f));
            }
        }
        private bool Cooling(Cast c,ComponentBody b)
        {double next;return c.Ticks.TryGetValue(b.Entity.EntityId,out next)&&Now<next;}
        private bool Prune(Cast c)
        {
            m_stale.Clear();foreach(var pair in c.Ticks)if(Now>=pair.Value)m_stale.Add(pair.Key);
            foreach(var id in m_stale)c.Ticks.Remove(id);return c.Ticks.Count<PoseidonRules.MaxTickTargets;
        }
        private static bool Related(ComponentBody a,ComponentBody b)=>a.ParentBody==b||b.ParentBody==a||a.ChildBodies.Contains(b)||b.ChildBodies.Contains(a);
        private bool Wall(Vector3 a,Vector3 b)=>m_terrain.Raycast(a,b,false,true,(v,d)=>BlocksManager.Blocks[Terrain.ExtractContents(v)].IsCollidable).HasValue;
        internal bool ClearWave(Vector3 origin,Vector3 direction,float age)
        {Vector3 a=origin+Vector3.UnitY*.8f,b=a+direction*PoseidonRules.WaveDistance(age);return Loaded(b)&&!Wall(a,b);}
        private bool Loaded(Vector3 p)
        {if(!Finite(p)||p.Y<1||p.Y>254)return false;var c=m_terrain.Terrain.GetChunkAtCell(Terrain.ToCell(p.X),Terrain.ToCell(p.Z));return c!=null&&c.State>=TerrainChunkState.Valid;}
        private static bool Finite(Vector3 v)=>PoseidonRules.Finite(v.X)&&PoseidonRules.Finite(v.Y)&&PoseidonRules.Finite(v.Z);
        private void Publish(State s,byte kind,Cast c)
        {
            if(CommonLib.WorkType!=WorkType.Server)return;
            CommonLib.Net.QueuePackage(new PoseidonV2Package{Kind=kind,Caster=s.Player.Entity.EntityId,Sequence=++s.Revision,
                Cooldowns=new Vector3((float)Math.Max(0,s.ShieldReady-Now),(float)Math.Max(0,s.WaveReady-Now),(float)Math.Max(0,s.DomainReady-Now)),
                Shield=s.Shield,DomainRemaining=(float)Math.Max(0,s.DomainEnd-Now),DomainOrigin=s.DomainOrigin,DomainDirection=s.DomainDirection,DomainWater=s.DomainWater,
                Skill=c?.Skill??0,Origin=c?.Origin??Vector3.Zero,Direction=c?.Direction??Vector3.Zero,Water=c?.Water??false});
        }
        internal void Receive(PoseidonV2Package packet)
        {
            if(!Finite(packet.Cooldowns)||!Finite(packet.Origin)||!Finite(packet.Direction)||!Finite(packet.DomainOrigin)||!Finite(packet.DomainDirection)
                ||!PoseidonRules.Finite(packet.Shield)||!PoseidonRules.Finite(packet.DomainRemaining)||packet.Shield<0||packet.Shield>5||packet.DomainRemaining<0||packet.DomainRemaining>20
                ||packet.Cooldowns.X<0||packet.Cooldowns.X>15||packet.Cooldowns.Y<0||packet.Cooldowns.Y>11||packet.Cooldowns.Z<0||packet.Cooldowns.Z>60)return;
            foreach(var p in m_players.ComponentPlayers)
            {
                if(p.Entity.EntityId!=packet.Caster)continue;
                State s=Get(p);if(!PoseidonRules.Newer(packet.Sequence,s.Received))return;s.Received=packet.Sequence;s.LastSnapshot=Now;
                s.ShieldReady=Now+packet.Cooldowns.X;s.WaveReady=Now+packet.Cooldowns.Y;s.DomainReady=Now+packet.Cooldowns.Z;
                s.Shield=packet.Shield;s.DomainEnd=packet.DomainRemaining>0?Now+packet.DomainRemaining:0;s.DomainOrigin=packet.DomainOrigin;s.DomainDirection=packet.DomainDirection;s.DomainWater=packet.DomainWater;
                if(packet.Kind==1&&PoseidonRules.Skill(packet.Skill)&&Casts.Count<PoseidonRules.MaxCasts&&Actor(p))
                {var c=new Cast{Player=p,Skill=packet.Skill,Start=Now,Origin=packet.Origin,Direction=packet.Direction,Water=packet.Water};Casts.Add(c);Present(c);}
                return;
            }
        }
        /// <inheritdoc/>
        public void Draw(Camera camera,int order)
        {
            if(order==106)m_scene.Draw(camera);
            else
            {
                float blend=DomainBlend(camera.ViewPosition,camera.GameWidget);if(blend<=0)return;
                camera.GameWidget.PlayerData.ComponentPlayer?.Entity.FindComponent<ComponentScreenOverlays>(false)?.DrawOverlay(camera,new Color(10,79,175),.20f*blend,.37f*blend);
            }
        }
        /// <inheritdoc/>
        public override void Dispose()
        {
            lock(m_pending){m_disposed=true;m_pending.Clear();}
            foreach(var s in m_states.Values)ApplyPassive(s,false);
            m_hud?.Dispose();m_scene?.Dispose();Casts.Clear();m_states.Clear();base.Dispose();
        }
    }
}

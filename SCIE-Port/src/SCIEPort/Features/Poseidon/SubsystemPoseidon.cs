using System;
using System.Collections.Generic;
using Engine;
using Game;
using Game.NetWork;
using SCIEPort.Features.FourSaints;
using TemplatesDatabase;

namespace SCIEPort.Features.Poseidon
{
    /// <summary>World-scoped ocean artifact. No terrain edits, summons or persistent status effects.</summary>
    public sealed class SubsystemPoseidon : SubsystemBlockBehavior, IUpdateable, IDrawable
    {
        internal sealed class State
        {
            internal ComponentPlayer Player;
            internal uint Sent, Accepted, Revision, Received;
            internal double DashReady, WaveReady, NextSnapshot, LastRequest = -100;
            internal float SwimDelta;
        }
        internal sealed class Cast
        {
            internal ComponentPlayer Player;
            internal byte Skill;
            internal Vector3 Origin, Direction, Previous;
            internal double Start;
            internal float Distance;
            internal bool Water;
            internal readonly HashSet<ushort> Hit = new HashSet<ushort>();
        }
        private readonly Dictionary<ushort, State> m_states = new Dictionary<ushort, State>();
        internal readonly List<Cast> Casts = new List<Cast>(PoseidonRules.MaxCasts);
        private readonly DynamicArray<ComponentBody> m_candidates = new DynamicArray<ComponentBody>();
        private readonly List<ushort> m_remove = new List<ushort>();
        private sealed class Pending { internal ComponentPlayer Player; internal PoseidonPackage Packet; }
        private readonly Queue<Pending> m_pending = new Queue<Pending>();
        private bool m_disposed;
        private SubsystemTime m_time;
        private SubsystemPlayers m_players;
        private SubsystemTerrain m_terrain;
        private SubsystemBodies m_bodies;
        private SubsystemGameInfo m_info;
        private PoseidonScene m_scene;
        private PoseidonHud m_hud;
        internal double Now => m_time.GameTime;
        private bool Authority => CommonLib.WorkType != WorkType.Client;
        /// <inheritdoc />
        public override int[] HandledBlocks => new[] { PoseidonBlock.Index };
        /// <inheritdoc />
        public UpdateOrder UpdateOrder => (UpdateOrder)(-4);
        /// <inheritdoc />
        public int[] DrawOrders => new[] { 106 };
        /// <inheritdoc />
        public override void Load(ValuesDictionary values)
        {
            base.Load(values);
            m_time = Project.FindSubsystem<SubsystemTime>(true);
            m_players = Project.FindSubsystem<SubsystemPlayers>(true);
            m_terrain = Project.FindSubsystem<SubsystemTerrain>(true);
            m_bodies = Project.FindSubsystem<SubsystemBodies>(true);
            m_info = Project.FindSubsystem<SubsystemGameInfo>(true);
            m_scene = new PoseidonScene(this);
            m_hud = new PoseidonHud(this);
        }
        internal static bool Equipped(ComponentPlayer p) => p != null && p.IsAddedToProject && p.ComponentHealth.Health > 0 && Terrain.ExtractContents(p.ComponentMiner.ActiveBlockValue) == PoseidonBlock.Index;
        internal State Get(ComponentPlayer p)
        {
            State state;
            if (!m_states.TryGetValue(p.Entity.EntityId, out state))
            {
                state = new State { Player = p };
                m_states.Add(p.Entity.EntityId, state);
            }
            else if (!ReferenceEquals(state.Player,p))
            {
                ApplyPassive(state,false); m_hud.Remove(state.Player);
                state = new State { Player = p }; m_states[p.Entity.EntityId] = state;
            }
            return state;
        }
        internal void TickGui(ComponentGui gui) => m_hud.Update(gui);
        internal static bool InWater(ComponentPlayer p) => p.ComponentBody.ImmersionFactor > 0.25f && p.ComponentBody.ImmersionFluidBlock is WaterBlock;
        internal static bool BlocksDrowning(ComponentHealth health, ComponentCreature attacker, string cause)
        {
            ComponentPlayer p = health?.Entity.FindComponent<ComponentPlayer>(false);
            if (!Equipped(p) || attacker != null || cause != LanguageControl.Get(ComponentHealth.fName, 7)) return false;
            var terrain = p.Project.FindSubsystem<SubsystemTerrain>(true);
            Vector3 eye = p.ComponentCreatureModel.EyePosition;
            return BlocksManager.Blocks[terrain.Terrain.GetCellContents(Terrain.ToCell(eye.X), Terrain.ToCell(eye.Y), Terrain.ToCell(eye.Z))] is WaterBlock;
        }
        internal void Request(ComponentPlayer p, byte skill, Vector3 direction)
        {
            if (!Equipped(p)) return;
            if (p.ComponentLocomotion.IsCreativeFlyEnabled || p.ComponentBody.ParentBody != null || p.Entity.FindComponent<ComponentRider>(false)?.Mount != null)
            {
                if (p.PlayerData.IsMainPlayer) p.ComponentGui.DisplaySmallMessage("请先关闭创造飞行并离开坐骑，再施放海神技能。",new Color(112,221,245),false,false);
                return;
            }
            State state = Get(p);
            if (Now - state.LastRequest < 0.2) return;
            state.LastRequest = Now;
            var packet = new PoseidonPackage { Caster = p.Entity.EntityId, Skill = skill, Direction = direction, Sequence = ++state.Sent };
            if (Authority) Enqueue(p, packet); else CommonLib.Net.QueuePackage(packet);
        }
        // Network callbacks only enqueue immutable deserialized requests. All validation, impulses,
        // damage and presentation-state mutations run in the world's IUpdateable game-thread pass.
        internal void Enqueue(ComponentPlayer player, PoseidonPackage packet)
        {
            lock (m_pending)
            {
                if (!m_disposed && m_pending.Count < 32) m_pending.Enqueue(new Pending { Player = player, Packet = packet });
            }
        }
        internal void Accept(ComponentPlayer p, PoseidonPackage packet)
        {
            if (!Authority || !Equipped(p) || packet.Skill < 1 || packet.Skill > 2 || !Finite(packet.Direction)) return;
            State state = Get(p);
            if (!PoseidonRules.Newer(packet.Sequence, state.Accepted)) return;
            state.Accepted = packet.Sequence;
            var saints = Project.FindSubsystem<SubsystemFourSaints>(false);
            if (saints?.IsActionLocked(p) == true || p.ComponentBody.ParentBody != null || p.Entity.FindComponent<ComponentRider>(false)?.Mount != null || p.ComponentLocomotion.IsCreativeFlyEnabled) return;
            if ((packet.Skill == 1 ? state.DashReady : state.WaveReady) > Now || Casts.Count >= PoseidonRules.MaxCasts) return;
            foreach (Cast c in Casts) if (c.Player == p) return;
            Vector3 direction = packet.Direction;
            float length = direction.Length();
            if (length < 0.95f || length > 1.05f) return;
            bool water = InWater(p);
            direction.Y = packet.Skill == 1 && water ? MathUtils.Clamp(direction.Y, -0.35f, 0.35f) : 0;
            if (direction.LengthSquared() < 0.01f) return;
            direction = Vector3.Normalize(direction);
            Vector3 origin = p.ComponentBody.Position;
            if (!Loaded(origin) || !Loaded(origin + direction * 2)) return;
            if (packet.Skill == 1)
            {
                // Native authoritative impulse packets reach the remote owner; never teleport or
                // overwrite client-owned positions. ComponentBody performs swept collision steps.
                if (Wall(origin + Vector3.UnitY * 0.8f, origin + Vector3.UnitY * 0.8f + direction * 1.2f)) return;
                Vector3 desired = direction * PoseidonRules.DashSpeed(water);
                if (!water) desired.Y = MathUtils.Clamp(p.ComponentBody.Velocity.Y, 0.5f, 2f);
                p.ComponentBody.ApplyImpulse(desired - p.ComponentBody.Velocity);
                state.DashReady = Now + PoseidonRules.DashCooldown;
            }
            else state.WaveReady = Now + PoseidonRules.WaveCooldown;
            var cast = new Cast { Player = p, Skill = packet.Skill, Origin = origin, Direction = direction, Previous = origin, Start = Now, Water = water };
            Casts.Add(cast);
            Present(cast);
            Publish(state, 1, cast);
            Log.Information("[SCIEPort:Poseidon] cast=" + packet.Skill + " water=" + water + " role=" + CommonLib.WorkType + " caster=" + p.Entity.EntityId);
        }
        private void Present(Cast c)
        {
            c.Player.ComponentMiner.Poke(true);
            Project.FindSubsystem<SubsystemAudio>(true).PlaySound(c.Skill == 1 ? "Audio/Poseidon/Surge" : "Audio/Poseidon/Tsunami", 0.85f, 0, c.Origin, 16, false);
            if (c.Player.PlayerData.IsMainPlayer) c.Player.ComponentBody.ApplyShaking(c.Skill == 1 ? 0.12f : 0.3f);
        }
        /// <inheritdoc />
        public void Update(float dt)
        {
            for (int budget = 0; budget < 16; budget++)
            {
                Pending pending;
                lock (m_pending) { if (m_pending.Count == 0) break; pending = m_pending.Dequeue(); }
                if (pending.Player != null) Accept(pending.Player,pending.Packet); else if (!Authority) Receive(pending.Packet);
            }
            foreach (ComponentPlayer p in m_players.ComponentPlayers)
            {
                if (!Equipped(p) && !m_states.ContainsKey(p.Entity.EntityId)) continue;
                State s = Get(p);
                ApplyPassive(s, Equipped(p));
                if (Authority && Now >= s.NextSnapshot) { Publish(s, 2, null); s.NextSnapshot = Now + 0.5; }
            }
            m_remove.Clear();
            foreach (var pair in m_states)
                if (!pair.Value.Player.IsAddedToProject || !m_players.ComponentPlayers.Contains(pair.Value.Player)) { ApplyPassive(pair.Value, false); m_remove.Add(pair.Key); }
            foreach (ushort id in m_remove) { m_hud.Remove(m_states[id].Player); m_states.Remove(id); }
            for (int i = Casts.Count - 1; i >= 0; i--)
            {
                Cast c = Casts[i];
                float age = (float)(Now - c.Start);
                if (c.Start < 0 || age < 0 || !Equipped(c.Player) || c.Player.ComponentBody.ParentBody != null || c.Player.ComponentLocomotion.IsCreativeFlyEnabled)
                { Casts.RemoveAt(i); continue; }
                if (c.Skill == 1) { if (Authority) Dash(c); }
                else if (age >= PoseidonRules.WaveWindup) Wave(c, age);
                if (!PoseidonRules.Alive(c.Skill,age)) Casts.RemoveAt(i);
            }
        }
        private void ApplyPassive(State s, bool active)
        {
            var p = s.Player;
            var locomotion = p.ComponentLocomotion;
            float delta = PoseidonRules.SwimDelta(locomotion.SwimSpeed,s.SwimDelta,active);
            locomotion.SwimSpeed += delta - s.SwimDelta;
            s.SwimDelta = delta;
            if (active && InWater(p)) p.ComponentHealth.Air = 1f;
        }
        private void Dash(Cast c)
        {
            Vector3 next = c.Player.ComponentBody.Position;
            float moved = Vector3.Distance(c.Previous, next);
            // Network discontinuity, teleport or stale owner update is never an attack corridor.
            if (moved > 3f || !Loaded(next) || Wall(c.Previous + Vector3.UnitY, next + Vector3.UnitY)) { c.Start = -100; return; }
            Vector3 a = c.Previous + Vector3.UnitY * 0.8f, b = next + Vector3.UnitY * 0.8f + c.Direction * 0.7f;
            Query(c, (a+b)*0.5f, moved + 2f, body =>
            {
                Vector3 p = body.BoundingBox.Center();
                return PoseidonRules.SegmentDistanceSquared(p.X,p.Y,p.Z,a.X,a.Y,a.Z,b.X,b.Y,b.Z) < 1.5f * 1.5f;
            }, a);
            c.Previous = next;
        }
        private void Wave(Cast c, float age)
        {
            float next = PoseidonRules.WaveDistance(age);
            Vector3 a = c.Origin + c.Direction * c.Distance + Vector3.UnitY * 0.8f;
            Vector3 b = c.Origin + c.Direction * next + Vector3.UnitY * 0.8f;
            if (!Loaded(b) || Wall(a, b)) { c.Start = -100; return; }
            Vector3 side = Vector3.Cross(c.Direction, Vector3.UnitY);
            if (Authority) Query(c, (a+b)*0.5f, (next-c.Distance)*0.5f + 6f, body =>
            {
                Vector3 p = body.BoundingBox.Center() - c.Origin;
                return PoseidonRules.InSweep(Vector3.Dot(p,c.Direction),Vector3.Dot(p,side),p.Y,c.Distance,next,0.5f);
            }, a);
            c.Distance = next;
        }
        private void Query(Cast c, Vector3 center, float radius, Func<ComponentBody, bool> overlaps, Vector3 sightOrigin)
        {
            m_candidates.Clear();
            m_bodies.FindBodiesAroundPoint(new Vector2(center.X, center.Z), radius, m_candidates);
            for (int i = 0; i < Math.Min(m_candidates.Count, PoseidonRules.MaxCandidates) && c.Hit.Count < PoseidonRules.MaxHits; i++)
            {
                ComponentBody b = m_candidates.Array[i];
                if (b == c.Player.ComponentBody || !b.Entity.IsAddedToProject || c.Hit.Contains(b.Entity.EntityId) || Related(c.Player.ComponentBody, b)) continue;
                if (b.Player != null && !m_info.WorldSettings.IsFriendlyFireEnabled) continue;
                var health = b.Entity.FindComponent<ComponentHealth>(false);
                if (health == null || health.Health <= 0 || !overlaps(b) || Wall(sightOrigin, b.BoundingBox.Center())) continue;
                c.Hit.Add(b.Entity.EntityId);
                ComponentMiner.AttackBody(b, c.Player, b.BoundingBox.Center(), c.Direction, PoseidonRules.Damage(c.Skill,c.Water), false);
                b.ApplyImpulse(c.Direction * (c.Skill == 1 ? 6f : 10f) + Vector3.UnitY * 2f);
            }
        }
        private static bool Related(ComponentBody a, ComponentBody b) => a.ParentBody == b || b.ParentBody == a || a.ChildBodies.Contains(b) || b.ChildBodies.Contains(a);
        private bool Wall(Vector3 a, Vector3 b) => m_terrain.Raycast(a,b,false,true,(v,d) => BlocksManager.Blocks[Terrain.ExtractContents(v)].IsCollidable).HasValue;
        private bool Loaded(Vector3 p)
        {
            if (p.Y < 1 || p.Y > 254) return false;
            var chunk = m_terrain.Terrain.GetChunkAtCell(Terrain.ToCell(p.X),Terrain.ToCell(p.Z));
            return chunk != null && chunk.State >= TerrainChunkState.Valid;
        }
        private static bool Finite(Vector3 v) => PoseidonRules.Finite(v.X) && PoseidonRules.Finite(v.Y) && PoseidonRules.Finite(v.Z);
        private void Publish(State s, byte kind, Cast c)
        {
            if (CommonLib.WorkType != WorkType.Server) return;
            CommonLib.Net.QueuePackage(new PoseidonPackage { Kind = kind, Caster = s.Player.Entity.EntityId, Sequence = ++s.Revision,
                DashRemaining = (float)Math.Max(0, s.DashReady-Now), WaveRemaining = (float)Math.Max(0,s.WaveReady-Now),
                Skill = c?.Skill ?? 0, Origin = c?.Origin ?? Vector3.Zero, Direction = c?.Direction ?? Vector3.Zero, Water = c?.Water ?? false });
        }
        internal void Receive(PoseidonPackage packet)
        {
            foreach (ComponentPlayer p in m_players.ComponentPlayers)
            {
                if (p.Entity.EntityId != packet.Caster) continue;
                State s = Get(p);
                if (!PoseidonRules.Newer(packet.Sequence,s.Received)) return;
                s.Received = packet.Sequence;
                s.DashReady = Now + packet.DashRemaining; s.WaveReady = Now + packet.WaveRemaining;
                if (packet.Kind == 1 && Casts.Count < PoseidonRules.MaxCasts && Equipped(p))
                {
                    var c = new Cast { Player = p, Skill = packet.Skill, Start = Now, Origin = packet.Origin, Direction = packet.Direction, Water = packet.Water };
                    Casts.Add(c); Present(c);
                }
                return;
            }
        }
        /// <inheritdoc />
        public void Draw(Camera camera, int drawOrder) => m_scene.Draw(camera);
        /// <inheritdoc />
        public override void Dispose()
        {
            lock (m_pending) { m_disposed = true; m_pending.Clear(); }
            foreach (State s in m_states.Values) ApplyPassive(s,false);
            m_hud?.Dispose(); m_scene?.Dispose(); Casts.Clear(); m_states.Clear(); base.Dispose();
        }
    }
}

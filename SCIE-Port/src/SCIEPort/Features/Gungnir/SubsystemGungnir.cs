using System;
using System.Collections.Generic;
using Engine;
using Game;
using Game.NetWork;
using Game.NetWork.Packages;
using SCIEPort.Features.Stormhammer;
using TemplatesDatabase;

namespace SCIEPort.Features.Gungnir
{
    /// <summary>World-scoped Gungnir authority, native input adapter and presentation state.</summary>
    public sealed class SubsystemGungnir : SubsystemBlockBehavior, IUpdateable, IDrawable
    {
        internal sealed class State
        {
            internal ComponentPlayer Player;
            internal uint Sequence, Sent, Revision, Received;
            internal double BuffEnd, BuffReady, BuffVisualStart = -100, JudgmentReady, NextMelee, LastSnapshot, NextSnapshot, ChargeStart = -1;
            internal double CastStart = -100, FlightStart;
            internal float WalkDelta, JumpDelta, BaseWalk, Distance;
            internal bool Equipped, Impact;
            internal byte Phase;
            internal int Slot;
            internal Vector3 Position, Direction, Target, PreviousOwner;
            internal readonly HashSet<ushort> Hit = new HashSet<ushort>();
        }
        private sealed class PendingHolyDamage
        {
            internal ComponentPlayer Attacker;
            internal ComponentBody Target;
            internal double ApplyAt;
            internal float Power;
        }
        internal readonly Dictionary<ushort, State> States = new Dictionary<ushort, State>();
        private SubsystemTime m_time;
        private SubsystemBodies m_bodies;
        private SubsystemTerrain m_terrain;
        private SubsystemPlayers m_players;
        private SubsystemGameInfo m_info;
        private GungnirScene m_scene;
        private readonly List<ushort> m_remove = new List<ushort>();
        private readonly DynamicArray<ComponentBody> m_targets = new DynamicArray<ComponentBody>();
        private readonly List<PendingHolyDamage> m_pendingHolyDamage = new List<PendingHolyDamage>();
        internal double Now => m_time.GameTime;
        private bool Authority => CommonLib.WorkType != WorkType.Client;
        /// <inheritdoc />
        public override int[] HandledBlocks => new[] { GungnirBlock.Index };
        /// <inheritdoc />
        public UpdateOrder UpdateOrder => (UpdateOrder)(-5);
        /// <inheritdoc />
        public int[] DrawOrders => new[] { 105 };
        /// <inheritdoc />
        public override void Load(ValuesDictionary values)
        {
            base.Load(values);
            m_time = Project.FindSubsystem<SubsystemTime>(true);
            m_bodies = Project.FindSubsystem<SubsystemBodies>(true);
            m_terrain = Project.FindSubsystem<SubsystemTerrain>(true);
            m_players = Project.FindSubsystem<SubsystemPlayers>(true);
            m_info = Project.FindSubsystem<SubsystemGameInfo>(true);
            m_scene = new GungnirScene(this, m_terrain);
        }
        internal static bool Equipped(ComponentPlayer p) => p != null && p.IsAddedToProject && p.ComponentHealth.Health > 0 && Terrain.ExtractContents(p.ComponentMiner.ActiveBlockValue) == GungnirBlock.Index;
        internal State Get(ComponentPlayer p)
        {
            State s;
            if (!States.TryGetValue(p.Entity.EntityId, out s))
            {
                s = new State { Player = p, PreviousOwner = p.ComponentBody.Position };
                States.Add(p.Entity.EntityId, s);
            }
            return s;
        }
        internal bool InFlight(ComponentPlayer p) { State s; return p != null && States.TryGetValue(p.Entity.EntityId, out s) && s.Phase != 0; }
        internal void Request(ComponentPlayer p, byte command, Vector3 direction)
        {
            if (!Equipped(p)) return;
            State s = Get(p);
            var packet = new GungnirPackage { Caster = p.Entity.EntityId, Command = command, Direction = direction, Sequence = ++s.Sent };
            if (Authority) Accept(p, packet); else CommonLib.Net.QueuePackage(packet);
        }
        internal void Accept(ComponentPlayer p, GungnirPackage packet)
        {
            if (!Authority || !Equipped(p) || packet.Command < 1 || packet.Command > 4 || !Finite(packet.Direction)) return;
            State s = Get(p);
            bool nativeThrow = packet.Command == GungnirRules.Throw && packet.Kind == 255;
            if (!nativeThrow)
            {
                if (!GungnirRules.IsNewer(packet.Sequence, s.Sequence)) return;
                s.Sequence = packet.Sequence;
            }
            Vector3 direction = packet.Direction;
            float length = direction.Length();
            if (length < 0.9f || length > 1.1f) return;
            direction /= length;
            if (packet.Command == GungnirRules.Throw && !nativeThrow)
            {
                if (!GungnirRules.IsThrowSignal(packet.Phase)) return;
                if (packet.Phase == GungnirRules.ThrowBegin)
                {
                    if (s.Phase == 0 && s.ChargeStart < 0) s.ChargeStart = Now;
                    return;
                }
                if (packet.Phase == GungnirRules.ThrowCancel)
                {
                    s.ChargeStart = -1;
                    return;
                }
                // The host owns the charge clock. Client duration only tolerates ordered packets
                // delivered in one network batch; it cannot bypass the required begin signal.
                if (!GungnirRules.CanCompleteThrow(s.ChargeStart >= 0, Now - s.ChargeStart, packet.Blessing)) return;
            }
            if (packet.Command == GungnirRules.Blessing)
            {
                if (Now < s.BuffReady) return;
                s.BuffVisualStart = Now; s.BuffEnd = Now + 15; s.BuffReady = Now + GungnirRules.BlessingCooldown;
                Sound("Invoke", p.ComponentBody.Position);
            }
            else if (packet.Command == GungnirRules.Judgment)
            {
                if (Now < s.JudgmentReady || s.Phase != 0) return;
                Vector3 origin = p.ComponentCreatureModel.EyePosition;
                var hit = m_terrain.Raycast(origin, origin + direction * 25, false, true, (v, d) => BlocksManager.Blocks[Terrain.ExtractContents(v)].IsCollidable);
                if (!hit.HasValue || !Loaded(hit.Value.HitPoint())) return;
                s.Target = hit.Value.HitPoint() + Vector3.UnitY * 0.04f;
                s.CastStart = Now; s.Impact = false; s.JudgmentReady = Now + GungnirRules.JudgmentCooldown;
                Sound("Invoke", s.Target);
            }
            else if (packet.Command == GungnirRules.Throw)
            {
                if (s.Phase != 0 || s.ChargeStart < 0 || nativeThrow && Now - s.ChargeStart < 0.25 || Now - s.CastStart < 0.3) return;
                s.ChargeStart = -1; s.Phase = 1;
                s.Slot = p.ComponentMiner.Inventory.ActiveSlotIndex;
                s.Position = p.ComponentCreatureModel.EyePosition; s.Direction = direction;
                s.Distance = 0; s.FlightStart = Now; s.Hit.Clear();
            }
            else
            {
                if (s.Phase != 0 || Now < s.NextMelee || Now - s.CastStart < 0.3) return;
                s.NextMelee = Now + GungnirRules.AttackInterval(Now < s.BuffEnd);
                Vector3 start = p.ComponentCreatureModel.EyePosition, end = start + direction * 3.5f;
                var wall = m_terrain.Raycast(start, end, false, true, (v, d) => BlocksManager.Blocks[Terrain.ExtractContents(v)].IsCollidable);
                if (wall.HasValue) end = wall.Value.HitPoint();
                var target = m_bodies.Raycast(start, end, 0.08f, (b, d) => Valid(p, b));
                p.ComponentMiner.Poke(true);
                if (target.HasValue)
                {
                    float boost = Now < s.BuffEnd ? 1.5f : 1;
                    Damage(p, target.Value.ComponentBody, 45 * boost, direction);
                    QueueHolyDamage(p, target.Value.ComponentBody, 20 * boost);
                }
                Publish(new GungnirPackage { Kind = 2, Caster = p.Entity.EntityId, Position = start, Direction = direction });
            }
            Snapshot(s);
        }
        /// <inheritdoc />
        public override bool OnAim(Ray3 aim, ComponentMiner miner, AimState state)
        {
            var p = miner.ComponentPlayer;
            if (!Equipped(p)) return false;
            var s = Get(p);
            if (state == AimState.InProgress)
            {
                if (s.Phase != 0) return true;
                if (s.ChargeStart < 0)
                {
                    s.ChargeStart = Now;
                    if (!Authority) SendThrowSignal(p, GungnirRules.ThrowBegin, aim.Direction, 0f);
                }
                p.ComponentCreatureModel.AimHandAngleOrder = 3.2f;
                p.ComponentCreatureModel.InHandItemRotationOrder = new Vector3(3.14159f, 0, 0);
                var model = p.Entity.FindComponent<ComponentFirstPersonModel>(false);
                if (model != null) { model.ItemOffsetOrder = new Vector3(0, 0.35f, 0.17f); model.ItemRotationOrder = new Vector3(-1.5f, 0, 0); }
                if (p.PlayerData.IsMainPlayer) p.ComponentAimingSights.ShowAimingSights(aim.Position, aim.Direction);
            }
            else if (state == AimState.Completed)
            {
                if (Authority) Accept(p, new GungnirPackage { Kind = 255, Command = GungnirRules.Throw, Direction = aim.Direction });
                else SendThrowSignal(p, GungnirRules.ThrowComplete, aim.Direction, (float)Math.Max(0, Now - s.ChargeStart));
                s.ChargeStart = -1;
            }
            else
            {
                if (!Authority && s.ChargeStart >= 0) SendThrowSignal(p, GungnirRules.ThrowCancel, aim.Direction, 0f);
                s.ChargeStart = -1;
            }
            // x25.01.17 interprets true during InProgress as "cancel aiming now".
            // Match SubsystemThrowableBlockBehavior: keep holding with false and consume
            // only the terminal Completed/Cancelled callbacks.
            return GungnirRules.ShouldConsumeAim(state == AimState.InProgress);
        }

        private void SendThrowSignal(ComponentPlayer player, byte signal, Vector3 direction, float charge)
        {
            State state = Get(player);
            CommonLib.Net.QueuePackage(new GungnirPackage
            {
                Kind = 0,
                Command = GungnirRules.Throw,
                Phase = signal,
                Caster = player.Entity.EntityId,
                Sequence = ++state.Sent,
                Direction = direction,
                Blessing = charge
            });
        }
        /// <inheritdoc />
        public void Update(float dt)
        {
            foreach (var p in m_players.ComponentPlayers)
            {
                if (!Equipped(p) && !States.ContainsKey(p.Entity.EntityId)) continue;
                State s = Get(p);
                if (Authority)
                {
                    bool equipped = Equipped(p);
                    if (s.Equipped != equipped) { s.Equipped = equipped; s.BaseWalk = p.ComponentLocomotion.WalkSpeed - s.WalkDelta; s.NextSnapshot = 0; }
                    if (!equipped) { s.BuffEnd = Now; s.Phase = 0; s.ChargeStart = -1; }
                    if (s.Phase != 0) AdvanceFlight(s, MathUtils.Clamp(dt, 0, 0.1f));
                    if (!s.Impact && Now - s.CastStart >= 1.4 && Now - s.CastStart < 4)
                    {
                        s.Impact = true;
                        if (p.ComponentHealth.Health > 0 && Loaded(s.Target)) Judgment(s);
                    }
                    if (Now >= s.NextSnapshot) { Snapshot(s); s.NextSnapshot = Now + (s.Phase == 0 ? 0.5 : 0.05); }
                }
                if (!Authority && (Now - s.LastSnapshot >= 2 || p.ComponentHealth.Health <= 0))
                {
                    s.Equipped = false; s.Phase = 0; s.BuffEnd = Now; s.CastStart = -100;
                    GungnirSkyVisuals.RemoveCaster(Project, p.Entity.EntityId);
                }
                float judgmentAge = (float)(Now - s.CastStart);
                if (p.ComponentHealth.Health > 0f && judgmentAge >= 0f && judgmentAge < GungnirSkyRules.Lifetime)
                    GungnirSkyVisuals.Sync(Project, p.Entity.EntityId, s.Target, judgmentAge, p.PlayerData.IsMainPlayer, Now);
                else if (p.ComponentHealth.Health <= 0f || judgmentAge >= GungnirSkyRules.Lifetime)
                    GungnirSkyVisuals.RemoveCaster(Project, p.Entity.EntityId);
                bool active = s.Equipped && Equipped(p) && (Authority || Now - s.LastSnapshot < 2);
                ApplyModifiers(s, active);
                if (p.PlayerData.IsMainPlayer && Equipped(p))
                {
                    var input = p.ComponentInput.PlayerInput;
                    if (input.Hit.HasValue)
                    {
                        if (Now >= s.NextMelee || Authority) Request(p, GungnirRules.Melee, input.Hit.Value.Direction);
                        if (!Authority && Now >= s.NextMelee) s.NextMelee = Now + GungnirRules.AttackInterval(Now < s.BuffEnd);
                        // Consume only native melee; native Aim continues unchanged.
                        p.ComponentInput.m_playerInput.Hit = null;
                    }
                }
                if (Now - s.CastStart < 0.3)
                {
                    p.ComponentCreatureModel.AimHandAngleOrder = 3.2f;
                    var first = p.Entity.FindComponent<ComponentFirstPersonModel>(false);
                    if (first != null) { first.ItemOffsetOrder = new Vector3(-0.12f, 0.45f, 0.12f); first.ItemRotationOrder = new Vector3(-0.6f, 0, 0); }
                }
            }
            m_remove.Clear();
            foreach (var entry in States) if (entry.Value.Player == null || !entry.Value.Player.IsAddedToProject) m_remove.Add(entry.Key);
            foreach (ushort id in m_remove) { ApplyModifiers(States[id], false); GungnirSkyVisuals.RemoveCaster(Project, id); States.Remove(id); }
            GungnirSkyVisuals.Update(Project, Now);
            ApplyPendingHolyDamage();
            MjolnirDisplay.Update(Project, Now);
            m_scene.Update(dt);
        }

        private void QueueHolyDamage(ComponentPlayer attacker, ComponentBody target, float power)
        {
            m_pendingHolyDamage.Add(new PendingHolyDamage { Attacker = attacker, Target = target, Power = power, ApplyAt = Now + GungnirRules.HolyDamageDelay });
        }

        private void ApplyPendingHolyDamage()
        {
            if (!Authority) return;
            for (int i = m_pendingHolyDamage.Count - 1; i >= 0; i--)
            {
                PendingHolyDamage pending = m_pendingHolyDamage[i];
                if (Now < pending.ApplyAt) continue;
                m_pendingHolyDamage.RemoveAt(i);
                if (pending.Attacker == null || !pending.Attacker.IsAddedToProject || pending.Target == null || !pending.Target.Entity.IsAddedToProject) continue;
                ComponentHealth health = pending.Target.Entity.FindComponent<ComponentHealth>(false);
                if (health == null || health.Health <= 0f) continue;
                health.Injure(pending.Power / MathUtils.Max(health.AttackResilience, 0.001f), pending.Attacker, false, "永恒之枪·神圣贯穿");
                ShowHolyHitValue(pending.Target, pending.Power);
            }
        }

        private void ShowHolyHitValue(ComponentBody target, float power)
        {
            Vector3 position = target.BoundingBox.Center() + new Vector3(0f, 0.72f, 0f);
            Vector3 velocity = new Vector3(0.28f, 1.75f, 0.28f);
            string text = ((int)MathUtils.Round(power)).ToString();
            Color color = new Color(255, 205, 92, 255);
            CommonLib.Net.QueuePackage(new ComponentHealthPackage(position, velocity, color, text));
            Project.FindSubsystem<SubsystemParticles>(true).AddParticleSystem(new HitValueParticleSystem(position, velocity, color, text));
        }
        private void ApplyModifiers(State s, bool enabled)
        {
            if (s.Player == null) return;
            var l = s.Player.ComponentLocomotion;
            // Subtract only our additive contribution; preserve other writers' additive changes.
            l.WalkSpeed -= s.WalkDelta; l.JumpSpeed -= s.JumpDelta;
            s.WalkDelta = s.JumpDelta = 0;
            if (!enabled || l.IsCreativeFlyEnabled || s.Player.ComponentRider.Mount != null) return;
            bool blessed = Now < s.BuffEnd;
            s.WalkDelta = MathUtils.Max(0, s.BaseWalk) * (GungnirRules.SpeedFactor(blessed) - 1);
            if (blessed) s.JumpDelta = MathUtils.Max(0, GungnirRules.JumpSpeed(s.Player.ComponentLevel.SpeedFactor) - l.JumpSpeed);
            l.WalkSpeed += s.WalkDelta; l.JumpSpeed += s.JumpDelta;
        }
        private void AdvanceFlight(State s, float dt)
        {
            Vector3 owner = s.Player.ComponentBody.Position;
            // Inventory is never removed or re-created: this is a visual flight with a server combat lease.
            if (Vector3.DistanceSquared(owner, s.PreviousOwner) > 1024 || Now - s.FlightStart > 6 || !Loaded(s.Position)) { s.Phase = 0; return; }
            s.PreviousOwner = owner;
            if (s.Player.ComponentMiner.Inventory.ActiveSlotIndex != s.Slot) { s.Phase = 0; return; }
            if (s.Phase == 2)
            {
                Vector3 delta = s.Player.ComponentCreatureModel.EyePosition - s.Position;
                float length = delta.Length();
                if (length < 0.6f) { s.Phase = 0; return; }
                s.Direction = delta / length;
                s.Position += s.Direction * MathUtils.Min(length, 45 * dt);
                return;
            }
            float distance = MathUtils.Min(35 * dt, 40 - s.Distance);
            Vector3 end = s.Position + s.Direction * distance;
            if (!Loaded(end)) { BeginReturn(s); return; }
            var wall = m_terrain.Raycast(s.Position, end, false, true, (v, d) => BlocksManager.Blocks[Terrain.ExtractContents(v)].IsCollidable);
            if (wall.HasValue) end = wall.Value.HitPoint();
            for (int i = s.Hit.Count; i < 8; i++)
            {
                var hit = m_bodies.Raycast(s.Position, end, 0.12f, (b, d) => Valid(s.Player, b) && !s.Hit.Contains(b.Entity.EntityId));
                if (!hit.HasValue) break;
                var target = hit.Value.ComponentBody;
                float power = (s.Hit.Count == 0 ? 120 : 90) * (Now < s.BuffEnd ? 1.5f : 1);
                s.Hit.Add(target.Entity.EntityId);
                Damage(s.Player, target, power, s.Direction);
                m_scene.FlightHit(target.BoundingBox.Center(), s.Direction);
            }
            s.Distance += Vector3.Distance(s.Position, end); s.Position = end;
            if (wall.HasValue || s.Distance >= 39.99f || s.Hit.Count >= 8 || Now - s.FlightStart > 2) BeginReturn(s);
        }
        private void BeginReturn(State s)
        {
            if (s.Phase == 2) return;
            s.Phase = 2;
            Vector3 returnDirection = s.Player.ComponentCreatureModel.EyePosition - s.Position;
            m_scene.FlightReturn(s.Position, returnDirection.LengthSquared() > 0.01f ? Vector3.Normalize(returnDirection) : -s.Direction);
        }
        private void Judgment(State s)
        {
            m_targets.Clear(); m_bodies.FindBodiesAroundPoint(new Vector2(s.Target.X, s.Target.Z), 7, m_targets);
            for (int i = 0; i < m_targets.Count; i++)
            {
                var b = m_targets.Array[i];
                float power = GungnirRules.JudgmentDamage(Vector3.Distance(b.BoundingBox.Center(), s.Target));
                if (power > 0 && Valid(s.Player, b)) Damage(s.Player, b, power, Vector3.UnitY);
            }
            Sound("Impact", s.Target);
        }
        private bool Valid(ComponentPlayer p, ComponentBody b)
        {
            if (b == p.ComponentBody || !b.Entity.IsAddedToProject || b.Player != null && !m_info.WorldSettings.IsFriendlyFireEnabled) return false;
            var h = b.Entity.FindComponent<ComponentHealth>(false); return h != null && h.Health > 0;
        }
        private void Damage(ComponentPlayer p, ComponentBody b, float power, Vector3 direction) => ComponentMiner.AttackBody(b, p, b.BoundingBox.Center(), direction, power, false);
        private bool Loaded(Vector3 p)
        {
            var chunk = m_terrain.Terrain.GetChunkAtCell(Terrain.ToCell(p.X), Terrain.ToCell(p.Z));
            return chunk != null && chunk.State >= TerrainChunkState.Valid;
        }
        private static bool Finite(Vector3 v) => !float.IsNaN(v.X) && !float.IsNaN(v.Y) && !float.IsNaN(v.Z) && !float.IsInfinity(v.X) && !float.IsInfinity(v.Y) && !float.IsInfinity(v.Z);
        private void Snapshot(State s)
        {
            Publish(new GungnirPackage { Kind = 1, Caster = s.Player.Entity.EntityId, Sequence = ++s.Revision, Equipped = s.Equipped, Phase = s.Phase, Position = s.Position, Direction = s.Direction, Target = s.Target,
                Blessing = (float)Math.Max(0, s.BuffEnd - Now), BlessingCooldown = (float)Math.Max(0, s.BuffReady - Now), JudgmentCooldown = (float)Math.Max(0, s.JudgmentReady - Now), JudgmentAge = (float)(Now - s.CastStart) });
        }
        private void Publish(GungnirPackage p) { if (p.Kind == 2) m_scene.Stab(p.Position, p.Direction); if (CommonLib.WorkType == WorkType.Server) CommonLib.Net.QueuePackage(p); }
        internal void Receive(GungnirPackage p)
        {
            if (p.Kind == 2)
            {
                m_scene.Stab(p.Position, p.Direction);
                foreach (var item in m_players.ComponentPlayers) if (item.Entity.EntityId == p.Caster) item.ComponentMiner.Poke(true);
                return;
            }
            if (p.Kind == 3) { PlaySound(p.Command == 0 ? "Invoke" : "Impact", p.Position); return; }
            ComponentPlayer player = null;
            foreach (var item in m_players.ComponentPlayers) if (item.Entity.EntityId == p.Caster) { player = item; break; }
            if (player == null) return;
            State s = Get(player);
            if (!GungnirRules.IsNewer(p.Sequence, s.Received)) return;
            s.Received = p.Sequence; s.LastSnapshot = Now;
            if (!s.Equipped && p.Equipped) s.BaseWalk = player.ComponentLocomotion.WalkSpeed - s.WalkDelta;
            byte previousPhase = s.Phase;
            s.Equipped = p.Equipped; s.Phase = p.Phase; s.Position = p.Position; s.Direction = p.Direction; s.Target = p.Target;
            if (previousPhase == 1 && p.Phase == 2)
            {
                Vector3 returnDirection = player.ComponentCreatureModel.EyePosition - p.Position;
                m_scene.FlightReturn(p.Position, returnDirection.LengthSquared() > 0.01f ? Vector3.Normalize(returnDirection) : -p.Direction);
            }
            if (p.Blessing > 0f && s.BuffEnd <= Now) s.BuffVisualStart = Now;
            s.BuffEnd = Now + p.Blessing; s.BuffReady = Now + p.BlessingCooldown; s.JudgmentReady = Now + p.JudgmentCooldown; s.CastStart = Now - p.JudgmentAge;
        }
        private void Sound(string name, Vector3 p)
        {
            PlaySound(name, p);
            if (CommonLib.WorkType == WorkType.Server) CommonLib.Net.QueuePackage(new GungnirPackage { Kind = 3, Command = (byte)(name == "Invoke" ? 0 : 1), Position = p });
        }
        private void PlaySound(string name, Vector3 p)
        {
            Project.FindSubsystem<SubsystemAudio>(true).PlaySound("Audio/Gungnir/" + name, 0.8f, 0, p, 10, false);
            if (name == "Impact" && SCIEPort.Features.Stormhammer.StormhammerOptions.CameraShake)
            {
                foreach (var player in m_players.ComponentPlayers)
                {
                    float distance = Vector3.Distance(player.ComponentBody.Position, p);
                    if (player.PlayerData.IsMainPlayer && distance < 20) player.ComponentBody.ApplyShaking(0.6f * (1 - distance / 20));
                }
            }
        }
        /// <inheritdoc />
        public void Draw(Camera camera, int drawOrder) => m_scene.Draw(camera);
        /// <inheritdoc />
        public override void Dispose()
        {
            foreach (var s in States.Values) ApplyModifiers(s, false);
            m_pendingHolyDamage.Clear(); States.Clear(); GungnirSkyVisuals.Reset(Project); GungnirHud.Clear(); base.Dispose();
        }
    }
}

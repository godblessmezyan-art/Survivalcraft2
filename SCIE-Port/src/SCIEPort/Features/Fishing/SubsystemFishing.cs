#pragma warning disable CS1591
using System.Collections.Generic;
using Engine;
using Game;
using Game.NetWork;
using GameEntitySystem;
using TemplatesDatabase;

namespace SCIEPort.Features.Fishing
{
    public sealed partial class SubsystemFishing : SubsystemBlockBehavior, IUpdateable, IDrawable
    {
        private sealed class Session
        {
            internal ComponentPlayer Player;
            internal Client Client;
            internal uint Sequence;
            internal Point3 Cell;
            internal int RodSlot;
            internal int BaitSlot;
            internal Vector3 CastEye;
            internal FishingCastArc Arc;
            internal Point3? Water;
            internal double ImpactAt;
            internal FishingCycle Cycle;
            internal double NextSnapshot;
        }

        private readonly Dictionary<ushort, Session> m_sessions = new Dictionary<ushort, Session>();
        private readonly List<Session> m_updating = new List<Session>();
        private readonly System.Random m_random = new System.Random();
        private SubsystemTerrain m_terrain;
        private SubsystemParticles m_particles;
        private SubsystemAudio m_audio;
        private SubsystemPickables m_pickables;
        private SubsystemPlayers m_players;
        private SubsystemTime m_time;
        private FishingWater m_water;
        private uint m_sequence;

        public override int[] HandledBlocks => new[] { FishingRodBlock.Index };
        // Sample the native aim input after ComponentInput and before ComponentPlayer can mine or interact.
        public UpdateOrder UpdateOrder => (UpdateOrder)(-5);
        private static bool Authority => CommonLib.WorkType != WorkType.Client;
        private double Now => m_time.GameTime;

        public override void Load(ValuesDictionary values)
        {
            base.Load(values);
            m_terrain = Project.FindSubsystem<SubsystemTerrain>(true);
            m_particles = Project.FindSubsystem<SubsystemParticles>(false);
            m_audio = Project.FindSubsystem<SubsystemAudio>(false);
            m_pickables = Project.FindSubsystem<SubsystemPickables>(true);
            m_players = Project.FindSubsystem<SubsystemPlayers>(true);
            m_time = Project.FindSubsystem<SubsystemTime>(true);
            m_water = new FishingWater(m_terrain);
        }

        public override void OnEntityRemoved(Entity entity)
        {
            base.OnEntityRemoved(entity);
            if (m_sessions.TryGetValue(entity.EntityId, out Session session))
                Finish(session, FishingCatchPackage.Closed);
            m_castAttempts.Remove(entity.EntityId);
            m_heldInput.Remove(entity.EntityId);
            m_views.Remove(entity.EntityId);
        }

        public override void Dispose()
        {
            m_sessions.Clear();
            m_castAttempts.Clear();
            m_heldInput.Clear();
            m_updating.Clear();
            m_views.Clear();
            m_landings.Clear();
            m_renderer.Clear();
            base.Dispose();
        }

        public override bool OnUse(Ray3 ray, ComponentMiner miner)
        {
            return Terrain.ExtractContents(miner.ActiveBlockValue) == FishingRodBlock.Index;
        }

        public void Update(float dt)
        {
            foreach (ComponentPlayer player in m_players.ComponentPlayers)
            {
                if (IsLocal(player)) ReadInput(player);
            }

            if (Authority)
            {
                ExpireCharges();
                m_updating.AddRange(m_sessions.Values);
                foreach (Session session in m_updating)
                    Advance(session, Now);
                m_updating.Clear();
            }
            UpdateViews();
        }

        private void ReadInput(ComponentPlayer player)
        {
            ushort id = player.Entity.EntityId;
            ComponentInput component = player.ComponentInput;
            PlayerInput input = component.PlayerInput;
            bool held = input.Aim.HasValue;
            bool pressed = held ? m_heldInput.Add(id) : false;
            if (!held) m_heldInput.Remove(id);
            int slot = player.ComponentMiner.Inventory?.ActiveSlotIndex ?? -1;

            if (!m_views.TryGetValue(id, out View view))
            {
                if (!Equipped(player, slot) || !CanAim(player)) return;
                if (held) ConsumeRodInput(component);
                if (pressed && FishingCastArc.TryDirection(input.Aim.Value.Direction, out Vector3 direction))
                    BeginLocalCharge(player, slot, direction);
                return;
            }

            if (!Equipped(player, view.Slot))
            {
                Submit(player, view, view.Drawing ? FishingCatchPackage.AbortCharge : FishingCatchPackage.Cancel);
                m_views.Remove(id);
                return;
            }

            bool tapped = input.Interact.HasValue;
            if (tapped || held) ConsumeRodInput(component);
            if (view.Drawing)
            {
                ReadCharge(player, view, input.Aim);
                return;
            }
            if (view.Kind >= FishingCatchPackage.Windup || FishingCatchPackage.IsTerminal(view.Kind))
                return;

            if (view.Kind == FishingCatchPackage.Reeling || view.HookSent)
            {
                if (held != view.SentHeld || (held && !Authority && Now >= view.NextHold))
                {
                    view.SentHeld = held;
                    view.NextHold = Now + FishingRules.UpdateInterval;
                    Submit(player, view, held ? FishingCatchPackage.Hold : FishingCatchPackage.Release);
                }
            }
            else if (view.Kind == FishingCatchPackage.Bite && (tapped || pressed))
            {
                view.HookSent = true;
                view.SentHeld = held;
                view.NextHold = Now + FishingRules.UpdateInterval;
                Submit(player, view, FishingCatchPackage.Hook, count: held ? (byte)1 : (byte)0);
            }
            else if (tapped)
            {
                Submit(player, view, FishingCatchPackage.Cancel);
            }
        }

        private static void ConsumeRodInput(ComponentInput component)
        {
            component.m_playerInput.Interact = null;
            component.m_playerInput.Hit = null;
            component.m_playerInput.Dig = null;
        }

        private void Submit(ComponentPlayer player, View view, byte kind, Point3 cell = default(Point3), byte count = 0)
        {
            var packet = new FishingCatchPackage
            {
                Kind = kind,
                Caster = player.Entity.EntityId,
                Sequence = view.Sequence,
                Cell = cell,
                Count = count
            };
            if (Authority)
                AcceptInput(player, packet, null);
            else
                CommonLib.Net.QueuePackage(packet);
        }

        internal void AcceptInput(ComponentPlayer player, FishingCatchPackage packet, Client client)
        {
            if (!Authority || packet.Sequence == 0) return;
            double now = Now;
            ushort id = player.Entity.EntityId;
            if (packet.Kind >= FishingCatchPackage.Charge && packet.Kind <= FishingCatchPackage.AbortCharge)
            {
                AcceptCastInput(player, packet, client, now);
                return;
            }
            if (!m_sessions.TryGetValue(id, out Session session) || session.Player != player ||
                !ReferenceEquals(session.Client, client) || session.Sequence != packet.Sequence) return;
            if (packet.Kind == FishingCatchPackage.Cancel || !ValidSession(session))
            {
                Finish(session, FishingCatchPackage.Closed);
                return;
            }

            if (session.Cycle == null) return;
            if (packet.Kind == FishingCatchPackage.Hook && session.Cycle.Hook(now, packet.Count == 1))
            {
                Snapshot(session, now);
                return;
            }
            if (packet.Kind == FishingCatchPackage.Hold || packet.Kind == FishingCatchPackage.Release)
                session.Cycle.SetHeld(now, packet.Kind == FishingCatchPackage.Hold);
        }

        private void Advance(Session session, double now)
        {
            if (!ValidSession(session))
            {
                Finish(session, FishingCatchPackage.Closed);
                return;
            }
            if (session.Cycle == null)
            {
                if (now >= session.ImpactAt) Impact(session, now);
                return;
            }

            FishingPhase previous = session.Cycle.Phase;
            bool tense = session.Cycle.Tense;
            session.Cycle.Advance(now);
            switch (session.Cycle.Phase)
            {
                case FishingPhase.Approach:
                    if (previous != FishingPhase.Approach) Publish(session, FishingCatchPackage.Approach);
                    break;
                case FishingPhase.Bite:
                    if (previous == FishingPhase.Bite) break;
                    if (session.BaitSlot >= 0 && !ConsumeBait(session))
                        Finish(session, FishingCatchPackage.LostBait);
                    else
                        Publish(session, FishingCatchPackage.Bite);
                    break;
                case FishingPhase.Reeling:
                    if (now >= session.NextSnapshot || tense != session.Cycle.Tense)
                        Snapshot(session, now);
                    break;
                case FishingPhase.Landed:
                    Finish(session, FishingCatchPackage.Landed);
                    break;
                case FishingPhase.Escaped:
                    Finish(session, FishingCatchPackage.Escaped);
                    break;
            }
        }

        private void Snapshot(Session session, double now)
        {
            session.NextSnapshot = now + FishingRules.UpdateInterval;
            int percent = (int)(session.Cycle.Progress * 100f);
            if (session.Cycle.Tense) percent |= FishingCatchPackage.TenseBit;
            Publish(session, FishingCatchPackage.Reeling, (byte)percent);
        }

        private bool ValidSession(Session session)
        {
            if (!Equipped(session.Player, session.RodSlot)) return false;
            Vector3 target = session.Arc.Origin;
            if (session.Cycle != null)
            {
                if (!m_water.IsDeep(session.Cell)) return false;
                target = m_water.Surface(session.Cell, session.Arc.End);
            }
            return Vector3.DistanceSquared(session.Player.ComponentCreatureModel.EyePosition, target)
                <= FishingRules.MaxSessionDistance * FishingRules.MaxSessionDistance;
        }

        private static bool Equipped(ComponentPlayer player, int slot)
        {
            return player != null && player.IsAddedToProject && player.ComponentHealth.Health > 0 &&
                player.Entity.FindComponent<ComponentSleep>(false)?.IsSleeping != true &&
                slot >= 0 && player.ComponentMiner.Inventory?.ActiveSlotIndex == slot &&
                Terrain.ExtractContents(player.ComponentMiner.ActiveBlockValue) == FishingRodBlock.Index;
        }

        private static bool IsLocal(ComponentPlayer player) => player?.PlayerData?.IsMainPlayer == true;

        private static bool HasBait(IInventory inventory, int slot)
        {
            if (inventory == null || slot < 0 || slot >= inventory.VisibleSlotsCount || inventory.GetSlotCount(slot) == 0)
                return false;
            int value = inventory.GetSlotValue(slot);
            var seed = (SeedsBlock.SeedType)Terrain.ExtractData(value);
            return Terrain.ExtractContents(value) == SeedsBlock.Index &&
                (seed == SeedsBlock.SeedType.Rye || seed == SeedsBlock.SeedType.WildRye);
        }

        private static bool ConsumeBait(Session session)
        {
            IInventory inventory = session.Player.ComponentMiner.Inventory;
            return HasBait(inventory, session.BaitSlot) && inventory.RemoveSlotItems(session.BaitSlot, 1) == 1;
        }

        private void Finish(Session session, byte kind)
        {
            m_sessions.Remove(session.Player.Entity.EntityId);
            byte count = kind == FishingCatchPackage.Landed ? Grant(session.Player, session.Cycle.Reward) : (byte)0;
            Publish(session, kind, count);
        }

        private byte Grant(ComponentPlayer player, int count)
        {
            int value = Terrain.MakeBlockValue(RawFishBlock.Index);
            IInventory inventory = player.ComponentMiner.Inventory;
            int remaining = inventory != null ? ComponentInventoryBase.AcquireItems(inventory, value, count) : count;
            if (remaining > 0)
            {
                Vector3 position = player.ComponentBody.Position + new Vector3(0, 0.6f, 0);
                m_pickables.AddPickable(value, remaining, position, new Vector3(0, 1.5f, 0), null);
            }
            return FishingRules.PackReward(count, remaining);
        }

        private void Publish(Session session, byte kind, byte count = 0)
        {
            Broadcast(session.Player, session.Sequence, kind, session.Cell, count);
        }

        private void Broadcast(ComponentPlayer player, uint sequence, byte kind, Point3 position = default(Point3), byte count = 0)
        {
            var packet = new FishingCatchPackage
            {
                Kind = kind,
                Caster = player.Entity.EntityId,
                Cell = position,
                Sequence = sequence,
                Count = count
            };
            if (CommonLib.WorkType == WorkType.Server)
                CommonLib.Net.QueuePackage(packet);
            ApplyState(packet, player);
        }

        private void Reject(FishingCatchPackage request, Client client, byte reason)
        {
            var packet = new FishingCatchPackage { Kind = reason, Caster = request.Caster, Sequence = request.Sequence, To = client };
            if (client != null)
                CommonLib.Net.QueuePackage(packet);
            else
                ApplyToPlayer(packet);
        }
    }
}

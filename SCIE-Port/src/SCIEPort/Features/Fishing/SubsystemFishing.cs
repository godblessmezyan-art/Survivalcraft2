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
            internal FishingCycle Cycle;
            internal double NextSnapshot;
        }

        private sealed class CastAttempt
        {
            internal uint Sequence;
            internal double NextCast;
        }

        private readonly Dictionary<ushort, Session> m_sessions = new Dictionary<ushort, Session>();
        private readonly Dictionary<ushort, CastAttempt> m_castAttempts = new Dictionary<ushort, CastAttempt>();
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
        // Sample the native touch hold after ComponentInput and before ComponentPlayer handles digging.
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
            m_water = new FishingWater(m_terrain.Terrain);
        }

        public override void OnEntityRemoved(Entity entity)
        {
            base.OnEntityRemoved(entity);
            if (m_sessions.TryGetValue(entity.EntityId, out Session session))
                Finish(session, FishingCatchPackage.Closed);
            m_castAttempts.Remove(entity.EntityId);
            m_views.Remove(entity.EntityId);
        }

        public override void Dispose()
        {
            m_sessions.Clear();
            m_castAttempts.Clear();
            m_updating.Clear();
            m_views.Clear();
            m_landings.Clear();
            m_renderer.Clear();
            base.Dispose();
        }

        public override bool OnUse(Ray3 ray, ComponentMiner miner)
        {
            if (Terrain.ExtractContents(miner.ActiveBlockValue) != FishingRodBlock.Index) return false;
            ComponentPlayer player = miner.ComponentPlayer;
            if (!IsLocal(player) || m_views.ContainsKey(player.Entity.EntityId)) return true;
            if (!m_water.TryCast(player.ComponentCreatureModel.EyePosition, ray.Direction,
                FishingRules.MaxCastDistance, out Point3 cell))
            {
                Message(player, "此处无鱼");
                return true;
            }

            var view = new View
            {
                Sequence = ++m_sequence,
                Slot = miner.Inventory.ActiveSlotIndex,
                WasHeld = player.ComponentInput.PlayerInput.Aim.HasValue,
                Seen = Now
            };
            m_views.Add(player.Entity.EntityId, view);
            Submit(player, view, FishingCatchPackage.Cast, cell);
            return true;
        }

        public void Update(float dt)
        {
            foreach (ComponentPlayer player in m_players.ComponentPlayers)
            {
                if (IsLocal(player) && m_views.TryGetValue(player.Entity.EntityId, out View view))
                    ReadInput(player, view);
            }

            if (Authority)
            {
                m_updating.AddRange(m_sessions.Values);
                foreach (Session session in m_updating)
                    Advance(session, Now);
                m_updating.Clear();
            }
            UpdateViews();
        }

        private void ReadInput(ComponentPlayer player, View view)
        {
            if (!Equipped(player, view.Slot))
            {
                Submit(player, view, FishingCatchPackage.Cancel);
                m_views.Remove(player.Entity.EntityId);
                return;
            }

            ComponentInput component = player.ComponentInput;
            PlayerInput input = component.PlayerInput;
            bool tapped = input.Interact.HasValue;
            bool held = input.Aim.HasValue;
            bool pressed = held && !view.WasHeld;
            view.WasHeld = held;
            if (tapped || held)
            {
                component.m_playerInput.Interact = null;
                component.m_playerInput.Hit = null;
                component.m_playerInput.Dig = null;
            }

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
            else if (tapped && view.Kind != 0)
            {
                Submit(player, view, FishingCatchPackage.Cancel);
            }
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
            if (packet.Kind == FishingCatchPackage.Cast)
            {
                StartCast(player, packet, client, now);
                return;
            }
            if (!m_sessions.TryGetValue(id, out Session session) || session.Player != player ||
                !ReferenceEquals(session.Client, client) || session.Sequence != packet.Sequence) return;
            if (packet.Kind == FishingCatchPackage.Cancel)
            {
                Finish(session, FishingCatchPackage.Closed);
                return;
            }

            if (!ValidSession(session))
            {
                Finish(session, FishingCatchPackage.Closed);
                return;
            }
            if (packet.Kind == FishingCatchPackage.Hook && session.Cycle.Hook(now, packet.Count == 1))
            {
                Snapshot(session, now);
                return;
            }
            if (packet.Kind == FishingCatchPackage.Hold || packet.Kind == FishingCatchPackage.Release)
                session.Cycle.SetHeld(now, packet.Kind == FishingCatchPackage.Hold);
        }

        private void StartCast(ComponentPlayer player, FishingCatchPackage packet, Client client, double now)
        {
            ushort id = player.Entity.EntityId;
            if (!m_castAttempts.TryGetValue(id, out CastAttempt attempt))
                m_castAttempts.Add(id, attempt = new CastAttempt());
            if (packet.Sequence <= attempt.Sequence) return;
            attempt.Sequence = packet.Sequence;
            if (now < attempt.NextCast)
            {
                Reject(packet, client, FishingCatchPackage.Closed);
                return;
            }
            attempt.NextCast = now + FishingRules.CastInterval;

            IInventory inventory = player.ComponentMiner.Inventory;
            int slot = inventory?.ActiveSlotIndex ?? -1;
            if (m_sessions.ContainsKey(id) || !Equipped(player, slot))
            {
                Reject(packet, client, FishingCatchPackage.Closed);
                return;
            }
            if (!m_water.ValidateTarget(player.ComponentCreatureModel.EyePosition, packet.Cell))
            {
                Reject(packet, client, FishingCatchPackage.InvalidWater);
                return;
            }

            int baitSlot = slot + 1;
            bool baited = HasBait(inventory, baitSlot);
            bool large = FishingRules.IsLarge(m_random, baited);
            var session = new Session
            {
                Player = player,
                Client = client,
                Sequence = packet.Sequence,
                Cell = packet.Cell,
                RodSlot = slot,
                BaitSlot = baited ? baitSlot : -1,
                Cycle = new FishingCycle(now, FishingRules.WaitDuration(m_random, baited),
                    FishingRules.ReelDuration(m_random, large), large, client != null)
            };
            m_sessions.Add(id, session);
            Publish(session, FishingCatchPackage.Waiting, baited ? (byte)1 : (byte)0);
        }

        private void Advance(Session session, double now)
        {
            if (!ValidSession(session))
            {
                Finish(session, FishingCatchPackage.Closed);
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
            return Equipped(session.Player, session.RodSlot) && m_water.IsDeep(session.Cell) &&
                Vector3.Distance(session.Player.ComponentCreatureModel.EyePosition, m_water.Surface(session.Cell))
                    <= FishingRules.MaxSessionDistance;
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
            var packet = new FishingCatchPackage
            {
                Kind = kind,
                Caster = session.Player.Entity.EntityId,
                Cell = session.Cell,
                Sequence = session.Sequence,
                Count = count
            };
            if (CommonLib.WorkType == WorkType.Server)
                CommonLib.Net.QueuePackage(packet);
            ApplyState(packet, session.Player);
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

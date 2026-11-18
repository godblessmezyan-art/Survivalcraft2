#pragma warning disable CS1591
using System.Collections.Generic;
using Engine;
using Game;
using Game.NetWork;

namespace SCIEPort.Features.Fishing
{
    public sealed partial class SubsystemFishing
    {
        private sealed class CastAttempt
        {
            internal uint Sequence;
            internal double NextCast;
            internal double Started;
            internal ComponentPlayer Player;
            internal Client Client;
            internal int Slot;
            internal bool Charging;
        }

        private readonly Dictionary<ushort, CastAttempt> m_castAttempts = new Dictionary<ushort, CastAttempt>();
        private readonly HashSet<ushort> m_heldInput = new HashSet<ushort>();

        private static bool CanAim(ComponentPlayer player)
        {
            return player.PlayerData.GameWidget == null || (Window.IsActive &&
                player.ComponentGui.ModalPanelWidget == null && !DialogsManager.HasDialogs(player.GuiWidget));
        }

        private void BeginLocalCharge(ComponentPlayer player, int slot, Vector3 direction)
        {
            var view = new View
            {
                Sequence = ++m_sequence,
                Slot = slot,
                Kind = FishingCatchPackage.Windup,
                Drawing = true,
                Aim = direction,
                Seen = Now,
                PhaseAt = Now
            };
            m_views.Add(player.Entity.EntityId, view);
            RefreshPreview(player, view);
            Submit(player, view, FishingCatchPackage.Charge);
        }

        private void ReadCharge(ComponentPlayer player, View view, Ray3? aim)
        {
            double heldFor = Now - view.PhaseAt;
            if (!CanAim(player) || heldFor > FishingCastArc.ChargeTimeout)
            {
                view.Drawing = false;
                Submit(player, view, FishingCatchPackage.AbortCharge);
                return;
            }
            if (aim.HasValue)
            {
                bool valid = FishingCastArc.TryDirection(aim.Value.Direction, out Vector3 direction);
                view.Aim = direction;
                if (!valid)
                    view.Preview = default(FishingCastArc);
                else if (Now >= view.NextPreview)
                    RefreshPreview(player, view);
                return;
            }

            view.Drawing = false;
            bool cast = heldFor >= FishingCastArc.MinimumCharge && view.Aim.LengthSquared() > 0f;
            if (cast)
            {
                view.CastAt = Now;
                view.CastForce = FishingCastArc.Charge(heldFor);
            }
            Submit(player, view, cast ? FishingCatchPackage.Cast : FishingCatchPackage.AbortCharge,
                FishingCastArc.Pack(view.Aim));
        }

        private void RefreshPreview(ComponentPlayer player, View view)
        {
            Vector3 eye = player.ComponentCreatureModel.EyePosition;
            var arc = FishingCastArc.Launch(eye, view.Aim, FishingCastArc.Charge(Now - view.PhaseAt));
            view.Preview = m_water.Trace(eye, arc, out Point3? water);
            view.PreviewWater = water.HasValue;
            view.NextPreview = Now + 0.075;
        }

        private void AcceptCastInput(ComponentPlayer player, FishingCatchPackage packet, Client client, double now)
        {
            ushort id = player.Entity.EntityId;
            if (packet.Kind == FishingCatchPackage.Charge)
            {
                if (!m_castAttempts.TryGetValue(id, out CastAttempt fresh))
                    m_castAttempts.Add(id, fresh = new CastAttempt());
                if (packet.Sequence <= fresh.Sequence) return;
                if (fresh.Charging || m_sessions.ContainsKey(id) || now < fresh.NextCast)
                {
                    Reject(packet, client, FishingCatchPackage.Closed);
                    return;
                }
                fresh.Sequence = packet.Sequence;
                fresh.NextCast = now + FishingRules.CastInterval;

                int slot = player.ComponentMiner.Inventory?.ActiveSlotIndex ?? -1;
                if (!Equipped(player, slot))
                {
                    Reject(packet, client, FishingCatchPackage.Closed);
                    return;
                }
                fresh.Charging = true;
                fresh.Player = player;
                fresh.Client = client;
                fresh.Slot = slot;
                fresh.Started = now;
                Broadcast(player, packet.Sequence, FishingCatchPackage.Windup);
                return;
            }

            if (!m_castAttempts.TryGetValue(id, out CastAttempt attempt) || !attempt.Charging ||
                attempt.Sequence != packet.Sequence || attempt.Player != player || !ReferenceEquals(attempt.Client, client))
                return;
            attempt.Charging = false;
            double elapsed = now - attempt.Started;
            // A small lower-edge allowance covers two valid input messages delivered in adjacent network batches.
            double minimum = FishingCastArc.MinimumCharge - (client != null ? 0.06 : 0);
            if (packet.Kind != FishingCatchPackage.Cast || elapsed < minimum || elapsed > FishingCastArc.ChargeTimeout ||
                !Equipped(player, attempt.Slot) ||
                !FishingCastArc.TryDirection(FishingCastArc.Unpack(packet.Cell), out Vector3 direction))
            {
                Broadcast(player, packet.Sequence, FishingCatchPackage.Closed);
                return;
            }

            Vector3 eye = player.ComponentCreatureModel.EyePosition;
            var arc = FishingCastArc.Launch(eye, direction, FishingCastArc.Charge(elapsed));
            arc = m_water.Trace(eye, arc, out Point3? water);
            m_sessions.Add(id, new Session
            {
                Player = player,
                Client = client,
                Sequence = packet.Sequence,
                RodSlot = attempt.Slot,
                CastEye = eye,
                Arc = arc,
                Water = water,
                ImpactAt = now + arc.Duration
            });
            Broadcast(player, packet.Sequence, FishingCatchPackage.FlightOrigin, FishingCastArc.Pack(arc.Origin));
            Broadcast(player, packet.Sequence, FishingCatchPackage.Flight, FishingCastArc.Pack(arc.Velocity),
                (byte)MathUtils.Clamp((int)MathUtils.Round(arc.Duration * 100f), 1, 200));
        }

        private void ExpireCharges()
        {
            foreach (CastAttempt attempt in m_castAttempts.Values)
            {
                if (!attempt.Charging || (Equipped(attempt.Player, attempt.Slot) && Now - attempt.Started <= FishingCastArc.ChargeTimeout))
                    continue;
                attempt.Charging = false;
                Broadcast(attempt.Player, attempt.Sequence, FishingCatchPackage.Closed);
            }
        }

        private void Impact(Session session, double now)
        {
            // Recheck against current terrain in case the water or a wall changed in flight.
            var arc = session.Arc;
            if (!session.Water.HasValue)
            {
                Finish(session, FishingCatchPackage.InvalidWater);
                return;
            }
            m_water.Trace(session.CastEye, arc, out Point3? currentWater);
            if (!currentWater.HasValue || currentWater.Value != session.Water.Value)
            {
                Finish(session, FishingCatchPackage.InvalidWater);
                return;
            }

            session.Cell = currentWater.Value;
            int baitSlot = session.RodSlot + 1;
            bool baited = HasBait(session.Player.ComponentMiner.Inventory, baitSlot);
            bool large = FishingRules.IsLarge(m_random, baited);
            session.BaitSlot = baited ? baitSlot : -1;
            session.Cycle = new FishingCycle(now, FishingRules.WaitDuration(m_random, baited),
                FishingRules.ReelDuration(m_random, large), large, session.Client != null);
            Publish(session, FishingCatchPackage.Waiting, baited ? (byte)1 : (byte)0);
        }
    }
}

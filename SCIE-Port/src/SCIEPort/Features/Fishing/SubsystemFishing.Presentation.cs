#pragma warning disable CS1591
using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;

namespace SCIEPort.Features.Fishing
{
    public sealed partial class SubsystemFishing
    {
        private sealed class View
        {
            internal uint Sequence;
            internal int Slot;
            internal byte Kind;
            internal Vector3 Bobber;
            internal double Seen;
            internal double PhaseAt;
            internal bool Drawing;
            internal Vector3 Aim;
            internal FishingCastArc Preview;
            internal bool PreviewWater;
            internal double NextPreview;
            internal FishingCastArc Flight;
            internal double FlightAt;
            internal double CastAt = double.NegativeInfinity;
            internal float CastForce;
            internal bool HookSent;
            internal bool SentHeld;
            internal double NextHold;
            internal bool Tense;
            internal float Progress;
            internal float PreviousProgress;

            internal float DrawProgress(double now) => MathUtils.Lerp(PreviousProgress, Progress,
                MathUtils.Saturate((float)(now - Seen) / FishingRules.UpdateInterval));
        }

        private sealed class Landing
        {
            internal ComponentPlayer Player;
            internal Vector3 Start;
            internal double Time;
        }

        private static readonly int[] s_drawOrders = { 205 };
        private readonly Dictionary<ushort, View> m_views = new Dictionary<ushort, View>();
        private readonly List<ushort> m_stale = new List<ushort>();
        private readonly List<Landing> m_landings = new List<Landing>();
        private readonly PrimitivesRenderer3D m_renderer = new PrimitivesRenderer3D();
        private readonly DrawBlockEnvironmentData m_fishLight = new DrawBlockEnvironmentData { Light = 15 };

        public int[] DrawOrders => s_drawOrders;

        internal void ReceiveState(FishingCatchPackage packet)
        {
            if (!Authority) ApplyToPlayer(packet);
        }

        private void ApplyToPlayer(FishingCatchPackage packet)
        {
            foreach (ComponentPlayer player in m_players.ComponentPlayers)
            {
                if (player.Entity.EntityId != packet.Caster) continue;
                ApplyState(packet, player);
                return;
            }
        }

        private void ApplyState(FishingCatchPackage packet, ComponentPlayer player)
        {
            ushort id = packet.Caster;
            m_views.TryGetValue(id, out View view);
            bool local = IsLocal(player);
            byte kind = packet.Kind;
            if (view != null && view.Sequence != packet.Sequence)
            {
                if (local || FishingCatchPackage.IsTerminal(kind)) return;
                view = null;
            }

            if (FishingCatchPackage.IsTerminal(kind))
            {
                if (view == null) return;
                if (kind == FishingCatchPackage.InvalidWater && Now - view.CastAt < FollowThroughDuration)
                {
                    view.Kind = kind;
                    view.Seen = Now;
                }
                else
                    m_views.Remove(id);
                if (kind == FishingCatchPackage.Landed)
                    ShowLanded(player, view, packet.Count);
                else if (local && kind == FishingCatchPackage.Escaped)
                    Message(player, "鱼跑了");
                else if (local && kind == FishingCatchPackage.InvalidWater)
                    Message(player, "此处无鱼");
                else if (local && kind == FishingCatchPackage.LostBait)
                    Message(player, "鱼饵已移走");
                return;
            }

            if (view == null)
            {
                view = new View { Sequence = packet.Sequence, Slot = player.ComponentMiner.Inventory?.ActiveSlotIndex ?? -1 };
                m_views[id] = view;
            }
            double now = Now;
            if (kind >= FishingCatchPackage.Windup)
            {
                ApplyCastView(player, view, packet, now);
                return;
            }
            bool changed = view.Kind != kind;
            if (changed) view.PhaseAt = now;
            view.PreviousProgress = view.DrawProgress(now);
            view.Seen = now;
            view.Kind = kind;
            view.Bobber = m_water.Surface(packet.Cell,
                view.Flight.Duration > 0f ? (Vector3?)view.Flight.End : null);

            if (kind == FishingCatchPackage.Waiting && changed)
            {
                Splash(view.Bobber, true);
                if (local && packet.Count == 1) Message(player, "已挂饵");
            }
            else if (kind == FishingCatchPackage.Bite && changed)
            {
                Splash(view.Bobber, true);
                if (local) Message(player, "提竿");
            }
            else if (kind == FishingCatchPackage.Reeling)
            {
                view.Progress = (packet.Count & 127) / 100f;
                bool tense = (packet.Count & FishingCatchPackage.TenseBit) != 0;
                if (tense && !view.Tense) Splash(view.Bobber, true);
                if (local && (changed || view.Tense != tense)) Message(player, tense ? "松线" : "按住收线");
                view.Tense = tense;
            }
        }

        private void ShowLanded(ComponentPlayer player, View view, byte reward)
        {
            if (m_landings.Count >= 16) m_landings.RemoveAt(0);
            m_landings.Add(new Landing { Player = player, Start = view.Bobber, Time = Now });
            Splash(view.Bobber, true);
            if (!IsLocal(player)) return;
            int total = reward & 15;
            int dropped = reward >> 4;
            string fish = BlocksManager.Blocks[RawFishBlock.Index].GetDisplayName(m_terrain, Terrain.MakeBlockValue(RawFishBlock.Index));
            Message(player, dropped == 0 ? $"获得{fish} ×{total}"
                : total == dropped ? $"{fish}掉在脚边 ×{dropped}"
                : $"获得{fish} ×{total - dropped}，落地 ×{dropped}");
        }

        private void UpdateViews()
        {
            double now = Now;
            m_stale.Clear();
            foreach (var pair in m_views)
            {
                View view = pair.Value;
                double timeout = FishingRules.ClientTimeout;
                if (view.Kind == FishingCatchPackage.Waiting) timeout = 27f;
                else if (view.Kind == FishingCatchPackage.Windup) timeout = FishingCastArc.ChargeTimeout + 1f;
                if (now - view.Seen > timeout || (FishingCatchPackage.IsTerminal(view.Kind) && now - view.CastAt >= FollowThroughDuration))
                    m_stale.Add(pair.Key);
            }
            foreach (ushort id in m_stale) m_views.Remove(id);
            m_landings.RemoveAll(landing => now - landing.Time > 0.55);
            foreach (ComponentPlayer player in m_players.ComponentPlayers)
            {
                if (m_views.TryGetValue(player.Entity.EntityId, out View view))
                    ApplyRodPose(player, view, now);
            }
        }

        public void Draw(Camera camera, int drawOrder)
        {
            if (m_views.Count == 0 && m_landings.Count == 0) return;
            double now = Now;
            FlatBatch3D batch = m_renderer.FlatBatch(0, DepthStencilState.DepthRead,
                RasterizerState.CullNoneScissor, BlendState.NonPremultiplied);

            foreach (ComponentPlayer player in m_players.ComponentPlayers)
            {
                if (!m_views.TryGetValue(player.Entity.EntityId, out View view)) continue;
                if (view.Drawing && camera.GameWidget.PlayerData.ComponentPlayer == player)
                    DrawPreview(batch, view.Preview, view.PreviewWater);
                if (view.Kind == FishingCatchPackage.Flight)
                {
                    if (Vector3.DistanceSquared(camera.ViewPosition, view.Flight.Origin) <= 34f * 34f)
                        DrawFlight(camera, batch, player, view, now);
                }
                else if (view.Kind >= FishingCatchPackage.Waiting && view.Kind <= FishingCatchPackage.Reeling &&
                    Vector3.DistanceSquared(camera.ViewPosition, view.Bobber) <= 34f * 34f)
                    DrawLine(camera, batch, player, view, now);
            }

            m_fishLight.SubsystemTerrain = m_terrain;
            int value = Terrain.MakeBlockValue(RawFishBlock.Index);
            foreach (Landing landing in m_landings)
            {
                if (!landing.Player.IsAddedToProject ||
                    Vector3.DistanceSquared(camera.ViewPosition, landing.Start) > 34f * 34f) continue;
                float progress = MathUtils.Saturate((float)(now - landing.Time) / 0.55f);
                Vector3 end = RodTip(camera, landing.Player) + new Vector3(0, -0.2f, 0);
                Vector3 position = Vector3.Lerp(landing.Start, end, 0.55f + progress * 0.45f);
                position.Y += (float)Math.Sin(progress * Math.PI) * 0.75f;
                var matrix = Matrix.CreateFromYawPitchRoll(4f * progress, -0.45f, 0.5f * MathUtils.Sin(progress * 24f));
                matrix.Translation = position;
                m_fishLight.InWorldMatrix = matrix;
                BlocksManager.Blocks[RawFishBlock.Index].DrawBlock(m_renderer, value, Color.White,
                    0.45f, ref matrix, m_fishLight);
            }
            m_renderer.Flush(camera.ViewProjectionMatrix);
        }

        private static void DrawLine(Camera camera, FlatBatch3D batch, ComponentPlayer player, View view, double now)
        {
            Vector3 tip = RodTip(camera, player);
            float age = (float)(now - view.PhaseAt);
            float progress = view.DrawProgress(now);
            Vector3 position = view.Bobber;
            if (view.Kind == FishingCatchPackage.Reeling)
            {
                position = Vector3.Lerp(position, tip - new Vector3(0, 0.35f, 0), progress * 0.93f);
                position.Y += view.Tense ? 0.09f * MathUtils.Sin(age * 23f) : 0.025f * MathUtils.Sin(age * 8f);
            }
            else if (view.Kind == FishingCatchPackage.Bite)
                position.Y -= 0.07f + 0.04f * MathUtils.Sin(age * 13f);
            else
                position.Y += 0.025f * MathUtils.Sin(age * 4.5f);

            Vector3 middle = (tip + position) * 0.5f;
            middle.Y -= view.Tense ? 0.025f : 0.19f;
            Color line = view.Tense ? new Color(245, 164, 109, 235) : new Color(229, 229, 204, 200);
            batch.QueueLine(tip, middle, line);
            batch.QueueLine(middle, position, line);

            DrawBobber(camera, batch, position);
            if (view.Kind == FishingCatchPackage.Approach)
                DrawApproach(batch, position, tip, age);
        }

        private static void BobberBand(FlatBatch3D batch, Vector3 position, Vector3 side, Vector3 up, float bottom, float top, Color color)
        {
            Vector3 a = position + up * bottom;
            Vector3 b = position + up * top;
            batch.QueueQuad(a - side, a + side, b + side * 0.85f, b - side * 0.85f, color);
        }

        private static void DrawApproach(FlatBatch3D batch, Vector3 bobber, Vector3 tip, float age)
        {
            Vector3 offset = bobber - tip;
            offset.Y = 0;
            if (offset.LengthSquared() < 0.01f) return;
            Vector3 direction = Vector3.Normalize(offset);
            Vector3 side = Vector3.Cross(Vector3.UnitY, direction) * 0.22f;
            Vector3 center = bobber + direction * (1.55f * MathUtils.Saturate(1f - age / 0.9f));
            center.Y += 0.045f;
            Color water = new Color(203, 233, 231, 210);
            batch.QueueLine(center + side + direction * 0.23f, center, water);
            batch.QueueLine(center, center - side + direction * 0.23f, water);
        }

        private void Splash(Vector3 position, bool sound)
        {
            if (m_particles != null)
                m_particles.AddParticleSystem(new WaterSplashParticleSystem(m_terrain, position, false));
            if (sound) m_audio?.PlayRandomSound("Audio/Splashes", 0.8f, 0f, position, 12f, false);
        }

        private static void Message(ComponentPlayer player, string text)
        {
            player.ComponentGui.DisplaySmallMessage(text, Color.White, false, false);
        }
    }
}

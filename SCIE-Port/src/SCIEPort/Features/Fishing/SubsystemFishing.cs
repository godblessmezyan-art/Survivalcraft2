#pragma warning disable CS1591
using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;
using Game.NetWork;
using TemplatesDatabase;

namespace SCIEPort.Features.Fishing
{
    /// <summary>Surface-aware touch fishing with bounded host rounds and persistent bobbers.</summary>
    public sealed class SubsystemFishing : SubsystemBlockBehavior, IUpdateable, IDrawable
    {
        private sealed class Session
        {
            internal ComponentPlayer Player;
            internal Client Client;
            internal Vector3 Bobber;
            internal Point3 Cell;
            internal FishingRound Round;
            internal uint Sequence;
            internal double PendingSince;
            internal bool AwaitingStart, AwaitingResult, Biting;
        }
        private sealed class Pending { internal FishingCatchPackage Packet; internal Client Client; }
        private readonly Dictionary<ushort, Session> m_sessions = new Dictionary<ushort, Session>();
        private readonly Dictionary<ushort, Session> m_history = new Dictionary<ushort, Session>();
        private readonly Dictionary<ushort, double> m_nextCast = new Dictionary<ushort, double>();
        private readonly Dictionary<ushort, FishingRateLimiter> m_limiters = new Dictionary<ushort, FishingRateLimiter>();
        private readonly List<ushort> m_remove = new List<ushort>();
        private readonly Queue<Pending> m_pending = new Queue<Pending>();
        private readonly Engine.Random m_random = new Engine.Random();
        private SubsystemTerrain m_terrain;
        private SubsystemParticles m_particles;
        private SubsystemPickables m_pickables;
        private SubsystemTime m_time;
        private PrimitivesRenderer3D m_renderer;
        private Func<int, int, int, FishingCell> m_readCell;
        private uint m_sequence;
        private bool m_disposed;
        public override int[] HandledBlocks => new[] { FishingRodBlock.Index };
        public UpdateOrder UpdateOrder => UpdateOrder.Default;
        public int[] DrawOrders { get; } = new[] { 106 };
        private bool Authority => CommonLib.WorkType != WorkType.Client;
        private double Now => m_time.GameTime;

        public override void Load(ValuesDictionary values)
        {
            base.Load(values);
            m_terrain = Project.FindSubsystem<SubsystemTerrain>(true);
            m_particles = Project.FindSubsystem<SubsystemParticles>(true);
            m_pickables = Project.FindSubsystem<SubsystemPickables>(true);
            m_time = Project.FindSubsystem<SubsystemTime>(true);
            m_readCell = ReadCell;
        }
        public override void Dispose()
        {
            lock (m_pending) { m_disposed = true; m_pending.Clear(); }
            m_sessions.Clear(); m_history.Clear(); m_nextCast.Clear(); m_limiters.Clear();
            m_renderer?.Clear(); m_renderer = null;
            base.Dispose();
        }
        private static bool Equipped(ComponentPlayer p) => p != null && p.IsAddedToProject &&
            p.ComponentHealth.Health > 0f && Terrain.ExtractContents(p.ComponentMiner.ActiveBlockValue) == FishingRodBlock.Index;
        private static bool Local(ComponentPlayer p) => p?.PlayerData != null && p.PlayerData.IsMainPlayer;
        private static FishingPoint Point(Vector3 p) => new FishingPoint(p.X, p.Y, p.Z);

        public override bool OnUse(Ray3 ray, ComponentMiner miner)
        {
            if (Terrain.ExtractContents(miner.ActiveBlockValue) != FishingRodBlock.Index) return false;
            ComponentPlayer p = miner.ComponentPlayer;
            if (!Local(p) || !Equipped(p)) return true;
            Session s;
            if (m_sessions.TryGetValue(p.Entity.EntityId, out s))
            { if (!s.AwaitingStart && !s.AwaitingResult) Reel(s); }
            else Cast(p, ray);
            return true;
        }
        // Network callbacks only copy bounded messages. World/inventory work stays in Update.
        internal void Enqueue(FishingCatchPackage packet, Client client)
        {
            lock (m_pending)
            {
                if (m_disposed || m_pending.Count >= 128) return;
                m_pending.Enqueue(new Pending { Client = client, Packet = new FishingCatchPackage {
                    Kind = packet.Kind, Caster = packet.Caster, Cell = packet.Cell,
                    Sequence = packet.Sequence, Count = packet.Count } });
            }
        }
        public void Update(float dt)
        {
            for (int i = 0; i < 16; i++)
            {
                Pending request;
                lock (m_pending) { if (m_pending.Count == 0) break; request = m_pending.Dequeue(); }
                if (Authority) HandleHost(request); else HandleReply(request.Packet);
            }
            m_remove.Clear();
            foreach (var pair in m_sessions)
            {
                Session s = pair.Value;
                bool remote = s.Client != null;
                if (!Equipped(s.Player) || (remote && !ReferenceEquals(s.Client.PlayerData?.ComponentPlayer, s.Player)) ||
                    FishingRules.ShouldEndSession(Vector3.Distance(s.Player.ComponentBody.Position, s.Bobber)) || !ValidWater(s))
                { Message(s.Player, "已收竿"); End(s, true); m_remove.Add(pair.Key); continue; }
                if (s.AwaitingStart || s.AwaitingResult)
                {
                    if (Now - s.PendingSince > FishingRules.ReplyTimeout)
                    { Message(s.Player, "连接超时"); End(s, true); m_remove.Add(pair.Key); }
                    continue;
                }
                if (!s.Biting && Now >= s.Round.Start + s.Round.Wait)
                {
                    s.Biting = true;
                    if (Local(s.Player)) { Splash(s.Bobber); Message(s.Player, "咬钩", true); }
                }
                if (s.Round.Expired(Now, remote ? FishingRules.NetworkGrace : 0f))
                { Message(s.Player, "鱼跑了"); End(s, true); m_remove.Add(pair.Key); }
            }
            foreach (ushort id in m_remove) m_sessions.Remove(id);
            // Prune only disconnected identities; keep replay history for connected players.
            m_remove.Clear();
            foreach (var pair in m_history)
                if (pair.Value.Client == null || pair.Value.Client.State < ClientState.ProjectLoaded ||
                    !pair.Value.Player.IsAddedToProject || !ReferenceEquals(pair.Value.Client.PlayerData?.ComponentPlayer, pair.Value.Player)) m_remove.Add(pair.Key);
            foreach (ushort id in m_remove) { m_history.Remove(id); m_nextCast.Remove(id); m_limiters.Remove(id); }
        }
        private FishingCell ReadCell(int x, int y, int z)
        {
            Terrain t = m_terrain.Terrain;
            if (!t.IsCellValid(x, y, z)) return new FishingCell(3);
            TerrainChunk chunk = t.GetChunkAtCell(x, z);
            if (chunk == null || chunk.State != TerrainChunkState.Valid) return new FishingCell(3);
            int value = t.GetCellValue(x, y, z), contents = Terrain.ExtractContents(value);
            if (contents == 0) return new FishingCell(0);
            if (contents != WaterBlock.Index || y >= 255 || t.GetCellContents(x, y + 1, z) != 0) return new FishingCell(2);
            var water = BlocksManager.Blocks[WaterBlock.Index] as FluidBlock;
            return water == null ? new FishingCell(3) : new FishingCell(1, DecodeWaterHeight(water, value));
        }
        internal static float DecodeWaterHeight(FluidBlock water, int value)
            => water.GetLevelHeight(FluidBlock.GetLevel(Terrain.ExtractData(value)));
        private bool ValidWater(Session s)
        {
            FishingCell cell = ReadCell(s.Cell.X, s.Cell.Y, s.Cell.Z);
            return cell.Kind == 1 && cell.Height > 0f && Math.Abs(s.Bobber.Y - s.Cell.Y - cell.Height) < .15f;
        }
        private void Cast(ComponentPlayer p, Ray3 ray)
        {
            ushort id = p.Entity.EntityId;
            double next;
            if (m_nextCast.TryGetValue(id, out next) && Now < next) return;
            FishingLanding hit;
            FishingCastFailure failure = FishingCastResolver.Resolve(Point(ray.Position), Point(ray.Direction), m_readCell, out hit);
            if (failure != FishingCastFailure.None)
            {
                m_nextCast[id] = Now + .5f;
                Message(p, failure == FishingCastFailure.Blocked ? "落点受阻" : failure == FishingCastFailure.Unavailable ? "水域未就绪" : "抛向近处水面");
                return;
            }
            if (m_sessions.Count >= 64) { Message(p, "稍后再钓"); return; }
            var s = new Session { Player = p, Cell = new Point3(hit.X, hit.Y, hit.Z),
                Bobber = new Vector3(hit.Position.X, hit.Position.Y, hit.Position.Z), Sequence = ++m_sequence };
            if (s.Sequence == 0) s.Sequence = ++m_sequence;
            if (Authority) s.Round = NewRound(s.Sequence);
            else { s.AwaitingStart = true; s.PendingSince = Now; Send(s, FishingCatchPackage.Start); }
            m_sessions[id] = s; Splash(s.Bobber);
        }
        private FishingRound NewRound(uint sequence)
        {
            float wait = FishingRules.MinWaitDuration + (float)Math.Floor((FishingRules.WaitDuration(m_random.Float(0f, 1f)) - FishingRules.MinWaitDuration) * 50f) / 50f;
            return new FishingRound(sequence, Now, wait);
        }
        private void Reel(Session s)
        {
            if (!s.Round.TryReel(s.Sequence, Now, 0f) || !ValidWater(s) || !Reachable(s.Player, s.Bobber, s.Cell))
            { Message(s.Player, "鱼跑了"); End(s, true); m_sessions.Remove(s.Player.Entity.EntityId); return; }
            if (Authority)
            { Grant(s.Player, FishingRules.RewardCount(m_random.Int(0, 99))); Message(s.Player, "钓到鱼"); End(s, false); m_sessions.Remove(s.Player.Entity.EntityId); }
            else { s.AwaitingResult = true; s.PendingSince = Now; Send(s, FishingCatchPackage.Request); }
        }
        private void HandleHost(Pending pending)
        {
            Client client = pending.Client;
            ComponentPlayer p = client?.PlayerData?.ComponentPlayer;
            FishingCatchPackage packet = pending.Packet;
            if (p == null || client.State < ClientState.ProjectLoaded || p.Entity.EntityId != packet.Caster || packet.Sequence == 0) return;
            ushort id = p.Entity.EntityId;
            Session s;
            m_sessions.TryGetValue(id, out s);
            if (packet.Kind == FishingCatchPackage.Start)
            {
                Session previous;
                if (m_history.TryGetValue(id, out previous) && ReferenceEquals(previous.Client, client) &&
                    !FishingRules.NewerSequence(packet.Sequence, previous.Sequence)) return;
                double next;
                if (s != null || m_sessions.Count >= 64 || (m_history.Count >= 128 && !m_history.ContainsKey(id)) || (m_nextCast.TryGetValue(id, out next) && Now < next))
                { Reply(client, packet, FishingCatchPackage.Result, 0); return; }
                m_nextCast[id] = Now + .5f;
                FishingCell cell = ReadCell(packet.Cell.X, packet.Cell.Y, packet.Cell.Z);
                Vector3 center = new Vector3(packet.Cell.X + .5f, packet.Cell.Y + cell.Height, packet.Cell.Z + .5f);
                if (!Equipped(p) || cell.Kind != 1 || !Reachable(p, center, packet.Cell))
                { Reply(client, packet, FishingCatchPackage.Result, 0); return; }
                s = new Session { Player = p, Client = client, Cell = packet.Cell, Bobber = center,
                    Sequence = packet.Sequence, Round = NewRound(packet.Sequence) };
                m_sessions.Add(id, s); m_history[id] = s;
                Reply(client, packet, FishingCatchPackage.Started, (byte)Math.Round((s.Round.Wait - FishingRules.MinWaitDuration) * 50f));
                return;
            }
            if (s == null || !ReferenceEquals(s.Client, client) || s.Sequence != packet.Sequence || !s.Cell.Equals(packet.Cell)) return;
            if (packet.Kind == FishingCatchPackage.Cancel) { End(s, true); m_sessions.Remove(id); return; }
            if (packet.Kind != FishingCatchPackage.Request) return;
            // Consume/remove before inventory changes so retries cannot duplicate the reward.
            m_sessions.Remove(id);
            bool success = s.Round.TryReel(packet.Sequence, Now, FishingRules.NetworkGrace) && Equipped(p) && ValidWater(s) && Reachable(p, s.Bobber, s.Cell) && Limiter(id).TryAccept(Now);
            End(s, !success);
            byte count = success ? (byte)FishingRules.RewardCount(m_random.Int(0, 99)) : (byte)0;
            if (count > 0) Grant(p, count);
            Reply(client, packet, FishingCatchPackage.Result, count);
        }
        private bool Reachable(ComponentPlayer p, Vector3 target, Point3 cell)
        {
            Vector3 origin = p.ComponentCreatureModel.EyePosition, direction = target - origin;
            float distance = direction.Length();
            if (distance > FishingRules.MaxCastDistance + .75f || distance < .01f) return false;
            FishingLanding hit;
            return FishingCastResolver.Trace(Point(origin), Point(direction), Math.Min(distance + .02f, FishingRules.MaxCatchDistance), m_readCell, out hit) == FishingCastFailure.None &&
                hit.X == cell.X && hit.Y == cell.Y && hit.Z == cell.Z;
        }
        private void HandleReply(FishingCatchPackage packet)
        {
            Session s;
            if (!m_sessions.TryGetValue(packet.Caster, out s) || s.Sequence != packet.Sequence || !s.Cell.Equals(packet.Cell) || !Local(s.Player)) return;
            if (packet.Kind == FishingCatchPackage.Started && s.AwaitingStart)
            {
                if (packet.Count > 150) return;
                s.Round = new FishingRound(s.Sequence, Now, FishingRules.MinWaitDuration + packet.Count / 50f);
                s.AwaitingStart = false;
            }
            else if (packet.Kind == FishingCatchPackage.Result && (s.AwaitingResult || s.AwaitingStart))
            {
                if (packet.Count > 3) return;
                Message(s.Player, packet.Count == 0 ? "未钓到" : "钓到鱼");
                if (packet.Count > 0) Splash(s.Bobber);
                End(s, packet.Count == 0); m_sessions.Remove(packet.Caster);
            }
        }
        private void Send(Session s, byte kind) => CommonLib.Net.QueuePackage(new FishingCatchPackage {
            Kind = kind, Caster = s.Player.Entity.EntityId, Cell = s.Cell, Sequence = s.Sequence });
        private static void Reply(Client client, FishingCatchPackage p, byte kind, byte count) => CommonLib.Net.QueuePackage(new FishingCatchPackage {
            Kind = kind, To = client, Caster = p.Caster, Cell = p.Cell, Sequence = p.Sequence, Count = count });
        private void End(Session s, bool failed)
        {
            m_nextCast[s.Player.Entity.EntityId] = Now + (failed ? FishingRules.FailCooldown : .35f);
            if (!Authority && !s.AwaitingResult) Send(s, FishingCatchPackage.Cancel);
        }
        private FishingRateLimiter Limiter(ushort id)
        { FishingRateLimiter l; if (!m_limiters.TryGetValue(id, out l)) { l = new FishingRateLimiter(); m_limiters.Add(id, l); } return l; }
        private void Grant(ComponentPlayer p, int count)
        {
            int value = Terrain.MakeBlockValue(RawFishBlock.Index);
            IInventory inventory = p.ComponentMiner.Inventory;
            // Fill capacity across slots, then drop only the remainder.
            for (int attempts = 0; inventory != null && count > 0 && attempts < inventory.SlotsCount; attempts++)
            {
                int slot = ComponentInventoryBase.FindAcquireSlotForItem(inventory, value);
                if (slot < 0) break;
                int add = Math.Min(count, inventory.GetSlotCapacity(slot, value) - inventory.GetSlotCount(slot));
                if (add <= 0) break;
                inventory.AddSlotItems(slot, value, add); count -= add;
            }
            if (count > 0) m_pickables.AddPickable(value, count, p.ComponentBody.Position + new Vector3(0f, .6f, 0f), new Vector3?(Vector3.UnitY * 1.5f), null);
        }
        private void Splash(Vector3 position) => m_particles.AddParticleSystem(new WaterSplashParticleSystem(m_terrain, position, false));
        private static void Message(ComponentPlayer p, string text, bool blink = false)
        { if (Local(p)) p.ComponentGui.DisplaySmallMessage(text, Color.White, blink, blink); }

        public void Draw(Camera camera, int order)
        {
            if (order != 106 || m_sessions.Count == 0) return;
            if (m_renderer == null) m_renderer = new PrimitivesRenderer3D();
            FlatBatch3D batch = m_renderer.FlatBatch(0, DepthStencilState.DepthRead, RasterizerState.CullNone, BlendState.AlphaBlend);
            int drawn = 0;
            foreach (Session s in m_sessions.Values)
            {
                if (s.AwaitingResult || Vector3.DistanceSquared(s.Bobber, camera.ViewPosition) > 4096 || !Equipped(s.Player)) continue;
                if (++drawn > 8) break;
                float age = s.Round == null ? 0f : (float)(Now - s.Round.Start);
                Vector3 center = s.Bobber + Vector3.UnitY * (s.Biting ? -.07f + .035f * MathUtils.Sin(age * 15) : .04f + .015f * MathUtils.Sin(age * 3));
                Vector3 top = center + Vector3.UnitY * .14f, bottom = center - Vector3.UnitY * .06f;
                for (int i = 0; i < 4; i++)
                {
                    float a = i * MathUtils.PI / 2, b = (i + 1) * MathUtils.PI / 2;
                    Vector3 u = center + new Vector3(MathUtils.Cos(a), 0, MathUtils.Sin(a)) * .055f;
                    Vector3 v = center + new Vector3(MathUtils.Cos(b), 0, MathUtils.Sin(b)) * .055f;
                    batch.QueueTriangle(top, u, v, new Color(224, 63, 41));
                    batch.QueueTriangle(bottom, v, u, new Color(246, 236, 208));
                }
                Matrix eye = Matrix.CreateFromQuaternion(s.Player.ComponentCreatureModel.EyeRotation);
                Vector3 start = s.Player.ComponentCreatureModel.EyePosition + eye.Forward * .6f + eye.Right * .18f - Vector3.UnitY * .28f;
                Vector3 previous = start;
                for (int i = 1; i <= 4; i++)
                { float t = i / 4f; Vector3 next = Vector3.Lerp(start, top, t) - Vector3.UnitY * (MathUtils.Sin(t * MathUtils.PI) * .14f); batch.QueueLine(previous, next, new Color(227, 223, 201, 190)); previous = next; }
                for (int i = 0; i < 12; i++)
                {
                    float a = i * MathUtils.PI / 6, b = (i + 1) * MathUtils.PI / 6, r = s.Biting ? .18f : .12f;
                    Vector3 surface = s.Bobber + Vector3.UnitY * .012f;
                    batch.QueueLine(surface + new Vector3(MathUtils.Cos(a) * r, 0, MathUtils.Sin(a) * r), surface + new Vector3(MathUtils.Cos(b) * r, 0, MathUtils.Sin(b) * r), new Color(193, 228, 234, 150));
                }
            }
            m_renderer.Flush(camera.ViewProjectionMatrix);
        }
    }
}

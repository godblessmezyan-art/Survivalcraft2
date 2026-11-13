#pragma warning disable CS1591
using System;
using System.Collections.Generic;
using Engine;
using Game;
using Game.NetWork;
using TemplatesDatabase;

namespace SCIEPort.Features.Fishing
{
    /// <summary>Fishing sessions, touch-driven reel timing and host-authoritative rewards.</summary>
    public sealed class SubsystemFishing : SubsystemBlockBehavior, IUpdateable
    {
        private sealed class Session
        {
            internal ComponentPlayer Player;
            internal Vector3 Bobber;
            internal Point3 Cell;
            internal float Elapsed;
            internal float Wait;
            internal bool Biting;
        }

        private readonly Dictionary<ushort, Session> m_sessions = new Dictionary<ushort, Session>();
        private readonly Dictionary<ushort, double> m_nextCast = new Dictionary<ushort, double>();
        private readonly Dictionary<ushort, FishingRateLimiter> m_limiters = new Dictionary<ushort, FishingRateLimiter>();
        private readonly List<ushort> m_remove = new List<ushort>();
        private readonly System.Random m_random = new System.Random();
        private SubsystemTerrain m_terrain;
        private SubsystemParticles m_particles;
        private SubsystemPickables m_pickables;
        private SubsystemTime m_time;
        private SubsystemPlayers m_players;
        private uint m_sequence;

        public override int[] HandledBlocks => new[] { FishingRodBlock.Index };
        public UpdateOrder UpdateOrder => UpdateOrder.Default;
        private bool Authority => CommonLib.WorkType != WorkType.Client;
        private double Now => m_time.GameTime;

        public override void Load(ValuesDictionary values)
        {
            base.Load(values);
            m_terrain = Project.FindSubsystem<SubsystemTerrain>(true);
            m_particles = Project.FindSubsystem<SubsystemParticles>(true);
            m_pickables = Project.FindSubsystem<SubsystemPickables>(true);
            m_time = Project.FindSubsystem<SubsystemTime>(true);
            m_players = Project.FindSubsystem<SubsystemPlayers>(true);
        }

        public override void Dispose()
        {
            m_sessions.Clear();
            m_nextCast.Clear();
            m_limiters.Clear();
            base.Dispose();
        }

        public override bool OnUse(Ray3 ray, ComponentMiner miner)
        {
            if (Terrain.ExtractContents(miner.ActiveBlockValue) != FishingRodBlock.Index) return false;
            ComponentPlayer player = miner.ComponentPlayer;
            if (player == null || player.PlayerData == null || !player.PlayerData.IsMainPlayer) return true;
            Session session;
            if (m_sessions.TryGetValue(player.Entity.EntityId, out session))
            {
                Reel(player, session);
            }
            else
            {
                Cast(player, ray);
            }
            return true;
        }

        public void Update(float dt)
        {
            if (m_sessions.Count == 0) return;
            m_remove.Clear();
            foreach (KeyValuePair<ushort, Session> pair in m_sessions)
            {
                ushort id = pair.Key;
                Session session = pair.Value;
                ComponentPlayer player = session.Player;
                if (player == null || !player.IsAddedToProject || player.ComponentHealth.Health <= 0f ||
                    Terrain.ExtractContents(player.ComponentMiner.ActiveBlockValue) != FishingRodBlock.Index ||
                    FishingRules.ShouldEndSession(Vector3.Distance(player.ComponentBody.Position, session.Bobber)))
                {
                    m_remove.Add(id);
                    continue;
                }
                session.Elapsed += dt;
                if (!session.Biting && session.Elapsed >= session.Wait)
                {
                    session.Biting = true;
                    Splash(session.Bobber);
                    Message(player, "咬钩", true);
                }
                if (session.Biting && session.Elapsed >= session.Wait + FishingRules.BiteWindow)
                {
                    m_remove.Add(id);
                    m_nextCast[id] = Now + FishingRules.FailCooldown;
                    Message(player, "鱼跑了", false);
                }
            }
            foreach (ushort id in m_remove) m_sessions.Remove(id);
        }

        private void Cast(ComponentPlayer player, Ray3 ray)
        {
            ushort id = player.Entity.EntityId;
            double next;
            if (m_nextCast.TryGetValue(id, out next) && Now < next) return;
            Vector3 direction = ray.Direction;
            float length = direction.Length();
            if (length < 1e-6f) return;
            direction /= length;
            Terrain terrain = m_terrain.Terrain;
            FluidBlock water = BlocksManager.Blocks[WaterBlock.Index] as FluidBlock;
            if (water == null) return;
            for (float distance = 0.25f; distance <= FishingRules.MaxCastDistance; distance += 0.25f)
            {
                Vector3 point = ray.Position + direction * distance;
                int x = (int)Math.Floor(point.X);
                int y = (int)Math.Floor(point.Y);
                int z = (int)Math.Floor(point.Z);
                if (!terrain.IsCellValid(x, y, z)) break;
                int value = terrain.GetCellValue(x, y, z);
                int contents = Terrain.ExtractContents(value);
                if (contents == 0) continue;
                if (contents != WaterBlock.Index || y <= 0) break;
                float surface = water.GetLevelHeight(value);
                if (surface < 0.5f) break;
                if (terrain.GetCellContents(x, y - 1, z) != WaterBlock.Index) break;
                Session session = new Session
                {
                    Player = player,
                    Cell = new Point3(x, y, z),
                    Bobber = new Vector3(x + 0.5f, y + surface, z + 0.5f),
                    Wait = FishingRules.WaitDuration(m_random)
                };
                m_sessions[id] = session;
                Splash(session.Bobber);
                return;
            }
            Message(player, "此处无鱼", false);
        }

        private void Reel(ComponentPlayer player, Session session)
        {
            ushort id = player.Entity.EntityId;
            m_sessions.Remove(id);
            FishingRules.ReelResult result = FishingRules.Reel(session.Elapsed, session.Wait, FishingRules.BiteWindow);
            if (result != FishingRules.ReelResult.Success)
            {
                m_nextCast[id] = Now + FishingRules.FailCooldown;
                Message(player, "鱼跑了", false);
                return;
            }
            if (Authority)
            {
                int count = FishingRules.RewardCount(m_random.Next(100));
                Grant(player, count);
                Message(player, "钓到鱼", false);
            }
            else
            {
                CommonLib.Net.QueuePackage(new FishingCatchPackage
                {
                    Kind = FishingCatchPackage.Request,
                    Caster = id,
                    Cell = session.Cell,
                    Sequence = ++m_sequence
                });
            }
        }

        internal void AcceptCatch(ComponentPlayer player, FishingCatchPackage packet, Client client)
        {
            if (!Authority || player == null || client == null) return;
            int count = 0;
            if (ValidateCatch(player, packet.Cell) && Limiter(player.Entity.EntityId).TryAccept(Now))
            {
                count = FishingRules.RewardCount(m_random.Next(100));
                Grant(player, count);
            }
            CommonLib.Net.QueuePackage(new FishingCatchPackage
            {
                Kind = FishingCatchPackage.Result,
                To = client,
                Cell = packet.Cell,
                Count = (byte)count
            });
        }

        internal void ReceiveCatch(FishingCatchPackage packet)
        {
            if (packet.Kind != FishingCatchPackage.Result || packet.Count == 0) return;
            Splash(new Vector3(packet.Cell.X + 0.5f, packet.Cell.Y + 0.5f, packet.Cell.Z + 0.5f));
            foreach (ComponentPlayer player in m_players.ComponentPlayers)
            {
                if (player.PlayerData == null || !player.PlayerData.IsMainPlayer) continue;
                Message(player, "钓到鱼", false);
                return;
            }
        }

        private bool ValidateCatch(ComponentPlayer player, Point3 cell)
        {
            if (!player.IsAddedToProject || player.ComponentHealth.Health <= 0f) return false;
            if (Terrain.ExtractContents(player.ComponentMiner.ActiveBlockValue) != FishingRodBlock.Index) return false;
            Terrain terrain = m_terrain.Terrain;
            if (!terrain.IsCellValid(cell.X, cell.Y, cell.Z)) return false;
            if (terrain.GetCellContents(cell.X, cell.Y, cell.Z) != WaterBlock.Index) return false;
            Vector3 center = new Vector3(cell.X + 0.5f, cell.Y + 0.5f, cell.Z + 0.5f);
            return Vector3.Distance(player.ComponentBody.Position + new Vector3(0f, 1f, 0f), center) <= FishingRules.MaxCatchDistance;
        }

        private void Grant(ComponentPlayer player, int count)
        {
            int value = Terrain.MakeBlockValue(RawFishBlock.Index);
            IInventory inventory = player.ComponentMiner.Inventory;
            if (inventory != null)
            {
                int slot = ComponentInventoryBase.FindAcquireSlotForItem(inventory, value);
                if (slot >= 0)
                {
                    inventory.AddSlotItems(slot, value, count);
                    return;
                }
            }
            Vector3 position = player.ComponentBody.Position + new Vector3(0f, 0.6f, 0f);
            m_pickables.AddPickable(value, count, position, new Vector3?(new Vector3(0f, 1.5f, 0f)), null);
        }

        private FishingRateLimiter Limiter(ushort id)
        {
            FishingRateLimiter limiter;
            if (!m_limiters.TryGetValue(id, out limiter))
            {
                limiter = new FishingRateLimiter();
                m_limiters.Add(id, limiter);
            }
            return limiter;
        }

        private void Splash(Vector3 position)
        {
            m_particles.AddParticleSystem(new WaterSplashParticleSystem(m_terrain, position, false));
        }

        private static void Message(ComponentPlayer player, string text, bool blink)
        {
            player.ComponentGui.DisplaySmallMessage(text, Color.White, blink, true);
        }
    }
}

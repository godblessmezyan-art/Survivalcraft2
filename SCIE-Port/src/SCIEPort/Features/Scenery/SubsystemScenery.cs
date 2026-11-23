#pragma warning disable CS1591

using System.Collections.Generic;
using Engine;
using Game;
using Game.NetWork;
using GameEntitySystem;
using SCIEPort.Features.Pyramid;
using SCIEPort.Features.Shrines;
using SCIEPort.Features.Suspension;
using TemplatesDatabase;

namespace SCIEPort.Features.Scenery
{
    /// <summary>
    /// Stocks the single artifact carried by every airship and sky island. The display
    /// block is written during terrain generation; the prize is attached afterwards,
    /// when its chunk is initialized, and only by the host. Unstocked prizes survive
    /// save/reload so a crash between generation and stocking cannot lose the reward.
    /// </summary>
    public sealed class SubsystemScenery : Subsystem, IUpdateable
    {
        private sealed class Pending
        {
            public Point3 Point;
            public int Reward;
            public double NextAttempt;
        }

        private readonly Dictionary<Point3, Pending> m_pending = new Dictionary<Point3, Pending>();
        private SubsystemTerrain m_terrain;
        private SubsystemBlockEntities m_entities;
        private SubsystemTime m_time;

        public UpdateOrder UpdateOrder => UpdateOrder.Default;

        public override void Load(ValuesDictionary valuesDictionary)
        {
            base.Load(valuesDictionary);
            m_terrain = Project.FindSubsystem<SubsystemTerrain>(true);
            m_entities = Project.FindSubsystem<SubsystemBlockEntities>(true);
            m_time = Project.FindSubsystem<SubsystemTime>(true);
            m_pending.Clear();
            var rows = valuesDictionary.GetValue<ValuesDictionary>("Rewards", null);
            if (rows == null) return;
            foreach (object entry in rows.Values)
            {
                var row = entry as ValuesDictionary;
                if (row == null) continue;
                int x = row.GetValue("X", 0);
                int y = row.GetValue("Y", 0);
                int z = row.GetValue("Z", 0);
                int reward = row.GetValue("Reward", 0);
                if (y < 2 || y > 253 || !PyramidLoot.IsEligible(reward)) continue;
                var point = new Point3(x, y, z);
                m_pending[point] = new Pending { Point = point, Reward = reward, NextAttempt = m_time.GameTime };
            }
        }

        public override void Save(ValuesDictionary valuesDictionary)
        {
            var rows = new ValuesDictionary();
            int index = 0;
            foreach (Pending item in m_pending.Values)
            {
                var row = new ValuesDictionary();
                row.SetValue("X", item.Point.X);
                row.SetValue("Y", item.Point.Y);
                row.SetValue("Z", item.Point.Z);
                row.SetValue("Reward", item.Reward);
                rows.SetValue("Reward" + index++, row);
            }
            valuesDictionary.SetValue("Rewards", rows);
        }

        /// <summary>Records the prize of a freshly painted sky landmark. Safe to call per chunk.</summary>
        internal void Note(ScenerySite site)
        {
            if (CommonLib.WorkType == WorkType.Client) return;
            if (site.Kind != SceneryKind.Airship && site.Kind != SceneryKind.SkyIsland) return;
            var point = new Point3(site.DisplayX, site.DisplayY, site.DisplayZ);
            if (m_pending.ContainsKey(point)) return;
            m_pending[point] = new Pending
            {
                Point = point,
                Reward = PyramidLoot.Draw(new Game.Random(site.Seed)),
                NextAttempt = m_time.GameTime
            };
        }

        public void Update(float dt)
        {
            if (CommonLib.WorkType == WorkType.Client || m_pending.Count == 0) return;
            double now = m_time.GameTime;
            List<Point3> finished = null;
            foreach (Pending item in m_pending.Values)
            {
                if (item.NextAttempt > now) continue;
                item.NextAttempt = now + 0.5;
                if (!ShrineWorld.Ready(m_terrain, item.Point)) continue;
                Point3 point = item.Point;
                int contents = Terrain.ExtractContents(
                    m_terrain.Terrain.GetCellValue(point.X, point.Y, point.Z));
                if (contents != SuspendedItemBlock.Index)
                {
                    // The player already took the prize, or the display was destroyed.
                    (finished ?? (finished = new List<Point3>())).Add(point);
                    continue;
                }
                if (!PyramidLoot.IsRegistered(item.Reward)) continue;
                if (!ShrineWorld.TryStock(m_terrain, m_entities, point,
                        Terrain.MakeBlockValue(SuspendedItemBlock.Index),
                        Terrain.MakeBlockValue(item.Reward)))
                {
                    continue;
                }
                (finished ?? (finished = new List<Point3>())).Add(point);
            }
            if (finished == null) return;
            for (int i = 0; i < finished.Count; i++) m_pending.Remove(finished[i]);
        }
    }
}

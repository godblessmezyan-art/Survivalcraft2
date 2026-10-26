using System;
using System.Collections.Generic;
using Engine;
using Game;
using Game.NetWork;
using GameEntitySystem;
using TemplatesDatabase;

namespace SCIEPort.Features.PortableFortress
{
    /// <summary>One live fortress instance and its proxies.</summary>
    public sealed class FortressInstance
    {
        /// <summary>Anchor component.</summary>
        public ComponentPortableFortress Anchor { get; set; }

        /// <summary>Anchor body; also the frame the model is drawn from.</summary>
        public ComponentBody AnchorBody { get; set; }

        /// <summary>Proxy components by segment index.</summary>
        public Dictionary<int, ComponentPortableFortressSegment> Segments { get; } =
            new Dictionary<int, ComponentPortableFortressSegment>();

        /// <summary>Record shortcut.</summary>
        public FortressRecord Record => Anchor?.Record;
    }

    /// <summary>
    /// Owns every deployed Portable Fortress: batched construction, proxy maintenance,
    /// save/load reconciliation and the bounded stair/hatch rise assist.
    /// </summary>
    public sealed class SubsystemPortableFortress : Subsystem, IUpdateable
    {
        /// <summary>Anchor entity template name.</summary>
        public const string AnchorTemplateName = "SCIEPortFortress";

        /// <summary>Collision proxy template name.</summary>
        public const string SegmentTemplateName = "SCIEPortFortressSegment";

        private const float ProxyMass = 100000f;

        private readonly Dictionary<string, FortressInstance> m_instances = new Dictionary<string, FortressInstance>();
        private readonly List<DeploymentJob> m_jobs = new List<DeploymentJob>();
        private readonly List<(int X, int Z)> m_thresholdScratch = new List<(int X, int Z)>();

        private SubsystemTerrain m_terrain;
        private SubsystemBodies m_bodies;
        private SubsystemPlayers m_players;
        private SubsystemTime m_time;

        /// <summary>Update before bodies integrate so the rise assist is not overwritten.</summary>
        public UpdateOrder UpdateOrder => UpdateOrder.Locomotion;

        /// <summary>Live instance count.</summary>
        public int InstanceCount => m_instances.Count;

        /// <inheritdoc />
        public override void Load(ValuesDictionary valuesDictionary)
        {
            base.Load(valuesDictionary);
            m_terrain = Project.FindSubsystem<SubsystemTerrain>(throwOnError: true);
            m_bodies = Project.FindSubsystem<SubsystemBodies>(throwOnError: true);
            m_players = Project.FindSubsystem<SubsystemPlayers>(throwOnError: true);
            m_time = Project.FindSubsystem<SubsystemTime>(throwOnError: true);
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            m_instances.Clear();
            m_jobs.Clear();
            base.Dispose();
        }

        /// <summary>Registers an anchor component discovered during load.</summary>
        public void Attach(ComponentPortableFortress anchor)
        {
            if (anchor?.Record == null || string.IsNullOrEmpty(anchor.Record.Id))
            {
                return;
            }

            if (!m_instances.TryGetValue(anchor.Record.Id, out FortressInstance instance))
            {
                instance = new FortressInstance();
                m_instances.Add(anchor.Record.Id, instance);
            }

            instance.Anchor = anchor;
            instance.AnchorBody = anchor.Entity.FindComponent<ComponentBody>(throwOnError: false);
        }

        /// <summary>Registers a proxy component discovered during load.</summary>
        public void AttachSegment(ComponentPortableFortressSegment segment)
        {
            if (segment == null || string.IsNullOrEmpty(segment.FortressId))
            {
                return;
            }

            if (!m_instances.TryGetValue(segment.FortressId, out FortressInstance instance))
            {
                instance = new FortressInstance();
                m_instances.Add(segment.FortressId, instance);
            }

            if (segment.SegmentIndex >= 0 && segment.SegmentIndex < PortableFortressGeometry.Segments.Count)
            {
                instance.Segments[segment.SegmentIndex] = segment;
            }
        }

        /// <summary>Finds an instance by its control core cell.</summary>
        public FortressInstance FindByCoreCell(int x, int y, int z)
        {
            foreach (FortressInstance instance in m_instances.Values)
            {
                FortressRecord record = instance.Record;
                if (record != null && record.CoreX == x && record.CoreY == y && record.CoreZ == z)
                {
                    return instance;
                }
            }

            return null;
        }

        /// <summary>Finds an instance by id.</summary>
        public FortressInstance Find(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            m_instances.TryGetValue(id, out FortressInstance instance);
            return instance;
        }

        /// <summary>True when a deployment for the same core cell is still running.</summary>
        public bool IsDeployingAt(int x, int y, int z)
        {
            foreach (DeploymentJob job in m_jobs)
            {
                if (job.Record.CoreX == x && job.Record.CoreY == y && job.Record.CoreZ == z)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>True when a deployment is in flight for any instance.</summary>
        public bool HasPendingDeployment => m_jobs.Count > 0;

        /// <summary>
        /// Starts a deployment. The terrain journal is captured here, before anything is
        /// written, so a failure can restore the exact previous cells.
        /// </summary>
        public bool TryBeginDeploy(FortressRecord record, ComponentMiner miner)
        {
            if (record == null || !record.IsValid())
            {
                return false;
            }

            if (m_instances.ContainsKey(record.Id) || IsDeployingAt(record.CoreX, record.CoreY, record.CoreZ))
            {
                return false;
            }

            var job = new DeploymentJob(record, miner);
            m_jobs.Add(job);
            return true;
        }

        /// <inheritdoc />
        public void Update(float dt)
        {
            if (m_terrain == null)
            {
                return;
            }

            PumpJobs(dt);
            SyncProxies();
            ApplyRiseAssist(dt);
        }

        private void PumpJobs(float dt)
        {
            if (m_jobs.Count == 0)
            {
                return;
            }

            if (CommonLib.WorkType == WorkType.Client)
            {
                m_jobs.Clear();
                return;
            }

            for (int index = m_jobs.Count - 1; index >= 0; index--)
            {
                DeploymentJob job = m_jobs[index];
                try
                {
                    if (!AdvanceJob(job))
                    {
                        continue;
                    }

                    m_jobs.RemoveAt(index);
                }
                catch (Exception exception)
                {
                    Log.Error("[SCIEPort] Portable fortress deployment failed: " + exception);
                    Rollback(job);
                    job.Notify("堡垒展开失败，已回滚到部署前状态。");
                    m_jobs.RemoveAt(index);
                }
            }
        }

        /// <summary>Advances one job. Returns true when the job is finished.</summary>
        private bool AdvanceJob(DeploymentJob job)
        {
            if (job.Stage == DeploymentStage.Prepare)
            {
                if (!CaptureAndWriteTerrain(job))
                {
                    job.Notify("堡垒展开失败：地形写入被拒绝，未消耗道具。");
                    return true;
                }

                if (!SpawnAnchor(job))
                {
                    Rollback(job);
                    job.Notify("堡垒展开失败：控制核心无法建立，未消耗道具。");
                    return true;
                }

                job.Stage = DeploymentStage.Proxies;
                return false;
            }

            if (job.Stage == DeploymentStage.Proxies)
            {
                int created = 0;
                while (job.NextSegment < PortableFortressGeometry.Segments.Count
                    && created < FortressTransactionLimits.SegmentsPerTick)
                {
                    SpawnProxy(job, job.NextSegment);
                    job.NextSegment++;
                    created++;
                }

                if (job.NextSegment < PortableFortressGeometry.Segments.Count)
                {
                    return false;
                }

                job.Stage = DeploymentStage.Settle;
                return false;
            }

            if (job.Record.State == FortressState.Deploying)
            {
                job.Record.State = FortressState.Deployed;
                job.AnchorComponent?.SetState(FortressState.Deployed);
            }

            job.ConsumeItem();
            job.Notify("堡垒已展开：11×13×9，控制核心在门口，回收接口已预留。");
            return true;
        }

        private bool CaptureAndWriteTerrain(DeploymentJob job)
        {
            FortressRecord record = job.Record;
            int coreValue = Terrain.MakeBlockValue(
                PortableFortressCoreBlock.Index,
                0,
                PortableFortressCoreBlock.SetRotation(0, (int)record.Facing));
            int thresholdValue = Terrain.MakeBlockValue(PortableFortressThresholdBlock.Index);

            FortressDeploymentRules.ThresholdCellList(record.AnchorX, record.AnchorZ, record.Facing, m_thresholdScratch);
            foreach ((int x, int z) in m_thresholdScratch)
            {
                if (job.CellWrites.Count >= FortressTransactionLimits.MaxTerrainCellsPerDeployment)
                {
                    return false;
                }

                int previous = m_terrain.Terrain.GetCellValue(x, record.BaseY, z);
                m_terrain.ChangeCell(x, record.BaseY, z, thresholdValue);
                job.CellWrites.Add(new FortressCellWrite(x, record.BaseY, z, previous, thresholdValue));
            }

            int previousCore = m_terrain.Terrain.GetCellValue(record.CoreX, record.CoreY, record.CoreZ);
            m_terrain.ChangeCell(record.CoreX, record.CoreY, record.CoreZ, coreValue);
            job.CellWrites.Add(new FortressCellWrite(record.CoreX, record.CoreY, record.CoreZ, previousCore, coreValue));
            return true;
        }

        private bool SpawnAnchor(DeploymentJob job)
        {
            FortressRecord record = job.Record;
            Entity entity = DatabaseManager.CreateEntity(Project, AnchorTemplateName, throwIfNotFound: false);
            if (entity == null)
            {
                return false;
            }

            ComponentPortableFortress component = entity.FindComponent<ComponentPortableFortress>(throwOnError: false);
            ComponentBody body = entity.FindComponent<ComponentBody>(throwOnError: false);
            if (component == null || body == null)
            {
                return false;
            }

            component.Initialize(record);
            body.Position = new Vector3(record.AnchorX + 0.5f, record.BaseY, record.AnchorZ + 0.5f);
            body.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, PortableFortressGeometry.Yaw(record.Facing));
            body.BoxSize = new Vector3(0.2f, 0.2f, 0.2f);
            body.Mass = ProxyMass;
            body.IsGravityEnabled = false;
            body.Velocity = Vector3.Zero;
            Project.AddEntity(entity);

            job.AnchorEntity = entity;
            job.AnchorComponent = component;
            Attach(component);
            return true;
        }

        private void SpawnProxy(DeploymentJob job, int index)
        {
            FortressRecord record = job.Record;
            FortressSegment definition = PortableFortressGeometry.Segments[index];
            Entity entity = DatabaseManager.CreateEntity(Project, SegmentTemplateName, throwIfNotFound: true);
            var component = entity.FindComponent<ComponentPortableFortressSegment>(throwOnError: true);
            var body = entity.FindComponent<ComponentBody>(throwOnError: true);
            component.Initialize(record.Id, index);
            ApplySegmentTransform(record, definition, body);
            Project.AddEntity(entity);
            job.SegmentEntities.Add(entity);

            FortressInstance instance = Find(record.Id);
            if (instance != null)
            {
                instance.Segments[index] = component;
            }
        }

        private static void ApplySegmentTransform(FortressRecord record, FortressSegment definition, ComponentBody body)
        {
            FortressBox box = definition.Box;
            PortableFortressGeometry.RotateOffset(box.CentreX, box.CentreZ, record.Facing, out float ox, out float oz);
            bool swap = record.Facing == FortressFacing.PositiveX || record.Facing == FortressFacing.NegativeX;
            body.BoxSize = swap
                ? new Vector3(box.SizeZ, box.SizeY, box.SizeX)
                : new Vector3(box.SizeX, box.SizeY, box.SizeZ);
            body.Position = new Vector3(
                record.AnchorX + 0.5f + ox,
                record.BaseY + box.CentreY,
                record.AnchorZ + 0.5f + oz);
            body.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, PortableFortressGeometry.Yaw(record.Facing));
            body.Mass = ProxyMass;
            body.IsGravityEnabled = false;
            body.Velocity = Vector3.Zero;
        }

        private void SyncProxies()
        {
            foreach (FortressInstance instance in m_instances.Values)
            {
                FortressRecord record = instance.Record;
                if (record == null || record.State != FortressState.Deployed)
                {
                    continue;
                }

                foreach (KeyValuePair<int, ComponentPortableFortressSegment> pair in instance.Segments)
                {
                    if (pair.Key < 0 || pair.Key >= PortableFortressGeometry.Segments.Count)
                    {
                        continue;
                    }

                    ComponentBody body = pair.Value?.Entity.FindComponent<ComponentBody>(throwOnError: false);
                    if (body == null)
                    {
                        continue;
                    }

                    FortressSegment definition = PortableFortressGeometry.Segments[pair.Key];
                    PortableFortressGeometry.RotateOffset(definition.Box.CentreX, definition.Box.CentreZ, record.Facing, out float ox, out float oz);
                    var expected = new Vector3(
                        record.AnchorX + 0.5f + ox,
                        record.BaseY + definition.Box.CentreY,
                        record.AnchorZ + 0.5f + oz);
                    if (Vector3.DistanceSquared(body.Position, expected) > 0.0004f)
                    {
                        ApplySegmentTransform(record, definition, body);
                    }
                }
            }
        }

        private void ApplyRiseAssist(float dt)
        {
            if (m_players == null)
            {
                return;
            }

            ReadOnlyList<ComponentPlayer> players = m_players.ComponentPlayers;
            for (int i = 0; i < players.Count; i++)
            {
                ComponentPlayer player = players[i];
                ComponentBody body = player?.ComponentBody;
                if (body == null || body.ParentBody != null || !player.PlayerData.IsMainPlayer)
                {
                    continue;
                }

                Vector3 position = body.Position;
                foreach (FortressInstance instance in m_instances.Values)
                {
                    FortressRecord record = instance.Record;
                    if (record == null || record.State != FortressState.Deployed)
                    {
                        continue;
                    }

                    FortressDeploymentRules.WorldToLocal(record, position.X, position.Z, out float localX, out float localZ);
                    if (!FortressTraversalRules.TryGetTargetHeight(localX, localZ, position.Y - record.BaseY, out float targetOffset))
                    {
                        continue;
                    }

                    float feetOffset = position.Y - record.BaseY;
                    if (!FortressTraversalRules.TryGetAssistedHeight(feetOffset, body.Velocity.Y, targetOffset, dt, out float newOffset))
                    {
                        break;
                    }

                    body.Position = new Vector3(position.X, record.BaseY + newOffset, position.Z);
                    break;
                }
            }
        }

        private void Rollback(DeploymentJob job)
        {
            for (int i = job.SegmentEntities.Count - 1; i >= 0; i--)
            {
                Entity entity = job.SegmentEntities[i];
                if (entity != null)
                {
                    Project.RemoveEntity(entity, disposeEntity: true);
                }
            }

            job.SegmentEntities.Clear();

            if (job.AnchorEntity != null)
            {
                if (job.AnchorComponent?.Record != null)
                {
                    m_instances.Remove(job.AnchorComponent.Record.Id);
                }

                Project.RemoveEntity(job.AnchorEntity, disposeEntity: true);
                job.AnchorEntity = null;
            }

            for (int i = job.CellWrites.Count - 1; i >= 0; i--)
            {
                FortressCellWrite write = job.CellWrites[i];
                m_terrain.ChangeCell(write.X, write.Y, write.Z, write.PreviousValue);
            }

            job.CellWrites.Clear();
            job.Record.State = FortressState.Faulted;
        }

        private enum DeploymentStage
        {
            Prepare = 0,
            Proxies = 1,
            Settle = 2
        }

        private sealed class DeploymentJob
        {
            private readonly ComponentMiner m_miner;

            public DeploymentJob(FortressRecord record, ComponentMiner miner)
            {
                Record = record;
                m_miner = miner;
            }

            public FortressRecord Record { get; }

            public DeploymentStage Stage { get; set; }

            public int NextSegment { get; set; }

            public Entity AnchorEntity { get; set; }

            public ComponentPortableFortress AnchorComponent { get; set; }

            public List<Entity> SegmentEntities { get; } = new List<Entity>();

            public List<FortressCellWrite> CellWrites { get; } = new List<FortressCellWrite>();

            public void ConsumeItem()
            {
                m_miner?.RemoveActiveTool(1);
            }

            public void Notify(string message)
            {
                m_miner?.ComponentPlayer?.ComponentGui.DisplaySmallMessage(
                    message,
                    Color.White,
                    blinking: false,
                    playNotificationSound: false);
            }
        }
    }
}

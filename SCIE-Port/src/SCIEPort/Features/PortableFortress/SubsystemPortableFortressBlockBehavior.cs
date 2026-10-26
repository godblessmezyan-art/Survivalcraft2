using System;
using Engine;
using Game;
using Game.NetWork;
using SCIEPort.Features.PortableFortress;

namespace SCIEPort.Features.PortableFortress
{
    /// <summary>
    /// Handles the fortress deploy item and the deployed control core. Deployment runs the
    /// full preflight first and never consumes the item when anything is wrong.
    /// </summary>
    public sealed class SubsystemPortableFortressBlockBehavior : SubsystemBlockBehavior
    {
        private SubsystemPortableFortress m_fortresses;
        private SubsystemTerrain m_terrain;
        private SubsystemBodies m_bodies;

        /// <inheritdoc />
        public override int[] HandledBlocks => new[]
        {
            PortableFortressCoreItemBlock.Index,
            PortableFortressCoreBlock.Index
        };

        /// <inheritdoc />
        public override void Load(TemplatesDatabase.ValuesDictionary valuesDictionary)
        {
            base.Load(valuesDictionary);
            m_fortresses = Project.FindSubsystem<SubsystemPortableFortress>(throwOnError: true);
            m_terrain = Project.FindSubsystem<SubsystemTerrain>(throwOnError: true);
            m_bodies = Project.FindSubsystem<SubsystemBodies>(throwOnError: true);
        }

        /// <inheritdoc />
        public override bool OnUse(Ray3 ray, ComponentMiner componentMiner)
        {
            int activeContents = Terrain.ExtractContents(componentMiner.ActiveBlockValue);
            if (activeContents != PortableFortressCoreItemBlock.Index
                && activeContents != PortableFortressCoreBlock.Index)
            {
                return false;
            }

            TerrainRaycastResult? hit = componentMiner.Raycast<TerrainRaycastResult>(ray, RaycastMode.Digging);
            if (!hit.HasValue)
            {
                return true;
            }

            if (Terrain.ExtractContents(hit.Value.Value) == PortableFortressCoreBlock.Index)
            {
                ReportCore(componentMiner, hit.Value.CellFace.X, hit.Value.CellFace.Y, hit.Value.CellFace.Z);
                return true;
            }

            if (activeContents == PortableFortressCoreItemBlock.Index)
            {
                return TryDeploy(componentMiner, hit.Value);
            }

            return true;
        }

        private void ReportCore(ComponentMiner miner, int x, int y, int z)
        {
            FortressInstance instance = m_fortresses.FindByCoreCell(x, y, z);
            FortressRecoveryStatus status = FortressRecoveryRules.Evaluate(
                instance?.Record,
                m_fortresses.HasPendingDeployment);
            string message;
            switch (status)
            {
                case FortressRecoveryStatus.Busy:
                    message = "堡垒仍在展开，回收接口已锁。";
                    break;
                case FortressRecoveryStatus.Faulted:
                    message = "堡垒实例处于故障状态，回收已锁定。";
                    break;
                case FortressRecoveryStatus.Unknown:
                    message = "未找到对应的堡垒实例。";
                    break;
                default:
                    message = "控制核心：回收接口已预留，本轮不执行拆除（避免不安全回收丢失物资）。";
                    break;
            }

            Show(miner, message);
        }

        private bool TryDeploy(ComponentMiner miner, TerrainRaycastResult hit)
        {
            if (CommonLib.WorkType == WorkType.Client)
            {
                Show(miner, "只有单人模式或联机房主可以展开堡垒。");
                return true;
            }

            if (m_fortresses.HasPendingDeployment)
            {
                Show(miner, "上一个堡垒仍在展开，请稍候。");
                return true;
            }

            if (hit.CellFace.Face != 4)
            {
                Show(miner, "请对准方块顶面使用堡垒展开核心。");
                return true;
            }

            int clickX = hit.CellFace.X;
            int clickZ = hit.CellFace.Z;
            int baseY = hit.CellFace.Y + 1;

            Vector3 look = Matrix.CreateFromQuaternion(
                miner.ComponentCreature.ComponentCreatureModel.EyeRotation).Forward;
            FortressFacing facing = PortableFortressGeometry.FacingFromLook(look.X, look.Z);
            FortressDeploymentRules.AnchorCell(clickX, clickZ, facing, out int anchorX, out int anchorZ);

            bool duplicate = m_fortresses.FindByCoreCell(clickX, baseY, clickZ) != null;
            var site = new TerrainSite(m_terrain);
            bool bodyInTheWay = AnyBodyInTheWay(anchorX, anchorZ, baseY, facing, miner);

            FortressDeployResult result = FortressDeploymentRules.Evaluate(
                anchorX,
                baseY,
                anchorZ,
                facing,
                site,
                modelAvailable: true,
                bodyInTheWay: bodyInTheWay,
                duplicateInstance: duplicate);
            if (!result.IsReady)
            {
                Show(miner, "无法展开：" + result.Describe() + "。请清理该区域后重试，道具未消耗。");
                return true;
            }

            var record = new FortressRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                Facing = facing,
                BaseY = baseY,
                AnchorX = anchorX,
                AnchorZ = anchorZ,
                CoreX = clickX,
                CoreY = baseY,
                CoreZ = clickZ,
                Revision = 1,
                State = FortressState.Deploying
            };

            if (!m_fortresses.TryBeginDeploy(record, miner))
            {
                Show(miner, "无法展开：该位置已有堡垒或展开任务。");
                return true;
            }

            Show(miner, "正在展开堡垒…");
            return true;
        }

        private bool AnyBodyInTheWay(int anchorX, int anchorZ, int baseY, FortressFacing facing, ComponentMiner miner)
        {
            var bodies = new DynamicArray<ComponentBody>();
            var centre = new Vector2(anchorX + 0.5f, anchorZ + 0.5f);
            m_bodies.FindBodiesInArea(centre - new Vector2(12f), centre + new Vector2(12f), bodies);
            for (int i = 0; i < bodies.Count; i++)
            {
                ComponentBody body = bodies.Array[i];
                if (body == null)
                {
                    continue;
                }

                Vector3 position = body.Position;
                float halfWidth = MathUtils.Max(body.BoxSize.X, body.BoxSize.Z) * 0.5f;
                if (FortressDeploymentRules.BodyOverlaps(
                        anchorX,
                        baseY,
                        anchorZ,
                        facing,
                        position.X,
                        position.Y,
                        position.Z,
                        halfWidth,
                        body.BoxSize.Y,
                        margin: 0.25f))
                {
                    return true;
                }
            }

            return false;
        }

        private static void Show(ComponentMiner miner, string message)
        {
            miner.ComponentPlayer?.ComponentGui.DisplaySmallMessage(
                message,
                Color.White,
                blinking: false,
                playNotificationSound: false);
        }

        private sealed class TerrainSite : IFortressSite
        {
            private readonly SubsystemTerrain m_subsystemTerrain;

            public TerrainSite(SubsystemTerrain subsystemTerrain)
            {
                m_subsystemTerrain = subsystemTerrain;
            }

            public bool IsChunkReady(int x, int y, int z)
            {
                TerrainChunk chunk = m_subsystemTerrain.Terrain.GetChunkAtCell(x, z);
                return chunk != null && chunk.State >= TerrainChunkState.InvalidLight;
            }

            public int GetContents(int x, int y, int z)
            {
                return Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValue(x, y, z));
            }

            public bool IsSolid(int x, int y, int z)
            {
                int contents = GetContents(x, y, z);
                if (contents == 0)
                {
                    return false;
                }

                Block block = BlocksManager.Blocks[contents];
                return block != null && block.IsCollidable;
            }
        }
    }
}

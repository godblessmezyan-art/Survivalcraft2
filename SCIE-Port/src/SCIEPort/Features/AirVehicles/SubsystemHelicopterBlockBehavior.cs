using System.Linq;
using Engine;
using Game;
using Game.NetWork;
using SCIEPort.Features.Vehicles;
using TemplatesDatabase;

namespace SCIEPort.Features.AirVehicles
{
    /// <summary>Deploys bounded helicopter entities without changing terrain cells.</summary>
    public sealed class SubsystemHelicopterBlockBehavior : SubsystemBlockBehavior
    {
        private const int NearbyLimit = 2;
        private SubsystemBodies m_bodies;
        private SubsystemCreatureSpawn m_spawn;

        /// <inheritdoc />
        public override int[] HandledBlocks => new[] { HelicopterBlock.Index };

        /// <inheritdoc />
        public override bool OnUse(Ray3 ray, ComponentMiner miner)
        {
            if (Terrain.ExtractContents(miner.ActiveBlockValue) != HelicopterBlock.Index) return false;
            if (CommonLib.WorkType == WorkType.Client) return true;
            if (!VehicleActivationService.EnsureActivated()) return true;

            TerrainRaycastResult? hit = miner.Raycast<TerrainRaycastResult>(ray, RaycastMode.Digging);
            if (!hit.HasValue) return true;

            Vector3 position = new Vector3(hit.Value.CellFace.Point) + new Vector3(0.5f, 1.15f, 0.5f);
            DynamicArray<ComponentBody> bodies = new DynamicArray<ComponentBody>();
            m_bodies.FindBodiesInArea(position.XZ - new Vector2(20f), position.XZ + new Vector2(20f), bodies);
            if (bodies.Count(body => body.Entity.ValuesDictionary.DatabaseObject.Name == "SCIEPortHelicopter") >= NearbyLimit)
            {
                miner.ComponentPlayer?.ComponentGui.DisplaySmallMessage(
                    "附近的 SCIE 双人直升机已达到 2 架上限。",
                    Color.White,
                    blinking: true,
                    playNotificationSound: false);
                return true;
            }

            GameEntitySystem.Entity entity = m_spawn.SpawnCreature("SCIEPortHelicopter", position, constantSpawn: true);
            Vector3 forward = Matrix.CreateFromQuaternion(miner.ComponentCreature.ComponentCreatureModel.EyeRotation).Forward;
            forward.Y = 0f;
            if (forward.LengthSquared() > 0.0001f)
            {
                float yaw = MathUtils.Atan2(-forward.X, -forward.Z);
                entity.FindComponent<ComponentBody>(throwOnError: true).Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, yaw);
            }

            miner.RemoveActiveTool(1);
            return true;
        }

        /// <inheritdoc />
        public override void Load(ValuesDictionary valuesDictionary)
        {
            base.Load(valuesDictionary);
            m_bodies = Project.FindSubsystem<SubsystemBodies>(throwOnError: true);
            m_spawn = Project.FindSubsystem<SubsystemCreatureSpawn>(throwOnError: true);
        }
    }
}

using System.Linq;
using Engine;
using Game;
using TemplatesDatabase;

namespace SCIEPort.Features.LandVehicles
{
    /// <summary>Deploys ATVs on terrain without writing or replacing terrain cells.</summary>
    public sealed class SubsystemAtvBlockBehavior : SubsystemBlockBehavior
    {
        private const int NearbyLimit = 4;

        private SubsystemBodies m_bodies;
        private SubsystemCreatureSpawn m_spawn;

        /// <inheritdoc />
        public override int[] HandledBlocks => new[] { AtvBlock.Index };

        /// <inheritdoc />
        public override bool OnUse(Ray3 ray, ComponentMiner componentMiner)
        {
            if (Terrain.ExtractContents(componentMiner.ActiveBlockValue) != AtvBlock.Index)
            {
                return false;
            }

            TerrainRaycastResult? hit = componentMiner.Raycast<TerrainRaycastResult>(ray, RaycastMode.Digging);
            if (!hit.HasValue)
            {
                return true;
            }

            Vector3 position = new Vector3(hit.Value.CellFace.Point) + new Vector3(0.5f, 1.05f, 0.5f);
            DynamicArray<ComponentBody> bodies = new DynamicArray<ComponentBody>();
            m_bodies.FindBodiesInArea(position.XZ - new Vector2(12f), position.XZ + new Vector2(12f), bodies);
            int nearbyCount = bodies.Count(body => body.Entity.ValuesDictionary.DatabaseObject.Name == "SCIEPortATV");
            if (nearbyCount >= NearbyLimit)
            {
                componentMiner.ComponentPlayer?.ComponentGui.DisplaySmallMessage(
                    "附近的 SCIE 越野摩托已达到 4 辆上限。",
                    Color.White,
                    blinking: true,
                    playNotificationSound: false);
                return true;
            }

            GameEntitySystem.Entity entity = m_spawn.SpawnCreature("SCIEPortATV", position, constantSpawn: true);
            Vector3 forward = Matrix.CreateFromQuaternion(
                componentMiner.ComponentCreature.ComponentCreatureModel.EyeRotation).Forward;
            forward.Y = 0f;
            if (forward.LengthSquared() > 0.0001f)
            {
                // ComponentBody's yaw-zero forward vector is (0,0,-1), the
                // same convention used by the existing rail direction table.
                float yaw = MathUtils.Atan2(-forward.X, -forward.Z);
                entity.FindComponent<ComponentBody>(throwOnError: true).Rotation =
                    Quaternion.CreateFromAxisAngle(Vector3.UnitY, yaw);
            }

            componentMiner.RemoveActiveTool(1);
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

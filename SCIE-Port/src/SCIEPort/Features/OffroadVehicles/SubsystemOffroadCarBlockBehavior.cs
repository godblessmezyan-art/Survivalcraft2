using System.Linq;
using Engine;
using Game;
using Game.NetWork;
using TemplatesDatabase;

namespace SCIEPort.Features.OffroadVehicles
{
    /// <summary>Deploys off-road cars without writing terrain cells.</summary>
    public sealed class SubsystemOffroadCarBlockBehavior : SubsystemBlockBehavior
    {
        private SubsystemBodies m_bodies;
        private SubsystemCreatureSpawn m_spawn;

        /// <inheritdoc />
        public override int[] HandledBlocks => new[] { OffroadCarBlock.Index };

        /// <inheritdoc />
        public override bool OnUse(Ray3 ray, ComponentMiner componentMiner)
        {
            if (Terrain.ExtractContents(componentMiner.ActiveBlockValue) != OffroadCarBlock.Index)
            {
                return false;
            }

            // A normal client has no entity-spawn authority; it must not manufacture an
            // unsynchronized vehicle. Mirror the fox/rabbit/chicken spawner guard.
            if (CommonLib.WorkType == WorkType.Client)
            {
                return true;
            }

            TerrainRaycastResult? hit = componentMiner.Raycast<TerrainRaycastResult>(ray, RaycastMode.Digging);
            if (!hit.HasValue)
            {
                return true;
            }

            Vector3 position = new Vector3(hit.Value.CellFace.Point) + new Vector3(0.5f, 1.05f, 0.5f);
            DynamicArray<ComponentBody> bodies = new DynamicArray<ComponentBody>();
            m_bodies.FindBodiesInArea(position.XZ - new Vector2(14f), position.XZ + new Vector2(14f), bodies);
            if (bodies.Count(body => body.Entity.ValuesDictionary.DatabaseObject.Name == "SCIEPortOffroadCar") >= 4)
            {
                componentMiner.ComponentPlayer?.ComponentGui.DisplaySmallMessage(
                    "附近的 SCIE 双人越野车已达到 4 辆上限。", Color.White, blinking: true, playNotificationSound: false);
                return true;
            }

            GameEntitySystem.Entity entity = m_spawn.SpawnCreature("SCIEPortOffroadCar", position, constantSpawn: true);
            Vector3 forward = Matrix.CreateFromQuaternion(componentMiner.ComponentCreature.ComponentCreatureModel.EyeRotation).Forward;
            forward.Y = 0f;
            if (forward.LengthSquared() > 0.0001f)
            {
                float yaw = MathUtils.Atan2(-forward.X, -forward.Z);
                entity.FindComponent<ComponentBody>(throwOnError: true).Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, yaw);
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

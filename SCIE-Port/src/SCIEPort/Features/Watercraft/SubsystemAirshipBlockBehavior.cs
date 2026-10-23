using System.Linq;
using Engine;
using Game;
using Game.NetWork;
using TemplatesDatabase;

namespace SCIEPort.Features.Watercraft
{
    /// <summary>Deploys the independent SCIE airship entity from item 998.</summary>
    public sealed class SubsystemAirshipBlockBehavior : SubsystemBlockBehavior
    {
        private const int NearbyLimit = 2;

        private SubsystemBodies m_subsystemBodies;
        private SubsystemCreatureSpawn m_creatureSpawn;

        /// <inheritdoc />
        public override int[] HandledBlocks => new[] { AirshipBlock.Index };

        /// <inheritdoc />
        public override bool OnUse(Ray3 ray, ComponentMiner componentMiner)
        {
            if (Terrain.ExtractContents(componentMiner.ActiveBlockValue) != AirshipBlock.Index)
            {
                return false;
            }

            // A normal client has no entity-spawn authority; it must not manufacture an
            // unsynchronized airship. Mirror the fox/rabbit/chicken spawner guard.
            if (CommonLib.WorkType == WorkType.Client)
            {
                return true;
            }

            TerrainRaycastResult? hit = componentMiner.Raycast<TerrainRaycastResult>(
                ray,
                RaycastMode.Digging);
            if (!hit.HasValue)
            {
                return true;
            }

            Vector3 position = hit.Value.HitPoint() + 0.1f * Vector3.UnitY;
            DynamicArray<ComponentBody> bodies = new DynamicArray<ComponentBody>();
            m_subsystemBodies.FindBodiesInArea(
                new Vector2(position.X, position.Z) - new Vector2(16f),
                new Vector2(position.X, position.Z) + new Vector2(16f),
                bodies);

            int nearbyCount = bodies.Count(body =>
                body.Entity.ValuesDictionary.DatabaseObject.Name == "SCIEPortAirship");
            if (nearbyCount >= NearbyLimit)
            {
                componentMiner.ComponentPlayer?.ComponentGui.DisplaySmallMessage(
                    "附近的 SCIE 飞艇已达到 2 艘上限。",
                    Color.White,
                    blinking: true,
                    playNotificationSound: false);
                return true;
            }

            m_creatureSpawn.SpawnCreature("SCIEPortAirship", position, constantSpawn: true);
            componentMiner.RemoveActiveTool(1);
            return true;
        }

        /// <inheritdoc />
        public override void Load(ValuesDictionary valuesDictionary)
        {
            base.Load(valuesDictionary);
            m_subsystemBodies = Project.FindSubsystem<SubsystemBodies>(throwOnError: true);
            m_creatureSpawn = Project.FindSubsystem<SubsystemCreatureSpawn>(throwOnError: true);
        }
    }
}

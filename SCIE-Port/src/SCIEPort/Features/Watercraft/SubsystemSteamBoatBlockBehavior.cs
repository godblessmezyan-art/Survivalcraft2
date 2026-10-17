using System.Linq;
using Engine;
using Game;
using TemplatesDatabase;

namespace SCIEPort.Features.Watercraft
{
    /// <summary>Deploys the independent steam-boat entity from item 999.</summary>
    public sealed class SubsystemSteamBoatBlockBehavior : SubsystemBlockBehavior
    {
        private const int NearbyLimit = 4;

        private SubsystemBodies m_subsystemBodies;
        private SubsystemCreatureSpawn m_creatureSpawn;

        /// <inheritdoc />
        public override int[] HandledBlocks => new[] { SteamBoatBlock.Index };

        /// <inheritdoc />
        public override bool OnUse(Ray3 ray, ComponentMiner componentMiner)
        {
            if (Terrain.ExtractContents(componentMiner.ActiveBlockValue) != SteamBoatBlock.Index)
            {
                return false;
            }

            TerrainRaycastResult? hit = componentMiner.Raycast<TerrainRaycastResult>(
                ray,
                RaycastMode.Digging);
            if (!hit.HasValue)
            {
                return true;
            }

            Vector3 position = hit.Value.HitPoint();
            DynamicArray<ComponentBody> bodies = new DynamicArray<ComponentBody>();
            m_subsystemBodies.FindBodiesInArea(
                new Vector2(position.X, position.Z) - new Vector2(8f),
                new Vector2(position.X, position.Z) + new Vector2(8f),
                bodies);

            int nearbyCount = bodies.Count(body =>
                body.Entity.ValuesDictionary.DatabaseObject.Name == "SCIEPortSteamBoat");
            if (nearbyCount >= NearbyLimit)
            {
                componentMiner.ComponentPlayer?.ComponentGui.DisplaySmallMessage(
                    "附近的 SCIE 蒸汽船已达到 4 艘上限。",
                    Color.White,
                    blinking: true,
                    playNotificationSound: false);
                return true;
            }

            m_creatureSpawn.SpawnCreature("SCIEPortSteamBoat", position, constantSpawn: true);
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

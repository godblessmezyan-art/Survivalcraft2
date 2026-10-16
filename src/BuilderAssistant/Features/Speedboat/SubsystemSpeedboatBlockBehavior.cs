using System.Linq;
using Engine;
using Game;
using Game.NetWork;
using TemplatesDatabase;

namespace BuilderAssistant.Features.Speedboat
{
    /// <summary>Deploys the independent Speedboat entity from item 900.</summary>
    public sealed class SubsystemSpeedboatBlockBehavior : SubsystemBlockBehavior
    {
        private const int NearbySpeedboatLimit = 4;

        private SubsystemBodies m_subsystemBodies;
        private SubsystemCreatureSpawn m_creatureSpawn;

        /// <inheritdoc />
        public override int[] HandledBlocks => new[] { SpeedboatBlock.Index };

        /// <inheritdoc />
        public override bool OnUse(Ray3 ray, ComponentMiner componentMiner)
        {
            if (Terrain.ExtractContents(componentMiner.ActiveBlockValue) != SpeedboatBlock.Index)
            {
                return false;
            }

            if (CommonLib.WorkType != WorkType.Local)
            {
                componentMiner.ComponentPlayer?.ComponentGui.DisplaySmallMessage(
                    "[BuilderAssistant] Speedboat spawning is disabled in multiplayer until server authority is verified.",
                    Color.Yellow,
                    blinking: false,
                    playNotificationSound: false);
                return true;
            }

            TerrainRaycastResult? hit = componentMiner.Raycast<TerrainRaycastResult>(
                ray,
                RaycastMode.Digging);
            if (!hit.HasValue)
            {
                return true;
            }

            Vector3 position = hit.Value.HitPoint();
            DynamicArray<ComponentBody> nearbyBodies = new DynamicArray<ComponentBody>();
            m_subsystemBodies.FindBodiesInArea(
                new Vector2(position.X, position.Z) - new Vector2(8f),
                new Vector2(position.X, position.Z) + new Vector2(8f),
                nearbyBodies);

            int speedboatCount = nearbyBodies.Count(body =>
                body.Entity.ValuesDictionary.DatabaseObject.Name == "Speedboat");
            if (speedboatCount >= NearbySpeedboatLimit)
            {
                componentMiner.ComponentPlayer?.ComponentGui.DisplaySmallMessage(
                    "[BuilderAssistant] Too many speedboats nearby (limit: 4).",
                    Color.White,
                    blinking: true,
                    playNotificationSound: false);
                return true;
            }

            m_creatureSpawn.SpawnCreature("Speedboat", position, constantSpawn: true);
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

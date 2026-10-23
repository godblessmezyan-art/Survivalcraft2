using System.Linq;
using Engine;
using Game;
using Game.NetWork;
using TemplatesDatabase;

namespace SCIEPort.Features.Rail
{
    /// <summary>Deploys train and carriage entities only on SCIE rails.</summary>
    public sealed class SubsystemRailVehicleBlockBehavior : SubsystemBlockBehavior
    {
        private const int NearbyLimit = 8;
        private SubsystemBodies m_bodies;
        private SubsystemCreatureSpawn m_spawn;

        /// <inheritdoc />
        public override int[] HandledBlocks => new[] { TrainBlock.Index, CarriageBlock.Index };

        /// <inheritdoc />
        public override bool OnUse(Ray3 ray, ComponentMiner componentMiner)
        {
            // A normal client has no entity-spawn authority; it must not manufacture an
            // unsynchronized rail vehicle. Mirror the fox/rabbit/chicken spawner guard.
            if (CommonLib.WorkType == WorkType.Client)
            {
                return true;
            }

            int item = Terrain.ExtractContents(componentMiner.ActiveBlockValue);
            TerrainRaycastResult? hit = componentMiner.Raycast<TerrainRaycastResult>(ray, RaycastMode.Digging);
            if (!hit.HasValue) return true;
            if (Terrain.ExtractContents(hit.Value.Value) != RailBlock.Index)
            {
                componentMiner.ComponentPlayer?.ComponentGui.DisplaySmallMessage("载具只能放在 SCIE 铁轨上。", Color.White, true, false);
                return true;
            }

            Vector3 position = new Vector3(hit.Value.CellFace.Point) + new Vector3(.5f);
            DynamicArray<ComponentBody> bodies = new DynamicArray<ComponentBody>();
            m_bodies.FindBodiesInArea(position.XZ - new Vector2(10f), position.XZ + new Vector2(10f), bodies);
            int count = bodies.Count(b => b.Entity.FindComponent<ComponentRailVehicle>(false) != null);
            if (count >= NearbyLimit)
            {
                componentMiner.ComponentPlayer?.ComponentGui.DisplaySmallMessage("附近轨道载具已达到 8 辆上限。", Color.White, true, false);
                return true;
            }

            string entityName = item == TrainBlock.Index ? "SCIEPortTrain" : "SCIEPortCarriage";
            GameEntitySystem.Entity entity = m_spawn.SpawnCreature(entityName, position, true);
            Vector3 forward = Matrix.CreateFromQuaternion(componentMiner.ComponentCreature.ComponentCreatureModel.EyeRotation).Forward;
            int railType = RailBlock.GetRailType(Terrain.ExtractData(hit.Value.Value));
            int direction = RailBlock.IsDirectionX(railType) ? (forward.Z < 0f ? 0 : 2) : (forward.X < 0f ? 1 : 3);
            entity.FindComponent<ComponentRailVehicle>(true).SetDirection(direction);
            componentMiner.RemoveActiveTool(1);
            return true;
        }

        /// <inheritdoc />
        public override void Load(ValuesDictionary valuesDictionary)
        {
            base.Load(valuesDictionary);
            m_bodies = Project.FindSubsystem<SubsystemBodies>(true);
            m_spawn = Project.FindSubsystem<SubsystemCreatureSpawn>(true);
        }
    }
}

using Engine;
using Game;
using Game.NetWork;
using TemplatesDatabase;

namespace SCIEPort.Features.Creatures
{
    /// <summary>Creates a persistent fox only when the explicit creative item is used.</summary>
    public sealed class SubsystemFoxSpawnBlockBehavior : SubsystemBlockBehavior
    {
        private SubsystemCreatureSpawn m_creatureSpawn;

        /// <inheritdoc />
        public override int[] HandledBlocks => new[] { FoxSpawnBlock.Index };

        /// <inheritdoc />
        public override void Load(ValuesDictionary valuesDictionary)
        {
            base.Load(valuesDictionary);
            m_creatureSpawn = Project.FindSubsystem<SubsystemCreatureSpawn>(true);
            CreatureWorldSpawn.SetProject(Project);
        }

        /// <inheritdoc />
        public override bool OnUse(Ray3 ray, ComponentMiner miner)
        {
            if (Terrain.ExtractContents(miner.ActiveBlockValue) != FoxSpawnBlock.Index)
            {
                return false;
            }

            // Phase one deliberately has no client-side entity authority. A
            // normal client must not manufacture an unsynchronized creature.
            if (CommonLib.WorkType == WorkType.Client)
            {
                return true;
            }

            TerrainRaycastResult? hit = miner.Raycast<TerrainRaycastResult>(ray, RaycastMode.Digging);
            if (!hit.HasValue)
            {
                return true;
            }

            Vector3 position = new Vector3(hit.Value.CellFace.Point) + new Vector3(0.5f, 1.05f, 0.5f);
            GameEntitySystem.Entity entity = m_creatureSpawn.SpawnCreature("SCIEPortFox", position, true);
            if (entity == null)
            {
                return true;
            }

            Vector3 forward = Matrix.CreateFromQuaternion(miner.ComponentCreature.ComponentCreatureModel.EyeRotation).Forward;
            forward.Y = 0f;
            if (forward.LengthSquared() > 0.0001f)
            {
                entity.FindComponent<ComponentBody>(true).Rotation =
                    Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathUtils.Atan2(-forward.X, -forward.Z));
            }

            miner.RemoveActiveTool(1);
            return true;
        }
    }
}

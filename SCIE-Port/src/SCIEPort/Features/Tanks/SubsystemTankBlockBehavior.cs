using System.Linq;
using Engine;
using Game;
using Game.NetWork;
using TemplatesDatabase;

namespace SCIEPort.Features.Tanks
{
    /// <summary>Spawns tanks above terrain without changing terrain cells.</summary>
    public sealed class SubsystemTankBlockBehavior : SubsystemBlockBehavior
    {
        private SubsystemBodies m_bodies;
        private SubsystemCreatureSpawn m_spawn;
        /// <inheritdoc />
        public override int[] HandledBlocks => new[] { TankBlock.Index };
        /// <inheritdoc />
        public override void Load(ValuesDictionary valuesDictionary) { base.Load(valuesDictionary); m_bodies = Project.FindSubsystem<SubsystemBodies>(true); m_spawn = Project.FindSubsystem<SubsystemCreatureSpawn>(true); }
        /// <inheritdoc />
        public override bool OnUse(Ray3 ray, ComponentMiner miner)
        {
            if (Terrain.ExtractContents(miner.ActiveBlockValue) != TankBlock.Index) return false;

            // A normal client has no entity-spawn authority; it must not manufacture an
            // unsynchronized tank. Mirror the fox/rabbit/chicken spawner guard.
            if (CommonLib.WorkType == WorkType.Client)
            {
                return true;
            }

            TerrainRaycastResult? hit = miner.Raycast<TerrainRaycastResult>(ray, RaycastMode.Digging);
            if (!hit.HasValue) return true;
            Vector3 p = new Vector3(hit.Value.CellFace.Point) + new Vector3(.5f, 1.05f, .5f);
            DynamicArray<ComponentBody> bodies = new DynamicArray<ComponentBody>();
            m_bodies.FindBodiesInArea(p.XZ - new Vector2(16f), p.XZ + new Vector2(16f), bodies);
            if (bodies.Count(b => b.Entity.ValuesDictionary.DatabaseObject.Name == "SCIEPortT34Tank") >= 3) return true;
            GameEntitySystem.Entity entity = m_spawn.SpawnCreature("SCIEPortT34Tank", p, true);
            Vector3 f = Matrix.CreateFromQuaternion(miner.ComponentCreature.ComponentCreatureModel.EyeRotation).Forward; f.Y = 0f;
            if (f.LengthSquared() > .0001f) entity.FindComponent<ComponentBody>(true).Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathUtils.Atan2(-f.X, -f.Z));
            miner.RemoveActiveTool(1);
            return true;
        }
    }
}

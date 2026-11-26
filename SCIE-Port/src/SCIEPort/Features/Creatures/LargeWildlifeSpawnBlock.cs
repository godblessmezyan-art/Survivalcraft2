#pragma warning disable CS1591
using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;
using Game.NetWork;
using TemplatesDatabase;

namespace SCIEPort.Features.Creatures
{
    public sealed class LargeWildlifeSpawnBlock : Block
    {
        public const int Index = 891;
        private readonly BlockMesh[] meshes = { new BlockMesh(), new BlockMesh() };
        private Texture2D texture;

        public override void Initialize()
        {
            for (int kind = 0; kind < meshes.Length; kind++)
            {
                string modelName = kind == 0 ? "MossbackTurtle" : "CloudwingRay";
                Model model = ContentManager.Get<Model>("Models/" + modelName);
                float scale = kind == 0 ? .25f : .145f;
                float y = kind == 0 ? -.72f : -.46f;
                foreach (ModelMesh modelMesh in model.Meshes)
                {
                    Matrix transform = BlockMesh.GetBoneAbsoluteTransform(modelMesh.ParentBone)
                        * Matrix.CreateTranslation(0f, y, 0f) * Matrix.CreateScale(scale);
                    foreach (ModelMeshPart part in modelMesh.MeshParts)
                        meshes[kind].AppendModelMeshPart(part, transform, false, false, false, false, Color.White);
                }
            }
            texture = ContentManager.Get<Texture2D>("Textures/LargeWildlife");
            base.Initialize();
        }

        public override string GetDisplayName(SubsystemTerrain terrain, int value)
            => Terrain.ExtractData(value) == 1 ? "云翼巨鳐生成器" : "苔背巨龟生成器";

        public override IEnumerable<int> GetCreativeValues()
        {
            yield return Terrain.MakeBlockValue(Index);
            yield return Terrain.MakeBlockValue(Index, 0, 1);
        }

        public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry,
            int value, int x, int y, int z) { }

        public override void DrawBlock(PrimitivesRenderer3D renderer, int value, Color color, float size,
            ref Matrix matrix, DrawBlockEnvironmentData environment)
            => BlocksManager.DrawMeshBlock(renderer, meshes[Terrain.ExtractData(value) == 1 ? 1 : 0],
                texture, color, size, ref matrix, environment);
    }

    public sealed class SubsystemLargeWildlifeSpawnBehavior : SubsystemBlockBehavior
    {
        private SubsystemCreatureSpawn spawn;
        public override int[] HandledBlocks => new[] { LargeWildlifeSpawnBlock.Index };

        public override void Load(ValuesDictionary values)
        {
            base.Load(values);
            spawn = Project.FindSubsystem<SubsystemCreatureSpawn>(true);
        }

        public override bool OnUse(Ray3 ray, ComponentMiner miner)
        {
            int value = miner.ActiveBlockValue;
            if (Terrain.ExtractContents(value) != LargeWildlifeSpawnBlock.Index) return false;
            if (CommonLib.WorkType == WorkType.Client) return true;
            int kind = Terrain.ExtractData(value);
            if (kind > 1) return true;
            TerrainRaycastResult? hit = miner.Raycast<TerrainRaycastResult>(ray, RaycastMode.Digging);
            if (!hit.HasValue || hit.Value.CellFace.Face != 4) return true;
            Point3 p = hit.Value.CellFace.Point;
            p.Y++;
            Terrain terrain = spawn.m_subsystemTerrain.Terrain;
            bool flying = kind == 1;
            Vector3 destination = LargeWildlifeHabitat.SpawnPosition(p, flying);
            if (flying)
            {
                if (destination.Y >= 238 || !LargeWildlifeHabitat.TryFlightFloor(terrain, destination, out float floor)
                    || floor + 4f > destination.Y) return true;
            }
            else if (!LargeWildlifeHabitat.TurtleFooting(terrain, destination, false, out int floor)
                || floor > p.Y) return true;

            foreach (ComponentCreature existing in spawn.Creatures)
                if (existing.ComponentHealth.Health > 0f && (existing.ComponentBody.Position - destination).LengthSquared() < 2f)
                    return true;
            string template = WildlifeRules.Names[flying ? WildlifeRules.CloudwingRay : WildlifeRules.MossbackTurtle];
            if (spawn.SpawnCreature(template, destination, true) != null) miner.RemoveActiveTool(1);
            return true;
        }
    }
}

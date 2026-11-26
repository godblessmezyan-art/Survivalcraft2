#pragma warning disable CS1591
using Engine;
using Engine.Graphics;
using Game;
using Game.NetWork;
using TemplatesDatabase;

namespace SCIEPort.Features.Creatures
{
    public abstract class ArthropodItemBlock : Block
    {
        private readonly BlockMesh mesh = new BlockMesh();
        private Texture2D texture;
        protected abstract string ModelName { get; }
        protected abstract float DisplayScale { get; }

        public override void Initialize()
        {
            Model model = ContentManager.Get<Model>("Models/" + ModelName);
            foreach (ModelMesh modelMesh in model.Meshes)
            {
                Matrix transform = BlockMesh.GetBoneAbsoluteTransform(modelMesh.ParentBone) *
                    Matrix.CreateTranslation(0f, -.15f, 0f) * Matrix.CreateScale(DisplayScale);
                foreach (ModelMeshPart part in modelMesh.MeshParts)
                    mesh.AppendModelMeshPart(part, transform, false, false, false, false, Color.White);
            }
            texture = ContentManager.Get<Texture2D>("Textures/WildArthropods");
            base.Initialize();
        }

        public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry,
            int value, int x, int y, int z) { }

        public override void DrawBlock(PrimitivesRenderer3D renderer, int value, Color color, float size,
            ref Matrix matrix, DrawBlockEnvironmentData environment)
            => BlocksManager.DrawMeshBlock(renderer, mesh, texture, color, size, ref matrix, environment);
    }

    public sealed class CrabBaitBlock : ArthropodItemBlock
    {
        public const int Index = 887;
        protected override string ModelName => "FiddlerCrab";
        protected override float DisplayScale => 1.45f;
    }

    public sealed class SandScorpionSpawnBlock : ArthropodItemBlock
    {
        public const int Index = 888;
        protected override string ModelName => "SandScorpion";
        protected override float DisplayScale => .82f;
    }

    public sealed class FiddlerCrabSpawnBlock : ArthropodItemBlock
    {
        public const int Index = 889;
        protected override string ModelName => "FiddlerCrab";
        protected override float DisplayScale => 1.70f;
    }

    public sealed class SubsystemArthropodSpawnBlockBehavior : SubsystemBlockBehavior
    {
        private SubsystemCreatureSpawn spawn;
        public override int[] HandledBlocks => new[] { SandScorpionSpawnBlock.Index, FiddlerCrabSpawnBlock.Index };

        public override void Load(ValuesDictionary values)
        {
            base.Load(values);
            spawn = Project.FindSubsystem<SubsystemCreatureSpawn>(true);
        }

        public override bool OnUse(Ray3 ray, ComponentMiner miner)
        {
            int id = Terrain.ExtractContents(miner.ActiveBlockValue);
            if (id != SandScorpionSpawnBlock.Index && id != FiddlerCrabSpawnBlock.Index) return false;
            if (CommonLib.WorkType == WorkType.Client) return true;
            TerrainRaycastResult? hit = miner.Raycast<TerrainRaycastResult>(ray, RaycastMode.Digging);
            if (!hit.HasValue || hit.Value.CellFace.Face != 4) return true;
            Point3 p = hit.Value.CellFace.Point;
            Terrain cells = spawn.m_subsystemTerrain.Terrain;
            Block floor = ArthropodHabitat.At(cells, p.X, p.Y, p.Z);
            Block above = ArthropodHabitat.At(cells, p.X, p.Y + 1, p.Z);
            if (!floor.IsCollidable || !(above is AirBlock || (id == FiddlerCrabSpawnBlock.Index && above is WaterBlock)))
                return true;
            string name = WildlifeRules.Names[id == SandScorpionSpawnBlock.Index ? WildlifeRules.SandScorpion : WildlifeRules.FiddlerCrab];
            if (spawn.SpawnCreature(name, new Vector3(p.X + .5f, p.Y + 1.035f, p.Z + .5f), true) != null)
                miner.RemoveActiveTool(1);
            return true;
        }
    }
}

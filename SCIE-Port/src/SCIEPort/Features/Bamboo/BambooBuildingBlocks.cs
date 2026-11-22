#pragma warning disable CS1591

using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;

namespace SCIEPort.Features.Bamboo
{
    public sealed class BambooFenceBlock : FenceBlock
    {
        public const int Index = 1002;

        public BambooFenceBlock() : base(null, false, false, BambooTextures.CaneSlot, Color.White, Color.White) { }

        public override void Initialize()
        {
            _ = BambooTextures.Atlas;
            for (int variant = 0; variant < m_blockMeshes.Length; variant++)
            {
                BlockMesh mesh = BambooBuildingGeometry.Fence(variant, out m_collisionBoxes[variant]);
                m_blockMeshes[variant] = m_coloredBlockMeshes[variant] = mesh;
            }
            m_standaloneBlockMesh = BambooBuildingGeometry.Fence(3, out _);
            m_standaloneBlockMesh.TransformPositions(Matrix.CreateTranslation(-.5f, -.5f, -.5f));
        }

        public override string GetCategory(int value) => DefaultCategory;
        public override IEnumerable<int> GetCreativeValues() { yield return Terrain.MakeBlockValue(Index); }

        public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
            => base.GenerateTerrainVertices(generator, geometry.GetGeometry(BambooTextures.Atlas), value, x, y, z);

        public override void DrawBlock(PrimitivesRenderer3D renderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environment)
            => BlocksManager.DrawMeshBlock(renderer, m_standaloneBlockMesh, BambooTextures.Atlas,
                color * SubsystemPalette.GetColor(environment, GetColor(Terrain.ExtractData(value))), size, ref matrix, environment);

        public override BlockDebrisParticleSystem CreateDebrisParticleSystem(SubsystemTerrain terrain, Vector3 position, int value, float strength)
            => BambooTextures.CreateDebris(this, terrain, position, value, strength);

        public override IEnumerable<CraftingRecipe> GetProceduralCraftingRecipes()
        {
            string bamboo = BlocksManager.Blocks[BambooBlock.Index].CraftingId;
            string stick = BlocksManager.Blocks[StickBlock.Index].CraftingId;
            yield return BambooBuildRecipe.Create(Index, 3, "用竹材和木棍扎成三段竹栅栏。", bamboo, stick, bamboo, bamboo, stick, bamboo);
        }
    }

    public sealed class BambooFenceGateBlock : FenceGateBlock
    {
        public const int Index = 1003;

        public BambooFenceGateBlock() : base(null, .08f, false, false, BambooTextures.CaneSlot, Color.White, Color.White) { }

        public override void Initialize()
        {
            _ = BambooTextures.Atlas;
            for (int variant = 0; variant < m_blockMeshes.Length; variant++)
            {
                BlockMesh mesh = BambooBuildingGeometry.Gate(variant, m_pivotDistance, out m_collisionBoxes[variant]);
                m_blockMeshes[variant] = m_coloredBlockMeshes[variant] = mesh;
            }
            m_standaloneBlockMesh = BambooBuildingGeometry.Gate(0, m_pivotDistance, out _);
            m_standaloneBlockMesh.TransformPositions(Matrix.CreateTranslation(-.5f, -.5f, -.5f));
        }

        public override string GetCategory(int value) => DefaultCategory;
        public override IEnumerable<int> GetCreativeValues() { yield return Terrain.MakeBlockValue(Index); }

        public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
        {
            int data = Terrain.ExtractData(value);
            generator.GenerateMeshVertices(this, x, y, z, m_blockMeshes[GetVariant(data)],
                SubsystemPalette.GetColor(generator, GetColor(data)), null, geometry.GetGeometry(BambooTextures.Atlas).SubsetOpaque);
            // Wire vertices still sample the native block atlas.
            generator.GenerateWireVertices(value, x, y, z, GetHingeFace(data), m_pivotDistance * 2f, Vector2.Zero, geometry.SubsetOpaque);
        }

        public override void DrawBlock(PrimitivesRenderer3D renderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environment)
            => BlocksManager.DrawMeshBlock(renderer, m_standaloneBlockMesh, BambooTextures.Atlas,
                color * SubsystemPalette.GetColor(environment, GetColor(Terrain.ExtractData(value))), size, ref matrix, environment);

        public override BlockDebrisParticleSystem CreateDebrisParticleSystem(SubsystemTerrain terrain, Vector3 position, int value, float strength)
            => BambooTextures.CreateDebris(this, terrain, position, value, strength);

        public override IEnumerable<CraftingRecipe> GetProceduralCraftingRecipes()
        {
            string bamboo = BlocksManager.Blocks[BambooBlock.Index].CraftingId;
            string stick = BlocksManager.Blocks[StickBlock.Index].CraftingId;
            yield return BambooBuildRecipe.Create(Index, 1, "用木棍和竹材装一扇竹栅栏门。", stick, bamboo, stick, stick, bamboo, stick);
        }
    }

    public sealed class BambooScreenBlock : Block
    {
        public const int Index = 1004;
        private readonly BlockMesh[] meshes = new BlockMesh[4];
        private readonly BoundingBox[][] boxes = new BoundingBox[4][];
        private BlockMesh display;

        public override void Initialize()
        {
            _ = BambooTextures.Atlas;
            for (int rotation = 0; rotation < meshes.Length; rotation++)
                meshes[rotation] = BambooBuildingGeometry.Screen(rotation, out boxes[rotation]);
            display = new BlockMesh();
            display.AppendBlockMesh(meshes[0]);
            display.TransformPositions(Matrix.CreateTranslation(-.5f, -.5f, -.5f));
            base.Initialize();
        }

        public override BlockPlacementData GetPlacementValue(SubsystemTerrain terrain, ComponentMiner miner, int value, TerrainRaycastResult hit)
        {
            Vector3 forward = Matrix.CreateFromQuaternion(miner.ComponentCreature.ComponentCreatureModel.EyeRotation).Forward;
            int facing = CellFace.Vector3ToFace(forward, 3);
            return new BlockPlacementData { CellFace = hit.CellFace, Value = Terrain.MakeBlockValue(Index, 0, facing) };
        }

        public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value) => boxes[Terrain.ExtractData(value) & 3];

        public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
            => generator.GenerateMeshVertices(this, x, y, z, meshes[Terrain.ExtractData(value) & 3],
                Color.White, null, geometry.GetGeometry(BambooTextures.Atlas).SubsetOpaque);

        public override void DrawBlock(PrimitivesRenderer3D renderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environment)
            => BlocksManager.DrawMeshBlock(renderer, display, BambooTextures.Atlas, color, size, ref matrix, environment);

        public override BlockDebrisParticleSystem CreateDebrisParticleSystem(SubsystemTerrain terrain, Vector3 position, int value, float strength)
            => BambooTextures.CreateDebris(this, terrain, position, value, strength);

        public override IEnumerable<CraftingRecipe> GetProceduralCraftingRecipes()
        {
            string bamboo = BlocksManager.Blocks[BambooBlock.Index].CraftingId;
            yield return BambooBuildRecipe.Create(Index, 2, "把四块竹材劈成篾条，编成两扇屏风。", bamboo, bamboo, null, bamboo, bamboo, null);
        }
    }

    internal static class BambooBuildRecipe
    {
        internal static CraftingRecipe Create(int index, int count, string description,
            string topLeft, string top, string topRight, string bottomLeft, string bottom, string bottomRight)
        {
            return new CraftingRecipe
            {
                ResultValue = Terrain.MakeBlockValue(index),
                ResultCount = count,
                Description = description,
                Ingredients = new[] { topLeft, top, topRight, bottomLeft, bottom, bottomRight, null, null, null }
            };
        }
    }
}

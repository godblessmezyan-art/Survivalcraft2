using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;

namespace SCIEPort.Features.Pyramid
{
    /// <summary>
    /// Original Egyptian tomb decor. One block index carries nine kinds via DATA, each with a
    /// baked Y-up model and a shared opaque atlas; meshes are cached per normalized DATA value.
    /// </summary>
    public sealed class PyramidDecorBlock : Block
    {
        /// <summary>Reserved content index; Terrain.ContentsMask caps block IDs at 1023.</summary>
        public const int Index = PyramidDecorRules.Index;
        private readonly Dictionary<int, BlockMesh> meshes = new Dictionary<int, BlockMesh>();
        private readonly Dictionary<int, BoundingBox[]> boundsByData = new Dictionary<int, BoundingBox[]>();
        private readonly Dictionary<int, Vector3> iconOffsets = new Dictionary<int, Vector3>();
        private Texture2D texture;
        /// <inheritdoc/>
        public override void Initialize()
        {
            texture = ContentManager.Get<Texture2D>("Textures/PyramidDecor");
            for (int kind = 0; kind < PyramidDecorRules.KindCount; kind++)
            for (int variant = 0; variant < PyramidDecorRules.VariantCount(kind); variant++)
            for (int state = 0; state < (PyramidDecorRules.IsSwitchable(kind) ? 2 : 1); state++)
            for (int rotation = 0; rotation < 4; rotation++)
            {
                int data = PyramidDecorRules.Encode(kind, variant, state != 0, rotation);
                BlockMesh mesh = CreateMesh(data, rotation);
                mesh.GenerateSidesData();
                meshes[data] = mesh;
                BoundingBox bounds = mesh.CalculateBoundingBox();
                boundsByData[data] = new[] { bounds };
                if (rotation == 0)
                    iconOffsets[PyramidDecorRules.ItemData(data)] = -.5f * (bounds.Min + bounds.Max);
            }
            base.Initialize();
        }
        private BlockMesh CreateMesh(int data, int rotation)
        {
            Model model = ContentManager.Get<Model>("Models/" + PyramidDecorRules.Model(data));
            var mesh = new BlockMesh();
            Matrix placement = Matrix.CreateRotationY(rotation * MathUtils.PI / 2f) * Matrix.CreateTranslation(.5f, 0, .5f);
            foreach (ModelMesh source in model.Meshes)
            foreach (ModelMeshPart part in source.MeshParts)
                mesh.AppendModelMeshPart(part, BlockMesh.GetBoneAbsoluteTransform(source.ParentBone) * placement, false, false, false, false, Color.White);
            return mesh;
        }
        private int Data(int value) => PyramidDecorRules.Normalize(Terrain.ExtractData(value));
        /// <inheritdoc/>
        public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value) => boundsByData[Data(value)];
        /// <inheritdoc/>
        public override BoundingBox[] GetCustomInteractionBoxes(SubsystemTerrain terrain, int value)
            => GetCustomCollisionBoxes(terrain, value);
        /// <inheritdoc/>
        public override bool IsFaceTransparent(SubsystemTerrain terrain, int face, int value) => true;
        /// <inheritdoc/>
        public override int GetEmittedLightAmount(int value)
            => PyramidDecorRules.Kind(Terrain.ExtractData(value)) == PyramidDecorRules.Brazier && PyramidDecorRules.IsOpen(Terrain.ExtractData(value)) ? 13 : 0;
        /// <inheritdoc/>
        public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
            => generator.GenerateShadedMeshVertices(this, x, y, z, meshes[Data(value)], Color.White, null, null, geometry.GetGeometry(texture).SubsetOpaque);
        /// <inheritdoc/>
        public override void DrawBlock(PrimitivesRenderer3D renderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environment)
            => BlocksManager.DrawMeshBlock(renderer, meshes[PyramidDecorRules.ItemData(Terrain.ExtractData(value))], texture, color, size, ref matrix, environment);
        /// <inheritdoc/>
        public override Vector3 GetIconBlockOffset(int value, DrawBlockEnvironmentData environment)
            => iconOffsets[PyramidDecorRules.ItemData(Terrain.ExtractData(value))];
        /// <inheritdoc/>
        public override IEnumerable<int> GetCreativeValues()
        {
            for (int kind = 0; kind < PyramidDecorRules.KindCount; kind++)
            for (int variant = 0; variant < PyramidDecorRules.VariantCount(kind); variant++)
                yield return Terrain.MakeBlockValue(Index, 0, PyramidDecorRules.Encode(kind, variant, false, 0));
        }
        /// <inheritdoc/>
        public override void GetDropValues(SubsystemTerrain terrain, int oldValue, int newValue, int toolLevel, List<BlockDropValue> drops, out bool showDebris)
        {
            showDebris = true;
            drops.Add(new BlockDropValue { Value = Terrain.MakeBlockValue(Index, 0, PyramidDecorRules.ItemData(Terrain.ExtractData(oldValue))), Count = 1 });
        }
        /// <inheritdoc/>
        public override string GetDisplayName(SubsystemTerrain terrain, int value)
            => PyramidDecorRules.Name(Terrain.ExtractData(value));
    }
}

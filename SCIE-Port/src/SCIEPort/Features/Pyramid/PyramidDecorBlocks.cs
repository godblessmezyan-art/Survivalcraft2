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
        private readonly Dictionary<int, BlockMesh> monumentMeshes = new Dictionary<int, BlockMesh>();
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
                BlockMesh mesh = CreateMesh(PyramidDecorRules.Model(data), rotation);
                mesh.GenerateSidesData();
                meshes[data] = mesh;
                BoundingBox bounds = mesh.CalculateBoundingBox();
                boundsByData[data] = new[] { bounds };
                if (rotation == 0)
                    iconOffsets[PyramidDecorRules.ItemData(data)] = -.5f * (bounds.Min + bounds.Max);
            }
            for (int kind = PyramidMonuments.Coffin; kind <= PyramidMonuments.Obelisk; kind++)
            {
                foreach (bool open in PyramidMonuments.Switchable(kind) ? new[] { false, true } : new[] { false })
                {
                    int data = PyramidMonuments.Data(kind, 0, open);
                    string name = PyramidMonuments.Model(kind, open);
                    meshes[data] = CreateMesh(name, 0);
                    BoundingBox bounds = meshes[data].CalculateBoundingBox();
                    iconOffsets[data] = -.5f * (bounds.Min + bounds.Max);
                    for (int part = 0; part < PyramidMonuments.Size(kind); part++)
                    {
                        Point3 cell = PyramidMonuments.Offset(kind, part);
                        BlockMesh mesh = CreateMesh(name, 0, "Part" + part, cell);
                        monumentMeshes[PyramidMonuments.RenderKey(kind, part, open)] = mesh;
                    }
                }
            }
            base.Initialize();
        }
        private static BlockMesh CreateMesh(string modelName, int rotation, string bone = null, Point3 cell = default)
        {
            Model model = ContentManager.Get<Model>("Models/" + modelName);
            var mesh = new BlockMesh();
            Matrix placement = Matrix.CreateRotationY(rotation * MathUtils.PI / 2f) *
                Matrix.CreateTranslation(.5f - cell.X, -cell.Y, .5f - cell.Z);
            foreach (ModelMesh source in model.Meshes)
            {
                if (bone != null && source.ParentBone.Name != bone) continue;
                foreach (ModelMeshPart part in source.MeshParts)
                    mesh.AppendModelMeshPart(part, BlockMesh.GetBoneAbsoluteTransform(source.ParentBone) * placement, false, false, false, false, Color.White);
            }
            return mesh;
        }
        private int Data(int value) => PyramidDecorRules.Normalize(Terrain.ExtractData(value));
        /// <inheritdoc/>
        public override BlockPlacementData GetPlacementValue(SubsystemTerrain terrain, ComponentMiner miner, int value, TerrainRaycastResult hit)
            => PyramidMonuments.IsMonument(Terrain.ExtractData(value)) ? default : base.GetPlacementValue(terrain, miner, value, hit);
        /// <inheritdoc/>
        public override bool IsInteractive(SubsystemTerrain terrain, int value)
        {
            int data = Terrain.ExtractData(value);
            return PyramidMonuments.IsMonument(data)
                ? PyramidMonuments.Switchable(PyramidDecorRules.Kind(data))
                : base.IsInteractive(terrain, value);
        }
        /// <inheritdoc/>
        public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
        {
            int data = Terrain.ExtractData(value);
            return PyramidMonuments.IsMonument(data)
                ? PyramidMonuments.CollisionBoxes(PyramidDecorRules.Kind(data), PyramidMonuments.Part(data))
                : boundsByData[Data(value)];
        }
        /// <inheritdoc/>
        public override BoundingBox[] GetCustomInteractionBoxes(SubsystemTerrain terrain, int value)
            => GetCustomCollisionBoxes(terrain, value);
        /// <inheritdoc/>
        public override bool IsFaceTransparent(SubsystemTerrain terrain, int face, int value) => true;
        /// <inheritdoc/>
        public override int GetEmittedLightAmount(int value)
        {
            int data = Terrain.ExtractData(value);
            if (!PyramidDecorRules.IsOpen(data)) return 0;
            if (PyramidMonuments.IsMonument(data)) return PyramidDecorRules.Kind(data) == PyramidMonuments.Brazier ? 9 : 0;
            return PyramidDecorRules.Kind(data) == PyramidDecorRules.Brazier ? 13 : 0;
        }
        /// <inheritdoc/>
        public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
        {
            int data = Terrain.ExtractData(value);
            BlockMesh mesh;
            if (PyramidMonuments.IsMonument(data))
            {
                int kind = PyramidDecorRules.Kind(data), part = PyramidMonuments.Part(data);
                Point3 offset = PyramidMonuments.Offset(kind, part);
                int root = Terrain.ReplaceLight(generator.Terrain.GetCellValueFastChunkExists(x - offset.X, y - offset.Y, z - offset.Z), 0);
                if (root != PyramidMonuments.Value(kind, 0) && root != PyramidMonuments.Value(kind, 0, true)) return;
                int geometryData = data | (Terrain.ExtractData(root) & PyramidDecorRules.StateBit);
                mesh = monumentMeshes[geometryData];
            }
            else mesh = meshes[Data(value)];
            if (mesh.Indices.Count > 0)
                generator.GenerateShadedMeshVertices(this, x, y, z, mesh, Color.White, null, null, geometry.GetGeometry(texture).SubsetOpaque);
        }

        private static int IconData(int value)
        {
            int data = Terrain.ExtractData(value);
            return PyramidMonuments.IsMonument(data)
                ? PyramidMonuments.Data(PyramidDecorRules.Kind(data), 0, PyramidDecorRules.IsOpen(data))
                : PyramidDecorRules.ItemData(data);
        }
        /// <inheritdoc/>
        public override void DrawBlock(PrimitivesRenderer3D renderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environment)
            => BlocksManager.DrawMeshBlock(renderer, meshes[IconData(value)], texture, color, size, ref matrix, environment);
        /// <inheritdoc/>
        public override Vector3 GetIconBlockOffset(int value, DrawBlockEnvironmentData environment)
            => iconOffsets[IconData(value)];
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
            if (PyramidMonuments.IsMonument(Terrain.ExtractData(oldValue))) return;
            drops.Add(new BlockDropValue { Value = Terrain.MakeBlockValue(Index, 0, PyramidDecorRules.ItemData(Terrain.ExtractData(oldValue))), Count = 1 });
        }
        /// <inheritdoc/>
        public override string GetDisplayName(SubsystemTerrain terrain, int value)
        {
            int data = Terrain.ExtractData(value);
            return PyramidMonuments.IsMonument(data) ? PyramidMonuments.Name(PyramidDecorRules.Kind(data)) : PyramidDecorRules.Name(data);
        }
    }
}

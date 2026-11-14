using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;

namespace SCIEPort.Features.Architecture
{
    /// <summary>Single-cell meshes with exact DATA rotation/reflection and ordinary native terrain lifecycle.</summary>
    public abstract class ArchitectureBlock : Block
    {
        private readonly Dictionary<int, ArchitectureGeometry.Shape> shapes = new Dictionary<int, ArchitectureGeometry.Shape>();
        private Texture2D texture;
        /// <inheritdoc/>
        public override void Initialize()
        {
            texture = new Texture2D(1, 1, 1, ColorFormat.Rgba8888);
            texture.SetData(0, new byte[] { 255, 255, 255, 255 });
            for (int v = 0; v < ArchitectureRules.Variants(BlockIndex); v++)
            for (int state = 0; state < 8; state++)
                shapes[(v << 3) | state] = ArchitectureGeometry.Build(BlockIndex, (v << 3) | state);
            base.Initialize();
        }
        private ArchitectureGeometry.Shape Shape(int value) => shapes[ArchitectureRules.Normalize(BlockIndex, Terrain.ExtractData(value))];
        /// <inheritdoc/>
        public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value) => Shape(value).Collision;
        /// <inheritdoc/>
        public override BoundingBox[] GetCustomInteractionBoxes(SubsystemTerrain terrain, int value) => Shape(value).Interaction;
        /// <inheritdoc/>
        public override bool IsFaceTransparent(SubsystemTerrain terrain, int face, int value) => true;
        /// <inheritdoc/>
        public override int GetEmittedLightAmount(int value) => BlockIndex == ArchitectureRules.Lantern ? 12 : 0;
        /// <inheritdoc/>
        public override string GetDisplayName(SubsystemTerrain terrain, int value) => ArchitectureRules.Name(BlockIndex, Terrain.ExtractData(value));
        /// <inheritdoc/>
        public override IEnumerable<int> GetCreativeValues()
        {
            for (int v = 0; v < ArchitectureRules.Variants(BlockIndex); v++) yield return Terrain.MakeBlockValue(BlockIndex, 0, v << 3);
        }
        /// <inheritdoc/>
        public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
        {
            ArchitectureGeometry.Shape shape = Shape(value);
            generator.GenerateShadedMeshVertices(this, x, y, z, shape.Opaque, Color.White, null, null, geometry.GetGeometry(texture).SubsetOpaque);
            if (shape.Glass.Vertices.Count > 0)
                generator.GenerateMeshVertices(this, x, y, z, shape.Glass, Color.White, null, geometry.GetGeometry(texture).SubsetTransparent);
        }
        /// <inheritdoc/>
        public override void DrawBlock(PrimitivesRenderer3D renderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environment)
        {
            ArchitectureGeometry.Shape shape = Shape(value);
            BlocksManager.DrawMeshBlock(renderer, shape.Opaque, texture, color, size, ref matrix, environment);
            if (shape.Glass.Vertices.Count > 0) BlocksManager.DrawMeshBlock(renderer, shape.Glass, texture, color, size, ref matrix, environment);
        }
        /// <inheritdoc/>
        public override Vector3 GetIconBlockOffset(int value, DrawBlockEnvironmentData environment)
        {
            BoundingBox b = Shape(value).Bounds;
            return -.5f * (b.Min + b.Max);
        }
        /// <inheritdoc/>
        public override BlockPlacementData GetPlacementValue(SubsystemTerrain terrain, ComponentMiner miner, int value, TerrainRaycastResult hit)
        {
            Point3 p = hit.CellFace.Point + CellFace.FaceToPoint3(hit.CellFace.Face);
            Vector3 f = Matrix.CreateFromQuaternion(miner.ComponentCreature.ComponentCreatureModel.EyeRotation).Forward;
            int r = System.Math.Abs(f.X) > System.Math.Abs(f.Z) ? (f.X > 0 ? 1 : 3) : (f.Z > 0 ? 0 : 2);
            var chunk = terrain.Terrain.GetChunkAtCell(p.X, p.Z);
            bool valid = p.Y >= 0 && p.Y < 256 && p.X >= -1000000 && p.X <= 1000000 && p.Z >= -1000000 && p.Z <= 1000000
                && chunk != null && chunk.State == TerrainChunkState.Valid && terrain.Terrain.GetCellContents(p.X, p.Y, p.Z) == 0;
            int data = ArchitectureRules.Normalize(BlockIndex, Terrain.ExtractData(value));
            return new BlockPlacementData { CellFace = hit.CellFace, Value = valid ? Terrain.MakeBlockValue(BlockIndex, 0, (data & ~3) | r) : 0 };
        }
        /// <inheritdoc/>
        public override void GetDropValues(SubsystemTerrain terrain, int oldValue, int newValue, int toolLevel, List<BlockDropValue> drops, out bool debris)
        {
            debris = true;
            int data = ArchitectureRules.Normalize(BlockIndex, Terrain.ExtractData(oldValue)) & ~3;
            drops.Add(new BlockDropValue { Value = Terrain.MakeBlockValue(BlockIndex, 0, data), Count = 1 });
        }
    }
    /// <summary>Tiled roofing, ridge and eave modules.</summary>
    public sealed class AncientRoofBlock : ArchitectureBlock { /// <summary>Content index.</summary>
        public const int Index = ArchitectureRules.Roof; }
    /// <summary>Columns, beams, brackets, braces and balustrades.</summary>
    public sealed class AncientTimberBlock : ArchitectureBlock { /// <summary>Content index.</summary>
        public const int Index = ArchitectureRules.Timber; }
    /// <summary>Thin lattice glass and four quarter-circle wall modules.</summary>
    public sealed class AncientWindowBlock : ArchitectureBlock { /// <summary>Content index.</summary>
        public const int Index = ArchitectureRules.Window; }
    /// <summary>Hanging, wall and pedestal lanterns with native terrain lighting.</summary>
    public sealed class AncientLanternBlock : ArchitectureBlock { /// <summary>Content index.</summary>
        public const int Index = ArchitectureRules.Lantern; }
    /// <summary>Tea table, cushion, tea service and split fabric valance.</summary>
    public sealed class AncientTeaBlock : ArchitectureBlock { /// <summary>Content index.</summary>
        public const int Index = ArchitectureRules.Tea; }
    /// <summary>Static cherry branches, vines and potted plants.</summary>
    public sealed class AncientGardenBlock : ArchitectureBlock { /// <summary>Content index.</summary>
        public const int Index = ArchitectureRules.Garden; }
    /// <summary>Decorative copper telescope on a three-legged stand.</summary>
    public sealed class AncientTelescopeBlock : ArchitectureBlock { /// <summary>Content index.</summary>
        public const int Index = ArchitectureRules.Telescope; }
}

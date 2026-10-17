using Engine;
using Engine.Graphics;
using Game;

namespace SCIEPort.Features.Rail
{
    /// <summary>Shared creative-item renderer for SCIE rail vehicles.</summary>
    public abstract class RailVehicleBlock : Block
    {
        private readonly BlockMesh m_mesh = new BlockMesh();
        /// <summary>Content model name.</summary>
        protected abstract string ModelName { get; }
        /// <summary>Model mesh node used by the item.</summary>
        protected abstract string MeshName { get; }
        /// <summary>Content texture name.</summary>
        protected abstract string TextureName { get; }
        /// <summary>Item rendering scale.</summary>
        protected virtual float ModelScale => 0.45f;

        /// <inheritdoc />
        public override void Initialize()
        {
            ModelMesh mesh = ContentManager.Get<Model>(ModelName).FindMesh(MeshName);
            Matrix transform = BlockMesh.GetBoneAbsoluteTransform(mesh.ParentBone);
            foreach (ModelMeshPart part in mesh.MeshParts)
            {
                m_mesh.AppendModelMeshPart(part, transform, false, false, false, false, Color.White);
                m_mesh.AppendModelMeshPart(part, transform, false, true, false, false, Color.White);
            }
            base.Initialize();
        }

        /// <inheritdoc />
        public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z) { }

        /// <inheritdoc />
        public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
        {
            BlocksManager.DrawMeshBlock(primitivesRenderer, m_mesh, ContentManager.Get<Texture2D>(TextureName), color, size * ModelScale, ref matrix, environmentData);
        }
    }

    /// <summary>Creative deployment item for the SCIE locomotive.</summary>
    public sealed class TrainBlock : RailVehicleBlock
    {
        /// <summary>Block ID reserved for the locomotive item.</summary>
        public const int Index = 996;
        /// <inheritdoc />
        protected override string ModelName => "Models/Train";
        /// <inheritdoc />
        protected override string MeshName => "Cylinder";
        /// <inheritdoc />
        protected override string TextureName => "Textures/Train";
        /// <inheritdoc />
        protected override float ModelScale => 0.35f;
    }

    /// <summary>Creative deployment item for the SCIE carriage.</summary>
    public sealed class CarriageBlock : RailVehicleBlock
    {
        /// <summary>Block ID reserved for the carriage item.</summary>
        public const int Index = 995;
        /// <inheritdoc />
        protected override string ModelName => "Models/Minecart";
        /// <inheritdoc />
        protected override string MeshName => "obj1";
        /// <inheritdoc />
        protected override string TextureName => "Textures/Creatures/Jaguar";
        /// <inheritdoc />
        protected override float ModelScale => 0.55f;
    }
}

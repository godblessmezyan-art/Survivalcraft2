using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;

namespace SCIEPort.Features.Tanks
{
    /// <summary>Large solid-black spherical projectile used only by the tank main gun.</summary>
    public sealed class TankShellBlock : Block
    {
        /// <summary>Reserved SCIE-Port content ID.</summary>
        public const int Index = 926;
        private readonly BlockMesh m_mesh = new BlockMesh();

        /// <inheritdoc />
        public override void Initialize()
        {
            // Reuse the target game's sphere mesh without packaging any game asset.
            Model model = ContentManager.Get<Model>("Models/Snowball");
            ModelMesh mesh = model.FindMesh("Snowball");
            Matrix transform = BlockMesh.GetBoneAbsoluteTransform(mesh.ParentBone);
            foreach (ModelMeshPart part in mesh.MeshParts)
                m_mesh.AppendModelMeshPart(part, transform, false, false, false, false, Color.Black);
            base.Initialize();
        }

        /// <inheritdoc />
        public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z) { }

        /// <inheritdoc />
        public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
        {
            // Native projectiles request size 0.3. A 3.5 multiplier makes the shell
            // substantially larger than a musket-ball sprite while remaining below one cell.
            BlocksManager.DrawMeshBlock(primitivesRenderer, m_mesh, Color.Black, TankRules.ShellVisualScale * size, ref matrix, environmentData);
        }

        /// <inheritdoc />
        public override IEnumerable<int> GetCreativeValues()
        {
            yield break;
        }
    }
}

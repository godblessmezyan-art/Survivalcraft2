using Engine;
using Engine.Graphics;
using Game;

namespace SCIEPort.Features.Gungnir
{
    /// <summary>Original UV-mapped divine spear shared by all four item rendering paths.</summary>
    public sealed class GungnirBlock : Block
    {
        /// <summary>Reserved after the fortress content range.</summary>
        public const int Index = 948;
        internal static BlockMesh Mesh;
        internal static Texture2D Texture;
        private static BlockMesh s_emission;
        /// <inheritdoc />
        public override void Initialize()
        {
            Mesh = new BlockMesh(); s_emission = new BlockMesh();
            var model = ContentManager.Get<Model>("Models/Gungnir");
            foreach (var mesh in model.Meshes)
            foreach (var part in mesh.MeshParts)
            {
                Matrix transform = BlockMesh.GetBoneAbsoluteTransform(mesh.ParentBone);
                Mesh.AppendModelMeshPart(part, transform, false, false, false, false, Color.White);
                if (mesh.Name.StartsWith("Light")) s_emission.AppendModelMeshPart(part, transform, false, false, false, true, Color.White);
            }
            Texture = ContentManager.Get<Texture2D>("Textures/Gungnir");
            base.Initialize();
        }
        /// <inheritdoc />
        public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z) { }
        /// <inheritdoc />
        public override void DrawBlock(PrimitivesRenderer3D renderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environment)
        {
            bool blessed = false;
            var runtime = environment?.SubsystemTerrain?.Project.FindSubsystem<SubsystemGungnir>(false);
            if (runtime != null && (environment.DrawBlockMode == DrawBlockMode.FirstPerson || environment.DrawBlockMode == DrawBlockMode.ThirdPerson))
            {
                foreach (var s in runtime.States.Values)
                {
                    var first = s.Player.Entity.FindComponent<ComponentFirstPersonModel>(false);
                    var third = s.Player.Entity.FindComponent<ComponentHumanModel>(false);
                    if ((first != null && ReferenceEquals(first.m_drawBlockEnvironmentData, environment)) || (third != null && ReferenceEquals(third.m_drawBlockEnvironmentData, environment)))
                    {
                        if (s.Phase != 0) return;
                        blessed = runtime.Now < s.BuffEnd;
                        break;
                    }
                }
            }
            BlocksManager.DrawMeshBlock(renderer, Mesh, Texture, color, size, ref matrix, environment);
            float glow = blessed ? 1f : 0.65f + 0.2f * MathUtils.Sin((float)Time.RealTime * 1.5f);
            BlocksManager.DrawMeshBlock(renderer, s_emission, Texture, Color.White * glow, size * 1.001f, ref matrix, environment);
        }
        /// <inheritdoc />
        public override Vector3 GetIconViewOffset(int value, DrawBlockEnvironmentData environmentData) => new Vector3(-0.8f, 0.2f, -1);
    }
}

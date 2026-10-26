using Engine;
using Engine.Graphics;
using Game;
using GameEntitySystem;

namespace SCIEPort.Features.Stormhammer
{
    /// <summary>Short-lived world-space renderer for the authored thunder-spear model.</summary>
    internal sealed class ThunderSpearModelParticleSystem : ParticleSystemBase
    {
        private static readonly BlockMesh s_mesh = BuildMesh();
        private static readonly Texture2D s_texture = ContentManager.Get<Texture2D>("Textures/StormhammerAwakened");
        private readonly Project m_project;
        private readonly Vector3 m_start;
        private readonly Vector3 m_end;
        private readonly PrimitivesRenderer3D m_renderer = new PrimitivesRenderer3D();
        private readonly DrawBlockEnvironmentData m_environment = new DrawBlockEnvironmentData();
        private float m_age;

        public ThunderSpearModelParticleSystem(Project project, Vector3 start, Vector3 end)
        {
            m_project = project;
            m_start = start;
            m_end = end;
            m_environment.SubsystemTerrain = project.FindSubsystem<SubsystemTerrain>(false);
            m_environment.Light = 15;
        }

        public override bool Simulate(float dt)
        {
            m_age += dt;
            return m_age >= 0.24f;
        }

        public override void Draw(Camera camera)
        {
            Vector3 direction = m_end - m_start;
            float length = direction.Length();
            if (length < 0.05f) return;
            direction /= length;
            float t = MathUtils.Saturate(m_age / 0.18f);
            Vector3 position = Vector3.Lerp(m_start, m_end, t);
            Vector3 forward = Vector3.Cross(direction, Vector3.UnitX);
            if (forward.LengthSquared() < 0.05f) forward = Vector3.Cross(direction, Vector3.UnitZ);
            forward = Vector3.Normalize(forward);
            Matrix matrix = Matrix.CreateWorld(position, forward, direction);
            m_environment.InWorldMatrix = matrix;
            m_environment.ViewProjectionMatrix = camera.ViewProjectionMatrix;
            m_environment.BillboardDirection = position - camera.ViewPosition;
            BlocksManager.DrawMeshBlock(m_renderer, s_mesh, s_texture, new Color(164, 226, 255), 0.48f, ref matrix, m_environment);
            m_renderer.Flush(camera.ViewProjectionMatrix);
        }

        private static BlockMesh BuildMesh()
        {
            BlockMesh result = new BlockMesh();
            Model model = ContentManager.Get<Model>("Models/ThunderSpear");
            foreach (ModelMesh mesh in model.Meshes)
            {
                Matrix transform = BlockMesh.GetBoneAbsoluteTransform(mesh.ParentBone);
                foreach (ModelMeshPart part in mesh.MeshParts)
                {
                    result.AppendModelMeshPart(part, transform, false, false, false, false, Color.White);
                }
            }
            return result;
        }
    }
}

using Engine;
using Engine.Graphics;
using Game;
using SCIEPort.Features.Stormhammer;

namespace SCIEPort.Features.Gungnir
{
    /// <summary>Original UV-mapped divine spear shared by all four item rendering paths.</summary>
    public sealed class GungnirBlock : Block
    {
        /// <summary>Reserved after the fortress content range.</summary>
        public const int Index = 948;
        internal static BlockMesh Mesh;
        internal static Texture2D Texture;
        private static BlockMesh s_energyRidge;
        private static readonly BlockMesh[] s_runes = new BlockMesh[24];

        internal static BlockMesh EnergyRidge => s_energyRidge;
        internal static BlockMesh[] RuneMeshes => s_runes;
        /// <inheritdoc />
        public override void Initialize()
        {
            Mesh = new BlockMesh(); s_energyRidge = new BlockMesh();
            var model = ContentManager.Get<Model>("Models/Gungnir");
            int runeIndex = 0;
            foreach (var mesh in model.Meshes)
            foreach (var part in mesh.MeshParts)
            {
                Matrix transform = BlockMesh.GetBoneAbsoluteTransform(mesh.ParentBone);
                Mesh.AppendModelMeshPart(part, transform, false, false, false, false, Color.White);
                if (mesh.Name.StartsWith("LightRune_") && runeIndex < s_runes.Length)
                {
                    if (s_runes[runeIndex] == null) s_runes[runeIndex] = new BlockMesh();
                    s_runes[runeIndex++].AppendModelMeshPart(part, transform, false, false, false, true, Color.White);
                }
                else if (mesh.Name.StartsWith("Light"))
                {
                    s_energyRidge.AppendModelMeshPart(part, transform, false, false, false, true, Color.White);
                }
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
            float blessingRemaining = 0f;
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
                        blessingRemaining = (float)(s.BuffEnd - runtime.Now);
                        break;
                    }
                }
            }
            BlocksManager.DrawMeshBlock(renderer, Mesh, Texture, color, size, ref matrix, environment);
            float endFade = blessed ? MathUtils.Saturate(blessingRemaining / 2f) : 1f;
            BlocksManager.DrawMeshBlock(renderer, s_energyRidge, Texture, Color.White * (blessed ? 0.82f * endFade : 0.28f), size * 1.001f, ref matrix, environment);
            int stride = StormhammerOptions.Quality == StormhammerEffectQuality.Low ? 2 : 1;
            float travel = (float)(Time.RealTime * (blessed ? 7.2 : 1.15)) % 12f;
            for (int i = 0; i < s_runes.Length; i += stride)
            {
                if (s_runes[i] == null) continue;
                int segment = i % 12;
                float distance = MathUtils.Abs(segment - travel);
                distance = MathUtils.Min(distance, 12f - distance);
                // A narrow travelling head and a softer tail produce directional shaft flow;
                // idle runes remain etched at low brightness instead of pulsing as one object.
                float head = MathUtils.Pow(MathUtils.Saturate(1f - distance / 2.2f), 2f);
                float trailDistance = (travel - segment + 12f) % 12f;
                float tail = MathUtils.Saturate(1f - trailDistance / 5f) * 0.28f;
                float glow = blessed ? (0.18f + head * 0.82f + tail) * endFade : 0.12f + head * 0.12f;
                BlocksManager.DrawMeshBlock(renderer, s_runes[i], Texture, new Color(255, 245, 206) * glow, size * 1.002f, ref matrix, environment);
            }
        }
        /// <inheritdoc />
        public override Vector3 GetIconViewOffset(int value, DrawBlockEnvironmentData environmentData) => new Vector3(-0.8f, 0.2f, -1);
    }
}

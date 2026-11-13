#pragma warning disable CS1591
using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;

namespace SCIEPort.Features.Fishing
{
    /// <summary>Craftable fishing rod; the minigame itself lives in <see cref="SubsystemFishing"/>.</summary>
    public sealed class FishingRodBlock : Block
    {
        public const int Index = 972;

        private readonly BlockMesh m_mesh = new BlockMesh();
        private Texture2D m_texture;

        public override void Initialize()
        {
            BuildMesh(m_mesh);
            m_texture = new Texture2D(1, 1, 1, ColorFormat.Rgba8888);
            m_texture.SetData(0, new byte[] { 255, 255, 255, 255 });
            base.Initialize();
        }

        public override string GetCategory(int value)
        {
            return Inventory.PortInventoryRules.Tools;
        }

        public override IEnumerable<CraftingRecipe> GetProceduralCraftingRecipes()
        {
            Block stick = BlocksManager.FindBlockByTypeName("StickBlock", false);
            Block line = BlocksManager.FindBlockByTypeName("StringBlock", false);
            if (stick == null || line == null) yield break;
            string stickId = stick.GetCraftingId(0);
            string lineId = line.GetCraftingId(0);
            if (string.IsNullOrEmpty(stickId) || string.IsNullOrEmpty(lineId)) yield break;
            yield return new CraftingRecipe
            {
                ResultValue = Terrain.MakeBlockValue(Index),
                ResultCount = 1,
                RemainsValue = 0,
                RemainsCount = 0,
                RequiredHeatLevel = 0f,
                RequiredPlayerLevel = 0f,
                Ingredients = new[] { stickId, lineId, string.Empty, lineId },
                Description = "鱼竿",
                Message = string.Empty,
                IsAdHocCraftingRecipe = false
            };
        }

        public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
        {
        }

        public override void DrawBlock(PrimitivesRenderer3D renderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environment)
        {
            BlocksManager.DrawMeshBlock(renderer, m_mesh, m_texture, color, size, ref matrix, environment);
        }

        private static void BuildMesh(BlockMesh mesh)
        {
            Matrix tilt = Matrix.CreateRotationY(0.5f) * Matrix.CreateRotationX(0.18f);
            Box(mesh, tilt, new Vector3(0f, 0f, -0.03f), new Vector3(0.13f, 0.13f, 0.06f), new Color(48, 33, 24));
            Box(mesh, tilt, new Vector3(0f, 0f, 0.16f), new Vector3(0.105f, 0.105f, 0.32f), new Color(166, 121, 74));
            Box(mesh, tilt, new Vector3(0f, 0f, 0.34f), new Vector3(0.115f, 0.115f, 0.05f), new Color(206, 170, 112));
            Box(mesh, tilt, new Vector3(0f, 0f, 0.48f), new Vector3(0.08f, 0.08f, 0.26f), new Color(62, 41, 27));
            Box(mesh, tilt, new Vector3(0f, 0f, 0.70f), new Vector3(0.058f, 0.058f, 0.24f), new Color(48, 33, 22));
            Box(mesh, tilt, new Vector3(0f, 0f, 0.90f), new Vector3(0.04f, 0.04f, 0.2f), new Color(37, 27, 19));
            Box(mesh, tilt, new Vector3(0f, 0f, 1.01f), new Vector3(0.02f, 0.02f, 0.08f), new Color(222, 212, 184));
            Box(mesh, tilt, new Vector3(0f, -0.10f, 0.44f), new Vector3(0.15f, 0.075f, 0.15f), new Color(138, 144, 152));
            Box(mesh, tilt, new Vector3(0f, -0.15f, 0.44f), new Vector3(0.08f, 0.03f, 0.11f), new Color(198, 168, 92));
            Box(mesh, tilt, new Vector3(0.10f, -0.10f, 0.41f), new Vector3(0.035f, 0.06f, 0.035f), new Color(70, 70, 76));
            for (int i = 0; i < 3; i++)
            {
                float z = 0.58f + i * 0.17f;
                Box(mesh, tilt, new Vector3(0f, -0.045f, z), new Vector3(0.075f - i * 0.015f, 0.012f, 0.012f), new Color(198, 168, 92));
            }
        }

        private static void Box(BlockMesh mesh, Matrix transform, Vector3 center, Vector3 size, Color color)
        {
            Vector3 h = size * 0.5f;
            Vector3[] corners =
            {
                new Vector3(-h.X, -h.Y, -h.Z), new Vector3(h.X, -h.Y, -h.Z), new Vector3(h.X, h.Y, -h.Z), new Vector3(-h.X, h.Y, -h.Z),
                new Vector3(-h.X, -h.Y, h.Z), new Vector3(h.X, -h.Y, h.Z), new Vector3(h.X, h.Y, h.Z), new Vector3(-h.X, h.Y, h.Z)
            };
            int[] faces = { 0, 3, 2, 1, 5, 6, 7, 4, 1, 2, 6, 5, 4, 7, 3, 0, 3, 7, 6, 2, 4, 0, 1, 5 };
            for (int f = 0; f < 6; f++)
            {
                ushort start = (ushort)mesh.Vertices.Count;
                for (int j = 0; j < 4; j++)
                {
                    Vector3 p = corners[faces[f * 4 + j]];
                    mesh.Vertices.Add(new BlockMeshVertex
                    {
                        Position = Vector3.Transform(center + p, transform),
                        TextureCoordinates = new Vector2(0.5f),
                        Color = Color.MultiplyColorOnly(color, f == 4 ? 1f : f == 5 ? 0.5f : 0.8f),
                        Face = (byte)f
                    });
                }
                foreach (int k in new[] { 0, 1, 2, 0, 2, 3 })
                {
                    mesh.Indices.Add((ushort)(start + k));
                }
            }
        }
    }
}

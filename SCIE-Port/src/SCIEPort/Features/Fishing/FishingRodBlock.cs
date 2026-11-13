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
            Matrix tilt = Matrix.CreateRotationY(0.4f) * Matrix.CreateRotationX(0.12f);
            Box(mesh, tilt, new Vector3(0f, 0f, 0.36f), new Vector3(0.14f, 0.14f, 0.07f), new Color(48, 33, 24));
            Box(mesh, tilt, new Vector3(0f, 0f, 0.20f), new Vector3(0.115f, 0.115f, 0.30f), new Color(166, 121, 74));
            Box(mesh, tilt, new Vector3(0f, 0f, 0.04f), new Vector3(0.125f, 0.125f, 0.05f), new Color(206, 170, 112));
            Box(mesh, tilt, new Vector3(0f, 0f, -0.13f), new Vector3(0.09f, 0.09f, 0.30f), new Color(62, 41, 27));
            Box(mesh, tilt, new Vector3(0f, 0f, -0.32f), new Vector3(0.065f, 0.065f, 0.26f), new Color(48, 33, 22));
            Box(mesh, tilt, new Vector3(0f, 0f, -0.49f), new Vector3(0.045f, 0.045f, 0.20f), new Color(37, 27, 19));
            Box(mesh, tilt, new Vector3(0f, 0f, -0.59f), new Vector3(0.022f, 0.022f, 0.09f), new Color(222, 212, 184));
            Box(mesh, tilt, new Vector3(0f, -0.10f, 0.06f), new Vector3(0.16f, 0.08f, 0.16f), new Color(138, 144, 152));
            Box(mesh, tilt, new Vector3(0f, -0.15f, 0.06f), new Vector3(0.085f, 0.03f, 0.12f), new Color(198, 168, 92));
            Box(mesh, tilt, new Vector3(0.10f, -0.10f, 0.09f), new Vector3(0.04f, 0.065f, 0.04f), new Color(70, 70, 76));
            for (int i = 0; i < 3; i++)
            {
                float z = -0.10f - i * 0.16f;
                Box(mesh, tilt, new Vector3(0f, -0.048f, z), new Vector3(0.078f - i * 0.014f, 0.014f, 0.014f), new Color(198, 168, 92));
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

#pragma warning disable CS1591
using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;

namespace SCIEPort.Features.Flashlight
{
    public sealed class FlashlightBlock : Block
    {
        public const int Index = 890;
        internal const int Capacity = 300;
        private readonly BlockMesh m_body = new BlockMesh();
        private readonly BlockMesh m_darkLens = new BlockMesh();
        private readonly BlockMesh m_brightLens = new BlockMesh();
        private Texture2D m_white;

        internal static int Spent(int data) => data < 0 ? Capacity : Math.Min(data >> 1, Capacity);
        internal static bool IsOn(int value) => Terrain.ExtractContents(value) == Index &&
            (Terrain.ExtractData(value) & 1) != 0 && Spent(Terrain.ExtractData(value)) < Capacity;

        internal static int SetPower(int value, bool on)
        {
            int spent = Spent(Terrain.ExtractData(value));
            return Terrain.ReplaceData(value, (spent << 1) | (on && spent < Capacity ? 1 : 0));
        }

        internal static int Drain(int value, int seconds)
        {
            if (!IsOn(value) || seconds <= 0) return value;
            int spent = Spent(Terrain.ExtractData(value));
            spent += Math.Min(seconds, Capacity - spent);
            return Terrain.ReplaceData(value, (spent << 1) | (spent < Capacity ? 1 : 0));
        }

        public override int GetDamage(int value) => Spent(Terrain.ExtractData(value));
        // The native first-person model asks the new block about every value change, including DATA.
        public override bool IsSwapAnimationNeeded(int oldValue, int newValue)
            => Terrain.ExtractContents(oldValue) != Index || Terrain.ExtractContents(newValue) != Index;

        // The native inventory bar reads durability. Striking or mining leaves electrical charge alone.
        public override int SetDamage(int value, int damage) => value;
        public override int GetDamageDestructionValue(int value) => value;
        public override BlockPlacementData GetPlacementValue(SubsystemTerrain terrain, ComponentMiner miner,
            int value, TerrainRaycastResult hit) => default(BlockPlacementData);
        public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry,
            int value, int x, int y, int z) { }

        public override IEnumerable<CraftingRecipe> GetProceduralCraftingRecipes()
        {
            string battery = BlocksManager.Blocks[BatteryBlock.Index].CraftingId;
            string bulb = BlocksManager.Blocks[LightbulbBlock.Index].CraftingId;
            string iron = BlocksManager.Blocks[IronIngotBlock.Index].CraftingId;
            yield return Recipe("手电筒", new[] { bulb, iron, null, battery, iron, null, null, null, null });
            CraftingRecipe replacement = Recipe("更换电池", RefillIngredients(CraftingId + ":" + (Capacity << 1), battery));
            replacement.IsAdHocCraftingRecipe = true;
            yield return replacement;
        }

        public override CraftingRecipe GetAdHocCraftingRecipe(SubsystemTerrain terrain, string[] ingredients,
            float heatLevel, ComponentPlayer player)
        {
            if (ingredients.Length != 9) return null;
            string prefix = CraftingId + ":";
            foreach (string ingredient in ingredients)
            {
                if (ingredient == null || !ingredient.StartsWith(prefix, StringComparison.Ordinal)) continue;
                CraftingRecipesManager.DecodeIngredient(ingredient, out string id, out int? data);
                if (id != CraftingId || !data.HasValue || Spent(data.Value) == 0) continue;
                string battery = BlocksManager.Blocks[BatteryBlock.Index].CraftingId;
                string[] pattern = RefillIngredients(ingredient, battery);
                if (CraftingRecipesManager.MatchRecipe(pattern, ingredients))
                    return Recipe("更换电池", pattern);
            }
            return null;
        }

        private static string[] RefillIngredients(string flashlight, string battery)
            => new[] { flashlight, battery, null, null, null, null, null, null, null };

        private static CraftingRecipe Recipe(string description, string[] ingredients) => new CraftingRecipe
        {
            ResultValue = Terrain.MakeBlockValue(Index),
            ResultCount = 1,
            RequiredPlayerLevel = 1f,
            Description = description,
            Ingredients = ingredients
        };

        public override void Initialize()
        {
            var graphite = new Color(47, 51, 55);
            var rubber = new Color(31, 34, 36);
            var trim = new Color(196, 132, 38);
            Ring(m_body, -0.34f, 0.153f, -0.30f, 0.153f, graphite);
            Ring(m_body, -0.30f, 0.153f, -0.29f, 0.156f, trim);
            Ring(m_body, -0.29f, 0.156f, -0.17f, 0.114f, graphite);
            Ring(m_body, -0.17f, 0.114f, 0.28f, 0.101f, rubber);
            Ring(m_body, 0.28f, 0.101f, 0.29f, 0.107f, graphite);
            Ring(m_body, 0.29f, 0.107f, 0.32f, 0.107f, graphite);
            for (int i = 0; i < 5; i++)
            {
                float z = -0.09f + 0.069f * i;
                Ring(m_body, z - 0.005f, 0.101f, z, 0.116f, graphite);
                Ring(m_body, z, 0.116f, z + 0.017f, 0.116f, graphite);
                Ring(m_body, z + 0.017f, 0.116f, z + 0.022f, 0.101f, graphite);
            }
            Ring(m_body, -0.34f, 0.121f, -0.34f, 0.153f, graphite);
            Ring(m_body, -0.326f, 0.114f, -0.34f, 0.121f, new Color(157, 165, 167));
            Ring(m_body, 0.32f, 0.107f, 0.32f, 0f, graphite);
            Ring(m_darkLens, -0.326f, 0f, -0.326f, 0.114f, new Color(131, 156, 159));
            Ring(m_brightLens, -0.326f, 0f, -0.326f, 0.114f, new Color(255, 249, 215), true);
            m_white = new Texture2D(1, 1, 1, ColorFormat.Rgba8888);
            m_white.SetData(0, new byte[] { 255, 255, 255, 255 });
            base.Initialize();
        }

        private static void Ring(BlockMesh mesh, float z1, float radius1, float z2, float radius2,
            Color color, bool emissive = false)
        {
            const int sides = 12;
            for (int i = 0; i < sides; i++)
            {
                float a = 2f * MathUtils.PI * i / sides;
                float b = 2f * MathUtils.PI * (i + 1) / sides;
                Vector2 start = new Vector2(MathUtils.Cos(a), MathUtils.Sin(a));
                Vector2 end = new Vector2(MathUtils.Cos(b), MathUtils.Sin(b));
                Color facet = emissive ? color : Color.MultiplyColorOnly(color, 0.78f + 0.22f * start.Y);
                ushort index = (ushort)mesh.Vertices.Count;
                AddVertex(mesh, start * radius1, z1, facet, emissive);
                AddVertex(mesh, end * radius1, z1, facet, emissive);
                AddVertex(mesh, end * radius2, z2, facet, emissive);
                AddVertex(mesh, start * radius2, z2, facet, emissive);
                if (radius1 > 0f)
                {
                    mesh.Indices.Add(index);
                    mesh.Indices.Add((ushort)(index + 2));
                    mesh.Indices.Add((ushort)(index + 1));
                }
                if (radius2 > 0f)
                {
                    mesh.Indices.Add(index);
                    mesh.Indices.Add((ushort)(index + 3));
                    mesh.Indices.Add((ushort)(index + 2));
                }
            }
        }

        private static void AddVertex(BlockMesh mesh, Vector2 xy, float z, Color color, bool emissive)
            => mesh.Vertices.Add(new BlockMeshVertex
            {
                Position = new Vector3(xy.X, xy.Y, z),
                TextureCoordinates = new Vector2(0.5f),
                Color = color,
                IsEmissive = emissive
            });

        public override void DrawBlock(PrimitivesRenderer3D renderer, int value, Color color, float size,
            ref Matrix matrix, DrawBlockEnvironmentData environment)
        {
            BlocksManager.DrawMeshBlock(renderer, m_body, m_white, color, size, ref matrix, environment);
            bool lit = IsOn(value) && environment?.DrawBlockMode != DrawBlockMode.World;
            BlocksManager.DrawMeshBlock(renderer, lit ? m_brightLens : m_darkLens, m_white, color, size, ref matrix, environment);
        }
    }
}

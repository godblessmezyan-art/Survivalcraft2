#pragma warning disable CS1591

using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;

namespace SCIEPort.Features.Geology
{
    public sealed class PeakSandstoneBlock : GeologyCube
    {
        public const int Index = 892;
        internal const int TextureSlot = 21;

        public override int GetFaceTextureSlot(int face, int value) => face >= 4 ? 22 : TextureSlot;
    }

    public sealed class MossyPeakRockBlock : GeologyCube
    {
        public const int Index = 893;

        public override int GetFaceTextureSlot(int face, int value) => face == 4 ? 24 : face == 5 ? 22 : 23;
    }

    public sealed class PeakStoneStairsBlock : StairsBlock
    {
        public const int Index = 894;

        public PeakStoneStairsBlock() : base(PeakSandstoneBlock.TextureSlot) { }

        public override string GetCategory(int value) => Inventory.PortInventoryRules.Structures;

        public override void Initialize()
        {
            _ = GeologyCube.Atlas;
            base.Initialize();
        }

        public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry,
            int value, int x, int y, int z)
            => base.GenerateTerrainVertices(generator, geometry.GetGeometry(GeologyCube.Atlas), value, x, y, z);

        public override void DrawBlock(PrimitivesRenderer3D renderer, int value, Color color, float size,
            ref Matrix matrix, DrawBlockEnvironmentData environment)
        {
            int? paint = GetColor(Terrain.ExtractData(value));
            Color tint = paint.HasValue ? color * SubsystemPalette.GetColor(environment, paint) : color;
            BlockMesh mesh = paint.HasValue ? m_standaloneColoredBlockMesh : m_standaloneUncoloredBlockMesh;
            BlocksManager.DrawMeshBlock(renderer, mesh, GeologyCube.Atlas, tint, size, ref matrix, environment);
        }

        public override IEnumerable<CraftingRecipe> GetProceduralCraftingRecipes()
        {
            if (!(BlocksManager.Blocks[PeakSandstoneBlock.Index] is PeakSandstoneBlock stone) ||
                string.IsNullOrEmpty(stone.CraftingId)) yield break;

            string rock = stone.CraftingId;
            yield return new CraftingRecipe
            {
                ResultValue = Terrain.MakeBlockValue(Index),
                ResultCount = 4,
                Ingredients = new[] { rock, null, null, rock, rock, null, rock, rock, rock },
                Description = "将叠纹石英砂岩加工成峰纹石阶。"
            };
        }
    }
}

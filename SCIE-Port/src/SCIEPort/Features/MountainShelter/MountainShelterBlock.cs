using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;
using SCIEPort.Features.Flax;

namespace SCIEPort.Features.MountainShelter
{
    /// <summary>A foldable shelter with terrain-backed floors, walls, doors and lantern.</summary>
    public sealed class MountainShelterBlock : Block
    {
        /// <summary>Shared contents ID for the item and its deployed parts.</summary>
        public const int Index = 947;
        internal static int ItemValue => Terrain.MakeBlockValue(Index);
        private readonly Dictionary<int, BlockMesh> meshes = new Dictionary<int, BlockMesh>();
        private static readonly Dictionary<int, BoundingBox[]> boxes = MakeBoxes();
        private Texture2D texture;

        /// <inheritdoc/>
        public override void Initialize()
        {
            texture = new Texture2D(1, 1, 1, ColorFormat.Rgba8888);
            texture.SetData(0, new byte[] { 255, 255, 255, 255 });
            meshes[0] = ShelterMesh.Folded();
            foreach (int data in boxes.Keys)
            {
                ShelterLayout.Decode(data, out int part, out int rotation, out bool closed, out bool lit);
                meshes[data] = ShelterMesh.Part(part, rotation, closed, lit);
            }
            base.Initialize();
        }

        private static Dictionary<int, BoundingBox[]> MakeBoxes()
        {
            var result = new Dictionary<int, BoundingBox[]>();
            for (int part = 0; part < ShelterLayout.CellCount; part++)
            for (int rotation = 0; rotation < 4; rotation++)
            {
                result.Add(ShelterLayout.Encode(part, rotation), ShelterMesh.Boxes(part, rotation, false));
                if (ShelterLayout.DoorPart(part))
                    result.Add(ShelterLayout.Encode(part, rotation, true), ShelterMesh.Boxes(part, rotation, true));
                if (ShelterLayout.LampPart(part))
                    result.Add(ShelterLayout.Encode(part, rotation, lit: true), result[ShelterLayout.Encode(part, rotation)]);
            }
            return result;
        }

        /// <inheritdoc/>
        public override IEnumerable<int> GetCreativeValues() { yield return ItemValue; }

        /// <inheritdoc/>
        public override IEnumerable<CraftingRecipe> GetProceduralCraftingRecipes()
        {
            string flax = BlocksManager.Blocks[FlaxFiberBlock.Index].GetCraftingId(0);
            string bamboo = BlocksManager.Blocks[SCIEPort.Features.Bamboo.BambooBlock.Index].GetCraftingId(0);
            string torch = BlocksManager.Blocks[TorchBlock.Index].GetCraftingId(0);
            yield return new CraftingRecipe
            {
                ResultValue = ItemValue,
                ResultCount = 1,
                Description = "双人山地庇护帐",
                Ingredients = new[] { bamboo, flax, bamboo, flax, torch, flax, bamboo, flax, bamboo }
            };
        }

        /// <inheritdoc/>
        public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
            => boxes.TryGetValue(Terrain.ExtractData(value), out var result) ? result : Compatibility.BlockRaycastSafety.NonSolid;

        /// <inheritdoc/>
        public override BoundingBox[] GetCustomInteractionBoxes(SubsystemTerrain terrain, int value) => GetCustomCollisionBoxes(terrain, value);

        /// <inheritdoc/>
        public override int GetEmittedLightAmount(int value)
            => ShelterLayout.Decode(Terrain.ExtractData(value), out int part, out _, out _, out bool lit)
                && part == ShelterLayout.LightSource && lit ? 12 : 0;

        /// <inheritdoc/>
        public override bool IsFaceTransparent(SubsystemTerrain terrain, int face, int value) => true;

        /// <inheritdoc/>
        public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
        {
            if (Terrain.ExtractData(value) != 0 && meshes.TryGetValue(Terrain.ExtractData(value), out var mesh))
                generator.GenerateShadedMeshVertices(this, x, y, z, mesh, Color.White, null, null, geometry.GetGeometry(texture).SubsetOpaque);
        }

        /// <inheritdoc/>
        public override void DrawBlock(PrimitivesRenderer3D renderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environment)
            => BlocksManager.DrawMeshBlock(renderer, meshes[0], texture, color, size, ref matrix, environment);

        /// <inheritdoc/>
        public override void GetDropValues(SubsystemTerrain terrain, int oldValue, int newValue, int toolLevel, List<BlockDropValue> drops, out bool showDebris)
            => showDebris = false;
    }
}

#pragma warning disable CS1591

using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;
using SCIEPort.Features.Plants;

namespace SCIEPort.Features.Geology
{
    public abstract class GeologyCube : CubeBlock
    {
        private static Texture2D atlas;
        internal static Texture2D Atlas => atlas ?? (atlas = ContentManager.Get<Texture2D>("Textures/Geology"));

        public override void Initialize()
        {
            _ = Atlas;
            base.Initialize();
        }

        public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry,
            int value, int x, int y, int z)
            => base.GenerateTerrainVertices(generator, geometry.GetGeometry(Atlas), value, x, y, z);

        public override void DrawBlock(PrimitivesRenderer3D renderer, int value, Color color, float size,
            ref Matrix matrix, DrawBlockEnvironmentData environment)
            => BlocksManager.DrawCubeBlock(renderer, value, new Vector3(size), ref matrix, color, color, environment, Atlas);

        public override BlockDebrisParticleSystem CreateDebrisParticleSystem(SubsystemTerrain terrain,
            Vector3 position, int value, float strength)
            => TreeBlockRendering.Debris(this, Atlas, terrain, position, value, strength);

        internal static bool Registered => BlocksManager.Blocks[VolcanicRockBlock.Index] is VolcanicRockBlock
            && BlocksManager.Blocks[BasaltPillarBlock.Index] is BasaltPillarBlock
            && BlocksManager.Blocks[VolcanicSandBlock.Index] is VolcanicSandBlock
            && BlocksManager.Blocks[TravertineBlock.Index] is TravertineBlock
            && BlocksManager.Blocks[PaintedStrataBlock.Index] is PaintedStrataBlock
            && BlocksManager.Blocks[KarstLimestoneBlock.Index] is KarstLimestoneBlock
            && BlocksManager.Blocks[BlueIceBlock.Index] is BlueIceBlock
            && BlocksManager.Blocks[SaltCrustBlock.Index] is SaltCrustBlock
            && BlocksManager.Blocks[MineralWaterBlock.Index] is MineralWaterBlock;
    }

    public sealed class VolcanicRockBlock : GeologyCube { public const int Index = 1005; }

    public sealed class BasaltPillarBlock : GeologyCube
    {
        public const int Index = 1006;
        public override int GetFaceTextureSlot(int face, int value) => face >= 4 ? 2 : base.GetFaceTextureSlot(face, value);
    }

    public sealed class VolcanicSandBlock : GeologyCube { public const int Index = 1007; }
    public sealed class TravertineBlock : GeologyCube { public const int Index = 1008; }

    public sealed class PaintedStrataBlock : GeologyCube
    {
        public const int Index = 1009;
        private static readonly string[] Names = { "赭红彩岩", "橙黄彩岩", "灰蓝彩岩", "浅金彩岩" };

        public override int GetFaceTextureSlot(int face, int value) => DefaultTextureSlot + (Terrain.ExtractData(value) & 3);
        public override string GetDisplayName(SubsystemTerrain terrain, int value) => Names[Terrain.ExtractData(value) & 3];

        public override IEnumerable<int> GetCreativeValues()
        {
            for (int band = 0; band < Names.Length; band++) yield return Terrain.MakeBlockValue(Index, 0, band);
        }

        public override void GetDropValues(SubsystemTerrain terrain, int oldValue, int newValue, int toolLevel,
            List<BlockDropValue> drops, out bool debris)
        {
            debris = true;
            if (toolLevel >= RequiredToolLevel)
                drops.Add(new BlockDropValue { Value = Terrain.MakeBlockValue(Index, 0, Terrain.ExtractData(oldValue) & 3), Count = 1 });
        }
    }

    public sealed class KarstLimestoneBlock : GeologyCube { public const int Index = 1010; }
    public sealed class BlueIceBlock : GeologyCube { public const int Index = 1011; }
    public sealed class SaltCrustBlock : GeologyCube { public const int Index = 1012; }

    public sealed class MineralWaterBlock : WaterBlock
    {
        public new const int Index = 1013;

        private static readonly Color[] Colors =
        {
            new Color(212, 233, 226), new Color(52, 166, 169), new Color(158, 63, 69), new Color(113, 173, 101)
        };

        internal static int Make(int color, int level = 0)
            => Terrain.MakeBlockValue(Index, 0, FluidBlock.SetLevel((color & 3) << 5, level));

        public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry,
            int value, int x, int y, int z)
        {
            Color side = Colors[(Terrain.ExtractData(value) >> 5) & 3];
            Color top = side;
            top.A = 0;
            GenerateFluidTerrainVertices(generator, value, x, y, z, side, top, geometry.TransparentSubsetsByFace);
        }
    }
}

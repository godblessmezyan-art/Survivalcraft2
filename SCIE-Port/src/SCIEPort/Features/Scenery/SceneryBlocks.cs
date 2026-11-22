#pragma warning disable CS1591

using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;

namespace SCIEPort.Features.Scenery
{
    /// <summary>
    /// Clean-room 512x512 atlas laid out as a 16x16 grid of 32x32 tiles,
    /// matching the audited SCNET custom texture layout.
    /// </summary>
    internal static class SceneryTextures
    {
        private static Texture2D s_atlas;

        public const int HullSide = 0;
        public const int HullTop = 1;
        public const int EnvelopeCream = 2;
        public const int EnvelopeTeal = 3;
        public const int EnvelopeCrimson = 4;
        public const int Cloudstone = 5;
        public const int CrystalCyan = 6;
        public const int CrystalViolet = 7;
        public const int Brass = 8;
        public const int Sail = 9;

        public static Texture2D Atlas
        {
            get
            {
                if (s_atlas == null)
                {
                    s_atlas = ContentManager.Get<Texture2D>("Textures/Scenery");
                }
                return s_atlas;
            }
        }

        public static BlockDebrisParticleSystem CreateDebris(
            Block block,
            SubsystemTerrain terrain,
            Vector3 position,
            int value,
            float strength)
        {
            return new BlockDebrisParticleSystem(
                terrain,
                position,
                strength,
                block.DestructionDebrisScale,
                Color.White,
                block.GetFaceTextureSlot(4, value));
        }
    }

    public abstract class SceneryCubeBlock : Block
    {
        public override int GetFaceTextureSlot(int face, int value)
        {
            return Textures(face, Terrain.ExtractData(value));
        }

        public override void GenerateTerrainVertices(
            BlockGeometryGenerator generator,
            TerrainGeometry geometry,
            int value,
            int x,
            int y,
            int z)
        {
            TerrainGeometrySubset opaque = geometry.GetGeometry(SceneryTextures.Atlas).SubsetOpaque;
            generator.GenerateCubeVertices(
                this,
                value,
                x,
                y,
                z,
                Color.White,
                new[] { opaque, opaque, opaque, opaque, opaque, opaque });
        }

        public override void DrawBlock(
            PrimitivesRenderer3D primitivesRenderer,
            int value,
            Color color,
            float size,
            ref Matrix matrix,
            DrawBlockEnvironmentData environmentData)
        {
            BlocksManager.DrawCubeBlock(
                primitivesRenderer,
                value,
                new Vector3(size, size, size),
                ref matrix,
                color,
                color,
                environmentData,
                SceneryTextures.Atlas);
        }

        public override BlockDebrisParticleSystem CreateDebrisParticleSystem(
            SubsystemTerrain subsystemTerrain,
            Vector3 position,
            int value,
            float strength)
        {
            return SceneryTextures.CreateDebris(this, subsystemTerrain, position, value, strength);
        }

        public override void GetDropValues(
            SubsystemTerrain subsystemTerrain,
            int oldValue,
            int newValue,
            int toolLevel,
            List<BlockDropValue> dropValues,
            out bool showDebris)
        {
            showDebris = true;
            dropValues.Add(new BlockDropValue
            {
                Value = Terrain.MakeBlockValue(BlockIndex, 0, Terrain.ExtractData(oldValue)),
                Count = 1
            });
        }

        protected abstract int Textures(int face, int data);
    }

    public sealed class SceneryCloudstoneBlock : SceneryCubeBlock
    {
        public const int Index = 880;

        protected override int Textures(int face, int data)
        {
            return SceneryTextures.Cloudstone;
        }

        public override IEnumerable<int> GetCreativeValues()
        {
            yield return Terrain.MakeBlockValue(Index);
        }
    }

    public sealed class SceneryHullBlock : SceneryCubeBlock
    {
        public const int Index = 881;

        protected override int Textures(int face, int data)
        {
            return face == 4 ? SceneryTextures.HullTop : SceneryTextures.HullSide;
        }

        public override IEnumerable<int> GetCreativeValues()
        {
            yield return Terrain.MakeBlockValue(Index);
        }
    }

    public sealed class SceneryEnvelopeBlock : SceneryCubeBlock
    {
        public const int Index = 882;

        protected override int Textures(int face, int data)
        {
            int variant = data < 0 ? 0 : data > 2 ? 2 : data;
            return SceneryTextures.EnvelopeCream + variant;
        }

        public override IEnumerable<int> GetCreativeValues()
        {
            for (int data = 0; data <= 2; data++)
            {
                yield return Terrain.MakeBlockValue(Index, 0, data);
            }
        }
    }

    public sealed class SceneryCrystalBlock : SceneryCubeBlock
    {
        public const int Index = 883;

        protected override int Textures(int face, int data)
        {
            return data == 0 ? SceneryTextures.CrystalCyan : SceneryTextures.CrystalViolet;
        }

        public override int GetEmittedLightAmount(int value)
        {
            return Terrain.ExtractData(value) == 0 ? 12 : 10;
        }

        public override IEnumerable<int> GetCreativeValues()
        {
            yield return Terrain.MakeBlockValue(Index, 0, 0);
            yield return Terrain.MakeBlockValue(Index, 0, 1);
        }
    }

    public sealed class SceneryBrassBlock : SceneryCubeBlock
    {
        public const int Index = 884;

        protected override int Textures(int face, int data)
        {
            return SceneryTextures.Brass;
        }

        public override IEnumerable<int> GetCreativeValues()
        {
            yield return Terrain.MakeBlockValue(Index);
        }
    }

    public sealed class ScenerySailBlock : SceneryCubeBlock
    {
        public const int Index = 885;

        protected override int Textures(int face, int data)
        {
            return SceneryTextures.Sail;
        }

        public override IEnumerable<int> GetCreativeValues()
        {
            yield return Terrain.MakeBlockValue(Index);
        }
    }
}

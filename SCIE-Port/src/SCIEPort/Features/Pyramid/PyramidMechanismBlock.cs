using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;
using SCIEPort.Features.Plants;

namespace SCIEPort.Features.Pyramid
{
    /// <summary>Sandstone seals, reliefs and lamps generated inside the pyramid.</summary>
    public sealed class PyramidMechanismBlock : CubeBlock
    {
        /// <summary>Stable world-only mechanism contents ID.</summary>
        public const int Index = 1023;
        internal const int Lit = 3, Clue = 6, Gate = 9, Seal = 10, Lamp = 11, Trim = 12;
        private static Texture2D atlas;
        private static Texture2D Atlas => atlas ?? (atlas = ContentManager.Get<Texture2D>("Textures/PyramidMechanism"));

        internal static int Value(int data) => Terrain.MakeBlockValue(Index, 0, data);

        /// <inheritdoc/>
        public override void Initialize()
        {
            _ = Atlas;
            base.Initialize();
        }

        /// <inheritdoc/>
        public override IEnumerable<int> GetCreativeValues() { yield break; }

        /// <inheritdoc/>
        public override bool IsInteractive(SubsystemTerrain terrain, int value)
        {
            int data = Terrain.ExtractData(value);
            return Terrain.ExtractContents(value) == Index && data >= 0 && data < Lit;
        }

        /// <inheritdoc/>
        public override string GetDisplayName(SubsystemTerrain terrain, int value)
        {
            int data = Terrain.ExtractData(value);
            if (data == Gate) return "沉沙石门";
            if (data == Seal) return "藏器坛";
            if (data == Lamp) return "石灯";
            if (data >= Clue) return "日阶浮雕";
            return "日阶封印";
        }

        /// <inheritdoc/>
        public override int GetEmittedLightAmount(int value)
        {
            int data = Terrain.ExtractData(value);
            if (data == Lamp) return 13;
            if (data >= Lit && data < Clue) return 11;
            return data >= 0 && data < Gate ? 6 : 0;
        }

        /// <inheritdoc/>
        public override int GetFaceTextureSlot(int face, int value)
        {
            int data = Terrain.ExtractData(value);
            return data >= 0 && data <= Trim ? data : Trim;
        }

        /// <inheritdoc/>
        public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry,
            int value, int x, int y, int z) =>
            base.GenerateTerrainVertices(generator, geometry.GetGeometry(Atlas), value, x, y, z);

        /// <inheritdoc/>
        public override void DrawBlock(PrimitivesRenderer3D renderer, int value, Color color, float size,
            ref Matrix matrix, DrawBlockEnvironmentData environment) =>
            BlocksManager.DrawCubeBlock(renderer, value, new Vector3(size), ref matrix, color, color, environment, Atlas);

        /// <inheritdoc/>
        public override BlockDebrisParticleSystem CreateDebrisParticleSystem(SubsystemTerrain terrain,
            Vector3 position, int value, float strength) =>
            TreeBlockRendering.Debris(this, Atlas, terrain, position, value, strength);

        /// <inheritdoc/>
        public override void GetDropValues(SubsystemTerrain terrain, int oldValue, int newValue, int toolLevel,
            List<BlockDropValue> drops, out bool debris) => debris = true;
    }
}

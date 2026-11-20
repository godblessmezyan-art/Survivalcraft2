using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;
using SCIEPort.Features.Plants;

namespace SCIEPort.Features.TidePalace
{
    /// <summary>Palace-only stone mechanisms and embossed clues.</summary>
    public sealed class TidePalaceBlock : CubeBlock
    {
        /// <summary>Stable terrain identity for all palace mechanism forms.</summary>
        public const int Index = 1022;
        internal const int Weight = 9;
        internal const int LiftedWeight = 10;
        internal const int Signal = 11;
        internal const int LitSignal = 12;
        internal const int Gate = 13;
        internal const int Clue = 14;
        internal const int Seal = 17;
        internal const int Lamp = 18;

        private static Texture2D atlas;
        private static Texture2D Atlas => atlas ?? (atlas = ContentManager.Get<Texture2D>("Textures/TidePalace"));

        internal static int Value(int data) => Terrain.MakeBlockValue(Index, 0, data);
        internal static bool Matches(int value, int first, int last)
        {
            int data = Terrain.ExtractData(value);
            return Terrain.ExtractContents(value) == Index && data >= first && data <= last;
        }

        /// <inheritdoc/>
        public override void Initialize()
        {
            _ = Atlas;
            base.Initialize();
        }

        /// <inheritdoc/>
        public override IEnumerable<int> GetCreativeValues() { yield break; }

        /// <inheritdoc/>
        public override bool IsInteractive(SubsystemTerrain terrain, int value) => Matches(value, 0, Weight);

        /// <inheritdoc/>
        public override string GetDisplayName(SubsystemTerrain terrain, int value)
        {
            int data = Terrain.ExtractData(value);
            if (data < Weight) return "潮轮";
            if (data <= LiftedWeight) return "配重";
            if (data == Gate) return "潮闸";
            if (data == Seal) return "藏器坛";
            if (data == Lamp) return "潮灯";
            return "潮纹石";
        }

        /// <inheritdoc/>
        public override int GetEmittedLightAmount(int value) => Terrain.ExtractData(value) == Lamp ? 13 :
            Terrain.ExtractData(value) == LitSignal ? 11 : Terrain.ExtractData(value) < Gate ? 6 : 0;

        /// <inheritdoc/>
        public override int GetFaceTextureSlot(int face, int value)
        {
            int data = Terrain.ExtractData(value);
            if (data < Weight) return face == 5 ? 0 : 1 + data % 3;
            return data <= Lamp ? data - 5 : 0;
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

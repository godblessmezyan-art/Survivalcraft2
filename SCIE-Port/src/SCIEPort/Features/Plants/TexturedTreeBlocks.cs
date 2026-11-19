#pragma warning disable CS1591

using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;

namespace SCIEPort.Features.Plants
{
    public abstract class TexturedTreeWoodBlock : WoodBlock
    {
        internal const int GrownRoot = 4;

        protected TexturedTreeWoodBlock(int endGrain, int bark) : base(endGrain, bark) { }

        protected abstract Texture2D Atlas { get; }
        protected abstract int SaplingIndex { get; }

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

        public override void GetDropValues(SubsystemTerrain terrain, int oldValue, int newValue, int toolLevel,
            List<BlockDropValue> drops, out bool debris)
        {
            base.GetDropValues(terrain, Terrain.ReplaceData(oldValue, 0), newValue, toolLevel, drops, out debris);
            if ((Terrain.ExtractData(oldValue) & GrownRoot) != 0)
                drops.Add(new BlockDropValue { Value = Terrain.MakeBlockValue(SaplingIndex), Count = 1 });
        }

        public override BlockDebrisParticleSystem CreateDebrisParticleSystem(SubsystemTerrain terrain,
            Vector3 position, int value, float strength)
            => TreeBlockRendering.Debris(this, Atlas, terrain, position, value, strength);
    }

    public abstract class TexturedTreeLeavesBlock : LeavesBlock24
    {
        protected abstract Texture2D Atlas { get; }

        public override Color GetLeavesBlockColor(int value, Terrain terrain, int x, int y, int z) => Color.White;
        public override Color GetLeavesItemColor(int value, DrawBlockEnvironmentData environment) => Color.White;

        public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry,
            int value, int x, int y, int z)
            => base.GenerateTerrainVertices(generator, geometry.GetGeometry(Atlas), value, x, y, z);

        public override void DrawBlock(PrimitivesRenderer3D renderer, int value, Color color, float size,
            ref Matrix matrix, DrawBlockEnvironmentData environment)
            => BlocksManager.DrawCubeBlock(renderer, value, new Vector3(size), ref matrix, color, color, environment, Atlas);

        public override BlockPlacementData GetPlacementValue(SubsystemTerrain terrain, ComponentMiner miner,
            int value, TerrainRaycastResult hit)
            => base.GetPlacementValue(terrain, miner, Terrain.ReplaceData(value, 0), hit);

        public override BlockDebrisParticleSystem CreateDebrisParticleSystem(SubsystemTerrain terrain,
            Vector3 position, int value, float strength)
            => TreeBlockRendering.Debris(this, Atlas, terrain, position, value, strength);
    }

    internal static class TreeBlockRendering
    {
        internal static BlockDebrisParticleSystem Debris(Block block, Texture2D atlas, SubsystemTerrain terrain,
            Vector3 position, int value, float strength)
            => new BlockDebrisParticleSystem(terrain, position, strength, block.DestructionDebrisScale, Color.White,
                block.GetFaceTextureSlot(4, value)) { Texture = atlas };
    }
}

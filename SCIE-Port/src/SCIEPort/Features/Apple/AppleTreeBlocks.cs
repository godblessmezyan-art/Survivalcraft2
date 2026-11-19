#pragma warning disable CS1591

using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;

namespace SCIEPort.Features.Apple
{
    public sealed class AppleWoodBlock : WoodBlock
    {
        public const int Index = 978;
        internal const int GrownRoot = 4; // The native log axis occupies the low two bits.

        public AppleWoodBlock() : base(AppleTextures.EndGrainSlot, AppleTextures.BarkSlot) { }

        public override void Initialize()
        {
            // Load the shared atlas on the block-initialization thread.
            _ = AppleTextures.Atlas;
            base.Initialize();
        }

        public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry,
            int value, int x, int y, int z)
            => base.GenerateTerrainVertices(generator, geometry.GetGeometry(AppleTextures.Atlas), value, x, y, z);

        public override void DrawBlock(PrimitivesRenderer3D renderer, int value, Color color, float size,
            ref Matrix matrix, DrawBlockEnvironmentData environment)
            => BlocksManager.DrawCubeBlock(renderer, value, new Vector3(size), ref matrix, color, color,
                environment, AppleTextures.Atlas);

        public override void GetDropValues(SubsystemTerrain terrain, int oldValue, int newValue, int toolLevel,
            List<BlockDropValue> drops, out bool debris)
        {
            // The native drop also resets the cut direction. Never move a living root into inventory.
            base.GetDropValues(terrain, Terrain.ReplaceData(oldValue, 0), newValue, toolLevel, drops, out debris);
            if ((Terrain.ExtractData(oldValue) & GrownRoot) != 0)
                drops.Add(new BlockDropValue { Value = Terrain.MakeBlockValue(ApplePlantBlock.Index), Count = 1 });
        }

        public override BlockDebrisParticleSystem CreateDebrisParticleSystem(SubsystemTerrain terrain,
            Vector3 position, int value, float strength) => AppleTextures.CreateDebris(this, terrain, position, value, strength);
    }

    public abstract class AppleCanopyBlock : LeavesBlock24
    {
        public override Color GetLeavesBlockColor(int value, Terrain terrain, int x, int y, int z) => Color.White;

        public override Color GetLeavesItemColor(int value, DrawBlockEnvironmentData environment) => Color.White;

        public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry,
            int value, int x, int y, int z)
            => base.GenerateTerrainVertices(generator, geometry.GetGeometry(AppleTextures.Atlas), value, x, y, z);

        public override void DrawBlock(PrimitivesRenderer3D renderer, int value, Color color, float size,
            ref Matrix matrix, DrawBlockEnvironmentData environment)
            => BlocksManager.DrawCubeBlock(renderer, value, new Vector3(size), ref matrix, color, color,
                environment, AppleTextures.Atlas);

        public override BlockPlacementData GetPlacementValue(SubsystemTerrain terrain, ComponentMiner miner,
            int value, TerrainRaycastResult hit)
            => base.GetPlacementValue(terrain, miner, Terrain.ReplaceData(value, 0), hit);

        public override BlockDebrisParticleSystem CreateDebrisParticleSystem(SubsystemTerrain terrain,
            Vector3 position, int value, float strength) => AppleTextures.CreateDebris(this, terrain, position, value, strength);
    }

    public sealed class AppleLeavesBlock : AppleCanopyBlock
    {
        public const int Index = 979;

        public override int GetFaceTextureSlot(int face, int value)
        {
            int stage = AppleRules.GetSize(Terrain.ExtractData(value));
            if (face >= 4) return AppleTextures.LeavesSlot;
            if (stage == 1) return AppleTextures.BlossomSlot;
            return stage >= 3 ? AppleTextures.GreenFruitSlot : AppleTextures.LeavesSlot;
        }
    }

    public sealed class AppleFruitingLeavesBlock : AppleCanopyBlock
    {
        public const int Index = 980;

        public override int GetFaceTextureSlot(int face, int value)
            => face < 4 ? AppleTextures.RipeFruitSlot : AppleTextures.LeavesSlot;

        public override void GetDropValues(SubsystemTerrain terrain, int oldValue, int newValue, int toolLevel,
            List<BlockDropValue> drops, out bool debris)
        {
            drops.Add(new BlockDropValue { Value = Terrain.MakeBlockValue(AppleBlock.Index), Count = AppleRules.GetAppleYield() });
            debris = true;
        }
    }
}

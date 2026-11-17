#pragma warning disable CS1591

using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;
using SCIEPort.Features.Plants;

namespace SCIEPort.Features.Apple
{
    internal static class AppleTextures
    {
        private static Texture2D s_atlas;

        // Clean-room 512x512 RGBA atlas laid out as the confirmed 16x16 grid of 32x32
        // tiles (see docs/COMPATIBILITY.md). Apple plant growth stages occupy slots 0..7,
        // the fruiting (apples-on-branches) variant is slot 8 and the harvested apple
        // item is slot 9.
        public const int PlantSlotBase = 0;
        public const int FruitingSlot = 8;
        public const int AppleSlot = 9;

        public static Texture2D Atlas
        {
            get
            {
                if (s_atlas == null)
                {
                    s_atlas = ContentManager.Get<Texture2D>("Textures/Apple");
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

    public sealed class ApplePlantBlock : CrossBlock
    {
        private readonly PlantModel m_plantModel = new PlantModel();
        private static readonly BoundingBox[] s_trunkBoxes={new BoundingBox(new Vector3(.35f,0,.35f),new Vector3(.65f,1,.65f))};

        public override void Initialize()
        {
            m_plantModel.Initialize(BlockIndex);
            base.Initialize();
        }

        public const int Index = 939;

        public override int GetFaceTextureSlot(int face, int value)
        {
            int data = Terrain.ExtractData(value);
            if (AppleRules.GetFruiting(data))
            {
                return AppleTextures.FruitingSlot;
            }
            return AppleTextures.PlantSlotBase + AppleRules.GetSize(data);
        }

        public override int GetShadowStrength(int value)
        {
            return AppleRules.GetSize(Terrain.ExtractData(value)) + 1;
        }

        public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain,int value)
        {
            int data=Terrain.ExtractData(value),part=AppleTreeRules.Part(data);
            if(part==AppleTreeRules.Root||part==AppleTreeRules.Trunk)
                return s_trunkBoxes;
            return m_plantModel.Boxes(this,value);
        }

        public override void GenerateTerrainVertices(
            BlockGeometryGenerator generator,
            TerrainGeometry geometry,
            int value,
            int x,
            int y,
            int z)
        {
            m_plantModel.Generate(this, generator, geometry, value, x, y, z);
        }

        public override void DrawBlock(
            PrimitivesRenderer3D primitivesRenderer,
            int value,
            Color color,
            float size,
            ref Matrix matrix,
            DrawBlockEnvironmentData environmentData)
        {
            m_plantModel.Draw(this, primitivesRenderer, value, color, size, ref matrix, environmentData);
        }

        public override BlockDebrisParticleSystem CreateDebrisParticleSystem(
            SubsystemTerrain subsystemTerrain,
            Vector3 position,
            int value,
            float strength)
        {
            return AppleTextures.CreateDebris(this, subsystemTerrain, position, value, strength);
        }

        public override IEnumerable<int> GetCreativeValues()
        {
            yield return Terrain.MakeBlockValue(Index);
        }

        public override BlockPlacementData GetPlacementValue(
            SubsystemTerrain subsystemTerrain,
            ComponentMiner componentMiner,
            int value,
            TerrainRaycastResult raycastResult)
        {
            return ApplePlacement.GetPlacementValue(subsystemTerrain, raycastResult);
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
            int data = Terrain.ExtractData(oldValue);

            // A ripe, fruiting plant yields edible apples; this mirrors the audited
            // WildBerry behaviour where breaking a fruiting bush hands you the fruit.
            if (AppleRules.GetFruiting(data) && (AppleTreeRules.Part(data) == AppleTreeRules.Leaf || AppleTreeRules.Part(data) == 0))
            {
                dropValues.Add(new BlockDropValue
                {
                    Value = Terrain.MakeBlockValue(AppleBlock.Index),
                    Count = AppleRules.GetAppleYield()
                });
            }

            // A mature plant also yields one replantable young plant (the stage-zero
            // item) so harvesting a stand does not strip the field bare.
            if (AppleTreeRules.Part(data) == 0 || AppleTreeRules.Part(data) == AppleTreeRules.Root)
            {
                dropValues.Add(new BlockDropValue
                {
                    Value = Terrain.MakeBlockValue(ApplePlantBlock.Index, 0, AppleRules.SetSize(0, 0)),
                    Count = AppleRules.GetReplantSaplingYield()
                });
            }
        }
    }

    public sealed class AppleBlock : FlatBlock
    {
        private readonly PlantModel m_plantModel = new PlantModel();

        public override void Initialize()
        {
            m_plantModel.Initialize(BlockIndex);
            base.Initialize();
        }

        public const int Index = 940;

        public override bool IsFaceTransparent(SubsystemTerrain terrain,int face,int value) => true;

        public override BlockPlacementData GetPlacementValue(SubsystemTerrain terrain,ComponentMiner miner,int value,TerrainRaycastResult hit)
            => new BlockPlacementData { Value=0,CellFace=hit.CellFace };

        public override void GetDropValues(SubsystemTerrain terrain,int oldValue,int newValue,int toolLevel,List<BlockDropValue> drops,out bool debris)
        {
            debris=true;
            drops.Add(new BlockDropValue {Value=Terrain.MakeBlockValue(Index),Count=1});
        }

        public override int GetFaceTextureSlot(int face, int value)
        {
            return AppleTextures.AppleSlot;
        }

        public override void GenerateTerrainVertices(
            BlockGeometryGenerator generator,
            TerrainGeometry geometry,
            int value,
            int x,
            int y,
            int z)
        {
            m_plantModel.Generate(this, generator, geometry, value, x, y, z);
        }

        public override void DrawBlock(
            PrimitivesRenderer3D primitivesRenderer,
            int value,
            Color color,
            float size,
            ref Matrix matrix,
            DrawBlockEnvironmentData environmentData)
        {
            m_plantModel.Draw(this, primitivesRenderer, value, color, size, ref matrix, environmentData);
        }

        public override BlockDebrisParticleSystem CreateDebrisParticleSystem(
            SubsystemTerrain subsystemTerrain,
            Vector3 position,
            int value,
            float strength)
        {
            return AppleTextures.CreateDebris(this, subsystemTerrain, position, value, strength);
        }

        public override IEnumerable<int> GetCreativeValues()
        {
            yield return Terrain.MakeBlockValue(Index);
        }
    }

    internal static class ApplePlacement
    {
        public static BlockPlacementData GetPlacementValue(
            SubsystemTerrain subsystemTerrain,
            TerrainRaycastResult raycastResult)
        {
            CellFace target = raycastResult.CellFace;
            Point3 direction = CellFace.FaceToPoint3(target.Face);
            bool isSoil = SubsystemAppleBlockBehavior.IsGround(Terrain.ExtractContents(
                subsystemTerrain.Terrain.GetCellValue(target.X, target.Y, target.Z)));
            if (AppleRules.CanPlaceApple(direction.Y == 1, isSoil))
            {
                return new BlockPlacementData
                {
                    Value = Terrain.MakeBlockValue(ApplePlantBlock.Index, 0, AppleRules.SetSize(0, 0)),
                    CellFace = target
                };
            }

            return new BlockPlacementData { Value = 0, CellFace = target };
        }
    }
}

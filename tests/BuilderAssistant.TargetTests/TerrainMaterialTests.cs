using System;
using BuilderAssistant.Core.Blueprints;
using BuilderAssistant.Features.WorldEdit;
using Game;

internal static partial class TargetTests
{
    private static void TestTerrainMaterialSupport()
    {
        for(int variant=0;variant<8;variant++)for(int axis=0;axis<2;axis++)
        {
            int d=GravestoneBlock.SetRotation(GravestoneBlock.SetVariant(0,variant),axis);
            Check(d==variant+(axis<<3) && BlockOrientation.IsValidAuthoredData(BlockFamily.Gravestone,d),"grave variant and axis DATA");
            Check(GravestoneBlock.GetVariant(BlockOrientation.Rotate(BlockFamily.Gravestone,d,1))==variant &&
                GravestoneBlock.GetRotation(BlockOrientation.Rotate(BlockFamily.Gravestone,d,1))==1-axis,"grave quarter-turn preserves variant");
        }
        for(int n=0;n<4;n++)for(int wet=0;wet<2;wet++)
        {
            int d=SoilBlock.SetNitrogen(SoilBlock.SetHydration(0,wet!=0),n);
            Check(d==2*n+wet && BlockOrientation.IsValidAuthoredData(BlockFamily.Soil,d),"native soil moisture/fertility DATA");
        }
        VanillaBlockCapability grass,flower;
        VanillaBlockCapabilities.TryGetByTypeName("TallGrassBlock",out grass);
        VanillaBlockCapabilities.TryGetByTypeName("RedFlowerBlock",out flower);
        Check(TallGrassBlock.SetIsSmall(0,true)==8 && grass.IsValidData(8) && !grass.IsValidData(1),"tall-grass small bit is 8, not flower bit 1");
        Check(FlowerBlock.SetIsSmall(0,true)==1 && flower.IsValidData(1) && !flower.IsValidData(8),"flower small bit is 1");
        // Native predicate fixtures: no game world, meshing, GPU or live network is initialized.
        Block[] saved=BlocksManager.m_blocks;
        try
        {
            var blocks=new Block[1024];BlocksManager.m_blocks=blocks;
            blocks[0]=new AirBlock { BlockIndex=0,IsTransparent=true,IsCollidable=false };
            blocks[2]=new DirtBlock { BlockIndex=2 };
            blocks[8]=new GrassBlock { BlockIndex=8 };
            blocks[7]=new SandBlock { BlockIndex=7 };
            blocks[168]=new SoilBlock { BlockIndex=168,IsTransparent=true };
            blocks[15]=new GlassBlock { BlockIndex=15,IsTransparent=true };
            blocks[21]=new PlanksBlock { BlockIndex=21 };
            blocks[14]=new SpruceLeavesBlock { BlockIndex=14,IsTransparent=true };
            blocks[94]=new WoodenFenceBlock { BlockIndex=94,IsTransparent=true };
            foreach(int id in new[] {0,2,8,7,168,15,21,14,94})
            {
                int v=Terrain.MakeBlockValue(id);
                Check(VanillaSupportRules.Supports(blocks[id],null,v,BlockFamily.Snow,4)==SubsystemSnowBlockBehavior.CanSupportSnow(v),"snow uses native support predicate "+id);
                Check(VanillaSupportRules.Supports(blocks[id],null,v,BlockFamily.Carpet,4)==!blocks[id].IsTransparent,"carpet native opaque support "+id);
                Check(VanillaSupportRules.Supports(blocks[id],null,v,BlockFamily.GroundCover,4)==SubsystemFallenLeavesBlockBehavior.CanSupportFallenLeaves(v),"leaf litter native support "+id);
                Check(VanillaSupportRules.Supports(blocks[id],null,v,BlockFamily.Plant,4)==(id==2||id==8||id==7||id==168),"plant soil types match audited native behavior "+id);
            }
            Check(VanillaSupportRules.Supports(blocks[21],null,Terrain.MakeBlockValue(21),BlockFamily.Falling,4) &&
                VanillaSupportRules.Supports(blocks[168],null,Terrain.MakeBlockValue(168),BlockFamily.Falling,4) &&
                !VanillaSupportRules.Supports(blocks[0],null,0,BlockFamily.Falling,4),"gravity support allows solid/farmland and rejects air");
            Check(VanillaSupportRules.Supports(blocks[94],null,Terrain.MakeBlockValue(94),BlockFamily.Lantern,4),"lantern native fence support exception");
            Check(new SnowBlock().GetCustomCollisionBoxes(null,0)[0].Max.Y==.125f,"snow is a fixed one-eighth layer, not DATA thickness");
        }
        finally { BlocksManager.m_blocks=saved; }
    }
}

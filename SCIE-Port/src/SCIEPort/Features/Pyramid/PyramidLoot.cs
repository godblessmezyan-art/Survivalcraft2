using System;
using Game;
using SCIEPort.Features.AbyssReaper;
using SCIEPort.Features.Blueprints;
using SCIEPort.Features.Codex;
using SCIEPort.Features.DevouringEye;
using SCIEPort.Features.Excalibur;
using SCIEPort.Features.Gungnir;
using SCIEPort.Features.Inventory;
using SCIEPort.Features.OmniscientEye;
using SCIEPort.Features.Stormhammer;

namespace SCIEPort.Features.Pyramid
{
    internal static class PyramidLoot
    {
        // Persist IDs rather than indexes into this table so future loot changes cannot
        // change the prize in a pyramid that has already been generated.
        private static readonly (int Index, Type Type)[] Pool =
        {
            (StormhammerAwakenedBlock.Index, typeof(StormhammerAwakenedBlock)),
            (StormhammerBlock.Index, typeof(StormhammerBlock)),
            (GungnirBlock.Index, typeof(GungnirBlock)),
            (OmniscientEyeBlock.Index, typeof(OmniscientEyeBlock)),
            (DevouringEyeBlock.Index, typeof(DevouringEyeBlock)),
            (AbyssReaperBlock.Index, typeof(AbyssReaperBlock)),
            (ExcaliburBlock.Index, typeof(ExcaliburBlock)),
            (ArtifactCodexBlock.Index, typeof(ArtifactCodexBlock)),
            (BlueprintToolBlock.Index, typeof(BlueprintToolBlock))
        };

        internal static int Draw(Game.Random random) => Pool[random.Int(0, Pool.Length - 1)].Index;

        internal static bool IsEligible(int id)
        {
            foreach (var item in Pool)
                if (item.Index == id) return true;
            return false;
        }

        internal static bool IsRegistered(int id)
        {
            foreach (var item in Pool)
            {
                if (item.Index != id) continue;
                Block block = BlocksManager.Blocks[id];
                int value = Terrain.MakeBlockValue(id);
                return block?.GetType() == item.Type && block.BlockIndex == id &&
                    block.GetCategory(value) == PortInventoryRules.Artifacts && block.GetMaxStacking(value) > 0;
            }
            return false;
        }
    }
}

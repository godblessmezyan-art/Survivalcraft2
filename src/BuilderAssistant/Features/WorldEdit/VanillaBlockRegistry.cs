using System;
using System.Collections.Generic;
using Game;
using BuilderAssistant.Core.Blueprints;

namespace BuilderAssistant.Features.WorldEdit
{
    /// <summary>
    /// Activates audited capabilities against the blocks actually registered by the
    /// running SCNET client. A documented type name alone is never enough: the exact
    /// runtime block and its expected base family must both match.
    /// </summary>
    internal static class VanillaBlockRegistry
    {
        internal sealed class Entry
        {
            public readonly VanillaBlockCapability Capability;
            public readonly Block Block;

            public Entry(VanillaBlockCapability capability, Block block)
            {
                Capability = capability;
                Block = block;
            }
        }

        private static Dictionary<string, Entry> _active;

        public static bool TryGet(string typeName, out Entry entry)
        {
            EnsureInitialized();
            return _active.TryGetValue(typeName ?? string.Empty, out entry);
        }

        public static void Reset()
        {
            _active = null;
        }

        private static void EnsureInitialized()
        {
            if (_active != null)
            {
                return;
            }

            var active = new Dictionary<string, Entry>(StringComparer.Ordinal);
            foreach (VanillaBlockCapability capability in VanillaBlockCapabilities.All)
            {
                Block block = BlocksManager.FindBlockByTypeName(capability.TypeName, false);
                if (block != null &&
                    string.Equals(block.GetType().Name, capability.TypeName, StringComparison.Ordinal) &&
                    MatchesRuntimeFamily(block, capability.Family) &&
                    (capability.Family != BlockFamily.Cube || capability.SupportsPaint == (block is PaintedCubeBlock)))
                {
                    active.Add(capability.TypeName, new Entry(capability, block));
                }
            }
            _active = active;
        }

        private static bool MatchesRuntimeFamily(Block block, BlockFamily family)
        {
            switch (family)
            {
                case BlockFamily.Cube: return block is CubeBlock;
                case BlockFamily.Stairs: return block is StairsBlock;
                case BlockFamily.Slab: return block is SlabBlock;
                case BlockFamily.Fence: return block is FenceBlock;
                case BlockFamily.Door: return block is DoorBlock;
                case BlockFamily.Log: return block is WoodBlock;
                case BlockFamily.Ladder: return block is LadderBlock;
                case BlockFamily.Torch: return block is TorchBlock;
                default: return false;
            }
        }
    }
}

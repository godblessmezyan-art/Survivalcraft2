using System;
using System.Collections.Generic;
using Game;
using BuilderAssistant.Core.Blueprints;
using BuilderAssistant.Core.Transactions;

namespace BuilderAssistant.Features.WorldEdit
{
    /// <summary>
    /// Resolves blueprint material keys through the audited capability catalog and the
    /// live runtime registry. No block index is hardcoded and no family/DATA mismatch is
    /// normalized into a different state.
    /// </summary>
    internal sealed class VanillaBlockPalette : IBlockValueResolver
    {
        /// <summary>Compatibility/readability view of the audited authoring catalog.</summary>
        public static readonly Dictionary<string, BlockFamily> SupportedBlocks = BuildSupportedBlocks();

        public static readonly HashSet<string> SupportedPlainCubes = BuildCubes();

        private static Dictionary<string, BlockFamily> BuildSupportedBlocks()
        {
            var result = new Dictionary<string, BlockFamily>(StringComparer.Ordinal);
            foreach (VanillaBlockCapability capability in VanillaBlockCapabilities.All)
            {
                result.Add(capability.TypeName, capability.Family);
            }
            return result;
        }

        private static HashSet<string> BuildCubes()
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, BlockFamily> pair in SupportedBlocks)
            {
                if (pair.Value == BlockFamily.Cube)
                {
                    result.Add(pair.Key);
                }
            }
            return result;
        }

        /// <summary>
        /// Look up an ACTIVE runtime capability. A type in the audit catalog is not
        /// exposed if the running client lacks it or registers it under another family.
        /// </summary>
        public static bool TryGetFamily(string typeName, out BlockFamily family)
        {
            family = BlockFamily.Cube;
            VanillaBlockRegistry.Entry entry;
            if (!VanillaBlockRegistry.TryGet(typeName, out entry))
            {
                return false;
            }
            family = entry.Capability.Family;
            return true;
        }

        public bool TryResolve(string materialKey, out int value)
        {
            VanillaBlockCapability capability;
            string typeName;
            if (!VanillaBlockCapabilities.TryGetByMaterialKey(materialKey, out capability, out typeName))
            {
                value = 0;
                return false;
            }
            string ignored;
            return TryResolve(materialKey, capability.Family, 0, out value, out ignored);
        }

        public bool TryResolve(string materialKey, int data, out int value)
        {
            VanillaBlockCapability capability;
            string typeName;
            if (!VanillaBlockCapabilities.TryGetByMaterialKey(materialKey, out capability, out typeName))
            {
                value = 0;
                return false;
            }
            string ignored;
            return TryResolve(materialKey, capability.Family, data, out value, out ignored);
        }

        public bool TryResolve(
            string materialKey,
            BlockFamily family,
            int data,
            out int value,
            out string error)
        {
            value = 0;
            error = null;

            VanillaBlockCapability capability;
            string typeName;
            if (!VanillaBlockCapabilities.TryGetByMaterialKey(materialKey, out capability, out typeName))
            {
                error = "未知或未开放的原版方块材料: " + (materialKey ?? "(null)");
                return false;
            }
            if (capability.Family != family)
            {
                error = "材料 " + materialKey + " 的实际族为 " +
                    BlockOrientation.FamilyToTag(capability.Family) + "，但蓝图声明 f=" +
                    BlockOrientation.FamilyToTag(family) + "。";
                return false;
            }
            if (!capability.IsValidData(data))
            {
                error = "材料 " + materialKey + " 的 DATA=" + data + " 非法（" +
                    BlockOrientation.FamilyToTag(family) + " 允许 " +
                    BlockOrientation.AuthoredDataRange(family) + "）。";
                return false;
            }

            VanillaBlockRegistry.Entry entry;
            if (!VanillaBlockRegistry.TryGet(typeName, out entry))
            {
                error = "当前客户端未按预期注册原版方块 " + typeName + "。";
                return false;
            }

            value = Terrain.MakeBlockValue(entry.Block.BlockIndex, 0, data);
            return true;
        }

        public bool IsResolvableKey(string materialKey)
        {
            int ignored;
            return TryResolve(materialKey, out ignored);
        }

        public bool TryValidateCell(BlueprintCell cell, out string error)
        {
            int ignored;
            return TryResolve(cell.MaterialKey, cell.Family, cell.Data, out ignored, out error);
        }

        public void Reset()
        {
            VanillaBlockRegistry.Reset();
        }
    }
}

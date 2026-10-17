using System.Collections.Generic;
using Game;
using BuilderAssistant.Core.Blueprints;
using BuilderAssistant.Core.Transactions;

namespace BuilderAssistant.Features.WorldEdit
{
    /// <summary>
    /// Resolves a blueprint LOGICAL material key to a real vanilla block value,
    /// and does so by name at runtime - never by a hardcoded index. It looks the
    /// block up through BlocksManager and reads its assigned BlockIndex, so if a
    /// named block does not exist in this build the key simply fails to resolve
    /// and the build plan aborts safely. All mapped blocks are plain cubes
    /// (Cobblestone/StoneBrick/Planks/Glass/Bricks) that need no orientation
    /// data bits, so the packed value is just the block index (data = 0).
    /// </summary>
    internal sealed class VanillaBlockPalette : IBlockValueResolver
    {
        // key -> CLR type name of a verified plain-cube vanilla block.
        private static readonly Dictionary<string, string> KeyToTypeName =
            new Dictionary<string, string>
            {
                { BuildingMaterials.StoneBase, "CobblestoneBlock" },  // index 5  (plain cube)
                { BuildingMaterials.StoneWall, "StoneBrickBlock" },  // index 26 (plain cube)
                { BuildingMaterials.WoodWall,  "PlanksBlock" },      // index 21 (plain cube)
                { BuildingMaterials.WoodTrim,  "PlanksBlock" },      // index 21
                { BuildingMaterials.Roof,      "BricksBlock" },      // index 73 (plain cube)
                { BuildingMaterials.Glass,     "GlassBlock" },       // index 15 (plain cube)
                { BuildingMaterials.Floor,     "PlanksBlock" },      // index 21
            };

        private readonly Dictionary<string, int> _resolved = new Dictionary<string, int>();

        public bool TryResolve(string materialKey, out int value)
        {
            int cached;
            if (_resolved.TryGetValue(materialKey, out cached))
            {
                value = cached;
                return true;
            }

            value = 0;
            string typeName;
            if (!KeyToTypeName.TryGetValue(materialKey, out typeName))
            {
                return false;
            }

            Block block = BlocksManager.FindBlockByTypeName(typeName, false);
            if (block == null)
            {
                return false;
            }

            // Plain cube: pack the bare content index (light 0, data 0).
            int packed = Terrain.MakeBlockValue(block.BlockIndex, 0, 0);
            _resolved[materialKey] = packed;
            value = packed;
            return true;
        }

        public void Reset()
        {
            _resolved.Clear();
        }
    }
}

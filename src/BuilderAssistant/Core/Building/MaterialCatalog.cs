using System.Collections.Generic;
using BuilderAssistant.Core.Blueprints;

namespace BuilderAssistant.Core.Building
{
    /// <summary>
    /// The single, game-free source of truth that maps a logical material key (what a
    /// generator emits) to the concrete vanilla block CLR type name and the
    /// <see cref="BlockFamily"/> that owns its DATA bits. Two consumers share it:
    ///
    ///   - <c>Features/WorldEdit/VanillaBlockPalette</c> builds its logical->type table
    ///     from here, so the runtime resolver and this catalog can never drift apart.
    ///   - The offline <c>BuildingValidator</c> and the net8 test harness resolve material
    ///     keys WITHOUT the game assembly, because every mapping here is a plain string +
    ///     the pure BlockFamily enum - neither needs <c>using Game;</c>.
    ///
    /// The type names below were each confirmed against the x25.01.17 assembly when the
    /// v0.9 palette was taken (see docs/COMPATIBILITY.md); this class only centralizes that
    /// already-verified knowledge, it does not claim any new block.
    /// </summary>
    internal static class MaterialCatalog
    {
        /// <summary>One logical key's resolved concrete block type + orientation family.</summary>
        internal readonly struct Entry
        {
            public readonly string TypeName;
            public readonly BlockFamily Family;

            public Entry(string typeName, BlockFamily family)
            {
                TypeName = typeName;
                Family = family;
            }
        }

        private static readonly Dictionary<string, Entry> Map = BuildMap();

        private static Dictionary<string, Entry> BuildMap()
        {
            var d = new Dictionary<string, Entry>(System.StringComparer.Ordinal);
            Add(d, BuildingMaterials.StoneBase, "CobblestoneBlock", BlockFamily.Cube);
            Add(d, BuildingMaterials.StoneWall, "StoneBrickBlock", BlockFamily.Cube);
            Add(d, BuildingMaterials.WoodWall, "PlanksBlock", BlockFamily.Cube);
            Add(d, BuildingMaterials.WoodTrim, "PlanksBlock", BlockFamily.Cube);
            Add(d, BuildingMaterials.Roof, "BricksBlock", BlockFamily.Cube);
            Add(d, BuildingMaterials.Glass, "GlassBlock", BlockFamily.Cube);
            Add(d, BuildingMaterials.Floor, "PlanksBlock", BlockFamily.Cube);

            Add(d, BuildingMaterials.RoofStairs, "WoodenStairsBlock", BlockFamily.Stairs);
            Add(d, BuildingMaterials.InteriorStairs, "WoodenStairsBlock", BlockFamily.Stairs);
            Add(d, BuildingMaterials.EntryDoor, "WoodenDoorBlock", BlockFamily.Door);
            Add(d, BuildingMaterials.WindowSill, "StoneSlabBlock", BlockFamily.Slab);
            Add(d, BuildingMaterials.FlatDeck, "StoneSlabBlock", BlockFamily.Slab);
            Add(d, BuildingMaterials.GardenFence, "WoodenFenceBlock", BlockFamily.Fence);
            Add(d, BuildingMaterials.StoneRailing, "StoneFenceBlock", BlockFamily.Fence);

            Add(d, BuildingMaterials.InteriorWall, "PlanksBlock", BlockFamily.Cube);
            Add(d, BuildingMaterials.SupportBeam, "PlanksBlock", BlockFamily.Cube);

            // v0.12 giant-tree blocks. OakWoodBlock / SpruceLeavesBlock were each confirmed
            // to exist in the x25.01.17 assembly in the M1 audit (OakWoodBlock Index=9,
            // SpruceLeavesBlock Index=14). Logs are authored vertical (data 0); leaves carry
            // no authored data. VanillaBlockPalette maps both concrete names to these families.
            Add(d, BuildingMaterials.LogTrunk,  "OakWoodBlock",     BlockFamily.Log);
            Add(d, BuildingMaterials.LogBranch, "OakWoodBlock",     BlockFamily.Log);
            Add(d, BuildingMaterials.Leaves,    "SpruceLeavesBlock", BlockFamily.Cube);
            return d;
        }

        private static void Add(Dictionary<string, Entry> d, string key, string typeName, BlockFamily family)
        {
            d[key] = new Entry(typeName, family);
        }

        /// <summary>True if <paramref name="key"/> is a logical key the catalog can resolve.</summary>
        public static bool TryGet(string key, out Entry entry)
        {
            return Map.TryGetValue(key ?? string.Empty, out entry);
        }

        /// <summary>Whether a logical material key is known (used by the offline validator).</summary>
        public static bool IsKnownKey(string key)
        {
            return !string.IsNullOrEmpty(key) && Map.ContainsKey(key);
        }

        /// <summary>The family a logical key belongs to, or Cube if unknown (fail-safe).</summary>
        public static BlockFamily FamilyOf(string key)
        {
            Entry e;
            return TryGet(key, out e) ? e.Family : BlockFamily.Cube;
        }

        /// <summary>All logical keys the catalog defines. Used to assert BuildingMaterials parity.</summary>
        public static IEnumerable<string> Keys { get { return Map.Keys; } }
    }
}

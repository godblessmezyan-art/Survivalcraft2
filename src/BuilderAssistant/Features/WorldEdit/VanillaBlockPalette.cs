using System.Collections.Generic;
using Game;
using BuilderAssistant.Core.Blueprints;
using BuilderAssistant.Core.Transactions;

namespace BuilderAssistant.Features.WorldEdit
{
    /// <summary>
    /// Resolves a blueprint material key to a real vanilla block value, and does
    /// so by name at runtime - never by a hardcoded index. Two kinds of keys are
    /// accepted:
    ///  1. A logical cottage key (StoneBase / WoodWall / ...), mapped to a verified
    ///     plain-cube block type. This is what generators emit and lets a blueprint
    ///     re-skin later.
    ///  2. A concrete vanilla block CLR type name (e.g. "CobblestoneBlock") - what a
    ///     COPY produces. Such a key only resolves when the name is in
    ///     <see cref="SupportedPlainCubes"/>, so importing a blueprint that references
    ///     stairs, slabs, oriented logs, plants, fluids or entity-bearing "complex"
    ///     blocks simply fails to resolve and the build aborts with a clear reason
    ///     instead of placing a wrong/low-quality block (AGENTS.md: no garbage).
    ///
    /// Every accepted block is a plain cube that needs no orientation data bits, so
    /// the packed value is just the block index (light 0, data 0).
    /// </summary>
    internal sealed class VanillaBlockPalette : IBlockValueResolver
    {
        // logical key -> CLR type name of a verified plain-cube vanilla block.
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

        /// <summary>
        /// The set of vanilla blocks Builder Assistant is willing to copy and paste:
        /// full solid/transparent cubes that render identically with data == 0 and
        /// carry no per-block entity state. Everything outside it - stairs, slabs,
        /// oriented logs, fences, glass panes, ladders, plants, snow, water/lava,
        /// doors, chests/furnaces, gravity sand/gravel and other entity-bearing blocks
        /// - is NOT reproduced: copy skips it with a clear report, and an imported
        /// blueprint referencing it aborts with a clear message rather than generating
        /// a degraded structure.
        ///
        /// Policy for extension (v0.8.2): a block may be ADDED here once reflection over
        /// D:\SC\Survivalcraft.exe confirms it is a plain CubeBlock/PaintedCubeBlock with
        /// no orientation/entity/state data bits. Adding a name is fail-safe at runtime:
        /// TryResolve looks the type up via BlocksManager.FindBlockByTypeName, so if a
        /// name is somehow not a registered in-game block it simply returns false and the
        /// build/import aborts cleanly - it can never place a wrong or guessed block. The
        /// remaining open question for every newly added entry is on-device RENDERING on
        /// the 7723 target, which stays 待验证 until confirmed block-by-block (see
        /// docs/COMPATIBILITY.md). PaintedCube-family members are pasted with the engine
        /// default color in this phase (per-cell color is not part of the terrain value);
        /// preserving color is reserved for a later schemaVersion=2 with a color payload.
        /// </summary>
        public static readonly HashSet<string> SupportedPlainCubes =
            new HashSet<string>(System.StringComparer.Ordinal)
            {
                // Proven on-device since the cottage (v0.6/v0.7):
                "CobblestoneBlock", // logical StoneBase   (PaintedCube)
                "StoneBrickBlock",  // logical StoneWall   (PaintedCube)
                "PlanksBlock",      // logical WoodWall / WoodTrim / Floor (PaintedCube)
                "BricksBlock",      // logical Roof        (PaintedCube)
                "GlassBlock",       // logical Glass       (AlphaTestCube)

                // v0.8.2: reflection-verified plain stone/brick cubes. class exists and
                // data==0 is safe; on-device rendering on 7723 is 待验证 (COMPATIBILITY).
                "BasaltBlock",      // PaintedCube, plain stone   (待验证)
                "GraniteBlock",     // PaintedCube, plain stone   (待验证)
                "MarbleBlock",      // PaintedCube, plain stone   (待验证)
                "SandstoneBlock",   // PaintedCube, plain stone   (待验证)
                "ClayBlock",        // PaintedCube, plain block   (待验证)
                "LimestoneBlock",   // Cube (fixed texture)       (待验证)
                "MalachiteBlock",   // Cube (fixed texture)       (待验证)
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
            if (string.IsNullOrEmpty(materialKey))
            {
                return false;
            }

            // Resolve the key to a type name: a logical key maps through the table;
            // a concrete key is used directly only if it is a supported plain cube.
            string typeName;
            if (!KeyToTypeName.TryGetValue(materialKey, out typeName))
            {
                if (!SupportedPlainCubes.Contains(materialKey))
                {
                    return false; // unknown or unsupported block -> abort, never guess
                }
                typeName = materialKey;
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

        /// <summary>
        /// Whether a material key from an imported/copied blueprint is buildable by
        /// this palette: true for logical cottage keys and for concrete vanilla keys
        /// in <see cref="SupportedPlainCubes"/>. Used to pre-flight an import so the
        /// user gets a precise "block type not yet supported" list instead of a bare
        /// planner failure.
        /// </summary>
        public bool IsResolvableKey(string materialKey)
        {
            int ignored;
            return TryResolve(materialKey, out ignored);
        }

        public void Reset()
        {
            _resolved.Clear();
        }
    }
}

using System.Collections.Generic;
using Game;
using BuilderAssistant.Core.Blueprints;
using BuilderAssistant.Core.Building;
using BuilderAssistant.Core.Transactions;

namespace BuilderAssistant.Features.WorldEdit
{
    /// <summary>
    /// Resolves a blueprint material key to a real vanilla block value, and does
    /// so by name at runtime - never by a hardcoded index. Two kinds of keys are
    /// accepted:
    ///  1. A logical cottage key (StoneBase / WoodWall / RoofStairs / ...), mapped to a
    ///     verified vanilla block CLR type. This is what generators emit and lets a
    ///     blueprint re-skin later.
    ///  2. A concrete vanilla block CLR type name (e.g. "CobblestoneBlock" or
    ///     "WoodenStairsBlock") - what a COPY produces. Such a key only resolves when it
    ///     is in <see cref="SupportedBlocks"/>, so importing a blueprint that references
    ///     an unknown or entity-bearing "complex" block simply fails to resolve and the
    ///     build aborts with a clear reason instead of placing a wrong/low-quality block
    ///     (AGENTS.md: no garbage).
    ///
    /// Since schema v2 the palette also packs per-cell AUTHORED orientation: the terrain
    /// value's DATA field (bits 14-31) carries stair rotation/upside-down, slab top-half,
    /// fence connection arms and door rotation/open/hinge. Every family's data is first
    /// masked by <see cref="BlockOrientation.Normalize"/> to the bits the block itself
    /// owns, so we never bake in engine-derived state (stair corner-joints, all colors) -
    /// the game's SubsystemStairsBlockBehavior / SubsystemFenceBlockBehavior recompute
    /// those from neighbors when the block is placed, and a door's upper half is spawned
    /// automatically by SubsystemDoorBlockBehavior from the bottom cell we write.
    /// </summary>
    internal sealed class VanillaBlockPalette : IBlockValueResolver
    {
        // logical key -> CLR type name of a verified vanilla block.
        //
        // Derived from MaterialCatalog (the game-free single source of truth in
        // Core.Building) instead of a hand-maintained duplicate, so this runtime resolver and
        // the offline BuildingValidator can never drift apart, and every logical key the
        // parametric engine emits (including InteriorWall / SupportBeam / InteriorStairs /
        // StoneRailing / FlatDeck) is resolvable without editing this file. The catalog's
        // concrete type names are all guaranteed members of SupportedBlocks below.
        private static readonly Dictionary<string, string> KeyToTypeName = BuildKeyToTypeName();

        private static Dictionary<string, string> BuildKeyToTypeName()
        {
            var d = new Dictionary<string, string>(System.StringComparer.Ordinal);
            foreach (string key in MaterialCatalog.Keys)
            {
                MaterialCatalog.Entry e;
                if (MaterialCatalog.TryGet(key, out e))
                {
                    d[key] = e.TypeName;
                }
            }
            return d;
        }

        /// <summary>
        /// Every vanilla block Builder Assistant is willing to build and copy, mapped to
        /// the family that decides which DATA bits it owns. Plain solid/transparent cubes
        /// are Cube (data 0). Stairs/slab/fence/door carry the authored orientation bits
        /// described in <see cref="BlockOrientation"/>. Anything outside this map - plants,
        /// snow, fluids, gravity sand/gravel, chests/furnaces, oriented logs, glass panes,
        /// ladders, ladders, redstone and other entity-bearing "complex" blocks - is NOT
        /// reproduced: copy skips it with a clear report, and an imported blueprint
        /// referencing it aborts with a clear message rather than a degraded structure.
        ///
        /// Extension policy: a name may be ADDED once reflection over the x25.01.17
        /// assembly confirms its family + owned data bits, and <see cref="TryResolve(string,int,out int)"/>
        /// stays fail-safe (an unregistered name resolves to null and aborts cleanly). The
        /// on-device RENDERING of every oriented block on the 7723 target stays 待验证
        /// until confirmed block-by-block (see docs/COMPATIBILITY.md). PaintedCube /
        /// stair / slab colors are NOT part of the terrain value we author, so painted
        /// sources paste in the engine default color (color preservation is out of scope).
        /// </summary>
        public static readonly Dictionary<string, BlockFamily> SupportedBlocks =
            new Dictionary<string, BlockFamily>(System.StringComparer.Ordinal)
            {
                // ---- Proven plain cubes on-device since the cottage (v0.6/v0.7) ----
                { "CobblestoneBlock", BlockFamily.Cube }, // StoneBase   (PaintedCube)
                { "StoneBrickBlock",  BlockFamily.Cube }, // StoneWall   (PaintedCube)
                { "PlanksBlock",      BlockFamily.Cube }, // WoodWall/WoodTrim/Floor
                { "BricksBlock",      BlockFamily.Cube }, // Roof        (PaintedCube)
                { "GlassBlock",       BlockFamily.Cube }, // Glass       (AlphaTestCube)

                // ---- v0.8.2 reflection-verified plain stone/brick cubes (待验证) ----
                { "BasaltBlock",    BlockFamily.Cube },
                { "GraniteBlock",   BlockFamily.Cube },
                { "MarbleBlock",    BlockFamily.Cube },
                { "SandstoneBlock", BlockFamily.Cube },
                { "ClayBlock",      BlockFamily.Cube },
                { "LimestoneBlock", BlockFamily.Cube },
                { "MalachiteBlock", BlockFamily.Cube },

                // ---- v0.9 stairs (StairsBlock: rotation(0-1)+upsideDown(2); corners/color derived) ----
                { "WoodenStairsBlock",      BlockFamily.Stairs },
                { "CobblestoneStairsBlock", BlockFamily.Stairs },
                { "StoneStairsBlock",       BlockFamily.Stairs },
                { "StoneBrickStairsBlock",  BlockFamily.Stairs },
                { "BrickStairsBlock",       BlockFamily.Stairs },
                { "BasaltStairsBlock",      BlockFamily.Stairs },
                { "MarbleStairsBlock",      BlockFamily.Stairs },
                { "SandstoneStairsBlock",   BlockFamily.Stairs },
                { "DirtStairsBlock",        BlockFamily.Stairs },

                // ---- v0.9 slabs / half-blocks (SlabBlock: isTop(0); color derived) ----
                { "WoodenSlabBlock",      BlockFamily.Slab },
                { "CobblestoneSlabBlock", BlockFamily.Slab },
                { "StoneSlabBlock",       BlockFamily.Slab },
                { "StoneBrickSlabBlock",  BlockFamily.Slab },
                { "BrickSlabBlock",       BlockFamily.Slab },
                { "BasaltSlabBlock",      BlockFamily.Slab },
                { "MarbleSlabBlock",      BlockFamily.Slab },
                { "SandstoneSlabBlock",   BlockFamily.Slab },
                { "DirtSlabBlock",        BlockFamily.Slab },

                // ---- v0.9 fences (FenceBlock: 4 connection arms(0-3), engine-recomputed) ----
                { "WoodenFenceBlock", BlockFamily.Fence },
                { "StoneFenceBlock",  BlockFamily.Fence },
                { "BrickFenceBlock",  BlockFamily.Fence },
                { "BasaltFenceBlock", BlockFamily.Fence },
                { "IronFenceBlock",   BlockFamily.Fence },

                // ---- v0.9 doors (DoorBlock: rotation(0-1)+open(2)+rightHanded(3); two-cell) ----
                { "WoodenDoorBlock", BlockFamily.Door }, // index 56
                { "IronDoorBlock",   BlockFamily.Door }, // index 57
            };

        /// <summary>The plain-cube subset, kept for readability/back-compat. Every member
        /// is also in <see cref="SupportedBlocks"/> tagged as <see cref="BlockFamily.Cube"/>.</summary>
        public static readonly HashSet<string> SupportedPlainCubes = BuildCubes();

        private static HashSet<string> BuildCubes()
        {
            var s = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (KeyValuePair<string, BlockFamily> kv in SupportedBlocks)
            {
                if (kv.Value == BlockFamily.Cube) s.Add(kv.Key);
            }
            return s;
        }

        /// <summary>
        /// Whether a concrete vanilla CLR type name is a block this Mod can build/copy,
        /// and if so which orientation family it belongs to. The copier uses this to turn
        /// a live cell into a faithful oriented key; unknown/complex blocks return false.
        /// </summary>
        public static bool TryGetFamily(string typeName, out BlockFamily family)
        {
            return SupportedBlocks.TryGetValue(typeName ?? string.Empty, out family);
        }

        private readonly Dictionary<string, int> _resolvedCube = new Dictionary<string, int>();

        public bool TryResolve(string materialKey, out int value)
        {
            return TryResolve(materialKey, 0, out value);
        }

        public bool TryResolve(string materialKey, int data, out int value)
        {
            value = 0;
            if (string.IsNullOrEmpty(materialKey))
            {
                return false;
            }

            // Resolve the key to a type name: a logical key maps through the table; a
            // concrete key is used directly only if it is a supported block.
            string typeName;
            if (!KeyToTypeName.TryGetValue(materialKey, out typeName))
            {
                if (!SupportedBlocks.ContainsKey(materialKey))
                {
                    return false; // unknown or unsupported block -> abort, never guess
                }
                typeName = materialKey;
            }

            BlockFamily family;
            if (!SupportedBlocks.TryGetValue(typeName, out family))
            {
                return false; // mapped to a type we cannot build -> abort, never guess
            }

            // Plain cubes need no orientation: pack the bare content index (light 0,
            // data 0). Cache those (the common cottage path) to skip reflection re-lookup.
            if (family == BlockFamily.Cube)
            {
                int cached;
                if (_resolvedCube.TryGetValue(typeName, out cached))
                {
                    value = cached;
                    return true;
                }
            }

            Block block = BlocksManager.FindBlockByTypeName(typeName, false);
            if (block == null)
            {
                return false;
            }

            int packed = Terrain.MakeBlockValue(block.BlockIndex, 0, BlockOrientation.Normalize(family, data));

            if (family == BlockFamily.Cube)
            {
                _resolvedCube[typeName] = packed;
            }

            value = packed;
            return true;
        }

        /// <summary>
        /// Whether a material key from an imported/copied blueprint is buildable by
        /// this palette (logical cottage keys or concrete vanilla keys in
        /// <see cref="SupportedBlocks"/>). Used to pre-flight an import so the user gets
        /// a precise "block type not yet supported" list instead of a bare planner failure.
        /// </summary>
        public bool IsResolvableKey(string materialKey)
        {
            int ignored;
            return TryResolve(materialKey, out ignored);
        }

        public void Reset()
        {
            _resolvedCube.Clear();
        }
    }
}

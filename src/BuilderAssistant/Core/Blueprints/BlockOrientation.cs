namespace BuilderAssistant.Core.Blueprints
{
    /// <summary>
    /// The vanilla block "family" a blueprint cell belongs to. This is NOT the
    /// block index or a CLR type name - it is the minimal information the pure
    /// Core needs to know HOW (and whether) a cell's authored data bits must be
    /// transformed when the whole blueprint is rotated or mirrored. The WorldEdit
    /// palette is the only place that maps concrete vanilla type names to a family;
    /// Core never references the game assembly.
    ///
    /// Families here are exactly the ones that carry orientation / connection /
    /// state in the terrain value's DATA field (bits 14-31 of Terrain.MakeBlockValue).
    /// Plain cubes (the original supported set) have no authored data and are Cube.
    /// </summary>
    internal enum BlockFamily
    {
        /// <summary>No authored data. Stairs corner-joints / colors / fence links
        /// that the engine recomputes are deliberately NOT stored here.</summary>
        Cube = 0,
        /// <summary>Rotation (2 bits) + upside-down (1 bit). Corner &amp; color bits are
        /// auto-derived by the engine and stripped on authoring.</summary>
        Stairs = 1,
        /// <summary>isTop (1 bit). Color bits are stripped. Rotation-invariant.</summary>
        Slab = 2,
        /// <summary>Four connection bits (+X,-X,+Z,-Z). The engine recomputes them from
        /// neighbors; we still preserve the authored mask so a copied run renders
        /// immediately and a generated lone post links correctly once placed.</summary>
        Fence = 3,
        /// <summary>Rotation (2) + open (1) + right-handed (1). A door is a two-cell
        /// block: writing the bottom cell makes the engine spawn the matching top cell
        /// (SubsystemDoorBlockBehavior.OnBlockAdded), so a blueprint stores only the
        /// bottom half and the top is derived at build time.</summary>
        Door = 4,
        /// <summary>Vanilla log / wood block (Game.WoodBlock subclass, e.g. OakWoodBlock).
        /// An opaque SOLID cube; its DATA carries only a 2-bit cut-face: the direction the
        /// growth rings face. Confirmed from the decompiled x25.01.17 WoodBlock:
        ///   GetCutFace(data) = (data &amp; 3) switch {0 -> 4(Y), 1 -> 0, else -> 1}
        ///   SetCutFace(data, face): face 0/2 -> bits 1, face 1/3 -> bits 2, else -> bits 0.
        /// So data 0 = a VERTICAL log (rings on the top/bottom faces) — the tree-trunk
        /// orientation. A vertical log is invariant under both Y-rotation and X-mirror.
        /// Horizontal logs (bits 1/2) swap axes on a quarter turn; the v0.12 treehouse only
        /// ever authors vertical logs, so horizontal-axis mirror correctness is deliberately
        /// out of scope here (kept group-law consistent, but not device-verified).</summary>
        Log = 5,
    }

    /// <summary>
    /// Pure, game-independent codec for the DATA field of oriented blueprint cells.
    /// Every formula here was derived from the decompiled x25.01.17 <c>Game</c>
    /// assembly (StairsBlock / SlabBlock / FenceBlock / DoorBlock bit accessors,
    /// Block.GetPlacementValue facing, and SubsystemFenceBlockBehavior / SubsystemStairs-
    /// BlockBehavior neighbor logic), NOT guessed. Keeping this in Core means the
    /// rotation/mirror/normalization rules are unit-testable offline with no game DLL.
    ///
    /// Bit layouts (all relative to the block's own <c>data</c> int, i.e. what
    /// Terrain.ExtractData returns):
    ///   Stairs: [0-1]=rotation, [2]=upsideDown, [3-4]=corner(derived), [5+]=color(derived)
    ///   Slab:   [0]=isTop,      [1+]=color(derived)
    ///   Fence:  [0]=+X arm, [1]=-X arm, [2]=+Z arm, [3]=-Z arm
    ///   Door:   [0-1]=rotation, [2]=open, [3]=rightHanded(1 bit)
    ///
    /// Facing convention (matches Block.GetPlacementValue in both stairs and doors):
    ///   rot0 = -Z, rot1 = -X, rot2 = +Z, rot3 = +X
    /// The <see cref="Blueprint.Reorient"/> position transform for one clockwise
    /// quarter turn is (x,z) -> (length-1-z, x), i.e. displacement (dx,dz) -> (-dz,dx).
    /// Applying that to each facing vector gives newRot = (rot + 3) &amp; 3 per step,
    /// hence (rot + 3*steps) &amp; 3. Mirror-in-X flips dx: newRot = (-rot) &amp; 3,
    /// and a door's hinge side is left/right so its rightHanded bit is toggled.
    /// </summary>
    internal static class BlockOrientation
    {
        /// <summary>
        /// Validate author-supplied DATA without masking it. JSON/import/planning use
        /// this strict path so unsupported color, transient or unknown bits can never be
        /// silently discarded.
        /// </summary>
        public static bool IsValidAuthoredData(BlockFamily family, int data)
        {
            if (data < 0)
            {
                return false;
            }
            switch (family)
            {
                case BlockFamily.Cube: return data == 0;
                case BlockFamily.Stairs: return data <= 7;
                case BlockFamily.Slab: return data <= 1;
                case BlockFamily.Fence: return data <= 15;
                case BlockFamily.Door: return data <= 15;
                case BlockFamily.Log: return data == 0;
                default: return false;
            }
        }

        public static string AuthoredDataRange(BlockFamily family)
        {
            switch (family)
            {
                case BlockFamily.Stairs: return "0..7";
                case BlockFamily.Slab: return "0..1";
                case BlockFamily.Fence:
                case BlockFamily.Door: return "0..15";
                case BlockFamily.Cube:
                case BlockFamily.Log: return "仅 0";
                default: return "不支持";
            }
        }

        /// <summary>
        /// Decode a live terrain DATA value into the subset schema v2 can faithfully
        /// author. Stair corner bits (3-4) are engine-derived and may be stripped. Any
        /// color bit, horizontal-log value or unknown bit fails instead of degrading the
        /// copied block to an unpainted/default state.
        /// </summary>
        public static bool TryDecodeTerrainData(BlockFamily family, int terrainData, out int authoredData)
        {
            authoredData = 0;
            if (terrainData < 0)
            {
                return false;
            }
            switch (family)
            {
                case BlockFamily.Stairs:
                    // 0-2 authored; 3-4 derived corner. Bit 5+ includes paint/unknown state.
                    if ((terrainData & ~0x1F) != 0) return false;
                    authoredData = terrainData & 0x7;
                    return true;
                case BlockFamily.Cube:
                case BlockFamily.Slab:
                case BlockFamily.Fence:
                case BlockFamily.Door:
                case BlockFamily.Log:
                    if (!IsValidAuthoredData(family, terrainData)) return false;
                    authoredData = terrainData;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Keep only the bits that are genuinely authored per cell, dropping any
        /// engine-derived/transient bits (stair corners, all colors, etc.) so a
        /// blueprint never bakes in state the engine would recompute differently.
        /// This is applied when a copier captures a live cell and when a generator
        /// emits an oriented cell.
        /// </summary>
        public static int Normalize(BlockFamily family, int data)
        {
            switch (family)
            {
                case BlockFamily.Stairs:
                    return data & 0x7; // rotation(0-1) + upsideDown(2)
                case BlockFamily.Slab:
                    return data & 0x1; // isTop(0)
                case BlockFamily.Fence:
                    return data & 0xF; // four arms
                case BlockFamily.Door:
                    return data & 0xF; // rotation + open + rightHanded
                case BlockFamily.Log:
                    return data & 0x3; // cut-face axis only (0 = vertical)
                case BlockFamily.Cube:
                default:
                    return 0;
            }
        }

        /// <summary>
        /// Transform a cell's authored data under a blueprint-space rotation of
        /// <paramref name="steps"/> clockwise quarter turns (0..3). Only the families
        /// whose orientation depends on the horizontal facing change; slabs (isTop)
        /// and cubes are invariant.
        /// </summary>
        public static int Rotate(BlockFamily family, int data, int steps)
        {
            int r = ((steps % 4) + 4) % 4;
            if (r == 0)
            {
                return data;
            }

            switch (family)
            {
                case BlockFamily.Stairs:
                {
                    int rot = data & 3;
                    int up = data & 4;
                    int newRot = (rot + 3 * r) & 3;
                    return newRot | up;
                }
                case BlockFamily.Door:
                {
                    int rot = data & 3;
                    int open = data & 4;
                    int right = data & 8;
                    int newRot = (rot + 3 * r) & 3;
                    return newRot | open | right; // hinge handedness is invariant to pure rotation
                }
                case BlockFamily.Fence:
                {
                    int mask = data & 0xF;
                    for (int i = 0; i < r; i++)
                    {
                        mask = RotateFenceOnce(mask);
                    }
                    return mask;
                }
                case BlockFamily.Log:
                {
                    int bits = data & 0x3; // 0 = vertical (invariant)
                    if (bits == 0)
                    {
                        return 0;
                    }
                    // Horizontal axis toggles once per quarter turn; an even count is identity.
                    return ((r & 1) == 0) ? bits : (bits == 1 ? 2 : 1);
                }
                case BlockFamily.Slab:
                case BlockFamily.Cube:
                default:
                    return data; // isTop / nothing are invariant under Y-axis rotation
            }
        }

        /// <summary>
        /// Transform authored data for a mirror about the X-normal axis (the flip
        /// <see cref="Blueprint.Reorient"/> applies after the rotation step). Facing
        /// reverses in X (newRot = (-rot)&amp;3); a door also flips its hinge side;
        /// a fence's +X/-X arms swap while its +Z/-Z arms stay. Slabs/cubes are invariant.
        /// </summary>
        public static int MirrorX(BlockFamily family, int data)
        {
            switch (family)
            {
                case BlockFamily.Stairs:
                {
                    int rot = data & 3;
                    int up = data & 4;
                    int newRot = (0 - rot) & 3;
                    return newRot | up;
                }
                case BlockFamily.Door:
                {
                    int rot = data & 3;
                    int open = data & 4;
                    int right = data & 8;
                    int newRot = (0 - rot) & 3;
                    return newRot | open | (right ^ 8); // mirror swaps left/right hinge
                }
                case BlockFamily.Fence:
                {
                    int mask = data & 0xF;
                    int x0 = mask & 1; // +X
                    int x1 = mask & 2; // -X
                    int z = mask & 0xC; // +Z / -Z unchanged
                    return ((x0 << 1) | (x1 >> 1) | z) & 0xF;
                }
                case BlockFamily.Log:
                {
                    // Vertical logs (the only kind the generator authors) are mirror-invariant.
                    // Horizontal-log mirror axis mapping is intentionally not modeled here:
                    // the v0.12 treehouse never emits a horizontal log, and the exact face->axis
                    // mirror behaviour is not device-verified (kept honest, not guessed).
                    return data;
                }
                case BlockFamily.Slab:
                case BlockFamily.Cube:
                default:
                    return data;
            }
        }

        // One clockwise quarter turn of a fence connection mask. Vector map
        // (dx,dz)->(-dz,dx) sends +X->+Z, +Z->-X, -X->-Z, -Z->+X. So:
        //   new +X(bit0) = old -Z(bit3), new -X(bit1) = old +Z(bit2),
        //   new +Z(bit2) = old +X(bit0), new -Z(bit3) = old -X(bit1).
        private static int RotateFenceOnce(int mask)
        {
            int b0 = (mask >> 3) & 1;
            int b1 = (mask >> 2) & 1;
            int b2 = (mask >> 0) & 1;
            int b3 = (mask >> 1) & 1;
            return (b0 << 0) | (b1 << 1) | (b2 << 2) | (b3 << 3);
        }

        /// <summary>Strictly parses a family tag string used in the JSON exchange format.</summary>
        public static bool TryParseFamilyTag(string tag, out BlockFamily family)
        {
            family = BlockFamily.Cube;
            if (string.IsNullOrEmpty(tag) || tag == "cube") return true;
            switch (tag)
            {
                case "stairs": family = BlockFamily.Stairs; return true;
                case "slab": family = BlockFamily.Slab; return true;
                case "fence": family = BlockFamily.Fence; return true;
                case "door": family = BlockFamily.Door; return true;
                case "log": family = BlockFamily.Log; return true;
                default: return false;
            }
        }

        /// <summary>Back-compat helper for already validated internal tags.</summary>
        public static BlockFamily FamilyFromTag(string tag)
        {
            BlockFamily family;
            return TryParseFamilyTag(tag, out family) ? family : BlockFamily.Cube;
        }

        public static string FamilyToTag(BlockFamily family)
        {
            switch (family)
            {
                case BlockFamily.Stairs: return "stairs";
                case BlockFamily.Slab: return "slab";
                case BlockFamily.Fence: return "fence";
                case BlockFamily.Door: return "door";
                case BlockFamily.Log: return "log";
                case BlockFamily.Cube:
                default: return "cube";
            }
        }
    }
}

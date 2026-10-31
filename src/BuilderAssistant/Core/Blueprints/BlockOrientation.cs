using System;

namespace BuilderAssistant.Core.Blueprints
{
    /// <summary>DATA codecs proven against the fixed Windows x25.01.17 assembly.</summary>
    internal enum BlockFamily
    {
        Cube, Stairs, Slab, Fence, Door, Log, Ladder, Torch,
        // Trapdoor/FenceGate/Ivy remain research-only; Carpet is active since v0.24.0.
        Trapdoor, FenceGate, Carpet, Ivy,
        Falling, Grass, Soil, Plant, Snow, GroundCover, Gravestone, Lantern, Cairn,
    }

    internal static class BlockOrientation
    {
        public static bool IsValidAuthoredData(BlockFamily family, int data)
        {
            if (data < 0) return false;
            switch (family)
            {
                // Cube is type-qualified by VanillaBlockCapability: only PaintedCube accepts paint.
                case BlockFamily.Cube: return ValidPaint(data, 0, 1, 1);
                case BlockFamily.Stairs: return ValidPaint(data, 31, 32, 6) && (data & 24) != 24;
                case BlockFamily.Slab: return ValidPaint(data, 1, 2, 2);
                case BlockFamily.Fence:
                case BlockFamily.FenceGate: return ValidPaint(data, 15, 16, 5);
                case BlockFamily.Gravestone:
                case BlockFamily.Door:
                case BlockFamily.Trapdoor:
                case BlockFamily.Carpet: return data <= 15;
                case BlockFamily.Falling:
                case BlockFamily.Snow:
                case BlockFamily.GroundCover:
                case BlockFamily.Cairn: return data == 0;
                case BlockFamily.Grass: return data <= 1;
                case BlockFamily.Plant: return data == 0 || data == 1 || data == 8;
                case BlockFamily.Soil: return data <= 7;
                case BlockFamily.Lantern: return data <= 3;
                case BlockFamily.Log: return data <= 2; // SetCutFace never emits alias 3.
                case BlockFamily.Ladder:
                case BlockFamily.Ivy: return data <= 3;
                case BlockFamily.Torch: return data <= 4; // ceiling placement is forbidden by GetPlacementValue.
                default: return false;
            }
        }

        private static bool ValidPaint(int data, int geometryMask, int flag, int shift)
        {
            int paintMask = flag | (15 << shift);
            return (data & ~(geometryMask | paintMask)) == 0 &&
                ((data & flag) != 0 || (data & (15 << shift)) == 0);
        }

        public static bool TryEncodePaint(BlockFamily family, int geometry, int? color, out int data)
        {
            data = 0;
            int mask, flag, shift;
            if (!PaintLayout(family, out mask, out flag, out shift) || geometry < 0 || (geometry & ~mask) != 0 ||
                (color.HasValue && (color.Value < 0 || color.Value > 15))) return false;
            data = geometry | (color.HasValue ? flag | (color.Value << shift) : 0);
            return IsValidAuthoredData(family, data);
        }

        public static bool TryDecodePaint(BlockFamily family, int data, out int geometry, out int? color)
        {
            geometry = 0; color = null;
            int mask, flag, shift;
            if (!IsValidAuthoredData(family, data) || !PaintLayout(family, out mask, out flag, out shift)) return false;
            geometry = data & mask;
            if ((data & flag) != 0) color = (data >> shift) & 15;
            return true;
        }

        private static bool PaintLayout(BlockFamily family, out int mask, out int flag, out int shift)
        {
            mask = flag = shift = 0;
            switch (family)
            {
                case BlockFamily.Cube: mask = 0; flag = 1; shift = 1; return true;
                case BlockFamily.Stairs: mask = 31; flag = 32; shift = 6; return true;
                case BlockFamily.Slab: mask = 1; flag = 2; shift = 2; return true;
                case BlockFamily.Fence:
                case BlockFamily.FenceGate: mask = 15; flag = 16; shift = 5; return true;
                default: return false;
            }
        }

        public static string AuthoredDataRange(BlockFamily family)
        {
            switch (family)
            {
                case BlockFamily.Cube: return "0；PaintedCube 另允许 1+2*color，color=0..15";
                case BlockFamily.Stairs: return "geometry=0..23；可加 32+64*color，color=0..15";
                case BlockFamily.Slab: return "0..1；可加 2+4*color，color=0..15";
                case BlockFamily.Fence:
                case BlockFamily.FenceGate: return "0..15；可加 16+32*color，color=0..15";
                case BlockFamily.Falling:
                case BlockFamily.Snow:
                case BlockFamily.GroundCover:
                case BlockFamily.Cairn: return "0";
                case BlockFamily.Grass:
                case BlockFamily.Plant: return "按类型：草地/花0..1，高草0/8，枯灌木0";
                case BlockFamily.Soil: return "0..7（水分位0，肥力位1..2）";
                case BlockFamily.Lantern: return "0..3";
                case BlockFamily.Gravestone: return "0..15（造型位0..2，轴向位3）";
                case BlockFamily.Log: return "0(Y),1(Z),2(X)";
                case BlockFamily.Ladder:
                case BlockFamily.Ivy: return "0..3";
                case BlockFamily.Torch: return "0..4";
                case BlockFamily.Door:
                case BlockFamily.Trapdoor:
                case BlockFamily.Carpet: return "0..15";
                default: return "不支持";
            }
        }

        public static bool TryDecodeTerrainData(BlockFamily family, int terrainData, out int authoredData)
        {
            authoredData = terrainData;
            return IsValidAuthoredData(family, terrainData);
        }

        // Kept for generator callers; invalid states must never be silently masked.
        public static int Normalize(BlockFamily family, int data)
        {
            RequireValid(family, data);
            return data;
        }

        private static void RequireValid(BlockFamily family, int data)
        {
            if (!IsValidAuthoredData(family, data))
                throw new ArgumentOutOfRangeException("data", AuthoredDataRange(family));
        }

        public static int Rotate(BlockFamily family, int data, int steps)
        {
            RequireValid(family, data);
            int r = ((steps % 4) + 4) % 4;
            switch (family)
            {
                case BlockFamily.Lantern:
                case BlockFamily.Stairs:
                case BlockFamily.Door:
                case BlockFamily.Trapdoor:
                case BlockFamily.FenceGate:
                case BlockFamily.Ladder:
                case BlockFamily.Ivy:
                    return (data & ~3) | (((data & 3) + 3 * r) & 3);
                case BlockFamily.Torch:
                    return data == 4 ? data : (data + 3 * r) & 3;
                case BlockFamily.Fence:
                    int mask = data & 15;
                    for (int i = 0; i < r; i++)
                        mask = ((mask >> 3) & 1) | ((mask >> 1) & 2) | ((mask << 2) & 4) | ((mask << 2) & 8);
                    return (data & ~15) | mask;
                case BlockFamily.Gravestone:
                    return data ^ ((r & 1) << 3);
                case BlockFamily.Log:
                    return data == 0 || (r & 1) == 0 ? data : 3 - data;
                default: return data;
            }
        }

        public static int MirrorX(BlockFamily family, int data)
        {
            RequireValid(family, data);
            switch (family)
            {
                case BlockFamily.Stairs:
                    // Collision boxes 8..23: each corner mirrors rot0 <-> rot1, rot2 <-> rot3.
                    return (data & ~3) | (((data & 24) == 0 ? 0 - data : 1 - data) & 3);
                case BlockFamily.Door:
                case BlockFamily.FenceGate:
                    return ((data & ~3) | ((-data) & 3)) ^ 8;
                case BlockFamily.Lantern:
                case BlockFamily.Trapdoor:
                case BlockFamily.Ladder:
                case BlockFamily.Ivy:
                    return (data & ~3) | ((-data) & 3);
                case BlockFamily.Torch:
                    return data == 4 ? data : (-data) & 3;
                case BlockFamily.Fence:
                    return (data & ~3) | ((data & 1) << 1) | ((data & 2) >> 1);
                default: return data; // A log axis has no signed direction.
            }
        }

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
                case "ladder": family = BlockFamily.Ladder; return true;
                case "torch": family = BlockFamily.Torch; return true;
                case "falling": family = BlockFamily.Falling; return true;
                case "grass": family = BlockFamily.Grass; return true;
                case "soil": family = BlockFamily.Soil; return true;
                case "plant": family = BlockFamily.Plant; return true;
                case "snow": family = BlockFamily.Snow; return true;
                case "groundcover": family = BlockFamily.GroundCover; return true;
                case "carpet": family = BlockFamily.Carpet; return true;
                case "gravestone": family = BlockFamily.Gravestone; return true;
                case "lantern": family = BlockFamily.Lantern; return true;
                case "cairn": family = BlockFamily.Cairn; return true;
                default: return false; // Research-only families are deliberately not importable.
            }
        }

        public static BlockFamily FamilyFromTag(string tag)
        {
            BlockFamily family;
            if (!TryParseFamilyTag(tag, out family)) throw new ArgumentException("Unknown family", "tag");
            return family;
        }

        public static string FamilyToTag(BlockFamily family)
        {
            return family.ToString().ToLowerInvariant();
        }
    }
}

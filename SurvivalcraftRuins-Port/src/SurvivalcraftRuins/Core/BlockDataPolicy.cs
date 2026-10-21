namespace SurvivalcraftRuins.Core;

public static class BlockDataPolicy
{
    public static bool IsSupported(string blockName, int data, out string reason)
    {
        if (data < 0 || data > 0x3FFFF)
        {
            reason = "terrain data is outside the SC 18-bit range";
            return false;
        }

        if (blockName.EndsWith("StairsBlock", StringComparison.Ordinal) && data > 7)
        {
            reason = "stair direction/upside-down data must be 0..7";
            return false;
        }
        if (blockName.EndsWith("SlabBlock", StringComparison.Ordinal) && data > 1)
        {
            reason = "slab half data must be 0 or 1";
            return false;
        }
        if (blockName.EndsWith("WoodBlock", StringComparison.Ordinal) && data > 2)
        {
            reason = "wood axis data must be 0..2";
            return false;
        }
        if (blockName == "WoodenLadderBlock" && data > 3)
        {
            reason = "ladder face data must be 0..3";
            return false;
        }
        if (blockName == "TorchBlock" && data > 4)
        {
            reason = "torch mounting data must be 0..4";
            return false;
        }
        if ((blockName == "CarpetBlock" || blockName == "PigmentBlock") && data > 15)
        {
            reason = "color data must be 0..15";
            return false;
        }
        if (blockName == "DetonatorBlock" && data > 5)
        {
            reason = "detonator mounting face must be 0..5";
            return false;
        }
        if (blockName == "PressurePlateBlock"
            && (((data >> 1) & 7) > 5 || (data & ~0xF) != 0))
        {
            reason = "pressure plate material/mounting-face bits are invalid";
            return false;
        }
        if (blockName == "DispenserBlock" && ((data & 7) > 5 || (data & ~0x1F) != 0))
        {
            reason = "dispenser direction/mode/drop bits are invalid";
            return false;
        }
        if (blockName == "SwitchBlock" && (((data >> 1) & 7) > 5 || (data & ~0xFF) != 0))
        {
            reason = "switch state/face/voltage bits are invalid";
            return false;
        }
        if ((blockName == "WireBlock") && (data & ~0x7FF) != 0)
        {
            reason = "wire face-mask/color bits are invalid";
            return false;
        }
        if ((blockName == "WireThroughBlock" || blockName == "WireThroughStoneBlock")
            && data > 2)
        {
            reason = "wire-through axis data must be 0..2";
            return false;
        }
        if (blockName == "PistonBlock"
            && ((data & 1) != 0
                || ((data >> 1) & 3) > 2
                || ((data >> 3) & 7) > 5
                || ((data >> 6) & 7) == 0
                || (data & ~0x3FFF) != 0))
        {
            reason = "piston initial state/mode/face/extension/speed bits are invalid";
            return false;
        }

        reason = string.Empty;
        return true;
    }
}

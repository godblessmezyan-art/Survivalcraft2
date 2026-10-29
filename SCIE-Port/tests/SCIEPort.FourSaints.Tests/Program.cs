using System;
using System.Linq;
using SCIEPort.Features.FourSaints;

internal static class Program
{
    private static int Main()
    {
        Check(FourSaintsRules.EffectiveDamage(.2f, false, true) == 0f, "Xuanwu clamps below three points");
        Check(Near(FourSaintsRules.EffectiveDamage(.5f, false, true), .2f), "Xuanwu subtracts exactly three points");
        Check(Near(1f + FourSaintsRules.DamageBonus(true, false), 6f), "unarmed passive attack is base 1 plus 5");
        Check(Near(1f + FourSaintsRules.DamageBonus(true, true), 16f), "awakening replaces passive bonus with 15");
        Check(FourSaintsRules.DamageBonus(false, true) == 0f, "no bonus without Baihu");
        Check(Near(FourSaintsRules.EffectiveDamage(1f, true, true), .7f), "Xuanwu reduction has no old Baihu multiplier");
        Check(Near(FourSaintsRules.AttackFrequency(true, false, false), 1.2f), "Baihu passive frequency");
        Check(Near(FourSaintsRules.AttackFrequency(true, true, false), 2f), "Awakening replaces passive frequency");
        Check(Near(FourSaintsRules.AttackFrequency(true, true, true), 2.2f), "Mjolnir and Baihu multiply without overwrite");
        float jump = FourSaintsRules.JumpSpeedForExtraBlocks(6f, 1f, 1f);
        Check(Near((jump * jump - 36f) / 20f, 1f), "jump apex gains one block");
        Check(Near((FourSaintsRules.JumpSpeedForExtraBlocks(6f, 1f, 2f) * FourSaintsRules.JumpSpeedForExtraBlocks(6f, 1f, 2f) - 36f) / 20f, 2f), "awakened jump gains two blocks");
        Check(FourSaintsRules.Sturdiness(200) == 700 && FourSaintsRules.Sturdiness(201) == 1200 && FourSaintsRules.Sturdiness(202) == 600 && FourSaintsRules.Sturdiness(203) == 500, "four distinct sturdiness values");
        Check(FourSaintsRules.RepairSteps(40f, 700f, 16, 2) == 0 && FourSaintsRules.RepairSteps(50f, 700f, 16, 2) == 1, "repair accrues across quantized durability levels");
        Check(FourSaintsRules.RepairSteps(1000f, 700f, 16, 2) == 2 && FourSaintsRules.RepairSteps(10f, 700f, 16, 0) == 0, "repair clamps to existing damage");
        Check(Near(FourSaintsRules.MaximumHealth(15), 2f) && Near(FourSaintsRules.MaximumHealth(1), 1.5f) && Near(FourSaintsRules.MaximumHealth(14), 1f), "full set and removal health limits");
        Check(FourSaintsRules.QinglongHealPeriod == 5.0 && Near(FourSaintsRules.QinglongHealAmount, .1f), "Qinglong heal cadence");
        Check(Near(FourSaintsRules.QinglongMaximumHealth, 1.5f), "Qinglong maximum health");
        Check(FourSaintsRules.BaihuDuration == 5.0 && FourSaintsRules.BaihuCooldown == 60.0, "Baihu duration and cooldown");
        Check(FourSaintsRules.ZhuqueLockDuration == 1.0, "Zhuque rebirth lock duration");
        Check(FourSaintsVisualRules.QinglongPulseSeconds == .8 && FourSaintsVisualRules.XuanwuPulseSeconds == .55, "visual pulses stay brief");
        Check(FourSaintsVisualRules.XuanwuThrottleSeconds == .35, "environment immunity feedback is throttled");
        Check(FourSaintsVisualRules.MaximumGlyphs(0) == 5 && FourSaintsVisualRules.MaximumGlyphs(1) == 9 && FourSaintsVisualRules.MaximumGlyphs(2) == 12, "visual glyph budget by quality");
        Check(FourSaintsRules.IsXuanwuEnvironmentalImmunity(true, "drown", "drown", "cold-death", "freezing"), "Xuanwu drowning immunity");
        Check(FourSaintsRules.IsXuanwuEnvironmentalImmunity(true, "cold-death", "drown", "cold-death", "freezing"), "Xuanwu lethal cold immunity");
        Check(FourSaintsRules.IsXuanwuEnvironmentalImmunity(true, "freezing", "drown", "cold-death", "freezing"), "Xuanwu freezing tick immunity");
        Check(!FourSaintsRules.IsXuanwuEnvironmentalImmunity(false, "freezing", "drown", "cold-death", "freezing"), "Xuanwu immunity ends when unequipped");
        Check(!FourSaintsRules.IsXuanwuEnvironmentalImmunity(true, "heat", "drown", "cold-death", "freezing"), "Xuanwu does not block heat");
        Check(!FourSaintsRules.IsXuanwuEnvironmentalImmunity(true, "flu", "drown", "cold-death", "freezing"), "Xuanwu does not block flu");
        Check(!FourSaintsRules.IsXuanwuEnvironmentalImmunity(true, "fall", "drown", "cold-death", "freezing"), "Xuanwu does not block fall");
        Check(Near(FourSaintsRules.ClampTemperature(2f, true, false), FourSaintsRules.XuanwuTemperatureFloor), "Xuanwu warms a freezing body to the floor");
        Check(Near(FourSaintsRules.ClampTemperature(12f, true, false), 12f), "Xuanwu leaves a comfortable body untouched");
        Check(Near(FourSaintsRules.ClampTemperature(30f, false, true), FourSaintsRules.ZhuqueTemperatureCeiling), "Zhuque cools an overheating body to the ceiling");
        Check(Near(FourSaintsRules.ClampTemperature(5f, false, true), 5f), "Zhuque leaves a cool body untouched");
        Check(Near(FourSaintsRules.ClampTemperature(2f, true, true), 8f) && Near(FourSaintsRules.ClampTemperature(30f, true, true), 8f), "full set pins both extremes to the shared safe value");
        Check(Near(FourSaintsRules.ClampTemperature(2f, false, false), 2f) && Near(FourSaintsRules.ClampTemperature(30f, false, false), 30f), "no Four Saints pieces leaves temperature untouched");
        Check(FourSaintsRules.XuanwuTemperatureFloor > 6f, "Xuanwu floor clears the flu onset gate at 6");
        Check(FourSaintsRules.ZhuqueTemperatureCeiling < 24f, "Zhuque ceiling stays far below the overheat damage at 24");
        var sparseClothing = new[]
        {
            new ClothingProbe { Index = 200, DisplayOrder = 4 },
            null,
            new ClothingProbe { Index = 201, DisplayOrder = 2 },
            null
        };
        int[] orderedClothing = FourSaintsClothingRules.NonNullInDisplayOrder(sparseClothing, item => item.DisplayOrder)
            .Select(item => item.Index)
            .ToArray();
        Check(orderedClothing.SequenceEqual(new[] { 201, 200 }), "sparse clothing slots are filtered before display ordering");
        Layout(1280, 720, 1f); Layout(1920, 1080, 1f); Layout(1280, 720, 1.35f);
        Console.WriteLine("Four Saints rules/layout/visual budget/clothing compatibility/temperature clamp: PASS (37 assertions)");
        return 0;
    }

    private static void Layout(float width, float height, float scale)
    {
        var look = new FourSaintsHudLayout.Rect { X = width - 160f * scale, Y = height - 160f * scale, Width = 160f * scale, Height = 160f * scale };
        var hotbar = new FourSaintsHudLayout.Rect { X = width / 2f - 280f * scale, Y = height - 72f * scale, Width = 560f * scale, Height = 72f * scale };
        var skill = FourSaintsHudLayout.Place(width, height, look, hotbar, false);
        Check(!skill.Intersects(look), "Baihu avoids Look"); Check(!skill.Intersects(hotbar), "Baihu avoids hotbar");
    }

    private static bool Near(float a, float b) => Math.Abs(a - b) < .0001f;
    private static void Check(bool value, string name) { if (!value) throw new InvalidOperationException(name); }

    private sealed class ClothingProbe
    {
        public int Index;
        public int DisplayOrder;
    }
}

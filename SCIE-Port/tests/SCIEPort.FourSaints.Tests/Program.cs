using System;
using SCIEPort.Features.FourSaints;

internal static class Program
{
    private static int Main()
    {
        Check(FourSaintsRules.EffectiveDamage(.2f, false, true) == 0f, "Xuanwu clamps below three points");
        Check(Near(FourSaintsRules.EffectiveDamage(.5f, false, true), .2f), "Xuanwu subtracts exactly three points");
        Check(Near(FourSaintsRules.EffectiveDamage(1f, true, false), 1.5f), "Baihu damage multiplier is applied once");
        Check(Near(FourSaintsRules.EffectiveDamage(1f, true, true), 1.2f), "Baihu then Xuanwu composition");
        Check(Near(FourSaintsRules.AttackFrequency(true, false, false), 1.2f), "Baihu passive frequency");
        Check(Near(FourSaintsRules.AttackFrequency(true, true, false), 2f), "Awakening replaces passive frequency");
        Check(Near(FourSaintsRules.AttackFrequency(true, true, true), 2.2f), "Mjolnir and Baihu multiply without overwrite");
        float jump = FourSaintsRules.JumpSpeedForOneExtraBlock(6f, 1f);
        Check(Near((jump * jump - 36f) / 20f, 1f), "jump apex gains one block");
        Layout(1280, 720, 1f); Layout(1920, 1080, 1f); Layout(1280, 720, 1.35f);
        Console.WriteLine("Four Saints rules/layout: PASS (14 assertions)");
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
}

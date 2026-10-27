using System;
using SCIEPort.Features.Gungnir;

internal static class Program
{
    private static int s_tests;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        s_tests++;
    }
    private static void Main()
    {
        // Simulate a level-adjusted jump with the actual semi-implicit native gravity step.
        foreach (float level in new[] { 0.5f, 1f, 1.5f, 2f })
        foreach (float dt in new[] { 1f / 30, 1f / 60, 1f / 120 })
        {
            float velocity = GungnirRules.JumpSpeed(level) * (1 + .25f * (level - 1)), height = 0, peak = 0;
            for (int i = 0; i < 500 && (i == 0 || height > 0); i++)
            { velocity -= 10 * dt; height += velocity * dt; peak = Math.Max(peak, height); }
            Check(peak > 3.8f && peak < 4.05f, "Jump apex must remain near four cells across level and frame rates");
        }
        // Repeated malicious packet delivery: only strictly newer half-window sequences pass.
        uint previous = uint.MaxValue - 2;
        int accepted = 0;
        foreach (uint sequence in new uint[] { uint.MaxValue-1, uint.MaxValue-1, uint.MaxValue, 0, uint.MaxValue, 1, 0, 0x80000001 })
            if (GungnirRules.IsNewer(sequence, previous)) { previous = sequence; accepted++; }
        Check(accepted == 4, "Duplicate, stale and half-window requests must be rejected across wrap");
        // A flood of input cannot exceed the equipment rate; blessing is additive, never 2.25x.
        foreach (bool blessed in new[] { false, true })
        {
            int hits = 0; double next = 0;
            for (int i = 0; i < 66000; i++) if (i / 1000.0 >= next) { hits++; next = i / 1000.0 + GungnirRules.AttackInterval(blessed); }
            Check(Math.Abs(hits - (blessed ? 200 : 150)) <= 1, "Authority attack gate changed under command flood");
        }
        Check(GungnirRules.SpeedFactor(false) == 1.5f && GungnirRules.SpeedFactor(true) == 2, "Buff stacking regressed");
        float last = float.MaxValue;
        for (int i = 0; i <= 70; i++)
        { float power = GungnirRules.JudgmentDamage(i / 10f); Check(power <= last && power >= 150 && power <= 300, "Falloff discontinuity"); last = power; }
        Check(GungnirRules.JudgmentDamage(7.001f) == 0 && GungnirRules.JudgmentDamage(float.NaN) == 0 && GungnirRules.JudgmentDamage(float.PositiveInfinity) == 0, "Invalid radius must never damage");
        foreach (var sample in new[]
        {
            (Width: 1280f, Height: 720f, Scale: 1f),
            (Width: 1920f, Height: 1080f, Scale: 1f),
            (Width: 1280f / 1.25f, Height: 720f / 1.25f, Scale: 1.25f),
            (Width: 1920f / 1.5f, Height: 1080f / 1.5f, Scale: 1.5f)
        })
        {
            var look = new GungnirHudLayout.Rect { X = sample.Width - 160f, Y = sample.Height - 160f, Width = 160f, Height = 160f };
            var hotbar = new GungnirHudLayout.Rect { X = sample.Width * .5f - 280f, Y = sample.Height - 72f, Width = 560f, Height = 72f };
            var layout = GungnirHudLayout.Place(sample.Width, sample.Height, look, hotbar, false);
            Check(!layout.Blessing.Intersects(look) && !layout.Judgment.Intersects(look), "Skill buttons overlap native Look at scale " + sample.Scale);
            Check(!layout.Blessing.Intersects(hotbar) && !layout.Judgment.Intersects(hotbar), "Skill buttons overlap hotbar at scale " + sample.Scale);
            Check(Math.Abs(layout.Blessing.Width - 72f) < .01f && Math.Abs(layout.Judgment.Width - 88f) < .01f, "Logical button sizes changed");
            Check(layout.Judgment.X > layout.Blessing.X && layout.Judgment.Y < layout.Blessing.Y, "Judgment must remain upper-right of Blessing");
            Console.WriteLine($"HUD {sample.Width:0.#}x{sample.Height:0.#} ui={sample.Scale:0.##}: blessing=({layout.Blessing.X:0.#},{layout.Blessing.Y:0.#},72,72) judgment=({layout.Judgment.X:0.#},{layout.Judgment.Y:0.#},88,88) lookOverlap=false hotbarOverlap=false");
        }
        var leftLook = new GungnirHudLayout.Rect { X = 0, Y = 560, Width = 160, Height = 160 };
        var leftLayout = GungnirHudLayout.Place(1280, 720, leftLook, default(GungnirHudLayout.Rect), true);
        Check(!leftLayout.Blessing.Intersects(leftLook) && !leftLayout.Judgment.Intersects(leftLook) && leftLayout.Mirrored, "Left-handed native layout was not mirrored safely");
        Check(GungnirVisualRules.ReturnTrailLength > GungnirVisualRules.OutboundTrailLength, "Return trail must remain longer and easier to track than outbound");
        foreach (int quality in new[] { -1, 0, 1, 2, 3 })
        {
            int clamped = quality < 0 ? 0 : quality > 2 ? 2 : quality;
            Check(GungnirVisualRules.TrailBands(quality) == clamped + 1, "Trail quality clamp or band budget changed");
            Check(GungnirVisualRules.ReturnSpiralSegments(quality) == 5 + clamped * 2, "Return spiral quality budget changed");
            Check(GungnirVisualRules.ReturnRuneCount(quality) >= 1, "Low quality must retain a return rune");
            Check(GungnirVisualRules.ReturnRingCount(quality) >= 1, "Low quality must retain a return ring");
        }
        foreach (float life in new[] { GungnirVisualRules.HitCueLife, GungnirVisualRules.ReturnCueLife })
        {
            Check(GungnirVisualRules.CueAlpha(-0.01f, life) == 0f && GungnirVisualRules.CueAlpha(life, life) == 0f, "Flight cue lifetime bounds changed");
            Check(GungnirVisualRules.CueAlpha(life * 0.2f, life) > 0f, "Flight cue must be visible after attack-in");
        }
        Console.WriteLine("Gungnir: " + s_tests + " checks passed (pure rules only; game/network/device unverified).");
    }
}

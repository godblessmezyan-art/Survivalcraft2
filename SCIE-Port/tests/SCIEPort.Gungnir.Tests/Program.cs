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
        Check(GungnirRules.BlessingCooldown == 8f, "Rune Blessing cooldown must be 8 seconds");
        Check(GungnirRules.JudgmentCooldown == 50f, "Eternal Judgment cooldown must be 50 seconds");
        Check(GungnirRules.HolyDamageDelay == 0.4f, "holy follow-up must remain visually separated from the native hit");
        Check(!GungnirRules.ShouldConsumeAim(true) && GungnirRules.ShouldConsumeAim(false), "hold must not trigger x25.01.17 aim cancellation");
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
        Check(GungnirSkyRules.SkyBlend(-0.01f) == 0f && GungnirSkyRules.SkyBlend(9f) == 0f, "Sky lifecycle must be closed outside nine seconds");
        Check(GungnirSkyRules.SkyBlend(0.6f) > 0f && GungnirSkyRules.SkyBlend(1.2f) > 0.99f, "Edict sky must fade in across phase A");
        Check(GungnirSkyRules.SkyBlend(7f) > 0.99f && GungnirSkyRules.SkyBlend(8f) > 0f && GungnirSkyRules.SkyBlend(8.99f) > 0f, "Oracle afterglow must recover gradually from seven to nine seconds");
        float fullFlash = GungnirSkyRules.ImpactFlash(1.4f, false);
        Check(fullFlash == 1f && GungnirSkyRules.ImpactFlash(1.4f, true) == 0.35f && GungnirSkyRules.ImpactFlash(1.7f, false) == 0f, "Impact pulse or reduced-flash policy changed");
        float combinedSky = GungnirSkyRules.Combine(0.7f, 0.6f);
        float divineShare = GungnirSkyRules.DivineShare(0.7f, 0.6f);
        Check(combinedSky > 0.7f && combinedSky <= 1f, "Concurrent artifact skies must use screen composition");
        Check(divineShare > 0f && divineShare < 1f, "Concurrent Mjolnir and Gungnir skies must both retain weight");
        Check(GungnirSkyRules.DivineShare(0.7f, 0f) == 0f && GungnirSkyRules.DivineShare(0f, 0.6f) == 1f, "Single-artifact sky priority changed");
        Check(!GungnirSkyRules.ShouldRemove(8.99f, 2f, true), "Live sky was cleaned before its tail completed");
        Check(GungnirSkyRules.ShouldRemove(9f, 0f, true) && GungnirSkyRules.ShouldRemove(2f, 2.11f, true) && GungnirSkyRules.ShouldRemove(2f, 0f, false), "Expiry, disconnect or death cleanup changed");
        Check(GungnirSkyRules.CloudBands(0) == 0 && GungnirSkyRules.CloudBands(1) == 3 && GungnirSkyRules.CloudBands(2) == 5, "Cloud quality budget changed");
        Check(GungnirSkyRules.LightColumns(0) == 0 && GungnirSkyRules.LightColumns(1) == 3 && GungnirSkyRules.LightColumns(2) == 6, "Light-column quality budget changed");
        Check(GungnirSkyRules.SkyRunes(0) == 2 && GungnirSkyRules.SkyRunes(1) == 5 && GungnirSkyRules.SkyRunes(2) == 9, "Rune quality budget changed");
        Check(GungnirRules.IsThrowSignal(GungnirRules.ThrowBegin) && GungnirRules.IsThrowSignal(GungnirRules.ThrowComplete) && GungnirRules.IsThrowSignal(GungnirRules.ThrowCancel) && !GungnirRules.IsThrowSignal(0), "Throw signal domain changed");
        Check(!GungnirRules.CanCompleteThrow(false, 1.0, 1f) && !GungnirRules.CanCompleteThrow(true, 0.1, 0.1f), "Throw must require a begin signal and long press");
        Check(GungnirRules.CanCompleteThrow(true, 0.3, 0.1f) && GungnirRules.CanCompleteThrow(true, 0.01, 0.3f), "Host clock and batched client duration must both support a valid long press");
        Check(!GungnirRules.CanCompleteThrow(true, 1.0, 10.01f), "Unbounded reported throw charge must be rejected");
        Console.WriteLine("Gungnir: " + s_tests + " checks passed (pure rules only; game/network/device unverified).");
    }
}

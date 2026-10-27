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
        Console.WriteLine("Gungnir: " + s_tests + " checks passed (pure rules only; game/network/device unverified).");
    }
}

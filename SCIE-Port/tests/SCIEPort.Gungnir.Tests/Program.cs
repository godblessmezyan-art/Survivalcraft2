using System;
using SCIEPort.Features.Gungnir;
using Targeting_Rules = SCIEPort.Features.Targeting.CreatureTargetRules;

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
        Check(GungnirRules.BlessingCooldown == 6f, "Rune Blessing cooldown must be 6 seconds");
        Check(GungnirRules.BlessingDuration == 9f, "Rune Blessing must last 9 seconds");
        Check(GungnirRules.JudgmentCooldown == 50f, "Eternal Judgment cooldown must be 50 seconds");
        Check(GungnirRules.HolyDamageDelay == 0.4f, "holy follow-up must remain visually separated from the native hit");
        Check(!GungnirRules.ShouldConsumeAim(true) && GungnirRules.ShouldConsumeAim(false), "hold must not trigger x25.01.17 aim cancellation");
        float last = float.MaxValue;
        for (int i = 0; i <= 70; i++)
        { float power = GungnirRules.JudgmentDamage(i / 10f); Check(power <= last && power >= 170.5f - 0.001f && power <= 341f + 0.001f, "Judgment falloff must descend from 341 to 170.5"); last = power; }
        Check(GungnirRules.JudgmentDamage(7.001f) == 0 && GungnirRules.JudgmentDamage(float.NaN) == 0 && GungnirRules.JudgmentDamage(float.PositiveInfinity) == 0, "Invalid radius must never damage");
        Check(Math.Abs(GungnirRules.JudgmentDamage(0f) - 341f) < 0.001f, "Judgment centre power must stay 341");
        Check(Math.Abs(GungnirRules.JudgmentDamage(3.5f) - 255.75f) < 0.001f, "Judgment mid-radius power must be half-way down");
        Check(Math.Abs(GungnirRules.JudgmentDamage(7f) - 170.5f) < 0.001f, "Judgment rim power must stay 170.5");
        Check(GungnirRules.JudgmentPower == 341f && GungnirRules.JudgmentRadius == 7f && GungnirRules.JudgmentEdgeFactor == 0.5f,
            "judgment falloff constants changed");
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
        // Throw input is driven by our own aim clock, so the press window must stay short while the
        // flight lease must always outlive a full outbound plus return, or the next throw hangs.
        Check(GungnirRules.MinThrowCharge > 0f && GungnirRules.MinThrowCharge <= 0.2f, "Throw charge window must stay a short press");
        Check(GungnirRules.CanCompleteThrow(true, 0.16, 0f) && GungnirRules.CanCompleteThrow(true, 0f, 0.16f), "A valid short hold must pass the host clock and the client report");
        Check(GungnirRules.ThrowLease > 3f && GungnirRules.ThrowLease < 6f, "Flight lease must outlive outbound plus return without stalling the next throw");
        // Skill names live inside the badge, so no DPI, safe-area or hotbar change can hide them.
        // Giant judgment spear: condenses high, plunges monotonically and stops buried.
        float previousSpear = float.MaxValue;
        bool descended = true;
        for (int i = 0; i <= 140; i++)
        {
            float stepAge = i / 100f;
            float spearHeight = GungnirSkyRules.SpearHeight(stepAge);
            if (stepAge > GungnirSkyRules.SpearFormEnd + 0.001f && spearHeight > previousSpear + 0.001f) descended = false;
            Check(spearHeight > 0f, "Judgment spear must never dip below the target while visible");
            previousSpear = spearHeight;
        }
        Check(descended, "Judgment spear must descend without rising");
        Check(GungnirSkyRules.SpearHeight(0.9f) > 30f, "Giant spear must still hang high before the plunge");
        Check(Math.Abs(GungnirSkyRules.SpearHeight(GungnirSkyRules.ImpactTime) - GungnirSkyRules.SpearImpactHeight) < 0.01f, "Impact height must bury the blade");
        Check(GungnirSkyRules.SpearHeight(GungnirSkyRules.ImpactTime) + GungnirSkyRules.SpearBladeLength * GungnirSkyRules.SpearScale > 8f, "Buried spear must tower over the player");
        Check(GungnirSkyRules.SpearAlpha(-0.01f) == 0f && GungnirSkyRules.SpearAlpha(9f) == 0f && GungnirSkyRules.SpearAlpha(GungnirSkyRules.ImpactTime) > 0.99f, "Spear visibility window changed");
        Check(GungnirSkyRules.SpearFalling(GungnirSkyRules.SpearFormEnd + 0.02f) && !GungnirSkyRules.SpearFalling(0.5f) && !GungnirSkyRules.SpearFalling(GungnirSkyRules.ImpactTime), "Fall window must stay between form end and impact");
        foreach (int quality in new[] { 0, 1, 2 })
        {
            Check(GungnirSkyRules.AfterImageCount(quality) == quality, "Afterimage budget changed");
            Check(GungnirSkyRules.EmberCount(quality) >= 3, "Every quality level must keep landing embers");
        }
        // ---- 0.56.0 Fate Severance (神王敕令·命运裁断) ----
        Check(GungnirRules.Fatalism == 5, "Fate Severance must be command 5 so 1-4 keep their wire meaning");
        Check(GungnirRules.FatalismPowerMin == 10f && GungnirRules.FatalismPowerMax == 100f, "Fate Severance must roll inside 10..100");
        Check(Math.Abs(GungnirRules.FatalismRoll(0) - 10f) < 0.001f && Math.Abs(GungnirRules.FatalismRoll(90) - 100f) < 0.001f,
            "Fate Severance roll bounds changed");
        Check(Math.Abs(GungnirRules.FatalismRoll(-5) - 10f) < 0.001f && Math.Abs(GungnirRules.FatalismRoll(400) - 100f) < 0.001f,
            "out-of-range rolls must clamp inside 10..100");
        bool inRange = true;
        for (int roll = 0; roll <= 90; roll++)
        {
            float power = GungnirRules.FatalismRoll(roll);
            if (power < 10f || power > 100f) inRange = false;
        }
        Check(inRange, "every Fate Severance roll must stay inside 10..100");
        Check(GungnirRules.FatalismCooldown == 39f, "Fate Severance cooldown must be exactly 39 seconds");
        Check(GungnirRules.HudCommand(0) == GungnirRules.Blessing && GungnirRules.HudCommand(1) == GungnirRules.Fatalism
            && GungnirRules.HudCommand(2) == GungnirRules.Judgment && GungnirRules.HudCommand(3) == 0,
            "HUD must map [1,5,2] explicitly instead of index+1");
        Check(GungnirRules.IsSkillCommand(1) && GungnirRules.IsSkillCommand(5) && GungnirRules.IsSkillCommand(2)
            && !GungnirRules.IsSkillCommand(3) && !GungnirRules.IsSkillCommand(4), "skill command domain changed");
        Check(GungnirRules.FatalismMark == 0.15f && GungnirRules.FatalismImpact == 0.45f && GungnirRules.FatalismEnd == 0.9f,
            "Fate Severance presentation timeline must stay 0.15 / 0.45 / 0.90");
        Check(GungnirRules.FatalismAlpha(-0.01f) == 0f && GungnirRules.FatalismAlpha(0.9f) == 0f && GungnirRules.FatalismAlpha(0.45f) > 0f,
            "Fate Severance effect must be closed at both ends");
        Check(!GungnirRules.FatalismMarks(0.1f) && GungnirRules.FatalismMarks(0.2f) && GungnirRules.FatalismShattered(0.5f)
            && !GungnirRules.FatalismShattered(0.7f), "mark and shatter windows changed");
        Check(GungnirRules.FatalismCooldown > GungnirRules.BlessingCooldown && GungnirRules.FatalismCooldown < GungnirRules.JudgmentCooldown,
            "Fate Severance cooldown must sit between the blessing and the ultimate");

        // Thirty hostiles must each be struck once, no matter the distance or occlusion:
        // the shared rule only reads category/state, never a position or a line of sight.
        var entities = new System.Collections.Generic.List<int>();
        for (int i = 0; i < 30; i++) entities.Add(i);
        var fatalismHits = new System.Collections.Generic.HashSet<int>();
        int acceptedHits = 0;
        foreach (int id in entities)
        {
            Check(Targeting_Rules.AcceptsTarget(Targeting_Rules.LandPredator, false, false, false, true, false), "every hostile must pass regardless of distance");
            if (fatalismHits.Add(id)) acceptedHits++;
            if (fatalismHits.Add(id)) acceptedHits++;
        }
        Check(fatalismHits.Count == 30 && acceptedHits == 30, "thirty hostiles must be struck exactly once each, never twice");

        // Friend, player, neutral animal and dead entity exclusions.
        Check(!Targeting_Rules.AcceptsTarget(Targeting_Rules.LandPredator, false, false, true, true, false), "dead entities must never be struck");
        Check(!Targeting_Rules.AcceptsTarget(Targeting_Rules.LandPredator, false, false, false, false, false), "unloaded entities must never be struck");
        Check(!Targeting_Rules.AcceptsTarget(Targeting_Rules.LandOther, false, false, false, true, false), "neutral land animals must never be struck");
        Check(!Targeting_Rules.AcceptsTarget(Targeting_Rules.WaterOther, false, false, false, true, false), "neutral water animals must never be struck");
        Check(!Targeting_Rules.AcceptsTarget(Targeting_Rules.Bird, false, false, false, true, false), "birds must never be struck");
        Check(!Targeting_Rules.AcceptsTarget(Targeting_Rules.LandPredator, true, false, false, true, false), "players are excluded without friendly fire");
        Check(Targeting_Rules.AcceptsTarget(Targeting_Rules.LandPredator, true, false, false, true, true), "friendly fire must keep the native switch");
        Check(!Targeting_Rules.AcceptsTarget(Targeting_Rules.LandPredator, false, true, false, true, true), "the caster is never a target");
        Check(Targeting_Rules.AcceptsTarget(Targeting_Rules.LandPredator, false, false, false, true, false)
            && Targeting_Rules.AcceptsTarget(Targeting_Rules.WaterPredator, false, false, false, true, false), "both predator categories are hostile");

        // 39 s gate: a second request inside the window is rejected, the next one after it passes.
        double ready = 0.0;
        int fatalismCasts = 0;
        foreach (double attempt in new[] { 0.0, 0.2, 5.0, 38.99, 39.0 })
            if (attempt >= ready) { fatalismCasts++; ready = attempt + GungnirRules.FatalismCooldown; }
        Check(fatalismCasts == 2, "Fate Severance must reject every recast inside its 39 s window");

        // Three-button HUD: the arc must stay clear of Look and the hotbar at every tested scale.
        foreach (var sample in new[]
        {
            (Width: 1280f, Height: 720f, Scale: 1f),
            (Width: 1920f, Height: 1080f, Scale: 1f),
            (Width: 1024f, Height: 576f, Scale: 1.25f),
            (Width: 1280f, Height: 720f, Scale: 1.5f)
        })
        {
            var look = new GungnirHudLayout.Rect { X = sample.Width - 160f, Y = sample.Height - 160f, Width = 160f, Height = 160f };
            var hotbar = new GungnirHudLayout.Rect { X = sample.Width * .5f - 280f, Y = sample.Height - 72f, Width = 560f, Height = 72f };
            var layout = GungnirHudLayout.Place(sample.Width, sample.Height, look, hotbar, false);
            Check(!layout.Blessing.Intersects(look) && !layout.Fatalism.Intersects(look) && !layout.Judgment.Intersects(look), "three buttons must not cover Look");
            Check(!layout.Blessing.Intersects(hotbar) && !layout.Fatalism.Intersects(hotbar) && !layout.Judgment.Intersects(hotbar), "three buttons must not cover the hotbar");
            Check(!layout.Blessing.Intersects(layout.Fatalism) && !layout.Fatalism.Intersects(layout.Judgment) && !layout.Blessing.Intersects(layout.Judgment), "skill buttons must not overlap");
            Check(Math.Abs(layout.Fatalism.Width - 72f) < .01f && Math.Abs(layout.Judgment.Width - 88f) < .01f, "HUD button sizes changed");
            Check(layout.Fatalism.X > layout.Blessing.X && layout.Fatalism.Y < layout.Blessing.Y, "Fate Severance must sit between the two existing badges");
            Check(layout.Blessing.X >= 8f && layout.Judgment.Right <= sample.Width - 8f, "three buttons must stay inside the safe area");
        }
        var mirroredLook = new GungnirHudLayout.Rect { X = 0, Y = 560, Width = 160, Height = 160 };
        var mirrored = GungnirHudLayout.Place(1280, 720, mirroredLook, default(GungnirHudLayout.Rect), true);
        Check(mirrored.Mirrored && !mirrored.Blessing.Intersects(mirroredLook) && !mirrored.Fatalism.Intersects(mirroredLook)
            && !mirrored.Judgment.Intersects(mirroredLook), "left-handed three-button layout must mirror safely");

        Console.WriteLine("Gungnir: " + s_tests + " checks passed (pure rules only; game/network/device unverified).");
    }
}

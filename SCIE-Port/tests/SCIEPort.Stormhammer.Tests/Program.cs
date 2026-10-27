using System;
using SCIEPort.Features.Stormhammer;

internal static class Program
{
    private static int s_count;

    private static int Main()
    {
        PathIsContinuousAndAnchored();
        PathIsDeterministic();
        TremorLifetimeShape();
        DamageTables();
        StormSchedule();
        PhasedStormSchedule();
        GreatBoltSchedule();
        EquipmentBonuses();
        Console.WriteLine("Skywrath V5 lightning rule tests passed: " + s_count);
        return 0;
    }

    private static void EquipmentBonuses()
    {
        Assert(Math.Abs(StormhammerEquipmentRules.MovementFactor - 1.2f) < 0.0001f, "Mjolnir movement bonus is 20 percent");
        Assert(Math.Abs(StormhammerEquipmentRules.AttackSpeedFactor - 1.1f) < 0.0001f, "Mjolnir attack-speed bonus is 10 percent");
        Assert(Math.Abs(StormhammerEquipmentRules.AttackInterval - 0.6) < 0.0001, "10 percent attack speed maps native 0.66 s to 0.60 s");
        Assert(Math.Abs(StormhammerEquipmentRules.LastHitTimeAdvance - 0.06) < 0.0001, "native hit timestamp adjustment is exact");
    }

    private static void PathIsContinuousAndAnchored()
    {
        float[] path = new float[LightningRules.PathFloats];
        LightningRules.BuildPath(0f, 40f, 0f, 0f, 0f, 0f, 18, 1.6f, 0x1234u, path, out int points);
        Assert(points == 19, "segment count honoured: " + points);

        Equal(0f, path[0], "start x anchored");
        Equal(40f, path[1], "start y anchored");
        Equal(0f, path[2], "start z anchored");
        int last = (points - 1) * LightningRules.PathStride;
        Equal(0f, path[last], "end x anchored");
        Equal(0f, path[last + 1], "end y anchored");
        Equal(0f, path[last + 2], "end z anchored");

        float previousGap = 0f;
        float maximumGap = 0f;
        for (int i = 0; i < points; i++)
        {
            int offset = i * LightningRules.PathStride;
            if (i > 0)
            {
                int previous = offset - LightningRules.PathStride;
                float dx = path[offset] - path[previous];
                float dy = path[offset + 1] - path[previous + 1];
                float dz = path[offset + 2] - path[previous + 2];
                float gap = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
                maximumGap = Math.Max(maximumGap, gap);
                Assert(gap > 0f, "no duplicated points (continuity) at " + i);
            }

            // Displacement must stay bounded: a bolt is jagged, not a scatter cloud.
            float deviation = (float)Math.Sqrt(path[offset] * path[offset] + path[offset + 2] * path[offset + 2]);
            Assert(deviation <= 2.2f, "lateral displacement bounded at " + i + ": " + deviation);
            Assert(path[offset + 1] <= 40.001f && path[offset + 1] >= -0.001f, "vertical displacement bounded at " + i);
        }

        // A straight 40 block bolt with 18 segments is 2.22 blocks per step; jag adds at most ~80%.
        Assert(maximumGap < 2.2222f * 1.9f, "segments stay continuous, max gap " + maximumGap);
        Assert(previousGap == 0f, "sanity");
    }

    private static void PathIsDeterministic()
    {
        float[] a = new float[LightningRules.PathFloats];
        float[] b = new float[LightningRules.PathFloats];
        float[] c = new float[LightningRules.PathFloats];
        LightningRules.BuildPath(1f, 30f, 2f, -3f, 0f, 8f, 12, 1.1f, 0xABCDEFu, a, out int countA);
        LightningRules.BuildPath(1f, 30f, 2f, -3f, 0f, 8f, 12, 1.1f, 0xABCDEFu, b, out int countB);
        LightningRules.BuildPath(1f, 30f, 2f, -3f, 0f, 8f, 12, 1.1f, 0xABCDEEu, c, out int countC);
        Assert(countA == countB && countA == countC, "same segment count");
        bool identical = true;
        bool different = false;
        for (int i = 0; i < countA * LightningRules.PathStride; i++)
        {
            if (Math.Abs(a[i] - b[i]) > 0.0001f) identical = false;
            if (Math.Abs(a[i] - c[i]) > 0.0001f) different = true;
        }

        Assert(identical, "same seed reproduces the same bolt");
        Assert(different, "different seed changes the bolt");
    }

    private static void TremorLifetimeShape()
    {
        Equal(0f, LightningRules.LifeAlpha(-0.1, 0.5, 0.06f, 0.4f), "not alive before birth");
        Equal(0f, LightningRules.LifeAlpha(0.5, 0.5, 0.06f, 0.4f), "not alive at the end");
        float attack = LightningRules.LifeAlpha(0.015, 0.5, 0.06f, 0.4f);
        Assert(attack > 0f && attack < 1f, "attack ramps up: " + attack);
        Equal(1f, LightningRules.LifeAlpha(0.20, 0.5, 0.06f, 0.4f), "hold is full brightness");
        float release = LightningRules.LifeAlpha(0.45, 0.5, 0.06f, 0.4f);
        Assert(release > 0f && release < 0.5f, "release decays: " + release);

        Equal(1f, LightningRules.Taper(0f, 1f, 0.45f), "root width");
        Equal(0.45f, LightningRules.Taper(1f, 1f, 0.45f), "tip width");
        Assert(LightningRules.Taper(0.5f, 1f, 0.45f) < 1f, "taper narrows toward the tip");
    }

    private static void DamageTables()
    {
        Equal(110f, LightningRules.JudgmentDamage(0f), "judgment core");
        Equal(110f, LightningRules.JudgmentDamage(4f), "judgment inner edge");
        Assert(Math.Abs(LightningRules.JudgmentDamage(9f - 0.001f) - 70f) < 0.05f, "judgment outer edge");
        Equal(0f, LightningRules.JudgmentDamage(9f), "judgment outside radius");
        Assert(LightningRules.JudgmentDamage(6f) < LightningRules.JudgmentDamage(4.5f), "judgment falls off");

        Equal(220f, LightningRules.TribulationDamage(0f), "tribulation core");
        Equal(220f, LightningRules.TribulationDamage(2.5f), "tribulation core edge");
        Equal(120f, LightningRules.TribulationDamage(5f), "tribulation mid edge");
        Assert(Math.Abs(LightningRules.TribulationDamage(8f - 0.001f) - 70f) < 0.05f, "tribulation outer edge");
        Equal(70f, LightningRules.TribulationDamage(8f), "tribulation radius edge is inclusive");
        Equal(0f, LightningRules.TribulationDamage(8.001f), "tribulation outside radius");
        Assert(LightningRules.TribulationDamage(4f) > LightningRules.TribulationDamage(7f), "tribulation falls off");
        Assert(LightningRules.TribulationDamage(1f) > LightningRules.JudgmentDamage(1f) * 1.9f, "ultimate hits far harder");
        Equal(18f, LightningRules.AftershockDamage, "aftershock tick value");
    }

    private static void StormSchedule()
    {
        const uint seed = 0x5F356495u;
        LightningRules.StormStrike[] schedule = LightningRules.BuildStormSchedule(seed, 1.15f, 4.35f, 10, 26);
        Assert(schedule.Length == 36, "schedule size: " + schedule.Length);

        float previous = -1f;
        int near = 0;
        int far = 0;
        foreach (LightningRules.StormStrike strike in schedule)
        {
            Assert(strike.Time >= 1.10f && strike.Time <= 4.45f, "strike inside the show window: " + strike.Time);
            Assert(strike.Time >= previous, "schedule is sorted by time");
            previous = strike.Time;
            if (strike.Near)
            {
                near++;
                Assert(strike.Distance >= 14.9f && strike.Distance <= 25.1f, "near distance band: " + strike.Distance);
                Assert(Math.Abs(strike.Angle) <= (43f * Math.PI / 180f) + 0.001f, "near cone: " + strike.Angle);
            }
            else
            {
                far++;
                Assert(strike.Distance >= 24.9f && strike.Distance <= 55.1f, "far distance band: " + strike.Distance);
                Assert(Math.Abs(strike.Angle) <= (49f * Math.PI / 180f) + 0.001f, "far cone: " + strike.Angle);
            }

            Assert(strike.Height >= 25f, "strikes start above the caster");
        }

        Assert(near == 10 && far == 26, "layer counts");

        LightningRules.StormStrike[] repeat = LightningRules.BuildStormSchedule(seed, 1.15f, 4.35f, 10, 26);
        bool identical = true;
        for (int i = 0; i < schedule.Length; i++)
        {
            if (Math.Abs(schedule[i].Time - repeat[i].Time) > 0.0001f
                || Math.Abs(schedule[i].Angle - repeat[i].Angle) > 0.0001f
                || schedule[i].Seed != repeat[i].Seed)
            {
                identical = false;
            }
        }

        Assert(identical, "schedule is deterministic for a cast id");

        Assert(LightningRules.DueStrikes(schedule, 0f, 1.14f) == 0, "nothing due before the show");
        int firstSlice = LightningRules.DueStrikes(schedule, 1.14f, 2.0f);
        Assert(firstSlice > 0, "strikes start inside the first slice");
        Assert(LightningRules.DueStrikes(schedule, 4.4f, 10f) == 0, "nothing due after the window");
        int all = LightningRules.DueStrikes(schedule, -1f, 10f);
        Assert(all == schedule.Length, "a full catch-up walk sees every strike: " + all);
    }

    /// <summary>
    /// The heavy layer: full sized bolts that have to look like Mjolnir, not backdrop ticks.
    /// Everything here is deterministic so two clients render the same bombardment.
    /// </summary>
    private static void GreatBoltSchedule()
    {
        LightningRules.StormStrike[] great = LightningRules.BuildGreatBoltSchedule(0x2B7E1516u, 0.55f, 11.20f, 11, 8f, 18f);
        Assert(great.Length == 11, "great bolt count: " + great.Length);

        float previous = -1f;
        for (int i = 0; i < great.Length; i++)
        {
            Assert(great[i].Great, "great bolts are flagged as the heavy layer at " + i);
            Assert(great[i].Time >= 0.55f - 0.001f && great[i].Time <= 11.21f, "great bolt inside the window: " + great[i].Time);
            Assert(great[i].Time >= previous, "great bolts stay sorted");
            Assert(great[i].Distance >= 7.9f && great[i].Distance <= 18.1f, "great bolt distance band: " + great[i].Distance);
            Assert(Math.Abs(great[i].Angle) <= (34f * Math.PI / 180f) + 0.001f, "great bolt cone: " + great[i].Angle);
            Assert(great[i].Height >= 29.9f, "great bolt starts above the caster");
            previous = great[i].Time;
        }

        // Spread, not clustered: the first and last must sit in their halves of the window.
        Assert(great[0].Time < 3.0f, "the first great bolt lands early: " + great[0].Time);
        Assert(great[great.Length - 1].Time > 9.5f, "the last great bolt lands late: " + great[great.Length - 1].Time);

        LightningRules.StormStrike[] repeat = LightningRules.BuildGreatBoltSchedule(0x2B7E1516u, 0.55f, 11.20f, 11, 8f, 18f);
        bool identical = repeat.Length == great.Length;
        for (int i = 0; identical && i < repeat.Length; i++)
        {
            if (repeat[i].Seed != great[i].Seed || Math.Abs(repeat[i].Time - great[i].Time) > 0.0001f)
            {
                identical = false;
            }
        }

        Assert(identical, "great bolt schedule is deterministic for a cast id");

        LightningRules.StormStrike[] other = LightningRules.BuildGreatBoltSchedule(0x2B7E1517u, 0.55f, 11.20f, 11, 8f, 18f);
        bool differs = false;
        for (int i = 0; i < other.Length; i++)
        {
            if (Math.Abs(other[i].Time - great[i].Time) > 0.0001f) differs = true;
        }

        Assert(differs, "a different cast id changes the great bolt rhythm");
        Assert(LightningRules.BuildGreatBoltSchedule(1u, 0.55f, 11.2f, 0, 8f, 18f).Length == 0, "zero great bolts is allowed");
        Assert(LightningRules.BuildGreatBoltSchedule(1u, 5f, 5f, 4, 8f, 18f).Length == 0, "an empty window produces nothing");

        // Backdrop strikes must never be mistaken for the heavy layer.
        var phases = new[] { new LightningRules.StormPhase(0.05f, 1.10f, 4, 10) };
        foreach (LightningRules.StormStrike strike in LightningRules.BuildStormSchedule(0x51ED27u, phases))
        {
            Assert(!strike.Great, "backdrop strikes are not great bolts");
        }
    }

    private static void PhasedStormSchedule()
    {
        var phases = new[]
        {
            new LightningRules.StormPhase(1.15f, 2.70f, 5, 18),
            new LightningRules.StormPhase(2.70f, 4.10f, 6, 26),
            new LightningRules.StormPhase(4.10f, 4.85f, 1, 4),
            new LightningRules.StormPhase(4.85f, 6.80f, 9, 44),
            new LightningRules.StormPhase(6.80f, 11.60f, 6, 26)
        };

        LightningRules.StormStrike[] schedule = LightningRules.BuildStormSchedule(0x51ED27u, phases);
        Assert(schedule.Length == 145, "phased schedule size: " + schedule.Length);

        float previous = -1f;
        int near = 0;
        int far = 0;
        int hush = 0;
        int climax = 0;
        int sustain = 0;
        foreach (LightningRules.StormStrike strike in schedule)
        {
            Assert(strike.Time >= 1.10f && strike.Time <= 11.65f, "strike inside the 12 s window: " + strike.Time);
            Assert(strike.Time >= previous, "phased schedule stays sorted");
            previous = strike.Time;
            if (strike.Near) near++;
            else far++;
            if (strike.Time >= 4.10f && strike.Time < 4.85f) hush++;
            if (strike.Time >= 4.85f && strike.Time < 6.80f) climax++;
            if (strike.Time >= 6.80f) sustain++;
        }

        Assert(near == 27 && far == 118, "layer split: " + near + "/" + far);
        Assert(hush > 0 && climax > 0 && sustain > 0, "every phase actually emits");
        Assert(climax > hush * 4, "impact window is denser than the hush window: " + climax + " vs " + hush);
        Assert(sustain > hush, "sustain keeps the backdrop alive for the whole buff");
        Assert(sustain >= 6, "sustain phase reaches past the 10 s mark");

        // The 12 s storm must actually cover the buff, not stop at 4.35 s like V4.
        Assert(schedule[schedule.Length - 1].Time > 10.5f, "schedule reaches the end of the buff");

        LightningRules.StormStrike[] repeat = LightningRules.BuildStormSchedule(0x51ED27u, phases);
        bool identical = true;
        for (int i = 0; i < schedule.Length; i++)
        {
            if (Math.Abs(schedule[i].Time - repeat[i].Time) > 0.0001f || schedule[i].Seed != repeat[i].Seed)
            {
                identical = false;
            }
        }

        Assert(identical, "phased schedule is deterministic");

        Assert(LightningRules.DueStrikes(schedule, 4.0f, 4.9f) > 0, "hush window still emits a few strikes");
        Assert(LightningRules.DueStrikes(schedule, 6.0f, 6.1f) > 0, "climax keeps striking");
        Assert(LightningRules.DueStrikes(schedule, 6.0f, 11.7f) > 20, "sustain keeps a steady rate");

        Assert(LightningRules.BuildStormSchedule(1u, null).Length == 0, "null phases are tolerated");
        Assert(LightningRules.BuildStormSchedule(1u, new LightningRules.StormPhase[0]).Length == 0, "empty phases are tolerated");
    }

    private static void Assert(bool condition, string message)
    {
        s_count++;
        if (!condition)
        {
            throw new InvalidOperationException("Assertion failed: " + message);
        }
    }

    private static void Equal(float expected, float actual, string message)
    {
        Assert(Math.Abs(expected - actual) < 0.0001f, message + " (expected " + expected + ", got " + actual + ")");
    }
}

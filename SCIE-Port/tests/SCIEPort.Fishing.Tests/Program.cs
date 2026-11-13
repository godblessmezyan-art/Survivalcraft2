using System;
using SCIEPort.Features.Fishing;

internal static class Program
{
    private static int s_failures;

    private static void Check(bool condition, string name)
    {
        if (condition) return;
        s_failures++;
        Console.WriteLine("FAIL " + name);
    }

    private static int Main()
    {
        var random = new Random(20261114);
        for (int i = 0; i < 500; i++)
        {
            float wait = FishingRules.WaitDuration(random);
            Check(wait >= FishingRules.MinWaitDuration && wait <= FishingRules.MaxWaitDuration, "wait duration range");
        }

        Check(FishingRules.Reel(1.99f, 2f, 0.9f) == FishingRules.ReelResult.Early, "reel before bite");
        Check(FishingRules.Reel(2f, 2f, 0.9f) == FishingRules.ReelResult.Success, "reel at bite start");
        Check(FishingRules.Reel(2.9f, 2f, 0.9f) == FishingRules.ReelResult.Success, "reel at window end");
        Check(FishingRules.Reel(2.91f, 2f, 0.9f) == FishingRules.ReelResult.Late, "reel after window");

        Check(FishingRules.RewardCount(0) == 1, "reward roll 0");
        Check(FishingRules.RewardCount(69) == 1, "reward roll 69");
        Check(FishingRules.RewardCount(70) == 2, "reward roll 70");
        Check(FishingRules.RewardCount(94) == 2, "reward roll 94");
        Check(FishingRules.RewardCount(95) == 3, "reward roll 95");
        Check(FishingRules.RewardCount(99) == 3, "reward roll 99");

        Check(!FishingRules.IsWaterDepthValid(0), "dry cell");
        Check(!FishingRules.IsWaterDepthValid(1), "puddle");
        Check(FishingRules.IsWaterDepthValid(2), "surface water");
        Check(FishingRules.IsWaterDepthValid(8), "deep water");

        Check(!FishingRules.ShouldEndSession(20f), "session at limit");
        Check(FishingRules.ShouldEndSession(20.01f), "session beyond limit");

        var limiter = new FishingRateLimiter();
        Check(limiter.TryAccept(100d), "first catch");
        Check(!limiter.TryAccept(100.5d), "spam rejected");
        Check(limiter.TryAccept(100.81d), "cooldown elapsed");
        for (int i = 0; i < 16; i++) limiter.TryAccept(102d + i * 1d);
        Check(!limiter.TryAccept(120d), "minute cap reached");
        Check(limiter.TryAccept(161d), "minute window reset");

        limiter.Reset();
        Check(limiter.TryAccept(0d), "reset allows catch");

        Console.WriteLine(s_failures == 0 ? "Fishing rules: all checks passed." : "Fishing rules: failures=" + s_failures);
        return s_failures == 0 ? 0 : 1;
    }
}

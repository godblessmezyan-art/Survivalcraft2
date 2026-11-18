using System;
using SCIEPort.Features.Fishing;

internal static class Program
{
    private static int s_checks;

    private static void Check(bool condition, string name)
    {
        s_checks++;
        if (!condition) throw new InvalidOperationException(name);
    }

    private static bool InRange(float value, float min, float max) => value >= min && value <= max;

    private static int Main()
    {
        var random = new Random(20261118);
        for (int i = 0; i < 500; i++)
        {
            Check(InRange(FishingRules.WaitDuration(random, false), 12f, 22f), "unbaited wait");
            Check(InRange(FishingRules.WaitDuration(random, true), 9f, 17f), "baited wait");
            Check(InRange(FishingRules.ReelDuration(random, false), 3f, 5f), "ordinary fight");
            Check(InRange(FishingRules.ReelDuration(random, true), 6f, 8f), "large fight");
            Check(!FishingRules.IsLarge(random, false), "unbaited only catches ordinary fish");
        }

        var local = new FishingCycle(10, 12, 4, false, false);
        Check(!local.Hook(10, true), "cannot hook right after cast");
        local.Advance(21.5);
        Check(local.Phase == FishingPhase.Approach && !local.Hook(22, true), "approach is not a bite");
        local.Advance(22);
        Check(local.Phase == FishingPhase.Bite && local.Hook(25, true), "local last instant of bite");
        Check(!local.Hook(25.5, true), "duplicate hook cannot restart a fight");
        local.SetHeld(26, false);
        local.Advance(28);
        Check(local.Progress == 0.25f, "release pauses progress");
        local.SetHeld(28, true);
        local.Advance(31);
        Check(local.Phase == FishingPhase.Landed && local.Reward == 1, "ordinary catch closes once");
        local.SetHeld(32, true);
        Check(!local.Hook(33, true) && local.Phase == FishingPhase.Landed, "terminal catch cannot reopen");

        var missed = new FishingCycle(0, 12, 4, false, false);
        missed.Advance(12);
        Check(!missed.Hook(15.01, true), "late input cannot resurrect the bite");
        missed.Advance(15.01);
        Check(missed.Phase == FishingPhase.Escaped, "inactive bite escapes");

        var remote = new FishingCycle(0, 12, 4, false, true);
        remote.Advance(12);
        Check(remote.Hook(15.5, true), "bounded relay allowance");
        for (int i = 0; i < 100; i++) remote.SetHeld(15.5, true);
        Check(remote.Progress == 0f, "network repeats do not advance same-time progress");
        remote.Advance(17);
        Check(Math.Abs(remote.Progress - FishingRules.HeldLease / 4f) < 0.001f, "remote lease stops on loss of input");
        remote.Advance(32);
        Check(remote.Phase == FishingPhase.Escaped, "remote pause expires");

        var independent = new FishingCycle(0, 12, 3, false, true);
        independent.Advance(12);
        Check(independent.Hook(12.25, false), "remote touch tap sets the hook without pulling");
        independent.Advance(12.5);
        Check(independent.Progress == 0f, "remote touch hold is a separate signal");
        for (int i = 0; i <= 17; i++) independent.SetHeld(12.5 + i * 0.2, true);
        Check(independent.Phase == FishingPhase.Landed && remote.Phase == FishingPhase.Escaped, "independent remote sessions");

        var trophy = new FishingCycle(0, 12, 6, true, false);
        trophy.Advance(12);
        Check(trophy.Hook(12, true), "large fish hooks");
        trophy.Advance(14);
        Check(trophy.Tense, "first large fish surge");
        trophy.SetHeld(14.1, false);
        trophy.Advance(14.7);
        Check(!trophy.Tense, "slack clears first surge");
        trophy.SetHeld(14.7, true);
        trophy.Advance(16.7);
        Check(trophy.Tense, "second large fish surge");
        trophy.SetHeld(16.8, false);
        trophy.Advance(17.4);
        Check(!trophy.Tense, "slack clears second surge");
        trophy.SetHeld(17.4, true);
        trophy.Advance(19.6);
        Check(trophy.Phase == FishingPhase.Landed && trophy.Reward == 2, "large fish lands with two portions");

        var snapped = new FishingCycle(0, 12, 6, true, false);
        snapped.Advance(12);
        Check(snapped.Hook(12, true), "hook for strain test");
        snapped.Advance(14);
        snapped.Advance(15.81);
        Check(snapped.Phase == FishingPhase.Escaped, "holding through a surge loses the fish");

        var slow = new FishingCycle(0, 12, 4, false, true);
        slow.Advance(13);
        Check(slow.Hook(16.5, false), "server hitch leaves a full displayed bite window");
        Check(FishingRules.PackReward(2, 1) == 0x12, "reward reports one stored and one dropped");
        Console.WriteLine($"Fishing timing, holding, reward and authority-cycle checks passed ({s_checks}).");
        return 0;
    }
}

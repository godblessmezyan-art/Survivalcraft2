using System;
using System.Diagnostics;
using SCIEPort.Features.PixelFx;

internal static class Program
{
    private static int checks;
    private static void Check(bool value, string name) { checks++; if (!value) throw new Exception(name); }
    private static void Reject(Action action) { bool threw = false; try { action(); } catch (ArgumentException) { threw = true; } Check(threw, "invalid clip rejected"); }
    private static int Main()
    {
        SpriteFxClip charge = StormhammerSpriteClips.ChargeUp, strike = StormhammerSpriteClips.LightningStrike;
        var player = new SpriteFxPlayer();
        player.Update(0, 2); Check(player.LastUpdateScanCount == 0, "idle update skips pool scan");
        player.Play(charge, 0, 0, 0, 1, 0, 2);
        Check(player.LastUpdateScanCount == 0, "same-tick spawn reuses update");
        player.Update(.1, 2); Check(player.LastUpdateScanCount == SpriteFxPlayer.Capacity, "new time expires active pool");
        player.Update(.1, 2); Check(player.LastUpdateScanCount == 0, "same-time update skips duplicate scan");
        player.Update(.1, 0); Check(player.LastUpdateScanCount == SpriteFxPlayer.Capacity, "same-time quality change still updates");
        player.Clear();
        Reject(() => new SpriteFxClip("bad", int.MaxValue, int.MaxValue, 24, 1024, 1024, 10, 96, .5f, .5f, SpriteFxFacing.Screen, 1));
        Reject(() => new SpriteFxClip("bad", 0, 1, 24, 96, 96, 2, 96, .5f, .5f, SpriteFxFacing.Screen, 1));
        Reject(() => new SpriteFxClip("bad", 0, 1, 24, 96, 96, 1, 96, float.NaN, .5f, SpriteFxFacing.Screen, 1));
        foreach (SpriteFxClip clip in new[] { charge, strike, StormhammerSpriteClips.Thunderclap })
        for (int i = -1; i <= clip.FrameCount; i++)
        {
            float a,b,c,d; clip.Uv(i, out a, out b, out c, out d);
            Check(a >= 0 && b >= 0 && c <= 1 && d <= 1 && a < c && b < d, "UV bounds and clamping");
            Check(Math.Abs((c - a) * 1024 - 95) < .001f, "half-texel inset");
        }
        Check(!player.Play(charge, 0, 0, 0, float.NaN, 0, 0), "NaN size");
        Check(!player.Play(charge, float.PositiveInfinity, 0, 0, 1, 0, 0), "invalid anchor");
        Check(!player.Play(charge, 0, 0, 0, 1, double.NaN, 0), "invalid clock");
        Check(!player.Play(charge, 0, 0, 0, 1, 0, 0, -1), "invalid life");
        Check(player.Play(charge, 0, 0, 0, 1, 0, 0, .42f), "charge spawn");
        var item = player.At(0);
        Check(SpriteFxPlayer.Frame(item, -.1, 24) == -1, "future frames hidden");
        Check(SpriteFxPlayer.Frame(item, 0, 24) == 0, "first frame");
        Check(SpriteFxPlayer.Frame(item, .21, 24) == 16, "charge remapped to existing window");
        Check(SpriteFxPlayer.Frame(item, .42, 24) == -1, "charge expires without extra release");
        Check(SpriteFxPlayer.Frame(item, double.NaN, 24) == -1, "invalid draw clock");
        player.Update(1, 0); Check(player.ActiveCount == 0, "pool expiry");
        for (int q = 0; q < 3; q++)
        {
            player.Clear(); var budget = SpriteFxBudget.ForQuality(q);
            Check(budget.ScreenArea == (q == 0 ? 1f : q == 1 ? 1.5f : 2f), "tier screen coverage budget");
            for (int i = 0; i < 1000; i++) player.Play(charge, i, 0, 0, 1, 0, q);
            Check(player.ActiveCount == budget.Instances && player.DroppedCount == 1000 - budget.Instances, "bounded saturation");
            player.Play(strike, 0, 0, 0, 1, 0, q);
            int high = 0;
            for (int i = 0; i < SpriteFxPlayer.Capacity; i++) if (player.At(i).Clip == strike) high++;
            Check(high == 1, "impact replaces lower priority");
            for (int i = 0; i < budget.Instances; i++) player.Play(strike, 0, 0, 0, 1, 0, q);
            Check(!player.Play(charge, 0, 0, 0, 1, 0, q), "charge cannot evict impact");
            Check(player.ActiveCount == budget.Instances, "rejection preserves count");
        }
        player.Update(.1, 0); Check(player.ActiveCount == 8, "live quality reduction");
        player.Update(-1, 0); Check(player.ActiveCount == 0, "clock rollback clears old world effects");
        Check(player.LastUpdateScanCount == 0, "rollback into empty pool does not scan");
        player.Play(strike, 0, 0, 0, 1, 0, 2);
        player.Update(10, 2); player.Update(11, 2);
        Check(player.ActiveCount == 0 && player.LastUpdateScanCount == 0, "expired idle pool fast path");
        player.Play(strike, 0, 0, 0, 1, 0, 2); player.Clear();
        for (int i = 0; i < SpriteFxPlayer.Capacity; i++) Check(player.At(i).Clip == null, "clear releases clip references");
        // Warm up JIT and all operations before measuring steady-state managed allocation.
        for (int i = 0; i < 10000; i++) Cycle(player, strike, i * .01);
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        var watch = Stopwatch.StartNew(); long watchBytes = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100000; i++) Cycle(player, strike, 100 + i * .01);
        long bytes = GC.GetAllocatedBytesForCurrentThread() - watchBytes; watch.Stop();
        Check(bytes == 0, "steady-state pool/frame/UV path allocates zero bytes");
        Console.WriteLine(checks + " PixelFX pool/timing/UV/quality checks passed; 100000 cycles " + watch.ElapsedMilliseconds + " ms, " + bytes + " bytes (host CLR, not Android); harness " + (watchBytes - allocated) + " bytes.");
        return 0;
    }
    private static void Cycle(SpriteFxPlayer player, SpriteFxClip clip, double now)
    {
        player.Play(clip, 0, 0, 0, 1, now, 2);
        player.Update(now, 2);
        for (int j = 0; j < SpriteFxPlayer.Capacity; j++)
        {
            SpriteFxInstance item = player.At(j); int frame = SpriteFxPlayer.Frame(item, now, 24);
            if (frame >= 0) { float a,b,c,d; item.Clip.Uv(frame, out a, out b, out c, out d); }
        }
    }
}

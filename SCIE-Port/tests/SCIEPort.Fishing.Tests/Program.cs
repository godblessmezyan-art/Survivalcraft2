using System;
using SCIEPort.Features.Fishing;

internal static class Program
{
    private static int s_failures;
    private static int s_checks;

    private static void Check(bool condition, string name)
    {
        s_checks++;
        if (condition) return;
        s_failures++;
        Console.WriteLine("FAIL " + name);
    }

    private static int Main()
    {
        var random = new Random(20261114);
        for (int i = 0; i < 500; i++)
        {
            float wait = FishingRules.WaitDuration((float)random.NextDouble());
            Check(wait >= FishingRules.MinWaitDuration && wait <= FishingRules.MaxWaitDuration, "wait duration range");
        }

        Check(FishingRules.Reel(1.99f, 2f, 0.9f) == FishingRules.ReelResult.Early, "reel before bite");
        Check(FishingRules.WaitDuration(-1f)==2f && FishingRules.WaitDuration(2f)==5f, "wait input clamped");
        Check(FishingRules.WaitDuration(float.NaN)==2f, "invalid wait input safe");
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
        Check(FishingRules.IsWaterDepthValid(1), "one-cell-deep water is fishable");
        Check(FishingRules.IsWaterDepthValid(2), "surface water");
        Check(FishingRules.IsWaterDepthValid(8), "deep water");

        Check(!FishingRules.ShouldEndSession(20f), "session at limit");
        Check(FishingRules.ShouldEndSession(20.01f), "session beyond limit");

        var limiter = new FishingRateLimiter();
        Check(limiter.TryAccept(100d), "first catch");
        Check(!limiter.TryAccept(100.5d), "spam rejected");
        Check(limiter.TryAccept(100.81d), "cooldown elapsed");
        for (int i = 0; i < 28; i++) limiter.TryAccept(102d + i * 1d);
        Check(!limiter.TryAccept(140d), "minute cap reached");
        Check(limiter.TryAccept(161d), "minute window reset");

        limiter.Reset();
        Check(limiter.TryAccept(0d), "reset allows catch");

        Check(!limiter.TryAccept(double.NaN), "invalid limiter clock rejected");
        Check(FishingRules.NewerSequence(8, 7), "new cast accepted");
        Check(!FishingRules.NewerSequence(7, 7), "replayed cast rejected");
        Check(!FishingRules.NewerSequence(6, 7), "older cast rejected");
        Check(FishingRules.NewerSequence(1, uint.MaxValue), "sequence wraps safely");
        Check(!FishingRules.NewerSequence(0, uint.MaxValue), "zero reserved");
        var round = new FishingRound(7, 100, 2);
        Check(!round.TryReel(6, 102, 0), "wrong round rejected");
        Check(!round.Consumed, "wrong sequence cannot consume current round");
        Check(round.TryReel(7, 102, 0), "host round succeeds at bite");
        Check(!round.TryReel(7, 102.1, 0), "host round grants once");
        round = new FishingRound(8, 100, 2);
        Check(!round.TryReel(8, 101.99, 1), "host rejects early reel even with grace");
        Check(!round.TryReel(8, 102.2, 1), "failed reel also consumed");
        round = new FishingRound(9, 100, 2);
        Check(round.TryReel(9, 104, 1), "bounded network lateness accepted");
        Check(new FishingRound(10, 100, 2).Expired(104.36, 1), "network grace has a limit");
        Check(!new FishingRound(10, 100, 2).TryReel(10, 105, 1), "very late host reel fails");
        Check(!new FishingRound(11, 100, 2).TryReel(11, double.NaN, 0), "invalid reel clock rejected");
        CastTests(random);
        Console.WriteLine("Fishing checks=" + s_checks + ", failures=" + s_failures);
        return s_failures == 0 ? 0 : 1;
    }

    private static void CastTests(Random random)
    {
        FishingLanding hit;
        Func<int,int,int,FishingCell> lake = (x,y,z) => y < 10 ? new FishingCell(2) : y == 10 ? new FishingCell(1,.875f) : new FishingCell(0);
        Check(FishingCastResolver.Resolve(new FishingPoint(.5f,12,.5f),new FishingPoint(0,-1,0),lake,out hit)==FishingCastFailure.None, "shallow lake casts");
        Check(Math.Abs(hit.Position.Y-10.875f)<.00001f, "actual fluid height used");
        Check(FishingCastResolver.Resolve(new FishingPoint(.5f,12,.5f),new FishingPoint(0,0,1),lake,out hit)==FishingCastFailure.None, "level touch aim assisted downward");
        Check(hit.Position.Z>.5f && hit.Position.Z<12.5f, "assisted landing stays ahead within range");
        float assistedZ=hit.Position.Z;
        Check(FishingCastResolver.Resolve(new FishingPoint(.5f,12,.5f),new FishingPoint(0,0,100),lake,out hit)==FishingCastFailure.None && Math.Abs(hit.Position.Z-assistedZ)<.00001f, "non-unit touch ray keeps the same assistance");
        Check(FishingCastResolver.Trace(new FishingPoint(0,12,0),new FishingPoint(-1,-1,-1),12,lake,out hit)==FishingCastFailure.None, "negative ray on exact voxel corner");
        Func<int,int,int,FishingCell> wall = (x,y,z) => z==2 ? new FishingCell(2) : lake(x,y,z);
        Check(FishingCastResolver.Resolve(new FishingPoint(.5f,12,.5f),new FishingPoint(0,0,1),wall,out hit)==FishingCastFailure.Blocked, "assistance never bypasses first wall");
        Check(FishingCastResolver.Trace(new FishingPoint(.5f,10.5f,.5f),new FishingPoint(0,-1,0),12,lake,out hit)==FishingCastFailure.Blocked, "submerged origin rejected");
        Check(FishingCastResolver.Resolve(new FishingPoint(.5f,12,.5f),new FishingPoint(0,1,0),lake,out hit)==FishingCastFailure.NoWater, "sky aim not redirected into lake");
        Check(FishingCastResolver.Trace(new FishingPoint(.5f,25,.5f),new FishingPoint(0,-1,0),12,lake,out hit)==FishingCastFailure.NoWater, "distant water rejected");
        Check(FishingCastResolver.Resolve(new FishingPoint(float.NaN,12,0),new FishingPoint(0,-1,0),lake,out hit)==FishingCastFailure.Invalid, "invalid origin rejected");
        Check(FishingCastResolver.Resolve(new FishingPoint(0,12,0),new FishingPoint(),lake,out hit)==FishingCastFailure.Invalid, "zero ray rejected");
        Check(FishingCastResolver.Resolve(new FishingPoint(0,12,0),new FishingPoint(0,-1,0),(x,y,z)=>new FishingCell(3),out hit)==FishingCastFailure.Unavailable, "unloaded terrain distinct from no water");
        int reads=0;
        FishingCastResolver.Resolve(new FishingPoint(-.2f,12,-.2f),new FishingPoint(.2f,0,1),(x,y,z)=>{reads++;return new FishingCell(0);},out hit);
        Check(reads<=FishingCastResolver.MaxVisitedCells*3, "all aim attempts bounded");
        // Analytic flat surfaces, including negative coordinates, thin water and exact grid seams.
        for(int i=0;i<1000;i++)
        {
            float height=.125f+(i%7)*.125f;
            float ox=(float)random.NextDouble()*20-10, oz=(float)random.NextDouble()*20-10;
            float dx=(float)random.NextDouble()-.5f, dz=(float)random.NextDouble()-.5f;
            var result=FishingCastResolver.Trace(new FishingPoint(ox,12,oz),new FishingPoint(dx,-1,dz),12,
                (x,y,z)=>y==10?new FishingCell(1,height):y<10?new FishingCell(2):new FishingCell(0),out hit);
            Check(result==FishingCastFailure.None,"analytic lake hit "+i);
            Check(Math.Abs(hit.Position.Y-(10+height))<.00001,"thin/deep height "+i);
            Check(Math.Abs(hit.Position.X-(ox+dx*(2-height)))<.0001 && Math.Abs(hit.Position.Z-(oz+dz*(2-height)))<.0001,"surface intersection coordinates "+i);
        }
    }
}

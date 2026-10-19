using System;
using SCIEPort.Features.Tanks;

internal static class Program
{
    private static void Main()
    {
        AssertNear(TankRules.Acceleration(0f, 1f), TankRules.ForwardSpeed, "forward acceleration");
        AssertNear(TankRules.Acceleration(0f, -1f), -7f, "reverse acceleration");
        AssertNear(TankRules.Acceleration(5f, 0f), -2.5f, "forward coasting");
        AssertNear(TankRules.Acceleration(-2f, 0f), 2.5f, "reverse coasting");
        AssertNear(TankRules.Acceleration(1f, -1f), -14f, "opposite input braking");
        AssertNear(TankRules.Acceleration(TankRules.ForwardSpeed, 1f), 0f, "forward speed cap");
        AssertNear(TankRules.Acceleration(-TankRules.ReverseSpeed, -1f), 0f, "reverse speed cap");
        if (TankRules.StepHeight <= 0f || TankRules.StepHeight > .5f) throw new InvalidOperationException("Tank step limit must be conservative.");
        Console.WriteLine("Tank drive rule tests passed.");
    }

    private static void AssertNear(float actual, float expected, string name)
    {
        if (Math.Abs(actual - expected) > .0001f) throw new InvalidOperationException(name + ": expected " + expected + ", got " + actual);
    }
}

using System;
using SCIEPort.Features.Tanks;

internal static class Program
{
    private static void Main()
    {
        AssertNear(TankRules.GetLongitudinalAcceleration(0f, 1f), TankRules.DriveAcceleration, "forward acceleration");
        AssertNear(TankRules.GetLongitudinalAcceleration(0f, -1f), -TankRules.DriveAcceleration, "reverse acceleration");
        AssertNear(TankRules.GetLongitudinalAcceleration(5f, 0f), -TankRules.CoastDeceleration, "forward coasting");
        AssertNear(TankRules.GetLongitudinalAcceleration(-2f, 0f), TankRules.CoastDeceleration, "reverse coasting");
        AssertNear(TankRules.GetLongitudinalAcceleration(1f, -1f), -TankRules.BrakeAcceleration, "opposite input braking");
        AssertNear(TankRules.GetLongitudinalAcceleration(TankRules.ForwardSpeed, 1f), 0f, "forward speed cap");
        AssertNear(TankRules.GetLongitudinalAcceleration(-TankRules.ReverseSpeed, -1f), 0f, "reverse speed cap");
        AssertNear(TankRules.IntegrateForwardSpeed(.1f, 0f, 1f), 0f, "coast cannot cross zero");
        AssertNear(TankRules.IntegrateForwardSpeed(6.9f, 1f, 1f), TankRules.ForwardSpeed, "integrated forward cap");
        AssertNear(TankRules.IntegrateForwardSpeed(-2.9f, -1f, 1f), -TankRules.ReverseSpeed, "integrated reverse cap");
        AssertNear(TankRules.GetSteeringDirection(-1f), -1f, "reverse steering");
        AssertNear(TankRules.GetSteeringDirection(0f), 1f, "pivot steering");
        AssertNear(TankRules.GetSteeringScale(TankRules.ForwardSpeed), .6f, "high-speed steering scale");
        Assert(TankRules.CanApplyDrive(true, false, 0f), "terrain contact permits drive");
        Assert(!TankRules.CanApplyDrive(false, false, 0f), "airborne propulsion blocked");
        Assert(!TankRules.CanApplyDrive(true, true, 0f), "body-supported propulsion blocked");
        Assert(!TankRules.CanApplyDrive(true, false, .25f), "water propulsion blocked");
        if (TankRules.StepHeight <= 0f || TankRules.StepHeight > .5f) throw new InvalidOperationException("Tank step limit must be conservative.");
        Console.WriteLine("Tank drive rule tests passed.");
    }

    private static void Assert(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException(name);
    }

    private static void AssertNear(float actual, float expected, string name)
    {
        if (Math.Abs(actual - expected) > .0001f) throw new InvalidOperationException(name + ": expected " + expected + ", got " + actual);
    }
}

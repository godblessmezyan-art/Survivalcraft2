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
        AssertNear(TankRules.StepHeight, 1.05f, "one-cell smooth rise clearance");
        Assert(TankRules.CanFire(0f, true, 0f), "ready cannon can fire");
        Assert(!TankRules.CanFire(.1f, true, 0f), "cannon cooldown enforced");
        Assert(!TankRules.CanFire(0f, false, 0f), "cannon requires hit ray");
        Assert(!TankRules.CanFire(0f, true, .8f), "submerged cannon blocked");
        Assert(TankRules.ShellSpeed > TankRules.ForwardSpeed, "shell faster than tank");
        Assert(TankRules.ShellExplosionPressure > 0f, "shell explosion enabled");
        Assert(!TankRules.IsShellArmed(TankRules.ShellArmingDistance * TankRules.ShellArmingDistance - .01f), "near-body shell remains unarmed");
        Assert(TankRules.IsShellArmed(TankRules.ShellArmingDistance * TankRules.ShellArmingDistance), "shell arms at safe distance");
        Assert(TankRules.MuzzleClearance >= .5f, "muzzle clears inflated body raycast");
        AssertNear(TankRules.GetBoxExitDistance(0f, 0f, 0f, 0f, 0f, -1f, 1.8f, 1.05f, 2.95f), 2.95f, "forward muzzle exits vehicle box");
        AssertNear(TankRules.GetBoxExitDistance(0f, .516f, 0f, 1f, 0f, 0f, 1.8f, 1.05f, 2.95f), 1.8f, "side muzzle exits vehicle box");
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

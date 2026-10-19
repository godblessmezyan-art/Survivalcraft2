using System;
using SCIEPort.Features.LandVehicles;

internal static class Program
{
    private static int s_assertions;

    private static int Main()
    {
        Assert(AtvDriveRules.CanDrive(true, 0f), "grounded ATV drives");
        Assert(!AtvDriveRules.CanDrive(false, 0f), "airborne ATV does not drive");
        Assert(!AtvDriveRules.CanDrive(true, 0.25f), "submerged ATV does not drive");
        AssertEqual(10f, AtvDriveRules.GetLongitudinalAcceleration(0f, 1f), "forward acceleration");
        AssertEqual(-10f, AtvDriveRules.GetLongitudinalAcceleration(0f, -1f), "reverse acceleration");
        AssertEqual(-18f, AtvDriveRules.GetLongitudinalAcceleration(4f, -1f), "opposite input brakes forward movement");
        AssertEqual(18f, AtvDriveRules.GetLongitudinalAcceleration(-2f, 1f), "opposite input brakes reverse movement");
        AssertEqual(0f, AtvDriveRules.GetLongitudinalAcceleration(11f, 1f), "forward speed cap");
        AssertEqual(0f, AtvDriveRules.GetLongitudinalAcceleration(-4f, -1f), "reverse speed cap");
        AssertEqual(-3f, AtvDriveRules.GetLongitudinalAcceleration(2f, 0f), "coast braking");
        AssertEqual(-1f, AtvDriveRules.GetSteeringDirection(-1f), "reverse steering");
        AssertEqual(1f, AtvDriveRules.GetSteeringDirection(1f), "forward steering");
        AssertEqual(0.35f, AtvDriveRules.MaximumSmoothRiseHeight, "step limit");
        Console.WriteLine("ATV drive rule tests passed: " + s_assertions);
        return 0;
    }

    private static void Assert(bool condition, string message)
    {
        s_assertions++;
        if (!condition)
        {
            throw new InvalidOperationException("Assertion failed: " + message);
        }
    }

    private static void AssertEqual(float expected, float actual, string message)
    {
        Assert(Math.Abs(expected - actual) < 0.0001f, message + ": expected " + expected + ", actual " + actual);
    }
}

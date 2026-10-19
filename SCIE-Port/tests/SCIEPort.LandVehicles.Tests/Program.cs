using System;
using SCIEPort.Features.LandVehicles;

internal static class Program
{
    private static int s_assertions;

    private static int Main()
    {
        Assert(AtvDriveRules.CanApplyDrive(true, false, 0f), "terrain-grounded ATV drives");
        Assert(!AtvDriveRules.CanApplyDrive(false, false, 0f), "airborne ATV does not accelerate");
        Assert(!AtvDriveRules.CanApplyDrive(false, true, 0f), "ATV does not propel through a supporting body");
        Assert(!AtvDriveRules.CanApplyDrive(true, false, 0.25f), "submerged ATV does not drive");
        Assert(AtvDriveRules.IsAirborneGrace(false, 0.2f, 0f), "short airtime preserves transition state");
        Assert(!AtvDriveRules.IsAirborneGrace(false, 0.5f, 0f), "long airtime leaves grace state");
        AssertEqual(10f, AtvDriveRules.GetLongitudinalAcceleration(0f, 1f), "forward acceleration");
        AssertEqual(-10f, AtvDriveRules.GetLongitudinalAcceleration(0f, -1f), "reverse acceleration");
        AssertEqual(-16f, AtvDriveRules.GetLongitudinalAcceleration(4f, -1f), "opposite input brakes forward movement");
        AssertEqual(16f, AtvDriveRules.GetLongitudinalAcceleration(-2f, 1f), "opposite input brakes reverse movement");
        AssertEqual(0f, AtvDriveRules.GetLongitudinalAcceleration(11f, 1f), "forward speed cap");
        AssertEqual(0f, AtvDriveRules.GetLongitudinalAcceleration(-4f, -1f), "reverse speed cap");
        AssertEqual(-2.2f, AtvDriveRules.GetLongitudinalAcceleration(2f, 0f), "coast braking");
        AssertEqual(0f, AtvDriveRules.IntegrateForwardSpeed(0.1f, 0f, 0.1f), "coasting cannot overshoot zero");
        AssertEqual(11f, AtvDriveRules.IntegrateForwardSpeed(10.8f, 1f, 0.1f), "integrator keeps forward cap");
        AssertEqual(-4f, AtvDriveRules.IntegrateForwardSpeed(-3.8f, -1f, 0.1f), "integrator keeps reverse cap");
        AssertEqual(-1f, AtvDriveRules.GetSteeringDirection(-1f), "reverse steering");
        AssertEqual(1f, AtvDriveRules.GetSteeringDirection(1f), "forward steering");
        AssertEqual(0.35f, AtvDriveRules.GetSteeringScale(0f), "standstill steering is reduced");
        AssertEqual(1f, AtvDriveRules.GetSteeringScale(4f), "normal-speed steering remains responsive");
        AssertEqual(0.55f, AtvDriveRules.GetSteeringScale(11f), "high-speed steering is damped");
        AssertEqual(11f, AtvDriveRules.MaximumForwardSpeed, "v0.2 does not hide drag issues by raising top speed");
        AssertEqual(1.05f, AtvDriveRules.MaximumSmoothRiseHeight, "one-cell native smooth-rise clearance");
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

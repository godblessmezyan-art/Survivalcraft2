using System;
using SCIEPort.Features.OffroadVehicles;
using SCIEPort.Features.Vehicles;

internal static class Program
{
    private static int s_count;

    private static int Main()
    {
        Assert(OffroadCarRules.ContainsModelBounds(-1.15f, 1.15f, 0.08f, 1.48f, -1.78f, 1.68f), "model fits body");
        Assert(OffroadCarRules.SeatSeparation >= 0.85f, "side-by-side seat clearance");
        Assert(Math.Abs(OffroadCarRules.DriverX) < OffroadCarRules.BodyWidth / 2f, "driver inside body");
        Assert(Math.Abs(OffroadCarRules.PassengerX) < OffroadCarRules.BodyWidth / 2f, "passenger inside body");
        Assert(OffroadCarRules.DismountX > OffroadCarRules.BodyWidth / 2f, "dismount outside body");
        Assert(OffroadCarRules.CanApplyDrive(true, false, 0f), "terrain drive allowed");
        Assert(!OffroadCarRules.CanApplyDrive(false, false, 0f), "air drive rejected");
        Assert(!OffroadCarRules.CanApplyDrive(true, true, 0f), "entity climbing rejected");
        Assert(!OffroadCarRules.CanApplyDrive(true, false, 0.3f), "water drive rejected");
        Equal(OffroadCarRules.MaximumForwardSpeed, OffroadCarRules.IntegrateForwardSpeed(9.4f, 1f, 1f), "forward cap");
        Equal(-OffroadCarRules.MaximumReverseSpeed, OffroadCarRules.IntegrateForwardSpeed(-3.4f, -1f, 1f), "reverse cap");
        Assert(OffroadCarRules.IntegrateForwardSpeed(4f, -1f, 0.1f) < 4f, "opposite input brakes");
        Assert(OffroadCarRules.IntegrateForwardSpeed(2f, 0f, 0.1f) < 2f, "coasting slows");
        Equal(-1f, OffroadCarRules.GetSteeringDirection(-2f), "reverse steering");
        Assert(OffroadCarRules.GetSteeringScale(9.5f) < OffroadCarRules.GetSteeringScale(4f), "high speed steering reduced");
        Assert(TwoSeatRules.SelectSeat(false, false) == VehicleSeatRole.Driver, "first rider driver");
        Assert(TwoSeatRules.SelectSeat(true, false) == VehicleSeatRole.Passenger, "second rider passenger");
        Assert(TwoSeatRules.SelectSeat(true, true) == VehicleSeatRole.None, "third rider rejected");
        Assert(TwoSeatRules.CanControlVehicle(VehicleSeatRole.Driver), "driver controls");
        Assert(!TwoSeatRules.CanControlVehicle(VehicleSeatRole.Passenger), "passenger cannot control");
        Console.WriteLine("Off-road car rule tests passed: " + s_count);
        return 0;
    }

    private static void Assert(bool condition, string message)
    {
        s_count++;
        if (!condition) throw new InvalidOperationException("Assertion failed: " + message);
    }

    private static void Equal(float expected, float actual, string message)
    {
        Assert(Math.Abs(expected - actual) < 0.0001f, message);
    }
}

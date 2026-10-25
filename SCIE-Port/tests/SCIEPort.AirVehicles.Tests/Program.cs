using System;
using SCIEPort.Features.AirVehicles;
using SCIEPort.Features.Vehicles;

internal static class Program
{
    private static int s_count;

    private static int Main()
    {
        Assert(HelicopterFlightRules.ContainsCabinBounds(-1.05f, 1.05f, 0.05f, 1.85f, -2.35f, 2.55f), "cabin fits body");
        Assert(Math.Abs(HelicopterFlightRules.DriverX) < HelicopterFlightRules.BodyWidth / 2f, "driver inside body");
        Assert(Math.Abs(HelicopterFlightRules.PassengerX) < HelicopterFlightRules.BodyWidth / 2f, "passenger inside body");
        Assert(HelicopterFlightRules.DismountX > HelicopterFlightRules.BodyWidth / 2f, "dismount outside body");
        Equal(0.4f, HelicopterFlightRules.UpdateRotorSpeed(0f, true, 2.5f, 3.5f, 1f), "rotor spools up");
        Equal(1f, HelicopterFlightRules.UpdateRotorSpeed(0.9f, true, 2.5f, 3.5f, 1f), "rotor caps at one");
        Assert(HelicopterFlightRules.UpdateRotorSpeed(1f, false, 2.5f, 2f, 1f) < 1f, "rotor spools down");
        Equal(4.5f, HelicopterFlightRules.GetVerticalTarget(true, false, true, 4.5f, 3.5f, 1.8f), "jump ascends");
        Equal(-3.5f, HelicopterFlightRules.GetVerticalTarget(false, true, true, 4.5f, 3.5f, 1.8f), "sneak descends");
        Equal(0f, HelicopterFlightRules.GetVerticalTarget(false, false, true, 4.5f, 3.5f, 1.8f), "neutral hovers");
        Equal(-1.8f, HelicopterFlightRules.GetVerticalTarget(false, false, false, 4.5f, 3.5f, 1.8f), "no pilot auto lands");
        Assert(HelicopterFlightRules.IntegrateVerticalSpeed(0f, 0f, 0f, 9.8f, 2.8f, 4.5f, 3.5f, 0.1f) < 0f, "unpowered gravity");
        Equal(0f, HelicopterFlightRules.IntegrateVerticalSpeed(0f, 0f, 1f, 9.8f, 2.8f, 4.5f, 3.5f, 0.1f), "full rotor hover compensates gravity");
        Assert(HelicopterFlightRules.IntegrateVerticalSpeed(0f, 4.5f, 1f, 9.8f, 2.8f, 4.5f, 3.5f, 0.1f) > 0f, "powered ascent accelerates");
        Equal(4.5f, HelicopterFlightRules.IntegrateVerticalSpeed(4.5f, 4.5f, 1f, 9.8f, 2.8f, 4.5f, 3.5f, 1f), "ascent capped");
        Equal(-3.5f, HelicopterFlightRules.IntegrateVerticalSpeed(-3.5f, -3.5f, 0f, 9.8f, 2.8f, 4.5f, 3.5f, 1f), "descent capped");
        Assert(HelicopterFlightRules.IntegrateForwardSpeed(0f, 1f, 5.5f, 8f, 1.5f, 12f, 5f, 0.1f) > 0f, "forward acceleration");
        Assert(HelicopterFlightRules.IntegrateForwardSpeed(3f, 0f, 5.5f, 8f, 1.5f, 12f, 5f, 0.1f) < 3f, "horizontal hover drag");
        Assert(HelicopterFlightRules.IntegrateForwardSpeed(3f, -1f, 5.5f, 8f, 1.5f, 12f, 5f, 0.1f) < 3f, "opposite input brakes");
        Equal(12f, HelicopterFlightRules.IntegrateForwardSpeed(11.9f, 1f, 5.5f, 8f, 1.5f, 12f, 5f, 1f), "forward speed cap");
        Equal(-5f, HelicopterFlightRules.IntegrateForwardSpeed(-4.9f, -1f, 5.5f, 8f, 1.5f, 12f, 5f, 1f), "reverse speed cap");
        Assert(HelicopterFlightRules.IsAtAltitudeLimit(240f, 240f), "height limit inclusive");
        Assert(!HelicopterFlightRules.IsAtAltitudeLimit(239.9f, 240f), "below height limit");
        Assert(TwoSeatRules.SelectSeat(false, false) == VehicleSeatRole.Driver, "first rider driver");
        Assert(TwoSeatRules.SelectSeat(true, false) == VehicleSeatRole.Passenger, "second rider passenger");
        Assert(TwoSeatRules.SelectSeat(true, true) == VehicleSeatRole.None, "third rider rejected");
        Assert(TwoSeatRules.CanControlVehicle(VehicleSeatRole.Driver), "driver controls");
        Assert(!TwoSeatRules.CanControlVehicle(VehicleSeatRole.Passenger), "passenger cannot control");
        Console.WriteLine("Helicopter rule tests passed: " + s_count);
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

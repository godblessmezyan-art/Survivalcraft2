using System;
using SCIEPort.Features.Vehicles;
using SCIEPort.Features.Watercraft;

internal static class Program
{
    private static int s_assertions;

    private static int Main()
    {
        Assert(SteamBoatRules.ContainsModelBounds(-1.057975f, 1.057976f, 0f, 1.693586f, -2.352572f, 2.742779f), "lifted model fits body");
        Assert(SteamBoatRules.SeatSeparation >= 0.8f, "side-by-side players have clearance");
        Assert(Math.Abs(SteamBoatRules.DriverX) < SteamBoatRules.BodyWidth / 2f, "driver is inside hull");
        Assert(Math.Abs(SteamBoatRules.PassengerX) < SteamBoatRules.BodyWidth / 2f, "passenger is inside hull");
        Assert(SteamBoatRules.DismountX > SteamBoatRules.BodyWidth / 2f, "dismount is outside hull");
        Assert(SteamBoatRules.SeatY < SteamBoatRules.BodyHeight, "seat is below body top");
        Assert(SteamBoatRules.IsWaterborne(0.5f, false, false), "floating boat can drive");
        Assert(!SteamBoatRules.IsWaterborne(0f, false, false), "dry boat cannot drive");
        Assert(!SteamBoatRules.IsWaterborne(0.96f, false, false), "submerged boat cannot drive");
        Assert(!SteamBoatRules.IsWaterborne(0.5f, true, false), "grounded boat cannot drive");
        Assert(!SteamBoatRules.IsWaterborne(0.5f, false, true), "body-supported boat cannot drive");
        AssertEqual(11f, SteamBoatRules.BaseAcceleration + SteamBoatRules.ExtraAcceleration, "v0.3 propulsion is preserved");
        AssertEqual(15f, SteamBoatRules.MaximumHorizontalSpeed, "speed remains bounded");
        Assert(TwoSeatRules.SelectSeat(false, false) == VehicleSeatRole.Driver, "first rider is driver");
        Assert(TwoSeatRules.SelectSeat(true, false) == VehicleSeatRole.Passenger, "second rider is passenger");
        Assert(TwoSeatRules.SelectSeat(true, true) == VehicleSeatRole.None, "third rider is rejected");
        Console.WriteLine("Watercraft rule tests passed: " + s_assertions);
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

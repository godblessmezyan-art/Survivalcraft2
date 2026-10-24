using System;
using SCIEPort.Features.Vehicles;

internal static class Program
{
    private static int s_assertions;

    private static int Main()
    {
        // Unified activation code fixed to 123456.
        AssertEqualText("123456", VehicleActivationRules.ActivationCode, "activation code is the fixed unified value");

        Assert(VehicleActivationRules.IsValidCode("123456"), "exact code is valid");
        Assert(VehicleActivationRules.IsValidCode("  123456\t"), "surrounding whitespace is trimmed before compare");
        Assert(!VehicleActivationRules.IsValidCode("12345"), "short code is invalid");
        Assert(!VehicleActivationRules.IsValidCode("1234567"), "long code is invalid");
        Assert(!VehicleActivationRules.IsValidCode("abcdef"), "non-numeric code is invalid");
        Assert(!VehicleActivationRules.IsValidCode("654321"), "reversed digits are invalid");
        Assert(!VehicleActivationRules.IsValidCode("1234 56"), "internal whitespace is invalid");
        Assert(!VehicleActivationRules.IsValidCode(null), "null code is invalid");
        Assert(!VehicleActivationRules.IsValidCode(string.Empty), "empty code is invalid");

        Assert(VehicleActivationRules.ShouldPrompt(0.0, -1.0), "first prompt always allowed before any prior attempt");
        Assert(VehicleActivationRules.ShouldPrompt(100.0, 93.0), "prompt allowed after more than the interval elapses");
        Assert(VehicleActivationRules.ShouldPrompt(100.0, 94.0), "prompt allowed exactly at the interval boundary");
        Assert(!VehicleActivationRules.ShouldPrompt(100.0, 95.0), "prompt throttled within the interval");
        Assert(!VehicleActivationRules.ShouldPrompt(95.0, 95.0), "prompt throttled with zero elapsed time");
        AssertEqual(6.0, VehicleActivationRules.PromptInterval, "throttle window is six seconds");

        Assert(VehicleActivationRules.ShouldGrantActivation(true), "host grants when the carried code is valid");
        Assert(!VehicleActivationRules.ShouldGrantActivation(false), "host refuses when the carried code is invalid");

        Assert(VehicleActivationRules.CanOperate(true), "authorized session may operate a vehicle");
        Assert(!VehicleActivationRules.CanOperate(false), "unauthorized session may not operate a vehicle");

        Assert(VehicleActivationRules.IsUsableSessionKey(Guid.Empty), "the all-zeros GUID is the local session key");
        Assert(VehicleActivationRules.IsUsableSessionKey(new Guid("1b2e4a6c-0d3f-4b5a-9c8d-7e6f5a4b3c2d")),
            "a real network player GUID is a usable session key");

        Console.WriteLine("Vehicle activation rule tests passed: " + s_assertions);
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

    private static void AssertEqual(double expected, double actual, string message)
    {
        Assert(Math.Abs(expected - actual) < 0.0001, message + ": expected " + expected + ", actual " + actual);
    }

    private static void AssertEqualText(string expected, string actual, string message)
    {
        Assert(string.Equals(expected, actual, StringComparison.Ordinal), message + ": expected '" + expected + "', actual '" + actual + "'");
    }
}

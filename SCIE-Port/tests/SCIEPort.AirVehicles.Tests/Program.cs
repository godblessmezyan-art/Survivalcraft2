using System;
using SCIEPort.Features.AirVehicles;
using SCIEPort.Features.AirVehicles.Hud;
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
        Assert(HelicopterFlightRules.IsViewClimb(0.9f, HelicopterFlightRules.VerticalPitchDeadzone), "looking up commands climb");
        Assert(HelicopterFlightRules.IsViewDive(-0.9f, HelicopterFlightRules.VerticalPitchDeadzone), "looking down commands dive");
        Assert(!HelicopterFlightRules.IsViewClimb(0.1f, HelicopterFlightRules.VerticalPitchDeadzone) && !HelicopterFlightRules.IsViewDive(-0.1f, HelicopterFlightRules.VerticalPitchDeadzone), "level view stays in hover deadzone");
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
        Assert(HelicopterFlightRules.GetRotorState(false, 0f) == HelicopterRotorState.Stopped, "rotor stopped state");
        Assert(HelicopterFlightRules.GetRotorState(true, 0.5f) == HelicopterRotorState.Starting, "rotor starting state");
        Assert(HelicopterFlightRules.GetRotorState(true, 1f) == HelicopterRotorState.Running, "rotor running state");
        Assert(HelicopterFlightRules.GetRotorState(false, 0.5f) == HelicopterRotorState.Stopping, "rotor stopping state");
        Equal(0.5f, HelicopterFlightRules.WrapAngle(6.2831855f * 3f + 0.5f), "multi-turn angle wraps");
        Equal(6.1831856f, HelicopterFlightRules.WrapAngle(-0.1f), "negative angle wraps");
        Assert(HelicopterFlightRules.ShortestAngleDelta(6.2f, 0.1f) > 0f, "yaw delta crosses wrap forward");
        Assert(HelicopterFlightRules.GetVisualPitch(6f, 12f, 5f, 0.12f) < 0f, "forward flight pitches nose down");
        Assert(HelicopterFlightRules.GetVisualPitch(-2.5f, 12f, 5f, 0.12f) > 0f, "reverse flight pitches nose up");
        Assert(Math.Abs(HelicopterFlightRules.GetVisualRoll(20f, 4f, 3f, 1.2f, 0.16f)) <= 0.1601f, "visual roll stays bounded");
        Assert(HelicopterFlightRules.SmoothVisualAngle(0f, 1f, 4f, 0.1f) > 0f && HelicopterFlightRules.SmoothVisualAngle(0f, 1f, 4f, 0.1f) < 1f, "visual damping is bounded");
        Equal(0f, HelicopterFlightRules.GetRotorBlurBlend(0.6f, 0.75f), "slow rotor uses solid blades");
        Equal(1f, HelicopterFlightRules.GetRotorBlurBlend(0.9f, 0.75f), "fast rotor uses blur geometry");

        // Vertical HUD intent arbitration.
        Assert(HelicopterControlSurface.ResolveIntent(true, false, false, false, false, false) == HelicopterVerticalIntent.Climb, "climb button climbs");
        Assert(HelicopterControlSurface.ResolveIntent(false, true, false, false, false, false) == HelicopterVerticalIntent.Dive, "dive button dives");
        Assert(HelicopterControlSurface.ResolveIntent(false, false, false, false, false, false) == HelicopterVerticalIntent.Hover, "no input hovers");
        Assert(HelicopterControlSurface.ResolveIntent(true, true, false, false, false, false) == HelicopterVerticalIntent.Hover, "both buttons cancel to hover");
        Assert(HelicopterControlSurface.ResolveIntent(false, false, true, false, false, false) == HelicopterVerticalIntent.Climb, "view climb fallback");
        Assert(HelicopterControlSurface.ResolveIntent(false, false, false, true, false, false) == HelicopterVerticalIntent.Dive, "view dive fallback");
        Assert(HelicopterControlSurface.ResolveIntent(true, false, false, true, false, false) == HelicopterVerticalIntent.Climb, "climb button overrides view dive");
        Assert(HelicopterControlSurface.ResolveIntent(false, false, true, false, false, true) == HelicopterVerticalIntent.Dive, "sneak key overrides view climb");
        Assert(HelicopterControlSurface.ResolveIntent(false, false, true, true, false, false) == HelicopterVerticalIntent.Hover, "conflicting view cancels");
        Assert(HelicopterControlSurface.ResolveIntent(false, false, false, false, true, false) == HelicopterVerticalIntent.Climb, "jump key climbs");
        Assert(HelicopterControlSurface.ResolveIntent(false, false, false, false, false, true) == HelicopterVerticalIntent.Dive, "sneak key dives");
        bool hudAscend;
        bool hudDescend;
        HelicopterControlSurface.ToVerticalFlags(HelicopterVerticalIntent.Climb, out hudAscend, out hudDescend);
        Assert(hudAscend && !hudDescend, "climb flag mapping");
        HelicopterControlSurface.ToVerticalFlags(HelicopterVerticalIntent.Hover, out hudAscend, out hudDescend);
        Assert(!hudAscend && !hudDescend, "hover flag mapping");

        // HUD visibility: only the local authorized main driver of a living helicopter sees it.
        Assert(HelicopterControlSurface.ShouldShowControls(true, true, true), "local driver shows HUD");
        Assert(!HelicopterControlSurface.ShouldShowControls(false, true, true), "remote driver hides HUD");
        Assert(!HelicopterControlSurface.ShouldShowControls(true, false, true), "passenger hides HUD");
        Assert(!HelicopterControlSurface.ShouldShowControls(true, true, false), "dead vehicle hides HUD");

        // Responsive button layout math.
        HelicopterButtonLayout phone = HelicopterControlSurface.ComputeLayout(812f, 375f, 1f);
        Assert(phone.Ascent.Size >= HelicopterControlSurface.MinButtonSize && phone.Ascent.Size <= HelicopterControlSurface.MaxButtonSize, "phone button size in band");
        Assert(phone.Ascent.Y < phone.Descent.Y, "ascent stacked above descent");
        Assert(phone.Descent.Right <= 812f && phone.Ascent.Right <= 812f, "buttons inside right edge");
        Assert(phone.Ascent.X > 812f / 2f && phone.Descent.X > 812f / 2f, "buttons clear left joystick");
        Assert(phone.Descent.Bottom <= 375f && phone.Ascent.Bottom <= 375f, "buttons inside bottom edge");
        Assert(phone.Ascent.X >= 0f && phone.Ascent.Y >= 0f && phone.Descent.X >= 0f && phone.Descent.Y >= 0f, "layout coordinates non-negative");
        Assert(Math.Abs(phone.Descent.Y - (phone.Ascent.Bottom + 375f * HelicopterControlSurface.VerticalGapFactor)) < 0.01f, "ascent-descent gap honored");
        float tinyButton = HelicopterControlSurface.ButtonSize(320f, 240f, 1f);
        Assert(tinyButton >= HelicopterControlSurface.MinButtonSize, "small screen hits minimum size");
        float bigButton = HelicopterControlSurface.ButtonSize(2048f, 1536f, 1f);
        Assert(bigButton <= HelicopterControlSurface.MaxButtonSize, "large screen hits maximum size");
        Equal(1f, HelicopterControlSurface.ClampUiScale(0f), "zero ui scale clamps to one");
        Equal(HelicopterControlSurface.MinimumUiScale, HelicopterControlSurface.ClampUiScale(0.3f), "low ui scale clamps up");
        Equal(1.5f, HelicopterControlSurface.ClampUiScale(1.5f), "high ui scale passes through");
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

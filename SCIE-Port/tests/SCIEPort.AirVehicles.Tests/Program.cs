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

        TestAdvancedFlight();

        Console.WriteLine("Helicopter rule tests passed: " + s_count);
        return 0;
    }

    private static void Assert(bool condition, string message)
    {
        s_count++;
        if (!condition) throw new InvalidOperationException("Assertion failed: " + message);
    }

    private static void TestAdvancedFlight()
    {
        var input = new HelicopterFlightInput();
        input.Update(0.04f, true, 0.1f);
        Equal(0f, input.Command, "collective deadzone");
        for (int i = 0; i < 20; i++) input.Update(0.5f, true, 0.02f);
        Assert(input.Command > 0.4f && input.Command < 0.5f, "continuous half collective");
        input.Update(0f, false, 0.02f);
        Assert(input.Value > 0f && input.Value < 0.5f, "release returns smoothly");
        for (int i = 0; i < 30; i++) input.Update(0f, false, 0.02f);
        Equal(0f, input.Command, "released collective reaches neutral");
        input.Reset();
        Assert(!input.IsDragging && input.Value == 0f, "input reset clears ownership");
        var hover = new HelicopterHoverController();
        hover.Reset(70f);
        Equal(0f, hover.Update(0f, false, 70f, 0f, true, 240f, 4.5f, 3.5f, 0.02f), "ground does not auto take off");
        hover.Update(0f, false, 70f, 0f, false, 240f, 4.5f, 3.5f, 0.02f);
        Assert(hover.Mode == HelicopterControlMode.AltitudeHold, "stable aircraft captures height");
        Assert(hover.Update(0f, false, 69f, 0f, false, 240f, 4.5f, 3.5f, 0.02f) > 0f, "altitude disturbance corrected");
        hover.Toggle(70f);
        Assert(!hover.Enabled, "hover can be disabled");
        hover.Update(0f, false, 65f, 0f, false, 240f, 4.5f, 3.5f, 0.02f);
        Assert(hover.Mode == HelicopterControlMode.Assisted, "off uses speed assist not height lock");
        hover.Toggle(65f);
        hover.Update(1f, true, 65f, 0f, false, 240f, 4.5f, 3.5f, 0.02f);
        Assert(hover.Mode == HelicopterControlMode.Manual, "drag overrides hover");
        hover.Update(0f, false, 67f, 2f, false, 240f, 4.5f, 3.5f, 0.02f);
        Assert(hover.Mode == HelicopterControlMode.Recovering, "release brakes before recapture");
        Assert(hover.Update(1f, true, 240f, 1f, false, 240f, 4.5f, 3.5f, 0.02f) <= 0f, "altitude cap cancels climb");
        // Closed-loop simulation includes target Engine's 0.35 vertical body drag,
        // rotor spool-up, collision-ground clamp, and the actual shared flight integrator.
        foreach (float dt in new[] { 1f / 20f, 1f / 30f, 1f / 60f, 1f / 120f })
        {
            hover.Reset(70f); input.Reset();
            float height = 70f, speed = 0f, rotor = 0f, captured = 0f;
            float maxError = 0f;
            for (int frame = 0; frame < (int)(35f / dt); frame++)
            {
                float time = frame * dt;
                bool held = time < 6f;
                input.Update(held ? 0.75f : 0f, held, dt);
                rotor = HelicopterFlightRules.UpdateRotorSpeed(rotor, true, 2.5f, 3.5f, dt);
                float target = hover.Update(input.Command, input.IsDragging, height, speed,
                    height <= 70f, 240f, 4.5f, 3.5f, dt);
                float next = HelicopterFlightRules.IntegrateVerticalSpeed(speed, target, rotor, 9.8f, 2.8f, 4.5f, 3.5f, dt);
                next = HelicopterFlightRules.MoveTowards(speed, next, hover.Acceleration * dt);
                Assert(Math.Abs(next - speed) <= hover.Acceleration * dt + 0.0001f, "vertical acceleration bounded");
                speed = next * (1f - 0.35f * dt);
                height += speed * dt;
                if (height < 70f) { height = 70f; speed = 0f; }
                if (time > 15f && captured == 0f) { captured = hover.TargetHeight; height -= 1f; }
                if (time > 30f) maxError = Math.Max(maxError, Math.Abs(height - captured));
            }
            Assert(height > 75f, "collective lifts aircraft from ground at " + dt);
            Assert(maxError < 0.08f, "hover settles after disturbance at " + dt);
            Assert(Math.Abs(speed) < 0.05f, "hover has no sustained oscillation");
        }
    }

    private static void Equal(float expected, float actual, string message)
    {
        Assert(Math.Abs(expected - actual) < 0.0001f, message);
    }
}

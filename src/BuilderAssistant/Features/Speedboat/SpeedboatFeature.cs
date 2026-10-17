using Engine;
using Game;

namespace BuilderAssistant.Features.Speedboat
{
    /// <summary>
    /// Adds a bounded speed boost only to the independent Speedboat entity.
    /// The native boat and native touch movement controls remain untouched.
    /// </summary>
    internal sealed class SpeedboatFeature
    {
        private const float ExtraAcceleration = 9f;
        private const float MaxHorizontalSpeed = 18f;
        private const float MinimumThrottle = 0.05f;

        private ComponentBody m_lastMountedBoatBody;

        public void Update(ComponentGui componentGui)
        {
            ComponentPlayer player = componentGui?.m_componentPlayer;
            ComponentMount mount = player?.ComponentRider?.Mount;
            ComponentBoat boat = mount?.Entity.FindComponent<ComponentBoat>(throwOnError: false);

            if (boat == null ||
                boat.m_componentBody == null ||
                boat.Entity.ValuesDictionary.DatabaseObject.Name != "Speedboat")
            {
                m_lastMountedBoatBody = null;
                return;
            }

            // ComponentMount.Rider is the first rider attached to the boat body in
            // x25.01.17. Keep the optional boost driver-only so a later passenger
            // seat cannot gain authority merely by having the same parent body.
            if (mount.Rider != player.ComponentRider)
            {
                m_lastMountedBoatBody = null;
                return;
            }

            ComponentBody body = boat.m_componentBody;
            if (m_lastMountedBoatBody != body)
            {
                componentGui.DisplaySmallMessage(
                    "[BuilderAssistant] Speedboat boost active.",
                    Color.White,
                    blinking: false,
                    playNotificationSound: false);
                m_lastMountedBoatBody = body;
            }

            // Match the native boat's valid floating state. Do not propel a
            // grounded or almost fully submerged/sinking boat.
            if (body.ImmersionFactor <= 0.01f ||
                body.ImmersionFactor > 0.95f ||
                body.StandingOnValue.HasValue ||
                body.StandingOnBody != null)
            {
                return;
            }

            float throttle = MathUtils.Clamp(player.ComponentInput.PlayerInput.Move.Z, -1f, 1f);
            if (MathUtils.Abs(throttle) < MinimumThrottle)
            {
                ClampHorizontalSpeed(body);
                return;
            }

            float dt = MathUtils.Clamp(boat.m_subsystemTime.GameTimeDelta, 0f, 0.1f);
            Vector3 forward = body.Matrix.Forward;
            forward.Y = 0f;
            if (forward.LengthSquared() > 0.0001f)
            {
                body.Velocity += ExtraAcceleration * throttle * dt * Vector3.Normalize(forward);
            }

            ClampHorizontalSpeed(body);
        }

        private static void ClampHorizontalSpeed(ComponentBody body)
        {
            Vector3 velocity = body.Velocity;
            Vector3 horizontalVelocity = new Vector3(velocity.X, 0f, velocity.Z);
            float maxSpeedSquared = MaxHorizontalSpeed * MaxHorizontalSpeed;
            if (horizontalVelocity.LengthSquared() <= maxSpeedSquared)
            {
                return;
            }

            horizontalVelocity = MaxHorizontalSpeed * Vector3.Normalize(horizontalVelocity);
            body.Velocity = new Vector3(horizontalVelocity.X, velocity.Y, horizontalVelocity.Z);
        }
    }
}

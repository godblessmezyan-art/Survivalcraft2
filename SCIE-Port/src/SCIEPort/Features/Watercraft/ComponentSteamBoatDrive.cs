using Engine;
using Game;
using Game.NetWork;
using GameEntitySystem;
using TemplatesDatabase;

namespace SCIEPort.Features.Watercraft
{
    /// <summary>
    /// Adds bounded steam-boat acceleration while leaving native boat steering,
    /// buoyancy, damage, mounting and touch input intact.
    /// </summary>
    public sealed class ComponentSteamBoatDrive : Component, IUpdateable
    {
        private const float ExtraAcceleration = 8f;
        private const float MaxHorizontalSpeed = 15f;
        private const float MinimumThrottle = 0.05f;

        private ComponentBody m_body;
        private ComponentMount m_mount;

        /// <inheritdoc />
        public UpdateOrder UpdateOrder => UpdateOrder.Default;

        /// <inheritdoc />
        public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
        {
            base.Load(valuesDictionary, idToEntityMap);
            Entity.FindComponent<ComponentBoat>(throwOnError: true);
            m_body = Entity.FindComponent<ComponentBody>(throwOnError: true);
            m_mount = Entity.FindComponent<ComponentMount>(throwOnError: true);

        }

        /// <inheritdoc />
        public void Update(float dt)
        {
            // Multiplayer authority for custom vehicle acceleration is not yet
            // verified. Native boat behavior remains active outside local play.
            if (CommonLib.WorkType != WorkType.Local)
            {
                return;
            }

            ComponentRider rider = m_mount.Rider;
            ComponentPlayer player = rider?.Entity.FindComponent<ComponentPlayer>(throwOnError: false);
            if (player == null || player.ComponentRider != rider)
            {
                return;
            }

            if (m_body.ImmersionFactor <= 0.01f ||
                m_body.ImmersionFactor > 0.95f ||
                m_body.StandingOnValue.HasValue ||
                m_body.StandingOnBody != null)
            {
                return;
            }

            float throttle = MathUtils.Clamp(player.ComponentInput.PlayerInput.Move.Z, -1f, 1f);
            if (MathUtils.Abs(throttle) >= MinimumThrottle)
            {
                float boundedDt = MathUtils.Clamp(dt, 0f, 0.1f);
                Vector3 forward = m_body.Matrix.Forward;
                forward.Y = 0f;
                if (forward.LengthSquared() > 0.0001f)
                {
                    m_body.Velocity += ExtraAcceleration
                        * throttle
                        * boundedDt
                        * Vector3.Normalize(forward);
                }
            }

            ClampHorizontalSpeed();
        }

        private void ClampHorizontalSpeed()
        {
            Vector3 velocity = m_body.Velocity;
            Vector3 horizontal = new Vector3(velocity.X, 0f, velocity.Z);
            if (horizontal.LengthSquared() <= MaxHorizontalSpeed * MaxHorizontalSpeed)
            {
                return;
            }

            horizontal = MaxHorizontalSpeed * Vector3.Normalize(horizontal);
            m_body.Velocity = new Vector3(horizontal.X, velocity.Y, horizontal.Z);
        }
    }
}

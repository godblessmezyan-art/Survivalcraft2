using Engine;
using Engine.Graphics;
using Game;
using Game.NetWork;
using GameEntitySystem;
using TemplatesDatabase;

namespace SCIEPort.Features.LandVehicles
{
    /// <summary>
    /// Single-player ATV controller. Collision, gravity, step-up and mounted-camera
    /// parent-body behavior remain entirely in the target game's native components.
    /// </summary>
    public sealed class ComponentAtvDrive : Component, IUpdateable
    {
        private const float TurnRate = 1.7f;
        private const float LateralGrip = 7f;

        private ComponentBody m_body;
        private ComponentMount m_mount;

        /// <inheritdoc />
        public UpdateOrder UpdateOrder => UpdateOrder.Default;

        /// <inheritdoc />
        public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
        {
            base.Load(valuesDictionary, idToEntityMap);
            m_body = Entity.FindComponent<ComponentBody>(throwOnError: true);
            m_mount = Entity.FindComponent<ComponentMount>(throwOnError: true);

            // Do not bypass ComponentBody collision. These values make its native
            // smooth-rise path the explicit ATV slope/step limit.
            m_body.IsGravityEnabled = true;
            m_body.IsGroundDragEnabled = true;
            m_body.IsSmoothRiseEnabled = true;
            m_body.MaxSmoothRiseHeight = AtvDriveRules.MaximumSmoothRiseHeight;

            ComponentModel model = Entity.FindComponent<ComponentModel>(throwOnError: true);
            model.TextureOverride = AtvBlock.Texture;
            model.DiffuseColor = new Vector3(0.38f, 0.74f, 0.16f);
            model.EmissionColor = new Vector4(0.04f, 0.06f, 0.02f, 0f);
        }

        /// <inheritdoc />
        public void Update(float dt)
        {
            // No custom input authority is claimed for multiplayer in this phase.
            if (CommonLib.WorkType != WorkType.Local)
            {
                return;
            }

            float boundedDt = MathUtils.Clamp(dt, 0f, 0.1f);
            ComponentRider rider = m_mount.Rider;
            ComponentPlayer player = rider?.Entity.FindComponent<ComponentPlayer>(throwOnError: false);
            if (player == null || player.ComponentRider != rider)
            {
                ApplyCoastingGrip(boundedDt);
                return;
            }

            bool grounded = m_body.StandingOnValue.HasValue || m_body.StandingOnBody != null;
            if (!AtvDriveRules.CanDrive(grounded, m_body.ImmersionFactor))
            {
                ApplyCoastingGrip(boundedDt);
                if (m_body.ImmersionFactor >= 0.8f)
                {
                    m_mount.Rider?.StartDismounting();
                }

                return;
            }

            Vector3 input = player.ComponentInput.PlayerInput.Move;
            float throttle = MathUtils.Clamp(input.Z, -1f, 1f);
            float turn = MathUtils.Clamp(input.X, -1f, 1f);
            Vector3 forward = m_body.Matrix.Forward;
            forward.Y = 0f;
            if (forward.LengthSquared() <= 0.0001f)
            {
                return;
            }

            forward = Vector3.Normalize(forward);
            Vector3 velocity = m_body.Velocity;
            Vector3 horizontal = new Vector3(velocity.X, 0f, velocity.Z);
            float forwardSpeed = Vector3.Dot(horizontal, forward);
            float acceleration = AtvDriveRules.GetLongitudinalAcceleration(forwardSpeed, throttle);
            horizontal += forward * acceleration * boundedDt;
            horizontal = LimitForwardAndReverseSpeed(horizontal, forward);
            m_body.Velocity = new Vector3(horizontal.X, velocity.Y, horizontal.Z);

            if (MathUtils.Abs(turn) >= 0.05f)
            {
                float steering = AtvDriveRules.GetSteeringDirection(forwardSpeed);
                m_body.Rotation *= Quaternion.CreateFromAxisAngle(
                    Vector3.UnitY,
                    -turn * TurnRate * steering * boundedDt);
            }
        }

        private void ApplyCoastingGrip(float dt)
        {
            Vector3 velocity = m_body.Velocity;
            Vector3 horizontal = new Vector3(velocity.X, 0f, velocity.Z);
            horizontal = Vector3.Lerp(horizontal, Vector3.Zero, MathUtils.Saturate(LateralGrip * dt));
            m_body.Velocity = new Vector3(horizontal.X, velocity.Y, horizontal.Z);
        }

        private static Vector3 LimitForwardAndReverseSpeed(Vector3 horizontal, Vector3 forward)
        {
            float forwardSpeed = Vector3.Dot(horizontal, forward);
            Vector3 lateral = horizontal - forward * forwardSpeed;
            forwardSpeed = MathUtils.Clamp(
                forwardSpeed,
                -AtvDriveRules.MaximumReverseSpeed,
                AtvDriveRules.MaximumForwardSpeed);
            lateral = Vector3.Lerp(lateral, Vector3.Zero, 0.35f);
            return forward * forwardSpeed + lateral;
        }
    }
}

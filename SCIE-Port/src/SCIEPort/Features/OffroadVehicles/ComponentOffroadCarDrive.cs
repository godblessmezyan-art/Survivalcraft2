using Engine;
using Engine.Graphics;
using Game;
using Game.NetWork;
using GameEntitySystem;
using SCIEPort.Features.Vehicles;
using TemplatesDatabase;

namespace SCIEPort.Features.OffroadVehicles
{
    /// <summary>Driver-only controller for the side-by-side two-seat off-road car.</summary>
    public sealed class ComponentOffroadCarDrive : Component, IUpdateable
    {
        private const float TurnRate = 1.25f;
        private const float LateralGrip = 6f;
        private ComponentBody m_body;
        private ComponentTwoSeatManager m_seats;

        /// <inheritdoc />
        public UpdateOrder UpdateOrder => UpdateOrder.Default;

        /// <inheritdoc />
        public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
        {
            base.Load(valuesDictionary, idToEntityMap);
            m_body = Entity.FindComponent<ComponentBody>(throwOnError: true);
            m_seats = Entity.FindComponent<ComponentTwoSeatManager>(throwOnError: true);
            m_body.IsGravityEnabled = true;
            m_body.IsGroundDragEnabled = true;
            m_body.IsSmoothRiseEnabled = true;
            m_body.MaxSmoothRiseHeight = OffroadCarRules.MaximumSmoothRiseHeight;

            ComponentModel model = Entity.FindComponent<ComponentModel>(throwOnError: true);
            model.TextureOverride = OffroadCarBlock.Texture;
            model.DiffuseColor = Vector3.One;
            model.EmissionColor = new Vector4(0.035f, 0.045f, 0.055f, 0f);
        }

        /// <inheritdoc />
        public void Update(float dt)
        {
            ComponentRider driver = m_seats.Driver;
            ComponentPlayer player = driver?.Entity.FindComponent<ComponentPlayer>(throwOnError: false);
            if (player == null || player.ComponentRider != driver || !player.PlayerData.IsMainPlayer)
            {
                m_body.IsGroundDragEnabled = driver == null;
                return;
            }

            // A vehicle that existed before activation must still be locked: without an
            // authorized session the driver gets the throttled password prompt and the car
            // holds parking drag instead of responding to throttle or steering.
            if (!VehicleActivationService.EnsureActivated())
            {
                m_body.IsGroundDragEnabled = true;
                return;
            }

            float boundedDt = MathUtils.Clamp(dt, 0f, 0.1f);
            bool terrainGrounded = m_body.StandingOnValue.HasValue;
            bool bodyGrounded = m_body.StandingOnBody != null;
            m_body.IsGroundDragEnabled = false;
            if (!OffroadCarRules.CanApplyDrive(terrainGrounded, bodyGrounded, m_body.ImmersionFactor))
            {
                if (m_body.ImmersionFactor >= 0.8f)
                {
                    m_seats.Driver?.StartDismounting();
                    m_seats.Passenger?.StartDismounting();
                }

                return;
            }

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
            Vector3 input = m_seats.DriverMove;
            forwardSpeed = OffroadCarRules.IntegrateForwardSpeed(forwardSpeed, MathUtils.Clamp(input.Z, -1f, 1f), boundedDt);
            Vector3 lateral = horizontal - forward * Vector3.Dot(horizontal, forward);
            lateral = Vector3.Lerp(lateral, Vector3.Zero, MathUtils.Saturate(LateralGrip * boundedDt));
            horizontal = forward * forwardSpeed + lateral;
            m_body.Velocity = new Vector3(horizontal.X, velocity.Y, horizontal.Z);

            float turn = MathUtils.Clamp(input.X, -1f, 1f);
            if (MathUtils.Abs(turn) >= 0.05f)
            {
                m_body.Rotation *= Quaternion.CreateFromAxisAngle(
                    Vector3.UnitY,
                    -turn * TurnRate * OffroadCarRules.GetSteeringDirection(forwardSpeed) *
                    OffroadCarRules.GetSteeringScale(MathUtils.Abs(forwardSpeed)) * boundedDt);
            }
        }
    }
}

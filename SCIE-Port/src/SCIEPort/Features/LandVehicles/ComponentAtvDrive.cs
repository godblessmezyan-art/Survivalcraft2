using Engine;
using Engine.Graphics;
using Game;
using Game.NetWork;
using GameEntitySystem;
using SCIEPort.Features.Vehicles;
using TemplatesDatabase;

namespace SCIEPort.Features.LandVehicles
{
    /// <summary>
    /// Driver-seat ATV controller. Collision, gravity, step-up and mounted-camera
    /// parent-body behavior remain entirely in the target game's native components.
    /// </summary>
    public sealed class ComponentAtvDrive : Component, IUpdateable
    {
        private const float TurnRate = 1.55f;
        private const float LateralGrip = 5f;
        private const float DiagnosticPeriod = 5f;

        private ComponentBody m_body;
        private ComponentMount m_mount;
        private ComponentTwoSeatManager m_seatManager;
        private float m_airborneTime;
        private float m_diagnosticTime;

        /// <inheritdoc />
        public UpdateOrder UpdateOrder => UpdateOrder.Default;

        /// <inheritdoc />
        public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
        {
            base.Load(valuesDictionary, idToEntityMap);
            m_body = Entity.FindComponent<ComponentBody>(throwOnError: true);
            m_mount = Entity.FindComponent<ComponentMount>(throwOnError: true);
            m_seatManager = Entity.FindComponent<ComponentTwoSeatManager>(throwOnError: true);

            // Do not bypass ComponentBody collision. These values make its native
            // smooth-rise path the explicit ATV slope/step limit.
            m_body.IsGravityEnabled = true;
            m_body.IsGroundDragEnabled = true;
            m_body.IsSmoothRiseEnabled = true;
            m_body.MaxSmoothRiseHeight = AtvDriveRules.MaximumSmoothRiseHeight;

            ComponentModel model = Entity.FindComponent<ComponentModel>(throwOnError: true);
            model.TextureOverride = AtvBlock.Texture;
            // The instance-model path multiplies the bound texture by DiffuseColor,
            // so any tint here recolors the whole bike in-world (the inventory icon
            // bakes into BlockMesh and ignores it). The legacy grass-green tint
            // matched the old four-color palette but turned the X1 atlas green;
            // keep white like every other vehicle plus a neutral night floor.
            model.DiffuseColor = Vector3.One;
            model.EmissionColor = new Vector4(0.045f, 0.045f, 0.05f, 0f);

            // Read-only audit: logs the authored collision box versus the model bounds when
            // VehicleAuditOptions.Diagnostics is on. It mutates nothing in the physics path.
            VehicleCollisionAudit.LogOnce("ATV", m_body, model);
        }

        /// <inheritdoc />
        public void Update(float dt)
        {
            float boundedDt = MathUtils.Clamp(dt, 0f, 0.1f);
            ComponentRider rider = m_seatManager.Driver;
            ComponentPlayer player = rider?.Entity.FindComponent<ComponentPlayer>(throwOnError: false);
            if (player == null || player.ComponentRider != rider || !player.PlayerData.IsMainPlayer)
            {
                RestoreParkingDrag(rider == null);
                return;
            }

            // A vehicle that existed before activation must still be locked: without an
            // authorized session the driver gets the throttled password prompt and the ATV
            // stays in neutral (no throttle, no steering) rather than responding to input.
            if (!VehicleActivationService.EnsureActivated())
            {
                RestoreParkingDrag(false);
                return;
            }

            bool terrainGrounded = m_body.StandingOnValue.HasValue;
            bool bodyGrounded = m_body.StandingOnBody != null;
            bool supported = terrainGrounded || bodyGrounded;
            m_airborneTime = supported ? 0f : m_airborneTime + boundedDt;

            // ComponentBody applies a 6*dt ground-speed lerp after custom drive updates.
            // Disable that competing drag only while occupied; this controller supplies
            // deterministic longitudinal coast and frame-rate-independent lateral grip.
            m_body.IsGroundDragEnabled = false;

            Vector3 input = m_seatManager.DriverMove;
            float throttle = MathUtils.Clamp(input.Z, -1f, 1f);
            float turn = MathUtils.Clamp(input.X, -1f, 1f);
            if (!AtvDriveRules.CanApplyDrive(terrainGrounded, bodyGrounded, m_body.ImmersionFactor))
            {
                if (m_body.ImmersionFactor >= 0.8f)
                {
                    m_mount.Rider?.StartDismounting();
                }

                WriteDiagnostics(boundedDt, throttle, terrainGrounded, bodyGrounded,
                    m_body.ImmersionFactor >= 0.25f ? "water-blocked" :
                    (bodyGrounded ? "body-supported" :
                    (AtvDriveRules.IsAirborneGrace(supported, m_airborneTime, m_body.ImmersionFactor) ? "airborne-grace" : "airborne")));
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
            forwardSpeed = AtvDriveRules.IntegrateForwardSpeed(forwardSpeed, throttle, boundedDt);
            Vector3 lateral = horizontal - forward * Vector3.Dot(horizontal, forward);
            lateral = Vector3.Lerp(lateral, Vector3.Zero, MathUtils.Saturate(LateralGrip * boundedDt));
            horizontal = forward * forwardSpeed + lateral;
            m_body.Velocity = new Vector3(horizontal.X, velocity.Y, horizontal.Z);

            if (MathUtils.Abs(turn) >= 0.05f)
            {
                float steering = AtvDriveRules.GetSteeringDirection(forwardSpeed);
                float steeringScale = AtvDriveRules.GetSteeringScale(MathUtils.Abs(forwardSpeed));
                m_body.Rotation *= Quaternion.CreateFromAxisAngle(
                    Vector3.UnitY,
                    -turn * TurnRate * steering * steeringScale * boundedDt);
            }

            WriteDiagnostics(boundedDt, throttle, terrainGrounded, bodyGrounded, "terrain-drive");
        }

        private void RestoreParkingDrag(bool unoccupied)
        {
            // An unoccupied ATV uses native parking drag. A remotely driven ATV is
            // interpolated from the native parent-body stream and must not receive
            // an extra observer-side ground-drag pass.
            m_body.IsGroundDragEnabled = unoccupied;
            m_airborneTime = 0f;
        }

        private void WriteDiagnostics(
            float dt,
            float throttle,
            bool terrainGrounded,
            bool bodyGrounded,
            string state)
        {
            m_diagnosticTime += dt;
            if (m_diagnosticTime < DiagnosticPeriod)
            {
                return;
            }

            m_diagnosticTime = 0f;
            Vector3 velocity = m_body.Velocity;
            float speed = new Vector2(velocity.X, velocity.Z).Length();
            Log.Information(string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "SCIE-Port ATV: state={0}, speed={1:0.00}, throttle={2:0.00}, terrainGrounded={3}, bodyGrounded={4}, airborne={5:0.00}, nativeGroundDrag={6}, lateralGrip={7:0.00}",
                state,
                speed,
                throttle,
                terrainGrounded,
                bodyGrounded,
                m_airborneTime,
                m_body.IsGroundDragEnabled,
                LateralGrip));
        }
    }
}

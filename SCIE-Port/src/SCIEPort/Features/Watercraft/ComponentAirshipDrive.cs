using Engine;
using Game;
using Game.NetWork;
using GameEntitySystem;
using SCIEPort.Features.Vehicles;
using TemplatesDatabase;

namespace SCIEPort.Features.Watercraft
{
    /// <summary>
    /// Implements bounded local airship flight without replacing the SCNET player component.
    /// Forward touch input follows the rider's view direction, matching the original design.
    /// </summary>
    public sealed class ComponentAirshipDrive : Component, IUpdateable
    {
        private const float Acceleration = 3f;
        private const float MaximumSpeed = 12f;
        private const float TurnRate = 1.4f;
        private const float MinimumInput = 0.05f;

        private ComponentBody m_body;
        private ComponentDamage m_damage;
        private ComponentMount m_mount;

        /// <inheritdoc />
        public UpdateOrder UpdateOrder => UpdateOrder.Default;

        /// <inheritdoc />
        public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
        {
            base.Load(valuesDictionary, idToEntityMap);
            m_body = Entity.FindComponent<ComponentBody>(throwOnError: true);
            m_damage = Entity.FindComponent<ComponentDamage>(throwOnError: true);
            m_mount = Entity.FindComponent<ComponentMount>(throwOnError: true);

            ComponentModel model = Entity.FindComponent<ComponentModel>(throwOnError: true);
            model.TextureOverride = AirshipBlock.GetWhiteTexture();
            model.DiffuseColor = Vector3.One;
            model.EmissionColor = new Vector4(0.12f, 0.12f, 0.12f, 0f);
        }

        /// <inheritdoc />
        public void Update(float dt)
        {
            m_body.IsGravityEnabled = false;
            m_body.IsGroundDragEnabled = false;

            float boundedDt = MathUtils.Clamp(dt, 0f, 0.1f);
            if (CommonLib.WorkType != WorkType.Client && m_body.ImmersionFactor > 0.95f)
            {
                m_damage.Damage(0.005f * boundedDt);
                m_mount.Rider?.StartDismounting();
            }

            ComponentRider rider = m_mount.Rider;
            ComponentPlayer player = rider?.Entity.FindComponent<ComponentPlayer>(throwOnError: false);
            if (player == null || player.ComponentRider != rider || !player.PlayerData.IsMainPlayer)
            {
                ClampSpeed();
                return;
            }

            // A vehicle that existed before activation must still be locked: without an
            // authorized session the pilot gets the throttled password prompt and the
            // airship holds its current (clamped) velocity instead of responding to input.
            if (!VehicleActivationService.EnsureActivated())
            {
                ClampSpeed();
                return;
            }

            Vector3 movement = player.ComponentInput.PlayerInput.Move;
            float turn = MathUtils.Clamp(movement.X, -1f, 1f);
            float throttle = MathUtils.Clamp(movement.Z, -1f, 1f);

            if (MathUtils.Abs(turn) >= MinimumInput)
            {
                Quaternion rotation = m_body.Rotation;
                float yaw = MathUtils.Atan2(
                    2f * rotation.Y * rotation.W - 2f * rotation.X * rotation.Z,
                    1f - 2f * rotation.Y * rotation.Y - 2f * rotation.Z * rotation.Z);
                m_body.Rotation = Quaternion.CreateFromAxisAngle(
                    Vector3.UnitY,
                    yaw - turn * TurnRate * boundedDt);
            }

            if (MathUtils.Abs(throttle) >= MinimumInput)
            {
                Vector3 direction = Matrix.CreateFromQuaternion(
                    rider.ComponentCreature.ComponentCreatureModel.EyeRotation).Forward;
                if (direction.LengthSquared() > 0.0001f)
                {
                    m_body.Velocity += Acceleration
                        * throttle
                        * boundedDt
                        * Vector3.Normalize(direction);
                }
            }

            ClampSpeed();
        }

        private void ClampSpeed()
        {
            if (m_body.Velocity.LengthSquared() > MaximumSpeed * MaximumSpeed)
            {
                m_body.Velocity = MaximumSpeed * Vector3.Normalize(m_body.Velocity);
            }
        }
    }
}

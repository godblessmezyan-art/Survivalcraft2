using Engine;
using Game;
using Game.NetWork;
using GameEntitySystem;
using SCIEPort.Features.Vehicles;
using TemplatesDatabase;

namespace SCIEPort.Features.Watercraft
{
    /// <summary>
    /// Driver-only two-seat speedboat controller. ComponentBody retains native
    /// buoyancy, drag, collision and persistence; no passenger input is consumed.
    /// </summary>
    public sealed class ComponentSteamBoatDrive : Component, IUpdateable
    {
        private const float MinimumThrottle = 0.05f;

        private SubsystemAudio m_audio;
        private ComponentBody m_body;
        private ComponentDamage m_damage;
        private ComponentTwoSeatManager m_seats;
        private float m_turnSpeed;

        /// <inheritdoc />
        public UpdateOrder UpdateOrder => UpdateOrder.Default;

        /// <inheritdoc />
        public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
        {
            base.Load(valuesDictionary, idToEntityMap);
            m_audio = Project.FindSubsystem<SubsystemAudio>(throwOnError: true);
            m_body = Entity.FindComponent<ComponentBody>(throwOnError: true);
            m_damage = Entity.FindComponent<ComponentDamage>(throwOnError: true);
            m_seats = Entity.FindComponent<ComponentTwoSeatManager>(throwOnError: true);
            ComponentModel model = Entity.FindComponent<ComponentModel>(throwOnError: true);
            model.DiffuseColor = Vector3.One;
            model.EmissionColor = new Vector4(0.12f, 0.12f, 0.12f, 0f);
        }

        /// <inheritdoc />
        public void Update(float dt)
        {
            float boundedDt = MathUtils.Clamp(dt, 0f, 0.1f);
            if (CommonLib.WorkType != WorkType.Client)
            {
                UpdateDamageAndSinking(boundedDt);
            }

            ComponentRider driver = m_seats.Driver;
            ComponentPlayer player = driver?.Entity.FindComponent<ComponentPlayer>(throwOnError: false);
            bool canDrive = player != null && player.ComponentRider == driver && player.PlayerData.IsMainPlayer;
            Vector3 driverMove = m_seats.DriverMove;
            float move = canDrive ? MathUtils.Clamp(driverMove.Z, -1f, 1f) : 0f;
            float turn = canDrive ? MathUtils.Clamp(driverMove.X, -1f, 1f) : 0f;

            bool waterborne = SteamBoatRules.IsWaterborne(
                m_body.ImmersionFactor,
                m_body.StandingOnValue.HasValue,
                m_body.StandingOnBody != null);
            m_turnSpeed += 2.5f * boundedDt * (turn - m_turnSpeed);

            Quaternion rotation = m_body.Rotation;
            float yaw = MathUtils.Atan2(
                2f * rotation.Y * rotation.W - 2f * rotation.X * rotation.Z,
                1f - 2f * rotation.Y * rotation.Y - 2f * rotation.Z * rotation.Z);
            if (waterborne)
            {
                yaw -= m_turnSpeed * boundedDt;
            }

            m_body.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, yaw);
            if (waterborne && MathUtils.Abs(move) >= MinimumThrottle)
            {
                m_body.Velocity += boundedDt *
                    (SteamBoatRules.BaseAcceleration + SteamBoatRules.ExtraAcceleration) *
                    move * m_body.Matrix.Forward;
            }

            ClampHorizontalSpeed();
        }

        private void UpdateDamageAndSinking(float dt)
        {
            if (m_damage.Hitpoints < 0.33f)
            {
                m_body.Density = 1.15f;
                if (m_damage.Hitpoints - m_damage.HitpointsChange >= 0.33f && m_body.ImmersionFactor > 0f)
                {
                    m_audio.PlaySound("Audio/Sinking", 1f, 0f, m_body.Position, 4f, autoDelay: true);
                }
            }
            else if (m_damage.Hitpoints < 0.66f)
            {
                m_body.Density = 0.7f;
                if (m_damage.Hitpoints - m_damage.HitpointsChange >= 0.66f && m_body.ImmersionFactor > 0f)
                {
                    m_audio.PlaySound("Audio/Sinking", 1f, 0f, m_body.Position, 4f, autoDelay: true);
                }
            }
            else
            {
                m_body.Density = 0.2f;
            }

            if (m_body.ImmersionFactor > 0.95f)
            {
                m_damage.Damage(0.005f * dt);
                m_seats.Driver?.StartDismounting();
                m_seats.Passenger?.StartDismounting();
            }
        }

        private void ClampHorizontalSpeed()
        {
            Vector3 velocity = m_body.Velocity;
            Vector3 horizontal = new Vector3(velocity.X, 0f, velocity.Z);
            float maximum = SteamBoatRules.MaximumHorizontalSpeed;
            if (horizontal.LengthSquared() <= maximum * maximum)
            {
                return;
            }

            horizontal = maximum * Vector3.Normalize(horizontal);
            m_body.Velocity = new Vector3(horizontal.X, velocity.Y, horizontal.Z);
        }
    }
}

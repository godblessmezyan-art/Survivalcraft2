using Engine;
using Game;
using Game.NetWork;
using GameEntitySystem;
using TemplatesDatabase;

namespace SCIEPort.Features.Tanks
{
    /// <summary>Single-player tracked ground drive; native ComponentBody owns all collision and gravity.</summary>
    public sealed class ComponentTankDrive : Component, IUpdateable
    {
        private ComponentBody m_body;
        private ComponentMount m_mount;
        private float m_turretYaw;
        private float m_gunPitch;

        /// <summary>Saved independent turret angle, reserved for phase-two aiming.</summary>
        public float TurretYaw => m_turretYaw;
        /// <summary>Saved independent gun pitch, reserved for phase-two aiming.</summary>
        public float GunPitch => m_gunPitch;

        /// <summary>Updates persisted turret state for a future aiming controller without affecting hull steering.</summary>
        public void SetTurretAngles(float yaw, float pitch)
        {
            m_turretYaw = MathUtils.Remainder(yaw, MathUtils.PI * 2f);
            m_gunPitch = MathUtils.Clamp(pitch, -.35f, .35f);
        }
        /// <inheritdoc />
        public UpdateOrder UpdateOrder => UpdateOrder.Default;

        /// <inheritdoc />
        public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
        {
            base.Load(valuesDictionary, idToEntityMap);
            m_body = Entity.FindComponent<ComponentBody>(true);
            m_mount = Entity.FindComponent<ComponentMount>(true);
            m_turretYaw = valuesDictionary.GetValue("TurretYaw", 0f);
            m_gunPitch = valuesDictionary.GetValue("GunPitch", 0f);
            m_body.IsGravityEnabled = true;
            m_body.IsGroundDragEnabled = true;
            m_body.IsSmoothRiseEnabled = true;
            m_body.MaxSmoothRiseHeight = TankRules.StepHeight;
            ComponentModel model = Entity.FindComponent<ComponentModel>(true);
            model.TextureOverride = TankBlock.Texture;
            model.DiffuseColor = new Vector3(.28f, .42f, .15f);
            model.EmissionColor = new Vector4(.12f, .12f, .12f, 0f);
        }

        /// <inheritdoc />
        public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
        {
            base.Save(valuesDictionary, entityToIdMap);
            if (m_turretYaw != 0f) valuesDictionary.SetValue("TurretYaw", m_turretYaw);
            if (m_gunPitch != 0f) valuesDictionary.SetValue("GunPitch", m_gunPitch);
        }

        /// <inheritdoc />
        public void Update(float dt)
        {
            if (CommonLib.WorkType != WorkType.Local) return;
            float t = MathUtils.Clamp(dt, 0f, .1f);
            ComponentRider rider = m_mount.Rider;
            ComponentPlayer player = rider?.Entity.FindComponent<ComponentPlayer>(false);
            if (player == null || player.ComponentRider != rider) { Coast(t); return; }
            bool grounded = m_body.StandingOnValue.HasValue || m_body.StandingOnBody != null;
            if (!grounded || m_body.ImmersionFactor >= .25f) { Coast(t); if (m_body.ImmersionFactor >= .8f) rider.StartDismounting(); return; }
            Vector3 forward = m_body.Matrix.Forward; forward.Y = 0f;
            if (forward.LengthSquared() < .0001f) return;
            forward = Vector3.Normalize(forward);
            Vector3 input = player.ComponentInput.PlayerInput.Move;
            Vector3 velocity = m_body.Velocity;
            Vector3 horizontal = new Vector3(velocity.X, 0f, velocity.Z);
            float speed = Vector3.Dot(horizontal, forward);
            horizontal += forward * TankRules.Acceleration(speed, MathUtils.Clamp(input.Z, -1f, 1f)) * t;
            float clamped = MathUtils.Clamp(Vector3.Dot(horizontal, forward), -TankRules.ReverseSpeed, TankRules.ForwardSpeed);
            m_body.Velocity = new Vector3((forward * clamped).X, velocity.Y, (forward * clamped).Z);
            if (MathUtils.Abs(input.X) >= .05f)
            {
                float direction = speed < -.1f ? -1f : 1f;
                m_body.Rotation *= Quaternion.CreateFromAxisAngle(Vector3.UnitY, -input.X * direction * 1.25f * t);
            }
        }

        private void Coast(float dt)
        {
            Vector3 v = m_body.Velocity;
            Vector3 h = Vector3.Lerp(new Vector3(v.X, 0f, v.Z), Vector3.Zero, MathUtils.Saturate(4f * dt));
            m_body.Velocity = new Vector3(h.X, v.Y, h.Z);
        }
    }
}

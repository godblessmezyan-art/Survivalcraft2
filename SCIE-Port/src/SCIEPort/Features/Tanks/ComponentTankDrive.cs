using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;
using Game.NetWork;
using GameEntitySystem;
using TemplatesDatabase;

namespace SCIEPort.Features.Tanks
{
    /// <summary>Single-player tracked ground drive; native ComponentBody owns all collision and gravity.</summary>
    public sealed class ComponentTankDrive : Component, IUpdateable
    {
        private const float TurnRate = 1.1f;
        private const float LateralGrip = 7f;
        private const float DiagnosticPeriod = 5f;

        private ComponentBody m_body;
        private ComponentMount m_mount;
        private ComponentModel m_model;
        private SubsystemProjectiles m_projectiles;
        private SubsystemExplosions m_explosions;
        private SubsystemAudio m_audio;
        private readonly Dictionary<Projectile, PlayerData> m_shells = new Dictionary<Projectile, PlayerData>();
        private ModelBone m_turretBone;
        private ModelBone m_gunBone;
        private float m_turretYaw;
        private float m_gunPitch;
        private float m_diagnosticTime;
        private float m_fireCooldown;

        /// <summary>Saved independent turret yaw used by the main-gun aiming controller.</summary>
        public float TurretYaw => m_turretYaw;
        /// <summary>Saved independent gun pitch used by the main-gun aiming controller.</summary>
        public float GunPitch => m_gunPitch;

        /// <summary>Updates persisted turret state without affecting hull steering.</summary>
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
            m_model = Entity.FindComponent<ComponentModel>(true);
            m_projectiles = Project.FindSubsystem<SubsystemProjectiles>(true);
            m_explosions = Project.FindSubsystem<SubsystemExplosions>(true);
            m_audio = Project.FindSubsystem<SubsystemAudio>(true);
            m_turretYaw = valuesDictionary.GetValue("TurretYaw", 0f);
            m_gunPitch = valuesDictionary.GetValue("GunPitch", 0f);
            m_body.IsGravityEnabled = true;
            m_body.IsGroundDragEnabled = true;
            m_body.IsSmoothRiseEnabled = true;
            m_body.MaxSmoothRiseHeight = TankRules.StepHeight;
            m_model.TextureOverride = TankBlock.Texture;
            // The palette already contains the armor/track colors. A near-white
            // diffuse multiplier preserves them instead of multiplying green by green.
            m_model.DiffuseColor = new Vector3(.85f, .9f, .8f);
            m_model.EmissionColor = new Vector4(.04f, .04f, .03f, 0f);
        }

        /// <inheritdoc />
        public override void OnEntityAdded()
        {
            base.OnEntityAdded();
            m_projectiles.ProjectileRemoved += OnProjectileRemoved;
        }

        /// <inheritdoc />
        public override void OnEntityRemoved()
        {
            m_projectiles.ProjectileRemoved -= OnProjectileRemoved;
            m_shells.Clear();
            base.OnEntityRemoved();
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
            m_fireCooldown = MathUtils.Max(0f, m_fireCooldown - t);
            ApplyArticulation();
            ComponentRider rider = m_mount.Rider;
            ComponentPlayer player = rider?.Entity.FindComponent<ComponentPlayer>(false);
            if (player == null || player.ComponentRider != rider)
            {
                m_body.IsGroundDragEnabled = true;
                return;
            }

            // ComponentBody otherwise applies native ground drag after this controller,
            // producing the same double-drag failure previously found on the ATV.
            m_body.IsGroundDragEnabled = false;
            bool terrainGrounded = m_body.StandingOnValue.HasValue;
            bool bodyGrounded = m_body.StandingOnBody != null;
            Vector3 input = player.ComponentInput.PlayerInput.Move;
            float throttle = MathUtils.Clamp(input.Z, -1f, 1f);

            Ray3? hitRay = player.ComponentInput.PlayerInput.Hit;
            AimTurret(hitRay.HasValue ? hitRay.Value.Direction :
                Matrix.CreateFromQuaternion(player.ComponentCreatureModel.EyeRotation).Forward);
            ApplyArticulation();
            if (TankRules.CanFire(m_fireCooldown, hitRay.HasValue, m_body.ImmersionFactor))
            {
                FireMainGun(player, hitRay.Value.Direction);
            }

            if (!TankRules.CanApplyDrive(terrainGrounded, bodyGrounded, m_body.ImmersionFactor))
            {
                // Preserve horizontal momentum while airborne. Gravity, collision and
                // native air/water drag remain owned by ComponentBody.
                if (m_body.ImmersionFactor >= .8f) rider.StartDismounting();
                WriteDiagnostics(t, throttle, terrainGrounded, bodyGrounded, "drive-blocked");
                return;
            }

            Vector3 forward = m_body.Matrix.Forward; forward.Y = 0f;
            if (forward.LengthSquared() < .0001f) return;
            forward = Vector3.Normalize(forward);
            Vector3 velocity = m_body.Velocity;
            Vector3 horizontal = new Vector3(velocity.X, 0f, velocity.Z);
            float speed = Vector3.Dot(horizontal, forward);
            speed = TankRules.IntegrateForwardSpeed(speed, throttle, t);
            Vector3 lateral = horizontal - forward * Vector3.Dot(horizontal, forward);
            lateral = Vector3.Lerp(lateral, Vector3.Zero, MathUtils.Saturate(LateralGrip * t));
            horizontal = forward * speed + lateral;
            m_body.Velocity = new Vector3(horizontal.X, velocity.Y, horizontal.Z);
            if (MathUtils.Abs(input.X) >= .05f)
            {
                float direction = TankRules.GetSteeringDirection(speed);
                float scale = TankRules.GetSteeringScale(MathUtils.Abs(speed));
                m_body.Rotation *= Quaternion.CreateFromAxisAngle(Vector3.UnitY, -input.X * direction * TurnRate * scale * t);
            }
            WriteDiagnostics(t, throttle, terrainGrounded, bodyGrounded, "terrain-drive");
        }

        private void AimTurret(Vector3 direction)
        {
            direction.Y = MathUtils.Clamp(direction.Y, -.75f, .55f);
            if (direction.LengthSquared() <= .0001f) return;
            direction = Vector3.Normalize(direction);
            Vector3 horizontal = new Vector3(direction.X, 0f, direction.Z);
            if (horizontal.LengthSquared() > .0001f)
            {
                horizontal = Vector3.Normalize(horizontal);
                m_turretYaw = MathUtils.Atan2(
                    -Vector3.Dot(horizontal, m_body.Matrix.Right),
                    Vector3.Dot(horizontal, m_body.Matrix.Forward));
            }
            m_gunPitch = MathUtils.Clamp(
                MathUtils.Atan2(direction.Y, new Vector2(direction.X, direction.Z).Length()),
                -.2f,
                .35f);
        }

        private void ApplyArticulation()
        {
            if (m_model.Model == null) return;
            if (m_turretBone == null) m_turretBone = m_model.Model.FindBone("TankTurret", false);
            if (m_gunBone == null) m_gunBone = m_model.Model.FindBone("TankGun", false);
            if (m_turretBone != null) m_model.SetBoneTransform(m_turretBone.Index, Matrix.CreateRotationY(m_turretYaw));
            if (m_gunBone != null) m_model.SetBoneTransform(m_gunBone.Index, Matrix.CreateRotationX(m_gunPitch));
        }

        private void FireMainGun(ComponentPlayer player, Vector3 requestedDirection)
        {
            if (requestedDirection.LengthSquared() <= .0001f) return;
            Vector3 direction = Vector3.Normalize(requestedDirection);
            Vector3 muzzle = m_body.Position + Vector3.UnitY * 1.65f + direction * 3.2f;
            int shellValue = Terrain.MakeBlockValue(BulletBlock.Index, 0, 0);
            Projectile shell = m_projectiles.FireProjectile(
                shellValue,
                muzzle,
                direction * TankRules.ShellSpeed,
                Vector3.Zero,
                player.Entity.FindComponent<ComponentCreature>(true));
            if (shell == null) return;
            shell.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
            m_shells[shell] = player.PlayerData;
            m_fireCooldown = TankRules.FireCooldown;
            m_audio.PlaySound("Audio/MusketFire", 1f, 0f, muzzle, 64f, true);
            Vector3 recoil = new Vector3(direction.X, 0f, direction.Z);
            if (recoil.LengthSquared() > .0001f)
                m_body.Velocity -= Vector3.Normalize(recoil) * .45f;
        }

        private void OnProjectileRemoved(Projectile projectile)
        {
            PlayerData playerData;
            if (!m_shells.TryGetValue(projectile, out playerData)) return;
            m_shells.Remove(projectile);
            m_explosions.AddExplosion(
                Terrain.ToCell(projectile.Position.X),
                Terrain.ToCell(projectile.Position.Y),
                Terrain.ToCell(projectile.Position.Z),
                TankRules.ShellExplosionPressure,
                false,
                false,
                playerData);
        }

        private void WriteDiagnostics(float dt, float throttle, bool terrainGrounded, bool bodyGrounded, string state)
        {
            m_diagnosticTime += dt;
            if (m_diagnosticTime < DiagnosticPeriod) return;
            m_diagnosticTime = 0f;
            Vector3 velocity = m_body.Velocity;
            Log.Information(string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "SCIE-Port Tank: state={0}, speed={1:0.00}, throttle={2:0.00}, terrainGrounded={3}, bodyGrounded={4}, immersion={5:0.00}, nativeGroundDrag={6}",
                state,
                new Vector2(velocity.X, velocity.Z).Length(),
                throttle,
                terrainGrounded,
                bodyGrounded,
                m_body.ImmersionFactor,
                m_body.IsGroundDragEnabled));
        }
    }
}

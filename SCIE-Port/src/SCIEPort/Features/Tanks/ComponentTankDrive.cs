using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;
using Game.NetWork;
using GameEntitySystem;
using SCIEPort.Features.Vehicles;
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
        private SubsystemTankShellBlockBehavior m_shellBehavior;
        private sealed class ShellState
        {
            public PlayerData PlayerData;
        }

        private readonly Dictionary<Projectile, ShellState> m_shells = new Dictionary<Projectile, ShellState>();
        private ModelBone m_turretBone;
        private ModelBone m_gunBone;
        private float m_turretYaw;
        private float m_gunPitch;
        private float m_diagnosticTime;
        private float m_fireCooldown;
        private float m_networkStateTime;
        private ushort m_localSequence;
        private ushort m_remoteSequence;
        private ushort m_remoteRiderId;
        private bool m_hasRemoteSequence;

        /// <summary>Saved independent turret yaw used by the main-gun aiming controller.</summary>
        public float TurretYaw => m_turretYaw;
        /// <summary>Saved independent gun pitch used by the main-gun aiming controller.</summary>
        public float GunPitch => m_gunPitch;

        /// <summary>Updates persisted turret state without affecting hull steering.</summary>
        public void SetTurretAngles(float yaw, float pitch)
        {
            m_turretYaw = TankRules.NormalizeAngle(yaw);
            m_gunPitch = MathUtils.Clamp(pitch, TankRules.MinimumGunPitch, TankRules.MaximumGunPitch);
        }

        /// <summary>Applies replicated visual articulation on a non-authoritative peer.</summary>
        public void ApplyNetworkTurretState(float yaw, float pitch)
        {
            SetTurretAngles(yaw, pitch);
            ApplyArticulation();
        }

        /// <summary>Validates and applies one remote-driver request on the server.</summary>
        public bool AcceptRemoteControl(
            ComponentPlayer player,
            ushort sequence,
            float yaw,
            float pitch,
            bool fire)
        {
            if (CommonLib.WorkType != WorkType.Server || player == null ||
                player.ComponentRider == null || m_mount.Rider != player.ComponentRider ||
                player.ComponentBody.ParentBody != m_body)
            {
                return false;
            }

            ushort riderId = player.Entity.EntityId;
            if (m_remoteRiderId != riderId)
            {
                m_remoteRiderId = riderId;
                m_hasRemoteSequence = false;
            }

            if (m_hasRemoteSequence && !TankRules.IsNewerSequence(sequence, m_remoteSequence))
            {
                return false;
            }

            m_hasRemoteSequence = true;
            m_remoteSequence = sequence;
            SetTurretAngles(yaw, pitch);
            ApplyArticulation();
            if (fire && TankRules.CanFire(m_fireCooldown, true, m_body.ImmersionFactor))
            {
                FireMainGun(player, CalculateGunDirection());
            }

            return true;
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
            m_shellBehavior = Project.FindSubsystem<SubsystemTankShellBlockBehavior>(true);
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

            // Read-only collision audit (authored box vs model bounds); mutates nothing.
            VehicleCollisionAudit.LogOnce("Tank", m_body, m_model);
        }

        /// <inheritdoc />
        public override void OnEntityAdded()
        {
            base.OnEntityAdded();
            m_projectiles.ProjectileRemoved += OnProjectileRemoved;
            m_shellBehavior.ShellHit += OnShellHit;
        }

        /// <inheritdoc />
        public override void OnEntityRemoved()
        {
            m_projectiles.ProjectileRemoved -= OnProjectileRemoved;
            m_shellBehavior.ShellHit -= OnShellHit;
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
            float t = MathUtils.Clamp(dt, 0f, .1f);
            m_fireCooldown = MathUtils.Max(0f, m_fireCooldown - t);
            ApplyArticulation();
            ComponentRider rider = m_mount.Rider;
            ComponentPlayer player = rider?.Entity.FindComponent<ComponentPlayer>(false);
            if (player == null || player.ComponentRider != rider || !player.PlayerData.IsMainPlayer)
            {
                // Remote occupied bodies are fed by SCNET's native parent-body
                // interpolation. Do not add a second local ground-drag pass.
                m_body.IsGroundDragEnabled = rider == null;
                return;
            }

            // A tank that existed before activation must still be locked: without an
            // authorized session the local driver gets the throttled password prompt and
            // neither drive integration nor the main-gun fire path below runs.
            if (!VehicleActivationService.EnsureActivated())
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
            // The camera continuously commands the turret. A tap is only the fire trigger:
            // it must not teleport the visual gun or create a click-to-hit trajectory that is
            // unrelated to the barrel. The shell always uses the current articulated angles.
            AimTurret(player.GameWidget.ActiveCamera.ViewDirection, t);
            ApplyArticulation();
            m_networkStateTime += t;
            if (CommonLib.WorkType != WorkType.Local && m_networkStateTime >= 0.1f)
            {
                m_networkStateTime = 0f;
                QueueTankControl(rider, fire: false);
            }

            UpdateDrive(t, rider, input, throttle, terrainGrounded, bodyGrounded);

            // Fire after the drive integration so the shot-frame recoil cannot be
            // overwritten by the normal throttle/traction pass later in this update.
            bool wantsFire = TankRules.CanFire(m_fireCooldown, hitRay.HasValue, m_body.ImmersionFactor);
            if (wantsFire && CommonLib.WorkType == WorkType.Client)
            {
                // Clients never create explosive projectiles. Apply immediate local
                // recoil, then ask the server to validate and create the actual shell.
                Vector3 direction = CalculateGunDirection();
                m_fireCooldown = TankRules.FireCooldown;
                ApplyRecoil(direction);
                QueueTankControl(rider, fire: true);
                m_networkStateTime = 0f;
            }
            else if (wantsFire)
            {
                FireMainGun(player, CalculateGunDirection());
            }
        }

        private void UpdateDrive(
            float dt,
            ComponentRider rider,
            Vector3 input,
            float throttle,
            bool terrainGrounded,
            bool bodyGrounded)
        {
            if (!TankRules.CanApplyDrive(terrainGrounded, bodyGrounded, m_body.ImmersionFactor))
            {
                // Preserve horizontal momentum while airborne. Gravity, collision and
                // native air/water drag remain owned by ComponentBody.
                if (m_body.ImmersionFactor >= .8f) rider.StartDismounting();
                WriteDiagnostics(dt, throttle, terrainGrounded, bodyGrounded, "drive-blocked");
                return;
            }

            Vector3 forward = m_body.Matrix.Forward;
            forward.Y = 0f;
            if (forward.LengthSquared() < .0001f) return;
            forward = Vector3.Normalize(forward);
            Vector3 velocity = m_body.Velocity;
            Vector3 horizontal = new Vector3(velocity.X, 0f, velocity.Z);
            float speed = Vector3.Dot(horizontal, forward);
            speed = TankRules.IntegrateForwardSpeed(speed, throttle, dt);
            Vector3 lateral = horizontal - forward * Vector3.Dot(horizontal, forward);
            lateral = Vector3.Lerp(lateral, Vector3.Zero, MathUtils.Saturate(LateralGrip * dt));
            horizontal = forward * speed + lateral;
            m_body.Velocity = new Vector3(horizontal.X, velocity.Y, horizontal.Z);
            if (MathUtils.Abs(input.X) >= .05f)
            {
                float direction = TankRules.GetSteeringDirection(speed);
                float scale = TankRules.GetSteeringScale(MathUtils.Abs(speed));
                m_body.Rotation *= Quaternion.CreateFromAxisAngle(Vector3.UnitY, -input.X * direction * TurnRate * scale * dt);
            }
            WriteDiagnostics(dt, throttle, terrainGrounded, bodyGrounded, "terrain-drive");
        }

        private void QueueTankControl(ComponentRider rider, bool fire)
        {
            m_localSequence++;
            bool request = CommonLib.WorkType == WorkType.Client;
            CommonLib.Net.QueuePackage(new TankControlPackage(this, rider, m_localSequence, fire, request));
        }

        private void AimTurret(Vector3 direction, float dt)
        {
            direction.Y = MathUtils.Clamp(direction.Y, -.75f, .55f);
            if (direction.LengthSquared() <= .0001f) return;
            direction = Vector3.Normalize(direction);
            Vector3 horizontal = new Vector3(direction.X, 0f, direction.Z);
            if (horizontal.LengthSquared() > .0001f)
            {
                horizontal = Vector3.Normalize(horizontal);
                float targetYaw = MathUtils.Atan2(
                    -Vector3.Dot(horizontal, m_body.Matrix.Right),
                    Vector3.Dot(horizontal, m_body.Matrix.Forward));
                m_turretYaw = TankRules.MoveAngleTowards(
                    m_turretYaw,
                    targetYaw,
                    TankRules.TurretTraverseSpeed * dt);
            }
            float targetPitch = MathUtils.Clamp(
                MathUtils.Atan2(direction.Y, new Vector2(direction.X, direction.Z).Length()),
                TankRules.MinimumGunPitch,
                TankRules.MaximumGunPitch);
            m_gunPitch = TankRules.MoveTowards(
                m_gunPitch,
                targetPitch,
                TankRules.GunElevationSpeed * dt);
        }

        private void ApplyArticulation()
        {
            if (m_model.Model == null) return;
            if (m_turretBone == null) m_turretBone = m_model.Model.FindBone("TankTurret", false);
            if (m_gunBone == null) m_gunBone = m_model.Model.FindBone("TankGun", false);
            if (m_turretBone != null) m_model.SetBoneTransform(m_turretBone.Index, Matrix.CreateRotationY(m_turretYaw));
            if (m_gunBone != null) m_model.SetBoneTransform(m_gunBone.Index, Matrix.CreateRotationX(m_gunPitch));
        }

        private Vector3 CalculateGunDirection()
        {
            Matrix hull = m_body.Matrix;
            Vector3 horizontal = hull.Forward * MathUtils.Cos(m_turretYaw)
                - hull.Right * MathUtils.Sin(m_turretYaw);
            Vector3 direction = horizontal * MathUtils.Cos(m_gunPitch)
                + hull.Up * MathUtils.Sin(m_gunPitch);
            return Vector3.Normalize(direction);
        }

        private Vector3 CalculateSafeMuzzlePosition(Vector3 direction)
        {
            BoundingBox box = m_body.BoundingBox;
            Vector3 origin = m_body.Position + m_body.Matrix.Up * TankRules.GunPivotHeight;
            // ComponentBody's collision box is axis-aligned even when the hull yaws. Starting
            // at its center line and calculating the first positive slab exit is therefore
            // safer than using a fixed barrel distance, especially for diagonal/downward aim.
            Vector3 center = box.Center();
            Vector3 halfSize = (box.Max - box.Min) * .5f;
            float exitDistance = TankRules.GetBoxExitDistance(
                origin.X - center.X, origin.Y - center.Y, origin.Z - center.Z,
                direction.X, direction.Y, direction.Z,
                halfSize.X, halfSize.Y, halfSize.Z);
            if (exitDistance == float.MaxValue || exitDistance < 0f) exitDistance = 0f;
            return origin + direction * (exitDistance + TankRules.MuzzleClearance);
        }

        private void FireMainGun(ComponentPlayer player, Vector3 requestedDirection)
        {
            if (requestedDirection.LengthSquared() <= .0001f) return;
            Vector3 direction = Vector3.Normalize(requestedDirection);
            Vector3 muzzle = CalculateSafeMuzzlePosition(direction);
            int shellValue = Terrain.MakeBlockValue(TankShellBlock.Index, 0, 0);
            ComponentCreature owner = player.Entity.FindComponent<ComponentCreature>(true);
            // Native FireProjectile owner clearance is designed for a projectile fired from a
            // creature's hands. Since the rider is inside this much larger vehicle, that code
            // moves an already-safe muzzle point back to the rider's box and into the tank.
            // Create without an owner to preserve the muzzle, then restore damage attribution.
            Projectile shell = m_projectiles.FireProjectile(
                shellValue,
                muzzle,
                direction * TankRules.ShellSpeed,
                Vector3.Zero,
                null);
            if (shell == null) return;
            shell.Owner = owner;
            shell.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
            m_shells[shell] = new ShellState
            {
                PlayerData = player.PlayerData
            };
            m_fireCooldown = TankRules.FireCooldown;
            m_audio.PlaySound("Audio/MusketFire", 1f, 0f, muzzle, 64f, true);
            ApplyRecoil(direction);
        }

        private void ApplyRecoil(Vector3 direction)
        {
            Vector3 recoil = new Vector3(direction.X, 0f, direction.Z);
            if (recoil.LengthSquared() > .0001f)
                m_body.Velocity -= Vector3.Normalize(recoil) * TankRules.RecoilSpeed;
        }

        private void OnProjectileRemoved(Projectile projectile)
        {
            ShellState state;
            if (!m_shells.TryGetValue(projectile, out state)) return;
            m_shells.Remove(projectile);
            // Lifetime expiry, chunk unload and other non-impact removals are not hits.
            // The first-impact callback is the only path that creates an explosion.
        }

        private void OnShellHit(Projectile projectile, CellFace? cellFace, ComponentBody hitBody)
        {
            ShellState state;
            if (!m_shells.TryGetValue(projectile, out state)) return;
            m_shells.Remove(projectile);
            projectile.ToRemove = true;
            Vector3 impactPosition = cellFace.HasValue
                ? new Vector3(cellFace.Value.X + .5f, cellFace.Value.Y + .5f, cellFace.Value.Z + .5f)
                : projectile.Position;
            ExplodeShell(state, impactPosition);
        }

        private void ExplodeShell(ShellState state, Vector3 impactPosition)
        {
            int explosionX = Terrain.ToCell(impactPosition.X);
            int explosionY = Terrain.ToCell(impactPosition.Y);
            int explosionZ = Terrain.ToCell(impactPosition.Z);
            m_explosions.AddExplosion(
                explosionX,
                explosionY,
                explosionZ,
                TankRules.ShellExplosionPressure,
                false,
                false,
                state.PlayerData);
        }

        private void WriteDiagnostics(float dt, float throttle, bool terrainGrounded, bool bodyGrounded, string state)
        {
            m_diagnosticTime += dt;
            if (m_diagnosticTime < DiagnosticPeriod) return;
            m_diagnosticTime = 0f;
            Vector3 velocity = m_body.Velocity;
            Log.Information(string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "SCIE-Port Tank: state={0}, speed={1:0.00}, throttle={2:0.00}, terrainGrounded={3}, bodyGrounded={4}, immersion={5:0.00}, nativeGroundDrag={6}, turretYaw={7:0.00}, gunPitch={8:0.00}",
                state,
                new Vector2(velocity.X, velocity.Z).Length(),
                throttle,
                terrainGrounded,
                bodyGrounded,
                m_body.ImmersionFactor,
                m_body.IsGroundDragEnabled,
                m_turretYaw,
                m_gunPitch));
        }
    }
}

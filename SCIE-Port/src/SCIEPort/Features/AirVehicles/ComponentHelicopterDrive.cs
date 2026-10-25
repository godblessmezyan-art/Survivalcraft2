using Engine;
using Engine.Audio;
using Engine.Graphics;
using Game;
using Game.NetWork;
using GameEntitySystem;
using SCIEPort.Features.Vehicles;
using TemplatesDatabase;

namespace SCIEPort.Features.AirVehicles
{
    /// <summary>Driver-only bounded helicopter flight with automatic unpiloted landing.</summary>
    public sealed class ComponentHelicopterDrive : Component, IUpdateable
    {
        private ComponentBody m_body;
        private ComponentDamage m_damage;
        private ComponentModel m_model;
        private ComponentTwoSeatManager m_seats;
        private SubsystemAudio m_audio;
        private Sound m_rotorSound;
        private ModelBone m_visualRootBone;
        private ModelBone m_mainRotorBone;
        private ModelBone m_tailRotorBone;
        private ModelBone m_mainRotorBlurBone;
        private ModelBone m_tailRotorBlurBone;
        private bool m_engineRunning;
        private HelicopterRotorState m_rotorState;
        private float m_rotorSpeed;
        private float m_mainRotorAngle;
        private float m_tailRotorAngle;
        private float m_visualPitch;
        private float m_visualRoll;
        private float m_previousYaw;
        private bool m_hasPreviousYaw;

        private float m_spoolUpTime;
        private float m_spoolDownTime;
        private float m_gravity;
        private float m_verticalResponse;
        private float m_ascentSpeed;
        private float m_descentSpeed;
        private float m_autoLandSpeed;
        private float m_horizontalAcceleration;
        private float m_brakeAcceleration;
        private float m_horizontalDrag;
        private float m_lateralGrip;
        private float m_maximumForwardSpeed;
        private float m_maximumReverseSpeed;
        private float m_turnRate;
        private float m_maximumWorldY;
        private float m_visualPitchLimit;
        private float m_visualRollLimit;
        private float m_visualResponse;
        private float m_visualLateralReferenceSpeed;
        private float m_mainRotorAngularSpeed;
        private float m_tailRotorSpeedMultiplier;
        private float m_rotorBlurThreshold;
        private float m_soundMinDistance;
        private float m_soundRolloffFactor;

        /// <inheritdoc />
        public UpdateOrder UpdateOrder => UpdateOrder.Default;

        /// <summary>Current presentation state derived from the existing engine/rotor flight state.</summary>
        public HelicopterRotorState RotorState => m_rotorState;

        /// <inheritdoc />
        public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
        {
            base.Load(valuesDictionary, idToEntityMap);
            m_body = Entity.FindComponent<ComponentBody>(throwOnError: true);
            m_damage = Entity.FindComponent<ComponentDamage>(throwOnError: true);
            m_model = Entity.FindComponent<ComponentModel>(throwOnError: true);
            m_seats = Entity.FindComponent<ComponentTwoSeatManager>(throwOnError: true);
            m_audio = Project.FindSubsystem<SubsystemAudio>(throwOnError: true);

            m_spoolUpTime = valuesDictionary.GetValue("SpoolUpTime", 2.5f);
            m_spoolDownTime = valuesDictionary.GetValue("SpoolDownTime", 3.5f);
            m_gravity = valuesDictionary.GetValue("Gravity", 9.8f);
            m_verticalResponse = valuesDictionary.GetValue("VerticalResponse", 2.8f);
            m_ascentSpeed = valuesDictionary.GetValue("MaximumAscentSpeed", 4.5f);
            m_descentSpeed = valuesDictionary.GetValue("MaximumDescentSpeed", 3.5f);
            m_autoLandSpeed = valuesDictionary.GetValue("AutoLandSpeed", 1.8f);
            m_horizontalAcceleration = valuesDictionary.GetValue("HorizontalAcceleration", 5.5f);
            m_brakeAcceleration = valuesDictionary.GetValue("BrakeAcceleration", 8f);
            m_horizontalDrag = valuesDictionary.GetValue("HorizontalDrag", 1.5f);
            m_lateralGrip = valuesDictionary.GetValue("LateralGrip", 2.5f);
            m_maximumForwardSpeed = valuesDictionary.GetValue("MaximumForwardSpeed", 12f);
            m_maximumReverseSpeed = valuesDictionary.GetValue("MaximumReverseSpeed", 5f);
            m_turnRate = valuesDictionary.GetValue("TurnRate", 1.2f);
            m_maximumWorldY = valuesDictionary.GetValue("MaximumWorldY", 240f);
            m_visualPitchLimit = valuesDictionary.GetValue("VisualPitchLimit", 0.122173f);
            m_visualRollLimit = valuesDictionary.GetValue("VisualRollLimit", 0.15708f);
            m_visualResponse = valuesDictionary.GetValue("VisualResponse", 4f);
            m_visualLateralReferenceSpeed = valuesDictionary.GetValue("VisualLateralReferenceSpeed", 4f);
            m_mainRotorAngularSpeed = valuesDictionary.GetValue("MainRotorAngularSpeed", 30f);
            m_tailRotorSpeedMultiplier = valuesDictionary.GetValue("TailRotorSpeedMultiplier", 3.2f);
            m_rotorBlurThreshold = valuesDictionary.GetValue("RotorBlurThreshold", 0.74f);
            m_soundMinDistance = valuesDictionary.GetValue("SoundMinDistance", 4f);
            m_soundRolloffFactor = valuesDictionary.GetValue("SoundRolloffFactor", 0.08f);
            m_engineRunning = valuesDictionary.GetValue("EngineRunning", false);
            m_rotorSpeed = valuesDictionary.GetValue("RotorSpeed", 0f);
            m_rotorState = HelicopterFlightRules.GetRotorState(m_engineRunning, m_rotorSpeed);

            m_body.IsGravityEnabled = false;
            m_body.IsGroundDragEnabled = false;
            m_body.IsSmoothRiseEnabled = false;
            m_model.TextureOverride = HelicopterBlock.Texture;
            m_model.DiffuseColor = Vector3.One;
            m_model.EmissionColor = new Vector4(0.045f, 0.045f, 0.05f, 0f);

            m_rotorSound = m_audio.CreateSound("Audio/SCIEPortHelicopterLoop");
            m_rotorSound.IsLooped = true;
            m_rotorSound.DisposeOnStop = false;
            m_rotorSound.Volume = 0f;
        }

        /// <inheritdoc />
        public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
        {
            base.Save(valuesDictionary, entityToIdMap);
            if (m_engineRunning) valuesDictionary.SetValue("EngineRunning", true);
            if (m_rotorSpeed > 0f) valuesDictionary.SetValue("RotorSpeed", m_rotorSpeed);
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            if (m_rotorSound != null)
            {
                m_rotorSound.Stop();
                m_rotorSound.Dispose();
                m_rotorSound = null;
            }

            base.Dispose();
        }

        /// <inheritdoc />
        public void Update(float dt)
        {
            float t = MathUtils.Clamp(dt, 0f, 0.1f);
            bool grounded = m_body.StandingOnValue.HasValue || m_body.StandingOnBody != null;
            ComponentRider driver = m_seats.Driver;
            ComponentPlayer player = driver?.Entity.FindComponent<ComponentPlayer>(throwOnError: false);
            bool localPilot = player != null && player.ComponentRider == driver && player.PlayerData.IsMainPlayer;
            bool authorizedPilot = localPilot && VehicleActivationService.EnsureActivated();

            if (authorizedPilot)
            {
                // Touch-friendly automatic start: entering the driver seat starts spool-up.
                m_engineRunning = true;
            }
            else if (driver == null)
            {
                // Keep enough rotor power for a bounded unattended descent, then stop on contact.
                m_engineRunning = !grounded;
            }
            else if (!localPilot)
            {
                // Remote replicas animate the rotors but do not run a competing physics loop.
                m_engineRunning = true;
            }

            m_rotorSpeed = HelicopterFlightRules.UpdateRotorSpeed(
                m_rotorSpeed, m_engineRunning, m_spoolUpTime, m_spoolDownTime, t);
            m_rotorState = HelicopterFlightRules.GetRotorState(m_engineRunning, m_rotorSpeed);

            bool authorityAutoLanding = driver == null && CommonLib.WorkType != WorkType.Client;
            if (authorizedPilot || authorityAutoLanding)
            {
                SimulateFlight(t, grounded, authorizedPilot);
            }

            UpdatePresentation(t, grounded);
        }

        private void SimulateFlight(float t, bool grounded, bool authorizedPilot)
        {
            bool ascend = authorizedPilot && m_seats.DriverJump;
            bool descend = authorizedPilot && m_seats.DriverSneakMove.LengthSquared() > 0.0025f;
            float targetVertical = HelicopterFlightRules.GetVerticalTarget(
                ascend,
                descend,
                authorizedPilot,
                m_ascentSpeed,
                m_descentSpeed,
                m_autoLandSpeed);
            if (HelicopterFlightRules.IsAtAltitudeLimit(m_body.Position.Y, m_maximumWorldY) && targetVertical > 0f)
            {
                targetVertical = 0f;
            }

            Vector3 velocity = m_body.Velocity;
            float vertical = HelicopterFlightRules.IntegrateVerticalSpeed(
                velocity.Y,
                targetVertical,
                m_rotorSpeed,
                m_gravity,
                m_verticalResponse,
                m_ascentSpeed,
                m_descentSpeed,
                t);
            if (grounded && vertical < 0f) vertical = 0f;

            Vector3 forward = m_body.Matrix.Forward;
            forward.Y = 0f;
            if (forward.LengthSquared() > 0.0001f) forward = Vector3.Normalize(forward);
            Vector3 horizontal = new Vector3(velocity.X, 0f, velocity.Z);
            float throttle = authorizedPilot && m_rotorSpeed >= 0.65f
                ? MathUtils.Clamp(m_seats.DriverMove.Z, -1f, 1f)
                : 0f;
            float forwardSpeed = Vector3.Dot(horizontal, forward);
            forwardSpeed = HelicopterFlightRules.IntegrateForwardSpeed(
                forwardSpeed,
                throttle,
                m_horizontalAcceleration,
                m_brakeAcceleration,
                m_horizontalDrag,
                m_maximumForwardSpeed,
                m_maximumReverseSpeed,
                t);
            Vector3 lateral = horizontal - forward * Vector3.Dot(horizontal, forward);
            lateral = Vector3.Lerp(lateral, Vector3.Zero, MathUtils.Saturate(m_lateralGrip * t));
            horizontal = forward * forwardSpeed + lateral;
            m_body.Velocity = new Vector3(horizontal.X, vertical, horizontal.Z);

            float turn = authorizedPilot ? MathUtils.Clamp(m_seats.DriverMove.X, -1f, 1f) : 0f;
            if (m_rotorSpeed >= 0.65f && MathUtils.Abs(turn) >= 0.05f)
            {
                m_body.Rotation *= Quaternion.CreateFromAxisAngle(Vector3.UnitY, -turn * m_turnRate * t);
            }

            if (CommonLib.WorkType != WorkType.Client && m_body.ImmersionFactor > 0.9f)
            {
                m_damage.Damage(0.02f * t);
                m_seats.Driver?.StartDismounting();
                m_seats.Passenger?.StartDismounting();
            }
        }

        private void UpdatePresentation(float dt, bool grounded)
        {
            UpdateVisualAttitude(dt, grounded);
            AnimateModel(dt);
            UpdateSound();
        }

        private void UpdateVisualAttitude(float dt, bool grounded)
        {
            Vector3 forward = m_body.Matrix.Forward;
            forward.Y = 0f;
            if (forward.LengthSquared() < 0.0001f) forward = new Vector3(0f, 0f, -1f);
            else forward = Vector3.Normalize(forward);
            Vector3 right = new Vector3(-forward.Z, 0f, forward.X);
            Vector3 horizontal = new Vector3(m_body.Velocity.X, 0f, m_body.Velocity.Z);
            float forwardSpeed = Vector3.Dot(horizontal, forward);
            float lateralSpeed = Vector3.Dot(horizontal, right);
            float yaw = MathUtils.Atan2(-forward.X, -forward.Z);
            float yawRate = 0f;
            if (m_hasPreviousYaw && dt > 0.0001f)
            {
                yawRate = HelicopterFlightRules.ShortestAngleDelta(m_previousYaw, yaw) / dt;
            }

            m_previousYaw = yaw;
            m_hasPreviousYaw = true;
            float targetPitch = grounded ? 0f : HelicopterFlightRules.GetVisualPitch(
                forwardSpeed, m_maximumForwardSpeed, m_maximumReverseSpeed, m_visualPitchLimit);
            float targetRoll = grounded ? 0f : HelicopterFlightRules.GetVisualRoll(
                lateralSpeed, m_visualLateralReferenceSpeed, yawRate, m_turnRate, m_visualRollLimit);
            m_visualPitch = HelicopterFlightRules.SmoothVisualAngle(m_visualPitch, targetPitch, m_visualResponse, dt);
            m_visualRoll = HelicopterFlightRules.SmoothVisualAngle(m_visualRoll, targetRoll, m_visualResponse, dt);
        }

        private void AnimateModel(float dt)
        {
            if (m_model.Model == null) return;
            if (m_visualRootBone == null) m_visualRootBone = m_model.Model.FindBone("VisualRoot", throwIfNotFound: false);
            if (m_mainRotorBone == null) m_mainRotorBone = m_model.Model.FindBone("MainRotor", throwIfNotFound: false);
            if (m_tailRotorBone == null) m_tailRotorBone = m_model.Model.FindBone("TailRotor", throwIfNotFound: false);
            if (m_mainRotorBlurBone == null) m_mainRotorBlurBone = m_model.Model.FindBone("MainRotorBlur", throwIfNotFound: false);
            if (m_tailRotorBlurBone == null) m_tailRotorBlurBone = m_model.Model.FindBone("TailRotorBlur", throwIfNotFound: false);

            float angularSpeed = m_mainRotorAngularSpeed * m_rotorSpeed;
            m_mainRotorAngle = HelicopterFlightRules.WrapAngle(m_mainRotorAngle + angularSpeed * dt);
            m_tailRotorAngle = HelicopterFlightRules.WrapAngle(
                m_tailRotorAngle + angularSpeed * m_tailRotorSpeedMultiplier * dt);
            float blur = HelicopterFlightRules.GetRotorBlurBlend(m_rotorSpeed, m_rotorBlurThreshold);
            float solidScale = 1f - blur;

            if (m_visualRootBone != null)
            {
                m_model.SetBoneTransform(m_visualRootBone.Index,
                    Matrix.CreateRotationX(m_visualPitch) * Matrix.CreateRotationZ(m_visualRoll));
            }
            if (m_mainRotorBone != null)
            {
                m_model.SetBoneTransform(m_mainRotorBone.Index,
                    Matrix.CreateScale(solidScale) * Matrix.CreateRotationY(m_mainRotorAngle));
            }
            if (m_tailRotorBone != null)
            {
                m_model.SetBoneTransform(m_tailRotorBone.Index,
                    Matrix.CreateScale(solidScale) * Matrix.CreateRotationX(m_tailRotorAngle));
            }
            if (m_mainRotorBlurBone != null)
            {
                m_model.SetBoneTransform(m_mainRotorBlurBone.Index,
                    Matrix.CreateScale(blur) * Matrix.CreateRotationY(m_mainRotorAngle));
            }
            if (m_tailRotorBlurBone != null)
            {
                m_model.SetBoneTransform(m_tailRotorBlurBone.Index,
                    Matrix.CreateScale(blur) * Matrix.CreateRotationX(m_tailRotorAngle));
            }
        }

        private void UpdateSound()
        {
            if (m_rotorSound == null) return;
            if (m_rotorState == HelicopterRotorState.Stopped)
            {
                m_rotorSound.Volume = 0f;
                if (m_rotorSound.State == SoundState.Playing) m_rotorSound.Stop();
                return;
            }

            if (m_rotorSound.State != SoundState.Playing) m_rotorSound.Play();
            float distance = m_audio.CalculateListenerDistance(m_body.Position);
            float attenuation = m_audio.CalculateVolume(distance, m_soundMinDistance, m_soundRolloffFactor);
            Vector3 horizontal = new Vector3(m_body.Velocity.X, 0f, m_body.Velocity.Z);
            float flightLoad = MathUtils.Saturate(horizontal.Length() / MathUtils.Max(m_maximumForwardSpeed, 0.01f));
            m_rotorSound.Volume = MathUtils.Clamp(
                attenuation * (0.08f + 0.54f * m_rotorSpeed + 0.12f * flightLoad), 0f, 0.74f);
            m_rotorSound.Pitch = MathUtils.Clamp(-0.42f + 0.56f * m_rotorSpeed + 0.08f * flightLoad, -1f, 1f);
        }
    }
}

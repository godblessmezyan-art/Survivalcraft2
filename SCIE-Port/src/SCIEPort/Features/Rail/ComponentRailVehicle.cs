using Engine;
using Engine.Graphics;
using Game;
using Game.NetWork;
using GameEntitySystem;
using SCIEPort.Features.Vehicles;
using TemplatesDatabase;

namespace SCIEPort.Features.Rail
{
    /// <summary>Single-player rail guidance and touch driving for the train preview.</summary>
    public sealed class ComponentRailVehicle : Component, IUpdateable
    {
        private const float Acceleration = 8f;
        private const float MaximumSpeed = 12f;
        private static readonly Vector3[] ForwardVectors =
        {
            new Vector3(0f, 0f, -1f), new Vector3(-1f, 0f, 0f),
            new Vector3(0f, 0f, 1f), new Vector3(1f, 0f, 0f)
        };

        private ComponentBody m_body;
        private ComponentMount m_mount;
        private SubsystemTerrain m_terrain;
        private bool m_powered;
        private int m_direction;
        private Quaternion m_targetRotation;

        /// <inheritdoc />
        public UpdateOrder UpdateOrder => UpdateOrder.Default;
        /// <summary>Current cardinal direction, from zero through three.</summary>
        public int Direction => m_direction;

        /// <inheritdoc />
        public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
        {
            base.Load(valuesDictionary, idToEntityMap);
            m_body = Entity.FindComponent<ComponentBody>(true);
            m_mount = Entity.FindComponent<ComponentMount>(true);
            m_terrain = Project.FindSubsystem<SubsystemTerrain>(true);
            m_powered = valuesDictionary.GetValue("Powered", false);
            ComponentModel model = Entity.FindComponent<ComponentModel>(true);
            model.TextureOverride = ContentManager.Get<Texture2D>(
                m_powered ? "Textures/Train" : "Textures/Creatures/Jaguar");
            model.DiffuseColor = Vector3.One;
            model.EmissionColor = new Vector4(0.12f, 0.12f, 0.12f, 0f);
            SetDirection(valuesDictionary.GetValue("Direction", 0));
        }

        /// <inheritdoc />
        public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
        {
            base.Save(valuesDictionary, entityToIdMap);
            if (m_direction != 0) valuesDictionary.SetValue("Direction", m_direction);
        }

        /// <summary>Sets the initial rail direction and body rotation.</summary>
        public void SetDirection(int direction)
        {
            m_direction = direction & 3;
            m_targetRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathUtils.PI * .5f * m_direction);
            if (m_body != null) m_body.Rotation = m_targetRotation;
        }

        /// <inheritdoc />
        public void Update(float dt)
        {
            ComponentRider rider = m_mount.Rider;
            ComponentPlayer player = rider?.Entity.FindComponent<ComponentPlayer>(false);
            bool localDriver = player != null && player.ComponentRider == rider && player.PlayerData.IsMainPlayer
                && VehicleActivationService.EnsureActivated();
            bool simulateUnoccupiedAuthority = rider == null && CommonLib.WorkType != WorkType.Client;
            if (!localDriver && !simulateUnoccupiedAuthority) return;
            int railType;
            if (!TryGetRail(out railType)) return;

            float boundedDt = MathUtils.Clamp(dt, 0f, .1f);
            AlignToTrack();
            if (!SimulateRail(railType))
            {
                m_body.Velocity = new Vector3(0f, m_body.Velocity.Y, 0f);
                return;
            }

            float throttle = 0f;
            if (localDriver)
            {
                throttle = MathUtils.Clamp(player.ComponentInput.PlayerInput.Move.Z, -1f, 1f);
            }

            // The carriage remains independently testable until coupling has runtime evidence.
            float driveFactor = m_powered ? 1f : .55f;
            if (MathUtils.Abs(throttle) > .05f)
            {
                m_body.Velocity += ForwardVectors[m_direction] * (Acceleration * driveFactor * throttle * boundedDt);
            }

            Vector3 horizontal = new Vector3(m_body.Velocity.X, 0f, m_body.Velocity.Z);
            if (horizontal.LengthSquared() > MaximumSpeed * MaximumSpeed)
            {
                horizontal = MaximumSpeed * Vector3.Normalize(horizontal);
                m_body.Velocity = new Vector3(horizontal.X, m_body.Velocity.Y, horizontal.Z);
            }
            m_body.Rotation = Quaternion.Slerp(m_body.Rotation, m_targetRotation, .2f);
        }

        private bool TryGetRail(out int type)
        {
            int x = Terrain.ToCell(m_body.Position.X);
            int z = Terrain.ToCell(m_body.Position.Z);
            int topY = Terrain.ToCell(m_body.Position.Y);
            for (int offset = 0; offset <= 2; offset++)
            {
                int value = m_terrain.Terrain.GetCellValue(x, topY - offset, z);
                if (Terrain.ExtractContents(value) == RailBlock.Index)
                {
                    type = RailBlock.GetRailType(Terrain.ExtractData(value));
                    return true;
                }
            }
            type = 0;
            return false;
        }

        private void AlignToTrack()
        {
            Vector3 p = m_body.Position;
            if ((m_direction & 1) == 0) p.X = MathUtils.Floor(p.X) + .5f;
            else p.Z = MathUtils.Floor(p.Z) + .5f;
            m_body.Position = p;
        }

        private bool SimulateRail(int railType)
        {
            if (RailBlock.IsCorner(railType))
            {
                if (OffsetOnDirection(m_body.Position, m_direction) > .5f) Turn(railType);
                return true;
            }
            if (RailBlock.IsDirectionX(railType) ^ !RailBlock.IsDirectionX(m_direction))
            {
                if (railType > 5)
                {
                    Quaternion incline = Quaternion.CreateFromAxisAngle(Vector3.UnitX, (railType - 6 != m_direction ? 1f : -1f) * MathUtils.PI * .25f);
                    m_targetRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathUtils.PI * .5f * m_direction) * incline;
                }
                else m_targetRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathUtils.PI * .5f * m_direction);
                return true;
            }
            return false;
        }

        private void Turn(int turnType)
        {
            int next = m_direction;
            if (m_direction == turnType) next = (m_direction - 1) & 3;
            else if (((m_direction - 1) & 3) == turnType) next = (m_direction + 1) & 3;
            if (next == m_direction) return;
            float speed = MathUtils.Abs(m_body.Velocity.X + m_body.Velocity.Z);
            SetDirection(next);
            m_body.Velocity = speed * ForwardVectors[m_direction];
            m_body.Position = Vector3.Floor(m_body.Position) + new Vector3(.5f, 0f, .5f);
        }

        private static float OffsetOnDirection(Vector3 value, int direction)
        {
            float offset = (direction & 1) == 0 ? value.Z - MathUtils.Floor(value.Z) : value.X - MathUtils.Floor(value.X);
            return (direction & 2) == 0 ? 1f - offset : offset;
        }
    }
}

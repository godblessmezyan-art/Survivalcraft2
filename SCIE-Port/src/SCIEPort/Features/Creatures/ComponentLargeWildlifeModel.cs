#pragma warning disable CS1591
using Engine;
using Engine.Graphics;
using Game;
using GameEntitySystem;
using TemplatesDatabase;

namespace SCIEPort.Features.Creatures
{
    public sealed class ComponentLargeWildlifeModel : ComponentCreatureModel, IDrawable
    {
        private static readonly int[] RayShadowOrder = { 196 };
        private static readonly int[] NoShadowOrder = new int[0];
        private readonly int[] fins = new int[2], tips = new int[2], legs = new int[4];
        private SubsystemTerrain terrain;
        private SubsystemShadows shadows;
        private Vector3 previous;
        private float phase, speed, rise, wingBeat, tuck, lunge, settle, bank, previousYaw;
        private int head, tail, tailTip;
        private bool ray, sampled;
        private byte pose;
        private double poseExpires;

        public int[] DrawOrders => ray ? RayShadowOrder : NoShadowOrder;

        public override void Load(ValuesDictionary values, IdToEntityMap map)
        {
            base.Load(values, map);
            terrain = Project.FindSubsystem<SubsystemTerrain>(true);
            shadows = Project.FindSubsystem<SubsystemShadows>(true);
        }

        public override void SetModel(Model model)
        {
            base.SetModel(model);
            if (model == null) return;
            ray = model.FindBone("WingL", false) != null;
            for (int i = 0; i < 2; i++)
            {
                string label = i == 0 ? "L" : "R";
                if (ray)
                {
                    fins[i] = model.FindBone("Wing" + label).Index;
                    tips[i] = model.FindBone("WingTip" + label).Index;
                }
                else
                {
                    legs[i * 2] = model.FindBone("Leg" + label + "1").Index;
                    legs[i * 2 + 1] = model.FindBone("Leg" + label + "2").Index;
                }
            }
            if (ray)
            {
                tail = model.FindBone("Tail1").Index;
                tailTip = model.FindBone("Tail2").Index;
            }
            else
            {
                head = model.FindBone("Head").Index;
                tail = model.FindBone("Tail").Index;
            }
        }

        internal void SetTurtlePose(byte next)
        {
            if (ray || next > 2) return;
            pose = next;
            poseExpires = m_subsystemTime.GameTime + (next == 2 ? .55 : 3.25);
        }

        public override void Update(float dt)
        {
            base.Update(dt);
            ComponentBody body = m_componentCreature.ComponentBody;
            Vector3 current = body.Position;
            float yaw = body.Rotation.ToYawPitchRoll().X;
            float smooth = MathUtils.Saturate(7f * dt);
            if (sampled && dt > 0f)
            {
                Vector3 delta = current - previous;
                float traveled = delta.XZ.Length();
                if (traveled < 2f && MathUtils.Abs(delta.Y) < 2f)
                {
                    speed = MathUtils.Lerp(speed, MathUtils.Min(traveled / dt, 7f), smooth);
                    rise = MathUtils.Lerp(rise, MathUtils.Clamp(delta.Y / dt, -3f, 3f), smooth);
                    float turn = MathUtils.Sin(yaw - previousYaw) / dt;
                    bank = MathUtils.Lerp(bank, MathUtils.Clamp(-turn * .20f, -.39f, .39f), smooth);
                    phase = MathUtils.Remainder(phase + traveled * 3.9f, MathUtils.PI * 2f);
                }
            }
            previous = current;
            previousYaw = yaw;
            sampled = true;
            if (ray)
            {
                bool resting = body.StandingOnValue.HasValue;
                if (!resting && speed < .35f && MathUtils.Abs(rise) < .3f)
                {
                    int x = Terrain.ToCell(current.X), z = Terrain.ToCell(current.Z);
                    resting = LargeWildlifeHabitat.Ready(terrain.Terrain, x, z)
                        && current.Y - terrain.Terrain.GetTopHeight(x, z) < 1.18f;
                }
                settle = MathUtils.Lerp(settle, resting ? 1f : 0f, smooth);
                wingBeat = MathUtils.Remainder(wingBeat + dt * MathUtils.Lerp(2.1f, 5.1f, MathUtils.Saturate(rise * .6f)),
                    MathUtils.PI * 2f);
            }
            else
            {
                if (m_subsystemTime.GameTime > poseExpires) pose = 0;
                tuck = MathUtils.Lerp(tuck, pose == 1 ? 1f : 0f, MathUtils.Saturate(10f * dt));
                lunge = MathUtils.Lerp(lunge, pose == 2 ? 1f : 0f, MathUtils.Saturate(12f * dt));
            }
        }

        public override void Animate()
        {
            base.Animate();
            if (Model == null) return;
            ComponentBody body = m_componentCreature.ComponentBody;
            Vector3 position = body.Position;
            float yaw = body.Rotation.ToYawPitchRoll().X;
            float live = 1f - DeathPhase;
            if (ray)
            {
                float airborne = live * (1f - settle);
                position.Y -= .17f * settle * live;
                float flutter = MathUtils.Sin(wingBeat) * MathUtils.Lerp(.10f, .32f, MathUtils.Saturate(rise * .55f));
                SetBoneTransform(Model.RootBone.Index,
                    Matrix.CreateFromYawPitchRoll(yaw, airborne * MathUtils.Clamp(rise * .07f, -.20f, .21f),
                        airborne * bank + DeathPhase * .52f) * Matrix.CreateTranslation(position));
                for (int i = 0; i < 2; i++)
                {
                    float side = i == 0 ? -1f : 1f;
                    SetBoneTransform(fins[i], Matrix.CreateRotationZ(side * (airborne * flutter + .18f * settle * live)));
                    SetBoneTransform(tips[i], Matrix.CreateRotationZ(side * (airborne * MathUtils.Sin(wingBeat - .65f) * .125f
                        + .22f * settle * live)));
                }
                SetBoneTransform(tail, Matrix.CreateRotationY(airborne * MathUtils.Sin(wingBeat * .58f) * .11f));
                SetBoneTransform(tailTip, Matrix.CreateRotationY(airborne * MathUtils.Sin(wingBeat * .58f - .65f) * .15f));
                return;
            }

            float gait = MathUtils.Saturate(speed / .9f) * live * (1f - tuck);
            position.Y += .026f * gait * MathUtils.Abs(MathUtils.Sin(phase)) + .048f * tuck * live;
            SetBoneTransform(Model.RootBone.Index,
                Matrix.CreateFromYawPitchRoll(yaw, -.03f * lunge, .065f * tuck * live + .12f * DeathPhase)
                * Matrix.CreateTranslation(position));
            SetBoneTransform(head, Matrix.CreateRotationX(-.13f * tuck + .15f * lunge)
                * Matrix.CreateTranslation(0f, -.08f * tuck, .60f * tuck - .23f * lunge));
            for (int i = 0; i < legs.Length; i++)
            {
                float side = i < 2 ? -1f : 1f;
                float step = gait * MathUtils.Sin(phase + (i == 1 || i == 2 ? MathUtils.PI : 0f));
                SetBoneTransform(legs[i], Matrix.CreateRotationX(step * .36f)
                    * Matrix.CreateTranslation(-side * .17f * tuck, .025f * tuck, 0f));
            }
            SetBoneTransform(tail, Matrix.CreateRotationY(gait * MathUtils.Sin(phase) * .18f));
        }

        public void Draw(Camera camera, int drawOrder)
        {
            if (!ray || !SettingsManager.ObjectsShadowsEnabled || m_componentCreature.ComponentHealth.Health <= 0f) return;
            ComponentBody body = m_componentCreature.ComponentBody;
            Vector3 center = body.Position;
            if ((camera.ViewPosition - center).XZ.LengthSquared() > 30f * 30f) return;
            Vector3 right = body.Matrix.Right;
            QueueGroundShadow(camera, center, 3.05f, .78f);
            QueueGroundShadow(camera, center + right * 1.84f, 3.35f, .54f);
            QueueGroundShadow(camera, center - right * 1.84f, 3.35f, .54f);
        }

        private void QueueGroundShadow(Camera camera, Vector3 center, float diameter, float strength)
        {
            int x = Terrain.ToCell(center.X), z = Terrain.ToCell(center.Z);
            if (!LargeWildlifeHabitat.Ready(terrain.Terrain, x, z)) return;
            float floor = terrain.Terrain.GetTopHeight(x, z) + 1f;
            float altitude = center.Y - floor;
            if (altitude < -.2f || altitude > 20f) return;
            center.Y = floor + .10f;
            shadows.QueueShadow(camera, center, diameter, strength * MathUtils.Saturate(1f - altitude / 22f));
        }
    }
}

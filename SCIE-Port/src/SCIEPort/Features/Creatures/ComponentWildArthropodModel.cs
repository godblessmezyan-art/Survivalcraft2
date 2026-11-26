#pragma warning disable CS1591
using Engine;
using Engine.Graphics;
using Game;
using GameEntitySystem;
using TemplatesDatabase;

namespace SCIEPort.Features.Creatures
{
    public sealed class ComponentWildArthropodModel : ComponentCreatureModel
    {
        private readonly int[] legs = new int[8];
        private readonly int[] claws = new int[2];
        private readonly int[] tail = new int[3];
        private SubsystemTimeOfDay timeOfDay;
        private SubsystemAudio audio;
        private SubsystemTerrain terrain;
        private SubsystemParticles particles;
        private Vector3 previousPosition;
        private float walk, phase, stingTime;
        private bool sampled;

        // ComponentModel uploads one transform per bone to the native model shader.
        internal const int MaxBones = 14;
        internal const float Windup = .72f;
        internal const float Thrust = .17f;
        internal const float Recovery = .43f;
        internal const float StingerCycle = Windup + Thrust + Recovery;
        internal bool HasTail => Model != null && tail[0] >= 0;

        public override void Load(ValuesDictionary values, IdToEntityMap map)
        {
            base.Load(values, map);
            timeOfDay = Project.FindSubsystem<SubsystemTimeOfDay>(true);
            audio = Project.FindSubsystem<SubsystemAudio>(true);
            terrain = Project.FindSubsystem<SubsystemTerrain>(true);
            particles = Project.FindSubsystem<SubsystemParticles>(true);
        }

        public override void SetModel(Model model)
        {
            base.SetModel(model);
            if (model == null) return;
            for (int side = 0; side < 2; side++)
            {
                string letter = side == 0 ? "L" : "R";
                claws[side] = model.FindBone("Claw" + letter).Index;
                for (int i = 0; i < 4; i++)
                    legs[4 * side + i] = model.FindBone("Leg" + letter + (i + 1)).Index;
            }
            for (int i = 0; i < tail.Length; i++)
                tail[i] = model.FindBone("Tail" + (i + 1), false)?.Index ?? -1;
        }

        internal void BeginSting()
        {
            if (!HasTail || m_componentCreature.ComponentHealth.Health <= 0f || stingTime > .05f) return;
            stingTime = StingerCycle;
            Vector3 point = m_componentCreature.ComponentBody.Position;
            audio.PlayRandomSound("Audio/Footsteps/Sand", .75f, -.27f, point, 4.5f, false);
            if (audio.CalculateListenerDistance(point) > 16f) return;
            int ground = terrain.Terrain.GetCellValue(Terrain.ToCell(point.X),
                Terrain.ToCell(point.Y - .12f), Terrain.ToCell(point.Z));
            Block block = BlocksManager.Blocks[Terrain.ExtractContents(ground)];
            if (ArthropodHabitat.IsSand(block))
                particles.AddParticleSystem(block.CreateDebrisParticleSystem(terrain, point + Vector3.UnitY * .055f, ground, .28f));
        }

        public override void Update(float dt)
        {
            base.Update(dt);
            Vector3 position = m_componentCreature.ComponentBody.Position;
            if (sampled)
            {
                Vector3 travel = position - previousPosition;
                float delta = travel.XZ.Length();
                float speed = dt > 0f && delta < 1f ? delta / dt : 0f;
                walk = MathUtils.Lerp(walk, MathUtils.Saturate(speed / 1.3f), MathUtils.Saturate(dt * 16f));
                phase = MathUtils.Remainder(phase + MathUtils.Min(delta, .17f) * (HasTail ? 14f : 24f), 2f * MathUtils.PI);
            }
            previousPosition = position;
            sampled = true;
            stingTime = MathUtils.Max(0f, stingTime - dt);
        }

        public override void Animate()
        {
            base.Animate();
            if (Model == null) return;
            ComponentBody body = m_componentCreature.ComponentBody;
            bool sleeping = HasTail && stingTime <= 0f && !ArthropodHabitat.IsNight(timeOfDay.TimeOfDay);
            float rest = sleeping ? 1f - walk : 0f;
            float progress = stingTime > 0f ? StingerCycle - stingTime : 0f;
            float warning = stingTime > 0f ? MathUtils.Saturate(progress / Windup) : 0f;
            float stroke = progress >= Windup
                ? MathUtils.Saturate((progress - Windup) / .09f) * MathUtils.Saturate((StingerCycle - progress) / Recovery)
                : 0f;
            float live = 1f - DeathPhase;
            float yaw = body.Rotation.ToYawPitchRoll().X;
            Vector3 position = body.Position;
            position.Y += live * (.008f * walk * MathUtils.Abs(MathUtils.Sin(phase)) - .039f * rest)
                + (HasTail ? .12f : .085f) * DeathPhase;
            SetBoneTransform(Model.RootBone.Index,
                Matrix.CreateFromYawPitchRoll(yaw, stroke * .12f * live, DeathPhase * 1.5f) * Matrix.CreateTranslation(position));
            for (int side = 0; side < 2; side++)
            {
                float sign = side == 0 ? -1f : 1f;
                SetBoneTransform(claws[side], Matrix.CreateRotationX(live * (stroke * .34f - warning * .14f))
                    * Matrix.CreateRotationY(sign * live * (.14f * rest - .18f * warning)));
                for (int i = 0; i < 4; i++)
                {
                    float cycle = phase + (side + i) * MathUtils.PI;
                    float step = walk * MathUtils.Sin(cycle);
                    float lift = walk * MathUtils.Max(0f, MathUtils.Cos(cycle));
                    SetBoneTransform(legs[side * 4 + i],
                        Matrix.CreateRotationY(live * step * (HasTail ? .28f : .11f))
                        * Matrix.CreateRotationZ(sign * (live * (lift * .29f - rest * .13f) - DeathPhase * .43f)));
                }
            }
            if (HasTail)
            {
                SetBoneTransform(tail[0], Matrix.CreateRotationX(live * (.33f * rest - .53f * warning + .78f * stroke)));
                SetBoneTransform(tail[1], Matrix.CreateRotationX(live * (.33f * rest - .39f * warning + .70f * stroke)));
                SetBoneTransform(tail[2], Matrix.CreateRotationX(live * (.23f * rest - .31f * warning + .56f * stroke)));
            }
        }
    }
}

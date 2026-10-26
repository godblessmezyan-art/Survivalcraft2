using Engine;
using Game;

namespace SCIEPort.Features.Stormhammer
{
    /// <summary>Short-lived additive arc, spark and impact particles.</summary>
    internal sealed class StormhammerParticleSystem : ParticleSystem<Particle>
    {
        private readonly Vector3[] m_velocities;
        private readonly float[] m_lifetimes;
        private float m_age;

        private StormhammerParticleSystem(int count)
            : base(count)
        {
            Texture = ContentManager.Get<Engine.Graphics.Texture2D>("Textures/StormhammerEffects");
            TextureSlotsCount = 4;
            m_velocities = new Vector3[count];
            m_lifetimes = new float[count];
        }

        public static StormhammerParticleSystem CreateArc(Vector3 start, Vector3 end, float strength)
        {
            const int count = 28;
            StormhammerParticleSystem system = new StormhammerParticleSystem(count);
            Engine.Random random = new Engine.Random((int)(start.X * 97f + end.Z * 193f + Time.FrameIndex));
            Vector3 direction = end - start;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)(count - 1);
                float taper = MathUtils.Sin(t * MathUtils.PI);
                Particle particle = system.Particles[i];
                particle.IsActive = true;
                particle.Position = start + direction * t + random.Vector3(0.10f * taper * strength);
                particle.Size = new Vector2(0.045f + 0.035f * taper);
                particle.Color = new Color(90, 220, 255, 235);
                particle.TextureSlot = i % 4;
                particle.UseAdditiveBlending = true;
                particle.BillboardingMode = ParticleBillboardingMode.Camera;
                system.m_lifetimes[i] = 0.12f + random.Float(0f, 0.09f);
            }

            return system;
        }

        public static StormhammerParticleSystem CreateBurst(Vector3 position, float strength)
        {
            const int count = 38;
            StormhammerParticleSystem system = new StormhammerParticleSystem(count);
            Engine.Random random = new Engine.Random((int)(position.X * 137f + position.Z * 269f + Time.FrameIndex));
            for (int i = 0; i < count; i++)
            {
                Particle particle = system.Particles[i];
                particle.IsActive = true;
                particle.Position = position + random.Vector3(0.16f);
                particle.Size = new Vector2(random.Float(0.035f, 0.10f) * strength);
                particle.Color = new Color(65, 205, 255, 230);
                particle.TextureSlot = random.Int(0, 3);
                particle.UseAdditiveBlending = true;
                particle.BillboardingMode = ParticleBillboardingMode.Camera;
                system.m_velocities[i] = random.Vector3(3.2f * strength) + Vector3.UnitY * random.Float(0.8f, 3.6f);
                system.m_lifetimes[i] = random.Float(0.20f, 0.55f);
            }

            return system;
        }

        public static StormhammerParticleSystem CreateRing(Vector3 position, float radius, Color color, float lifetime, int requestedCount)
        {
            int count = MathUtils.Clamp(requestedCount, 12, 64);
            StormhammerParticleSystem system = new StormhammerParticleSystem(count);
            for (int i = 0; i < count; i++)
            {
                float angle = 2f * MathUtils.PI * i / count;
                Particle particle = system.Particles[i];
                particle.IsActive = true;
                particle.Position = position + new Vector3(MathUtils.Cos(angle) * radius, 0.08f + 0.06f * MathUtils.Sin(angle * 3f), MathUtils.Sin(angle) * radius);
                particle.Size = new Vector2(0.11f + radius * 0.012f);
                particle.Color = color;
                particle.TextureSlot = i % 4;
                particle.UseAdditiveBlending = true;
                particle.BillboardingMode = ParticleBillboardingMode.Camera;
                system.m_velocities[i] = new Vector3(-MathUtils.Sin(angle), 0.15f, MathUtils.Cos(angle)) * 0.45f;
                system.m_lifetimes[i] = lifetime;
            }
            return system;
        }

        public override bool Simulate(float dt)
        {
            m_age += dt;
            bool any = false;
            for (int i = 0; i < Particles.Length; i++)
            {
                Particle particle = Particles[i];
                if (!particle.IsActive)
                {
                    continue;
                }

                if (m_age >= m_lifetimes[i])
                {
                    particle.IsActive = false;
                    continue;
                }

                any = true;
                particle.Position += m_velocities[i] * dt;
                m_velocities[i] *= MathUtils.Pow(0.08f, dt);
                float alpha = MathUtils.Saturate(1f - m_age / m_lifetimes[i]);
                particle.Color = new Color(80, 215, 255, (int)(235f * alpha));
            }

            return !any;
        }
    }
}

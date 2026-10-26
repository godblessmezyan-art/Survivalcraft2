using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;
using GameEntitySystem;

namespace SCIEPort.Features.Stormhammer
{
    internal enum LightningBoltKind
    {
        /// <summary>Backdrop strike layer close to the caster.</summary>
        Near = 0,
        /// <summary>Backdrop strike layer far away.</summary>
        Far = 1,
        /// <summary>The one real vertical tribulation bolt.</summary>
        Main = 2,
        /// <summary>Radial trunk of Thunderous Judgment.</summary>
        Radial = 3
    }

    /// <summary>A single lightning bolt. All buffers are preallocated; the pool never allocates.</summary>
    internal sealed class LightningBolt
    {
        public const int BranchCapacity = 3;

        public readonly float[] MainPath = new float[LightningRules.PathFloats];
        public readonly float[][] BranchPaths = new float[BranchCapacity][];
        public readonly int[] BranchPointCounts = new int[BranchCapacity];

        public LightningBolt()
        {
            for (int i = 0; i < BranchCapacity; i++)
            {
                BranchPaths[i] = new float[LightningRules.PathFloats];
            }
        }

        public bool Active;
        public LightningBoltKind Kind;
        public int MainPointCount;
        public int BranchCount;
        public double Birth;
        public double Life;
        public float CoreWidth;
        public float HaloWidth;
        public Color CoreColor;
        public Color HaloColor;
        public Vector3 Impact;
        public float GroundRadius;
        public bool ShowImpactFlash;
        public uint Seed;
        public double NextJitter;
    }

    /// <summary>Ground rune ring, drawn flat on the terrain plane.</summary>
    internal struct LightningRune
    {
        public bool Active;
        public Vector3 Centre;
        public float StartRadius;
        public float EndRadius;
        public double Birth;
        public double Life;
        public Color Color;
    }

    /// <summary>
    /// Ribbon based lightning geometry. Every bolt is a continuous polyline expanded into
    /// camera facing quads (plus a crossed plane for the main bolt), so it reads as a solid
    /// jagged column at any distance instead of a chain of glow dots.
    /// </summary>
    internal sealed class LightningScene
    {
        /// <summary>Hard pool size; older bolts are recycled first.</summary>
        public const int BoltCapacity = 48;

        /// <summary>Hard cap on ground rune rings.</summary>
        public const int RuneCapacity = 12;

        private const int MaxQuadsPerFrame = 1400;

        private readonly LightningBolt[] m_bolts = new LightningBolt[BoltCapacity];
        private readonly LightningRune[] m_runes = new LightningRune[RuneCapacity];
        private int m_recycleCursor;

        private PrimitivesRenderer3D m_renderer;
        private SubsystemSky m_sky;
        private SubsystemTime m_time;
        private TexturedBatch3D m_beam;
        private TexturedBatch3D m_rune;
        private TexturedBatch3D m_glow;
        private int m_quadBudget;

        /// <summary>Number of bolts currently alive.</summary>
        public int ActiveBolts { get; private set; }

        /// <summary>Quads queued by the last draw call; diagnostics only.</summary>
        public int LastQuadCount { get; private set; }

        /// <summary>Creates the shared batches. Called once per project from the subsystem.</summary>
        public void Load(Project project)
        {
            m_renderer = new PrimitivesRenderer3D();
            m_sky = project.FindSubsystem<SubsystemSky>(false);
            m_time = project.FindSubsystem<SubsystemTime>(false);
            Texture2D beamTexture = ContentManager.Get<Texture2D>("Textures/LightningBeam");
            Texture2D runeTexture = ContentManager.Get<Texture2D>("Textures/LightningRune");
            Texture2D glowTexture = ContentManager.Get<Texture2D>("Textures/RoundGlow");
            m_beam = m_renderer.TexturedBatch(beamTexture, false, 0, DepthStencilState.DepthRead, RasterizerState.CullNone, BlendState.Additive, SamplerState.LinearClamp);
            m_rune = m_renderer.TexturedBatch(runeTexture, false, 0, DepthStencilState.DepthRead, RasterizerState.CullNone, BlendState.Additive, SamplerState.LinearClamp);
            m_glow = m_renderer.TexturedBatch(glowTexture, false, 0, DepthStencilState.DepthRead, RasterizerState.CullNone, BlendState.Additive, SamplerState.LinearClamp);
        }

        /// <summary>Releases pooled state.</summary>
        public void Clear()
        {
            for (int i = 0; i < m_bolts.Length; i++)
            {
                if (m_bolts[i] != null)
                {
                    m_bolts[i].Active = false;
                }
            }

            for (int i = 0; i < m_runes.Length; i++)
            {
                m_runes[i].Active = false;
            }

            ActiveBolts = 0;
        }

        private LightningBolt Rent()
        {
            for (int i = 0; i < m_bolts.Length; i++)
            {
                int index = (m_recycleCursor + i) % m_bolts.Length;
                LightningBolt bolt = m_bolts[index];
                if (bolt == null)
                {
                    bolt = new LightningBolt();
                    m_bolts[index] = bolt;
                    m_recycleCursor = (index + 1) % m_bolts.Length;
                    return bolt;
                }

                if (!bolt.Active)
                {
                    m_recycleCursor = (index + 1) % m_bolts.Length;
                    return bolt;
                }
            }

            // Pool exhausted: steal the oldest slot rather than growing without bound.
            LightningBolt stolen = m_bolts[m_recycleCursor];
            m_recycleCursor = (m_recycleCursor + 1) % m_bolts.Length;
            return stolen;
        }

        /// <summary>
        /// Adds one bolt. Branch count and roughness scale with the kind, so the main bolt is
        /// visually dominant while the far backdrop stays cheap.
        /// </summary>
        public LightningBolt Spawn(
            LightningBoltKind kind,
            Vector3 start,
            Vector3 end,
            uint seed,
            double now,
            double life,
            float coreWidth,
            float haloWidth,
            Color coreColor,
            Color haloColor,
            int branches,
            float groundRadius,
            bool impactFlash)
        {
            LightningBolt bolt = Rent();
            bolt.Active = true;
            bolt.Kind = kind;
            bolt.Seed = seed;
            bolt.Birth = now;
            bolt.Life = life;
            bolt.CoreWidth = coreWidth;
            bolt.HaloWidth = haloWidth;
            bolt.CoreColor = coreColor;
            bolt.HaloColor = haloColor;
            bolt.Impact = end;
            bolt.GroundRadius = groundRadius;
            bolt.ShowImpactFlash = impactFlash;
            bolt.NextJitter = now + 0.055 + LightningRules.NextFloat(LightningRules.Mix(seed, 7u)) * 0.045;

            int segments = kind == LightningBoltKind.Main ? 18
                : kind == LightningBoltKind.Radial ? 12
                : kind == LightningBoltKind.Near ? 10 : 7;
            float roughness = kind == LightningBoltKind.Main ? 1.45f
                : kind == LightningBoltKind.Radial ? 0.85f
                : kind == LightningBoltKind.Near ? 0.75f : 0.55f;
            float length = Vector3.Distance(start, end);
            float amplitude = MathUtils.Clamp(length * 0.085f, 0.18f, 3.4f) * roughness;

            LightningRules.BuildPath(start.X, start.Y, start.Z, end.X, end.Y, end.Z, segments, amplitude, seed, bolt.MainPath, out bolt.MainPointCount);

            bolt.BranchCount = 0;
            int branchLimit = kind == LightningBoltKind.Main ? 5
                : kind == LightningBoltKind.Radial ? 2
                : kind == LightningBoltKind.Near ? 2 : 0;
            branches = MathUtils.Clamp(branches, 0, branchLimit);
            for (int i = 0; i < branches; i++)
            {
                if (bolt.BranchCount >= LightningBolt.BranchCapacity)
                {
                    break;
                }

                uint branchSeed = LightningRules.Mix(seed, (uint)(0x200 + i));
                int anchor = 2 + (int)(LightningRules.NextFloat(branchSeed) * (bolt.MainPointCount - 4));
                anchor = MathUtils.Clamp(anchor, 1, bolt.MainPointCount - 2);
                int offset = anchor * LightningRules.PathStride;
                var branchStart = new Vector3(bolt.MainPath[offset], bolt.MainPath[offset + 1], bolt.MainPath[offset + 2]);
                Vector3 axis = end - start;
                float spread = LightningRules.Range(0.35f, 1.0f, LightningRules.Mix(branchSeed, 11u)) * length * 0.34f;
                float angle = LightningRules.Range(0f, MathUtils.PI * 2f, LightningRules.Mix(branchSeed, 12u));
                var lateral = new Vector3(MathUtils.Cos(angle) * spread, -length * LightningRules.Range(0.05f, 0.18f, LightningRules.Mix(branchSeed, 13u)), MathUtils.Sin(angle) * spread);
                if (axis.LengthSquared() > 0.001f)
                {
                    axis = Vector3.Normalize(axis);
                    lateral -= axis * Vector3.Dot(lateral, axis) * 0.55f;
                }

                Vector3 branchEnd = branchStart + lateral;
                LightningRules.BuildPath(
                    branchStart.X, branchStart.Y, branchStart.Z,
                    branchEnd.X, branchEnd.Y, branchEnd.Z,
                    6,
                    MathUtils.Clamp(Vector3.Distance(branchStart, branchEnd) * 0.12f, 0.1f, 1.3f),
                    branchSeed,
                    bolt.BranchPaths[bolt.BranchCount],
                    out bolt.BranchPointCounts[bolt.BranchCount]);
                bolt.BranchCount++;
            }

            RecountActive();
            return bolt;
        }

        /// <summary>Adds a flat ground rune ring that expands from start to end radius.</summary>
        public void SpawnRune(Vector3 centre, float startRadius, float endRadius, double now, double life, Color color)
        {
            for (int i = 0; i < m_runes.Length; i++)
            {
                if (!m_runes[i].Active)
                {
                    m_runes[i] = new LightningRune
                    {
                        Active = true,
                        Centre = centre,
                        StartRadius = startRadius,
                        EndRadius = endRadius,
                        Birth = now,
                        Life = life,
                        Color = color
                    };
                    return;
                }
            }

            // Replace the oldest ring when the pool is full.
            int oldest = 0;
            double best = double.MaxValue;
            for (int i = 0; i < m_runes.Length; i++)
            {
                if (m_runes[i].Birth < best)
                {
                    best = m_runes[i].Birth;
                    oldest = i;
                }
            }

            m_runes[oldest] = new LightningRune
            {
                Active = true,
                Centre = centre,
                StartRadius = startRadius,
                EndRadius = endRadius,
                Birth = now,
                Life = life,
                Color = color
            };
        }

        /// <summary>Advances lifetimes and re-jitters live bolts so the channel keeps crawling.</summary>
        public void Update(double now)
        {
            for (int i = 0; i < m_bolts.Length; i++)
            {
                LightningBolt bolt = m_bolts[i];
                if (bolt == null || !bolt.Active)
                {
                    continue;
                }

                if (now >= bolt.Birth + bolt.Life)
                {
                    bolt.Active = false;
                    continue;
                }

                if (now >= bolt.NextJitter && bolt.Kind != LightningBoltKind.Far)
                {
                    Reshape(bolt, now);
                }
            }

            for (int i = 0; i < m_runes.Length; i++)
            {
                if (m_runes[i].Active && now >= m_runes[i].Birth + m_runes[i].Life)
                {
                    m_runes[i].Active = false;
                }
            }

            RecountActive();
        }

        private void Reshape(LightningBolt bolt, double now)
        {
            uint seed = LightningRules.NextState(bolt.Seed + (uint)(now * 1000.0));
            bolt.Seed = seed;
            int offset = bolt.MainPointCount * LightningRules.PathStride;
            float endX = bolt.MainPath[offset - 3];
            float endY = bolt.MainPath[offset - 2];
            float endZ = bolt.MainPath[offset - 1];
            float startX = bolt.MainPath[0];
            float startY = bolt.MainPath[1];
            float startZ = bolt.MainPath[2];
            float length = Vector3.Distance(new Vector3(startX, startY, startZ), new Vector3(endX, endY, endZ));
            float amplitude = MathUtils.Clamp(length * 0.085f, 0.18f, 3.4f)
                * (bolt.Kind == LightningBoltKind.Main ? 1.45f : bolt.Kind == LightningBoltKind.Radial ? 0.85f : 0.7f);
            LightningRules.BuildPath(startX, startY, startZ, endX, endY, endZ, bolt.MainPointCount - 1, amplitude, seed, bolt.MainPath, out bolt.MainPointCount);
            bolt.NextJitter = now + 0.055 + LightningRules.NextFloat(LightningRules.Mix(seed, 9u)) * 0.05;
        }

        private void RecountActive()
        {
            int count = 0;
            for (int i = 0; i < m_bolts.Length; i++)
            {
                if (m_bolts[i] != null && m_bolts[i].Active)
                {
                    count++;
                }
            }

            ActiveBolts = count;
        }

        /// <summary>Queues every live bolt, rune and impact flash for the current camera.</summary>
        public void Draw(Camera camera, int quadBudgetScale = 1)
        {
            if (m_renderer == null || camera == null)
            {
                return;
            }

            m_quadBudget = MaxQuadsPerFrame * MathUtils.Clamp(quadBudgetScale, 1, 4) / 4;
            int quads = 0;
            m_quadBudget = MathUtils.Max(m_quadBudget, 240);
            double now = m_time?.GameTime ?? Time.RealTime;
            Vector3 viewPosition = camera.ViewPosition;
            Vector3 viewForward = camera.ViewDirection;

            for (int i = 0; i < m_bolts.Length; i++)
            {
                LightningBolt bolt = m_bolts[i];
                if (bolt == null || !bolt.Active)
                {
                    continue;
                }

                float alpha = LightningRules.LifeAlpha(now - bolt.Birth, bolt.Life, 0.06f, 0.42f);
                if (alpha <= 0.004f)
                {
                    continue;
                }

                if (quads >= m_quadBudget)
                {
                    break;
                }

                quads += DrawBolt(bolt, camera, viewPosition, viewForward, alpha);
                if (bolt.ShowImpactFlash)
                {
                    quads += DrawImpactFlash(bolt, camera, viewPosition, alpha);
                }
            }

            for (int i = 0; i < m_runes.Length; i++)
            {
                if (!m_runes[i].Active)
                {
                    continue;
                }

                DrawRune(m_runes[i], now);
            }

            LastQuadCount = quads;
            m_renderer.Flush(camera.ViewProjectionMatrix);
        }

        private int DrawBolt(LightningBolt bolt, Camera camera, Vector3 viewPosition, Vector3 viewForward, float alpha)
        {
            float fog = FogFactor(viewPosition, bolt.Impact);
            Color core = Blend(camera, bolt.CoreColor, fog, alpha);
            Color halo = Blend(camera, bolt.HaloColor, fog, alpha * 0.72f);
            int quads = 0;

            float distance = Vector3.Distance(viewPosition, bolt.Impact);
            // Angular minimum keeps distant bolts readable instead of sub-pixel thin.
            float distanceFloor = distance * 0.0035f;
            float coreWidth = MathUtils.Max(bolt.CoreWidth * 0.46f, distanceFloor * 0.55f);
            float haloWidth = MathUtils.Max(bolt.HaloWidth, distanceFloor * 1.6f);

            quads += QueueRibbon(bolt.MainPath, bolt.MainPointCount, viewPosition, coreWidth, core, false);
            quads += QueueRibbon(bolt.MainPath, bolt.MainPointCount, viewPosition, haloWidth, halo, bolt.Kind == LightningBoltKind.Main);

            for (int b = 0; b < bolt.BranchCount; b++)
            {
                Color branchCore = new Color(core.R, core.G, core.B, (int)(core.A * 0.78f));
                Color branchHalo = new Color(halo.R, halo.G, halo.B, (int)(halo.A * 0.66f));
                quads += QueueRibbon(bolt.BranchPaths[b], bolt.BranchPointCounts[b], viewPosition, coreWidth * 0.62f, branchCore, false);
                quads += QueueRibbon(bolt.BranchPaths[b], bolt.BranchPointCounts[b], viewPosition, haloWidth * 0.52f, branchHalo, false);
            }

            return quads;
        }

        /// <summary>
        /// Expands a polyline into camera facing quads. When <paramref name="crossPlane"/> is set a
        /// perpendicular plane is queued as well, which gives the main bolt real volume when the
        /// camera strafes past it.
        /// </summary>
        private int QueueRibbon(float[] path, int pointCount, Vector3 viewPosition, float width, Color color, bool crossPlane)
        {
            if (pointCount < 2 || width <= 0.0001f)
            {
                return 0;
            }

            int quads = 0;
            for (int i = 0; i < pointCount - 1; i++)
            {
                int a = i * LightningRules.PathStride;
                int b = (i + 1) * LightningRules.PathStride;
                var p0 = new Vector3(path[a], path[a + 1], path[a + 2]);
                var p1 = new Vector3(path[b], path[b + 1], path[b + 2]);
                Vector3 axis = p1 - p0;
                float length = axis.Length();
                if (length < 0.0005f)
                {
                    continue;
                }

                axis /= length;
                Vector3 mid = (p0 + p1) * 0.5f;
                Vector3 toCamera = viewPosition - mid;
                if (toCamera.LengthSquared() < 0.0001f)
                {
                    toCamera = Vector3.UnitY;
                }

                toCamera = Vector3.Normalize(toCamera);
                Vector3 side = Vector3.Cross(axis, toCamera);
                if (side.LengthSquared() < 0.0001f)
                {
                    side = Vector3.Cross(axis, Vector3.UnitY);
                }

                side = Vector3.Normalize(side);
                float t0 = LightningRules.Taper(i / (float)(pointCount - 1), 1f, 0.45f);
                float t1 = LightningRules.Taper((i + 1) / (float)(pointCount - 1), 1f, 0.45f);
                float w0 = width * t0;
                float w1 = width * t1;
                // Match SubsystemGlow's z bias: pull the ribbon slightly toward the camera so
                // it does not fight the surface it is drawn over.
                Vector3 push = (0.01f + 0.02f * Vector3.Dot(mid - viewPosition, toCamera)) * toCamera;
                QueueQuad(p0 - side * w0 + push, p0 + side * w0 + push, p1 + side * w1 + push, p1 - side * w1 + push, color);
                quads++;

                if (crossPlane)
                {
                    Vector3 side2 = Vector3.Cross(axis, side);
                    if (side2.LengthSquared() > 0.0001f)
                    {
                        side2 = Vector3.Normalize(side2);
                        Color faint = new Color(color.R, color.G, color.B, (int)(color.A * 0.45f));
                        QueueQuad(p0 - side2 * w0 * 0.8f + push, p0 + side2 * w0 * 0.8f + push, p1 + side2 * w1 * 0.8f + push, p1 - side2 * w1 * 0.8f + push, faint);
                        quads++;
                    }
                }
            }

            return quads;
        }

        private void QueueQuad(Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4, Color color)
        {
            m_beam.QueueQuad(p1, p2, p3, p4, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f), color);
        }

        private int DrawImpactFlash(LightningBolt bolt, Camera camera, Vector3 viewPosition, float alpha)
        {
            double now = m_time?.GameTime ?? Time.RealTime;
            double age = now - bolt.Birth;
            if (age > 0.22)
            {
                return 0;
            }

            float fade = (float)(1.0 - age / 0.22);
            float size = bolt.HaloWidth * (2.4f + 6.5f * (1f - fade));
            Vector3 forward = -camera.ViewDirection;
            Vector3 right = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, forward));
            Vector3 up = Vector3.Cross(forward, right);
            Color color = new Color(226, 248, 255, (int)(235f * fade * fade));
            Vector3 centre = bolt.Impact + Vector3.UnitY * 0.35f;
            m_glow.QueueQuad(
                centre - right * size - up * size,
                centre + right * size - up * size,
                centre + right * size + up * size,
                centre - right * size + up * size,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                color);
            return 1;
        }

        private void DrawRune(LightningRune rune, double now)
        {
            float progress = (float)MathUtils.Saturate((now - rune.Birth) / MathUtils.Max(rune.Life, 0.001));
            float radius = MathUtils.Lerp(rune.StartRadius, rune.EndRadius, 1f - (1f - progress) * (1f - progress));
            float alpha = 1f - progress;
            Color color = new Color(rune.Color.R, rune.Color.G, rune.Color.B, (int)(rune.Color.A * alpha * alpha));
            Vector3 centre = rune.Centre + Vector3.UnitY * 0.03f;
            Vector3 right = new Vector3(radius, 0f, 0f);
            Vector3 forward = new Vector3(0f, 0f, radius);
            m_rune.QueueQuad(
                centre - right - forward,
                centre + right - forward,
                centre + right + forward,
                centre - right + forward,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f), new Vector2(0f, 0f),
                color);
        }

        private float FogFactor(Vector3 viewPosition, Vector3 position)
        {
            return m_sky == null ? 0f : MathUtils.Saturate(m_sky.CalculateFog(viewPosition, position));
        }

        private Color Blend(Camera camera, Color color, float fog, float alpha)
        {
            Color blended = m_sky == null ? color : Color.LerpNotSaturated(color, m_sky.ViewFogColor, fog * 0.85f);
            int a = (int)(color.A * alpha * (1f - fog * 0.55f));
            return new Color(blended.R, blended.G, blended.B, MathUtils.Clamp(a, 0, 255));
        }
    }
}

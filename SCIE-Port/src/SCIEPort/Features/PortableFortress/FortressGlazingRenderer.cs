using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;
using GameEntitySystem;

namespace SCIEPort.Features.PortableFortress
{
    /// <summary>
    /// Draws the fortress glazing and the warm interior glow points.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The fortress body is a <c>ComponentSimpleModel</c>, and <c>SubsystemModelsRenderer</c> only
    /// ever submits it to <c>ShaderOpaque</c> or <c>ShaderAlphaTested</c>. There is no blended
    /// model pass, so a translucent pane cannot be part of that mesh: an alpha-tested cutout would
    /// read as a hole, and an opaque blue plate would block the view. The panes are therefore
    /// drawn here with <c>BlendState.AlphaBlend</c> and <c>DepthStencilState.DepthRead</c>, which
    /// keeps them out of the depth buffer so the world behind them still resolves.
    /// </para>
    /// <para>
    /// Panes are sorted far to near against the camera because additive-style overlap of two
    /// blended quads is order dependent. The pane list is a handful of quads, so the sort is
    /// cheap and allocation free.
    /// </para>
    /// </remarks>
    internal sealed class FortressGlazingRenderer
    {
        /// <summary>Warm light anchors, mirrored by the lamp geometry in the model generator.</summary>
        public static readonly Vector3[] GlowPoints =
        {
            new Vector3(-4.00f, 0.98f, 3.05f),
            new Vector3(-1.30f, 2.30f, 3.10f),
            new Vector3(1.30f, 2.30f, 3.10f),
            new Vector3(0.00f, 5.55f, 3.60f)
        };

        private const float GlowRadius = 0.85f;

        private readonly List<(float Depth, Vector3 A, Vector3 B, Vector3 C, Vector3 D)> m_panes =
            new List<(float, Vector3, Vector3, Vector3, Vector3)>(16);

        private PrimitivesRenderer3D m_renderer;
        private Texture2D m_glassTexture;
        private Texture2D m_glowTexture;
        private TexturedBatch3D m_glass;
        private TexturedBatch3D m_glow;
        private int m_glowQuads;

        /// <summary>Loads the pane textures and creates the batches.</summary>
        public void Load()
        {
            m_renderer = new PrimitivesRenderer3D();
            m_glassTexture = ContentManager.Get<Texture2D>("Textures/FortressGlass");
            m_glowTexture = ContentManager.Get<Texture2D>("Textures/FortressGlow");
            m_glass = m_renderer.TexturedBatch(
                m_glassTexture, false, 0,
                DepthStencilState.DepthRead, RasterizerState.CullNone, BlendState.AlphaBlend, SamplerState.LinearClamp);
            m_glow = m_renderer.TexturedBatch(
                m_glowTexture, false, 0,
                DepthStencilState.DepthRead, RasterizerState.CullNone, BlendState.Additive, SamplerState.LinearClamp);
        }

        /// <summary>Drops the batch references; the engine renderer owns no native handle here.</summary>
        public void Dispose()
        {
            m_renderer = null;
            m_glass = null;
            m_glow = null;
            m_panes.Clear();
            m_glowQuads = 0;
        }

        /// <summary>Queues every pane and glow of one instance.</summary>
        public void Queue(FortressRecord record, Vector3 viewPosition)
        {
            if (m_renderer == null || record == null)
            {
                return;
            }

            IReadOnlyList<FortressSegment> segments = PortableFortressGeometry.Segments;
            for (int i = 0; i < segments.Count; i++)
            {
                FortressSegment segment = segments[i];
                if (segment.Zone != FortressZone.Glazing)
                {
                    continue;
                }

                // The splayed nose corner uses a block proxy, not a pane.
                if (!segment.Name.StartsWith("Glazing", StringComparison.Ordinal))
                {
                    continue;
                }

                QueuePane(record, segment.Box, viewPosition);
            }

            if (FortressOptions.Allow(FortressQuality.Medium))
            {
                for (int i = 0; i < GlowPoints.Length; i++)
                {
                    QueueGlow(record, GlowPoints[i], viewPosition);
                }
            }
        }

        /// <summary>Sorts, submits and flushes the queued primitives.</summary>
        public void Flush(Camera camera)
        {
            if (m_renderer == null)
            {
                return;
            }

            m_panes.Sort((left, right) => right.Depth.CompareTo(left.Depth));
            for (int i = 0; i < m_panes.Count; i++)
            {
                (float _, Vector3 a, Vector3 b, Vector3 c, Vector3 d) = m_panes[i];
                m_glass.QueueQuad(
                    a, b, c, d,
                    new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f), new Vector2(0f, 0f),
                    new Color(196, 226, 244, 74));
            }

            if (m_panes.Count > 0 || m_glowQuads > 0)
            {
                m_renderer.Flush(camera.ViewProjectionMatrix, clearAfterFlush: false);
            }

            m_panes.Clear();
            m_glowQuads = 0;
        }

        private void QueuePane(FortressRecord record, FortressBox box, Vector3 viewPosition)
        {
            // The pane is a thin slab; draw its mid plane once and let CullNone show both sides.
            bool thinAlongX = box.SizeX <= box.SizeZ;
            float x0 = thinAlongX ? box.CentreX : box.MinX;
            float x1 = thinAlongX ? box.CentreX : box.MaxX;
            float z0 = thinAlongX ? box.MinZ : box.CentreZ;
            float z1 = thinAlongX ? box.MaxZ : box.CentreZ;

            Vector3 a = ToWorld(record, x0, box.MinY, z0);
            Vector3 b = ToWorld(record, x1, box.MinY, z1);
            Vector3 c = ToWorld(record, x1, box.MaxY, z1);
            Vector3 d = ToWorld(record, x0, box.MaxY, z0);

            float depth = Vector3.DistanceSquared((a + c) * 0.5f, viewPosition);
            m_panes.Add((depth, a, b, c, d));
        }

        private void QueueGlow(FortressRecord record, Vector3 local, Vector3 viewPosition)
        {
            Vector3 centre = ToWorld(record, local.X, local.Y, local.Z);
            float distance = Vector3.Distance(centre, viewPosition);
            if (distance > 42f)
            {
                return;
            }

            float radius = GlowRadius * MathUtils.Clamp(1.25f - distance / 60f, 0.6f, 1.25f);
            Vector3 right = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, Vector3.Normalize(viewPosition - centre)));
            if (float.IsNaN(right.X))
            {
                right = Vector3.UnitX;
            }

            Vector3 up = Vector3.UnitY;
            m_glow.QueueQuad(
                centre - right * radius - up * radius,
                centre + right * radius - up * radius,
                centre + right * radius + up * radius,
                centre - right * radius + up * radius,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Color(255, 176, 96, 150));
            m_glowQuads++;
        }

        private static Vector3 ToWorld(FortressRecord record, float x, float y, float z)
        {
            PortableFortressGeometry.RotateOffset(x, z, record.Facing, out float wx, out float wz);
            return new Vector3(record.AnchorX + 0.5f + wx, record.BaseY + y, record.AnchorZ + 0.5f + wz);
        }
    }
}

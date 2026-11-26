using System;
using Engine;
using Engine.Graphics;
using Game;

namespace SCIEPort.Features.Flashlight
{
    internal sealed class FlashlightTerrainLighting : IDisposable
    {
        private const string Glsl = "#ifdef GLSL";
        private const string Varying = @"
#ifdef GL_ES
varying mediump vec3 v_scieFlashlightDelta;
#else
varying vec3 v_scieFlashlightDelta;
#endif
";
        private const string TextureMultiply = "result *= texture2D(u_texture, v_texcoord);";
        private const string LitTexture = @"
    vec4 scieTexel = texture2D(u_texture, v_texcoord);
    result *= scieTexel;
    float scieDistance2 = dot(v_scieFlashlightDelta, v_scieFlashlightDelta);
    // Discard the light contribution outside 15 blocks, without affecting native alpha or fog.
    if (scieDistance2 < 225.0)
    {
        float scieCosine = dot(v_scieFlashlightDelta, u_scieFlashlightDirection)
            * inversesqrt(max(scieDistance2, 0.0001));
        float scieCone = smoothstep(0.88, 0.985, scieCosine);
        float scieRange = 1.0 - smoothstep(9.0, 225.0, scieDistance2);
        vec3 scieLight = vec3(0.94, 0.91, 0.82) * (scieCone * scieRange * v_color.a);
        result.rgb = max(result.rgb, scieTexel.rgb * scieLight);
    }
";
        private Shader m_opaque, m_alpha, m_transparent;
        private Shader m_originalOpaque, m_originalAlpha, m_originalTransparent;
        private bool m_unavailable;

        internal void Begin(Camera camera)
        {
            if (m_unavailable || TerrainRenderer.m_opaqueShader == null ||
                TerrainRenderer.m_alphaTestedShader == null || TerrainRenderer.m_transparentShader == null)
                return;

            try
            {
                if (m_opaque == null)
                {
                    m_opaque = CreateShader("Opaque", "Opaque");
                    m_alpha = CreateShader("AlphaTested", "ALPHATESTED");
                    m_transparent = CreateShader("Transparent", "Transparent");
                }

                Vector3 direction = camera.ViewDirection;
                m_opaque.GetParameter("u_scieFlashlightDirection").SetValue(direction);
                m_alpha.GetParameter("u_scieFlashlightDirection").SetValue(direction);
                m_transparent.GetParameter("u_scieFlashlightDirection").SetValue(direction);
                m_originalOpaque = TerrainRenderer.m_opaqueShader;
                m_originalAlpha = TerrainRenderer.m_alphaTestedShader;
                m_originalTransparent = TerrainRenderer.m_transparentShader;
                TerrainRenderer.m_opaqueShader = m_opaque;
                TerrainRenderer.m_alphaTestedShader = m_alpha;
                TerrainRenderer.m_transparentShader = m_transparent;
            }
            catch (Exception e)
            {
                m_unavailable = true;
                Dispose();
                Log.Warning("[SCIEPort:Flashlight] Terrain shader unavailable: " + e.Message);
            }
        }

        internal void End()
        {
            if (m_originalOpaque == null) return;
            if (ReferenceEquals(TerrainRenderer.m_opaqueShader, m_opaque))
                TerrainRenderer.m_opaqueShader = m_originalOpaque;
            if (ReferenceEquals(TerrainRenderer.m_alphaTestedShader, m_alpha))
                TerrainRenderer.m_alphaTestedShader = m_originalAlpha;
            if (ReferenceEquals(TerrainRenderer.m_transparentShader, m_transparent))
                TerrainRenderer.m_transparentShader = m_originalTransparent;
            m_originalOpaque = m_originalAlpha = m_originalTransparent = null;
        }

        public void Dispose()
        {
            End();
            m_opaque?.Dispose();
            m_alpha?.Dispose();
            m_transparent?.Dispose();
            m_opaque = m_alpha = m_transparent = null;
        }

        private static Shader CreateShader(string file, string macro) => new Shader(
            VertexSource(ShaderCodeManager.GetFast("Shaders/" + file + ".vsh")),
            FragmentSource(ShaderCodeManager.GetFast("Shaders/" + file + ".psh")),
            new ShaderMacro(macro));

        internal static string VertexSource(string code) => PatchGlsl(code, Varying,
            "v_texcoord = a_texcoord;", "v_texcoord = a_texcoord;\n    v_scieFlashlightDelta = a_position - u_viewPosition;");

        internal static string FragmentSource(string code) => PatchGlsl(code,
            Varying + "uniform vec3 u_scieFlashlightDirection;\n", TextureMultiply, LitTexture);

        private static string PatchGlsl(string source, string declarations, string before, string after)
        {
            int glsl = source.IndexOf(Glsl, StringComparison.Ordinal);
            int main = glsl < 0 ? -1 : source.IndexOf("void main()", glsl, StringComparison.Ordinal);
            int body = main < 0 ? -1 : source.IndexOf(before, main, StringComparison.Ordinal);
            if (body < 0) throw new InvalidOperationException("SCNET terrain shader layout is unrecognized.");
            return source.Substring(0, main) + declarations + source.Substring(main, body - main) +
                after + source.Substring(body + before.Length);
        }
    }
}

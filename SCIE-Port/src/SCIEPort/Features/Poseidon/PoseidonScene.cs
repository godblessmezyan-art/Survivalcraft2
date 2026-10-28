using System;
using Engine;
using Engine.Graphics;
using Game;
using SCIEPort.Features.Stormhammer;

namespace SCIEPort.Features.Poseidon
{
    /// <summary>Bounded water sheets, curling foam, spray and tide sigils, using native flat batches.</summary>
    internal sealed class PoseidonScene
    {
        private readonly SubsystemPoseidon m_runtime;
        private readonly PrimitivesRenderer3D m_renderer = new PrimitivesRenderer3D();
        private readonly FlatBatch3D m_water, m_foam;
        private Camera m_camera;
        private int m_segments;
        internal PoseidonScene(SubsystemPoseidon runtime)
        {
            m_runtime = runtime;
            m_water = m_renderer.FlatBatch(0,DepthStencilState.DepthRead,RasterizerState.CullNone,BlendState.AlphaBlend);
            m_foam = m_renderer.FlatBatch(1,DepthStencilState.DepthRead,RasterizerState.CullNone,BlendState.Additive);
        }
        internal void Draw(Camera camera)
        {
            m_camera = camera;
            m_segments = StormhammerOptions.Quality == StormhammerEffectQuality.Low ? 16 : StormhammerOptions.Quality == StormhammerEffectQuality.Medium ? 24 : 32;
            foreach (var c in m_runtime.Casts)
            {
                if (Vector3.DistanceSquared(c.Origin,camera.ViewPosition) > 6400) continue;
                float age = (float)(m_runtime.Now-c.Start);
                if (age < 0) continue;
                if (c.Skill == 1) Dash(c,age); else Wave(c,age);
            }
            int players = 0;
            foreach (var p in m_runtime.Project.FindSubsystem<SubsystemPlayers>(true).ComponentPlayers)
            {
                if (!SubsystemPoseidon.Equipped(p) || Vector3.DistanceSquared(p.ComponentBody.Position,camera.ViewPosition) > 1600 || players++ >= 8) continue;
                // Six tiny drifting droplets, not a full-screen aura. Invisible behind terrain.
                for (int i = 0; i < 6; i++)
                {
                    float a = (float)m_runtime.Now * 0.8f + i*6.283185f/6;
                    Vector3 point = p.ComponentBody.Position + new Vector3(MathUtils.Cos(a)*0.48f,0.3f+(i*0.29f+(float)m_runtime.Now*0.25f)%1.3f,MathUtils.Sin(a)*0.48f);
                    Beam(point,point+Vector3.UnitY*0.04f,0.018f,new Color(81,210,243)*0.55f);
                }
            }
            m_renderer.Flush(camera.ViewProjectionMatrix);
        }
        private void Dash(SubsystemPoseidon.Cast c,float age)
        {
            float fade = MathUtils.Saturate((PoseidonRules.DashDuration-age)/0.18f);
            Vector3 foot = c.Player.ComponentBody.Position + Vector3.UnitY*0.14f;
            Vector3 side = Vector3.Normalize(Vector3.Cross(c.Direction,Vector3.UnitY));
            for (int strand = 0; strand < 4; strand++)
            {
                Vector3 previous = foot + side*(strand%2==0 ? 0.45f : -0.45f);
                for (int i=1;i<=m_segments/2;i++)
                {
                    float t = i/(m_segments/2f);
                    float bend = MathUtils.Sin(t*6.28f-age*16f+strand)*0.2f;
                    Vector3 point = foot-c.Direction*(t*3.8f)+side*((strand%2==0 ? 0.45f : -0.45f)+bend)+Vector3.UnitY*(strand*0.22f+t*0.35f);
                    Beam(previous,point,(0.09f-0.065f*t),new Color(75,218,250)*((1-t)*fade*0.85f));
                    previous = point;
                }
            }
            Ring(c.Origin+Vector3.UnitY*0.07f,0.7f+age*4,0.045f,new Color(131,234,251)*fade);
            Spray(foot,side,c.Direction,age,fade,12);
        }
        private void Wave(SubsystemPoseidon.Cast c,float age)
        {
            float reveal = MathUtils.Saturate(age/PoseidonRules.WaveWindup);
            float fade = MathUtils.Saturate((PoseidonRules.WaveWindup+PoseidonRules.WaveDuration-age)/0.3f);
            Vector3 seal = c.Origin+Vector3.UnitY*0.08f;
            Ring(seal,1.6f*reveal,0.04f,new Color(233,191,95)*(fade*0.7f));
            Ring(seal,1.27f*reveal,0.025f,new Color(65,212,243)*fade);
            for(int i=0;i<8;i++)
            {
                float a=i*6.283185f/8+age*0.35f;
                Vector3 radial=new Vector3(MathUtils.Cos(a),0,MathUtils.Sin(a));
                Vector3 side=Vector3.Cross(radial,Vector3.UnitY);
                Vector3 p=seal+radial*(1.42f*reveal);
                Beam(p-radial*0.12f,p+radial*0.12f,0.019f,new Color(255,225,144)*fade);
                Beam(p-side*0.08f,p+side*0.08f,0.018f,new Color(78,223,246)*fade);
            }
            Vector3 origin=c.Origin+c.Direction*PoseidonRules.WaveDistance(age);
            Vector3 across=Vector3.Normalize(Vector3.Cross(c.Direction,Vector3.UnitY));
            int columns=m_segments, rows=6;
            for(int x=0;x<columns;x++)
            {
                float u=x/(float)columns, v=(x+1f)/columns;
                float edge=MathUtils.Sin((u+v)*0.5f*MathUtils.PI);
                for(int y=0;y<rows;y++)
                {
                    float t=y/(float)rows, s=(y+1f)/rows;
                    Vector3 a=Surface(origin,across,c.Direction,u,t,age,reveal);
                    Vector3 b=Surface(origin,across,c.Direction,v,t,age,reveal);
                    Vector3 d=Surface(origin,across,c.Direction,u,s,age,reveal);
                    Vector3 e=Surface(origin,across,c.Direction,v,s,age,reveal);
                    Color color=Color.Lerp(new Color(10,62,126,155),new Color(61,191,222,115),t);
                    m_water.QueueQuad(a,b,e,d,color*(fade*edge));
                    if(y==rows-1) Beam(d,e,0.08f+0.05f*edge,new Color(207,251,255)*(edge*fade));
                    if(y==3) Beam(d,e,0.025f,new Color(82,214,245)*(edge*fade*0.7f));
                }
            }
            // White curling crest + broken spray distinguish a tidal wave from a flat energy wall.
            Spray(origin+Vector3.UnitY*(2.8f*reveal),across,c.Direction,age,fade,m_segments);
            for(int i=0;i<3;i++) Ring(origin-c.Direction*(i*0.65f)+Vector3.UnitY*0.06f,1.8f+i*0.55f,0.03f,new Color(70,208,240)*(fade*0.35f));
        }
        private static Vector3 Surface(Vector3 o,Vector3 side,Vector3 dir,float u,float t,float age,float reveal)
        {
            float edge=MathUtils.Pow(MathUtils.Sin(u*MathUtils.PI),0.45f);
            float height=(3.65f+MathUtils.Sin(u*19-age*7)*0.15f)*edge*reveal;
            float curl=(float)Math.Sin(t*Math.PI*0.95f)*-1.1f+t*t*0.9f;
            return o+side*((u-0.5f)*PoseidonRules.WaveWidth)+Vector3.UnitY*(0.08f+t*height)+dir*(curl+0.2f*MathUtils.Cos(u*6.28f));
        }
        private void Spray(Vector3 center,Vector3 side,Vector3 direction,float age,float fade,int count)
        {
            for(int i=0;i<count;i++)
            {
                float phase=(i*0.618034f+age*1.5f)%1;
                float width=(i/(float)(count-1)-0.5f)*(count>12 ? 7.5f : 1.6f);
                Vector3 p=center+side*width+Vector3.UnitY*(MathUtils.Sin(phase*MathUtils.PI)*0.9f)-direction*(phase*0.8f);
                Beam(p,p+Vector3.UnitY*0.1f-direction*0.06f,0.025f*(1-phase)+0.008f,new Color(194,247,255)*((1-phase)*fade));
            }
        }
        private void Ring(Vector3 center,float radius,float width,Color color)
        {
            if(radius<=width)return;
            for(int i=0;i<m_segments;i++)
            {
                float a=i*6.283185f/m_segments,b=(i+1)*6.283185f/m_segments;
                Vector3 u=new Vector3(MathUtils.Cos(a),0,MathUtils.Sin(a)),v=new Vector3(MathUtils.Cos(b),0,MathUtils.Sin(b));
                m_foam.QueueQuad(center+u*(radius-width),center+u*(radius+width),center+v*(radius+width),center+v*(radius-width),color);
            }
        }
        private void Beam(Vector3 a,Vector3 b,float width,Color color)
        {
            Vector3 side=Vector3.Cross(b-a,m_camera.ViewPosition-(a+b)*0.5f);
            if(side.LengthSquared()<0.000001f)return;
            side=Vector3.Normalize(side)*width;
            m_foam.QueueQuad(a-side,a+side,b+side,b-side,color);
        }
        internal void Dispose() { m_renderer.Clear(); }
    }
}

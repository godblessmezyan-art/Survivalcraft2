using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;
namespace SCIEPort.Features.AbyssReaper
{
    // Presentation-only procedural triangle ribbons. No terrain, target enumeration or AttackBody.
    internal sealed class WorldCutScene : IDisposable
    {
        private readonly PrimitivesRenderer3D renderer=new PrimitivesRenderer3D();
        private readonly bool visible,low;
        private int triangles,lines;
        internal WorldCutScene(bool show,bool lowEffects) { visible=show;low=lowEffects; }
        internal void Draw(Camera camera,IList<WorldCutRuntime.Cast> casts,double now)
        {
            if(!visible || casts.Count==0)return;triangles=0;lines=0;
            var core=renderer.FlatBatch(-2,DepthStencilState.DepthRead,RasterizerState.CullNone,BlendState.Opaque);
            var edges=renderer.FlatBatch(-1,DepthStencilState.DepthRead,RasterizerState.CullNone,BlendState.AlphaBlend);
            foreach(var cast in casts)
            {
                if(cast.Motion.Phase==WorldCutPhase.Finished)continue;
                float scale=cast.Motion.Phase==WorldCutPhase.Windup?.15f+.35f*(float)Math.Min(1,(now-cast.Motion.Started)/WorldCutRules.Windup):1;
                float collapse=0;
                if(cast.Motion.Phase==WorldCutPhase.Closing)
                { collapse=(float)(now-cast.Motion.CloseStarted);scale=Math.Max(.02f,1-collapse/WorldCutRules.CloseImpact); }
                Vector3 center=cast.Origin+cast.Direction*cast.Motion.Distance;
                int segments=low?8:20;
                for(int i=0;i<segments;i++)
                {
                    Vector3 a,b,c,d;Ribbon(cast,center,i/(float)segments,scale,out a,out b);Ribbon(cast,center,(i+1f)/segments,scale,out d,out c);
                    Quad(core,a,b,c,d,new Color(3,1,7));
                    Quad(core,a-Vector3.UnitY*.10f,d-Vector3.UnitY*.10f,c-Vector3.UnitY*.10f,b-Vector3.UnitY*.10f,new Color(6,2,11));
                    Quad(core,a,d,d-Vector3.UnitY*.10f,a-Vector3.UnitY*.10f,new Color(24,7,39));
                    Quad(edges,a,d,d-cast.Direction*.035f*scale,a-cast.Direction*.035f*scale,new Color(183,112,242,245));
                    Quad(edges,b,c,c+cast.Direction*.025f*scale,b+cast.Direction*.025f*scale,new Color(104,54,169,235));
                    Line(edges,a,d,new Color(215,163,255,240));
                }
                if(!low && cast.Motion.Phase!=WorldCutPhase.Windup)
                {
                    // At most twelve deterministic serrated cracks, fading along the swept path.
                    for(int trail=0;trail<12;trail++)
                    {
                        float distance=cast.Motion.Distance-.4f-trail*1.6f;if(distance<0)break;
                        Vector3 origin=cast.Origin+cast.Direction*distance;
                        Vector3 previous=origin-Vector3.UnitY*.65f;
                        byte alpha=(byte)Math.Max(0,185-trail*13);
                        for(int j=1;j<=6;j++)
                        {
                            Vector3 next=origin+Vector3.UnitY*(-.65f+j*.22f)+cast.Right*((j%2==0?-.12f:.15f)*(1-collapse));
                            Line(edges,previous,next,new Color(117,57,174,(int)alpha));previous=next;
                        }
                    }
                }
                if(collapse>=WorldCutRules.CloseImpact)
                {
                    float q=Math.Min(1,(collapse-WorldCutRules.CloseImpact)/(WorldCutRules.CloseEnd-WorldCutRules.CloseImpact));
                    int spokes=low?8:16;byte alpha=(byte)(230*(1-q));
                    for(int i=0;i<spokes;i++)
                    {
                        float angle=(float)(i*Math.PI*2/spokes);
                        Vector3 axis=cast.Right*(float)Math.Cos(angle)+Vector3.UnitY*(float)Math.Sin(angle);
                        Vector3 tangent=cast.Right*(-(float)Math.Sin(angle))+Vector3.UnitY*(float)Math.Cos(angle);
                        Vector3 end=center+axis*(.3f+2.6f*q);
                        Quad(edges,center+tangent*.035f,end+tangent*.02f,end-tangent*.02f,center-tangent*.035f,new Color(160,91,224,(int)alpha));
                    }
                }
            }
            renderer.Flush(camera.ViewProjectionMatrix);
        }
        private static void Ribbon(WorldCutRuntime.Cast cast,Vector3 center,float t,float scale,out Vector3 outer,out Vector3 inner)
        {
            float side,forward,width;WorldCutRules.Crescent(t,out side,out forward,out width);
            Vector3 bank=cast.Right*side+Vector3.UnitY*(side*.08f);
            outer=center+(bank+cast.Direction*forward)*scale;
            inner=outer-cast.Direction*(width*scale);
        }
        private void Quad(FlatBatch3D batch,Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color)
        { if(triangles+2>1024)return;triangles+=2;batch.QueueQuad(a,b,c,d,color); }
        private void Line(FlatBatch3D batch,Vector3 a,Vector3 b,Color color)
        { if(lines>=256)return;lines++;batch.QueueLine(a,b,color); }
        public void Dispose() { renderer.Clear(); }
    }
}

using System;
using Engine;
using Engine.Graphics;
using Game;
namespace SCIEPort.Features.AbyssReaper
{
    // Per-swing geometry only. No targets, combat callbacks, particles or terrain writes.
    internal sealed class AbyssMeleeScene : IDisposable
    {
        private readonly PrimitivesRenderer3D renderer=new PrimitivesRenderer3D();
        internal void Queue(ComponentPlayer player,int stage,float phase)
        {
            if(phase<.22f||phase>.82f)return;
            Vector3 forward=player.ComponentCreatureModel.EyeRotation.GetForwardVector();forward.Y=0;
            if(forward.LengthSquared()<.001f)return;
            forward=Vector3.Normalize(forward);Vector3 side=Vector3.Cross(forward,Vector3.UnitY);
            Vector3 center=player.ComponentCreatureModel.EyePosition-Vector3.UnitY*.55f;
            float radius=stage==3?4.1f:3.45f,angle=AbyssMeleeMotion.TrailAngle(stage,phase);
            float fade=MathUtils.Saturate((.82f-phase)/.22f),direction=stage==1?1:-1;
            var core=renderer.FlatBatch(-2,DepthStencilState.DepthRead,RasterizerState.CullNone,BlendState.AlphaBlend);
            var edge=renderer.FlatBatch(-1,DepthStencilState.DepthRead,RasterizerState.CullNone,BlendState.AlphaBlend);
            for(int i=0;i<14;i++)
            {
                float a=angle+direction*i*.065f,b=a+direction*.065f;
                float width=.05f+.18f*(1-i/14f),alpha=(1-i/14f)*fade;
                Vector3 p=Point(center,forward,side,radius,a,stage),q=Point(center,forward,side,radius,b,stage);
                Vector3 inner=Point(center,forward,side,radius-width*2,a,stage),innerNext=Point(center,forward,side,radius-width*2,b,stage);
                core.QueueQuad(p,q,innerNext,inner,new Color(7,1,15,(int)(220*alpha)));
                edge.QueueLine(p,q,new Color(204,132,255,(int)(245*alpha)));
            }
        }
        private static Vector3 Point(Vector3 center,Vector3 forward,Vector3 side,float radius,float angle,int stage)
        {
            float vertical=stage==3?(float)Math.Cos(angle)*.55f:stage==2?(float)Math.Sin(angle)*.38f:0;
            return center+(forward*(float)Math.Cos(angle)+side*(float)Math.Sin(angle))*radius+Vector3.UnitY*vertical;
        }
        internal void Flush(Camera camera){renderer.Flush(camera.ViewProjectionMatrix);}
        public void Dispose(){renderer.Clear();}
    }
}

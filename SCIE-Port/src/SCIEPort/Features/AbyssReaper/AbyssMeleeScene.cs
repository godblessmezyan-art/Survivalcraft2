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
            Vector3 forward=player.ComponentCreatureModel.EyeRotation.GetForwardVector();forward.Y=0;
            if(forward.LengthSquared()<.001f)return;
            forward=Vector3.Normalize(forward);
            QueueGeometry(player.ComponentCreatureModel.EyePosition-Vector3.UnitY*.28f,forward,stage,phase);
        }
        internal int QueueGeometry(Vector3 center,Vector3 forward,int stage,float phase)
        {
            if(!WorldCutRules.Finite(phase)||phase<.22f||phase>=.9f||stage<0||stage>2)return 0;
            Vector3 side=Vector3.Cross(forward,Vector3.UnitY);
            float radius=4.8f,angle=AbyssMeleeMotion.TrailAngle(stage,phase);
            float fade=MathUtils.Saturate((.9f-phase)/.25f),direction=stage==1?1:-1;
            var core=renderer.FlatBatch(-2,DepthStencilState.DepthRead,RasterizerState.CullNone,BlendState.NonPremultiplied);
            var edge=renderer.FlatBatch(-1,DepthStencilState.DepthRead,RasterizerState.CullNone,BlendState.NonPremultiplied);
            for(int i=0;i<24;i++)
            {
                float a=angle+direction*i*.075f,b=a+direction*.075f;
                float width=.78f*(float)Math.Pow(Math.Max(0,Math.Sin(i*Math.PI/24)),.65),nextWidth=.78f*(float)Math.Pow(Math.Max(0,Math.Sin((i+1)*Math.PI/24)),.65),alpha=(1-i/24f)*fade;
                Vector3 p=Point(center,forward,side,radius,a,stage),q=Point(center,forward,side,radius,b,stage);
                Vector3 inner=Point(center,forward,side,radius-width*2,a,stage),innerNext=Point(center,forward,side,radius-nextWidth*2,b,stage);
                Vector3 height=Vector3.UnitY*(width*(.55f+.07f*(float)Math.Sin(i*1.9f+stage))),nextHeight=Vector3.UnitY*(nextWidth*(.55f+.07f*(float)Math.Sin((i+1)*1.9f+stage)));
                core.QueueQuad(p+height,q+nextHeight,innerNext+nextHeight,inner+height,new Color(5,0,12,(int)(245*alpha)));
                core.QueueQuad(p-height,q-nextHeight,q+nextHeight,p+height,new Color(9,1,19,(int)(245*alpha)));
                core.QueueQuad(inner-height,innerNext-nextHeight,q-nextHeight,p-height,new Color(12,2,25,(int)(245*alpha)));
                edge.QueueQuad(p+height,q+nextHeight,q+nextHeight+Vector3.UnitY*.065f,p+height+Vector3.UnitY*.065f,new Color(180,82,243,(int)(240*alpha)));
                edge.QueueQuad(p-height,q-nextHeight,q-nextHeight-Vector3.UnitY*.035f,p-height-Vector3.UnitY*.035f,new Color(103,33,177,(int)(200*alpha)));
                edge.QueueQuad(p+height*1.7f,q+nextHeight*1.7f,q-nextHeight*1.7f,p-height*1.7f,new Color(104,34,165,(int)(38*alpha)));
            }
            return 24*12;
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

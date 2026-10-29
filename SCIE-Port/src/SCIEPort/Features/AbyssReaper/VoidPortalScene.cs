using System;
using Engine;
using Engine.Graphics;
using Game;
namespace SCIEPort.Features.AbyssReaper
{
    // Presentation only: no movement, world writes or collision decisions.
    internal sealed class VoidPortalScene : IDisposable
    {
        private readonly PrimitivesRenderer3D renderer=new PrimitivesRenderer3D();
        private readonly int quality;
        internal VoidPortalScene(int quality){this.quality=VoidSkillRules.Quality(quality);}
        internal void Draw(Camera camera,VoidPortalRegistry registry,double now)
        {
            if(registry==null)return;
            var core=renderer.FlatBatch(-2,DepthStencilState.DepthRead,RasterizerState.CullNone,BlendState.Opaque);
            var light=renderer.FlatBatch(-1,DepthStencilState.DepthRead,RasterizerState.CullNone,BlendState.AlphaBlend);
            int count=0;
            foreach(var portal in registry.Records)
            {
                if(!portal.Active || count>=24)continue;
                Vector3 center=new Vector3(portal.X,portal.Y+2f,portal.Z);
                if(Vector3.DistanceSquared(camera.ViewPosition,center)>64f*64f)continue;
                count++;
                Vector3 axis=portal.Facing==0?Vector3.UnitX:Vector3.UnitZ;
                int segments=VoidSkillRules.VfxSegments(quality,8,16,24);
                for(int i=0;i<segments;i++)
                {
                    float a=(float)(i*Math.PI*2/segments),b=(float)((i+1)*Math.PI*2/segments);
                    Vector3 p=center+axis*(1.35f*(float)Math.Cos(a))+Vector3.UnitY*(1.85f*(float)Math.Sin(a));
                    Vector3 q=center+axis*(1.35f*(float)Math.Cos(b))+Vector3.UnitY*(1.85f*(float)Math.Sin(b));
                    core.QueueTriangle(center,p,q,new Color(5,1,10));
                    light.QueueLine(p,q,new Color(153,79,212,235));
                    Vector3 inset=center+(p-center)*.85f;
                    light.QueueLine(p,inset,new Color(94,42,148,130));
                }
                for(int i=0;i<3;i++)
                {
                    float angle=(float)(now*.35+i*2.0944);
                    Vector3 a=center+axis*((float)Math.Cos(angle)*.9f)+Vector3.UnitY*((float)Math.Sin(angle)*1.25f);
                    Vector3 b=center+axis*((float)Math.Cos(angle+.8f)*.45f)+Vector3.UnitY*((float)Math.Sin(angle+.8f)*.65f);
                    light.QueueLine(a,b,new Color(88,38,141,180));
                }
            }
            renderer.Flush(camera.ViewProjectionMatrix);
        }
        public void Dispose(){renderer.Clear();}
    }
}

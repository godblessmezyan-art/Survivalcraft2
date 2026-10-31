using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;
namespace SCIEPort.Features.AbyssReaper
{
    // World-space closed volumes and tapered fracture surfaces; no particle emitter or damage path.
    internal sealed class VoidRiftScene:IDisposable
    {
        private readonly PrimitivesRenderer3D renderer=new PrimitivesRenderer3D();
        private int triangles;
        private void Quad(FlatBatch3D b,Vector3 a,Vector3 c,Vector3 d,Vector3 e,Color color){b.QueueQuad(a,c,d,e,color);triangles+=2;}
        private void Tri(FlatBatch3D b,Vector3 a,Vector3 c,Vector3 d,Color color){b.QueueTriangle(a,c,d,color);triangles++;}
        internal int Queue(Vector3 from,Vector3 to,Vector3 direction,byte skill,float elapsed,int quality,uint seed)
        {
            int before=triangles;double duration=skill==VoidSpaceRules.Cut?4:3;
            float fade=VoidSpaceRules.Fade(elapsed,duration);if(fade<=0)return 0;
            var dark=renderer.FlatBatch(0,DepthStencilState.DepthRead,RasterizerState.CullNone,BlendState.NonPremultiplied);
            var light=renderer.FlatBatch(1,DepthStencilState.DepthRead,RasterizerState.CullNone,BlendState.NonPremultiplied);
            Vector3 flat=new Vector3(direction.X,0,direction.Z);if(flat.LengthSquared()<.01f)flat=Vector3.UnitZ;else flat=Vector3.Normalize(flat);
            Vector3 right=Vector3.Cross(flat,Vector3.UnitY);
            float close=(float)MathUtils.Saturate((float)((duration-elapsed)/.45));
            Color black=new Color(2,0,5,(int)(255*fade)),edge=new Color(126,51,194,(int)(210*fade));
            if(skill==VoidSpaceRules.Cut)
            {
                int n=VoidSkillRules.VfxSegments(quality,12,18,24);float distance=Vector3.Distance(from,to);
                for(int i=0;i<n;i++)
                {
                    float a=i/(float)n,b=(i+1f)/n;
                    Vector3 p=Vector3.Lerp(from,to,a)+right*(.12f*(float)Math.Sin(a*31+seed%31)),q=Vector3.Lerp(from,to,b)+right*(.12f*(float)Math.Sin(b*31+seed%31));
                    float wa=(.20f+.38f*(float)Math.Sin(a*Math.PI))*close,wb=(.20f+.38f*(float)Math.Sin(b*Math.PI))*close;
                    Vector3 thick=right*.10f;
                    Quad(dark,p-Vector3.UnitY*wa-thick,q-Vector3.UnitY*wb-thick,q+Vector3.UnitY*wb-thick,p+Vector3.UnitY*wa-thick,black);
                    Quad(dark,p+Vector3.UnitY*wa+thick,q+Vector3.UnitY*wb+thick,q-Vector3.UnitY*wb+thick,p-Vector3.UnitY*wa+thick,black);
                    Quad(dark,p+Vector3.UnitY*wa-thick,q+Vector3.UnitY*wb-thick,q+Vector3.UnitY*wb+thick,p+Vector3.UnitY*wa+thick,new Color(22,5,38,(int)(fade*250)));
                    Quad(light,p+Vector3.UnitY*wa-thick,q+Vector3.UnitY*wb-thick,q+Vector3.UnitY*(wb+.035f)-thick,p+Vector3.UnitY*(wa+.035f)-thick,edge);
                }
                // A wide torn end face preserves the six-unit slash silhouette from the caster's view.
                Slit(dark,light,to,right,flat,3, .65f*close,fade,quality,seed,elapsed);
            }
            else
            {
                Slit(dark,light,from+Vector3.UnitY,right,flat,.9f*close,1.45f,fade,quality,seed,elapsed);
                Slit(dark,light,to+Vector3.UnitY,right,flat,.9f*close,1.45f,fade,quality,seed+7,elapsed);
            }
            return triangles-before;
        }
        private void Slit(FlatBatch3D dark,FlatBatch3D light,Vector3 center,Vector3 right,Vector3 forward,float width,float height,float fade,int quality,uint seed,float time)
        {
            int n=VoidSkillRules.VfxSegments(quality,16,24,32);Vector3 thickness=forward*.11f;
            for(int i=0;i<n;i++)
            {
                float a=(float)Math.PI*2*i/n,b=(float)Math.PI*2*(i+1)/n;
                float ra=1+.045f*(float)Math.Sin(a*7+seed%13)+.025f*(float)Math.Sin(a*13+time*2),rb=1+.045f*(float)Math.Sin(b*7+seed%13)+.025f*(float)Math.Sin(b*13+time*2);
                Vector3 p=center+right*((float)Math.Cos(a)*width*ra)+Vector3.UnitY*((float)Math.Sin(a)*height*ra),q=center+right*((float)Math.Cos(b)*width*rb)+Vector3.UnitY*((float)Math.Sin(b)*height*rb);
                Color black=new Color(2,0,5,(int)(255*fade));Tri(dark,center-thickness,p-thickness,q-thickness,black);Tri(dark,center+thickness,q+thickness,p+thickness,black);
                Quad(dark,p-thickness,p+thickness,q+thickness,q-thickness,new Color(33,9,53,(int)(255*fade)));
                Vector3 po=center+(p-center)*1.045f,qo=center+(q-center)*1.045f;
                Quad(light,p-thickness,q-thickness,qo-thickness,po-thickness,new Color(147,64,216,(int)(210*fade)));
            }
            int shards=VoidSkillRules.VfxSegments(quality,3,5,7);
            for(int i=0;i<shards;i++)
            {
                float phase=(time*.6f+i*.173f)%1,angle=i*2.39996f;
                Vector3 radial=right*(float)Math.Cos(angle)+Vector3.UnitY*(float)Math.Sin(angle),p=center+radial*((1-phase)*(height+1)+.2f);
                Tri(light,p,p+right*.12f,p+radial*.3f,new Color(98,33,153,(int)(fade*130)));
            }
        }
        internal void Draw(Camera camera,IList<VoidSpaceRuntime.Effect> effects,double now,int quality)
        {
            renderer.Clear();triangles=0;
            try{foreach(var e in effects)Queue(e.From,e.To,e.Direction,e.Skill,(float)(now-e.Started),quality,e.Seed);renderer.Flush(camera.ViewProjectionMatrix);}
            finally{renderer.Clear();}
        }
        public void Dispose(){renderer.Clear();}
    }
}

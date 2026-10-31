using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;
namespace SCIEPort.Features.AbyssReaper
{
    // Geometric approximation of fractured reality: never claims to copy/slice native scene pixels.
    internal sealed class VoidShatterScene:IDisposable
    {
        internal sealed class Fragment{internal Vector3 Rest;internal float Spin,Scale;internal readonly Vector3[] Vertices=new Vector3[5];}
        internal sealed class Geometry{internal readonly Fragment[][] Levels=new Fragment[3][];}
        // Presentation footprint only. Combat radius and authoritative aim stay independent.
        private const float WidthScale=7.2f,HeightScale=4.5f;
        private readonly PrimitivesRenderer3D renderer=new PrimitivesRenderer3D();
        private static float Next(ref uint seed){seed=unchecked(seed*1664525+1013904223);return (seed>>8)/16777216f;}
        internal static Geometry Create(uint seed)
        {
            var result=new Geometry();
            for(int quality=0;quality<3;quality++)
            {
                int inner=4+quality*2,total=VoidShatterRules.Fragments(quality);
                var pieces=result.Levels[quality]=new Fragment[total];
                for(int ring=0;ring<2;ring++)
                {
                    int count=ring==0?inner:total-inner;float phase=ring==0?0:.17f;
                    var angles=new float[count+1];var radii=new float[count+1];
                    for(int i=0;i<count;i++){angles[i]=(float)(i*Math.PI*2/count)+phase+(Next(ref seed)-.5f)*.35f/count;radii[i]=5.6f+Next(ref seed)*.8f;}
                    angles[count]=angles[0]+2*MathUtils.PI;radii[count]=radii[0];
                    for(int i=0;i<count;i++)
                    {
                        float a=angles[i],b=angles[i+1],mid=(a+b)*.5f,near=ring==0?0:2.3f,far=ring==0?2.3f:radii[i];
                        var f=new Fragment{Spin=(Next(ref seed)-.5f)*2.4f,Scale=1};
                        f.Vertices[0]=new Vector3((float)Math.Cos(a)*near,(float)Math.Sin(a)*near,0);
                        f.Vertices[1]=new Vector3((float)Math.Cos(a)*far,(float)Math.Sin(a)*far,0);
                        f.Vertices[2]=new Vector3((float)Math.Cos(mid)*(far+.25f),(float)Math.Sin(mid)*(far+.25f),0);
                        float farB=ring==0?2.3f:radii[i+1];
                        f.Vertices[3]=new Vector3((float)Math.Cos(b)*farB,(float)Math.Sin(b)*farB,0);
                        f.Vertices[4]=new Vector3((float)Math.Cos(b)*near,(float)Math.Sin(b)*near,0);
                        for(int j=0;j<5;j++)f.Rest+=f.Vertices[j]/5;
                        for(int j=0;j<5;j++)f.Vertices[j]-=f.Rest;
                        // A joined fractured aperture separates into individual solid shards, rather than a card cloud.
                        pieces[(ring==0?0:inner)+i]=f;
                    }
                }
            }
            return result;
        }
        private void Quad(FlatBatch3D b,Vector3 a,Vector3 c,Vector3 d,Vector3 e,Color color){b.QueueQuad(a,c,d,e,color);}
        private static Vector3 Transform(Vector3 p,Vector3 center,Vector3 right,Vector3 up,Vector3 direction) => center+right*(p.X*WidthScale)+up*(p.Y*HeightScale)+direction*p.Z;
        internal int Queue(Geometry geometry,Vector3 center,Vector3 direction,float elapsed,int quality)
            => QueueCore(geometry,center,direction,elapsed,quality,false);
        // The caster's FPP sees reality fracture across the foreground as well as distant scenery.
        // These are the same world-fixed shards, never a camera-following fullscreen black quad.
        internal int QueueForeground(Geometry geometry,Vector3 center,Vector3 direction,float elapsed,int quality)
            => QueueCore(geometry,center,direction,elapsed,quality,true);
        private int QueueCore(Geometry geometry,Vector3 center,Vector3 direction,float elapsed,int quality,bool foreground)
        {
            if(geometry==null||elapsed>=(float)VoidShatterRules.Duration||!VoidSpaceRules.Live(elapsed,VoidShatterRules.Duration))return 0;
            Vector3 right=Vector3.Cross(direction,Vector3.UnitY);if(right.LengthSquared()<.01f)right=Vector3.UnitX;else right=Vector3.Normalize(right);
            Vector3 up=Vector3.Normalize(Vector3.Cross(right,direction));
            var depth=foreground?DepthStencilState.None:DepthStencilState.DepthRead;
            var dark=renderer.FlatBatch(foreground?2:0,depth,RasterizerState.CullNone,BlendState.NonPremultiplied);
            var edge=renderer.FlatBatch(foreground?3:1,depth,RasterizerState.CullNone,BlendState.NonPremultiplied);
            float opening=VoidHorizonVisualRules.Smooth(elapsed/.3f),broken=VoidShatterRules.Break(elapsed),collapse=VoidShatterRules.Collapse(elapsed),blast=VoidShatterRules.Blast(elapsed);
            float fade=(float)Math.Sqrt(1-blast),offset=(1+broken*.42f)*(1-collapse)+(elapsed>=1.4?blast*2.5f:0);
            int count=VoidShatterRules.Fragments(quality),triangles=0;
            for(int i=0;i<count;i++)
            {
                var f=geometry.Levels[VoidSkillRules.Quality(quality)][i];Vector3 at=f.Rest*offset+Vector3.UnitZ*((float)Math.Sin(i*2.39996f)*1.4f*broken*(1-collapse));
                Matrix rotation=Matrix.CreateFromYawPitchRoll(f.Spin*broken*.6f,f.Spin*broken*.45f,f.Spin*broken);
                float scale=(1-.92f*collapse)*(elapsed>=1.4?1+blast:1);
                Vector3 pivot=Transform(at,center,right,up,direction);
                for(int j=0;j<5;j++)
                {
                    Vector3 a=Vector3.TransformNormal(f.Vertices[j]*scale,rotation),b=Vector3.TransformNormal(f.Vertices[(j+1)%5]*scale,rotation);
                    Vector3 pa=Transform(at+a,center,right,up,direction),pb=Transform(at+b,center,right,up,direction);
                    if(elapsed>=.3f)
                    {
                        Vector3 thickness=Transform(Vector3.TransformNormal(new Vector3(0,0,.14f*scale),rotation),Vector3.Zero,right,up,direction);
                        dark.QueueTriangle(pivot,pa,pb,new Color(4+i%3*2,1,9+i%4*3,(int)(255*fade)));
                        dark.QueueTriangle(pivot+thickness,pb+thickness,pa+thickness,new Color(15,4,28,(int)(255*fade)));
                        Quad(dark,pa,pb,pb+thickness,pa+thickness,new Color(48,12,75,(int)(255*fade)));triangles+=4;
                    }
                    Vector3 outerA=Transform(at+a*1.045f,center,right,up,direction),outerB=Transform(at+b*1.045f,center,right,up,direction);
                    Quad(edge,pa,pb,outerB,outerA,new Color(129+i%3*12,45,194,(int)(220*fade*opening)));triangles+=2;
                }
                // Branching connected fissure surfaces appear before the opaque fragments separate.
                if(elapsed<.3f)
                {
                    Vector3 tip=Transform(f.Rest*opening,center,right,up,direction),width=right*.025f;
                    Quad(edge,center-width,center+width,tip+width*.2f,tip-width*.2f,new Color(94,26,148,(int)(230*opening)));triangles+=2;
                }
            }
            if(elapsed>=1.4f)
            {
                int segments=VoidSkillRules.VfxSegments(quality,24,36,48);float radius=.2f+blast*blast*10,width=.15f+radius*.065f;
                for(int i=0;i<segments;i++)
                {
                    float a=(float)(i*Math.PI*2/segments),b=(float)((i+1)*Math.PI*2/segments);
                    Vector3 p=right*(float)Math.Cos(a)+up*(float)Math.Sin(a),q=right*(float)Math.Cos(b)+up*(float)Math.Sin(b);
                    Vector3 pa=Transform(p*(radius-width),center,right,up,direction),pb=Transform(q*(radius-width),center,right,up,direction);
                    Quad(edge,pa,pb,Transform(q*radius,center,right,up,direction),Transform(p*radius,center,right,up,direction),new Color(180,104,230,(int)(240*fade)));triangles+=2;
                    Vector3 hz=new Vector3((float)Math.Cos(a),0,(float)Math.Sin(a)),hq=new Vector3((float)Math.Cos(b),0,(float)Math.Sin(b));
                    float shockScale=3.2f;
                    Quad(edge,center+hz*radius*.9f*shockScale,center+hq*radius*.9f*shockScale,center+hq*(radius+.2f)*shockScale,center+hz*(radius+.2f)*shockScale,new Color(86,36,142,(int)(180*fade)));triangles+=2;
                }
            }
            return triangles;
        }
        internal void Draw(Camera camera,IList<VoidSpaceRuntime.ShatterCast> casts,double now,int quality)
        {
            renderer.Clear();
            try
            {
                // Keep foreground batches after observer batches when several players cast together.
                foreach(var cast in casts)
                    if(!(camera is FppCamera&&camera.GameWidget.PlayerData.ComponentPlayer==cast.Owner))Queue(cast.Geometry,cast.Center,cast.Direction,(float)(now-cast.Started),quality);
                foreach(var cast in casts)
                    if(camera is FppCamera&&camera.GameWidget.PlayerData.ComponentPlayer==cast.Owner)QueueForeground(cast.Geometry,cast.Center,cast.Direction,(float)(now-cast.Started),quality);
                renderer.Flush(camera.ViewProjectionMatrix);
            }
            finally{renderer.Clear();}
        }
        public void Dispose(){renderer.Clear();}
    }
}

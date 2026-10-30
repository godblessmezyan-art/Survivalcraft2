using System;
using Engine;
using Engine.Graphics;
using Game;
namespace SCIEPort.Features.AbyssReaper
{
    // A complete camera-centred environment, drawn after native terrain/models/particles/overlays.
    // World-space facing is fixed on acceptance; looking away never drags the black hole along.
    internal sealed class VoidHorizonScene : IDisposable
    {
        private readonly PrimitivesRenderer3D renderer=new PrimitivesRenderer3D();
        private int triangles;
        private const float Pi=(float)Math.PI;
        private static readonly float[] Latitudes={-Pi/2,-.6f,-.14f,-.035f,-.008f,0,.008f,.035f,.14f,.6f,Pi/2};
        private FlatBatch3D Batch(int layer,bool opaque=false) => renderer.FlatBatch(layer,DepthStencilState.None,RasterizerState.CullNone,opaque?BlendState.Opaque:BlendState.AlphaBlend);
        private void Tri(FlatBatch3D batch,Vector3 a,Vector3 b,Vector3 c,Color ca,Color cb,Color cc)
        {batch.QueueTriangle(a,b,c,ca,cb,cc);triangles++;}
        private void Quad(FlatBatch3D batch,Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color)
        {batch.QueueQuad(a,b,c,d,color);triangles+=2;}
        internal void Draw(Camera camera,Vector3 facing,float elapsed,int quality)
        {
            try { Queue(camera.ViewPosition,facing,elapsed,quality);renderer.Flush(camera.ViewProjectionMatrix); }
            finally { renderer.Clear(); }
        }
        // CPU-only mesh generation can be checked against the real Engine without a GPU/window.
        internal int Queue(Vector3 origin,Vector3 facing,float t,int quality)
        {
            renderer.Clear();triangles=0;
            if(t>=(float)VoidSkillRules.HorizonDuration || !VoidHorizonRules.Live(t) || !WorldCutDamage.Finite(origin) || !WorldCutDamage.Finite(facing) || facing.LengthSquared()<.01f)return 0;
            facing=new Vector3(facing.X,0,facing.Z);
            if(facing.LengthSquared()<.01f)return 0;
            facing=Vector3.Normalize(facing);
            Vector3 right=Vector3.Normalize(Vector3.Cross(facing,Vector3.UnitY));
            int segments=VoidSkillRules.VfxSegments(quality,48,72,96);
            Environment(origin,segments,t);
            if(t>=VoidSkillRules.DomainStart)BlackHole(origin,facing,right,t,segments);
            if(t>=VoidSkillRules.ExpansionStart)Aperture(origin,facing,right,VoidHorizonVisualRules.Expansion(t),segments,t);
            return triangles;
        }
        private static Vector3 Sphere(float longitude,float latitude)
            => new Vector3((float)Math.Cos(longitude)*(float)Math.Cos(latitude),(float)Math.Sin(latitude),(float)Math.Sin(longitude)*(float)Math.Cos(latitude));
        private static Color Sky(float y,float t)
        {
            // An empty plane below a thin horizon; no stars, creatures, terrain or magic circles.
            float glow=(float)Math.Exp(-Math.Abs(y)*90);
            Color baseColor=y<0?new Color(4,5,10):new Color(7,5,17);
            return Color.Lerp(baseColor,new Color(99,84,135),glow*.65f);
        }
        private void Environment(Vector3 origin,int n,float t)
        {
            float opacity=VoidHorizonVisualRules.Entry(t);
            var batch=Batch(0,opacity>=1);
            for(int j=0;j<Latitudes.Length-1;j++)for(int i=0;i<n;i++)
            {
                float a=2*Pi*i/n,b=2*Pi*(i+1)/n;
                Vector3 da=Sphere(a,Latitudes[j]),db=Sphere(b,Latitudes[j]),dc=Sphere(b,Latitudes[j+1]),dd=Sphere(a,Latitudes[j+1]);
                Color low=Sky(da.Y,t),high=Sky(dc.Y,t);low.A=high.A=(byte)(255*opacity);
                Tri(batch,origin+da*32,origin+db*32,origin+dc*32,low,low,high);
                Tri(batch,origin+da*32,origin+dc*32,origin+dd*32,low,high,high);
            }
        }
        private Vector3 Cap(Vector3 forward,Vector3 right,Vector3 up,float a,float theta)
            => forward*(float)Math.Cos(theta)+(right*(float)Math.Cos(a)+up*(float)Math.Sin(a))*(float)Math.Sin(theta);
        private void Aperture(Vector3 origin,Vector3 forward,Vector3 right,float progress,int n,float t)
        {
            // The expansion is centred on the hole, including its elevation above the horizon.
            Vector3 normal=Vector3.Normalize(forward*24+Vector3.UnitY*6.8f),up=Vector3.Normalize(Vector3.Cross(right,normal));
            var fill=Batch(6,true);var rim=Batch(7);
            float angle=VoidHorizonVisualRules.ExpansionAngle(t);
            const int bands=4;
            for(int i=0;i<n;i++)
            {
                float a=2*Pi*i/n,b=2*Pi*(i+1)/n,ea=angle,eb=angle;
                for(int j=0;j<bands;j++)
                {
                    Vector3 da=Cap(normal,right,up,a,ea*j/bands),db=Cap(normal,right,up,b,eb*j/bands),dc=Cap(normal,right,up,b,eb*(j+1)/bands),dd=Cap(normal,right,up,a,ea*(j+1)/bands);
                    Color inner=new Color(1,0,4),edge=new Color(13,4,26);
                    Color low=Color.Lerp(inner,edge,j/(float)bands),high=Color.Lerp(inner,edge,(j+1)/(float)bands);
                    Tri(fill,origin+da*32,origin+db*32,origin+dc*32,low,low,high);
                    Tri(fill,origin+da*32,origin+dc*32,origin+dd*32,low,high,high);
                }
                Color glow=Color.Lerp(new Color(190,127,255),new Color(113,226,255),.5f+.5f*(float)Math.Cos(a));
                for(int band=3;band>=0;band--)
                {
                    float outer=Math.Min(Pi,angle+.012f*(band+1)),inner=Math.Min(Pi,angle+.012f*band);
                    Color haze=glow;haze.A=(byte)(90/(1+band*band));
                    Quad(rim,origin+Cap(normal,right,up,a,inner)*31,origin+Cap(normal,right,up,b,inner)*31,
                        origin+Cap(normal,right,up,b,outer)*31,origin+Cap(normal,right,up,a,outer)*31,haze);
                }
                float width=.002f+.002f*(float)Math.Sin(Pi*progress);
                Quad(rim,origin+Cap(normal,right,up,a,ea)*31,origin+Cap(normal,right,up,b,eb)*31,
                    origin+Cap(normal,right,up,b,Math.Max(0,eb-width))*31,origin+Cap(normal,right,up,a,Math.Max(0,ea-width))*31,new Color(231,226,255,220));
            }
        }
        private void BlackHole(Vector3 origin,Vector3 facing,Vector3 right,float t,int n)
        {
            float emergence=VoidHorizonVisualRules.Hole(t);
            // The formed event horizon remains large as the engulfing front starts from its centre.
            float radius=7.6f*(.15f+.85f*emergence);
            Vector3 center=origin+facing*24+Vector3.UnitY*6.8f;
            Vector3 normal=Vector3.Normalize(center-origin),up=Vector3.Normalize(Vector3.Cross(right,normal));
            Vector3 diskRight=right*.985f+up*.173f,diskUp=up*.985f-right*.173f;
            // A broad, dim lens halo, progressively tighter towards the razor event horizon.
            for(int band=0;band<7;band++)
            {
                float inner=1.0f+band*.065f,outer=inner+.065f;
                Ring(Batch(1),center,right,up,radius*inner,radius*outer,1,n,t,new Color(123,132,220,(int)((7-band)*(7-band)*3*emergence)),(int)((6-band)*(6-band)*3*emergence));
            }
            Disk(Batch(2),center,diskRight,diskUp,radius,n,t,emergence,false);
            var core=Batch(3,true);
            for(int i=0;i<n;i++)
            {
                float a=2*Pi*i/n,b=2*Pi*(i+1)/n;
                Tri(core,center,Point(center,right,up,radius,a,1),Point(center,right,up,radius,b,1),new Color(0,0,2),new Color(0,0,2),new Color(0,0,2));
            }
            Ring(Batch(4),center,right,up,radius*1.001f,radius*1.024f,1,n,t,new Color(239,224,255,(int)(250*emergence)));
            // Lensed rear disk bends over the core. The near half crosses in front: actual occlusion.
            Lens(Batch(4),center,diskRight,diskUp,radius,n,t,emergence);
            Disk(Batch(5),center,diskRight,diskUp,radius,n,t,emergence,true);
        }
        private void Lens(FlatBatch3D batch,Vector3 center,Vector3 right,Vector3 up,float radius,int n,float t,float fade)
        {
            // The far disk is seen twice through the gravitational lens: a broad arch over the core,
            // tapering back into the thin equatorial disk at both sides. Not a second circular outline.
            for(int band=0;band<4;band++)for(int i=0;i<n/2;i++)
            {
                float a=Pi*i/(n/2),b=Pi*(i+1)/(n/2);
                float ra=radius*(1.025f+band*.035f*(float)Math.Sin(a)),rb=radius*(1.025f+band*.035f*(float)Math.Sin(b));
                float wa=radius*.034f*(float)Math.Sin(a),wb=radius*.034f*(float)Math.Sin(b);
                Color color=Color.Lerp(new Color(192,162,231),new Color(80,48,130),band/3f);
                color.A=(byte)(fade*(150-band*25)*(.78f+.22f*(float)Math.Sin(a*3-t*.24f)));
                Quad(batch,Point(center,right,up,ra,a,1),Point(center,right,up,rb,b,1),Point(center,right,up,rb+wb,b,1),Point(center,right,up,ra+wa,a,1),color);
            }
        }
        private static Vector3 Point(Vector3 center,Vector3 right,Vector3 up,float radius,float angle,float flatten)
            => center+right*(radius*(float)Math.Cos(angle))+up*(radius*flatten*(float)Math.Sin(angle));
        private void Ring(FlatBatch3D batch,Vector3 center,Vector3 right,Vector3 up,float inner,float outer,float flatten,int n,float t,Color color,int outerAlpha=-1)
        {
            for(int i=0;i<n;i++)
            {
                float a=2*Pi*i/n,b=2*Pi*(i+1)/n;
                Color low=color,high=color;
                if(outerAlpha>=0)
                {
                    low=Color.Lerp(new Color(181,122,250,(int)color.A),new Color(93,220,250,(int)color.A),.5f+.5f*(float)Math.Cos(a));high=low;high.A=(byte)outerAlpha;
                }
                Vector3 pa=Point(center,right,up,inner,a,flatten),pb=Point(center,right,up,inner,b,flatten),pc=Point(center,right,up,outer,b,flatten),pd=Point(center,right,up,outer,a,flatten);
                Tri(batch,pa,pb,pc,low,low,high);Tri(batch,pa,pc,pd,low,high,high);
            }
        }
        private void Disk(FlatBatch3D batch,Vector3 center,Vector3 right,Vector3 up,float radius,int n,float t,float fade,bool front)
        {
            const int bands=6;
            for(int band=0;band<bands;band++)for(int i=0;i<n/2;i++)
            {
                float a=Pi*i/(n/2)+(front?Pi:0),b=Pi*(i+1)/(n/2)+(front?Pi:0);
                float flow=.68f+.22f*(float)Math.Sin(a*3-t*(.22f+band*.035f))+.10f*(float)Math.Sin(a*11+t*.3f+band);
                float inner=radius*(1.04f+band*.11f),outer=inner+radius*.105f;
                Color color=Color.Lerp(new Color(208,189,240),new Color(56,31,100),band/(float)(bands-1));
                color.A=(byte)(MathUtils.Saturate(fade*flow*(1-band*.10f))*210);
                Quad(batch,Point(center,right,up,inner,a,.24f),Point(center,right,up,inner,b,.24f),Point(center,right,up,outer,b,.24f),Point(center,right,up,outer,a,.24f),color);
            }
        }
        public void Dispose(){renderer.Clear();}
    }
}

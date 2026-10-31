using System;
using Engine;
using Engine.Graphics;
using Game;
namespace SCIEPort.Features.AbyssReaper
{
    // Native world depth remains intact in the late viewport pass. A shrinking 3D shell
    // passes LessEqual only BEHIND the front; closer mountains, water and models remain visible.
    // No depth texture, terrain edits, camera-facing screen mask or shared sky override.
    internal sealed class VoidHorizonScene : IDisposable
    {
        private readonly PrimitivesRenderer3D renderer=new PrimitivesRenderer3D();
        private int triangles;
        private const float Pi=(float)Math.PI;
        private static readonly float[] Latitudes={-Pi/2,-1.2f,-.8f,-.45f,-.2f,-.06f,0,.06f,.2f,.45f,.8f,1.2f,Pi/2};
        private bool worldDepth,projectHole;
        private float shell;
        private Vector3 viewForward;
        // Vertex colours below are straight RGBA; native AlphaBlend expects premultiplication.
        private FlatBatch3D Batch(int layer,bool opaque=false) => renderer.FlatBatch(layer,worldDepth?DepthStencilState.DepthRead:DepthStencilState.None,RasterizerState.CullNone,opaque?BlendState.Opaque:BlendState.NonPremultiplied);
        private static float Noise(Vector3 d) => 1+.012f*(float)Math.Sin(d.X*19+d.Z*13)+.008f*(float)Math.Sin(d.Y*27-d.Z*6);
        private Vector3 Project(Vector3 p)
        {
            if(projectHole)
            {
                float distance=p.Length();
                if(distance>=.001f){Vector3 d=p/distance;float limit=shell*Noise(d)*.995f;if(distance>limit)p=d*limit;}
            }
            // A front that has crossed the native near plane must still cover that ray.
            // Move only such forward vertices along their own ray onto the near plane;
            // their screen position is unchanged and all drawable world depth lies behind it.
            float z=Vector3.Dot(p,viewForward);
            if(worldDepth&&z>0&&z<.1005f)p*=.1005f/z;
            return p;
        }
        private void Tri(FlatBatch3D batch,Vector3 a,Vector3 b,Vector3 c,Color ca,Color cb,Color cc)
        {batch.QueueTriangle(Project(a),Project(b),Project(c),ca,cb,cc);triangles++;}
        private void Quad(FlatBatch3D batch,Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color)
        {Tri(batch,a,b,c,color,color,color);Tri(batch,a,c,d,color,color,color);}
        internal void Draw(Camera camera,Vector3 castOrigin,Vector3 facing,float elapsed,int quality,float visibility)
        {
            // Camera-relative vertices retain a fixed world origin while avoiding large-coordinate jitter.
            try { Queue(camera.ViewPosition,castOrigin,facing,elapsed,quality,visibility,camera.ViewDirection);renderer.Flush(camera.ViewMatrix.OrientationMatrix*camera.ProjectionMatrix); }
            finally { renderer.Clear(); }
        }
        // CPU-only mesh generation can be checked against the real Engine without a GPU/window.
        internal int Queue(Vector3 viewPosition,Vector3 castOrigin,Vector3 facing,float t,int quality,float visibility,Vector3 viewDirection)
        {
            renderer.Clear();triangles=0;
            if(t>=(float)VoidSkillRules.HorizonDuration || !VoidHorizonRules.Live(t) || !WorldCutDamage.Finite(viewPosition) || !WorldCutDamage.Finite(castOrigin) || !WorldCutDamage.Finite(facing) || !WorldCutDamage.Finite(viewDirection) || viewDirection.LengthSquared()<.01f || facing.LengthSquared()<.01f)return 0;
            viewForward=Vector3.Normalize(viewDirection);
            facing=new Vector3(facing.X,0,facing.Z);
            if(facing.LengthSquared()<.01f)return 0;
            facing=Vector3.Normalize(facing);
            Vector3 right=Vector3.Normalize(Vector3.Cross(facing,Vector3.UnitY));
            int segments=VoidSkillRules.VfxSegments(quality,32,48,64);
            worldDepth=t<(float)VoidSkillRules.DomainStart;projectHole=false;
            shell=VoidHorizonVisualRules.Radius(t,visibility);
            if(t>VoidSkillRules.ExpansionStart)Environment(segments,t);
            projectHole=worldDepth&&t>VoidSkillRules.ExpansionStart;
            Vector3 center=castOrigin-viewPosition+facing*160+Vector3.UnitY*46;
            if(t>0)BlackHole(center,facing,right,t,segments);
            Fractures(center,facing,right,t,quality);
            projectHole=false;
            if(t>VoidSkillRules.BurstStart)Explosion(center,facing,right,t,segments);
            return triangles;
        }
        private static Vector3 Sphere(float longitude,float latitude)
            => new Vector3((float)Math.Cos(longitude)*(float)Math.Cos(latitude),(float)Math.Sin(latitude),(float)Math.Sin(longitude)*(float)Math.Cos(latitude));
        private static Color Sky(float y)
        {
            // An empty plane below a thin horizon; no stars, creatures, terrain or magic circles.
            float glow=(float)Math.Exp(-Math.Abs(y)*90);
            Color baseColor=y<0?new Color(6,4,13):new Color(10,5,24);
            return Color.Lerp(baseColor,new Color(103,73,146),glow*.65f);
        }
        private void Environment(int n,float t)
        {
            bool complete=!worldDepth;
            // Glow first, then opaque void: the latter erases glow beyond the boundary,
            // leaving a soft bright band only where real scene depth intersects the front.
            int surfaces=complete?1:4;
            for(int surface=0;surface<surfaces;surface++)
            {
                bool fill=complete||surface==3;
                float width=Math.Min(4,Math.Max(.025f,shell*.055f));
                float radius=complete?16:shell-(fill?0:width*(1-surface*.32f));
                bool opaque=fill&&(complete||t>=1.5f);
                var batch=Batch(surface,opaque);
                for(int j=0;j<Latitudes.Length-1;j++)for(int i=0;i<n;i++)
                {
                    float a=2*Pi*i/n,b=2*Pi*(i+1)/n;
                    Vector3 da=Sphere(a,Latitudes[j]),db=Sphere(b,Latitudes[j]),dc=Sphere(b,Latitudes[j+1]),dd=Sphere(a,Latitudes[j+1]);
                    Color ca=EnvironmentColor(da,t,fill,surface,complete),cb=EnvironmentColor(db,t,fill,surface,complete),cc=EnvironmentColor(dc,t,fill,surface,complete),cd=EnvironmentColor(dd,t,fill,surface,complete);
                    Tri(batch,da*(radius*Noise(da)),db*(radius*Noise(db)),dc*(radius*Noise(dc)),ca,cb,cc);
                    Tri(batch,da*(radius*Noise(da)),dc*(radius*Noise(dc)),dd*(radius*Noise(dd)),ca,cc,cd);
                }
            }
        }
        private static Color EnvironmentColor(Vector3 d,float t,bool fill,int surface,bool complete)
        {
            float opacity=complete?1:VoidHorizonVisualRules.Smooth((t-.7f-Math.Abs(d.Y)*.45f)/.28f);
            Color color=fill?Sky(d.Y):Color.Lerp(new Color(152,54,235),new Color(103,207,232),.12f+.1f*(float)Math.Sin(d.X*16+d.Z*11));
            color.A=(byte)(opacity*(fill?255:surface==0?32:surface==1?62:112));return color;
        }
        private void BlackHole(Vector3 center,Vector3 facing,Vector3 right,float t,int n)
        {
            float emergence=VoidHorizonVisualRules.Hole(t);
            float radius=48*(.025f+.975f*emergence)*(1-.975f*VoidHorizonVisualRules.Collapse(t));
            if(t>VoidSkillRules.BurstStart)radius*=Math.Max(.1f,1-VoidHorizonVisualRules.Burst(t));
            Vector3 normal=Vector3.Normalize(facing*160+Vector3.UnitY*46),up=Vector3.Normalize(Vector3.Cross(right,normal));
            Vector3 diskRight=right*.985f+up*.173f,diskUp=up*.985f-right*.173f;
            // A broad, dim lens halo, progressively tighter towards the razor event horizon.
            for(int band=0;band<7;band++)
            {
                float inner=1.0f+band*.065f,outer=inner+.065f;
                Ring(Batch(10),center,right,up,radius*inner,radius*outer,1,n,t,new Color(123,132,220,(int)((7-band)*(7-band)*3*emergence)),(int)((6-band)*(6-band)*3*emergence));
            }
            Disk(Batch(11),center,diskRight,diskUp,radius,n,t,emergence,false);
            var core=Batch(12,true);
            for(int i=0;i<n;i++)
            {
                float a=2*Pi*i/n,b=2*Pi*(i+1)/n;
                Tri(core,center,Point(center,right,up,radius,a,1),Point(center,right,up,radius,b,1),new Color(0,0,2),new Color(0,0,2),new Color(0,0,2));
            }
            Ring(Batch(13),center,right,up,radius*1.001f,radius*1.024f,1,n,t,new Color(239,224,255,(int)(250*emergence)));
            // Lensed rear disk bends over the core. The near half crosses in front: actual occlusion.
            Lens(Batch(13),center,diskRight,diskUp,radius,n,t,emergence);
            Disk(Batch(14),center,diskRight,diskUp,radius,n,t,emergence,true);
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
                float spin=t*.7f+18*VoidHorizonVisualRules.Collapse(t);
                float flow=.68f+.22f*(float)Math.Sin(a*3-spin*(1+band*.12f))+.10f*(float)Math.Sin(a*11+spin*.6f+band);
                float inner=radius*(1.04f+band*.11f),outer=inner+radius*.105f;
                Color color=Color.Lerp(new Color(208,189,240),new Color(56,31,100),band/(float)(bands-1));
                color.A=(byte)(MathUtils.Saturate(fade*flow*(1-band*.10f))*210);
                Quad(batch,Point(center,right,up,inner,a,.24f),Point(center,right,up,inner,b,.24f),Point(center,right,up,outer,b,.24f),Point(center,right,up,outer,a,.24f),color);
            }
        }
        private void Ribbon(FlatBatch3D batch,Vector3 start,Vector3 end,Vector3 bend,Vector3 width,Color color)
        {
            // Connected tapered surfaces rather than a pile of disconnected line segments.
            for(int j=0;j<4;j++)
            {
                float a=j/4f,b=(j+1)/4f;
                Vector3 p=Vector3.Lerp(start,end,a)+bend*(4*a*(1-a)),q=Vector3.Lerp(start,end,b)+bend*(4*b*(1-b));
                Quad(batch,p-width*(1-a),p+width*(1-a),q+width*(1-b),q-width*(1-b),color);
            }
        }
        private void Fractures(Vector3 center,Vector3 facing,Vector3 right,float t,int quality)
        {
            if(t<=0)return;
            int count=VoidSkillRules.VfxSegments(quality,8,12,16);
            Vector3 up=Vector3.Normalize(Vector3.Cross(right,Vector3.Normalize(facing*160+Vector3.UnitY*46)));
            float fade=VoidHorizonVisualRules.Hole(t)*(1-VoidHorizonVisualRules.Collapse(t));
            var batch=Batch(15);
            for(int i=0;i<count;i++)
            {
                float a=i*2.39996f,phase=(t*.45f+i*.137f)%1;
                Vector3 radial=right*(float)Math.Cos(a)+up*(float)Math.Sin(a);
                Vector3 side=right*(float)-Math.Sin(a)+up*(float)Math.Cos(a);
                float distance=52+24*(1-phase);
                Vector3 p=center+radial*distance,q=center+radial*(distance+8+i%4*3)+side*3;
                Ribbon(batch,p,q,side*(i%2==0?2:-2),side*.13f,new Color(143,63,211,(int)(fade*100)));
                // Deterministic bounded debris advects towards the hole, never a particle emitter.
                Vector3 shard=center+radial*(48+40*(1-phase))+side*5;
                Tri(batch,shard,shard+side*(1+i%3),shard+radial*(3+i%4),new Color(62,22,103,(int)(fade*150)),new Color(170,94,226,(int)(fade*100)),new Color(12,3,23,0));
            }
        }
        private void Explosion(Vector3 center,Vector3 facing,Vector3 right,float t,int n)
        {
            float blast=VoidHorizonVisualRules.Burst(t),fade=(float)Math.Pow(1-blast,.35);
            Vector3 up=Vector3.Normalize(Vector3.Cross(right,Vector3.Normalize(facing*160+Vector3.UnitY*46)));
            var batch=Batch(20);
            // Physical expanding wavefronts at the fixed world-space singularity, not a one-frame flash.
            for(int ring=0;ring<2;ring++)
            {
                float radius=3+blast*(310-ring*80);
                Vector3 axis=Vector3.Normalize(up+facing*(ring==1?.5f:0));
                float width=2+radius*.07f;
                for(int i=0;i<n;i++)
                {
                    float a=2*Pi*i/n,b=2*Pi*(i+1)/n;
                    float ra=radius*(1+.025f*(float)Math.Sin(a*9+ring)+.01f*(float)Math.Sin(a*23)),rb=radius*(1+.025f*(float)Math.Sin(b*9+ring)+.01f*(float)Math.Sin(b*23));
                    Vector3 pa=Point(center,right,axis,ra,a,1),pb=Point(center,right,axis,rb,b,1);
                    Color hot=new Color(237,209,255,(int)(250*fade)),cold=new Color(110,39,202,0);
                    Tri(batch,Point(center,right,axis,Math.Max(0,ra-width),a,1),Point(center,right,axis,Math.Max(0,rb-width),b,1),pb,cold,cold,hot);
                    Tri(batch,Point(center,right,axis,Math.Max(0,ra-width),a,1),pb,pa,cold,hot,hot);
                    Tri(batch,pa,pb,Point(center,right,axis,rb+width*.5f,b,1),hot,hot,cold);
                    Tri(batch,pa,Point(center,right,axis,rb+width*.5f,b,1),Point(center,right,axis,ra+width*.5f,a,1),hot,cold,cold);
                }
            }
            float core=(8+28*(float)Math.Sin(Pi*blast))*fade;
            for(int i=0;i<n;i++)
                Tri(batch,center,Point(center,right,up,core,2*Pi*i/n,1),Point(center,right,up,core,2*Pi*(i+1)/n,1),new Color(250,231,255,(int)(255*fade)),new Color(158,70,238,0),new Color(158,70,238,0));
            int count=n/4;
            for(int i=0;i<count;i++)
            {
                float a=i*2.39996f;
                Vector3 radial=right*(float)Math.Cos(a)+up*(float)Math.Sin(a),side=Vector3.Cross(radial,facing);
                float reach=8+blast*(100+i%4*30);
                Ribbon(batch,center+radial*3,center+radial*reach,side*((i%2==0?1:-1)*(5+i%3*7)),side*(1.4f+i%3),new Color(167,79,231,(int)(fade*235)));
                Ribbon(batch,center+radial*(reach*.45f),center+radial*(reach*.8f)+side*(16+i%3*9),-side*4,side*.7f,new Color(113,40,185,(int)(fade*160)));
                Tri(batch,center+radial*reach,center+radial*(reach*.75f)+side*5,center+radial*(reach*.7f)-side*2,new Color(202,151,244,(int)(fade*180)),new Color(30,8,56,0),new Color(75,27,116,(int)(fade*110)));
            }
        }
        public void Dispose(){renderer.Clear();}
    }
}

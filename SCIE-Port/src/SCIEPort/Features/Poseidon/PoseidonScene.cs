using System;
using Engine;
using Engine.Graphics;
using Game;
using SCIEPort.Features.Stormhammer;
namespace SCIEPort.Features.Poseidon
{
    /// <summary>Bounded curved water volumes, foam crests, tide shields and double waterspouts.</summary>
    internal sealed class PoseidonScene
    {
        private readonly SubsystemPoseidon runtime;
        private readonly PrimitivesRenderer3D renderer=new PrimitivesRenderer3D();
        private readonly FlatBatch3D water,foam;
        private Camera camera;private int segments,triangles,limit;
        internal PoseidonScene(SubsystemPoseidon r)
        {runtime=r;water=renderer.FlatBatch(0,DepthStencilState.DepthRead,RasterizerState.CullNone,BlendState.AlphaBlend);foam=renderer.FlatBatch(1,DepthStencilState.DepthRead,RasterizerState.CullNone,BlendState.AlphaBlend);}
        internal void Draw(Camera c)
        {
            camera=c;triangles=0;bool low=StormhammerOptions.Quality==StormhammerEffectQuality.Low;
            segments=low?16:StormhammerOptions.Quality==StormhammerEffectQuality.Medium?24:32;limit=low?2048:4096;
            // Persistent domain is reconstructed from snapshots, including a late-joining viewer.
            foreach(var s in runtime.States)
            {
                if(SubsystemPoseidon.Equipped(s.Player)&&runtime.Fresh(s)&&s.Shield>0&&Vector3.DistanceSquared(s.Player.ComponentBody.Position,c.ViewPosition)<1600)Shield(s);
                if(!runtime.InDomain(s)||Vector3.DistanceSquared(s.DomainOrigin,c.ViewPosition)>6400)continue;
                float remaining=(float)(s.DomainEnd-runtime.Now),age=20-remaining,fade=PoseidonRules.DomainBlend(remaining);
                // Twelve o'clock symmetry: waterspouts at every bearing replace the old forward-facing pair.
                int spouts=low?3:segments>=32?5:4;
                if(!low)Vortex(s.DomainOrigin,age,fade,1,9);
                for(int i=0;i<spouts;i++)
                {
                    float bearing=i*6.283185f/spouts+age*.10f;
                    var center=s.DomainOrigin+new Vector3(MathUtils.Cos(bearing),0,MathUtils.Sin(bearing))*PoseidonRules.DomainSpoutRadius+Vector3.UnitY*.06f;
                    Vortex(center,age,fade,i%2==0?1:-1,6);
                }
                Ring(s.DomainOrigin+Vector3.UnitY*.08f,PoseidonRules.DomainRadius,.10f,new Color(29,139,208,60)*fade);
                Ring(s.DomainOrigin+Vector3.UnitY*.10f,3.0f+.15f*MathUtils.Sin(age*2),.05f,new Color(130,226,242,110)*fade);
                // One full-circle breaker expanding to the whole domain radius, followed by the standing whirlpool.
                Surge(s.DomainOrigin,age,fade);
            }
            foreach(var cast in runtime.Casts)
            {
                if(Vector3.DistanceSquared(cast.Origin,c.ViewPosition)>6400)continue;
                float age=(float)(runtime.Now-cast.Start);
                if(cast.Skill==1)Radial(cast.Origin,age);
                else if(cast.Skill==2&&cast.Distance>=0)Wave(cast.Origin,cast.Direction,age,1);
            }
            renderer.Flush(c.ViewProjectionMatrix);
        }
        private void Quad(FlatBatch3D batch,Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color)
        {if(triangles+2>limit)return;triangles+=2;batch.QueueQuad(a,b,c,d,color);}
        private void Radial(Vector3 origin,float age)
        {
            float t=MathUtils.Saturate(age/PoseidonRules.ShieldDuration),radius=6*t,fade=1-t;
            for(int row=0;row<3;row++)
            {
                float r=radius-row*.20f;if(r<.1f)continue;
                for(int i=0;i<segments;i++)
                {
                    float a=i*6.283185f/segments,b=(i+1)*6.283185f/segments;
                    Vector3 u=new Vector3(MathUtils.Cos(a),0,MathUtils.Sin(a)),v=new Vector3(MathUtils.Cos(b),0,MathUtils.Sin(b));
                    float height=(.4f+MathUtils.Sin(t*MathUtils.PI)*.7f)*(1-row*.2f);
                    Quad(water,origin+u*r+Vector3.UnitY*.10f,origin+v*r+Vector3.UnitY*.10f,
                        origin+v*(r-.22f)+Vector3.UnitY*height,origin+u*(r-.22f)+Vector3.UnitY*height,new Color(25,161,207,150)*fade);
                    Beam(origin+u*r+Vector3.UnitY*height,origin+v*r+Vector3.UnitY*height,.025f,new Color(198,247,252,210)*fade);
                }
            }
        }
        private void Shield(SubsystemPoseidon.State s)
        {
            Vector3 center=s.Player.ComponentBody.Position+Vector3.UnitY*.9f;float phase=(float)(runtime.Now%60)*1.6f;
            for(int band=0;band<2;band++)for(int i=0;i<segments/2;i++)
            {
                float a=phase+band*MathUtils.PI+i*3.8f/(segments/2),b=phase+band*MathUtils.PI+(i+1)*3.8f/(segments/2);
                Vector3 p=center+new Vector3(MathUtils.Cos(a)*.57f,MathUtils.Sin(a*2+band)*.23f,MathUtils.Sin(a)*.57f);
                Vector3 q=center+new Vector3(MathUtils.Cos(b)*.57f,MathUtils.Sin(b*2+band)*.23f,MathUtils.Sin(b)*.57f);
                Beam(p,q,.025f,new Color(85,218,241,140));
            }
        }
        private void Wave(Vector3 origin,Vector3 direction,float age,float overall)
        {
            if(!runtime.ClearWave(origin,direction,age))return;
            float total=PoseidonRules.WaveWindup+PoseidonRules.WaveDuration;
            float reveal=MathUtils.Saturate(age/PoseidonRules.WaveWindup),fade=MathUtils.Saturate((total-age)/.35f)*overall;
            if(fade<=0)return;
            Vector3 front=origin+direction*PoseidonRules.WaveDistance(age),side=Vector3.Cross(direction,Vector3.UnitY);
            float width=8,height=3.7f;
            for(int i=0;i<segments;i++)
            {
                float u=i/(float)segments,v=(i+1f)/segments,edge=MathUtils.Sin((u+v)*.5f*MathUtils.PI);
                for(int row=0;row<7;row++)
                {
                    float t=row/7f,z=(row+1f)/7;
                    Vector3 a=Surface(front,side,direction,u,t,age,width,height*reveal),b=Surface(front,side,direction,v,t,age,width,height*reveal);
                    Vector3 p=Surface(front,side,direction,v,z,age,width,height*reveal),q=Surface(front,side,direction,u,z,age,width,height*reveal);
                    Color col=Color.Lerp(new Color(9,51,103,175),new Color(42,166,200,130),t);
                    Quad(water,a,b,p,q,col*(fade*edge));
                    if(row==6)Beam(q,p,.065f,new Color(216,249,251,230)*(fade*edge));
                }
                if(i%2==0)
                {
                    float f=(i*.618034f+age*.8f)%1;var p=Surface(front,side,direction,u,1,age,width,height*reveal)+Vector3.UnitY*(f*.6f)-direction*f;
                    Beam(p,p+Vector3.UnitY*.1f-direction*.08f,.018f,new Color(179,235,246,180)*(fade*(1-f)));
                }
            }
            Ring(origin+Vector3.UnitY*.08f,1.4f*reveal,.035f,new Color(111,210,227,130)*fade);
        }
        private static Vector3 Surface(Vector3 o,Vector3 side,Vector3 dir,float u,float t,float age,float width,float height)
        {
            float edge=MathUtils.Pow(MathUtils.Sin(u*MathUtils.PI),.45f);
            // Upper section curls forward and over, rather than a uniformly vertical wall.
            float curl=-MathUtils.Sin(t*MathUtils.PI*.92f)*1.15f+t*t*1.10f;
            return o+side*((u-.5f)*width)+Vector3.UnitY*(.08f+t*(height+MathUtils.Sin(u*19-age*6)*.16f)*edge)+dir*(curl+.16f*MathUtils.Cos(u*6.28f));
        }
        // The ultimate's opening breaker is a closed cylinder: every bearing receives the same front.
        private void Surge(Vector3 origin,float age,float fade)
        {
            float t=PoseidonRules.DomainSurge(age);if(t<=0||t>=1)return;
            float radius=PoseidonRules.DomainRadius*(float)Math.Sqrt(t),swell=MathUtils.Sin(t*MathUtils.PI*.92f);
            for(int i=0;i<segments;i++)
            {
                float a=i*6.283185f/segments,b=(i+1)*6.283185f/segments;
                Vector3 u=new Vector3(MathUtils.Cos(a),0,MathUtils.Sin(a)),v=new Vector3(MathUtils.Cos(b),0,MathUtils.Sin(b));
                float hu=(4.6f+1.4f*MathUtils.Sin(a*3f+age*4.5f))*swell,hv=(4.6f+1.4f*MathUtils.Sin(b*3f+age*4.5f))*swell;
                for(int row=0;row<6;row++)
                {
                    float r=row/6f,z=(row+1f)/6,lean=.42f*z;
                    Color col=Color.Lerp(new Color(9,51,103,175),new Color(42,166,200,130),z);
                    Quad(water,origin+u*(radius-lean*.15f)+Vector3.UnitY*(r*hu),origin+v*(radius-lean*.15f)+Vector3.UnitY*(r*hv),
                        origin+v*(radius-lean)+Vector3.UnitY*(z*hv),origin+u*(radius-lean)+Vector3.UnitY*(z*hu),col*fade);
                }
                Beam(origin+u*radius+Vector3.UnitY*hu,origin+v*radius+Vector3.UnitY*hv,.10f,new Color(216,249,251,230)*fade);
            }
            Ring(origin+Vector3.UnitY*.08f,MathUtils.Max(radius,.7f),.05f,new Color(111,210,227,140)*fade);
        }
        private void Vortex(Vector3 center,float age,float fade,int sign,int rings)
        {
            for(int y=0;y<rings;y++)for(int i=0;i<segments;i++)
            {
                float t=y/(float)rings,z=(y+1f)/rings,a=i*6.283185f/segments,b=(i+1)*6.283185f/segments;
                Vector3 p=VortexPoint(center,t,a,age,sign),q=VortexPoint(center,t,b,age,sign),r=VortexPoint(center,z,b,age,sign),s=VortexPoint(center,z,a,age,sign);
                // Broken bands let the environment remain visible through the spinning water.
                if((i+y)%4!=0)Quad(water,p,q,r,s,Color.Lerp(new Color(13,83,146,105),new Color(50,181,212,85),t)*fade);
                if(i%4==0)Beam(p,s,.035f,new Color(182,240,246,170)*fade);
            }
            Ring(center+Vector3.UnitY*.08f,2.6f,.10f,new Color(126,221,235,150)*fade);
        }
        private static Vector3 VortexPoint(Vector3 c,float t,float angle,float age,int sign)
        {
            float a=angle+age*sign*2.4f+t*sign*7,r=.45f+2.3f*t*t;
            return c+new Vector3(MathUtils.Cos(a)*r,t*10,MathUtils.Sin(a)*r);
        }
        private void Ring(Vector3 c,float r,float w,Color color)
        {
            if(r<w)return;for(int i=0;i<segments;i++)
            {float a=i*6.283185f/segments,b=(i+1)*6.283185f/segments;var u=new Vector3(MathUtils.Cos(a),0,MathUtils.Sin(a));var v=new Vector3(MathUtils.Cos(b),0,MathUtils.Sin(b));Quad(foam,c+u*(r-w),c+u*(r+w),c+v*(r+w),c+v*(r-w),color);}
        }
        private void Beam(Vector3 a,Vector3 b,float width,Color color)
        {
            Vector3 side=Vector3.Cross(b-a,camera.ViewPosition-(a+b)*.5f);if(side.LengthSquared()<.000001f)return;side=Vector3.Normalize(side)*width;
            Quad(foam,a-side,a+side,b+side,b-side,color);
        }
        internal void Dispose()=>renderer.Clear();
    }
}

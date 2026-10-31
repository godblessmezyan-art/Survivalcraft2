using System;
using Engine;
using Engine.Graphics;
using Game;
using SCIEPort.Features.Stormhammer;
namespace SCIEPort.Features.Excalibur
{
    // Real depth-tested geometry. No world lighting/weather/terrain changes or full-screen flash.
    internal sealed class ExcaliburScene
    {
        private readonly SubsystemExcalibur runtime;
        private readonly PrimitivesRenderer3D renderer=new PrimitivesRenderer3D();
        private readonly FlatBatch3D gold,white;
        private int triangles,limit,segments;private Vector3 eye;
        internal ExcaliburScene(SubsystemExcalibur r)
        {runtime=r;gold=renderer.FlatBatch(0,DepthStencilState.DepthRead,RasterizerState.CullNone,BlendState.Additive);white=renderer.FlatBatch(1,DepthStencilState.DepthRead,RasterizerState.CullNone,BlendState.Additive);}
        private static readonly Color Gold=new Color(255,170,34,170),Ivory=new Color(255,249,216,235);
        internal void Draw(Camera view)
        {
            eye=view.ViewPosition;triangles=0;bool low=StormhammerOptions.Quality==StormhammerEffectQuality.Low;
            segments=low?16:StormhammerOptions.Quality==StormhammerEffectQuality.Medium?24:32;limit=low?3072:6144;
            foreach(var s in runtime.States)
            {
                if(!runtime.Fresh(s)||!SubsystemExcalibur.Equipped(s.Player))continue;
                if(Vector3.DistanceSquared(view.ViewPosition,s.Player.ComponentBody.Position)>96*96)continue;
                if(s.BlessingEnd>runtime.Now)
                {
                    float t=(float)(runtime.Now%60);var center=s.Player.ComponentBody.Position+Vector3.UnitY*.15f;
                    Ring(center,Vector3.UnitX,Vector3.UnitZ,.7f,.025f,Gold*.6f);
                    for(int i=0;i<8;i++){float a=i*MathUtils.PI/4+t*.5f;var p=center+new Vector3(MathUtils.Cos(a),1.4f,MathUtils.Sin(a))*.6f;Beam(p,p+Vector3.UnitY*.20f,.016f,Ivory*.5f);}
                }
                var c=s.Active;if(c==null)continue;float age=(float)(runtime.Now-c.Start);
                if(age<0||age>ExcaliburRules.Length(c.Skill))continue;
                if(c.Skill==3)Ultimate(c,age);else if(c.Skill==1)Lake(c,age);
            }
            renderer.Flush(view.ViewProjectionMatrix);
        }
        private void Quad(FlatBatch3D batch,Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color)
        {if(triangles+2>limit)return;triangles+=2;batch.QueueQuad(a,b,c,d,color);}
        private void Beam(Vector3 a,Vector3 b,float width,Color color)
        {
            Vector3 side=Vector3.Cross(b-a,eye-(a+b)*.5f);
            if(side.LengthSquared()<.00001f)side=Vector3.UnitX;else side=Vector3.Normalize(side);
            side*=width*.5f;Quad(white,a-side,b-side,b+side,a+side,color);
        }
        private void Ring(Vector3 o,Vector3 x,Vector3 y,float radius,float width,Color color)
        {
            for(int i=0;i<segments;i++)
            {float a=i*(2*MathUtils.PI)/segments,b=(i+1)*(2*MathUtils.PI)/segments;Beam(o+(x*MathUtils.Cos(a)+y*MathUtils.Sin(a))*radius,o+(x*MathUtils.Cos(b)+y*MathUtils.Sin(b))*radius,width,color);}
        }
        private static void Basis(Vector3 d,out Vector3 side,out Vector3 up)
        {side=Vector3.Cross(d,Vector3.UnitY);side=side.LengthSquared()<.001f?Vector3.UnitX:Vector3.Normalize(side);up=Vector3.Normalize(Vector3.Cross(side,d));}
        private void Lake(SubsystemExcalibur.Cast c,float age)
        {
            Vector3 side,up;Basis(c.Direction,out side,out up);float t=MathUtils.Saturate((age-.15f)/.6f),fade=1-t;
            Vector3 front=c.Origin+c.Direction*(16*t);
            for(int band=0;band<3;band++)for(int i=0;i<segments;i++)
            {
                float a=-1.2f+i*2.4f/segments,b=-1.2f+(i+1)*2.4f/segments,r=1.3f-band*.1f;
                var p=front+side*(MathUtils.Sin(a)*r)+up*(MathUtils.Cos(a)*r-.5f);
                var q=front+side*(MathUtils.Sin(b)*r)+up*(MathUtils.Cos(b)*r-.5f);
                Beam(p,q,.065f,new Color(164,221,255,230)*fade);Beam(p-c.Direction*.12f,q-c.Direction*.12f,.16f,Gold*(fade*.6f));
            }
        }
        private void Ultimate(SubsystemExcalibur.Cast c,float age)
        {
            Vector3 side,up;Basis(c.Direction,out side,out up);
            var basePoint=c.Player.ComponentBody.Position;var focus=c.Origin+c.Direction*.75f;
            float charge=MathUtils.Saturate(age/ExcaliburRules.Release),fade=MathUtils.Saturate((4-age)/.45f);
            if(age<ExcaliburRules.Release)
            {
                // Three counter-rotating lake seals and spirals pulling motes into the raised blade.
                for(int layer=0;layer<3;layer++)
                {
                    float radius=(.7f+layer*.65f)*(1+.15f*MathUtils.Sin(age*3));
                    Ring(basePoint+Vector3.UnitY*(.08f+layer*.10f),Vector3.UnitX,Vector3.UnitZ,radius,.025f+charge*.025f,Gold*(.3f+.65f*charge));
                    for(int i=0;i<segments/2;i++)
                    {
                        float t=(i/(segments*.5f)+age*.45f+layer*.23f)%1,a=i*2.39996f+age*(layer%2==0?1:-1);
                        var p=focus+new Vector3(MathUtils.Cos(a),.4f+MathUtils.Sin(a*2)*.4f,MathUtils.Sin(a))*(4*(1-t));
                        Beam(p,Vector3.Lerp(p,focus,.12f),.035f,Gold*(t*charge));
                    }
                }
                // A complete giant sword silhouette, including the gold crossguard and blue-gold grip.
                float grow=MathUtils.Saturate((age-.85f)/1.05f);var foot=focus+Vector3.UnitY*.3f;var tip=foot+Vector3.UnitY*(2+8*grow);
                var shoulder=foot+Vector3.UnitY*1.2f;var neck=tip-Vector3.UnitY*(.7f+grow*.6f);
                Quad(gold,shoulder-side*.95f,neck-side*.50f,tip,shoulder,Gold*grow);
                Quad(gold,shoulder,tip,neck+side*.50f,shoulder+side*.95f,Gold*grow);
                Beam(shoulder,tip,.38f,Ivory*grow);Beam(shoulder-side*1.8f,shoulder+side*1.8f,.30f,Gold*grow);
                Beam(shoulder-side*.93f,neck-side*.5f,.10f,Ivory*grow);Beam(shoulder+side*.93f,neck+side*.5f,.10f,Ivory*grow);
                Beam(foot,shoulder,.20f,new Color(110,155,255,150)*grow);
                for(int i=0;i<5;i++)Ring(focus+Vector3.UnitY*(i*.65f),side,c.Direction,.5f+grow*.7f,.02f,Gold*(charge*.3f));
                // Stable radial rays build to one crescendo; no random strobe.
                if(age>2.1f)for(int i=0;i<12;i++)
                {float a=i*(2*MathUtils.PI)/12;var v=side*MathUtils.Cos(a)+up*MathUtils.Sin(a);Beam(focus+v*.2f,focus+v*(1+charge*3),.04f,Gold*((age-2.1f)/.7f));}
                return;
            }
            float burst=MathUtils.Saturate((age-ExcaliburRules.Release)/.12f),length=35*burst;
            // 3D tapered shell + white core + rotating ribbons; full range persists through the release tail.
            for(int layer=0;layer<3;layer++)
            {
                float radius=layer==0?4:layer==1?2.4f:1.05f;
                Color color=(layer==2?Ivory:layer==1?new Color(255,219,122,210):new Color(255,173,28,175))*fade;
                for(int z=0;z<10;z++)for(int i=0;i<segments;i++)
                {
                    float t=z/10f,u=(z+1)/10f,a=i*(2*MathUtils.PI)/segments,b=(i+1)*(2*MathUtils.PI)/segments;
                    float r=radius*(.4f+.6f*MathUtils.Saturate(t*5)),q=radius*(.4f+.6f*MathUtils.Saturate(u*5));
                    var p=c.Origin+c.Direction*(t*length);var end=c.Origin+c.Direction*(u*length);
                    Quad(layer==2?white:gold,p+(side*MathUtils.Cos(a)+up*MathUtils.Sin(a))*r,end+(side*MathUtils.Cos(a)+up*MathUtils.Sin(a))*q,end+(side*MathUtils.Cos(b)+up*MathUtils.Sin(b))*q,p+(side*MathUtils.Cos(b)+up*MathUtils.Sin(b))*r,color);
                }
            }
            for(int i=0;i<8;i++)
            {
                for(int z=0;z<16;z++)
                {float t=z/16f,u=(z+1)/16f,a=i*(2*MathUtils.PI)/8+t*5-age*8,b=i*(2*MathUtils.PI)/8+u*5-age*8;
                    Beam(c.Origin+c.Direction*(length*t)+(side*MathUtils.Cos(a)+up*MathUtils.Sin(a))*3.3f,c.Origin+c.Direction*(length*u)+(side*MathUtils.Cos(b)+up*MathUtils.Sin(b))*3.3f,.055f,Ivory*fade);}
            }
            for(int i=0;i<5;i++){float t=(age*1.9f+i*.2f)%1;Ring(c.Origin+c.Direction*(length*t),side,up,4.1f,.06f,Gold*(fade*(1-t)));}
            Ring(basePoint+Vector3.UnitY*.12f,Vector3.UnitX,Vector3.UnitZ,2+(age-2.8f)*9,.07f,Gold*fade);
        }
        internal void Dispose(){gold.Clear();white.Clear();}
    }
}

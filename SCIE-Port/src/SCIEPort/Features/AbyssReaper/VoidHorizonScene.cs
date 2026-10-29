using System;
using Engine;
using Engine.Graphics;
using Game;
namespace SCIEPort.Features.AbyssReaper
{
    // Bounded, presentation-only primitives. No damage, terrain or entity operations.
    internal sealed class VoidHorizonScene : IDisposable
    {
        private readonly PrimitivesRenderer3D renderer=new PrimitivesRenderer3D();
        internal void Draw(Camera camera,Vector3 center,float t,float fade,int quality)
        {
            var core=renderer.FlatBatch(-2,DepthStencilState.DepthRead,RasterizerState.CullNone,BlendState.Opaque);
            var light=renderer.FlatBatch(-1,DepthStencilState.DepthRead,RasterizerState.CullNone,BlendState.AlphaBlend);
            Vector3 sky=camera.ViewPosition+new Vector3(0,65,55);
            Vector3 right=Vector3.UnitX,up=Vector3.Normalize(new Vector3(0,55,-65));
            if(t>=2 && t<17)
            {
                int crackSegments=VoidSkillRules.VfxSegments(quality,12,18,24);
                Vector3 previous=sky-right*58,previousTop=previous+up*.8f,previousBottom=previous-up*.8f;
                for(int i=1;i<=crackSegments;i++)
                {
                    Vector3 point=sky+right*(-58+i*(116f/crackSegments))+up*((i%2==0?1:-1)*(1.5f+(i%3)));
                    float width=.5f+(i%3)*.45f;Vector3 top=point+up*width,bottom=point-up*width;
                    core.QueueQuad(previousTop,top,bottom,previousBottom,new Color(3,0,8));
                    light.QueueLine(previousTop,top,new Color(170,82,225,(int)(230*fade)));
                    light.QueueLine(previousBottom,bottom,new Color(112,51,171,(int)(200*fade)));
                    if(i%3==0)
                    {
                        float side=(i%2==0?1f:-1f);
                        Vector3 branch=point+up*side*(4f+(i%4)*1.5f)+right*3f;
                        light.QueueLine(point,branch,new Color(151,65,211,(int)(190*fade)));
                        light.QueueLine(branch,branch+up*side*2f-right*1.5f,new Color(98,42,160,(int)(140*fade)));
                    }
                    previous=point;previousTop=top;previousBottom=bottom;
                }
            }
            if(t>=5 && t<17)
            {
                float appear=MathUtils.Saturate((t-5)/3),collapse=VoidHorizonVisualRules.CoreScale(t);
                float radius=18f*appear*collapse;
                int ringSegments=VoidSkillRules.VfxSegments(quality,16,32,48);
                for(int i=0;i<ringSegments;i++)
                {
                    float a=(float)(i*Math.PI*2/ringSegments),b=(float)((i+1)*Math.PI*2/ringSegments);
                    Vector3 p=sky+right*(float)Math.Cos(a)*radius+up*(float)Math.Sin(a)*radius;
                    Vector3 q=sky+right*(float)Math.Cos(b)*radius+up*(float)Math.Sin(b)*radius;
                    core.QueueTriangle(sky,p,q,new Color(2,0,5));
                    for(int ring=0;ring<3;ring++)
                    {
                        float angle=(t*(.4f+ring*.23f))*(ring%2==0?1:-1),r=radius*(1.12f+ring*.2f);
                        Vector3 r1=sky+right*(float)Math.Cos(a+angle)*r+up*(float)Math.Sin(a+angle)*r*.48f;
                        Vector3 r2=sky+right*(float)Math.Cos(b+angle)*r+up*(float)Math.Sin(b+angle)*r*.48f;
                        int opacity=((i+ring*3)%7<3)?230:105;
                        Color color=new Color(128+ring*20,51+ring*10,190+ring*15,(int)(opacity*fade));
                        if((i+ring*3)%7<3)
                        {
                            Vector3 inset1=sky+(r1-sky)*.975f,inset2=sky+(r2-sky)*.975f;
                            light.QueueQuad(r1,r2,inset2,inset1,color);
                        }
                        else light.QueueLine(r1,r2,color);
                    }
                }
            }
            if(t>=8 && t<17)
            {
                float radius=t<16?40f:40f*MathUtils.Saturate(t-16);
                int fieldSegments=VoidSkillRules.VfxSegments(quality,24,40,64);
                for(int i=0;i<fieldSegments;i++)
                {
                    float a=(float)(i*Math.PI*2/fieldSegments),b=(float)((i+1)*Math.PI*2/fieldSegments);
                    Vector3 p=center+new Vector3((float)Math.Cos(a)*radius,.05f,(float)Math.Sin(a)*radius);
                    Vector3 q=center+new Vector3((float)Math.Cos(b)*radius,.05f,(float)Math.Sin(b)*radius);
                    light.QueueLine(p,q,new Color(146,64,206,(int)(210*fade)));
                    if(t<14 && i%4==0)
                    {
                        float spin=t*.35f,inner=radius*.83f;
                        Vector3 rune=center+new Vector3((float)Math.Cos(a+spin)*inner,.08f,(float)Math.Sin(a+spin)*inner);
                        Vector3 runeTip=center+new Vector3((float)Math.Cos(a+spin+.045f)*(inner-2f),.08f,(float)Math.Sin(a+spin+.045f)*(inner-2f));
                        light.QueueLine(rune,runeTip,new Color(180,86,225,(int)(160*fade)));
                    }
                }
            }
            renderer.Flush(camera.ViewProjectionMatrix);
        }
        public void Dispose() { renderer.Clear(); }
    }
}

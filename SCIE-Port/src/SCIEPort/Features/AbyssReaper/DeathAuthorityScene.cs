using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;
namespace SCIEPort.Features.AbyssReaper
{
    internal sealed class DeathAuthorityScene:IDisposable
    {
        private sealed class SwingCast{internal ComponentPlayer Player;internal int Stage;internal double Started;internal Vector3 Direction;}
        private readonly List<SwingCast> swings=new List<SwingCast>();private readonly PrimitivesRenderer3D renderer=new PrimitivesRenderer3D();private readonly AbyssMeleeScene melee=new AbyssMeleeScene();
        internal void Swing(ComponentPlayer p,int stage,Vector3 direction,double now){if(p.PlayerData.IsMainPlayer)return;for(int i=swings.Count-1;i>=0;i--)if(swings[i].Player==p)swings.RemoveAt(i);if(swings.Count<16)swings.Add(new SwingCast{Player=p,Stage=stage,Direction=direction,Started=now});}
        internal void Clear(){swings.Clear();renderer.Clear();}
        internal void Update(double now)
        {
            for(int i=swings.Count-1;i>=0;i--)
            {
                var s=swings[i];double duration=s.Stage==0?.35:s.Stage==1?.42:.58;float phase=.48f+(float)((now-s.Started)/duration);
                if(!s.Player.IsAddedToProject||phase>=1){swings.RemoveAt(i);continue;}var pose=AbyssMeleeMotion.Sample(s.Stage,phase);s.Player.ComponentCreatureModel.InHandItemRotationOrder=new Vector3(pose.Pitch,pose.Yaw,pose.Roll);s.Player.ComponentCreatureModel.InHandItemOffsetOrder=new Vector3(pose.X,pose.Y,pose.Z)*.35f;
            }
        }
        internal int QueueWave(Vector3 center,float time,int quality)
        {
            if(!WorldCutRules.Finite(time)||time<0||time>=1.6f)return 0;
            var core=renderer.FlatBatch(0,DepthStencilState.DepthRead,RasterizerState.CullNone,BlendState.NonPremultiplied);var edge=renderer.FlatBatch(1,DepthStencilState.DepthRead,RasterizerState.CullNone,BlendState.NonPremultiplied);
            float expansion=MathUtils.Saturate(time/1.25f),radius=.1f+24*VoidHorizonVisualRules.Smooth(expansion),alpha=time<1.25f?1:MathUtils.Saturate((1.6f-time)/.35f);center+=Vector3.UnitY*.1f;
            int n=VoidSkillRules.VfxSegments(quality,32,48,64);int triangles=0;
            for(int i=0;i<n;i++)
            {
                float a=i*MathUtils.PI*2/n,b=(i+1)*MathUtils.PI*2/n;Vector3 p=new Vector3((float)Math.Cos(a),0,(float)Math.Sin(a)),q=new Vector3((float)Math.Cos(b),0,(float)Math.Sin(b));
                core.QueueTriangle(center,center+p*radius,center+q*radius,new Color(8,0,13,(int)(90*alpha)));triangles++;
                edge.QueueQuad(center+p*Math.Max(0,radius-.7f),center+q*Math.Max(0,radius-.7f),center+q*radius,center+p*radius,new Color(107,52,148,(int)(230*alpha)));triangles+=2;
                Vector3 up=Vector3.UnitY*(.5f+.25f*(float)Math.Sin(a*7-time*8));edge.QueueQuad(center+p*radius,center+q*radius,center+q*radius+up,center+p*radius+up,new Color(22,5,37,(int)(150*alpha)));triangles+=2;
                if(i%4==0){Vector3 tip=center+p*radius,side=new Vector3(-p.Z,0,p.X)*.10f;edge.QueueTriangle(tip-side,tip+side,tip+Vector3.UnitY*(1.3f*alpha),new Color(160,112,190,(int)(200*alpha)));triangles++;}
            }
            return triangles;
        }
        internal void Draw(Camera camera,IList<DeathAuthorityRuntime.WaveCast> casts,double now,int quality)
        {
            renderer.Clear();try{foreach(var c in casts)QueueWave(c.Center,(float)(now-c.Started),quality);renderer.Flush(camera.ViewProjectionMatrix);}finally{renderer.Clear();}
            foreach(var s in swings){float duration=s.Stage==0?.35f:s.Stage==1?.42f:.58f;melee.Queue(s.Player,s.Stage,.48f+(float)(now-s.Started)/duration);}melee.Flush(camera);
        }
        public void Dispose(){swings.Clear();renderer.Clear();melee.Dispose();}
    }
}

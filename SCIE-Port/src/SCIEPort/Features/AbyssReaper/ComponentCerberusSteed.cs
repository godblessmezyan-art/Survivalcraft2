using System;
using Engine;
using Game;
using Game.NetWork;
using GameEntitySystem;
using TemplatesDatabase;
namespace SCIEPort.Features.AbyssReaper
{
    /// <summary>Enable native steed orders for the stable mod-owned template name.</summary>
    public sealed class ComponentCerberusSteed:ComponentSteedBehavior,IUpdateable
    {
        private double nextCalibration;private float lastStep,lastDrag;
        internal Vector3? FaceTarget;internal double FaceUntil;
        /// <inheritdoc/>
        public override void Load(ValuesDictionary values,IdToEntityMap map)
        {base.Load(values,map);m_isEnabled=true;}
        /// <summary>Native steed control plus a bounded jump calibration for four-block ascent.</summary>
        public new void Update(float dt)
        {
            base.Update(dt);
            var rider=m_componentMount.Rider?.ComponentCreature as ComponentPlayer;
            bool ownsMotion=CommonLib.WorkType==WorkType.Local||CommonLib.WorkType==WorkType.Server&&(rider==null||rider.PlayerData.IsMainPlayer)||CommonLib.WorkType==WorkType.Client&&rider?.PlayerData.IsMainPlayer==true;
            if(ownsMotion&&FaceTarget.HasValue&&m_subsystemTime.GameTime<FaceUntil)
            {
                Vector3 direction=FaceTarget.Value-m_componentCreature.ComponentBody.Position;direction.Y=0;
                if(direction.LengthSquared()>.01f)
                {
                    var yaw=Quaternion.CreateFromAxisAngle(Vector3.UnitY,MathUtils.Atan2(-direction.X,-direction.Z));
                    m_componentCreature.ComponentBody.Rotation=Quaternion.Slerp(m_componentCreature.ComponentBody.Rotation,yaw,MathUtils.Saturate(dt*4));
                }
            }
            if(m_componentCreature.ComponentLocomotion.JumpOrder<=0&&m_subsystemTime.GameTime<nextCalibration)return;nextCalibration=m_subsystemTime.GameTime+.5;
            float step=MathUtils.Clamp(dt,.005f,.1f),drag=m_componentCreature.ComponentBody.AirDrag.Y;
            if(Math.Abs(step-lastStep)<.003f&&Math.Abs(drag-lastDrag)<.001f)return;
            lastStep=step;lastDrag=drag;double low=5,high=20;
            for(int i=0;i<14;i++){double speed=(low+high)*.5;if(Ascent(speed,step,drag)<DeathAuthorityRules.CerberusJumpHeight+.05)low=speed;else high=speed;}
            m_componentCreature.ComponentLocomotion.JumpSpeed=(float)high;
        }
        private static double Ascent(double speed,double step,double drag)
        {
            // Actual x25 body order: gravity -10*dt, then vertical air drag, then move.
            double a=1-Math.Max(0,Math.Min(.99,drag*step));
            if(a>.999999){double n=Math.Floor(speed/(10*step));return step*(n*speed-5*step*n*(n+1));}
            double b=10*step*a/(1-a),count=Math.Max(0,Math.Floor(Math.Log(b/(speed+b))/Math.Log(a)));
            return step*((speed+b)*a*(1-Math.Pow(a,count))/(1-a)-b*count);
        }
    }
}

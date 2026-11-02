using System;
using Engine;
using Game;
using GameEntitySystem;
using TemplatesDatabase;
namespace SCIEPort.Features.AbyssReaper
{
    /// <summary>One original horse-and-knight assembly, seven rigid bones. Presentation only.</summary>
    public sealed class ComponentNightmareKnightModel:ComponentCreatureModel
    {
        private int body,rider,sword;private readonly int[] legs=new int[4];
        private double swingStarted=-100;private uint swingRevision;private float chargeBlend;
        internal bool Charging;
        internal void Swing(double started){swingStarted=started;}
        internal void ReceiveSwing(uint revision,float age)
        {if(WorldCutRules.Newer(revision,swingRevision)&&age>=0&&age<NightmareKnightRules.SwingDuration){swingRevision=revision;Swing(m_subsystemTime.GameTime-age);}}
        /// <inheritdoc/>
        public override void Load(ValuesDictionary values,IdToEntityMap map)
        {
            base.Load(values,map);
            if(Model.Bones.Count!=7)throw new InvalidOperationException("Nightmare knight requires exactly seven native model matrices.");
            body=Model.FindBone("Body").Index;rider=Model.FindBone("Rider").Index;sword=Model.FindBone("SwordArm").Index;
            for(int i=0;i<4;i++)legs[i]=Model.FindBone("Leg"+(i+1)).Index;
        }
        /// <inheritdoc/>
        public override void Update(float dt)
        {
            AttackOrder=false;base.Update(dt);AttackOrder=false;IsAttackHitMoment=false;
            MovementAnimationPhase+=m_componentCreature.ComponentBody.Velocity.XZ.Length()*dt*.46f;
            chargeBlend=MathUtils.Lerp(chargeBlend,Charging?1:0,MathUtils.Saturate(dt*7));
        }
        /// <inheritdoc/>
        public override void Animate()
        {
            base.Animate();var b=m_componentCreature.ComponentBody;float moving=MathUtils.Saturate(b.Velocity.XZ.Length()/2);
            float phase=MovementAnimationPhase*6.283185f,bob=.025f*moving*MathUtils.Sin(phase*2);
            SetBoneTransform(body,Matrix.CreateRotationZ(DeathPhase*1.45f)*Matrix.CreateRotationY(b.Rotation.ToYawPitchRoll().X)*Matrix.CreateTranslation(b.Position+Vector3.UnitY*(bob-DeathPhase*.55f)));
            SetBoneTransform(rider,Matrix.CreateRotationX(-.16f*chargeBlend)*Matrix.CreateTranslation(0,.012f*moving*MathUtils.Sin(phase*2),0));
            for(int i=0;i<4;i++)SetBoneTransform(legs[i],Matrix.CreateRotationX((.30f+.10f*chargeBlend)*moving*MathUtils.Sin(phase+(i==0||i==3?0:MathUtils.PI))*(1-DeathPhase)));
            double age=m_subsystemTime.GameTime-swingStarted;
            SetBoneTransform(sword,Matrix.CreateRotationX(NightmareKnightRules.SwordAngle(age))*Matrix.CreateRotationZ(age>=0&&age<NightmareKnightRules.SwingDuration?.12f:0));
        }
        /// <inheritdoc/>
        public override Vector3 CalculateEyePosition()=>m_componentCreature.ComponentBody.Position+Vector3.UnitY*2.9f+m_componentCreature.ComponentBody.Matrix.Forward*.25f;
    }
}

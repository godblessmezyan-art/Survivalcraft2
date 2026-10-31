using System;
using Engine;
using Game;
using GameEntitySystem;
using TemplatesDatabase;
namespace SCIEPort.Features.AbyssReaper
{
    /// <summary>Original seven-bone skeleton dragon. Rendering never deals damage.</summary>
    public sealed class ComponentBoneDragonModel:ComponentCreatureModel
    {
        private int body,left,right;private readonly int[] legs=new int[4];private float airborne;
        /// <inheritdoc/>
        public override void Load(ValuesDictionary values,IdToEntityMap map)
        {
            base.Load(values,map);
            if(Model.Bones.Count!=7)throw new InvalidOperationException("Bone dragon requires exactly seven native model matrices.");
            body=Model.FindBone("Body").Index;left=Model.FindBone("WingL").Index;right=Model.FindBone("WingR").Index;
            for(int i=0;i<4;i++)legs[i]=Model.FindBone("Leg"+(i+1)).Index;
        }
        /// <inheritdoc/>
        public override void Update(float dt)
        {
            base.Update(dt);IsAttackHitMoment=false;
            var b=m_componentCreature.ComponentBody;
            float flight=!b.StandingOnValue.HasValue&&b.StandingOnBody==null&&b.ImmersionFactor<.5f?1:0;
            airborne=MathUtils.Lerp(airborne,flight,MathUtils.Saturate(5*dt));
            MovementAnimationPhase+=b.Velocity.XZ.Length()*dt*.55f;
        }
        /// <inheritdoc/>
        public override void Animate()
        {
            base.Animate();var b=m_componentCreature.ComponentBody;float t=(float)(m_subsystemTime.GameTime%100);
            float wing=MathUtils.Lerp(1.12f,.20f+.35f*MathUtils.Sin(t*6),airborne)*(1-DeathPhase);
            SetBoneTransform(body,Matrix.CreateRotationZ(DeathPhase*1.4f)*Matrix.CreateRotationY(b.Rotation.ToYawPitchRoll().X)*Matrix.CreateTranslation(b.Position-Vector3.UnitY*DeathPhase*.6f));
            SetBoneTransform(left,Matrix.CreateRotationZ(-wing));SetBoneTransform(right,Matrix.CreateRotationZ(wing));
            for(int i=0;i<4;i++)
            {
                float phase=MovementAnimationPhase*6.283185f+(i==0||i==3?0:3.141593f);
                float walk=.34f*MathUtils.Sin(phase)*MathUtils.Saturate(b.Velocity.XZ.Length()/2);
                SetBoneTransform(legs[i],Matrix.CreateRotationX(MathUtils.Lerp(walk,.32f,airborne)*(1-DeathPhase)));
            }
        }
        /// <inheritdoc/>
        public override Vector3 CalculateEyePosition()=>m_componentCreature.ComponentBody.Position+Vector3.UnitY*2.15f+m_componentCreature.ComponentBody.Matrix.Forward*2.6f;
    }
}

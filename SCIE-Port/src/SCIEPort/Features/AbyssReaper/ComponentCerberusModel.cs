using System;
using Engine;
using Engine.Graphics;
using Game;
using GameEntitySystem;
using TemplatesDatabase;
namespace SCIEPort.Features.AbyssReaper
{
    /// <summary>Original three-head assembly, synchronized jaws, native four-leg gait. No combat here.</summary>
    public sealed class ComponentCerberusModel:ComponentFourLeggedModel
    {
        private readonly PrimitivesRenderer3D soulFire=new PrimitivesRenderer3D();
        private int jaw;
        private readonly Vector3[] mouths=new Vector3[3];
        private double biteStarted=-100;
        private uint visualRevision;
        /// <summary>Begin presentation only. The owner runtime settles one hit on the host.</summary>
        internal void Bite(double started){biteStarted=started;}
        internal void ReceiveBite(uint revision,float age)
        {if(WorldCutRules.Newer(revision,visualRevision)&&age>=0&&age<CerberusRules.AnimationDuration){visualRevision=revision;Bite(m_subsystemTime.GameTime-age);}}
        /// <inheritdoc/>
        public override void Load(ValuesDictionary values,IdToEntityMap map)
        {
            base.Load(values,map);
            // x25.01.17's default ModelShader has seven world matrices. Reject an
            // incompatible asset during creation, before it can break world drawing.
            if(Model.Bones.Count>7)throw new InvalidOperationException("Cerberus exceeds the native seven-bone model limit.");
            var bone=Model.FindBone("Jaw");jaw=bone.Index;
            Vector3 pivot=BlockMesh.GetBoneAbsoluteTransform(bone).Translation;
            // Original mesh's three mouth centres share the same Y/Z bite pivot.
            for(int i=0;i<3;i++)mouths[i]=pivot+Vector3.UnitX*((i-1)*.5448873f);
        }
        /// <inheritdoc/>
        public override void Update(float dt)
        {
            // Native chase/butt hit moments must never add a second attack.
            AttackOrder=false;base.Update(dt);IsAttackHitMoment=false;AttackOrder=false;
        }
        /// <inheritdoc/>
        public override void Animate()
        {
            base.Animate();
            double time=m_subsystemTime.GameTime-biteStarted;
            float open=.055f;
            if(time>=0&&time<CerberusRules.AnimationDuration)
                open=time<CerberusRules.ImpactDelay?.08f+.48f*(float)(time/CerberusRules.ImpactDelay):.025f+.10f*MathUtils.Saturate((float)((time-CerberusRules.ImpactDelay)/.34));
            // All heads stay fixed relative to the body. Only their jaws share the bite phase.
            SetBoneTransform(m_headBone.Index,Matrix.Identity);
            SetBoneTransform(jaw,Matrix.CreateRotationX(-open));
        }
        /// <inheritdoc/>
        public override Vector3 CalculateEyePosition()=>m_componentCreature.ComponentBody.Position+Vector3.UnitY*2.23f+m_componentCreature.ComponentBody.Matrix.Forward*1.35f;
        /// <inheritdoc/>
        public override void DrawExtras(Camera camera)
        {
            base.DrawExtras(camera);if(m_componentCreature.ComponentHealth.Health<=0)return;
            soulFire.Clear();
            try
            {
                var batch=soulFire.FlatBatch(0,DepthStencilState.DepthRead,RasterizerState.CullNone,BlendState.NonPremultiplied);
                Matrix world=Matrix.CreateRotationY(m_componentCreature.ComponentBody.Rotation.ToYawPitchRoll().X)*Matrix.CreateTranslation(m_componentCreature.ComponentBody.Position+Vector3.UnitY*Bob);
                float pulse=.85f+.15f*MathUtils.Sin((float)(m_subsystemTime.GameTime%30)*4);
                bool biting=m_subsystemTime.GameTime-biteStarted>=0&&m_subsystemTime.GameTime-biteStarted<CerberusRules.AnimationDuration;
                for(int i=0;i<3;i++)
                {
                    Vector3 at=Vector3.Transform(mouths[i]+new Vector3(0,.015f,-.75f),world);
                    Vector3 up=world.Up*(biting?.36f:.19f)*pulse,side=world.Right*.085f,back=-world.Forward*.04f;
                    batch.QueueTriangle(at-side,at+side,at+up,new Color(216,242,250,205));
                    batch.QueueTriangle(at-back,at+back,at+up*1.12f,new Color(235,250,255,210));
                    batch.QueueTriangle(at-side*.4f,at+side*.4f,at+up*.7f,new Color(255,255,255,245));
                }
                soulFire.Flush(camera.ViewProjectionMatrix);
            }
            finally{soulFire.Clear();}
        }
        /// <inheritdoc/>
        public override void Dispose(){soulFire.Clear();base.Dispose();}
    }
}

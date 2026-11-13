using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;
using GameEntitySystem;
using TemplatesDatabase;

namespace SCIEPort.Features.AbyssReaper
{
    /// <summary>
    /// Original three-headed underworld mastiff. The native model format has rigid
    /// per-bone meshes and no imported animation clips, so every pose is authored
    /// here: three necks/heads/jaws, chest breathing, four-leg gait inherited from
    /// the native model, a three-segment serpent tail and three snake groups.
    /// Combat damage is never settled here; this component is presentation only.
    /// </summary>
    public sealed class ComponentCerberusModel:ComponentFourLeggedModel
    {
        private const int HeadCount=3;
        private readonly PrimitivesRenderer3D m_fx=new PrimitivesRenderer3D();
        private readonly ModelBone[] m_necks=new ModelBone[HeadCount];
        private readonly ModelBone[] m_heads=new ModelBone[HeadCount];
        private readonly ModelBone[] m_jaws=new ModelBone[HeadCount];
        private ModelBone m_chest,m_tailA,m_tailB,m_tailC,m_tailHead,m_tailJaw,m_snakeC,m_snakeL,m_snakeR,m_snakeJaw;
        private readonly Vector3[] m_eyeOffsets=new Vector3[HeadCount];
        private readonly Vector3[] m_mouthOffsets=new Vector3[HeadCount];
        private readonly List<Dust> m_dust=new List<Dust>();
        private readonly DynamicArray<ComponentBody> m_quakeBodies=new DynamicArray<ComponentBody>();
        private double m_biteStarted=-100,m_tailStarted=-100,m_roarStarted=-100,m_introStarted=-100;
        private double m_hitStarted=-100,m_staggerStarted=-100;
        private bool m_rage,m_introSkipped,m_tailImpactPuffed;
        private int m_roarLayer,m_biteLayer,m_introSoundPhase,m_introQuakePhase;
        private uint m_visualRevision;
        private float m_initialHealth=1f;
        private Vector3 m_tailMouthOffset;

        private struct Dust
        {
            internal Vector3 Start,Velocity;
            internal double Started;
            internal float Life,Size;
        }

        /// <summary>Begin the three-head bite presentation. The host settles each hit.</summary>
        internal void Bite(double started)
        {
            m_biteStarted=started;m_biteLayer=0;m_tailImpactPuffed=false;
        }

        /// <summary>Begin the serpent-tail sweep presentation.</summary>
        internal void TailAttack(double started)
        {
            m_tailStarted=started;m_tailImpactPuffed=false;
            SpawnDust(10,1.4f);
        }

        /// <summary>Begin the three-layer roar presentation.</summary>
        internal void Roar(double started)
        {
            m_roarStarted=started;m_roarLayer=0;
            SpawnDust(9,1.8f);
        }

        /// <summary>Begin the cinematic awakening. The host freezes AI for the duration.</summary>
        internal void BeginIntro(double started)
        {
            m_introStarted=started;m_introSkipped=false;m_introSoundPhase=0;m_roarLayer=0;
            SpawnDust(6,1.2f);
        }

        /// <summary>Fast-forward the local presentation after a host-authorised skip.</summary>
        internal void EndIntro(double now)
        {
            m_introStarted=now-CerberusRules.IntroDuration;
            m_introSkipped=true;m_introSoundPhase=3;m_roarLayer=3;
        }

        internal void SetRage(bool rage)=>m_rage=rage;

        internal bool Rage=>m_rage;

        /// <summary>Network presentation entry. Revision prevents duplicate or stale effects.</summary>
        internal void ReceiveEffect(int kind,uint revision,float age,int action=0)
        {
            if(age<0||!WorldCutRules.Finite(age)||!WorldCutRules.Newer(revision,m_visualRevision))return;
            double started=m_subsystemTime.GameTime-age;
            switch(kind)
            {
                case 8:
                    if(age>=CerberusRules.AnimationDuration)return;
                    m_visualRevision=revision;Bite(started);break;
                case 11:
                    if(age>=CerberusRules.TailDuration)return;
                    m_visualRevision=revision;TailAttack(started);break;
                case 12:
                    if(age>=CerberusRules.RoarDuration)return;
                    m_visualRevision=revision;Roar(started);break;
                case 13:
                    m_visualRevision=revision;
                    if(action==1){BeginIntro(started);m_introQuakePhase=0;}
                    else if(action==2)EndIntro(m_subsystemTime.GameTime);
                    else if(action==3)SetRage(age>=0.5f);
                    break;
            }
        }

        /// <inheritdoc/>
        public override void Load(ValuesDictionary values,IdToEntityMap map)
        {
            base.Load(values,map);
            if(Model.Bones.Count>CerberusRules.BoneCapacity)
                throw new InvalidOperationException("Cerberus exceeds the configured "+CerberusRules.BoneCapacity+"-bone model capacity.");
            m_chest=Model.FindBone("Chest");
            m_necks[0]=Model.FindBone("NeckC");m_necks[1]=Model.FindBone("NeckL");m_necks[2]=Model.FindBone("NeckR");
            m_heads[0]=Model.FindBone("Head");m_heads[1]=Model.FindBone("HeadL");m_heads[2]=Model.FindBone("HeadR");
            m_jaws[0]=Model.FindBone("Jaw");m_jaws[1]=Model.FindBone("JawL");m_jaws[2]=Model.FindBone("JawR");
            m_tailA=Model.FindBone("TailA");m_tailB=Model.FindBone("TailB");m_tailC=Model.FindBone("TailC");
            m_tailHead=Model.FindBone("TailHead");m_tailJaw=Model.FindBone("TailJaw");
            m_snakeC=Model.FindBone("SnakeC");m_snakeL=Model.FindBone("SnakeL");m_snakeR=Model.FindBone("SnakeR");
            m_snakeJaw=Model.FindBone("SnakeJaw");
            m_initialHealth=Math.Max(.01f,m_componentCreature.ComponentHealth.Health);
            // Bone-local presentation anchors. Geometry is authored with -Z forward
            // and +Y up, matching the existing original Cerberus asset pipeline.
            m_eyeOffsets[0]=new Vector3(0,.12f,-.31f);
            m_eyeOffsets[1]=new Vector3(0,.10f,-.26f);
            m_eyeOffsets[2]=new Vector3(0,.10f,-.26f);
            m_mouthOffsets[0]=new Vector3(0,-.10f,-.52f);
            m_mouthOffsets[1]=new Vector3(0,-.09f,-.47f);
            m_mouthOffsets[2]=new Vector3(0,-.09f,-.47f);
            m_tailMouthOffset=new Vector3(0,-.06f,-.42f);
        }

        /// <inheritdoc/>
        public override void Update(float dt)
        {
            // Native chase/butt hit moments must never add a second attack.
            AttackOrder=false;base.Update(dt);IsAttackHitMoment=false;AttackOrder=false;
            double now=m_subsystemTime.GameTime;
            float change=m_componentCreature.ComponentHealth.HealthChange;
            if(change<-.001f&&now-m_hitStarted>.18)
            {
                m_hitStarted=now;
                float fraction=MathUtils.Clamp(-change/Math.Max(.01f,m_initialHealth),0f,1f);
                if(fraction>.08f)m_staggerStarted=now;
                SpawnDust(5,.9f);
            }
            if(!m_rage&&m_componentCreature.ComponentHealth.Health<=m_initialHealth*CerberusRules.RageFraction)m_rage=true;
        }

        /// <inheritdoc/>
        public override void Animate()
        {
            base.Animate();
            double now=m_subsystemTime.GameTime;
            float t=(float)now;
            float speed=m_componentCreature.ComponentBody.Velocity.Length();
            bool moving=speed>.55f,running=speed>4.2f;
            bool alert=m_rage||IsIntroActive(now)||m_componentCreature.ComponentLocomotion.LookOrder.LengthSquared()>.01f;
            bool dead=m_componentCreature.ComponentHealth.Health<=0;
            float death=MathUtils.Saturate(DeathPhase);
            float breath=MathUtils.Sin(t*(m_rage?2.6f:1.45f))*.5f+.5f;
            float hit=Pulse(now-m_hitStarted,0,.35);
            float stagger=Pulse(now-m_staggerStarted,0,1.2);
            float intro=IntroLift(now),snakeRise=SnakeRise(now);
            float roarChest=0;

            // Body: keep the native gait/bob and add only presentation lean/flinch.
            Matrix body=Matrix.Identity;
            if(running)body*=Matrix.CreateRotationX(-.075f);
            else if(moving)body*=Matrix.CreateRotationX(-.025f);
            body*=Matrix.CreateRotationZ(.09f*stagger+.035f*hit);
            body*=Matrix.CreateRotationX(.055f*stagger-.025f*hit);
            if(dead)body=Matrix.Identity;
            ComposeBone(m_bodyBone,body);

            // Chest breathing/roar expansion. Necks are siblings of Chest, so this
            // scales the ribcage without stretching any head.
            float chestScale=1f+.018f*breath+(alert?.006f:0f);
            SetBoneTransform(m_chest.Index,Matrix.CreateScale(chestScale,1f+.012f*breath,chestScale));

            for(int i=0;i<HeadCount;i++)
            {
                float yaw=0,pitch=0,open=.045f,neckPitch=0,neckYaw=0;
                float phase=i*2.1f;
                // Idle/alert scanning. The side heads rest slightly outward.
                float scan=MathUtils.Sin(t*(alert?.95f:.42f)+phase);
                yaw=(i==0?scan*.11f:(i==1?.055f+scan*.085f:-.055f+scan*.085f));
                pitch=.02f+MathUtils.Sin(t*.7f+phase*1.3f)*.035f+(alert?.055f:0f);
                if(moving)
                {
                    pitch-=.025f+(running?.075f:0f);
                    yaw*=.55f;
                }
                if(stagger>0)
                {
                    pitch-=.12f*stagger;yaw+=(i==1?.10f:-.10f)*stagger;
                }
                if(hit>0)
                {
                    pitch-=.16f*hit;yaw+=(i==1?.07f:-.07f)*hit;
                }
                // Intro: heads stay low, then rise in sequence.
                if(IsIntroActive(now))
                {
                    float lift=MathUtils.Saturate((intro-(i==0?.10f:i==1?.25f:.35f))/.75f);
                    pitch=-.28f+.28f*lift;pitch+=.04f*lift;
                    yaw=(i==1?.08f:-.08f)*lift;
                    open=.05f+.10f*(1f-lift);
                }
                // Roar: left, right, centre; each head opens and lifts independently.
                double roarAge=now-m_roarStarted;
                if(roarAge>=0&&roarAge<CerberusRules.RoarDuration)
                {
                    float p=Pulse(roarAge,CerberusRules.RoarDelays[i],1.0);
                    pitch+=.30f*p;neckPitch+=.15f*p;open+=.52f*p;roarChest+=p;
                }
                // Intro roar is part of the awakening timeline and is not a second packet.
                if(IsIntroActive(now))
                {
                    double age=now-m_introStarted;
                    float p=Pulse(age,4.2+i*.55,1.0);
                    pitch+=.30f*p;neckPitch+=.15f*p;open+=.52f*p;roarChest+=p;
                }
                // Bite: three staggered lunges; only one head moves forward at a time.
                double biteAge=now-m_biteStarted;
                if(biteAge>=0&&biteAge<CerberusRules.AnimationDuration)
                {
                double start=i==1?CerberusRules.BiteDelays[0]:i==2?CerberusRules.BiteDelays[1]:CerberusRules.BiteDelays[2];
                    float lunge=Pulse(biteAge,start-.12,.34);
                    float wind=MathUtils.Saturate((float)((biteAge-(start-.20))/.20));
                    float snap=biteAge<start?wind:1f-MathUtils.Saturate((float)((biteAge-start)/.12));
                    open=MathUtils.Max(open,.05f+.40f*snap);
                    pitch-=.19f*lunge;neckPitch-=.13f*lunge;yaw*=.35f;
                }
                if(dead)
                {
                    pitch=MathUtils.Lerp(pitch,-.62f,death);yaw*=.35f;open=.05f+.18f*death;
                    neckPitch=MathUtils.Lerp(neckPitch,-.48f,death);
                }
                SetBoneTransform(m_necks[i].Index,Matrix.CreateFromYawPitchRoll(neckYaw,neckPitch,0f));
                SetBoneTransform(m_heads[i].Index,Matrix.CreateFromYawPitchRoll(yaw,pitch,(i==1?.04f:-.04f)*MathUtils.Saturate(hit+stagger)));
                SetBoneTransform(m_jaws[i].Index,Matrix.CreateRotationX(-open));
            }
            if(roarChest>0)
                SetBoneTransform(m_chest.Index,Matrix.CreateScale(1f+.035f*roarChest,1f+.025f*roarChest,1f+.035f*roarChest));

            // Serpent tail: continuous phase-lagged sway, plus a full sweep on attack.
            float tailBase=MathUtils.Sin(t*1.05f)*.13f+MathUtils.Sin(t*.47f)*.06f;
            float tailSweep=0,tailJawOpen=.03f;
            double tailAge=now-m_tailStarted;
            if(tailAge>=0&&tailAge<CerberusRules.TailDuration)
            {
                float p=MathUtils.Saturate((float)(tailAge/CerberusRules.TailDuration));
                tailSweep=MathUtils.Sin(p*MathUtils.PI)*1.05f;
                tailJawOpen=.04f+.55f*MathUtils.Sin(p*MathUtils.PI);
                if(!m_tailImpactPuffed&&tailAge>=CerberusRules.TailImpactDelay)
                {
                    m_tailImpactPuffed=true;SpawnDust(12,1.8f);
                    m_subsystemAudio.PlaySound("Audio/Creatures/WolfBark",.75f,-.30f,m_componentCreature.ComponentBody.Position,10f,false);
                }
            }
            if(dead)
            {
                tailBase*=.2f;tailSweep=0;tailJawOpen=.05f;
            }
            SetBoneTransform(m_tailA.Index,Matrix.CreateRotationY(tailBase+tailSweep*.40f)*Matrix.CreateRotationX(.02f*MathUtils.Sin(t*.9f)));
            SetBoneTransform(m_tailB.Index,Matrix.CreateRotationY(tailBase*.75f+tailSweep*.34f)*Matrix.CreateRotationX(.02f*MathUtils.Sin(t*.9f-.5f)));
            SetBoneTransform(m_tailC.Index,Matrix.CreateRotationY(tailBase*.55f+tailSweep*.28f)*Matrix.CreateRotationX(.03f*MathUtils.Sin(t*.9f-1f)));
            SetBoneTransform(m_tailHead.Index,Matrix.CreateRotationY(-tailSweep*.30f+MathUtils.Sin(t*.8f)*.08f)*Matrix.CreateRotationX(-.10f*death));
            SetBoneTransform(m_tailJaw.Index,Matrix.CreateRotationX(-tailJawOpen));

            // Snake mane: flat at rest, raised on alert/intro/rage, with restrained sway.
            float rise=MathUtils.Clamp((alert?.42f:0f)+snakeRise*.58f+(m_rage?.25f:0f),0f,1f);
            float snakeSway=MathUtils.Sin(t*1.7f)*(.05f+.09f*rise);
            float[] side={0f,.14f,-.14f};
            ModelBone[] snakes={m_snakeC,m_snakeL,m_snakeR};
            for(int i=0;i<3;i++)
            {
                float pitch=-.55f*rise+MathUtils.Sin(t*1.2f+i)*.05f*rise;
                float yaw=side[i]*rise+snakeSway;
                SetBoneTransform(snakes[i].Index,Matrix.CreateFromYawPitchRoll(yaw,pitch,0f));
            }
            SetBoneTransform(m_snakeJaw.Index,Matrix.CreateRotationX(-(.04f+.22f*rise+.18f*(m_rage?1f:0f))));

            // Intro front-paw step and stagger buckle are additive on native leg poses.
            float pawStep=Pulse(now-m_introStarted,2.45,.85);
            if(stagger>0||pawStep>0)
            {
                float buckle=.16f*stagger+.20f*pawStep;
                ComposeBone(m_leg1Bone,Matrix.CreateRotationX(buckle));
                ComposeBone(m_leg2Bone,Matrix.CreateRotationX(buckle*.85f));
            }

            UpdateSoundAndDust(now,moving);
        }

        private bool IsIntroActive(double now)=>!m_introSkipped&&now>=m_introStarted&&now<m_introStarted+CerberusRules.IntroDuration;

        private float IntroLift(double now)
        {
            if(!IsIntroActive(now))return 1f;
            double age=now-m_introStarted;
            return MathUtils.Saturate((float)((age-1.2)/2.0));
        }

        private float SnakeRise(double now)
        {
            if(!IsIntroActive(now))return m_rage?.55f:0f;
            double age=now-m_introStarted;
            return MathUtils.Saturate((float)((age-1.0)/1.6));
        }

        private void UpdateSoundAndDust(double now,bool moving)
        {
            if(IsIntroActive(now))
            {
                double age=now-m_introStarted;
                if(m_introSoundPhase<1&&age>=0)
                {
                    m_introSoundPhase=1;
                    m_subsystemAudio.PlaySound("Audio/Creatures/WolfGrowl",.55f,-.32f,m_componentCreature.ComponentBody.Position,8f,false);
                }
                if(m_introSoundPhase<2&&age>=2.45)
                {
                    m_introSoundPhase=2;
                    m_subsystemAudio.PlaySound("Audio/Creatures/WolfBark",.55f,-.28f,m_componentCreature.ComponentBody.Position,10f,false);
                    SpawnDust(10,1.6f);Quake(.30f);
                }
                int quake=(int)Math.Floor((age-4.2)/.55)+1;
                if(quake>m_introQuakePhase&&age>=4.2){m_introQuakePhase=quake;Quake(.42f);}
            }
            double roarAge=now-m_roarStarted;
            if(roarAge>=0&&roarAge<CerberusRules.RoarDuration&&m_roarLayer<3)
            {
                while(m_roarLayer<3&&roarAge>=CerberusRules.RoarDelays[m_roarLayer])
                {
                    float pitch=new[]{-.18f,-.05f,-.30f}[m_roarLayer];
                    m_subsystemAudio.PlaySound("Audio/Creatures/WolfGrowl",m_roarLayer==2?1f:.78f,pitch,m_componentCreature.ComponentBody.Position,14f,false);
                    m_roarLayer++;
                }
            }
            double biteAge=now-m_biteStarted;
            if(biteAge>=0&&biteAge<CerberusRules.AnimationDuration&&m_biteLayer<3)
            {
                while(m_biteLayer<3&&biteAge>=CerberusRules.BiteDelays[m_biteLayer])
                {
                    m_subsystemAudio.PlaySound("Audio/Creatures/WolfBark",.72f,-.08f,m_componentCreature.ComponentBody.Position,10f,false);
                    m_biteLayer++;
                }
            }
            for(int i=m_dust.Count-1;i>=0;i--)if(now-m_dust[i].Started>m_dust[i].Life)m_dust.RemoveAt(i);
            if(moving&&m_dust.Count<6&&m_random.Float()<.06)SpawnDust(1,.9f);
        }

        /// <summary>Local ground shake for nearby players; never alters movement or sync.</summary>
        private void Quake(float strength)
        {
            SubsystemBodies bodies=Project?.FindSubsystem<SubsystemBodies>(false);
            if(bodies==null)return;
            Vector3 center=m_componentCreature.ComponentBody.Position;
            m_quakeBodies.Clear();
            bodies.FindBodiesInArea(new Vector2(center.X-18f,center.Z-18f),new Vector2(center.X+18f,center.Z+18f),m_quakeBodies);
            for(int i=0;i<m_quakeBodies.Count;i++)
            {
                ComponentBody body=m_quakeBodies.Array[i];
                if(body==null||body.Player==null)continue;
                float distance=Vector3.Distance(body.Position,center);
                if(distance>18f)continue;
                body.ApplyShaking(strength*(1f-distance/18f));
            }
            m_quakeBodies.Clear();
        }

        private void SpawnDust(int count,float radius)
        {
            if(m_dust.Count>=32)return;
            Vector3 center=m_componentCreature.ComponentBody.Position+Vector3.UnitY*.08f;
            Vector3 forward=m_componentCreature.ComponentBody.Matrix.Forward;
            Vector3 right=m_componentCreature.ComponentBody.Matrix.Right;
            for(int i=0;i<count&&m_dust.Count<32;i++)
            {
                float a=m_random.Float()*2f-1f;
                float b=m_random.Float();
                m_dust.Add(new Dust
                {
                    Start=center+forward*(.6f+b*radius)+right*(a*radius),
                    Velocity=right*(a*.22f)+forward*(.10f+b*.20f),
                    Started=m_subsystemTime.GameTime,
                    Life=.55f+.35f*b,
                    Size=.14f+.20f*b
                });
            }
        }

        /// <summary>Compose an additive local rotation on a bone already posed by the native gait.</summary>
        private void ComposeBone(ModelBone bone,Matrix extra)
        {
            Matrix current=GetBoneTransform(bone.Index)??Matrix.Identity;
            SetBoneTransform(bone.Index,current*extra);
        }

        /// <inheritdoc/>
        public override Vector3 CalculateEyePosition()=>m_componentCreature.ComponentBody.Position+Vector3.UnitY*2.85f+m_componentCreature.ComponentBody.Matrix.Forward*1.65f;

        /// <inheritdoc/>
        public override void DrawExtras(Camera camera)
        {
            base.DrawExtras(camera);
            if(m_componentCreature.ComponentHealth.Health<=0)return;
            m_fx.Clear();
            try
            {
                var batch=m_fx.FlatBatch(0,DepthStencilState.DepthRead,RasterizerState.CullNone,BlendState.NonPremultiplied);
                float t=(float)m_subsystemTime.GameTime;
                float intro=IsIntroActive(m_subsystemTime.GameTime)?MathUtils.Saturate((float)((m_subsystemTime.GameTime-m_introStarted)/1.4)):1f;
                float glow=(m_rage?1f:.72f)*(.88f+.12f*MathUtils.Sin(t*3.1f));
                Color[] eyeColors={new Color(222,180,84),new Color(142,226,238),new Color(232,158,66)};
                Matrix body=BodyWorld();
                for(int i=0;i<HeadCount;i++)
                {
                    Matrix head=body*BoneAbsolute(m_heads[i]);
                    float alpha=i==0?glow:(glow*.86f);
                    Color eye=eyeColors[i];
                    Vector3 right=head.Right*.050f,up=head.Up*.030f,fwd=head.Forward*.012f;
                    Color c=new Color(eye.R,eye.G,eye.B,(int)(205*alpha*intro));
                    float spread=i==0?.25f:.225f;
                    for(int s=-1;s<=1;s+=2)
                    {
                        Vector3 center=Vector3.Transform(m_eyeOffsets[i]+new Vector3(s*spread,0,0),head);
                        batch.QueueTriangle(center-right,center+right,center+up+fwd,c);
                        batch.QueueTriangle(center-right,center+right,center-up+fwd,c);
                    }
                }
                // Poison drool: restrained pale strands, stronger while biting or enraged.
                double biteAge=m_subsystemTime.GameTime-m_biteStarted;
                float bite= biteAge>=0&&biteAge<CerberusRules.AnimationDuration?1f:0f;
                float drool=(m_rage?.55f:.28f)+.35f*bite;
                for(int i=0;i<HeadCount;i++)
                {
                    Matrix head=body*BoneAbsolute(m_heads[i]);
                    Vector3 at=Vector3.Transform(m_mouthOffsets[i],head);
                    Vector3 down=head.Up*(.16f+.12f*drool),side=head.Right*.028f;
                    Color c=new Color(202,220,224,(int)(80+90*drool));
                    batch.QueueTriangle(at-side,at+side,at-down,c);
                }
                if(m_subsystemTime.GameTime-m_tailStarted>=0&&m_subsystemTime.GameTime-m_tailStarted<CerberusRules.TailDuration)
                {
                    Matrix tail=body*BoneAbsolute(m_tailHead);
                    Vector3 at=Vector3.Transform(m_tailMouthOffset,tail);
                    Vector3 down=tail.Up*.13f,side=tail.Right*.024f;
                    batch.QueueTriangle(at-side,at+side,at-down,new Color(202,220,224,120));
                }
                // Ground dust puffs from roar, paw step, tail impact and heavy hits.
                for(int i=0;i<m_dust.Count;i++)
                {
                    Dust d=m_dust[i];float age=(float)(m_subsystemTime.GameTime-d.Started);
                    if(age<0||age>d.Life)continue;
                    Vector3 pos=d.Start+d.Velocity*age-Vector3.UnitY*(.28f*age*age);
                    float size=d.Size*(.6f+age*1.2f),fade=1f-age/d.Life;
                    Color c=new Color(118,112,104,(int)(52*fade));
                    batch.QueueQuad(pos-Vector3.UnitX*size-Vector3.UnitZ*size,pos+Vector3.UnitX*size-Vector3.UnitZ*size,pos+Vector3.UnitX*size+Vector3.UnitZ*size,pos-Vector3.UnitX*size+Vector3.UnitZ*size,c);
                }
                m_fx.Flush(camera.ViewProjectionMatrix);
            }
            finally{m_fx.Clear();}
        }

        private Matrix BodyWorld()
        {
            var body=m_componentCreature.ComponentBody;
            return Matrix.CreateFromQuaternion(body.Rotation)*Matrix.CreateTranslation(body.Position+Vector3.UnitY*Bob);
        }

        private Matrix BoneAbsolute(ModelBone bone)
        {
            if(bone==null)return Matrix.Identity;
            Matrix local=bone.Transform;
            Vector3 translation=local.Translation;
            local.Translation=Vector3.Zero;
            local*=GetBoneTransform(bone.Index)??Matrix.Identity;
            local.Translation+=translation;
            return bone.ParentBone!=null?BoneAbsolute(bone.ParentBone)*local:local;
        }

        private static float Pulse(double age,double start,double duration)
        {
            if(age<start||age>start+duration)return 0f;
            return MathUtils.Sin((float)((age-start)/duration)*MathUtils.PI);
        }

        /// <inheritdoc/>
        public override void Dispose()
        {
            m_fx.Clear();m_dust.Clear();base.Dispose();
        }
    }
}

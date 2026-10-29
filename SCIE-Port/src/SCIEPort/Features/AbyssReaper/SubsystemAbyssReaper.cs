using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;
using Game.NetWork;
using TemplatesDatabase;
namespace SCIEPort.Features.AbyssReaper
{
    /// <summary>Local/server-owned melee; no terrain writes or client damage packets.</summary>
    public sealed class SubsystemAbyssReaper : SubsystemBlockBehavior, IUpdateable, IDrawable
    {
        sealed class State { public AbyssCombo Combo=new AbyssCombo(); public Ray3 Ray; public WorldCutHud Hud; public WorldCutInputGate InputGate=new WorldCutInputGate(); public bool NormalImpactPending; public double RiftUntil; }
        readonly Dictionary<ComponentPlayer,State> states=new Dictionary<ComponentPlayer,State>();
        readonly List<ComponentPlayer> retired=new List<ComponentPlayer>();
        readonly HashSet<GameEntitySystem.Entity> hit=new HashSet<GameEntitySystem.Entity>();
        readonly PrimitivesRenderer3D renderer=new PrimitivesRenderer3D();
        SubsystemTime clock; SubsystemTerrain terrain; SubsystemBodies bodies; SubsystemPlayers players;
        bool allowPlayers,allowPassive,showRifts;
        internal WorldCutRuntime WorldCut { get; private set; }
        internal VoidPortalRegistry Portals { get; private set; }
        private string portalLoadFailure;
        /// <inheritdoc/>
        public override int[] HandledBlocks => new[]{AbyssReaperBlock.Index};
        /// <inheritdoc/>
        public UpdateOrder UpdateOrder => (UpdateOrder)((int)UpdateOrder.Input + 1);
        /// <inheritdoc/>
        public int[] DrawOrders {get;}=new[]{110};
        /// <inheritdoc/>
        public override void Load(ValuesDictionary v) {base.Load(v);players=Project.FindSubsystem<SubsystemPlayers>(true);clock=Project.FindSubsystem<SubsystemTime>(true);terrain=Project.FindSubsystem<SubsystemTerrain>(true);bodies=Project.FindSubsystem<SubsystemBodies>(true);allowPlayers=v.GetValue<bool>("AllowPlayers",false);allowPassive=v.GetValue<bool>("AllowPassive",false);showRifts=v.GetValue<bool>("ShowRifts",true);
            string savedPortals=v.GetValue<string>("VoidPortalRecords", "");
            VoidPortalRegistry loaded;
            if (string.IsNullOrEmpty(savedPortals)) Portals=new VoidPortalRegistry(Guid.NewGuid(),v.GetValue<int>("VoidPortalLimit",VoidPortalRegistry.DefaultLimit));
            else if(VoidPortalRegistry.TryDeserialize(savedPortals,out loaded)) Portals=loaded;
            else { portalLoadFailure=savedPortals; Log.Error("[SCIEPort:Abyss] Portal records invalid or unsupported; retained unchanged and portal operations disabled."); }
            WorldCut=new WorldCutRuntime(Project,players,clock,terrain,bodies,allowPlayers,allowPassive,showRifts,v.GetValue<bool>("WorldCutLowEffects",false),WorldCutRules.Damage(v.GetValue<float>("WorldCutInitialDamage",100f),100),WorldCutRules.Damage(v.GetValue<float>("WorldCutCloseDamage",40f),40));}
        /// <inheritdoc/>
        public override void Save(ValuesDictionary values)
        {
            base.Save(values);
            values.SetValue("VoidPortalRecords", portalLoadFailure ?? Portals.Serialize());
        }
        /// <summary>Consumes only world attack rays for this held item; native UI is processed first.</summary>
        public void Capture(ComponentInput input) {
            var p=input.m_componentPlayer; if(p==null)return;
            State s;bool active=Terrain.ExtractContents(p.ComponentMiner.ActiveBlockValue)==AbyssReaperBlock.Index;
            if(!active){if(states.TryGetValue(p,out s)){Remove(p,s);states.Remove(p);}return;}
            if(!states.TryGetValue(p,out s)){s=new State();states.Add(p,s);}
            bool blocked=!p.PlayerData.IsReadyForPlaying||clock.GameTimeDelta<=0||p.ComponentGui.ModalPanelWidget!=null||DialogsManager.HasDialogs(p.GuiWidget)||p.ComponentHealth.Health<=0||!input.AllowHandleInput||!Engine.Window.IsActive||p.ComponentSleep.SleepFactor>0||p.GameWidget==null||!p.GameWidget.ActiveCamera.IsEntityControlEnabled;
            var ray=input.m_playerInput.Dig??input.m_playerInput.Hit??input.m_playerInput.Aim;
            if(ray.HasValue)s.Ray=ray.Value;
            // Clear native melee/dig even on a blocked frame; never attack or mine via vanilla fallback.
            input.m_playerInput.Hit=null;input.m_playerInput.Dig=null;input.m_playerInput.Aim=null;
            input.m_playerInput.Interact=null;
            if(blocked){s.Combo=new AbyssCombo();s.NormalImpactPending=false;s.Hud?.CancelInput();s.InputGate.Reset();WorldCut.Cancel(p);return;}
            float elapsed=0;
            var touch=p.ComponentGui.ViewWidget?.TouchInput;
            if(touch.HasValue&&touch.Value.InputType==TouchInputType.Hold)elapsed=touch.Value.Duration;
            bool skillPressed=s.Hud?.Poll(p.GuiWidget.Input)==true;
            if(skillPressed)
            {
                bool meleeBusy=s.NormalImpactPending || s.Combo.Phase(clock.GameTime)<1 || s.Combo.Charge(clock.GameTime)>0;
                if(s.InputGate.CanRequest(ray.HasValue,meleeBusy,WorldCut.Busy(p)))
                { if(WorldCut.Request(p))s.InputGate.Accepted(); }
                else WorldCut.Report(p,WorldCutFailure.Busy);
            }
            if(s.InputGate.RouteNormal(ray.HasValue,WorldCut.Busy(p)))
            {
                s.Combo.Input(ray.HasValue,clock.GameTime,elapsed);
                if(s.Combo.Phase(clock.GameTime)==0)s.NormalImpactPending=true;
            }
        }
        /// <inheritdoc/>
        public override bool OnUse(Ray3 ray,ComponentMiner miner)=>true;
        /// <inheritdoc/>
        public void Update(float dt) {
            // Native UpdateInput Hook is before touch/gamepad assembly; consume the completed input
            // here at Input+1 (-9), strictly before ComponentPlayer Default (0).
            foreach(var player in players.ComponentPlayers)
                if(player.GameWidget!=null&&(CommonLib.WorkType==WorkType.Local||player.PlayerData.IsMainPlayer))Capture(player.ComponentInput);
            retired.Clear(); double now=clock.GameTime;
            foreach(var pair in states){var p=pair.Key;var s=pair.Value;
                if(!p.Entity.IsAddedToProject||p.ComponentHealth.Health<=0||Terrain.ExtractContents(p.ComponentMiner.ActiveBlockValue)!=AbyssReaperBlock.Index){retired.Add(p);continue;}
                if(WorldCut.Busy(p))continue;
                float t=s.Combo.Phase(now);float arc=(float)Math.Sin(t*Math.PI);
                Vector3 rotation=s.Combo.Stage==0?new Vector3(0,(t*2-1)*1.15f,-arc*.3f):s.Combo.Stage==1?new Vector3(0,(1-t*2)*1.3f,arc*.35f):new Vector3((t*2-1)*1.2f,0,arc*.25f);
                if(t>=1)rotation=Vector3.Zero;
                if(s.Combo.Charge(now)>0)rotation=new Vector3(-.65f*s.Combo.Charge(now),0,-.2f);
                var fp=p.Entity.FindComponent<ComponentFirstPersonModel>();if(fp!=null)fp.ItemRotationOrder=rotation;
                p.ComponentCreatureModel.InHandItemRotationOrder=rotation;
                if(s.Combo.Impact(now)){s.NormalImpactPending=false;if(s.Combo.Stage>=2)s.RiftUntil=now+.22;Strike(p,s);}
            }
            foreach(var p in retired){Remove(p,states[p]);states.Remove(p);}
            WorldCut.Update(dt);
        }
        void Strike(ComponentPlayer p,State s) {
            if(CommonLib.WorkType==WorkType.Client)return;
            if(CommonLib.WorkType==WorkType.Server&&!p.PlayerData.IsMainPlayer)return; // No untrusted remote input path in V0.1.
            Vector3 origin=p.ComponentCreatureModel.EyePosition;
            Vector3 forward=p.ComponentCreatureModel.EyeRotation.GetForwardVector();
            Vector3 flat=new Vector3(forward.X,0,forward.Z); if(flat.LengthSquared()<.001f)return;flat=Vector3.Normalize(flat);
            hit.Clear();int visited=0;
            foreach(var body in bodies.Bodies){if(++visited>512)break;
                if(body.Entity==p.Entity||!body.Entity.IsAddedToProject||hit.Contains(body.Entity))continue;
                var c=body.Entity.FindComponent<ComponentCreature>();if(c==null||c.ComponentHealth.Health<=0)continue;
                bool player=c is ComponentPlayer;
                bool hostile=c.Category==CreatureCategory.LandPredator||c.Category==CreatureCategory.WaterPredator;
                if(player?!allowPlayers:(!hostile&&!allowPassive))continue;
                Vector3 center=body.BoundingBox.Center();Vector3 delta=center-origin;
                Vector3 horizontal=new Vector3(delta.X,0,delta.Z);float len=horizontal.Length();
                if(!AbyssCombo.Sector(delta.Length(),len>.01f?Vector3.Dot(horizontal/len,flat):1,delta.Y,s.Combo.Stage))continue;
                Territoriy region;if(SubsystemBedrockBlockBehavior.CheckIsInTerritoriy(Terrain.ToCell(center.X),Terrain.ToCell(center.Z),out region))continue;
                if(terrain.Raycast(origin,center,false,true,(v,d)=>BlocksManager.Blocks[Terrain.ExtractContents(v)].IsCollidable).HasValue)continue;
                hit.Add(body.Entity);
                ComponentMiner.AttackBody(body,p,center,forward,s.Combo.Stage==3?52:s.Combo.Stage==2?34:22,true);
                if(hit.Count>=24)break;
            }
        }
        /// <summary>Only the second touch slot is unlocked; other skills remain unavailable.</summary>
        public void TickGui(ComponentGui gui) {
            State s;var p=gui?.m_componentPlayer;if(p==null||!states.TryGetValue(p,out s))return;
            if(s.Hud==null)s.Hud=new WorldCutHud(gui);
            string hint=WorldCut.Feedback(p);
            if(hint==null)hint=s.Combo.Charge(clock.GameTime)>=1?"蓄力完成 · 松开重斩":s.Combo.Charge(clock.GameTime)>0?"普攻蓄力中":"轻触连斩 · 长按蓄力";
            s.Hud.Update(WorldCut.Remaining(p),WorldCut.Busy(p),WorldCut.PendingRequest(p),hint,
                gui.ModalPanelWidget!=null||DialogsManager.HasDialogs(p.GuiWidget)||p.ComponentHealth.Health<=0||!p.ComponentInput.AllowHandleInput);
        }
        /// <inheritdoc/>
        public void Draw(Camera camera,int order) {
            WorldCut?.Draw(camera);
            if(!showRifts)return;var batch=renderer.FlatBatch(-1,DepthStencilState.DepthRead,RasterizerState.CullNone,BlendState.AlphaBlend);
            foreach(var pair in states){var p=pair.Key;var s=pair.Value;if(s.RiftUntil<=clock.GameTime)continue;
                Vector3 c=p.ComponentCreatureModel.EyePosition+p.ComponentCreatureModel.EyeRotation.GetForwardVector()*1.8f;
                for(int i=0;i<5;i++){Vector3 a=c+new Vector3((i%2==0?-.08f:.07f),.65f-i*.25f,0);Vector3 b=c+new Vector3((i%2==0?.07f:-.08f),.40f-i*.25f,0);batch.QueueLine(a,b,new Color(159,100,207));}}
            renderer.Flush(camera.ViewProjectionMatrix);
        }
        void Remove(ComponentPlayer p,State s){s.Hud?.Dispose();s.Hud=null;s.InputGate.Reset();WorldCut?.Cancel(p);}
        /// <inheritdoc/>
        public override void Dispose(){WorldCut?.Dispose();foreach(var x in states)Remove(x.Key,x.Value);states.Clear();WorldCut=null;renderer.Clear();base.Dispose();}
    }
}

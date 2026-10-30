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
        sealed class State { public AbyssCombo Combo=new AbyssCombo(); public Ray3 Ray; public WorldCutHud Hud; public VoidPortalDialog PortalDialog; public WorldCutInputGate InputGate=new WorldCutInputGate(); public bool NormalImpactPending; public double RiftUntil,CombatTime,LastSample; }
        readonly Dictionary<ComponentPlayer,State> states=new Dictionary<ComponentPlayer,State>();
        readonly Dictionary<ComponentPlayer,float> walkBonuses=new Dictionary<ComponentPlayer,float>();
        readonly List<ComponentPlayer> speedRetired=new List<ComponentPlayer>();
        readonly List<ComponentPlayer> retired=new List<ComponentPlayer>();
        readonly HashSet<GameEntitySystem.Entity> hit=new HashSet<GameEntitySystem.Entity>();
        readonly PrimitivesRenderer3D renderer=new PrimitivesRenderer3D();
        readonly AbyssMeleeScene meleeScene=new AbyssMeleeScene();
        VoidPortalScene portalScene;
        internal VoidPortalOperations PortalOperations { get; private set; }
        private VoidPortalLocalTravel portalTravel;
        private VoidBlinkRuntime blink;
        internal VoidHorizonRuntime Horizon { get; private set; }
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
        public int[] DrawOrders {get;}=new[]{110,VoidHorizonRules.DrawOrder};
        /// <inheritdoc/>
        public override void Load(ValuesDictionary v) {base.Load(v);players=Project.FindSubsystem<SubsystemPlayers>(true);clock=Project.FindSubsystem<SubsystemTime>(true);terrain=Project.FindSubsystem<SubsystemTerrain>(true);bodies=Project.FindSubsystem<SubsystemBodies>(true);allowPlayers=v.GetValue<bool>("AllowPlayers",false);allowPassive=v.GetValue<bool>("AllowPassive",false);showRifts=v.GetValue<bool>("ShowRifts",true);
            string savedPortals=v.GetValue<string>("VoidPortalRecords", "");
            VoidPortalRegistry loaded;
            if (string.IsNullOrEmpty(savedPortals)) Portals=new VoidPortalRegistry(Guid.NewGuid(),v.GetValue<int>("VoidPortalLimit",VoidPortalRegistry.DefaultLimit));
            else if(VoidPortalRegistry.TryDeserialize(savedPortals,out loaded)) Portals=loaded;
            else { portalLoadFailure=savedPortals; Log.Error("[SCIEPort:Abyss] Portal records invalid or unsupported; retained unchanged and portal operations disabled."); }
            if(Portals!=null && !Portals.SetLimit(v.GetValue<int>("VoidPortalLimit",VoidPortalRegistry.DefaultLimit)))
                Log.Warning("[SCIEPort:Abyss] Portal limit below existing count; previous limit retained.");
            PortalOperations=new VoidPortalOperations(this,terrain);
            portalScene=new VoidPortalScene(v.GetValue<int>("PortalEffectQuality",1));
            blink=new VoidBlinkRuntime(terrain,bodies,clock,Project.FindSubsystem<SubsystemAudio>(false),allowPlayers,allowPassive,v.GetValue<int>("BlinkEffectQuality",1));
            Horizon=new VoidHorizonRuntime(terrain,bodies,clock,Project.FindSubsystem<SubsystemAudio>(false),players,allowPassive,v.GetValue<int>("HorizonEffectQuality",1));
            if(Portals!=null)portalTravel=new VoidPortalLocalTravel(terrain,players,clock,Portals);
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
            if(!states.TryGetValue(p,out s)){s=new State { CombatTime=clock.GameTime,LastSample=clock.GameTime };states.Add(p,s);}
            float attackFrequency=1.1f*(Project.FindSubsystem<SCIEPort.Features.FourSaints.SubsystemFourSaints>(false)?.AttackFrequency(p)??1f);
            s.CombatTime+=Math.Max(0,clock.GameTime-s.LastSample)*attackFrequency;s.LastSample=clock.GameTime;
            bool blocked=!p.PlayerData.IsReadyForPlaying||clock.GameTimeDelta<=0||p.ComponentGui.ModalPanelWidget!=null||DialogsManager.HasDialogs(p.GuiWidget)||p.ComponentHealth.Health<=0||!input.AllowHandleInput||!Engine.Window.IsActive||p.ComponentSleep.SleepFactor>0||p.GameWidget==null||!p.GameWidget.ActiveCamera.IsEntityControlEnabled
                ||Project.FindSubsystem<SCIEPort.Features.FourSaints.SubsystemFourSaints>(false)?.IsActionLocked(p)==true;
            var ray=input.m_playerInput.Dig??input.m_playerInput.Hit??input.m_playerInput.Aim;
            var interact=input.m_playerInput.Interact;
            if(ray.HasValue)s.Ray=ray.Value;
            // Clear native melee/dig even on a blocked frame; never attack or mine via vanilla fallback.
            input.m_playerInput.Hit=null;input.m_playerInput.Dig=null;input.m_playerInput.Aim=null;
            input.m_playerInput.Interact=null;
            if(blocked){s.Combo=new AbyssCombo();s.NormalImpactPending=false;s.Hud?.CancelInput();s.InputGate.Reset();WorldCut.Cancel(p);blink.Cancel(p);Horizon.Cancel(p);return;}
            if(Horizon.Busy(p)){VoidHorizonRuntime.LockInput(p);s.Hud?.CancelInput();return;}
            if(interact.HasValue && !ray.HasValue && !WorldCut.Busy(p) && !Horizon.Busy(p) && !blink.Busy(p) && !s.NormalImpactPending && s.Combo.Phase(s.CombatTime)>=1)
            {
                Guid aimed=FindPortal(p,interact.Value.Direction);
                if(aimed!=Guid.Empty && OpenPortalDialog(p,aimed)){s.InputGate.Accepted();return;}
            }
            float elapsed=0;
            var touch=p.ComponentGui.ViewWidget?.TouchInput;
            if(touch.HasValue&&touch.Value.InputType==TouchInputType.Hold)elapsed=touch.Value.Duration;
            int skillPressed=s.Hud?.Poll(p.GuiWidget.Input)??0;
            if(skillPressed!=0)
            {
                bool meleeBusy=s.NormalImpactPending || s.Combo.Phase(s.CombatTime)<1 || s.Combo.Charge(s.CombatTime)>0;
                if(s.InputGate.CanRequest(ray.HasValue,meleeBusy,WorldCut.Busy(p)||Horizon.Busy(p)||blink.Busy(p)))
                { if(skillPressed==1) { if(PortalAction(p))s.InputGate.Accepted(); }
                  else if(skillPressed==2) { if(WorldCut.Request(p))s.InputGate.Accepted(); }
                  else { try { string reason;bool accepted=skillPressed==3?blink.TryCast(p,out reason):Horizon.TryCast(p,out reason);if(accepted)s.InputGate.Accepted();p.ComponentGui.DisplaySmallMessage(reason,Color.White,false,false); }
                    catch(Exception error){Log.Warning("[SCIEPort:Abyss] Skill request failed: "+error.GetType().Name);p.ComponentGui.DisplaySmallMessage("技能释放失败",Color.Yellow,false,false);} } }
                else WorldCut.Report(p,WorldCutFailure.Busy);
            }
            if(s.InputGate.RouteNormal(ray.HasValue,WorldCut.Busy(p)||Horizon.Busy(p)||blink.Busy(p)))
            {
                s.Combo.Input(ray.HasValue,s.CombatTime,elapsed*attackFrequency);
                if(s.Combo.Phase(s.CombatTime)==0)s.NormalImpactPending=true;
            }
        }
        internal bool OtherSkillBusy(ComponentPlayer p) => WorldCut?.Busy(p)==true || blink?.Busy(p)==true;
        private bool PortalAction(ComponentPlayer p)
        {
            if(CommonLib.WorkType==WorkType.Client) { p.ComponentGui.DisplaySmallMessage("星门管理暂由主机操作",Color.Yellow,false,false);return false; }
            if(Portals==null) { p.ComponentGui.DisplaySmallMessage("星门记录不可用，原存档已保留",Color.Yellow,false,false);return false; }
            Vector3 eye=p.ComponentCreatureModel.EyePosition, direction=p.ComponentCreatureModel.EyeRotation.GetForwardVector();
            if(!WorldCutDamage.Finite(eye)||!WorldCutDamage.Finite(direction)||direction.LengthSquared()<.1f)return false;
            direction=Vector3.Normalize(direction);Guid found=FindPortal(p,direction);
            if(found==Guid.Empty)
            {
                var hit=terrain.Raycast(eye,eye+direction*6f,false,true,(v,d)=>BlocksManager.Blocks[Terrain.ExtractContents(v)].IsCollidable);
                if(!hit.HasValue || hit.Value.CellFace.Face!=4) { p.ComponentGui.DisplaySmallMessage("请对准 6 格内平整地面",Color.Yellow,false,false);return false; }
                CellFace face=hit.Value.CellFace;bool alongX=Math.Abs(direction.Z)>=Math.Abs(direction.X);
                if(!PortalOperations.TryCreate(p,face.X,face.Y+1,face.Z,alongX,"星门 "+(Portals.Count+1),out found))
                { p.ComponentGui.DisplaySmallMessage("星门创建失败：空间、地面、领地或数量限制",Color.Yellow,false,false);return false; }
            }
            return OpenPortalDialog(p,found);
        }
        private Guid FindPortal(ComponentPlayer player,Vector3 direction)
        {
            if(CommonLib.WorkType==WorkType.Client||Portals==null||!WorldCutDamage.Finite(direction)||direction.LengthSquared()<.1f)return Guid.Empty;
            direction=Vector3.Normalize(direction);Vector3 eye=player.ComponentCreatureModel.EyePosition;Guid found=Guid.Empty;float nearest=6f;
            foreach(var portal in Portals.Records)
            {
                Vector3 center=new Vector3(portal.X,portal.Y+2f,portal.Z),delta=center-eye;float along=Vector3.Dot(delta,direction);
                if(!portal.Active||along<0||along>nearest||(delta-direction*along).LengthSquared()>2.25f)continue;
                if(terrain.Raycast(eye,center,false,true,(v,d)=>BlocksManager.Blocks[Terrain.ExtractContents(v)].IsCollidable).HasValue)continue;
                nearest=along;found=portal.Id;
            }
            return found;
        }
        private bool OpenPortalDialog(ComponentPlayer p,Guid id)
        {
            State state;if(!states.TryGetValue(p,out state)||state.PortalDialog!=null)return false;
            state.PortalDialog=new VoidPortalDialog(this,p,id);
            DialogsManager.ShowDialog(p.GameWidget.GuiWidget,state.PortalDialog);
            return true;
        }
        internal void ClosePortalDialog(ComponentPlayer p,VoidPortalDialog dialog)
        {
            State s;if(p!=null && states.TryGetValue(p,out s) && s.PortalDialog==dialog)s.PortalDialog=null;
            dialog.Release();DialogsManager.HideDialog(dialog);
        }
        /// <inheritdoc/>
        public override bool OnUse(Ray3 ray,ComponentMiner miner)=>true;
        /// <inheritdoc/>
        public void Update(float dt) {
            UpdateHeldSpeed();
            // Native UpdateInput Hook is before touch/gamepad assembly; consume the completed input
            // here at Input+1 (-9), strictly before ComponentPlayer Default (0).
            foreach(var player in players.ComponentPlayers)
                if(player.GameWidget!=null&&(CommonLib.WorkType==WorkType.Local||player.PlayerData.IsMainPlayer))Capture(player.ComponentInput);
            retired.Clear(); double now=clock.GameTime;
            foreach(var pair in states){var p=pair.Key;var s=pair.Value;
                if(!p.Entity.IsAddedToProject||p.ComponentHealth.Health<=0||Terrain.ExtractContents(p.ComponentMiner.ActiveBlockValue)!=AbyssReaperBlock.Index){retired.Add(p);continue;}
                if(WorldCut.Busy(p)||Horizon.Busy(p)||blink.Busy(p))continue;
                float t=s.Combo.Phase(s.CombatTime);
                var pose=AbyssMeleeMotion.Sample(s.Combo.Stage,t,s.Combo.Charge(s.CombatTime));
                Vector3 rotation=new Vector3(pose.Pitch,pose.Yaw,pose.Roll),offset=new Vector3(pose.X,pose.Y,pose.Z);
                var fp=p.Entity.FindComponent<ComponentFirstPersonModel>();
                if(fp!=null){fp.ItemRotationOrder=rotation;fp.ItemOffsetOrder=offset;}
                p.ComponentCreatureModel.InHandItemRotationOrder=rotation;
                p.ComponentCreatureModel.InHandItemOffsetOrder=offset*.35f;
                if(s.Combo.Impact(s.CombatTime)){s.NormalImpactPending=false;if(s.Combo.Stage>=2)s.RiftUntil=now+.22;Strike(p,s);}
            }
            foreach(var p in retired){Remove(p,states[p]);states.Remove(p);}
            WorldCut.Update(dt);
            try { portalTravel?.Update(); } catch(Exception error) { Log.Warning("[SCIEPort:Abyss] Portal travel cancelled: "+error.GetType().Name); }
            blink?.Update();
            Horizon?.Update();
        }
        private void ApplyHeldSpeed(ComponentPlayer p,bool equipped)
        {
            float owned;if(walkBonuses.TryGetValue(p,out owned))p.ComponentLocomotion.WalkSpeed-=owned;
            walkBonuses.Remove(p);
            float baseline=p.ComponentLocomotion.WalkSpeed;
            if(equipped && walkBonuses.Count<64 && p.IsAddedToProject && p.ComponentHealth.Health>0 && WorldCutRules.Finite(baseline))
            {
                float delta=Math.Max(0,baseline)*.2f;
                walkBonuses[p]=delta;p.ComponentLocomotion.WalkSpeed+=delta;
            }
        }
        private void UpdateHeldSpeed()
        {
            speedRetired.Clear();
            foreach(var pair in walkBonuses)
                if(!pair.Key.IsAddedToProject || pair.Key.ComponentHealth.Health<=0 || Terrain.ExtractContents(pair.Key.ComponentMiner.ActiveBlockValue)!=AbyssReaperBlock.Index) speedRetired.Add(pair.Key);
            foreach(var p in speedRetired)ApplyHeldSpeed(p,false);
            foreach(var p in players.ComponentPlayers)
                if(Terrain.ExtractContents(p.ComponentMiner.ActiveBlockValue)==AbyssReaperBlock.Index)ApplyHeldSpeed(p,true);
        }
        void Strike(ComponentPlayer p,State s) {
            if(CommonLib.WorkType==WorkType.Client)return;
            if(CommonLib.WorkType==WorkType.Server&&!p.PlayerData.IsMainPlayer)return; // No untrusted remote input path in V0.1.
            Vector3 origin=p.ComponentCreatureModel.EyePosition;
            Vector3 forward=p.ComponentCreatureModel.EyeRotation.GetForwardVector();
            Vector3 flat=new Vector3(forward.X,0,forward.Z); if(flat.LengthSquared()<.001f)return;flat=Vector3.Normalize(flat);
            hit.Clear();int visited=0,rays=0;
            foreach(var body in bodies.Bodies){if(++visited>512)break;
                if(body.Entity==p.Entity||!body.Entity.IsAddedToProject||hit.Contains(body.Entity))continue;
                var c=body.Entity.FindComponent<ComponentCreature>();if(c==null||c.ComponentHealth.Health<=0)continue;
                bool player=c is ComponentPlayer;
                bool hostile=c.Category==CreatureCategory.LandPredator||c.Category==CreatureCategory.WaterPredator;
                if(player?(!allowPlayers||!Project.FindSubsystem<SubsystemGameInfo>(true).WorldSettings.IsFriendlyFireEnabled):(!hostile&&!allowPassive))continue;
                Vector3 center=body.BoundingBox.Center();Vector3 delta=center-origin;
                Vector3 horizontal=new Vector3(delta.X,0,delta.Z);float len=horizontal.Length();
                if(!AbyssMeleeMotion.Sector(delta.Length(),len>.01f?Vector3.Dot(horizontal/len,flat):1,delta.Y,s.Combo.Stage))continue;
                Territoriy region;if(SubsystemBedrockBlockBehavior.CheckIsInTerritoriy(Terrain.ToCell(center.X),Terrain.ToCell(center.Z),out region))continue;
                if(++rays>48)break;
                if(!VoidCombatSafety.ClearPath(terrain,origin,center,AbyssMeleeMotion.ChargedReach))continue;
                hit.Add(body.Entity);
                ComponentMiner.AttackBody(body,p,center,forward,s.Combo.Stage==3?52:s.Combo.Stage==2?34:22,true);
                if(hit.Count>=24)break;
            }
        }
        /// <summary>Updates the touch HUD and skill cooldown feedback.</summary>
        public void TickGui(ComponentGui gui) {
            State s;var p=gui?.m_componentPlayer;if(p==null||!states.TryGetValue(p,out s))return;
            if(s.Hud==null)s.Hud=new WorldCutHud(gui);
            string hint=Horizon.Feedback(p)??WorldCut.Feedback(p);
            if(hint==null)hint=s.Combo.Charge(s.CombatTime)>=1?"蓄力完成 · 松开重斩":s.Combo.Charge(s.CombatTime)>0?"普攻蓄力中":"轻触连斩 · 长按蓄力";
            s.Hud.Update(WorldCut.Remaining(p),blink.Remaining(p),Horizon.Remaining(p),WorldCut.Busy(p)||Horizon.Busy(p)||blink.Busy(p),WorldCut.PendingRequest(p),hint,
                gui.ModalPanelWidget!=null||DialogsManager.HasDialogs(p.GuiWidget)||p.ComponentHealth.Health<=0||!p.ComponentInput.AllowHandleInput);
        }
        /// <inheritdoc/>
        public void Draw(Camera camera,int order) {
            if(order==VoidHorizonRules.DrawOrder){Horizon?.Draw(camera);return;}
            WorldCut?.Draw(camera);
            portalScene?.Draw(camera,Portals,clock.GameTime);
            blink?.Draw(camera);
            if(!showRifts)return;
            foreach(var pair in states)
            {
                var player=pair.Key;
                if(!player.Entity.IsAddedToProject||player.ComponentHealth.Health<=0||WorldCut.Busy(player)||Horizon.Busy(player)||blink.Busy(player))continue;
                meleeScene.Queue(player,pair.Value.Combo.Stage,pair.Value.Combo.Phase(pair.Value.CombatTime));
            }
            meleeScene.Flush(camera);
            var batch=renderer.FlatBatch(-1,DepthStencilState.DepthRead,RasterizerState.CullNone,BlendState.AlphaBlend);
            foreach(var pair in states){var p=pair.Key;var s=pair.Value;if(s.RiftUntil<=clock.GameTime)continue;
                Vector3 c=p.ComponentCreatureModel.EyePosition+p.ComponentCreatureModel.EyeRotation.GetForwardVector()*1.8f;
                for(int i=0;i<5;i++){Vector3 a=c+new Vector3((i%2==0?-.08f:.07f),.65f-i*.25f,0);Vector3 b=c+new Vector3((i%2==0?.07f:-.08f),.40f-i*.25f,0);batch.QueueLine(a,b,new Color(159,100,207));}}
            renderer.Flush(camera.ViewProjectionMatrix);
        }
        void Remove(ComponentPlayer p,State s){if(s.PortalDialog!=null)ClosePortalDialog(p,s.PortalDialog);s.Hud?.Dispose();s.Hud=null;s.InputGate.Reset();WorldCut?.Cancel(p);blink?.Cancel(p);Horizon?.Cancel(p);}
        /// <inheritdoc/>
        public override void Dispose(){foreach(var pair in walkBonuses)pair.Key.ComponentLocomotion.WalkSpeed-=pair.Value;walkBonuses.Clear();speedRetired.Clear();WorldCut?.Dispose();foreach(var x in states)Remove(x.Key,x.Value);states.Clear();WorldCut=null;PortalOperations=null;portalTravel?.Dispose();portalTravel=null;blink?.Dispose();blink=null;Horizon?.Dispose();Horizon=null;portalScene?.Dispose();meleeScene.Dispose();renderer.Clear();base.Dispose();}
    }
}

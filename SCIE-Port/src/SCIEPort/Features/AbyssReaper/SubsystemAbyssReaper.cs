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
        sealed class State { public AbyssCombo Combo=new AbyssCombo(); public Ray3 Ray; public Vector3 SwingDirection; public WorldCutHud Hud; public WorldCutInputGate InputGate=new WorldCutInputGate(); public bool NormalImpactPending,ChargeHeld; public double CombatTime,LastSample; }
        readonly Dictionary<ComponentPlayer,State> states=new Dictionary<ComponentPlayer,State>();
        readonly Dictionary<ComponentPlayer,float> walkBonuses=new Dictionary<ComponentPlayer,float>();
        readonly List<ComponentPlayer> speedRetired=new List<ComponentPlayer>();
        readonly List<ComponentPlayer> retired=new List<ComponentPlayer>();
        readonly HashSet<GameEntitySystem.Entity> hit=new HashSet<GameEntitySystem.Entity>();
        readonly PrimitivesRenderer3D renderer=new PrimitivesRenderer3D();
        readonly AbyssMeleeScene meleeScene=new AbyssMeleeScene();
        VoidPortalScene portalScene;
        internal VoidPortalNetwork Gateways { get; private set; }
        internal VoidPortalOperations PortalOperations { get; private set; }
        private VoidPortalLocalTravel portalTravel;
        private VoidBlinkRuntime blink;
        internal VoidSpaceRuntime Space => blink?.Space;
        internal DeathAuthorityRuntime Death { get; private set; }
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
        public int[] DrawOrders {get;}=new[]{110,310,VoidHorizonRules.DrawOrder};
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
            WorldCut=new WorldCutRuntime(Project,players,clock,terrain,bodies,allowPlayers,allowPassive,showRifts,v.GetValue<bool>("WorldCutLowEffects",false),WorldCutRules.Damage(v.GetValue<float>("WorldCutInitialDamage",100f),100),WorldCutRules.Damage(v.GetValue<float>("WorldCutCloseDamage",40f),40));
            Gateways=new VoidPortalNetwork(this,players,terrain,clock);
            Death=new DeathAuthorityRuntime(this,terrain,bodies,clock,players){Quality=VoidSkillRules.Quality(v.GetValue<int>("DeathEffectQuality",1))};Death.Load(v.GetValue<ValuesDictionary>("DeathAuthorityV1",null));
            if(portalTravel!=null){Gateways.Travel=portalTravel;portalTravel.Network=Gateways;}
        }
        /// <inheritdoc/>
        public override void Save(ValuesDictionary values)
        {
            base.Save(values);
            if(CommonLib.WorkType!=WorkType.Client)values.SetValue("VoidPortalRecords", portalLoadFailure ?? Portals.Serialize());
            if(CommonLib.WorkType!=WorkType.Client&&Death!=null)values.SetValue("DeathAuthorityV1",Death.Save());
        }
        /// <inheritdoc/>
        public override void OnEntityAdded(GameEntitySystem.Entity entity){base.OnEntityAdded(entity);Death?.EntityAdded(entity);}
        /// <summary>Consumes only world attack rays for this held item; native UI is processed first.</summary>
        public void Capture(ComponentInput input) {
            var p=input.m_componentPlayer; if(p==null)return;
            if(portalTravel?.InputLocked(p)==true||Space?.InputLocked(p)==true){VoidHorizonRuntime.LockInput(p);return;}
            State s;bool active=Terrain.ExtractContents(p.ComponentMiner.ActiveBlockValue)==AbyssReaperBlock.Index;
            if(!active){
                if(Gateways!=null&&Gateways.Actor(p)&&input.m_playerInput.Interact.HasValue&&p.ComponentGui.ModalPanelWidget==null&&!DialogsManager.HasDialogs(p.GuiWidget))
                {
                    Guid aimed=FindPortal(p,input.m_playerInput.Interact.Value.Direction);
                    if(aimed!=Guid.Empty&&OpenPortalDialog(p,aimed)){input.m_playerInput.Hit=null;input.m_playerInput.Interact=null;input.m_playerInput.Dig=null;input.m_playerInput.Aim=null;}
                }
                if(states.TryGetValue(p,out s)){Remove(p,s);states.Remove(p);}return;
            }
            if(!states.TryGetValue(p,out s)){s=new State { CombatTime=clock.GameTime,LastSample=clock.GameTime };states.Add(p,s);}
            float attackFrequency=1.1f*(Project.FindSubsystem<SCIEPort.Features.FourSaints.SubsystemFourSaints>(false)?.AttackFrequency(p)??1f);
            s.CombatTime+=Math.Max(0,clock.GameTime-s.LastSample)*attackFrequency;s.LastSample=clock.GameTime;
            bool blocked=!p.PlayerData.IsReadyForPlaying||clock.GameTimeDelta<=0||p.ComponentGui.ModalPanelWidget!=null||DialogsManager.HasDialogs(p.GuiWidget)||p.ComponentHealth.Health<=0||!input.AllowHandleInput||!Engine.Window.IsActive||p.ComponentSleep.SleepFactor>0||p.GameWidget==null||!p.GameWidget.ActiveCamera.IsEntityControlEnabled
                ||Project.FindSubsystem<SCIEPort.Features.FourSaints.SubsystemFourSaints>(false)?.IsActionLocked(p)==true;
            var sampled=input.m_playerInput;
            var ray=sampled.Dig??sampled.Hit??sampled.Aim;
            var interact=sampled.Interact;
            if(ray.HasValue)s.Ray=ray.Value;
            // Clear native melee/dig even on a blocked frame; never attack or mine via vanilla fallback.
            input.m_playerInput.Hit=null;input.m_playerInput.Dig=null;input.m_playerInput.Aim=null;
            input.m_playerInput.Interact=null;
            if(blocked){s.Combo=new AbyssCombo();s.NormalImpactPending=false;s.ChargeHeld=false;s.Hud?.CancelInput();s.InputGate.Reset();WorldCut.Cancel(p);blink.Cancel(p);Horizon.Cancel(p);return;}
            if(Horizon.Busy(p)){VoidHorizonRuntime.LockInput(p);s.Hud?.CancelInput();return;}
            // Native phone Tap emits Interact AND Hit. Aimed portal interaction wins over melee.
            if(WantsPortalInteraction(sampled,s.NormalImpactPending||s.Combo.Phase(s.CombatTime)<1,WorldCut.Busy(p)||blink.Busy(p)))
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
                if(s.InputGate.CanRequest(ray.HasValue,meleeBusy,WorldCut.Busy(p)||Horizon.Busy(p)||blink.Busy(p)||Death?.Busy(p)==true))
                { if(skillPressed==1) { if(Death.Request(p,DeathAuthorityRules.Toggle,Vector3.UnitZ))s.InputGate.Accepted(); }
                  else if(skillPressed==5) { if(Death.IsDeath(p)){DialogsManager.ShowDialog(p.GuiWidget,new DeathArmyDialog(p,Death));s.InputGate.Accepted();}else if(PortalAction(p))s.InputGate.Accepted(); }
                  else if(Death.IsDeath(p)&&skillPressed!=4){if(Death.Request(p,skillPressed==2?DeathAuthorityRules.Empower:DeathAuthorityRules.Wave,Vector3.UnitZ))s.InputGate.Accepted();}
                  else { try { string reason;bool accepted=skillPressed==2?blink.TryCast(p,out reason):skillPressed==3?Space.Request(p,VoidSpaceRules.Shatter,out reason):Horizon.TryCast(p,out reason);if(accepted)s.InputGate.Accepted();p.ComponentGui.DisplaySmallMessage(reason,Color.White,false,false); }
                    catch(Exception error){Log.Warning("[SCIEPort:Abyss] Skill request failed: "+error.GetType().Name);p.ComponentGui.DisplaySmallMessage("技能释放失败",Color.Yellow,false,false);} } }
                else WorldCut.Report(p,WorldCutFailure.Busy);
            }
            if(s.InputGate.RouteNormal(ray.HasValue,WorldCut.Busy(p)||Horizon.Busy(p)||blink.Busy(p)||Death?.Busy(p)==true))
            {
                UpdateMeleeInput(p,s,ray.HasValue,elapsed*attackFrequency);
            }
        }
        private void UpdateMeleeInput(ComponentPlayer p,State s,bool held,float elapsed)
        {
            s.Combo.Input(held,s.CombatTime,elapsed);
            bool empowered=Death?.Empowered(p)==true;
            if(empowered&&!held&&s.ChargeHeld&&s.Combo.Stage==3&&s.Combo.Phase(s.CombatTime)==0){s.Combo=new AbyssCombo();s.Combo.Input(true,s.CombatTime);s.Combo.Input(false,s.CombatTime);}
            if(!empowered&&held&&!s.NormalImpactPending&&s.Combo.Phase(s.CombatTime)>=1)WorldCut.BeginCharge(p);
            if(!held&&s.ChargeHeld)
            {
                bool heavy=!empowered&&s.Combo.Stage==3&&s.Combo.Phase(s.CombatTime)==0;
                WorldCut.EndCharge(p,heavy);
                if(heavy)
                {
                    // The authoritative cut owns this release; never also apply the old 52 melee hit.
                    s.Combo=new AbyssCombo();s.NormalImpactPending=false;s.ChargeHeld=false;s.InputGate.Accepted();return;
                }
            }
            s.ChargeHeld=held;
            if(s.Combo.Phase(s.CombatTime)==0){s.NormalImpactPending=true;s.SwingDirection=MeleeDirection(s.Ray.Direction,p.ComponentCreatureModel.EyeRotation.GetForwardVector());}
        }
        internal bool OtherSkillBusy(ComponentPlayer p) => WorldCut?.Busy(p)==true || blink?.Busy(p)==true||Death?.Busy(p)==true;
        internal bool PortalTransferLocked(ComponentPlayer p) => portalTravel?.InputLocked(p)==true;
        internal bool PortalBusy(ComponentPlayer p)
        {
            State state;return portalTravel?.InputLocked(p)==true||OtherSkillBusy(p)||Horizon?.Busy(p)==true||(states.TryGetValue(p,out state)&&(state.NormalImpactPending||state.Combo.Phase(state.CombatTime)<1||state.Combo.Charge(state.CombatTime)>0));
        }
        internal void AcceptPortalSnapshot(VoidPortalRegistry registry)
        {
            if(CommonLib.WorkType!=WorkType.Client)return;
            Portals=registry;
            if(portalTravel==null)
            {
                portalTravel=new VoidPortalLocalTravel(terrain,players,clock,registry);
                portalTravel.Network=Gateways;Gateways.Travel=portalTravel;
            }
        }
        private bool PortalAction(ComponentPlayer p)=>OpenPortalDialog(p,Guid.Empty);
        private Guid FindPortal(ComponentPlayer player,Vector3 direction)
        {
            if(Portals==null||!WorldCutDamage.Finite(direction)||direction.LengthSquared()<.1f)return Guid.Empty;
            direction=Vector3.Normalize(direction);Vector3 eye=player.ComponentCreatureModel.EyePosition;Guid found=Guid.Empty;float nearest=6f;
            foreach(var portal in Portals.Records)
            {
                // Hit the visible 1.35 x 1.85 oval in its own plane, including its lower edge.
                float normal=portal.Facing==0?direction.Z:direction.X;
                if(!portal.Active||Math.Abs(normal)<.001f)continue;
                float along=((portal.Facing==0?portal.Z:portal.X)-(portal.Facing==0?eye.Z:eye.X))/normal;
                if(along<0||along>nearest)continue;
                Vector3 at=eye+direction*along;
                float side=(portal.Facing==0?at.X-portal.X:at.Z-portal.Z)/1.35f,vertical=(at.Y-portal.Y-2)/1.85f;
                if(side*side+vertical*vertical>1)continue;
                if(terrain.Raycast(eye,at,false,true,(v,d)=>BlocksManager.Blocks[Terrain.ExtractContents(v)].IsCollidable).HasValue)continue;
                nearest=along;found=portal.Id;
            }
            return found;
        }
        private bool OpenPortalDialog(ComponentPlayer p,Guid id)=>Gateways?.Open(p,id)==true;
        internal void ClosePortalDialog(ComponentPlayer p,VoidPortalDialog dialog)=>Gateways?.Close(p,dialog);
        /// <inheritdoc/>
        public override bool OnUse(Ray3 ray,ComponentMiner miner)=>true;
        /// <inheritdoc/>
        public void Update(float dt) {
            UpdateHeldSpeed();
            Gateways?.Update();
            // Native UpdateInput Hook is before touch/gamepad assembly; consume the completed input
            // here at Input+1 (-9), strictly before ComponentPlayer Default (0).
            foreach(var player in players.ComponentPlayers)
                if(player.GameWidget!=null&&(CommonLib.WorkType==WorkType.Local||player.PlayerData.IsMainPlayer))Capture(player.ComponentInput);
            retired.Clear(); double now=clock.GameTime;
            foreach(var pair in states){var p=pair.Key;var s=pair.Value;
                if(!p.Entity.IsAddedToProject||p.ComponentHealth.Health<=0||Terrain.ExtractContents(p.ComponentMiner.ActiveBlockValue)!=AbyssReaperBlock.Index){retired.Add(p);continue;}
                if(WorldCut.Busy(p)||Horizon.Busy(p)||blink.Busy(p)||Death?.Busy(p)==true)continue;
                float t=s.Combo.Phase(s.CombatTime);
                var pose=AbyssMeleeMotion.Sample(s.Combo.Stage,t,s.Combo.Charge(s.CombatTime));
                Vector3 rotation=new Vector3(pose.Pitch,pose.Yaw,pose.Roll),offset=new Vector3(pose.X,pose.Y,pose.Z);
                var fp=p.Entity.FindComponent<ComponentFirstPersonModel>();
                if(fp!=null){fp.ItemRotationOrder=rotation;fp.ItemOffsetOrder=offset;}
                p.ComponentCreatureModel.InHandItemRotationOrder=rotation;
                p.ComponentCreatureModel.InHandItemOffsetOrder=offset*.35f;
                if(s.Combo.Impact(s.CombatTime)){s.NormalImpactPending=false;Strike(p,s);}
            }
            foreach(var p in retired){Remove(p,states[p]);states.Remove(p);}
            WorldCut.Update(dt);
            try { portalTravel?.Update(); } catch(Exception error) { Log.Warning("[SCIEPort:Abyss] Portal travel cancelled: "+error.GetType().Name); }
            blink?.Update();
            Horizon?.Update();
            Death?.Update();
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
            if(s.Combo.Stage==3)return; // Charged releases exclusively use WorldCutRuntime.
            if(Death?.IsDeath(p)==true){Death.Request(p,DeathAuthorityRules.Strike,MeleeDirection(s.SwingDirection,p.ComponentCreatureModel.EyeRotation.GetForwardVector()));return;}
            if(CommonLib.WorkType==WorkType.Client)return;
            if(CommonLib.WorkType==WorkType.Server&&!p.PlayerData.IsMainPlayer)return; // No untrusted remote input path in V0.1.
            Vector3 origin=p.ComponentCreatureModel.EyePosition;
            Vector3 forward=MeleeDirection(s.SwingDirection,p.ComponentCreatureModel.EyeRotation.GetForwardVector());
            float reach=s.Combo.Stage==3?AbyssMeleeMotion.ChargedReach:AbyssMeleeMotion.NormalReach;
            var aimed=bodies.Raycast(origin,origin+forward*reach,.15f,(body,distance)=>body.Entity!=p.Entity);
            var primary=aimed.HasValue?aimed.Value.ComponentBody:null;
            Vector3 flat=new Vector3(forward.X,0,forward.Z);flat=flat.LengthSquared()<.001f?Vector3.UnitZ:Vector3.Normalize(flat);
            hit.Clear();int visited=0,rays=0;
            foreach(var body in bodies.Bodies){if(++visited>512)break;
                if(body.Entity==p.Entity||!body.Entity.IsAddedToProject||hit.Contains(body.Entity))continue;
                if(Death?.IsFriendly(body.Entity.FindComponent<ComponentCreature>(false))==true)continue;
                var c=body.Entity.FindComponent<ComponentCreature>();if(c==null||c.ComponentHealth.Health<=0)continue;
                bool player=c is ComponentPlayer;
                bool hostile=c.Category==CreatureCategory.LandPredator||c.Category==CreatureCategory.WaterPredator;
                // A deliberately aimed animal can be hit like any normal weapon. Passive sweep
                // collateral remains opt-in; the default still excludes every other player.
                if(!MeleeTargetAllowed(player,hostile,body==primary,allowPassive,allowPlayers,player&&Project.FindSubsystem<SubsystemGameInfo>(true).WorldSettings.IsFriendlyFireEnabled))continue;
                Vector3 center=body.BoundingBox.Center();Vector3 delta=center-origin;
                Vector3 horizontal=new Vector3(delta.X,0,delta.Z);float len=horizontal.Length();
                if(!AbyssMeleeMotion.Sector(delta.Length(),body==primary||len<=.01f?1:Vector3.Dot(horizontal/len,flat),delta.Y,s.Combo.Stage))continue;
                Territoriy region;if(SubsystemBedrockBlockBehavior.CheckIsInTerritoriy(Terrain.ToCell(center.X),Terrain.ToCell(center.Z),out region))continue;
                if(++rays>48)break;
                if(!VoidCombatSafety.ClearPath(terrain,origin,center,AbyssMeleeMotion.ChargedReach))continue;
                hit.Add(body.Entity);
                ComponentMiner.AttackBody(body,p,center,forward,s.Combo.Stage==3?52:s.Combo.Stage==2?34:22,true);
                if(hit.Count>=24)break;
            }
        }
        internal static Vector3 MeleeDirection(Vector3 aimed,Vector3 fallback)
        {
            if(!WorldCutDamage.Finite(aimed)||aimed.LengthSquared()<.001f)aimed=fallback;
            return WorldCutDamage.Finite(aimed)&&aimed.LengthSquared()>=.001f?Vector3.Normalize(aimed):Vector3.UnitZ;
        }
        internal static bool MeleeTargetAllowed(bool player,bool hostile,bool primary,bool passive,bool players,bool friendlyFire)
            => player?players&&friendlyFire:hostile||primary||passive;
        internal static bool WantsPortalInteraction(PlayerInput input,bool meleeBusy,bool skillBusy)
            => input.Interact.HasValue&&!meleeBusy&&!skillBusy;
        /// <summary>Updates the touch HUD and skill cooldown feedback.</summary>
        public void TickGui(ComponentGui gui) {
            State s;var p=gui?.m_componentPlayer;if(p==null||!states.TryGetValue(p,out s))return;
            if(s.Hud==null)s.Hud=new WorldCutHud(gui);
            string hint=Horizon.Feedback(p)??WorldCut.Feedback(p);
            if(hint==null)hint=s.Combo.Charge(s.CombatTime)>=1?"蓄力完成 · 松开重斩":s.Combo.Charge(s.CombatTime)>0?"普攻蓄力中":"轻触连斩 · 长按蓄力";
            s.Hud.Update(WorldCut.Remaining(p),blink.Remaining(p),Space.Remaining(p,VoidSpaceRules.Shatter),Horizon.Remaining(p),WorldCut.Busy(p)||Horizon.Busy(p)||blink.Busy(p)||Death.Busy(p),WorldCut.PendingRequest(p),Death.Feedback(p)??Space.Feedback(p)??hint,
                gui.ModalPanelWidget!=null||DialogsManager.HasDialogs(p.GuiWidget)||p.ComponentHealth.Health<=0||!p.ComponentInput.AllowHandleInput,Death,p);
        }
        /// <inheritdoc/>
        public void Draw(Camera camera,int order) {
            if(order==VoidHorizonRules.DrawOrder){Horizon?.Draw(camera);return;}
            if(order==310){blink?.Draw(camera);Death?.Draw(camera);return;}
            WorldCut?.Draw(camera);
            portalScene?.Draw(camera,Portals,clock.GameTime);
            if(!showRifts)return;
            foreach(var pair in states)
            {
                var player=pair.Key;
                if(!player.Entity.IsAddedToProject||player.ComponentHealth.Health<=0||WorldCut.Busy(player)||Horizon.Busy(player)||blink.Busy(player))continue;
                meleeScene.Queue(player,pair.Value.Combo.Stage,pair.Value.Combo.Phase(pair.Value.CombatTime));
            }
            meleeScene.Flush(camera);
        }
        void Remove(ComponentPlayer p,State s){s.Hud?.Dispose();s.Hud=null;s.InputGate.Reset();WorldCut?.Cancel(p);blink?.Cancel(p);Horizon?.Cancel(p);}
        /// <inheritdoc/>
        public override void Dispose(){foreach(var pair in walkBonuses)pair.Key.ComponentLocomotion.WalkSpeed-=pair.Value;walkBonuses.Clear();speedRetired.Clear();WorldCut?.Dispose();foreach(var x in states)Remove(x.Key,x.Value);states.Clear();WorldCut=null;PortalOperations=null;portalTravel?.Dispose();portalTravel=null;Gateways?.Dispose();Gateways=null;blink?.Dispose();blink=null;Horizon?.Dispose();Horizon=null;Death?.Dispose();Death=null;portalScene?.Dispose();meleeScene.Dispose();renderer.Clear();base.Dispose();}
    }
}

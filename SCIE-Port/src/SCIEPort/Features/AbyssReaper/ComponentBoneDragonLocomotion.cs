using System;
using Engine;
using Game;
using Game.NetWork;
using GameEntitySystem;
using TemplatesDatabase;
namespace SCIEPort.Features.AbyssReaper
{
    /// <summary>Native FlyOrder movement, on the native body physics owner only.</summary>
    public sealed class ComponentBoneDragonLocomotion:ComponentLocomotion
    {
        private ComponentCreature creature;private ComponentMount mount;private ComponentPathfinding path;
        private SubsystemTerrain terrain;private SubsystemTime clock;private double liftUntil,verticalUntil;
        private float vertical;internal bool FlightEnabled,AutoDriving;
        internal bool VerticalInput=>clock.GameTime<verticalUntil&&Math.Abs(vertical)>.01f;
        internal void SetFlight(bool enabled)
        {if(enabled&&!FlightEnabled)liftUntil=clock.GameTime+.65;FlightEnabled=enabled;}
        internal void SetVertical(float value){vertical=MathUtils.Clamp(value,-1,1);verticalUntil=clock.GameTime+.18;}
        /// <inheritdoc/>
        public override void Load(ValuesDictionary values,IdToEntityMap map)
        {
            base.Load(values,map);creature=Entity.FindComponent<ComponentCreature>(true);mount=Entity.FindComponent<ComponentMount>(true);path=Entity.FindComponent<ComponentPathfinding>(true);
            terrain=Project.FindSubsystem<SubsystemTerrain>(true);clock=Project.FindSubsystem<SubsystemTime>(true);
        }
        /// <inheritdoc/>
        public override void Update(float dt)
        {
            var rider=mount.Rider?.ComponentCreature as ComponentPlayer;
            bool owns=CommonLib.WorkType==WorkType.Local||CommonLib.WorkType==WorkType.Server&&(rider==null||rider.PlayerData.IsMainPlayer)||CommonLib.WorkType==WorkType.Client&&rider?.PlayerData.IsMainPlayer==true;
            if(owns&&rider!=null&&rider.PlayerData.IsMainPlayer&&creature.ComponentHealth.Health>0)
            {
                var body=creature.ComponentBody;var input=rider.ComponentInput.m_playerInput;
                bool modal=rider.ComponentGui.ModalPanelWidget!=null||DialogsManager.HasDialogs(rider.GuiWidget);
                bool ground=body.StandingOnValue.HasValue||body.StandingOnBody!=null;
                bool authorized=Project.FindSubsystem<SubsystemAbyssReaper>(false)?.Death?.RiddenFriend(rider)?.Dragon==true;
                if(authorized&&(FlightEnabled||!ground&&body.ImmersionFactor<.5f))
                {
                    Vector3 order=Vector3.Zero;
                    if(AutoDriving&&FlightEnabled&&!modal)order=FlyOrder??Vector3.Zero;
                    else if(!modal)
                    {
                        order=body.Matrix.Forward*input.Move.Z;
                        TurnOrder=new Vector2(input.Move.X*.9f,0);
                    }
                    float climb=modal?0:clock.GameTime<verticalUntil?vertical:0;
                    if(input.Jump&&!modal)climb=1;
                    if(FlightEnabled)
                    {
                        if(!AutoDriving||modal||Math.Abs(climb)>.01f)order.Y=climb*.45f;
                        if(clock.GameTime<liftUntil)order.Y=Math.Max(order.Y,.45f);
                    }
                    else order.Y=-.18f; // Controlled landing, never switch off gravity at altitude.
                    if(body.Position.Y>246)order.Y=Math.Min(order.Y,0);
                    if(body.Position.Y<1)order.Y=Math.Max(order.Y,.25f);
                    var ahead=body.Position+new Vector3(order.X,0,order.Z)*3;
                    if(!VoidPortalSafety.Ready(terrain,Terrain.ToCell(ahead.X),Terrain.ToCell(ahead.Z)))order.X=order.Z=0;
                    WalkOrder=null;JumpOrder=0;FlyOrder=order;
                }
                else if(authorized&&!FlightEnabled)
                {
                    // Native pilot chooses flight for any long destination. Keep ground
                    // auto pursuit on foot until the rider explicitly requests takeoff.
                    if(AutoDriving&&FlyOrder.HasValue)WalkOrder=path.Destination.HasValue?new Vector2(0,1):Vector2.Zero;
                    FlyOrder=null;
                }
            }
            base.Update(dt);
        }
    }
}

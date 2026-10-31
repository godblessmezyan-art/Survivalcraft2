using System;
using Engine;
using Game;
namespace SCIEPort.Features.AbyssReaper
{
    // A text setting, not another weapon skill/icon. Uses the shared obstacle-aware anchor.
    internal sealed class DeathRidingHud:IDisposable
    {
        private readonly ComponentGui gui;private readonly CanvasWidget root=new CanvasWidget{IsHitTestVisible=false},slot=new CanvasWidget{Size=new Vector2(96),IsHitTestVisible=false};
        private readonly BevelledButtonWidget button=new BevelledButtonWidget{Size=new Vector2(96,68),FontScale=.43f};
        internal DeathRidingHud(ComponentGui gui){this.gui=gui;root.Children.Add(slot);slot.Children.Add(button);CanvasWidget.SetPosition(button,new Vector2(0,14));gui.ControlsContainerWidget.Children.Add(root);}
        internal void Update(DeathAuthorityRuntime runtime,ComponentPlayer player,DeathAuthorityRuntime.Friend row,bool pending)
        {
            root.IsVisible=row!=null&&player.ComponentHealth.Health>0&&gui.ModalPanelWidget==null&&!DialogsManager.HasDialogs(player.GuiWidget)&&player.GameWidget.ActiveCamera.IsEntityControlEnabled;
            if(!root.IsVisible)return;
            if(!Artifacts.ArtifactSkillHud.ArrangeWithStatus(gui,root,(LabelWidget)null,slot))return;
            button.Text="自动追击\n"+(pending?"同步中":row.RidingAuto?"开启":"关闭");button.IsEnabled=!pending;
            if(button.IsClicked)runtime.Request(player,DeathAuthorityRules.RidingMode,Vector3.UnitZ,row.Id);
        }
        internal bool Captures(WidgetInput input)
        {
            if(!root.IsVisibleGlobal||input==null)return false;
            foreach(var touch in input.TouchLocations)if(Inside(touch.Position))return true;
            return input.Click.HasValue&&(Inside(input.Click.Value.Start)||Inside(input.Click.Value.End));
        }
        private bool Inside(Vector2 screen){Vector2 at=button.ScreenToWidget(screen);return at.X>=0&&at.Y>=0&&at.X<button.ActualSize.X&&at.Y<button.ActualSize.Y;}
        public void Dispose(){if(root.ParentWidget!=null)root.ParentWidget.Children.Remove(root);}
    }
}

using System;
using Engine;
using Game;
namespace SCIEPort.Features.AbyssReaper
{
    // A text setting, not another weapon skill/icon. Uses the shared obstacle-aware anchor.
    internal sealed class DeathRidingHud:IDisposable
    {
        private readonly ComponentGui gui;private readonly CanvasWidget root=new CanvasWidget{IsHitTestVisible=false};
        private readonly CanvasWidget[] slots=new CanvasWidget[4],groundSlots=new CanvasWidget[1];
        private readonly BevelledButtonWidget[] buttons=new BevelledButtonWidget[4];
        internal DeathRidingHud(ComponentGui gui)
        {
            this.gui=gui;
            for(int i=0;i<4;i++){slots[i]=new CanvasWidget{Size=new Vector2(96),IsHitTestVisible=false};buttons[i]=new BevelledButtonWidget{Size=new Vector2(96,68),FontScale=.43f};root.Children.Add(slots[i]);slots[i].Children.Add(buttons[i]);CanvasWidget.SetPosition(buttons[i],new Vector2(0,14));}
            groundSlots[0]=slots[0];gui.ControlsContainerWidget.Children.Add(root);
        }
        internal void Update(DeathAuthorityRuntime runtime,ComponentPlayer player,DeathAuthorityRuntime.Friend row,bool pending)
        {
            root.IsVisible=row!=null&&player.ComponentHealth.Health>0&&gui.ModalPanelWidget==null&&!DialogsManager.HasDialogs(player.GuiWidget)&&player.GameWidget.ActiveCamera.IsEntityControlEnabled;
            if(!root.IsVisible)return;
            for(int i=0;i<4;i++){slots[i].IsVisible=i==0||row.Dragon;slots[i].Size=new Vector2(row.Dragon?72:96);buttons[i].Size=new Vector2(row.Dragon?72:96,row.Dragon?56:68);CanvasWidget.SetPosition(buttons[i],new Vector2(0,row.Dragon?8:14));}
            // Arrange() has no status widget. The old null-label overload dereferenced Size.
            if(!Artifacts.ArtifactSkillHud.Arrange(gui,root,row.Dragon?slots:groundSlots))return;
            buttons[0].Text="自动追击\n"+(pending?"同步中":row.RidingAuto?"开启":"关闭");buttons[0].IsEnabled=!pending;
            if(buttons[0].IsClicked)runtime.Request(player,DeathAuthorityRules.RidingMode,Vector3.UnitZ,row.Id);
            if(!row.Dragon)return;
            buttons[1].Text=pending?"同步中":row.DragonFlight?"降落":"起飞";buttons[1].IsEnabled=!pending;
            buttons[2].Text="上升\n按住";buttons[3].Text="下降\n按住";
            buttons[2].IsEnabled=buttons[3].IsEnabled=row.DragonFlight&&!pending;
            if(buttons[1].IsClicked)runtime.Request(player,DeathAuthorityRules.DragonFlight,Vector3.UnitZ,row.Id);
            runtime.DragonVertical(player,row.DragonFlight?(Held(buttons[2])?1:0)-(Held(buttons[3])?1:0):0);
        }
        private static bool Held(BevelledButtonWidget button)
        {foreach(var child in button.AllChildren){var hit=child as ClickableWidget;if(hit!=null&&hit.IsPressed)return true;}return false;}
        internal bool Captures(WidgetInput input)
        {
            if(!root.IsVisibleGlobal||input==null)return false;
            foreach(var touch in input.TouchLocations)if(Inside(touch.Position))return true;
            return input.Click.HasValue&&(Inside(input.Click.Value.Start)||Inside(input.Click.Value.End));
        }
        private bool Inside(Vector2 screen)
        {
            foreach(var button in buttons)if(button.IsVisibleGlobal){Vector2 at=button.ScreenToWidget(screen);if(at.X>=0&&at.Y>=0&&at.X<button.ActualSize.X&&at.Y<button.ActualSize.Y)return true;}return false;
        }
        public void Dispose(){if(root.ParentWidget!=null)root.ParentWidget.Children.Remove(root);}
    }
}

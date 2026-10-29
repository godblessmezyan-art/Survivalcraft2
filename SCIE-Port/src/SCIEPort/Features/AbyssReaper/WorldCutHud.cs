using System;
using System.Collections.Generic;
using Engine;
using Engine.Input;
using Game;
namespace SCIEPort.Features.AbyssReaper
{
    // Only slot two is a hit target. The panel/background/locked slots cannot consume world touches.
    internal sealed class WorldCutHud : IDisposable
    {
        private readonly ComponentGui gui;
        private readonly CanvasWidget panel;
        private readonly ClickableWidget cut;
        private readonly LabelWidget cutText,status;
        private readonly Widget[] nativeControls;
        private readonly List<WorldCutHudLayout.Rect> obstacles=new List<WorldCutHudLayout.Rect>(16);
        private int capturedTouch=-1;
        internal bool Visible => panel.IsVisible;
        internal WorldCutHud(ComponentGui owner)
        {
            gui=owner;panel=new CanvasWidget { Size=new Vector2(WorldCutHudLayout.Width,WorldCutHudLayout.Height),HorizontalAlignment=WidgetAlignment.Near,VerticalAlignment=WidgetAlignment.Near,IsHitTestVisible=false };
            Add(new RectangleWidget { Size=panel.Size,FillColor=new Color(10,6,17,230),OutlineColor=new Color(92,55,129),OutlineThickness=1,IsHitTestVisible=false },0,0);
            string[] names={"裂界星门","断界斩","虚影瞬狩","终焉·万象坍缩"};
            for(int i=0;i<4;i++)
            {
                float x=8+i*66;
                Add(new RectangleWidget { Size=new Vector2(64,62),FillColor=new Color(23,12,34,235),OutlineColor=i==1?new Color(174,113,224):new Color(66,53,78),OutlineThickness=1,IsHitTestVisible=false },x,8);
                var label=new LabelWidget { Size=new Vector2(64,62),Text=names[i]+(i==1?"\n就绪":"\n锁定"),FontScale=.48f,HorizontalAlignment=WidgetAlignment.Center,VerticalAlignment=WidgetAlignment.Center,IsHitTestVisible=false };
                Add(label,x,8);if(i==1)cutText=label;
            }
            var clickArea=new CanvasWidget { Size=new Vector2(64,62),IsHitTestVisible=false };
            cut=new ClickableWidget { HorizontalAlignment=WidgetAlignment.Stretch,VerticalAlignment=WidgetAlignment.Stretch,IsHitTestVisible=true };
            clickArea.Children.Add(cut);Add(clickArea,74,8);
            status=new LabelWidget { Size=new Vector2(264,32),FontScale=.48f,Color=new Color(194,168,219),IsHitTestVisible=false };Add(status,8,72);
            gui.ControlsContainerWidget.Children.Add(panel);
            nativeControls=new Widget[] { gui.MoveWidget,gui.LookWidget,gui.ShortInventoryWidget,gui.HealthBarWidget,gui.LevelLabelWidget,
                gui.m_backButtonWidget,gui.m_inventoryButtonWidget,gui.m_clothingButtonWidget,gui.m_moreButtonWidget };
        }
        private void Add(Widget widget,float x,float y) { panel.Children.Add(widget);panel.SetWidgetPosition(widget,new Vector2(x,y)); }
        internal bool Poll(WidgetInput input)
        {
            if(!panel.IsVisibleGlobal || !cut.IsEnabledGlobal || input==null){CancelInput();return false;}
            bool seen=false;
            foreach(var touch in input.TouchLocations)
            {
                if(capturedTouch<0 && touch.State==TouchLocationState.Pressed && Inside(touch.Position))capturedTouch=touch.Id;
                if(touch.Id!=capturedTouch)continue;seen=true;
                if(touch.State==TouchLocationState.Released){capturedTouch=-1;return Inside(touch.Position);}
            }
            if(capturedTouch>=0 && !seen)capturedTouch=-1;
            // Mouse/keyboard accessibility fallback; never clear the shared WidgetInput hierarchy.
            if(!input.Click.HasValue)return false;
            var click=input.Click.Value;return Inside(click.Start) && Inside(click.End);
        }
        internal void CancelInput() { capturedTouch=-1; }
        private bool Inside(Vector2 screen)
        {
            Vector2 p=cut.ScreenToWidget(screen);return p.X>=0 && p.Y>=0 && p.X<cut.ActualSize.X && p.Y<cut.ActualSize.Y;
        }
        internal void Update(double remaining,bool busy,bool pending,string feedback,bool blocked)
        {
            obstacles.Clear();
            foreach(var widget in nativeControls)
            {
                if(widget==null || !widget.IsVisibleGlobal || widget.ActualSize.X<=0 || widget.ActualSize.Y<=0)continue;
                Vector2 a=gui.ControlsContainerWidget.ScreenToWidget(widget.WidgetToScreen(Vector2.Zero));
                Vector2 b=gui.ControlsContainerWidget.ScreenToWidget(widget.WidgetToScreen(widget.ActualSize));
                obstacles.Add(new WorldCutHudLayout.Rect(Math.Min(a.X,b.X),Math.Min(a.Y,b.Y),Math.Abs(a.X-b.X),Math.Abs(a.Y-b.Y)));
            }
            WorldCutHudLayout.Rect place=default(WorldCutHudLayout.Rect);
            panel.IsVisible=!blocked && WorldCutHudLayout.Place(gui.ControlsContainerWidget.ActualSize.X,gui.ControlsContainerWidget.ActualSize.Y,obstacles,out place);
            if(panel.IsVisible)panel.Margin=new Vector2(place.X,place.Y);
            cutText.Text="断界斩\n"+(pending?"等待主机":busy?"释放中":remaining>0?remaining.ToString("0.0")+"s":"就绪 · 10s");
            cutText.Color=remaining>0?new Color(128,109,149):new Color(220,184,255);
            status.Text=feedback ?? "轻触连斩 · 长按蓄力";
        }
        public void Dispose() { CancelInput();if(panel.ParentWidget!=null)panel.ParentWidget.Children.Remove(panel);obstacles.Clear(); }
    }
}

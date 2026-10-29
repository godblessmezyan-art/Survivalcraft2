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
        private readonly ClickableWidget cut,portal,blink,horizon;
        private readonly LabelWidget cutText,blinkText,horizonText,status;
        private readonly Widget[] nativeControls;
        private readonly List<WorldCutHudLayout.Rect> obstacles=new List<WorldCutHudLayout.Rect>(16);
        private int capturedTouch=-1,capturedSlot;
        internal bool Visible => panel.IsVisible;
        internal WorldCutHud(ComponentGui owner)
        {
            gui=owner;panel=new CanvasWidget { Size=new Vector2(WorldCutHudLayout.Width,WorldCutHudLayout.Height),HorizontalAlignment=WidgetAlignment.Near,VerticalAlignment=WidgetAlignment.Near,IsHitTestVisible=false };
            Add(new RectangleWidget { Size=panel.Size,FillColor=new Color(10,6,17,230),OutlineColor=new Color(92,55,129),OutlineThickness=1,IsHitTestVisible=false },0,0);
            string[] names={"裂界星门","断界斩","虚影瞬狩","终焉·万象坍缩"};
            for(int i=0;i<4;i++)
            {
                float x=8+i*66;
                Add(new RectangleWidget { Size=new Vector2(64,62),FillColor=new Color(23,12,34,235),OutlineColor=i==3?new Color(210,158,76):new Color(174,113,224),OutlineThickness=1,IsHitTestVisible=false },x,8);
                var label=new LabelWidget { Size=new Vector2(64,62),Text=names[i]+(i==0?"\n管理":"\n就绪"),FontScale=.48f,HorizontalAlignment=WidgetAlignment.Center,VerticalAlignment=WidgetAlignment.Center,IsHitTestVisible=false };
                Add(label,x,8);if(i==1)cutText=label;else if(i==2)blinkText=label;else if(i==3)horizonText=label;
            }
            var portalArea=new CanvasWidget { Size=new Vector2(64,62),IsHitTestVisible=false };
            portal=new ClickableWidget { HorizontalAlignment=WidgetAlignment.Stretch,VerticalAlignment=WidgetAlignment.Stretch,IsHitTestVisible=true };
            portalArea.Children.Add(portal);Add(portalArea,8,8);
            var blinkArea=new CanvasWidget { Size=new Vector2(64,62),IsHitTestVisible=false };
            blink=new ClickableWidget { HorizontalAlignment=WidgetAlignment.Stretch,VerticalAlignment=WidgetAlignment.Stretch,IsHitTestVisible=true };
            blinkArea.Children.Add(blink);Add(blinkArea,140,8);
            var horizonArea=new CanvasWidget { Size=new Vector2(64,62),IsHitTestVisible=false };
            horizon=new ClickableWidget { HorizontalAlignment=WidgetAlignment.Stretch,VerticalAlignment=WidgetAlignment.Stretch,IsHitTestVisible=true };
            horizonArea.Children.Add(horizon);Add(horizonArea,206,8);
            var clickArea=new CanvasWidget { Size=new Vector2(64,62),IsHitTestVisible=false };
            cut=new ClickableWidget { HorizontalAlignment=WidgetAlignment.Stretch,VerticalAlignment=WidgetAlignment.Stretch,IsHitTestVisible=true };
            clickArea.Children.Add(cut);Add(clickArea,74,8);
            status=new LabelWidget { Size=new Vector2(264,32),FontScale=.48f,Color=new Color(194,168,219),IsHitTestVisible=false };Add(status,8,72);
            gui.ControlsContainerWidget.Children.Add(panel);
            nativeControls=new Widget[] { gui.MoveWidget,gui.LookWidget,gui.ShortInventoryWidget,gui.HealthBarWidget,gui.LevelLabelWidget,
                gui.m_backButtonWidget,gui.m_inventoryButtonWidget,gui.m_clothingButtonWidget,gui.m_moreButtonWidget };
        }
        private void Add(Widget widget,float x,float y) { panel.Children.Add(widget);panel.SetWidgetPosition(widget,new Vector2(x,y)); }
        internal int Poll(WidgetInput input)
        {
            if(!panel.IsVisibleGlobal || input==null){CancelInput();return 0;}
            bool seen=false;
            foreach(var touch in input.TouchLocations)
            {
                if(capturedTouch<0 && touch.State==TouchLocationState.Pressed)
                {
                    int slot=At(touch.Position);if(slot!=0){capturedTouch=touch.Id;capturedSlot=slot;}
                }
                if(touch.Id!=capturedTouch)continue;seen=true;
                if(touch.State==TouchLocationState.Released){int slot=capturedSlot;capturedTouch=-1;capturedSlot=0;return At(touch.Position)==slot?slot:0;}
            }
            if(capturedTouch>=0 && !seen)CancelInput();
            if(!input.Click.HasValue)return 0;
            var click=input.Click.Value;int picked=At(click.Start);return picked!=0 && At(click.End)==picked?picked:0;
        }
        internal void CancelInput() { capturedTouch=-1;capturedSlot=0; }
        private int At(Vector2 screen)
        {
            if(Inside(portal,screen))return 1;
            if(Inside(cut,screen))return 2;
            if(Inside(blink,screen))return 3;
            if(Inside(horizon,screen))return 4;
            return 0;
        }
        private static bool Inside(Widget widget,Vector2 screen)
        {
            if(!widget.IsEnabledGlobal)return false;
            Vector2 p=widget.ScreenToWidget(screen);return p.X>=0 && p.Y>=0 && p.X<widget.ActualSize.X && p.Y<widget.ActualSize.Y;
        }
        internal void Update(double remaining,double blinkRemaining,double horizonRemaining,bool busy,bool pending,string feedback,bool blocked)
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
            horizonText.Text="终焉坍缩\n"+(horizonRemaining>0?horizonRemaining.ToString("0")+"s":"就绪 · 180s");
            horizonText.Color=horizonRemaining>0?new Color(128,109,149):new Color(235,202,145);
            blinkText.Text="虚影瞬狩\n"+(blinkRemaining>0?blinkRemaining.ToString("0.0")+"s":"就绪 · 15s");
            blinkText.Color=blinkRemaining>0?new Color(128,109,149):new Color(220,184,255);
            cutText.Color=remaining>0?new Color(128,109,149):new Color(220,184,255);
            status.Text=feedback ?? "轻触连斩 · 长按蓄力";
        }
        public void Dispose() { CancelInput();if(panel.ParentWidget!=null)panel.ParentWidget.Children.Remove(panel);obstacles.Clear(); }
    }
}

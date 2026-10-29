using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Engine.Input;
using Game;
namespace SCIEPort.Features.AbyssReaper
{
    // Leave transparent sprite corners available to world input.
    internal sealed class AbyssSkillHitTarget : ClickableWidget
    {
        public override bool HitTest(Vector2 point)
        {
            Vector2 p=ScreenToWidget(point);
            return WorldCutHudLayout.Circle(p.X,p.Y,ActualSize.X,ActualSize.Y);
        }
    }
    internal sealed class WorldCutHud : IDisposable
    {
        private sealed class Button
        {
            internal CanvasWidget Root;
            internal RectangleWidget Icon,Glow;
            internal LabelWidget Cooldown;
            internal AbyssSkillHitTarget Hit;
        }
        private readonly ComponentGui gui;
        private readonly CanvasWidget panel;
        private readonly Button[] buttons=new Button[4];
        private readonly LabelWidget status;
        private readonly List<WorldCutHudLayout.Rect> obstacles=new List<WorldCutHudLayout.Rect>(32);
        private int capturedTouch=-1,capturedSlot;
        internal bool Visible => panel.IsVisible;
        internal WorldCutHud(ComponentGui owner)
        {
            gui=owner;
            panel=new CanvasWidget { Name="SCIEPort.Abyss.LookSkills",HorizontalAlignment=WidgetAlignment.Near,VerticalAlignment=WidgetAlignment.Near,IsHitTestVisible=false,IsVisible=false };
            Texture2D atlas=ContentManager.Get<Texture2D>("Textures/AbyssSkillsV1");
            string[] names={"星门","断界斩","瞬狩","终焉坍缩"};
            for(int i=0;i<4;i++)
            {
                float size=i==3?WorldCutHudLayout.Ultimate:WorldCutHudLayout.Slot;
                var button=new Button();buttons[i]=button;
                button.Root=new CanvasWidget { Size=new Vector2(size),IsHitTestVisible=false };
                Vector2 uv=new Vector2((i%2)*.5f,(i/2)*.5f);
                button.Icon=new RectangleWidget { Size=new Vector2(size),Subtexture=new Subtexture(atlas,uv,uv+new Vector2(.5f)),FillColor=Color.White,OutlineThickness=0,OutlineColor=Color.Transparent,IsHitTestVisible=false };
                button.Glow=new RectangleWidget { Size=new Vector2(size),Subtexture=button.Icon.Subtexture,FillColor=Color.Transparent,OutlineThickness=0,OutlineColor=Color.Transparent,IsHitTestVisible=false };
                button.Cooldown=new LabelWidget { Size=new Vector2(size),TextAnchor=TextAnchor.HorizontalCenter|TextAnchor.VerticalCenter,FontScale=i==3?.92f:.8f,DropShadow=true,Color=Color.White,IsHitTestVisible=false };
                var label=new LabelWidget { Size=new Vector2(size,16),Text=names[i],TextAnchor=TextAnchor.HorizontalCenter|TextAnchor.VerticalCenter,FontScale=.38f,DropShadow=true,Color=new Color(237,218,255),IsHitTestVisible=false };
                button.Hit=new AbyssSkillHitTarget { HorizontalAlignment=WidgetAlignment.Stretch,VerticalAlignment=WidgetAlignment.Stretch,IsHitTestVisible=true };
                button.Root.Children.Add(button.Icon);button.Root.Children.Add(button.Glow);button.Root.Children.Add(button.Cooldown);button.Root.Children.Add(label);button.Root.Children.Add(button.Hit);
                CanvasWidget.SetPosition(label,new Vector2(0,size*.71f));
                panel.Children.Add(button.Root);
            }
            status=new LabelWidget { Size=new Vector2(232,24),FontScale=.42f,DropShadow=true,Color=new Color(221,190,244),IsHitTestVisible=false };
            panel.Children.Add(status);gui.ControlsContainerWidget.Children.Add(panel);
        }
        internal int Poll(WidgetInput input)
        {
            if(!panel.IsVisibleGlobal||input==null){CancelInput();return 0;}
            bool seen=false;
            foreach(var touch in input.TouchLocations)
            {
                if(capturedTouch<0&&touch.State==TouchLocationState.Pressed)
                { int slot=At(touch.Position);if(slot!=0){capturedTouch=touch.Id;capturedSlot=slot;} }
                if(touch.Id!=capturedTouch)continue;seen=true;
                if(touch.State==TouchLocationState.Released)
                { int slot=capturedSlot;CancelInput();return At(touch.Position)==slot?slot:0; }
            }
            if(capturedTouch>=0&&!seen)CancelInput();
            if(!input.Click.HasValue)return 0;
            var click=input.Click.Value;int picked=At(click.Start);return picked!=0&&At(click.End)==picked?picked:0;
        }
        internal void CancelInput(){capturedTouch=-1;capturedSlot=0;}
        private int At(Vector2 screen)
        {
            for(int i=0;i<4;i++)if(buttons[i].Hit.IsEnabledGlobal&&buttons[i].Hit.HitTest(screen))return i+1;
            return 0;
        }
        private WorldCutHudLayout.Rect Bounds(Widget widget)
        {
            if(widget==null||!widget.IsVisibleGlobal||widget.ActualSize.X<=1||widget.ActualSize.Y<=1)return default(WorldCutHudLayout.Rect);
            Vector2 a=gui.ControlsContainerWidget.ScreenToWidget(widget.GlobalBounds.Min),b=gui.ControlsContainerWidget.ScreenToWidget(widget.GlobalBounds.Max);
            return new WorldCutHudLayout.Rect(Math.Min(a.X,b.X),Math.Min(a.Y,b.Y),Math.Abs(b.X-a.X),Math.Abs(b.Y-a.Y));
        }
        internal void Update(double remaining,double blinkRemaining,double horizonRemaining,bool busy,bool pending,string feedback,bool blocked)
        {
            if(blocked){panel.IsVisible=false;CancelInput();return;}
            obstacles.Clear();
            foreach(Widget widget in gui.ControlsContainerWidget.AllChildren)
            {
                if(widget==panel||widget.IsChildWidgetOf(panel)||!widget.IsVisibleGlobal||!widget.IsHitTestVisible)continue;
                if(!(widget is ButtonWidget)&&!(widget is TouchInputWidget)&&!(widget is MoveRoseWidget)&&!(widget is InventorySlotWidget))continue;
                if(obstacles.Count>=254){panel.IsVisible=false;CancelInput();return;}
                var r=Bounds(widget);if(r.Valid)obstacles.Add(r);
            }
            obstacles.Add(Bounds(gui.MoveWidget));obstacles.Add(Bounds(gui.ShortInventoryWidget));
            Vector2 size=gui.ControlsContainerWidget.ActualSize;panel.Size=size;
            WorldCutHudLayout.Placement placement;
            panel.IsVisible=WorldCutHudLayout.Place(size.X,size.Y,Bounds(gui.LookWidget),obstacles,SettingsManager.LeftHandedLayout,out placement);
            if(!panel.IsVisible){CancelInput();return;}
            for(int i=0;i<4;i++)
            {
                var r=placement.At(i);CanvasWidget.SetPosition(buttons[i].Root,new Vector2(r.X,r.Y));
                double cooldown=i==1?remaining:i==2?blinkRemaining:i==3?horizonRemaining:0;
                bool ready=cooldown<=0;
                buttons[i].Cooldown.Text=i==1&&pending?"…":ready?string.Empty:Math.Ceiling(cooldown).ToString("0");
                buttons[i].Icon.FillColor=ready&&!busy?Color.White:new Color(120,103,142,225);
                float pulse=.5f+.5f*MathUtils.Sin((float)Time.RealTime*2.2f);
                buttons[i].Glow.FillColor=ready&&!busy?new Color(177,112,240)*(buttons[i].Hit.IsPressed?.28f:.035f+pulse*.055f):Color.Transparent;
            }
            float left=Math.Min(placement.Portal.X,placement.Horizon.X),top=Math.Min(placement.Blink.Y,placement.Horizon.Y);
            var statusRect=new WorldCutHudLayout.Rect(Math.Max(8,Math.Min(size.X-240,left-28)),top-26,232,24);
            status.IsVisible=statusRect.Y>=8;
            foreach(var obstacle in obstacles)if(statusRect.Overlaps(obstacle))status.IsVisible=false;
            CanvasWidget.SetPosition(status,new Vector2(statusRect.X,statusRect.Y));
            status.Text=feedback??"轻触连斩 · 长按蓄力";
        }
        public void Dispose(){CancelInput();if(panel.ParentWidget!=null)panel.ParentWidget.Children.Remove(panel);obstacles.Clear();}
    }
}

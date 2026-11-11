using System;
using System.Collections.Generic;
using SCIEPort.Features.Artifacts;
using Engine;
using Game;

namespace SCIEPort.Features.Blueprints
{
    /// <summary>Touch controls without blocking camera movement.</summary>
    internal sealed class BlueprintPreviewControls : CanvasWidget
    {
        private readonly ComponentPlayer player;
        private readonly LabelWidget status;
        private readonly BevelledButtonWidget rotate, mirror, up, down, confirm, cancel;
        private readonly Action<int> adjust;
        private readonly Action accept, close;
        public BlueprintPreviewControls(ComponentPlayer player, Action<int> adjust, Action accept, Action close)
        {
            this.player=player; this.adjust=adjust; this.accept=accept; this.close=close;
            Size=new Vector2(360,190); HorizontalAlignment=WidgetAlignment.Near; VerticalAlignment=WidgetAlignment.Near;
            Margin=Vector2.Zero;
            Children.Add(new RectangleWidget { FillColor=new Color(18,28,38,225),OutlineColor=Color.White,OutlineThickness=1 });
            status=new LabelWidget { Size=new Vector2(340,70),WordWrap=true,FontScale=.6f,Color=Color.White };
            Children.Add(status);SetWidgetPosition(status,new Vector2(10,5));
            rotate=Button("旋转",8,76); mirror=Button("镜像",128,76); up=Button("升高",248,76);
            down=Button("降低",8,134); confirm=Button("建造",128,134); cancel=Button("取消",248,134);
        }
        private BevelledButtonWidget Button(string text,float x,float y)
        {
            var b=new BevelledButtonWidget { Text=text,Size=new Vector2(104,48),FontScale=.6f };
            Children.Add(b);SetWidgetPosition(b,new Vector2(x,y));return b;
        }
        private static void AddBounds(Widget parent,Widget widget,List<ArtifactSkillLayout.Rect> obstacles)
        {
            if(widget==null || !widget.IsVisibleGlobal)return;
            Vector2 a=parent.ScreenToWidget(widget.GlobalBounds.Min),b=parent.ScreenToWidget(widget.GlobalBounds.Max);
            obstacles.Add(new ArtifactSkillLayout.Rect{X=Math.Min(a.X,b.X)-4,Y=Math.Min(a.Y,b.Y)-4,W=Math.Abs(a.X-b.X)+8,H=Math.Abs(a.Y-b.Y)+8});
        }
        public void SetStatus(string text,bool ready) { status.Text=text;confirm.IsEnabled=ready; }
        internal void Arrange()
        {
            var parent=player.GameWidget.GuiWidget;var gui=player.ComponentGui;
            var obstacles=new List<ArtifactSkillLayout.Rect>();
            foreach(Widget w in gui.ControlsContainerWidget.AllChildren)
            {
                if(!w.IsVisibleGlobal || !w.IsHitTestVisible || (!(w is ButtonWidget)&&!(w is ClickableWidget)&&!(w is TouchInputWidget)&&!(w is MoveRoseWidget)&&!(w is InventorySlotWidget)))continue;
                if(obstacles.Count>=254){IsVisible=false;return;}
                AddBounds(parent,w,obstacles);
            }
            AddBounds(parent,gui.MoveWidget,obstacles);AddBounds(parent,gui.LookWidget,obstacles);AddBounds(parent,gui.ShortInventoryWidget,obstacles);
            ArtifactSkillLayout.Rect rect;
            IsVisible=BlueprintPreviewLayout.Place(parent.ActualSize.X,parent.ActualSize.Y,obstacles,out rect);
            if(!IsVisible)return;
            Size=new Vector2(rect.W,rect.H);CanvasWidget.SetPosition(this,new Vector2(rect.X,rect.Y));status.Size=new Vector2(rect.W-20,70);
            var buttons=new[]{rotate,mirror,up,down,confirm,cancel};float column=(rect.W-16)/3;
            for(int i=0;i<buttons.Length;i++){buttons[i].Size=new Vector2(column-8,48);SetWidgetPosition(buttons[i],new Vector2(8+(i%3)*column,i<3?76:134));}
        }
        public override void Update()
        {
            if(rotate.IsClicked) { adjust(0); return; }
            if(mirror.IsClicked) { adjust(2); return; }
            if(up.IsClicked) { adjust(1); return; }
            if(down.IsClicked) { adjust(-1); return; }
            if(confirm.IsClicked) { accept(); return; }
            if(cancel.IsClicked)close();
        }
    }
}

using System;
using Engine;
using Game;

namespace BuilderAssistant.Features.WorldEdit
{
    /// <summary>Touch controls without blocking camera movement.</summary>
    internal sealed class BlueprintPreviewControls : CanvasWidget
    {
        private readonly ComponentPlayer player;
        private readonly LabelWidget status;
        private readonly BevelledButtonWidget rotate, up, down, confirm, cancel;
        private readonly Action<int> adjust;
        private readonly Action accept, close;
        public BlueprintPreviewControls(ComponentPlayer player, Action<int> adjust, Action accept, Action close)
        {
            this.player=player; this.adjust=adjust; this.accept=accept; this.close=close;
            Size=new Vector2(540,160); HorizontalAlignment=WidgetAlignment.Center; VerticalAlignment=WidgetAlignment.Near;
            Margin=new Vector2(0,68);
            Children.Add(new RectangleWidget { FillColor=new Color(18,28,38,225),OutlineColor=Color.White,OutlineThickness=1 });
            status=new LabelWidget { Size=new Vector2(520,92),WordWrap=true,FontScale=.6f,Color=Color.White };
            Children.Add(status);SetWidgetPosition(status,new Vector2(10,5));
            rotate=Button("转90°",10,94); down=Button("降低",114,94); up=Button("升高",218,94);
            confirm=Button("确认建造",322,94); cancel=Button("取消",426,94);
        }
        private BevelledButtonWidget Button(string text,float x,float y)
        {
            var b=new BevelledButtonWidget { Text=text,Size=new Vector2(100,52),FontScale=.6f };
            Children.Add(b);SetWidgetPosition(b,new Vector2(x,y));return b;
        }
        public void SetStatus(string text,bool ready) { status.Text=text;confirm.IsEnabled=ready; }
        public override void Update()
        {
            Vector2 size=player.GameWidget.GuiWidget.ActualSize;
            float scale=MathUtils.Min(1f,MathUtils.Min((size.X-16)/540f,(size.Y-100)/160f));
            if(scale>0)LayoutTransform=Matrix.CreateScale(scale);
            if(rotate.IsClicked) { adjust(0); return; }
            if(up.IsClicked) { adjust(1); return; }
            if(down.IsClicked) { adjust(-1); return; }
            if(confirm.IsClicked) { accept(); return; }
            if(cancel.IsClicked)close();
        }
    }
}

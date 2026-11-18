using SCIEPort.Features.CelestialCodex.Core;
using Engine;
using Engine.Graphics;
using Game;

namespace SCIEPort.Features.CelestialCodex.UI
{
    // Resolve textures only while drawing, on the game's render thread. ContentManager
    // owns/caches both 256px textures; dialogs never allocate a render target or font.
    internal sealed class CodexTexture : RectangleWidget
    {
        private readonly string asset;
        internal CodexTexture(string asset)
        { this.asset=asset;IsHitTestVisible=false;OutlineThickness=0;FillColor=Color.White; }
        public override void Draw(DrawContext context)
        {
            if(Subtexture==null) Subtexture=new Subtexture(ContentManager.Get<Texture2D>("Textures/CelestialCodex/"+asset),Vector2.Zero,Vector2.One);
            base.Draw(context);
        }
    }

    internal enum CodexStroke { Frame, Rule, Chevron }
    // Stable, slightly irregular pen strokes, using only retained native 2D primitives.
    // No random animation, extra shader, tessellation, or per-frame collections.
    internal sealed class CodexInk : RectangleWidget
    {
        internal CodexStroke Stroke;
        internal CodexInk(CodexStroke stroke=CodexStroke.Frame)
        { Stroke=stroke;FillColor=Color.Transparent;OutlineThickness=1;IsHitTestVisible=false; }
        private void Line(FlatBatch2D batch,Vector2 a,Vector2 b,Color color)
        { batch.QueueLine(WidgetToScreen(a),WidgetToScreen(b),0,color); }
        public override void Draw(DrawContext context)
        {
            Color ink=OutlineColor*GlobalColorTransform;
            FlatBatch2D batch=context.PrimitivesRenderer2D.FlatBatch(1,DepthStencilState.None);
            float w=ActualSize.X,h=ActualSize.Y;
            if(Stroke==CodexStroke.Rule)
            {
                Line(batch,new Vector2(0,h/2),new Vector2(w*.36f,h/2+.35f),ink);
                Line(batch,new Vector2(w*.36f,h/2+.35f),new Vector2(w,h/2-.2f),ink);
                return;
            }
            if(Stroke==CodexStroke.Chevron)
            {
                Line(batch,new Vector2(w*.3f,h*.24f),new Vector2(w*.7f,h*.5f),ink);
                Line(batch,new Vector2(w*.7f,h*.5f),new Vector2(w*.3f,h*.76f),ink);
                return;
            }
            float r=MathUtils.Min(5,MathUtils.Min(w,h)/5);
            Line(batch,new Vector2(r,1),new Vector2(w*.52f,.55f),ink);
            Line(batch,new Vector2(w*.52f,.55f),new Vector2(w-r,1.2f),ink);
            Line(batch,new Vector2(w-r,1.2f),new Vector2(w-1,r),ink);
            Line(batch,new Vector2(w-1,r),new Vector2(w-1.5f,h*.55f),ink);
            Line(batch,new Vector2(w-1.5f,h*.55f),new Vector2(w-1,h-r),ink);
            Line(batch,new Vector2(w-1,h-r),new Vector2(w-r,h-1),ink);
            Line(batch,new Vector2(w-r,h-1),new Vector2(w*.43f,h-1.6f),ink);
            Line(batch,new Vector2(w*.43f,h-1.6f),new Vector2(r,h-1),ink);
            Line(batch,new Vector2(r,h-1),new Vector2(1,h-r),ink);
            Line(batch,new Vector2(1,h-r),new Vector2(1.5f,h*.46f),ink);
            Line(batch,new Vector2(1.5f,h*.46f),new Vector2(1,r),ink);
            Line(batch,new Vector2(1,r),new Vector2(r,1),ink);
        }
    }

    internal sealed class CodexSheet : CanvasWidget
    {
        private readonly RectangleWidget edge;
        private readonly CodexTexture grain,botanical;
        private readonly CodexInk frame,inner;
        internal CodexSheet()
        {
            IsHitTestVisible=false;
            edge=new RectangleWidget {OutlineThickness=0,IsHitTestVisible=false};
            grain=new CodexTexture("Paper") {TextureWrap=true};
            frame=new CodexInk();inner=new CodexInk();botanical=new CodexTexture("Botanical");
            Children.Add(edge);Children.Add(grain);Children.Add(frame);Children.Add(inner);Children.Add(botanical);
        }
        internal void Arrange(float width,float height,ReaderTheme theme,bool quiet,bool compact)
        {
            Size=new Vector2(width,height);
            edge.FillColor=CodexButton.Rgb(theme.Border);
            grain.Size=new Vector2(MathUtils.Max(1,width-8),MathUtils.Max(1,height-8));SetWidgetPosition(grain,new Vector2(4,3));
            grain.FillColor=ReferenceEquals(theme,ReaderTheme.Paper)?Color.White:CodexButton.Rgb(theme.Background);
            frame.Size=new Vector2(MathUtils.Max(1,width-19),MathUtils.Max(1,height-18));SetWidgetPosition(frame,new Vector2(9,8));
            frame.OutlineColor=CodexButton.Rgb(theme.Border);
            inner.Size=new Vector2(MathUtils.Max(1,width-25),MathUtils.Max(1,height-24));SetWidgetPosition(inner,new Vector2(12,11));
            Color soft=CodexButton.Rgb(theme.Border);inner.OutlineColor=new Color(soft.R*65/255,soft.G*65/255,soft.B*65/255,65);
            botanical.IsVisible=!quiet&&!compact;
            botanical.Size=new Vector2(88,88);SetWidgetPosition(botanical,new Vector2(18,19));
            botanical.FillColor=new Color(220,220,220,220);
        }
    }
}

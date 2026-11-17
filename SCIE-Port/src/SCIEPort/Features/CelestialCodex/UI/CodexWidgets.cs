using SCIEPort.Features.CelestialCodex.Core;
using Engine;
using Engine.Graphics;
using Engine.Media;
using Game;

namespace SCIEPort.Features.CelestialCodex.UI
{
    // Native LabelWidget.Text interprets matching words and [x:y] as localization keys.
    // Book content must remain literal. These cache fields are verified in the target ABI.
    internal class CodexLabel : LabelWidget
    {
        public new string Text
        {
            get { return m_text; }
            set { value = value ?? ""; if (m_text != value) { m_text = value; m_linesSize = null; } }
        }
        public override void MeasureOverride(Vector2 available)
        {
            // Native Canvas passes the remaining canvas, while Label wraps before honoring Size.
            // Cap measurement to the actual label box so long headings cannot cover neighboring buttons.
            if (Size.X >= 0) available.X = MathUtils.Min(available.X, Size.X);
            if (Size.Y >= 0) available.Y = MathUtils.Min(available.Y, Size.Y);
            base.MeasureOverride(available);
        }
    }

    // Presentation-only CJK line breaking. The native wrapper searches backwards for
    // punctuation, which leaves very short Chinese lines and orphaned closing marks.
    // Keep m_text untouched; only the native draw-line cache is rebuilt after reflow.
    internal sealed class CodexParagraph : CodexLabel
    {
        private static bool Closing(char c) => "，。！？；：、）》】」』〕〉〗］｝,.!?;:)]}”’…".IndexOf(c)>=0;
        private static bool Opening(char c) => "（《【「『〔〈〖［｛([{“‘".IndexOf(c)>=0;
        private static bool Cjk(char c) => c>='\u2e80'&&c<='\u9fff'||c>='\uff00'&&c<='\uffef';
        private static bool Latin(char c) => c<128&&char.IsLetterOrDigit(c);
        public override void MeasureOverride(Vector2 available)
        {
            bool cjk=false;foreach(char c in Text)if(Cjk(c)||char.IsSurrogate(c)){cjk=true;break;}
            if(!cjk){base.MeasureOverride(available);return;}
            if(Size.X>=0)available.X=MathUtils.Min(available.X,Size.X);
            IsDrawRequired=Text.Length>0&&Color.A!=0;
            if(!m_linesSize.HasValue||m_linesAvailableWidth!=available.X||m_linesAvailableHeight!=available.Y)
            {
                m_lines.Clear();
                foreach(string source in Text.Split('\n'))
                {
                    string paragraph=source.TrimEnd();int position=0;
                    if(paragraph.Length==0)m_lines.Add("");
                    while(position<paragraph.Length)
                    {
                        int fit=Font.FitText(available.X,paragraph,position,paragraph.Length-position,FontScale,FontSpacing.X);
                        int end=MathUtils.Min(paragraph.Length,position+MathUtils.Max(1,fit));
                        if(end<paragraph.Length)
                        {
                            if(char.IsHighSurrogate(paragraph[end-1])&&char.IsLowSurrogate(paragraph[end]))end--;
                            while(end>position+1&&(Closing(paragraph[end])||Opening(paragraph[end-1])))end--;
                            if(end>position&&Latin(paragraph[end-1])&&Latin(paragraph[end]))
                            {
                                int word=end;while(word>position&&Latin(paragraph[word-1]))word--;
                                if(word>position)end=word;
                            }
                            if(end>position&&char.IsHighSurrogate(paragraph[end-1])&&char.IsLowSurrogate(paragraph[end]))end--;
                        }
                        if(end<=position)end=position+(char.IsHighSurrogate(paragraph[position])&&position+1<paragraph.Length&&char.IsLowSurrogate(paragraph[position+1])?2:1);
                        m_lines.Add(paragraph.Substring(position,end-position).TrimEnd());position=end;
                        while(position<paragraph.Length&&char.IsWhiteSpace(paragraph[position]))position++;
                    }
                }
                Vector2 measured=Vector2.Zero;
                foreach(string line in m_lines)measured.X=MathUtils.Max(measured.X,Font.MeasureText(line,new Vector2(FontScale),FontSpacing).X);
                measured.Y=m_lines.Count==0?0:Font.GlyphHeight*FontScale*Font.Scale+(m_lines.Count-1)*CalculateLineHeight();
                m_linesSize=measured;m_linesAvailableWidth=available.X;m_linesAvailableHeight=available.Y;
            }
            DesiredSize=new Vector2(Size.X<0?m_linesSize.Value.X:Size.X,Size.Y<0?m_linesSize.Value.Y:Size.Y);
        }
    }

    internal sealed class CodexButton : ButtonWidget
    {
        private readonly RectangleWidget surface;
        private readonly CodexInk frame,mark;
        private readonly RectangleWidget ribbon;
        private readonly ClickableWidget click;
        internal readonly CodexLabel Caption, Detail;
        internal bool Prominent;
        internal bool CompactRow;
        internal ReaderTheme Theme = ReaderTheme.Paper;
        private readonly bool row;
        internal CodexButton(string text, bool row = false)
        {
            this.row = row;
            surface = new RectangleWidget { IsHitTestVisible = false, OutlineThickness = 0 };
            frame=new CodexInk();mark=new CodexInk(CodexStroke.Chevron);
            ribbon=new RectangleWidget {IsHitTestVisible=false,OutlineThickness=0};
            Caption = new CodexLabel { FontScale = row ? .78f : .68f, WordWrap = row, MaxLines = row ? 2 : 1, Ellipsis = true, TextAnchor = row ? TextAnchor.VerticalCenter : TextAnchor.HorizontalCenter | TextAnchor.VerticalCenter };
            Detail = new CodexLabel { FontScale = .56f, MaxLines = 1, Ellipsis = true, TextAnchor = TextAnchor.VerticalCenter };
            Detail.Text="";
            click = new ClickableWidget();
            Children.Add(surface);Children.Add(frame);Children.Add(ribbon);Children.Add(mark); Children.Add(Caption); Children.Add(Detail); Children.Add(click);
            Text = text; Size = new Vector2(80, row ? 92 : 48);
        }
        public override bool IsClicked { get { return click.IsClicked; } }
        public override bool IsChecked { get { return click.IsChecked; } set { click.IsChecked = value; } }
        public override bool IsAutoCheckingEnabled { get { return click.IsAutoCheckingEnabled; } set { click.IsAutoCheckingEnabled = value; } }
        public override string Text { get { return Caption.Text; } set { Caption.Text = value; } }
        public override BitmapFont Font { get { return Caption.Font; } set { Caption.Font = value; } }
        public override Color Color { get { return Caption.Color; } set { Caption.Color = value; } }
        internal static Color Rgb(int value) { return new Color((value >> 16) & 255, (value >> 8) & 255, value & 255); }
        public override void MeasureOverride(Vector2 available)
        {
            bool enabled = IsEnabledGlobal;
            surface.FillColor = Rgb(!enabled?Theme.Surface:click.IsPressed?Theme.Pressed:Prominent?Theme.Accent:Theme.Surface);
            frame.OutlineColor=Rgb(IsChecked?Theme.Accent:Theme.Border);
            Caption.Color = Rgb(!enabled ? Theme.Muted : Prominent && !click.IsPressed ? Theme.OnAccent : Theme.Ink);
            Detail.Color = Rgb(enabled&&Prominent&&!click.IsPressed?Theme.OnAccent:Theme.Muted);
            float inset = row ? 20 : 6;
            bool detail = row && !string.IsNullOrEmpty(Detail.Text);
            Caption.FontScale=row?(CompactRow?.68f:.78f):.65f;
            Caption.Size = new Vector2(MathUtils.Max(1, Size.X - inset * 2-(row?14:0)), detail ? Size.Y-34 : Size.Y-12);
            Detail.Size = new Vector2(MathUtils.Max(1, Size.X - inset*2-14), 22);
            SetWidgetPosition(Caption, new Vector2(inset, 6));
            SetWidgetPosition(Detail, new Vector2(inset, Size.Y-29));
            ribbon.IsVisible=row&&(Prominent||IsChecked)||!row&&IsChecked;
            ribbon.FillColor=Rgb(enabled&&Prominent&&!click.IsPressed?Theme.OnAccent:Theme.Accent);
            ribbon.Size=row?new Vector2(3,MathUtils.Max(8,Size.Y-32)):new Vector2(MathUtils.Max(1,Size.X-28),2);
            SetWidgetPosition(ribbon,row?new Vector2(8,16):new Vector2(14,Size.Y-6));
            mark.IsVisible=row;mark.OutlineColor=Caption.Color;mark.Size=new Vector2(10,18);SetWidgetPosition(mark,new Vector2(Size.X-22,(Size.Y-18)/2));
            Detail.IsVisible = detail;
            base.MeasureOverride(available);
        }
    }
}

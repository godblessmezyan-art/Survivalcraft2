using CelestialCodex.Core;
using Engine;
using Engine.Graphics;
using Engine.Media;
using Game;

namespace CelestialCodex.UI
{
    // Native LabelWidget.Text interprets matching words and [x:y] as localization keys.
    // Book content must remain literal. These cache fields are verified in the target ABI.
    internal sealed class CodexLabel : LabelWidget
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

    internal sealed class CodexButton : ButtonWidget
    {
        private readonly RectangleWidget surface;
        private readonly ClickableWidget click;
        internal readonly CodexLabel Caption, Detail;
        internal bool Prominent;
        internal ReaderTheme Theme = ReaderTheme.Paper;
        private readonly bool row;
        internal CodexButton(string text, bool row = false)
        {
            this.row = row;
            surface = new RectangleWidget { IsHitTestVisible = false, OutlineThickness = 1 };
            Caption = new CodexLabel { FontScale = row ? .78f : .68f, WordWrap = row, MaxLines = row ? 2 : 1, Ellipsis = true, TextAnchor = row ? TextAnchor.VerticalCenter : TextAnchor.HorizontalCenter | TextAnchor.VerticalCenter };
            Detail = new CodexLabel { FontScale = .56f, MaxLines = 1, Ellipsis = true, TextAnchor = TextAnchor.VerticalCenter };
            click = new ClickableWidget();
            Children.Add(surface); Children.Add(Caption); Children.Add(Detail); Children.Add(click);
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
            surface.FillColor = Rgb(click.IsPressed && enabled ? Theme.Pressed : Prominent ? Theme.Accent : Theme.Surface);
            surface.OutlineColor = Rgb(IsChecked ? Theme.Accent : Theme.Border);
            surface.OutlineThickness = IsChecked ? 2 : 1;
            Caption.Color = Rgb(!enabled ? Theme.Muted : Prominent && !click.IsPressed ? Theme.OnAccent : Theme.Ink);
            Detail.Color = Rgb(Prominent && !click.IsPressed ? Theme.OnAccent : Theme.Muted);
            float inset = row ? 16 : 4;
            bool detail = row && !string.IsNullOrEmpty(Detail.Text);
            Caption.Size = new Vector2(MathUtils.Max(1, Size.X - inset * 2), detail ? 52 : Size.Y - 8);
            Detail.Size = new Vector2(MathUtils.Max(1, Size.X - 32), 24);
            SetWidgetPosition(Caption, new Vector2(inset, 4));
            SetWidgetPosition(Detail, new Vector2(16, Size.Y - 30));
            Detail.IsVisible = detail;
            base.MeasureOverride(available);
        }
    }
}

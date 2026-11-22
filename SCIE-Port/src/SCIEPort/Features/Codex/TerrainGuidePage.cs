using Engine;
using Game;
using SCIEPort.Features.CelestialCodex.UI;

namespace SCIEPort.Features.Codex
{
    internal sealed class TerrainGuidePage : CanvasWidget
    {
        private static readonly Color Ink = new Color(53, 39, 30);
        private static readonly Color Slate = new Color(39, 45, 70);
        private static readonly Color Gold = new Color(113, 77, 42);
        private readonly RectangleWidget paper;
        private readonly CanvasWidget indexFrame, readingFrame;
        private readonly ScrollPanelWidget indexScroll, readingScroll;
        private readonly CanvasWidget index;
        private readonly LabelWidget title;
        private readonly CodexParagraph description;
        private readonly BevelledButtonWidget[] sections;
        private readonly BevelledButtonWidget source;
        private BevelledButtonWidget[] rows;
        private int section, selected;
        private bool compact;
        private float listWidth;

        internal BevelledButtonWidget Back { get; }
        internal BevelledButtonWidget Close { get; }

        internal TerrainGuidePage()
        {
            HorizontalAlignment = WidgetAlignment.Center;
            VerticalAlignment = WidgetAlignment.Center;
            paper = new RectangleWidget { FillColor = new Color(232, 222, 191, 252), OutlineColor = Gold, OutlineThickness = 3 };
            Children.Add(paper);
            Place(this, new LabelWidget { Text = "地形图鉴", Color = Ink, FontScale = .9f, Size = new Vector2(190, 32) }, 16, 9);
            sections = new[] { Button("奇景"), Button("地标"), Button("竹林与遗迹") };
            foreach (BevelledButtonWidget button in sections) Children.Add(button);

            indexFrame = new CanvasWidget();
            indexScroll = MakeScroll(indexFrame);
            index = new CanvasWidget();
            indexScroll.Children.Add(index);
            Children.Add(indexFrame);

            title = new LabelWidget { Color = Ink, FontScale = .91f };
            Children.Add(title);
            readingFrame = new CanvasWidget();
            readingScroll = MakeScroll(readingFrame);
            description = new CodexParagraph { Color = Ink, FontScale = .68f, FontSpacing = new Vector2(0, 5),
                WordWrap = true, MaxLines = int.MaxValue, Ellipsis = false };
            readingScroll.Children.Add(description);
            Children.Add(readingFrame);

            source = Button("资料来源");
            Back = Button("返回神器");
            Close = Button("关闭");
            Children.Add(source);
            Children.Add(Back);
            Children.Add(Close);
            SelectSection(0);
            Layout(new Vector2(660, 460));
        }

        internal void Refresh(Vector2 available)
        {
            Layout(available);
            for (int i = 0; i < sections.Length; i++)
            {
                if (!sections[i].IsClicked || i == section) continue;
                SelectSection(i);
                return;
            }
            for (int i = 0; i < rows.Length; i++)
            {
                if (!rows[i].IsClicked || i == selected) continue;
                SelectEntry(i);
                return;
            }
            string url = TerrainGuideData.Sections[section][selected].SourceUrl;
            if (source.IsVisible && source.IsClicked) WebBrowserManager.LaunchBrowser(url);
        }

        internal void SelectSection(int value)
        {
            section = value;
            for (int i = 0; i < sections.Length; i++) sections[i].CenterColor = i == section ? Gold : Slate;
            index.Children.Clear();
            TerrainGuideEntry[] entries = TerrainGuideData.Sections[section];
            rows = new BevelledButtonWidget[entries.Length];
            for (int i = 0; i < entries.Length; i++)
            {
                rows[i] = Button(entries[i].Name);
                index.Children.Add(rows[i]);
            }
            LayoutRows();
            Reset(indexScroll);
            SelectEntry(0);
        }

        internal void SelectEntry(int value)
        {
            selected = value;
            for (int i = 0; i < rows.Length; i++) rows[i].CenterColor = i == selected ? Gold : Slate;
            TerrainGuideEntry entry = TerrainGuideData.Sections[section][selected];
            title.Text = entry.Name;
            description.Text = entry.Text;
            source.IsVisible = entry.SourceUrl != null;
            Reset(readingScroll);
        }

        internal void Layout(Vector2 available)
        {
            float width = MathUtils.Clamp(available.X - 16, 260, 700);
            float height = MathUtils.Clamp(available.Y - 16, 220, 530);
            var size = new Vector2(width, height);
            if (Size == size) return;
            Size = paper.Size = size;
            compact = width < 530 && height >= 300;
            bool sharedHeader = width < 530 && !compact;
            listWidth = compact ? width - 32 : MathUtils.Min(206, width * .42f);
            for (int i = 0; i < sections.Length; i++)
                PositionButton(sections[i], 16 + i * 65, 44, i == 2 ? 92 : 60, 38);
            float indexHeight = compact ? MathUtils.Clamp(height * .24f, 44, 126) : height - 138;
            float readingX = compact ? 16 : listWidth + 34;
            float headingY = compact ? 90 + indexHeight + 6 : sharedHeader ? 89 : 15;
            float readingY = headingY + 32;
            float readingWidth = width - readingX - 16;
            indexFrame.Size = new Vector2(listWidth, indexHeight);
            SetWidgetPosition(indexFrame, new Vector2(16, 89));
            SetWidgetPosition(title, new Vector2(readingX, headingY));
            title.Size = new Vector2(readingWidth, 32);
            readingFrame.Size = new Vector2(readingWidth, MathUtils.Max(32, height - readingY - 54));
            SetWidgetPosition(readingFrame, new Vector2(readingX, readingY));
            description.Size = new Vector2(readingWidth - 8, -1);
            PositionButton(source, 16, height - 48, 88, 38);
            PositionButton(Back, width - 155, height - 48, 82, 38);
            PositionButton(Close, width - 68, height - 48, 52, 38);
            LayoutRows();
        }

        private void LayoutRows()
        {
            const int gap = 5, height = 39, stride = height + gap;
            int columns = compact && listWidth >= 310 ? 2 : 1;
            float width = (listWidth - 8 - (columns - 1) * gap) / columns;
            index.Size = new Vector2(listWidth - 8, ((rows.Length + columns - 1) / columns) * stride);
            for (int i = 0; i < rows.Length; i++)
            {
                index.SetWidgetPosition(rows[i], new Vector2(i % columns * (width + gap), i / columns * stride));
                rows[i].Size = new Vector2(width, height);
            }
        }

        private void PositionButton(BevelledButtonWidget button, float x, float y, float width, float height)
        {
            button.Size = new Vector2(width, height);
            SetWidgetPosition(button, new Vector2(x, y));
        }

        private static void Place(CanvasWidget parent, Widget widget, float x, float y)
        {
            if (widget.ParentWidget == null) parent.Children.Add(widget);
            parent.SetWidgetPosition(widget, new Vector2(x, y));
        }

        private static ScrollPanelWidget MakeScroll(CanvasWidget parent)
        {
            var scroll = new ScrollPanelWidget { Direction = LayoutDirection.Vertical, HorizontalAlignment = WidgetAlignment.Stretch,
                VerticalAlignment = WidgetAlignment.Stretch };
            parent.Children.Add(scroll);
            return scroll;
        }

        private static BevelledButtonWidget Button(string text) => new BevelledButtonWidget
        {
            Text = text, FontScale = .64f, CenterColor = Slate, BevelColor = new Color(177, 142, 78)
        };

        private static void Reset(ScrollPanelWidget scroll)
        {
            scroll.ScrollPosition = 0;
            scroll.ScrollSpeed = 0;
        }
    }
}

using Engine;
using Game;

namespace SCIEPort.Features.OmniscientEye
{
    internal sealed class EyeDialog : Dialog
    {
        private EyeView m_view;
        private readonly CanvasWidget m_panel;
        private readonly LabelWidget m_content;
        private readonly BevelledButtonWidget m_environment, m_blocks, m_hud, m_scan, m_close;
        private bool m_blockPage;
        private string m_scanText = "对准方块后点击扫描。\n扫描范围 8 格；只读取已加载数据。";

        internal EyeDialog(EyeView view)
        {
            m_view = view;
            m_panel = new CanvasWidget { Size = new Vector2(520, 344), HorizontalAlignment = WidgetAlignment.Center, VerticalAlignment = WidgetAlignment.Center };
            Children.Add(m_panel);
            m_panel.Children.Add(new RectangleWidget { FillColor = new Color(9, 20, 46, 245), OutlineColor = new Color(185, 132, 60), OutlineThickness = 2 });
            Add(new LabelWidget { Text = "全知之眼 · 万象解析", Color = new Color(255, 239, 202), FontScale = .9f }, 16, 12);
            m_environment = Button("环境", 16, 52, 116); m_blocks = Button("方块", 140, 52, 116);
            Button("生物 · 预留", 264, 52, 116).IsEnabled = false;
            Button("诊断 · 预留", 388, 52, 116).IsEnabled = false;
            m_content = new LabelWidget { Size = new Vector2(480, 160), Color = new Color(129, 226, 235), FontScale = .8f, WordWrap = true };
            Add(m_content, 20, 104);
            m_scan = Button("主动扫描", 16, 286, 130);
            m_hud = Button("开启全知视界", 156, 286, 200);
            m_close = Button("关闭", 366, 286, 138);
        }

        private void Add(Widget widget, float x, float y) { m_panel.Children.Add(widget); m_panel.SetWidgetPosition(widget, new Vector2(x, y)); }
        private BevelledButtonWidget Button(string text, float x, float y, float width)
        {
            var button = new BevelledButtonWidget { Text = text, Size = new Vector2(width, 44), FontScale = .7f, CenterColor = new Color(21, 39, 67), BevelColor = new Color(185, 132, 60) };
            Add(button, x, y); return button;
        }

        public override void Update()
        {
            if (m_view == null) return;
            Vector2 available = m_view.Gui.m_componentPlayer.GameWidget.GuiWidget.ActualSize;
            float scale = MathUtils.Min(1, MathUtils.Min((available.X - 16) / 520, (available.Y - 16) / 344));
            if (scale > 0) m_panel.LayoutTransform = Matrix.CreateScale(scale);
            if (m_environment.IsClicked) m_blockPage = false;
            if (m_blocks.IsClicked) m_blockPage = true;
            if (m_hud.IsClicked) m_view.Enabled = !m_view.Enabled;
            if (m_scan.IsClicked) m_scanText = m_view.Scan();
            m_scan.IsVisible = m_blockPage;
            m_hud.Text = m_view.Enabled ? "关闭全知视界" : "开启全知视界";
            m_content.Text = m_blockPage ? m_scanText : m_view.EnvironmentText + "\n气候为引擎等级（0–15），无现实单位。\nV0.1 只读 · 不修改地形或实体";
            if (m_close.IsClicked || Input.Back || Input.Cancel) { Input.Clear(); m_view.CloseDialog(); }
        }

        internal void Release() { m_view = null; }
    }
}

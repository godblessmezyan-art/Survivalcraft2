using System;
using System.Globalization;
using System.Collections.Generic;
using Engine;
using Game;

namespace SCIEPort.Features.OmniscientEye
{
    internal sealed class EyeView : IDisposable
    {
        internal bool OpenRequested;
        internal bool Enabled;
        internal string PositionText = "";
        internal string ClimateText = "";
        internal string EnvironmentText => PositionText + "\n" + ClimateText;
        internal readonly ComponentGui Gui;
        private readonly SubsystemTerrain m_terrain;
        private readonly CanvasWidget m_hud;
        private readonly LabelWidget m_label;
        private readonly RectangleWidget m_background;
        private EyeDialog m_dialog;
        private double m_nextPosition, m_nextClimate;

        internal EyeView(ComponentGui gui)
        {
            Gui = gui;
            m_terrain = gui.m_componentPlayer.Project.FindSubsystem<SubsystemTerrain>(true);
            m_hud = new CanvasWidget { Size = new Vector2(340, 118), HorizontalAlignment = WidgetAlignment.Near, VerticalAlignment = WidgetAlignment.Near, IsHitTestVisible = false, IsVisible = false };
            m_background = new RectangleWidget { Size = m_hud.Size, FillColor = new Color(9, 20, 46, 205), OutlineColor = new Color(178, 130, 66), OutlineThickness = 1, IsHitTestVisible = false };
            m_label = new LabelWidget { Text = "全知视界", FontScale = .7f, Color = new Color(129, 226, 235), Size = new Vector2(320, 108), IsHitTestVisible = false };
            m_hud.Children.Add(m_background); m_hud.Children.Add(m_label);
            m_hud.SetWidgetPosition(m_label, new Vector2(10, 6));
            gui.ControlsContainerWidget.Children.Add(m_hud);
        }

        internal void Tick()
        {
            if (m_dialog != null && m_dialog.ParentWidget != null && !DialogsManager.Dialogs.Contains(m_dialog))
            {
                m_dialog.Release(); m_dialog = null;
            }
            if (OpenRequested)
            {
                OpenRequested = false;
                if (m_dialog == null)
                {
                    m_dialog = new EyeDialog(this);
                    DialogsManager.ShowDialog(Gui.m_componentPlayer.GameWidget.GuiWidget, m_dialog);
                }
            }
            if (Enabled || m_dialog != null)
            {
                double now = Time.RealTime;
                if (now >= m_nextPosition) { RefreshPosition(); m_nextPosition = now + .2; }
                if (now >= m_nextClimate) { RefreshClimate(); m_nextClimate = now + 1; }
                m_label.Text = "全知视界\n" + EnvironmentText;
            }
            // Re-evaluate actual native control bounds (including left-handed layout and UI scale).
            float width = Gui.ControlsContainerWidget.ActualSize.X;
            float height = Gui.ControlsContainerWidget.ActualSize.Y;
            var obstacles = new List<EyeLayout.Rect>();
            foreach (Widget widget in new Widget[] { Gui.m_leftControlsContainerWidget, Gui.m_rightControlsContainerWidget, Gui.MoveWidget, Gui.LookWidget, Gui.ShortInventoryWidget, Gui.HealthBarWidget, Gui.LevelLabelWidget })
            {
                if (widget == null || !widget.IsVisibleGlobal || widget.ActualSize.X <= 0) continue;
                Vector2 a = Gui.ControlsContainerWidget.ScreenToWidget(widget.WidgetToScreen(Vector2.Zero));
                Vector2 b = Gui.ControlsContainerWidget.ScreenToWidget(widget.WidgetToScreen(widget.ActualSize));
                obstacles.Add(new EyeLayout.Rect { X = MathUtils.Min(a.X, b.X), Y = MathUtils.Min(a.Y, b.Y), Width = MathUtils.Abs(b.X-a.X), Height = MathUtils.Abs(b.Y-a.Y) });
            }
            EyeLayout.Rect? placement = EyeLayout.Place(width, height, obstacles);
            if (placement.HasValue)
            {
                m_hud.Margin = new Vector2(placement.Value.X, placement.Value.Y);
                m_hud.Size = new Vector2(placement.Value.Width, placement.Value.Height);
                m_background.Size = m_hud.Size;
                m_label.Size = new Vector2(placement.Value.Width - 20, 108);
            }
            m_hud.IsVisible = Enabled && Gui.ModalPanelWidget == null && m_dialog == null && !DialogsManager.HasDialogs(Gui.m_componentPlayer.GameWidget.GuiWidget) && placement.HasValue;
        }

        private void RefreshPosition()
        {
            Vector3 p = Gui.m_componentPlayer.ComponentBody.Position;
            int x = Terrain.ToCell(p.X), z = Terrain.ToCell(p.Z);
            Point2 chunk = Terrain.ToChunk(x, z);
            PositionText = string.Format(CultureInfo.InvariantCulture, "X {0:0.0}  Y {1:0.0}  Z {2:0.0}\n高度 {3}  区块 {4},{5}", p.X, p.Y, p.Z, Terrain.ToCell(p.Y), chunk.X, chunk.Y);
        }

        private void RefreshClimate()
        {
            Vector3 p = Gui.m_componentPlayer.ComponentBody.Position;
            int x = Terrain.ToCell(p.X), z = Terrain.ToCell(p.Z);
            TerrainChunk chunk = m_terrain.Terrain.GetChunkAtCell(x, z);
            if (chunk == null || chunk.State <= TerrainChunkState.InvalidContents4)
            {
                ClimateText = "气候：区块数据未就绪"; return;
            }
            ClimateText = string.Format(CultureInfo.InvariantCulture, "温度等级 {0}  湿度等级 {1}\n区块状态 {2}", m_terrain.Terrain.GetTemperature(x, z), m_terrain.Terrain.GetHumidity(x, z), chunk.State);
        }

        internal string Scan()
        {
            Camera camera = Gui.m_componentPlayer.GameWidget.ActiveCamera;
            TerrainRaycastResult? hit = Gui.m_componentPlayer.ComponentMiner.Raycast<TerrainRaycastResult>(new Ray3(camera.ViewPosition, camera.ViewDirection), RaycastMode.Digging);
            if (!hit.HasValue || hit.Value.Distance > 8) return "8 格内未命中方块。\n请对准已加载的方块，再点击扫描。";
            Point3 p = hit.Value.CellFace.Point;
            if (p.Y < 0 || p.Y >= 256 || m_terrain.Terrain.GetChunkAtCell(p.X, p.Z) == null) return "目标区块数据未就绪。";
            int value = m_terrain.Terrain.GetCellValue(p.X, p.Y, p.Z);
            int contents = Terrain.ExtractContents(value);
            Block block = BlocksManager.Blocks[contents];
            return string.Format(CultureInfo.InvariantCulture, "{0}\n坐标 {1}, {2}, {3}\nID {4}  DATA {5}  光照等级 {6}\n类型 {7}\n距离 {8:0.0} 格", block.GetDisplayName(m_terrain, value), p.X, p.Y, p.Z, contents, Terrain.ExtractData(value), Terrain.ExtractLight(value), block.GetType().Name, hit.Value.Distance);
        }

        internal void CloseDialog()
        {
            if (m_dialog != null) { m_dialog.Release(); DialogsManager.HideDialog(m_dialog); }
            m_dialog = null;
        }
        public void Dispose()
        {
            Enabled = false; OpenRequested = false; CloseDialog();
            m_hud.ParentWidget?.Children.Remove(m_hud);
        }
    }
}

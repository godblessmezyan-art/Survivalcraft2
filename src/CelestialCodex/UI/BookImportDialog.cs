using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CelestialCodex.Core;
using CelestialCodex.Mod;
using Engine;
using Game;

namespace CelestialCodex.UI
{
    /// <summary>
    /// In-game import surface. Tries the host's native file picker first;
    /// if unavailable, browses only the Engine.Storage paths accessible to this client.
    /// No host-specific Android DLL references and no world/room writes.
    /// </summary>
    internal sealed class BookImportDialog : Dialog
    {
        private readonly ComponentPlayer player;
        private readonly Func<string, CancellationToken, string> importFile;
        private readonly Action imported;
        private readonly CanvasWidget panel, viewport;
        private readonly ScrollPanelWidget scroll;
        private readonly StackPanelWidget entries;
        private readonly LabelWidget title, status;
        private readonly BevelledButtonWidget back, close, system, up, refresh;
        private readonly List<BevelledButtonWidget> rowButtons = new List<BevelledButtonWidget>();
        private readonly List<string> rowNames = new List<string>();
        private readonly List<bool> rowDirectories = new List<bool>();
        private readonly object choiceGate = new object();
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private string directory = BookFilePicker.Root;
        private string pendingPath;
        private bool pendingChoice, nativeChoosing, importing;
        private bool closed;
        private Task<string> importTask;

        internal bool Closed { get { return closed; } }

        internal BookImportDialog(ComponentPlayer player,
            Func<string, CancellationToken, string> importFile, Action imported)
        {
            this.player = player;
            this.importFile = importFile;
            this.imported = imported;
            panel = new CanvasWidget { HorizontalAlignment = WidgetAlignment.Center, VerticalAlignment = WidgetAlignment.Center };
            Children.Add(panel);
            panel.Children.Add(new RectangleWidget
            {
                FillColor = new Color(239, 222, 181),
                OutlineColor = new Color(112, 78, 39), OutlineThickness = 3
            });
            title = new LabelWidget
            {
                Color = new Color(63, 41, 24), FontScale = .75f,
                TextAnchor = TextAnchor.HorizontalCenter | TextAnchor.VerticalCenter,
                Text = "导入图书"
            };
            status = new LabelWidget { Color = new Color(99, 65, 34), FontScale = .57f, Text = "请选择 TXT / EPUB" };
            panel.Children.Add(title);
            panel.Children.Add(status);
            back = Button("返回"); close = Button("关闭");
            system = Button("系统选书"); up = Button("上一级"); refresh = Button("刷新");
            viewport = new CanvasWidget(); panel.Children.Add(viewport);
            scroll = new ScrollPanelWidget { Direction = LayoutDirection.Vertical, ScrollPosition = 0, ScrollSpeed = 0 };
            viewport.Children.Add(scroll);
            entries = new StackPanelWidget { Direction = LayoutDirection.Vertical };
            scroll.Children.Add(entries);
            Refresh();
        }

        private BevelledButtonWidget Button(string label)
        {
            var b = new BevelledButtonWidget
            {
                Text = label, FontScale = .64f, Color = new Color(62, 41, 23),
                BevelColor = new Color(183, 151, 102), CenterColor = new Color(222, 200, 156),
                Size = new Vector2(84, 44)
            };
            panel.Children.Add(b);
            return b;
        }

        private void Place(Widget widget, float x, float y, float w, float h)
        {
            var button = widget as BevelledButtonWidget;
            if (button != null) button.Size = new Vector2(w, h);
            var label = widget as LabelWidget;
            if (label != null) label.Size = new Vector2(w, h);
            panel.SetWidgetPosition(widget, new Vector2(x, y));
        }

        private void Layout()
        {
            Vector2 available = player.GameWidget.GuiWidget.ActualSize;
            var l = new ReaderLayout(available.X, available.Y);
            float w = l.Width, h = l.Height;
            panel.Size = new Vector2(w, h);
            Place(back, 12, 10, 70, 44);
            Place(close, w - 82, 10, 70, 44);
            Place(title, 86, 10, MathUtils.Max(1, w - 172), 44);
            Place(system, 12, 62, MathUtils.Max(1, (w - 36) / 2), 44);
            Place(up, 24 + MathUtils.Max(1, (w - 36) / 2), 62, MathUtils.Max(1, (w - 36) / 2), 44);
            Place(status, 16, 108, MathUtils.Max(1, w - 32), 34);
            viewport.Size = new Vector2(MathUtils.Max(1, w - 36), MathUtils.Max(1, h - 205));
            panel.SetWidgetPosition(viewport, new Vector2(18, 148));
            Place(refresh, 12, h - 50, MathUtils.Max(1, w - 24), 44);
            foreach (BevelledButtonWidget button in rowButtons)
                button.Size = new Vector2(MathUtils.Max(1, w - 40), 52);
        }

        private void Row(string name, bool isDirectory)
        {
            if (rowNames.Count >= 80) return;
            var button = new BevelledButtonWidget
            {
                Text = (isDirectory ? "目录 · " : "图书 · ") + (name.Length > 45 ? name.Substring(0, 44) + "…" : name),
                FontScale = .66f, Color = new Color(62, 41, 23),
                BevelColor = new Color(183, 151, 102), CenterColor = new Color(222, 200, 156),
                Size = new Vector2(400, 52), Margin = new Vector2(0, 3)
            };
            entries.Children.Add(button);
            rowButtons.Add(button); rowNames.Add(name); rowDirectories.Add(isDirectory);
        }

        private void Refresh()
        {
            entries.Children.Clear(); rowButtons.Clear(); rowNames.Clear(); rowDirectories.Clear();
            scroll.ScrollPosition = 0; scroll.ScrollSpeed = 0;
            List<string> folders = BookFilePicker.Directories(directory);
            List<string> books = BookFilePicker.BookFiles(directory);
            foreach (string name in folders) Row(name, true);
            foreach (string name in books) Row(name, false);
            if (folders.Count + books.Count == 0)
                status.Text = "当前目录没有可选的 TXT/EPUB";
            else
                status.Text = "浏览 " + (directory.Length > 42 ? "…" + directory.Substring(directory.Length - 41) : directory);
            if (folders.Count + books.Count > 80) status.Text = "文件过多，仅显示前80项";
        }

        private void BeginSystemPicker()
        {
            if (nativeChoosing || importing) return;
            nativeChoosing = true;
            bool started = BookFilePicker.TryChoose(path =>
            {
                // A platform callback may occur on a different thread. Never touch widgets here.
                lock (choiceGate) { pendingPath = path; pendingChoice = true; }
            });
            if (started) status.Text = "请选择 TXT 或 EPUB";
            else
            {
                nativeChoosing = false;
                status.Text = "系统选书不可用，请浏览游戏可访问的文件";
            }
        }

        private void BeginImport(string virtualPath)
        {
            if (importing || string.IsNullOrEmpty(virtualPath)) return;
            importing = true;
            status.Text = "正在导入…";
            CancellationToken token = cancellation.Token;
            importTask = Task.Run(() => importFile(virtualPath, token));
        }

        private void ProcessPending()
        {
            string chosen = null; bool ready;
            lock (choiceGate)
            {
                ready = pendingChoice;
                if (ready) { chosen = pendingPath; pendingChoice = false; pendingPath = null; }
            }
            if (ready)
            {
                nativeChoosing = false;
                if (string.IsNullOrEmpty(chosen)) status.Text = "已取消选择";
                else BeginImport(chosen);
            }
            Task<string> task = importTask;
            if (task == null || !task.IsCompleted) return;
            importTask = null; importing = false;
            if (task.IsFaulted)
            {
                Exception failure = task.Exception.GetBaseException();
                string message = failure.Message;
                status.Text = message.Length > 55 ? message.Substring(0, 54) + "…" : message;
            }
            else if (task.IsCanceled) status.Text = "导入已取消";
            else
            {
                status.Text = "导入成功：" + task.Result;
                Close(true);
            }
        }

        private void Close(bool success)
        {
            if (closed) return;
            closed = true;
            cancellation.Cancel();
            if (ParentWidget != null) DialogsManager.HideDialog(this);
            if (success && imported != null) imported();
        }

        public override void UpdateCeases() { Close(false); base.UpdateCeases(); }
        public override void Dispose() { Close(false); base.Dispose(); }

        public override void Update()
        {
            if (closed) return;
            try
            {
                if (player == null || player.GameWidget == null || player.GameWidget.GuiWidget == null)
                { Close(false); return; }
                Layout();
                ProcessPending();
                if (closed) return;
                bool ready = !importing && !nativeChoosing;
                system.IsEnabled = ready;
                up.IsEnabled = ready && directory != BookFilePicker.Root;
                refresh.IsEnabled = ready;
                foreach (BevelledButtonWidget b in rowButtons) b.IsEnabled = ready;

                if (ready)
                {
                    for (int i = 0; i < rowButtons.Count; i++)
                        if (rowButtons[i].IsClicked)
                        {
                            string path = BookFilePicker.Child(directory, rowNames[i]);
                            if (rowDirectories[i]) { directory = path; Refresh(); }
                            else BeginImport(path);
                            break;
                        }
                    if (system.IsClicked) BeginSystemPicker();
                    if (up.IsClicked) { directory = BookFilePicker.Parent(directory); Refresh(); }
                    if (refresh.IsClicked) Refresh();
                }
                if (close.IsClicked || back.IsClicked || Input.Back || Input.Cancel)
                {
                    Input.Clear(); Close(false);
                }
            }
            catch (Exception error)
            {
                Log.Warning("[CelestialCodex] Import UI: " + error.GetType().Name);
                status.Text = "导入界面暂不可用";
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SCIEPort.Features.CelestialCodex.Core;
using SCIEPort.Features.CelestialCodex.Mod;
using Engine;
using Engine.Graphics;
using Game;

namespace SCIEPort.Features.CelestialCodex.UI
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
        private readonly CodexLabel title,status,eyebrow;
        private readonly CodexSheet paper;
        private readonly CodexInk rule;
        private readonly ReaderTheme theme;
        private readonly CodexButton back,close,system,up,refresh;
        private readonly List<CodexButton> rowButtons = new List<CodexButton>();
        private readonly List<string> rowNames = new List<string>();
        private readonly List<bool> rowDirectories = new List<bool>();
        private readonly object choiceGate = new object();
        private readonly Action<string> choiceCallback;
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private string directory = BookFilePicker.Root;
        private string pendingPath;
        private bool pendingChoice, nativeChoosing, importing;
        private bool closed;
        private bool launchRequested=true;
        private Task<string> importTask;

        internal bool Closed { get { return closed; } }

        internal BookImportDialog(ComponentPlayer player,
            Func<string, CancellationToken, string> importFile, Action imported,ReaderTheme theme=null,bool populate=true)
        {
            this.player = player;
            this.importFile = importFile;
            this.imported = imported;
            this.theme=theme??ReaderTheme.Paper;
            choiceCallback=ReceiveChoice;
            panel = new CanvasWidget { HorizontalAlignment = WidgetAlignment.Center, VerticalAlignment = WidgetAlignment.Center };
            Children.Add(panel);
            paper=new CodexSheet();panel.Children.Add(paper);
            rule=new CodexInk(CodexStroke.Rule) {OutlineColor=CodexButton.Rgb(this.theme.Border)};panel.Children.Add(rule);
            eyebrow=new CodexLabel {Text="云 天 书 典",FontScale=.45f,MaxLines=1,TextAnchor=TextAnchor.HorizontalCenter|TextAnchor.VerticalCenter,Color=CodexButton.Rgb(this.theme.Muted)};panel.Children.Add(eyebrow);
            title = new CodexLabel
            {
                Color=CodexButton.Rgb(this.theme.Ink),FontScale=.82f,MaxLines=1,Ellipsis=true,
                TextAnchor = TextAnchor.HorizontalCenter | TextAnchor.VerticalCenter,
                Text = "导入图书"
            };
            status = new CodexLabel { Color=CodexButton.Rgb(this.theme.Muted),FontScale=.56f,Text="TXT · EPUB",MaxLines=1,Ellipsis=true,TextAnchor=TextAnchor.HorizontalCenter|TextAnchor.VerticalCenter };
            panel.Children.Add(title);
            panel.Children.Add(status);
            back = Button("返回"); close = Button("关闭");
            system = Button("系统选书"); up = Button("上一级"); refresh = Button("刷新");
            viewport = new CanvasWidget(); panel.Children.Add(viewport);
            scroll = new ScrollPanelWidget { Direction = LayoutDirection.Vertical, ScrollPosition = 0, ScrollSpeed = 0 };
            viewport.Children.Add(scroll);
            entries = new StackPanelWidget { Direction = LayoutDirection.Vertical };
            scroll.Children.Add(entries);
            if(populate)Refresh();
        }

        private CodexButton Button(string label)
        {
            var b=new CodexButton(label) {Theme=theme,Size=new Vector2(84,48)};
            panel.Children.Add(b);
            return b;
        }

        private void Place(Widget widget, float x, float y, float w, float h)
        {
            var button = widget as CodexButton;
            if (button != null) button.Size = new Vector2(w, h);
            var label = widget as LabelWidget;
            if (label != null) label.Size = new Vector2(w, h);
            panel.SetWidgetPosition(widget, new Vector2(x, y));
        }

        private void Layout(Vector2 available)
        {
            var l = new ReaderLayout(available.X, available.Y);
            float w = l.Width, h = l.Height;
            panel.Size = new Vector2(w, h);
            paper.Arrange(w,h,theme,false,l.Compact);
            Place(back,18,12,64,48);Place(close,w-82,12,64,48);
            eyebrow.IsVisible=!l.Compact;Place(eyebrow,88,20,MathUtils.Max(1,w-176),18);
            Place(title,88,l.Compact?4:42,MathUtils.Max(1,w-176),l.Compact?40:36);
            Place(status,l.ContentX,l.Compact?48:92,l.ContentWidth,l.Compact?16:24);
            rule.Size=new Vector2(l.ContentWidth,2);panel.SetWidgetPosition(rule,new Vector2(l.ContentX,l.ContentY-9));
            viewport.Size=new Vector2(l.ContentWidth,l.ContentHeight);panel.SetWidgetPosition(viewport,new Vector2(l.ContentX,l.ContentY));
            float bw=(l.ContentWidth-16)/3;
            Place(system,l.ContentX,l.FooterY,bw,48);Place(up,l.ContentX+bw+8,l.FooterY,bw,48);Place(refresh,l.ContentX+2*(bw+8),l.FooterY,bw,48);
            foreach(CodexButton button in rowButtons)button.Size=new Vector2(l.ContentWidth,64);
        }

        private void Row(string name, bool isDirectory)
        {
            if (rowNames.Count >= 80) return;
            var button = new CodexButton((isDirectory?"目录 · ":"图书 · ")+(name.Length>45?name.Substring(0,44)+"…":name),true)
            {
                Theme=theme,CompactRow=true,Size=new Vector2(400,64),Margin=new Vector2(0,6)
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
            bool started = BookFilePicker.TryChoose(choiceCallback);
            if (started) status.Text = "请选择 TXT 或 EPUB";
            else
            {
                nativeChoosing = false;
                status.Text = BookFilePicker.LastError??"系统选书不可用";
            }
        }

        private void ReceiveChoice(string path)
        {
            // Native result callbacks never change widgets directly.
            lock(choiceGate)
            {
                if(closed){BookFilePicker.Release(path);return;}
                pendingPath=path;pendingChoice=true;
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
                if (string.IsNullOrEmpty(chosen)) status.Text = BookFilePicker.LastError??"已取消选择";
                else BeginImport(chosen);
            }
            Task<string> task = importTask;
            if (task == null || !task.IsCompleted) return;
            importTask = null; importing = false;
            if (task.IsFaulted)
            {
                Exception failure = task.Exception;
                for(int i=0;failure.InnerException!=null&&i<8;i++)failure=failure.InnerException;
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
            BookFilePicker.Cancel(choiceCallback);
            lock(choiceGate){BookFilePicker.Release(pendingPath);pendingPath=null;pendingChoice=false;}
            if (ParentWidget != null) DialogsManager.HideDialog(this);
            if (success && imported != null) imported();
        }

        public override void UpdateCeases() { if(!nativeChoosing) Close(false); base.UpdateCeases(); }
        public override void Dispose() { Close(false); base.Dispose(); }

        public override void Update()
        {
            if (closed) return;
            try
            {
                if (player == null || player.GameWidget == null || player.GameWidget.GuiWidget == null)
                { Close(false); return; }
                Layout(player.GameWidget.GuiWidget.ActualSize);
                ProcessPending();
                if (closed) return;
                if(launchRequested){launchRequested=false;BeginSystemPicker();}
                bool ready = !importing && !nativeChoosing;
                system.IsEnabled = ready;
                up.IsEnabled = ready && directory != BookFilePicker.Root;
                refresh.IsEnabled = ready;
                foreach(CodexButton b in rowButtons)b.IsEnabled=ready;

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

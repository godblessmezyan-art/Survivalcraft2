using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using CelestialCodex.Core;
using CelestialCodex.Mod;
using CelestialCodex.Networking;
using Engine;
using Engine.Graphics;
using Engine.Media;
using Game;

namespace CelestialCodex.UI
{
    internal sealed class CodexDialog : Dialog
    {
        private enum View { Catalog, Chapters, Reading }
        internal ComponentPlayer Player { get; private set; }
        internal bool Closed { get; private set; }
        private readonly ReaderSession session;
        private readonly CanvasWidget panel, viewport;
        private readonly ScrollPanelWidget scroll;
        private readonly StackPanelWidget entries;
        private readonly LabelWidget title, status, text;
        private readonly BevelledButtonWidget back, close, previous, middle, next, smaller, larger;
        private readonly List<BevelledButtonWidget> rowButtons = new List<BevelledButtonWidget>();
        private readonly List<string> rowIds = new List<string>();
        private readonly List<string> rowLabels = new List<string>();
        private string heading = "云天书典";
        private View view;
        private Document catalog;
        private Book book;
        private Chapter chapter;
        private ReadingPosition saved = new ReadingPosition();
        private int page = 1, chapterPage, font = 2;
        private int restoreScroll = -1, restoreFrames;
        private bool busy, resume, local;
        private double lastSave;

        internal CodexDialog(ComponentPlayer player)
        {
            Player = player; session = new ReaderSession();
            panel = new CanvasWidget { HorizontalAlignment = WidgetAlignment.Center, VerticalAlignment = WidgetAlignment.Center }; Children.Add(panel);
            panel.Children.Add(new RectangleWidget { FillColor = new Color(239, 222, 181), OutlineColor = new Color(112, 78, 39), OutlineThickness = 3 });
            title = new LabelWidget { Color = new Color(63, 41, 24), FontScale = .75f, TextAnchor = TextAnchor.HorizontalCenter | TextAnchor.VerticalCenter };
            status = new LabelWidget { Color = new Color(99, 65, 34), FontScale = .6f };
            back = Button("返回"); close = Button("关闭"); previous = Button("上一页"); middle = Button("刷新"); next = Button("下一页"); smaller = Button("A−"); larger = Button("A+");
            panel.Children.Add(title); panel.Children.Add(status);
            viewport = new CanvasWidget(); panel.Children.Add(viewport);
            scroll = new ScrollPanelWidget { Direction = LayoutDirection.Vertical, ScrollPosition = 0, ScrollSpeed = 0 }; viewport.Children.Add(scroll);
            entries = new StackPanelWidget { Direction = LayoutDirection.Vertical }; scroll.Children.Add(entries);
            text = new LabelWidget { WordWrap = true, Color = new Color(53, 36, 24), FontScale = 1f, HorizontalAlignment = WidgetAlignment.Stretch, VerticalAlignment = WidgetAlignment.Near, Size = new Vector2(-1, -1) };
            Load(new Request(1));
        }
        private BevelledButtonWidget Button(string label)
        {
            var b = new BevelledButtonWidget { Text = label, FontScale = .65f, Color = new Color(62, 41, 23), BevelColor = new Color(183, 151, 102), CenterColor = new Color(222, 200, 156), Size = new Vector2(80, 46) }; panel.Children.Add(b); return b;
        }
        private void Place(Widget widget, float x, float y, float width, float height)
        {
            var b = widget as BevelledButtonWidget; if (b != null) b.Size = new Vector2(width, height);
            var l = widget as LabelWidget; if (l != null) l.Size = new Vector2(width, height);
            panel.SetWidgetPosition(widget, new Vector2(x, y));
        }
        private void Layout()
        {
            Vector2 available = Player.GameWidget.GuiWidget.ActualSize;
            var layout = new ReaderLayout(available.X, available.Y); float w = layout.Width, h = layout.Height;
            panel.Size = new Vector2(w, h);
            Place(back, 12, 10, 70, 46); Place(close, w - 82, 10, 70, 46);
            Place(title, 86, 14, MathUtils.Max(1, w - 172), 38);
            title.Text = Fit(title.Font, heading, MathUtils.Max(1, w - 172), title.FontScale);
            Place(status, 18, 72, MathUtils.Max(1, w - 162), 24);
            Place(smaller, w - 134, 62, 54, 44); Place(larger, w - 74, 62, 54, 44);
            viewport.Size = new Vector2(w - 36, layout.ContentHeight); panel.SetWidgetPosition(viewport, new Vector2(18, 110));
            Place(previous, 12, h - 50, layout.ButtonWidth, 44); Place(middle, 20 + layout.ButtonWidth, h - 50, layout.ButtonWidth, 44); Place(next, 28 + 2 * layout.ButtonWidth, h - 50, layout.ButtonWidth, 44);
            for (int i = 0; i < rowButtons.Count; i++) { var b = rowButtons[i]; b.Size = new Vector2(w - 40, 54); b.Text = Fit(b.Font, rowLabels[i], w - 64, b.FontScale); }
            text.FontScale = .75f + .13f * font;
        }
        private static string Fit(BitmapFont font, string value, float width, float scale)
        {
            if (font.MeasureText(value, new Vector2(scale), Vector2.Zero).X <= width) return value;
            int length = font.FitText(MathUtils.Max(1, width - font.MeasureText("…", new Vector2(scale), Vector2.Zero).X), value, 0, value.Length, scale, 0);
            if (length > 0 && char.IsHighSurrogate(value[length - 1])) length--;
            return value.Substring(0, length) + "…";
        }
        private void Clear()
        {
            entries.Children.Clear(); rowButtons.Clear(); rowIds.Clear(); rowLabels.Clear(); scroll.ScrollPosition = 0; scroll.ScrollSpeed = 0;
            restoreScroll = -1;
        }
        private void Row(string label, string id)
        {
            var b = new BevelledButtonWidget { Text = label, FontScale = .68f, Color = new Color(62, 41, 23), BevelColor = new Color(183, 151, 102), CenterColor = new Color(222, 200, 156), Size = new Vector2(400, 54), Margin = new Vector2(0, 3) };
            entries.Children.Add(b); rowButtons.Add(b); rowIds.Add(id); rowLabels.Add(label);
        }
        private void Load(Request request)
        {
            busy = true; status.Text = "载入中…"; session.Begin(request);
        }
        private void ToggleSource()
        {
            session.Cancel(); local = !local; session.SetLocal(local);
            catalog = null; book = null; chapter = null; saved = new ReadingPosition();
            page = 1; chapterPage = 0; view = View.Catalog;
            Load(new Request(1));
        }
        private void ShowCatalog()
        {
            view = View.Catalog; Clear(); heading = local ? "本地书库" : "云端书库";
            if (catalog == null) return;
            foreach (Book item in catalog.Books)
            {
                string mode = item.SourceMode == "ai_reading" ? "AI解读 · " : "";
                string state = item.Status == "unsupported" ? " · 不支持" : item.Status == "unavailable" ? " · 暂无正文" : "";
                Row(mode + item.Title + state, item.Slug);
            }
            if (catalog.Books.Count == 0) status.Text = local ? "导入目录暂无TXT/EPUB" : "书库为空";
        }
        private void ShowChapters()
        {
            view = View.Chapters; Clear(); heading = book.Title;
            for (int i = chapterPage * 24; i < book.Chapters.Count && i < (chapterPage + 1) * 24; i++) Row(book.Chapters[i].Title, book.Chapters[i].Id);
            if (book.Chapters.Count == 0) status.Text = book.Status == "unsupported" ? "暂不支持此格式" : book.Status == "empty" ? "正文为空" : "暂无正文";
        }
        private void ShowReading()
        {
            view = View.Reading; Clear(); heading = book.Title; text.Text = chapter.Title + "\n\n" + chapter.Text; entries.Children.Add(text);
        }
        private int ChapterIndex() { return book == null || chapter == null ? -1 : ChapterNavigation.IndexOf(book.Chapters, chapter.Id); }
        private void OpenBook(string slug, bool continueReading)
        {
            resume = continueReading; Load(new Request(slug));
        }
        private void OpenChapter(string id) { SavePosition(); Load(new Request(book.Slug, id, book.Revision)); }
        private void Apply(ReaderResult result)
        {
            busy = false;
            if (view == View.Catalog && result.Position != null) { saved = result.Position; font = saved.Font; }
            if (result.Document == null)
            {
                status.Text = result.Error ?? "书库暂不可用";
                if ((result.HttpStatus == 401 || result.HttpStatus == 403 || result.HttpStatus == 404) && view != View.Catalog && book != null) { book.Chapters.Clear(); book.Status = "unavailable"; ShowChapters(); }
                if (result.HttpStatus == 409 && book != null) { SavePosition(); Load(new Request(book.Slug)); }
                return;
            }
            status.Text = result.Offline ? "离线" : result.CacheFailed ? "缓存不可用" : "";
            Request request = result.Request;
            if (request.Slug == null)
            {
                catalog = result.Document; page = catalog.Page; saved = result.Position ?? saved; font = saved.Font; ShowCatalog();
            }
            else if (request.Chapter == null)
            {
                book = result.Document.Book; chapterPage = 0; ShowChapters();
                if (book.SourceMode == "ai_reading" && book.Status == "ready") status.Text = (result.Offline ? "离线 · " : "") + "AI解读";
                if (resume)
                {
                    resume = false;
                    if (saved.Slug == book.Slug && saved.Revision == book.Revision && ChapterNavigation.IndexOf(book.Chapters, saved.Chapter) >= 0) { Load(new Request(book.Slug, saved.Chapter, book.Revision)); return; }
                    if (saved.Slug == book.Slug && saved.Revision != book.Revision) status.Text = "内容已更新";
                }
            }
            else
            {
                chapter = result.Document.Chapter; ShowReading();
                if (book.SourceMode == "ai_reading") status.Text = (result.Offline ? "离线 · " : "") + "AI解读";
                if (saved.Slug == book.Slug && saved.Revision == book.Revision && saved.Chapter == chapter.Id) { restoreScroll = saved.Scroll; restoreFrames = 2; }
            }
        }
        private void SavePosition()
        {
            if (view != View.Reading || book == null || chapter == null || restoreScroll >= 0) return;
            float range = MathUtils.Max(0, scroll.CalculateScrollAreaLength() - scroll.ActualSize.Y);
            int fraction = range > 0 ? (int)(MathUtils.Clamp(scroll.ScrollPosition / range, 0, 1) * 1000000) : 0;
            saved = new ReadingPosition { Slug = book.Slug, Revision = book.Revision, Chapter = chapter.Id, Font = font, Scroll = fraction };
            session.Save(saved); lastSave = Time.RealTime;
        }
        private void GoBack()
        {
            session.Cancel(); busy = false; SavePosition();
            status.Text = session.IsOffline ? "离线" : "";
            if (view == View.Reading) { chapterPage = MathUtils.Max(0, ChapterIndex()) / 24; ShowChapters(); }
            else if (view == View.Chapters) ShowCatalog(); else Close();
        }
        internal void Close()
        {
            if (Closed) return; SavePosition(); Closed = true; session.Dispose(); if (ParentWidget != null) DialogsManager.HideDialog(this); Player = null;
        }
        public override void UpdateCeases() { Close(); base.UpdateCeases(); }
        public override void Dispose() { Close(); base.Dispose(); }
        public override void Update()
        {
            if (Closed) return;
            try { UpdateReader(); }
            catch (Exception error) { FailReader(error); }
        }
        // Keep JIT/member-resolution failures inside the caller's exception boundary.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void UpdateReader()
        {
            if (Player == null || Player.GameWidget == null || Player.GameWidget.GuiWidget == null) { Close(); return; }
            Layout(); ReaderResult result = session.Take(); if (result != null) Apply(result);
            if (restoreScroll >= 0 && --restoreFrames <= 0)
            {
                scroll.ScrollPosition = MathUtils.Max(0, scroll.CalculateScrollAreaLength() - scroll.ActualSize.Y) * restoreScroll / 1000000f; scroll.ScrollSpeed = 0; restoreScroll = -1;
            }
            if (view == View.Reading && Time.RealTime - lastSave > 5) SavePosition();
            bool reading = view == View.Reading;
            smaller.IsVisible = true; larger.IsVisible = reading;
            smaller.Text = reading ? "A−" : view == View.Catalog ? (local ? "云端" : "本地") : "刷新";
            smaller.IsEnabled = !busy && (!reading || font > 0); larger.IsEnabled = !busy && font < 4;
            if (reading && (smaller.IsClicked || larger.IsClicked)) { SavePosition(); font = MathUtils.Clamp(font + (larger.IsClicked ? 1 : -1), 0, 4); saved.Font = font; session.Save(saved); restoreScroll = saved.Scroll; restoreFrames = 2; }
            else if (!reading && smaller.IsClicked) { if (view == View.Catalog) ToggleSource(); else { session.GoOnline(); Load(new Request(book.Slug)); } }
            int index = ChapterIndex();
            previous.Text = reading ? "上节" : "上一页"; next.Text = reading ? "下节" : "下一页";
            previous.IsEnabled = !busy && (reading ? index > 0 : view == View.Catalog ? page > 1 : chapterPage > 0);
            next.IsEnabled = !busy && (reading ? book != null && index + 1 < book.Chapters.Count : view == View.Catalog ? catalog != null && catalog.HasMore : (chapterPage + 1) * 24 < book.Chapters.Count);
            middle.Text = busy ? "载入中" : reading ? "章节" : view == View.Catalog && Protocol.SafeSlug(saved.Slug) ? "继续" : local && view == View.Catalog ? "扫描" : "刷新"; middle.IsEnabled = !busy;
            foreach (var b in rowButtons) b.IsEnabled = !busy;
            if (!busy)
            {
                for (int i = 0; i < rowButtons.Count; i++) if (rowButtons[i].IsClicked) { if (view == View.Catalog) OpenBook(rowIds[i], false); else OpenChapter(rowIds[i]); break; }
                if (previous.IsClicked) { if (reading) OpenChapter(book.Chapters[index - 1].Id); else if (view == View.Catalog) Load(new Request(page - 1)); else { chapterPage--; ShowChapters(); } }
                if (next.IsClicked) { if (reading) OpenChapter(book.Chapters[index + 1].Id); else if (view == View.Catalog) Load(new Request(page + 1)); else { chapterPage++; ShowChapters(); } }
                if (middle.IsClicked) { if (reading) GoBack(); else if (view == View.Catalog && Protocol.SafeSlug(saved.Slug)) OpenBook(saved.Slug, true); else Load(view == View.Catalog ? new Request(page) : new Request(book.Slug)); }
            }
            if (close.IsClicked) Close(); else if (back.IsClicked || Input.Back || Input.Cancel) { Input.Clear(); GoBack(); }
        }
        private void FailReader(Exception error)
        {
            ComponentPlayer player = Player; Closed = true; Player = null;
            // A broken reader must not escape into Widget.UpdateWidgetsHierarchy.
            try { if (session != null) session.Dispose(); } catch (Exception) { }
            try { if (ParentWidget != null) DialogsManager.HideDialog(this); } catch (Exception) { }
            try { Log.Warning("[CelestialCodex] Reader update failed: " + error); } catch (Exception) { }
            try { if (player != null && player.ComponentGui != null) player.ComponentGui.DisplaySmallMessage("书典暂不可用", Color.White, false, false); } catch (Exception) { }
        }
    }
}

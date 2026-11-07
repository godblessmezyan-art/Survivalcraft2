using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using CelestialCodex.Core;
using CelestialCodex.Mod;
using CelestialCodex.Networking;
using Engine;
using Engine.Graphics;
using Game;

namespace CelestialCodex.UI
{
    internal sealed class CodexDialog : Dialog
    {
        private enum View { Catalog, Chapters, Reading, Settings }
        private static readonly string[] FontNames = { "较小", "小", "标准", "大", "较大" };
        internal ComponentPlayer Player { get; private set; }
        internal bool Closed { get; private set; }
        private readonly ReaderSession session;
        private readonly CanvasWidget panel, viewport, settingsPanel;
        private readonly RectangleWidget paper, rule;
        private readonly ScrollPanelWidget scroll;
        private readonly StackPanelWidget entries;
        private readonly CodexLabel title, status, text, chapterTitle, folio, fontName;
        private readonly CodexButton back, close, previous, middle, action, next, smaller, larger, light, dark;
        private readonly List<CodexButton> rows = new List<CodexButton>();
        private readonly List<string> rowIds = new List<string>();
        private string heading = "云天书典", feedback = "";
        private View view;
        private Document catalog;
        private Book book;
        private Chapter chapter;
        private ReadingPosition saved = new ReadingPosition();
        private int page = 1, chapterPage, font = 2, theme;
        private int restoreScroll = -1, restoreFrames;
        private bool busy, resume;
        private double lastSave;

        internal CodexDialog(ComponentPlayer player) : this(player, true) { }
        // Deferred loading allows the same native widget tree to be measured without network/file work.
        internal CodexDialog(ComponentPlayer player, bool loadInitially)
        {
            Player = player; session = new ReaderSession();
            panel = new CanvasWidget { HorizontalAlignment = WidgetAlignment.Center, VerticalAlignment = WidgetAlignment.Center }; Children.Add(panel);
            paper = new RectangleWidget { OutlineThickness = 2, IsHitTestVisible = false }; panel.Children.Add(paper);
            rule = new RectangleWidget { OutlineThickness = 0, IsHitTestVisible = false }; panel.Children.Add(rule);
            title = Label(.82f); title.TextAnchor = TextAnchor.HorizontalCenter | TextAnchor.VerticalCenter;
            status = Label(.56f); status.TextAnchor = TextAnchor.VerticalCenter;
            folio = Label(.58f); folio.TextAnchor = TextAnchor.HorizontalCenter | TextAnchor.VerticalCenter;
            panel.Children.Add(title); panel.Children.Add(status); panel.Children.Add(folio);
            back = Button("返回"); close = Button("关闭"); previous = Button("上页"); middle = Button("目录"); action = Button("刷新"); next = Button("下页");
            viewport = new CanvasWidget(); panel.Children.Add(viewport);
            scroll = new ScrollPanelWidget { Direction = LayoutDirection.Vertical, ScrollPosition = 0, ScrollSpeed = 0 }; viewport.Children.Add(scroll);
            entries = new StackPanelWidget { Direction = LayoutDirection.Vertical }; scroll.Children.Add(entries);
            chapterTitle = Label(.96f); chapterTitle.WordWrap = true; chapterTitle.MaxLines = int.MaxValue; chapterTitle.Ellipsis = false; chapterTitle.Margin = new Vector2(0, 12);
            text = Label(1f); text.WordWrap = true; text.MaxLines = int.MaxValue; text.Ellipsis = false; text.Size = new Vector2(-1, -1); text.VerticalAlignment = WidgetAlignment.Near;
            settingsPanel = new CanvasWidget { Size = new Vector2(300, 200) };
            fontName = Label(.7f); fontName.TextAnchor = TextAnchor.HorizontalCenter | TextAnchor.VerticalCenter; settingsPanel.Children.Add(fontName);
            smaller = new CodexButton("A−"); larger = new CodexButton("A+"); light = new CodexButton("纸页"); dark = new CodexButton("夜读");
            settingsPanel.Children.Add(smaller); settingsPanel.Children.Add(larger); settingsPanel.Children.Add(light); settingsPanel.Children.Add(dark);
            if (loadInitially) Load(new Request(1));
        }
        private static CodexLabel Label(float scale)
        { return new CodexLabel { FontScale = scale, MaxLines = 1, Ellipsis = true, HorizontalAlignment = WidgetAlignment.Stretch }; }
        private CodexButton Button(string label) { var b = new CodexButton(label); panel.Children.Add(b); return b; }
        private static void Place(CanvasWidget parent, Widget widget, float x, float y, float width, float height)
        {
            var canvas = widget as CanvasWidget; if (canvas != null) canvas.Size = new Vector2(width, height);
            var label = widget as LabelWidget; if (label != null) label.Size = new Vector2(width, height);
            var rectangle = widget as RectangleWidget; if (rectangle != null) rectangle.Size = new Vector2(width, height);
            parent.SetWidgetPosition(widget, new Vector2(x, y));
        }
        private void Layout(Vector2 available)
        {
            var layout = new ReaderLayout(available.X, available.Y); float w = layout.Width, h = layout.Height, x = layout.ContentX, bw = layout.ButtonWidth;
            panel.Size = new Vector2(w, h);
            Place(panel, back, 12, 8, 64, 48); Place(panel, close, w - 76, 8, 64, 48);
            Place(panel, title, 84, 8, MathUtils.Max(1, w - 168), 48); title.Text = heading;
            Place(panel, status, x, layout.Compact ? 56 : 64, layout.ContentWidth, layout.Compact ? 16 : 24);
            Place(panel, rule, x, layout.ContentY - 8, layout.ContentWidth, 1);
            viewport.Size = new Vector2(layout.ContentWidth, layout.ContentHeight); panel.SetWidgetPosition(viewport, new Vector2(x, layout.ContentY));
            Place(panel, previous, x, layout.FooterY, bw, 48); Place(panel, middle, x + bw + 8, layout.FooterY, bw, 48);
            Place(panel, folio, x + bw + 8, layout.FooterY, bw, 48); Place(panel, action, x + 2 * (bw + 8), layout.FooterY, bw, 48); Place(panel, next, x + 3 * (bw + 8), layout.FooterY, bw, 48);
            if (view == View.Settings) Place(panel, middle, x + (layout.ContentWidth - 144) / 2, layout.FooterY, 144, 48);
            foreach (CodexButton row in rows) row.Size = new Vector2(layout.ContentWidth, 92);
            settingsPanel.Size = new Vector2(layout.ContentWidth, 200);
            Place(settingsPanel, smaller, 0, 20, 64, 48); Place(settingsPanel, larger, layout.ContentWidth - 64, 20, 64, 48);
            Place(settingsPanel, fontName, 72, 20, MathUtils.Max(1, layout.ContentWidth - 144), 48);
            Place(settingsPanel, light, 0, 92, (layout.ContentWidth - 8) / 2, 48); Place(settingsPanel, dark, (layout.ContentWidth + 8) / 2, 92, (layout.ContentWidth - 8) / 2, 48);
            text.FontScale = .79f + .13f * font; text.FontSpacing = new Vector2(0, 8);
            ApplyTheme();
        }
        private void ApplyTheme()
        {
            ReaderTheme colors = theme == 0 ? ReaderTheme.Paper : ReaderTheme.Night;
            paper.FillColor = CodexButton.Rgb(colors.Background); paper.OutlineColor = CodexButton.Rgb(colors.Border); rule.FillColor = CodexButton.Rgb(colors.Border);
            title.Color = text.Color = chapterTitle.Color = CodexButton.Rgb(colors.Ink);
            status.Color = folio.Color = fontName.Color = CodexButton.Rgb(colors.Muted);
            back.Theme = close.Theme = previous.Theme = middle.Theme = action.Theme = next.Theme = smaller.Theme = larger.Theme = light.Theme = dark.Theme = colors;
            foreach (CodexButton row in rows) row.Theme = colors;
            light.IsChecked = theme == 0; dark.IsChecked = theme == 1;
        }
        private void Clear()
        {
            entries.Children.Clear(); rows.Clear(); rowIds.Clear(); scroll.ScrollPosition = 0; scroll.ScrollSpeed = 0; restoreScroll = -1;
        }
        private void Row(string caption, string detail, string id, bool prominent = false, bool selected = false)
        {
            var row = new CodexButton(caption, true) { Margin = new Vector2(0, 4), Prominent = prominent, IsChecked = selected };
            row.Detail.Text = detail; entries.Children.Add(row); rows.Add(row); rowIds.Add(id);
        }
        private void Empty(string message)
        { var label = Label(.72f); label.Text = message; label.Size = new Vector2(-1, 100); label.TextAnchor = TextAnchor.HorizontalCenter | TextAnchor.VerticalCenter; label.Color = CodexButton.Rgb((theme == 0 ? ReaderTheme.Paper : ReaderTheme.Night).Muted); entries.Children.Add(label); }
        private void Load(Request request) { busy = true; feedback = ""; session.Begin(request); }
        private void ShowCatalog()
        {
            view = View.Catalog; Clear(); heading = "云天书典";
            if (Protocol.SafeSlug(saved.Slug))
            {
                string name = "上次阅读的位置";
                if (catalog != null) foreach (Book item in catalog.Books) if (item.Slug == saved.Slug) { name = item.Title; break; }
                Row("继续阅读", name, "@resume", true);
            }
            if (catalog == null) return;
            foreach (Book item in catalog.Books)
            {
                string detail = item.SourceMode == "ai_reading" ? "AI解读" : "";
                if (item.Status == "unsupported") detail = Join(detail, "暂不支持");
                else if (item.Status == "unavailable") detail = Join(detail, "暂无正文");
                detail = Join(detail, item.Author);
                Row(item.Title, detail, item.Slug);
            }
            if (catalog.Books.Count == 0) Empty("书库为空");
        }
        private void ShowChapters()
        {
            view = View.Chapters; Clear(); heading = book.Title;
            for (int i = chapterPage * 24; i < book.Chapters.Count && i < (chapterPage + 1) * 24; i++)
            {
                bool selected = chapter != null && chapter.Id == book.Chapters[i].Id;
                Row(book.Chapters[i].Title, selected ? "正在阅读" : "第 " + (i + 1) + " 节", book.Chapters[i].Id, false, selected);
            }
            if (book.Chapters.Count == 0) Empty(book.Status == "unsupported" ? "暂不支持此格式" : book.Status == "empty" ? "正文为空" : "暂无正文");
        }
        private void ShowReading()
        {
            view = View.Reading; Clear(); heading = book.Title; chapterTitle.Text = chapter.Title; text.Text = chapter.Text;
            entries.Children.Add(chapterTitle); entries.Children.Add(text);
        }
        private void ShowSettings()
        { SavePosition(); view = View.Settings; Clear(); heading = "阅读设置"; entries.Children.Add(settingsPanel); }
        private int ChapterIndex() { return book == null || chapter == null ? -1 : ChapterNavigation.IndexOf(book.Chapters, chapter.Id); }
        private void OpenBook(string slug, bool continueReading) { resume = continueReading; Load(new Request(slug)); }
        private void OpenChapter(string id) { SavePosition(); Load(new Request(book.Slug, id, book.Revision)); }
        private void RestorePosition() { restoreScroll = saved.Scroll; restoreFrames = 2; }
        private void RestoreAfterLayout()
        {
            if (restoreScroll < 0 || --restoreFrames > 0) return;
            scroll.ScrollPosition = MathUtils.Max(0, scroll.CalculateScrollAreaLength() - scroll.ActualSize.Y) * restoreScroll / 1000000f;
            scroll.ScrollSpeed = 0; restoreScroll = -1;
        }
        private void Apply(ReaderResult result)
        {
            busy = false;
            if (view == View.Catalog && result.Position != null) { saved = result.Position; font = saved.Font; theme = saved.Theme; }
            if (result.Document == null)
            {
                feedback = result.Error ?? "书库暂不可用";
                if ((result.HttpStatus == 401 || result.HttpStatus == 403 || result.HttpStatus == 404) && view != View.Catalog && book != null) { book.Chapters.Clear(); book.Status = "unavailable"; ShowChapters(); }
                if (result.HttpStatus == 409 && book != null) { SavePosition(); resume = false; Load(new Request(book.Slug)); }
                return;
            }
            feedback = result.CacheFailed ? "缓存不可用" : "";
            Request request = result.Request;
            if (request.Slug == null)
            {
                catalog = result.Document; page = catalog.Page; saved = result.Position ?? saved; font = saved.Font; theme = saved.Theme; ShowCatalog();
            }
            else if (request.Chapter == null)
            {
                book = result.Document.Book; chapter = null; chapterPage = 0; ShowChapters();
                if (resume)
                {
                    resume = false;
                    if (saved.Slug == book.Slug && saved.Revision == book.Revision && ChapterNavigation.IndexOf(book.Chapters, saved.Chapter) >= 0) { Load(new Request(book.Slug, saved.Chapter, book.Revision)); return; }
                    if (saved.Slug == book.Slug && saved.Revision != book.Revision) feedback = "内容已更新";
                }
            }
            else
            {
                chapter = result.Document.Chapter; ShowReading();
                if (saved.Slug == book.Slug && saved.Revision == book.Revision && saved.Chapter == chapter.Id) RestorePosition();
            }
        }
        private void SavePosition()
        {
            if (view == View.Settings) { saved.Font = font; saved.Theme = theme; session.Save(saved); return; }
            if (view != View.Reading || book == null || chapter == null || restoreScroll >= 0) return;
            float range = MathUtils.Max(0, scroll.CalculateScrollAreaLength() - scroll.ActualSize.Y);
            int fraction = range > 0 ? (int)(MathUtils.Clamp(scroll.ScrollPosition / range, 0, 1) * 1000000) : 0;
            saved = new ReadingPosition { Slug = book.Slug, Revision = book.Revision, Chapter = chapter.Id, Font = font, Theme = theme, Scroll = fraction };
            session.Save(saved); lastSave = Time.RealTime;
        }
        private void GoBack()
        {
            session.Cancel(); busy = false; SavePosition(); feedback = "";
            if (view == View.Settings) { ShowReading(); RestorePosition(); }
            else if (view == View.Reading) { chapterPage = MathUtils.Max(0, ChapterIndex()) / 24; ShowChapters(); }
            else if (view == View.Chapters) ShowCatalog(); else Close();
        }
        internal void Close()
        { if (Closed) return; SavePosition(); Closed = true; session.Dispose(); if (ParentWidget != null) DialogsManager.HideDialog(this); Player = null; }
        public override void UpdateCeases() { Close(); base.UpdateCeases(); }
        public override void Dispose() { Close(); base.Dispose(); }
        public override void Update()
        {
            if (Closed) return;
            try { UpdateReader(); } catch (Exception error) { FailReader(error); }
        }
        private static string Join(string left, string right) { return string.IsNullOrEmpty(left) ? right : string.IsNullOrEmpty(right) ? left : left + " · " + right; }
        private void Chrome()
        {
            bool reading = view == View.Reading, settings = view == View.Settings;
            back.IsVisible = view != View.Catalog;
            previous.IsVisible = next.IsVisible = action.IsVisible = !settings;
            middle.IsVisible = reading || settings; folio.IsVisible = !reading && !settings;
            middle.Text = settings ? "完成" : "目录"; action.Text = reading ? "设置" : "刷新";
            previous.Text = reading ? "上节" : "上页"; next.Text = reading ? "下节" : "下页";
            int index = ChapterIndex();
            previous.IsEnabled = !busy && (reading ? index > 0 : view == View.Catalog ? page > 1 : chapterPage > 0);
            next.IsEnabled = !busy && (reading ? book != null && index + 1 < book.Chapters.Count : view == View.Catalog ? catalog != null && catalog.HasMore : book != null && (chapterPage + 1) * 24 < book.Chapters.Count);
            middle.IsEnabled = action.IsEnabled = !busy;
            foreach (CodexButton row in rows) row.IsEnabled = !busy;
            smaller.IsEnabled = font > 0; larger.IsEnabled = font < 4;
            fontName.Text = "字号 · " + FontNames[font];
            light.Text = theme == 0 ? "纸页 · 当前" : "纸页"; dark.Text = theme == 1 ? "夜读 · 当前" : "夜读";
            folio.Text = view == View.Catalog ? "第 " + page + " 页" : (chapterPage + 1) + " / " + MathUtils.Max(1, (book == null ? 0 : book.Chapters.Count + 23) / 24);
            string source = book != null && view != View.Catalog && !settings && book.SourceMode == "ai_reading" ? "AI解读" : "";
            string meta = source;
            if (reading) meta = Join(meta, (index + 1) + " / " + book.Chapters.Count);
            if (session.IsOffline) meta = Join(meta, "离线");
            if (book != null && view != View.Catalog && !settings) meta = Join(meta, book.Author);
            status.Text = busy ? Join(source, "载入中…") : string.IsNullOrEmpty(feedback) ? meta : Join(source, feedback);
        }
        // Keep JIT/member-resolution failures inside the native Update caller's boundary.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void UpdateReader()
        {
            if (Player == null || Player.GameWidget == null || Player.GameWidget.GuiWidget == null) { Close(); return; }
            ReaderResult result = session.Take(); if (result != null) Apply(result);
            Chrome(); Layout(Player.GameWidget.GuiWidget.ActualSize);
            RestoreAfterLayout();
            if (view == View.Reading && Time.RealTime - lastSave > 5) SavePosition();
            if (close.IsClicked) { Close(); return; }
            if (back.IsClicked || Input.Back || Input.Cancel) { Input.Clear(); GoBack(); return; }
            if (busy) return;
            if (view == View.Settings)
            {
                if (smaller.IsClicked || larger.IsClicked) font = MathUtils.Clamp(font + (larger.IsClicked ? 1 : -1), 0, 4);
                if (light.IsClicked || dark.IsClicked) theme = dark.IsClicked ? 1 : 0;
                if (smaller.IsClicked || larger.IsClicked || light.IsClicked || dark.IsClicked) SavePosition();
                if (middle.IsClicked) GoBack();
                return;
            }
            for (int i = 0; i < rows.Count; i++) if (rows[i].IsClicked)
            { if (view == View.Catalog) { if (rowIds[i] == "@resume") OpenBook(saved.Slug, true); else OpenBook(rowIds[i], false); } else OpenChapter(rowIds[i]); return; }
            bool reading = view == View.Reading; int index = ChapterIndex();
            if (previous.IsClicked) { if (reading) OpenChapter(book.Chapters[index - 1].Id); else if (view == View.Catalog) Load(new Request(page - 1)); else { chapterPage--; ShowChapters(); } }
            else if (next.IsClicked) { if (reading) OpenChapter(book.Chapters[index + 1].Id); else if (view == View.Catalog) Load(new Request(page + 1)); else { chapterPage++; ShowChapters(); } }
            else if (middle.IsClicked && reading) GoBack();
            else if (action.IsClicked) { if (reading) ShowSettings(); else { session.GoOnline(); Load(view == View.Catalog ? new Request(page) : new Request(book.Slug)); } }
        }
        private void FailReader(Exception error)
        {
            ComponentPlayer player = Player; Closed = true; Player = null;
            try { if (session != null) session.Dispose(); } catch (Exception) { }
            try { if (ParentWidget != null) DialogsManager.HideDialog(this); } catch (Exception) { }
            try { Log.Warning("[CelestialCodex] Reader update failed: " + error); } catch (Exception) { }
            try { if (player != null && player.ComponentGui != null) player.ComponentGui.DisplaySmallMessage("书典暂不可用", Color.White, false, false); } catch (Exception) { }
        }
    }
}

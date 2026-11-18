using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using SCIEPort.Features.CelestialCodex.Mod;
using Engine;
using Engine.Graphics;
using Engine.Media;
using Game;
using ZipArchive = System.IO.Compression.ZipArchive;

// Uses the real target widgets, font metrics, language data and layout/hit-test implementation.
// No game world, HTTP request or user-data write is started by these deferred-load fixtures.
internal static class NativeUiChecks
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static Assembly mod;
    static object Field(object obj, string name) { return obj.GetType().GetField(name, F).GetValue(obj); }
    static void Set(object obj, string name, object value) { obj.GetType().GetField(name, F).SetValue(obj, value); }
    static object Call(object obj, string name, params object[] args) { return obj.GetType().GetMethod(name, F).Invoke(obj, args); }
    static object Create(string name) { return Activator.CreateInstance(mod.GetType("SCIEPort.Features.CelestialCodex.Core." + name), true); }
    static Dialog ImportFixture(int theme)
    {
        Type type=mod.GetType("SCIEPort.Features.CelestialCodex.UI.BookImportDialog");
        object colors=mod.GetType("SCIEPort.Features.CelestialCodex.Core.ReaderTheme").GetField(theme==0?"Paper":"Night",F).GetValue(null);
        var dialog=(Dialog)Activator.CreateInstance(type,F,null,new object[]{null,null,null,colors,false},null);
        Call(dialog,"Row","Download",true);Call(dialog,"Row","山野随笔.epub",false);Call(dialog,"Row","致漫游者的信.txt",false);
        return dialog;
    }
    static void CacheFont(BitmapFont font)
    { ((IDictionary)typeof(ContentManager).GetField("Caches", F).GetValue(null))["Fonts/Pericles"] = new List<object> { font }; }
    static BitmapFont Metrics(ZipArchive zip)
    {
        using (var reader = new StreamReader(zip.GetEntry("Assets/Fonts/Pericles.lst").Open()))
        {
            int n = int.Parse(reader.ReadLine(), CultureInfo.InvariantCulture); var glyphs = new List<BitmapFont.Glyph>();
            for (int i = 0; i < n; i++)
            {
                string[] p = reader.ReadLine().Split(new[] { ' ', '\t' });
                if (p.Length == 9) p = new[] { " " }.Concat(p.Skip(2)).ToArray();
                Func<int, float> number = x => float.Parse(p[x], CultureInfo.InvariantCulture);
                glyphs.Add(new BitmapFont.Glyph(p[0][0], new Vector2(number(1), number(2)), new Vector2(number(3), number(4)), new Vector2(number(5), number(6)), number(7)));
            }
            float height = float.Parse(reader.ReadLine(), CultureInfo.InvariantCulture); string[] spacing = reader.ReadLine().Split(' ', '\t');
            float scale = float.Parse(reader.ReadLine(), CultureInfo.InvariantCulture); char fallback = reader.ReadLine()[0];
            return new BitmapFont(null, glyphs, fallback, height, new Vector2(float.Parse(spacing[0], CultureInfo.InvariantCulture), float.Parse(spacing[1], CultureInfo.InvariantCulture)), scale);
        }
    }
    static Dialog Fixture()
    {
        var dialog = (Dialog)Activator.CreateInstance(mod.GetType("SCIEPort.Features.CelestialCodex.UI.CodexDialog"), F, null, new object[] { null, false }, null);
        object book = Create("Book"); Set(book, "Slug", "test"); Set(book, "Title", "山海之间：给漫游者的漫长来信与星空阅读笔记"); Set(book, "Author", "云天书库"); Set(book, "SourceMode", "ai_reading"); Set(book, "Status", "ready"); Set(book, "Revision", new string('a', 64));
        string passage="晨雾越过山脊，松针上挂着昨夜的露水。沿着溪流向前，光从树叶的缝隙里落下来。\n\n我们在旧石桥边停下，把远行的故事写进纸页。风吹过时，山野也像一本慢慢翻开的书。\n\n愿你在忙碌的旅途中，留一小段时间给阅读。翻过这一页，便有新的风景等在前方。\n\n";
        for (int i = 1; i <= 30; i++)
        { object c = Create("Chapter"); Set(c, "Id", "c" + i); Set(c, "Title", i == 1 ? "第一封信 · 山海之间" : "山海来信 · 第 " + i + " 节"); Set(c, "Text", string.Concat(Enumerable.Repeat(passage,20)).TrimEnd('\n')); ((IList)Field(book, "Chapters")).Add(c); }
        object catalog = Create("Document"); ((IList)Field(catalog, "Books")).Add(book); Set(catalog, "Page", 1);
        foreach(string name in new[]{"林间手记","日落之后，沿河而行"})
        {object other=Create("Book");Set(other,"Slug","sample"+name.Length);Set(other,"Title",name);Set(other,"Author","荒野书房");Set(other,"Status","available");((IList)Field(catalog,"Books")).Add(other);}
        Set(dialog, "catalog", catalog); Set(dialog, "book", book); Set(dialog, "chapter", ((IList)Field(book, "Chapters"))[0]);
        object position = Field(dialog, "saved"); Set(position, "Slug", "test"); Set(position, "Revision", new string('a', 64)); Set(position, "Chapter", "c1");
        return dialog;
    }
    static void Layout(Dialog dialog, Vector2 size)
    { Call(dialog, "Chrome"); Call(dialog, "Layout", size); Widget.LayoutWidgetsHierarchy(dialog, size); Widget.LayoutWidgetsHierarchy(dialog, size); }
    static void Show(Dialog dialog, string name)
    {
        // Settings normally saves on entry. Isolate this fixture from the real user-data writer.
        if (name == "ShowSettings") Set(dialog, "view", Enum.Parse(mod.GetType("SCIEPort.Features.CelestialCodex.UI.CodexDialog+View"), "Catalog"));
        Call(dialog, name);
    }
    static void CheckReturnToCatalog(Vector2 size, Action<bool, string> check)
    {
        var dialog = Fixture();
        var gui = new CanvasWidget();
        Widget.LayoutWidgetsHierarchy(gui, size);
        var game = (GameWidget)FormatterServices.GetUninitializedObject(typeof(GameWidget));
        game.GuiWidget = gui;
        var player = (ComponentPlayer)FormatterServices.GetUninitializedObject(typeof(ComponentPlayer));
        player.PlayerData = (PlayerData)FormatterServices.GetUninitializedObject(typeof(PlayerData));
        player.PlayerData.GameWidget = game;
        dialog.GetType().GetProperty("Player", F).SetValue(dialog, player, null);

        try
        {
            object catalog = Field(dialog, "catalog");
            object book = Field(dialog, "book");
            Set(book, "Title", "WWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWW 山海之间的超长书名");
            Set(catalog, "Page", 2);
            Set(dialog, "page", 2);
            Set(dialog, "local", true);
            Set(dialog, "theme", 1);
            var back = (ButtonWidget)Field(dialog, "back");
            var click = (ClickableWidget)Field(back, "click");
            var area = (ScrollPanelWidget)Field(dialog, "scroll");
            for (int i = 0; i < 2; i++)
            {
                Call(dialog, "ShowChapters");
                Layout(dialog, size);
                click.IsClicked = true;
                dialog.Update();
                click.IsClicked = false;
                // The game measures and draws straight after Update, with no second reader update.
                if (i == 0) Console.WriteLine("Measuring first catalog frame after Back at " + size);
                Widget.LayoutWidgetsHierarchy(dialog, size);
                var rows = (IList)Field(dialog, "rows");
                var ids = (IList)Field(dialog, "rowIds");
                var bookRow = (CanvasWidget)rows[ids.IndexOf("test")];
                var caption = (LabelWidget)Field(bookRow, "Caption");
                check(!back.IsVisible && (int)Field(dialog, "page") == 2 && !((bool)Field(dialog, "busy")),
                    "Back displays the same catalog page and clears loading on its first frame");
                check(ids.Contains("@import") && ids.Contains("@resume") && rows.Count == 6,
                    "Back retains the local book list and its import and resume entries");
                check(Math.Abs(bookRow.ActualSize.X - area.ActualSize.X) < 1 && caption.m_lines.Count <= 2,
                    "Rebuilt catalog rows receive their final layout before the first native measure");
            }
        }
        finally { dialog.Dispose(); }
    }
    static void CheckNarrowCaption(Type literal, PropertyInfo text, Action<bool, string> check)
    {
        var label = (LabelWidget)Activator.CreateInstance(literal, true);
        label.WordWrap = true;
        label.Ellipsis = true;
        label.MaxLines = 2;
        const string title = "山海之间的漫游者 WWWWW";
        text.SetValue(label, title, null);
        foreach (float width in new[] { 0f, 1f, 26f, 120f, 500f })
        {
            label.MeasureOverride(new Vector2(width, 80));
            check(label.Text == title && label.m_lines.Count <= 2, "Caption reflow is bounded and preserves the source title");
            check(label.m_linesSize.Value.X <= width + .1f, "Caption fits even when there is no room for an ellipsis");
        }
        check(label.m_lines.SequenceEqual(new[] { title }), "Caption at full width shows the entire book title");
    }
    static void CheckReadingSpacing(PropertyInfo text, Action<bool, string> check)
    {
        var dialog = Fixture();
        try
        {
            object chapter = Field(dialog, "chapter");
            var body = (LabelWidget)Field(dialog, "text");
            foreach (Vector2 size in new[] { new Vector2(812, 375), new Vector2(375, 812) })
            foreach (string heading in new[] { "新版序", "Preface" })
            {
                string original = heading + "\n\n \n\t\n\u00a0\n\u3000\n\n正文😀\n下一行\n\n";
                if (heading == "Preface") original = original.Replace("正文😀", "First verse").Replace("下一行", "Second verse");
                Set(chapter, "Text", original);
                Set(dialog, "font", 4);
                Call(dialog, "ShowReading");
                Layout(dialog, size);
                string[] expected = heading == "Preface"
                    ? new[] { heading, "", "First verse", "Second verse" }
                    : new[] { heading, "", "正文😀", "下一行" };
                check(body.m_lines.SequenceEqual(expected), "Reader compacts EPUB spacer rows and keeps one blank line and explicit line breaks");
                check((string)Field(chapter, "Text") == original, "Reader spacing leaves the loaded chapter untouched");
                float compactHeight = body.ActualSize.Y;

                text.SetValue(body, original, null);
                Layout(dialog, size);
                check(body.ActualSize.Y - compactHeight >= 6 * body.CalculateLineHeight() - .1f,
                    "Extra blank lines consume no scroll height after reader compaction");
            }
        }
        finally { dialog.Dispose(); }
    }
    internal static void Run(Assembly assembly, ZipArchive zip, Action<bool, string> check)
    {
        mod = assembly; CacheFont(Metrics(zip));
        using (Stream language = zip.GetEntry("Assets/Lang/zh-CN.json").Open()) LanguageControl.loadJson(language);
        Type literal = mod.GetType("SCIEPort.Features.CelestialCodex.UI.CodexLabel"); var label = (LabelWidget)Activator.CreateInstance(literal, true);
        PropertyInfo text = literal.GetProperty("Text", F | BindingFlags.DeclaredOnly);
        CheckReadingSpacing(text, check);
        foreach (Vector2 size in new[] { new Vector2(320, 240), new Vector2(375, 812), new Vector2(812, 375) })
            CheckReturnToCatalog(size, check);
        CheckNarrowCaption(literal, text, check);
        foreach (string value in new[] { "1", "2", "on", "[Usual:1]", "皮肤颜色", "中文书名\n\n正文😀" })
        { label.m_linesSize = new Vector2(1, 1); text.SetValue(label, value, null); check(label.Text == value && label.m_linesSize == null, "literal text survives native localization and invalidates measurement"); }
        var native = new LabelWidget { Text = "on" }; text.SetValue(label, "on", null);
        check(native.Text == "开" && label.Text == "on", "real native localization substitution reproduced and avoided");
        var paragraph=(LabelWidget)Activator.CreateInstance(mod.GetType("SCIEPort.Features.CelestialCodex.UI.CodexParagraph"),true);
        paragraph.WordWrap=true;paragraph.Ellipsis=false;paragraph.MaxLines=int.MaxValue;paragraph.Size=new Vector2(-1,-1);
        foreach(string source in new[]{"山海（沿着旧路，缓缓前行）……今夜“星河”依然灿烂。","山海😀之间的阅读笔记与英文 mixed words，留在纸页。"})
        foreach(float width in new[]{240f,295f,660f})
        foreach(float scale in new[]{.79f,1.05f,1.31f})
        {
            text.SetValue(paragraph,source,null);paragraph.FontScale=scale;paragraph.MeasureOverride(new Vector2(width,float.PositiveInfinity));
            check(paragraph.Text==source,"paragraph layout never rewrites source text");
            check(string.Concat(paragraph.m_lines).Replace(" ","")==source.Replace(" ",""),"CJK wrapping retains all source characters");
            foreach(string line in paragraph.m_lines)
            {
                check(line.Length==0||!char.IsLowSurrogate(line[0])&&!char.IsHighSurrogate(line[line.Length-1]),"UTF16 surrogate pairs remain on one display line");
                check(line.Length==0||"，。！？；：、）》】」』〕〉〗］｝”’…".IndexOf(line[0])<0,"CJK closing marks stay with preceding glyph");
                check(paragraph.Font.MeasureText(line,new Vector2(scale),paragraph.FontSpacing).X<=width+.2f,"CJK/Latin mixed lines fit native font metrics");
            }
        }
        var widgets = (IDictionary<string, object>)LanguageControl.KeyWords["ContentWidgets"];
        foreach (var screen in widgets)
        {
            var values = screen.Value as IDictionary<string, object>; if (values == null) continue;
            foreach (var pair in values) if (pair.Value is string && ((string)pair.Value).Contains("肤"))
            {
                string key = "[" + screen.Key + ":" + pair.Key + "]";
                native.Text = key; text.SetValue(label, key, null);
                check(native.Text == (string)pair.Value && label.Text == key, "literal label does not resolve skin-related widget key");
                Console.WriteLine("Native localization evidence: " + key + " = " + native.Text);
            }
        }
        var dialog = Fixture();
        try
        {
            foreach (Vector2 size in new[] { new Vector2(320,240), new Vector2(375,812), new Vector2(812,375), new Vector2(1280,720), new Vector2(2340,1080), new Vector2(768,1024) })
            foreach (int theme in new[] { 0, 1 })
            foreach (int font in new[] { 0, 1, 2, 3, 4 })
            foreach (string show in new[] { "ShowCatalog", "ShowChapters", "ShowReading", "ShowSettings" })
            {
                Set(dialog, "theme", theme); Set(dialog, "font", font); Show(dialog, show); Layout(dialog, size);
                foreach (string name in new[] { "title", "status", "folio" })
                {
                    var caption = (LabelWidget)Field(dialog, name); if (!caption.IsVisible) continue;
                    check(!caption.m_linesSize.HasValue || caption.m_linesSize.Value.X <= caption.ActualSize.X + 1, "native measured text fits its own box " + name);
                }
                foreach (string name in new[] { "back", "close", "previous", "middle", "action", "next" })
                {
                    var button = (ButtonWidget)Field(dialog, name); if (!button.IsVisible) continue;
                    Vector2 start = button.WidgetToScreen(Vector2.Zero), end = button.WidgetToScreen(button.ActualSize);
                    check(button.ActualSize.X >= 48 && button.ActualSize.Y >= 48, "native fixed touch bounds " + name);
                    check(start.X >= 8 && start.Y >= 8 && end.X <= size.X - 8 && end.Y <= size.Y - 8, "native fixed controls within safe gutters " + name);
                    check(Widget.HitTestGlobal(dialog, button.WidgetToScreen(button.ActualSize / 2), null) is ClickableWidget, "native hit target unobscured " + name);
                }
                if (show == "ShowReading")
                {
                    var body = (LabelWidget)Field(dialog, "text"); var area = (ScrollPanelWidget)Field(dialog, "scroll");
                    check(body.ActualSize.X <= area.ActualSize.X + 1 && area.CalculateScrollAreaLength() > area.ActualSize.Y, "long text wraps and scrolls in native hierarchy");
                    check(body.Text==(string)Field(Field(dialog,"chapter"),"Text"),"typesetting preserves original chapter text verbatim");
                    foreach(string line in body.m_lines)
                        if(line.Length>0)check("，。！？；：、）》】」』〕〉〗］｝”’…".IndexOf(line[0])<0,"CJK closing punctuation never orphaned at wrapped line start");
                }
                // Verify actual native scrolling after reflow, not only the restoration flag.
                if (show == "ShowSettings")
                {
                    object chapter = Field(dialog, "chapter");
                    Set(dialog, "font", 4); Set(Field(dialog, "saved"), "Scroll", 500000); Call(dialog, "ShowReading"); Call(dialog, "RestorePosition");
                    Layout(dialog, size); Call(dialog, "RestoreAfterLayout"); Layout(dialog, size); Call(dialog, "RestoreAfterLayout");
                    var area = (ScrollPanelWidget)Field(dialog, "scroll"); float range = area.CalculateScrollAreaLength() - area.ActualSize.Y;
                    check(range > 0 && Math.Abs(area.ScrollPosition / range - .5f) < .001f && area.ScrollSpeed == 0 && Field(dialog, "chapter") == chapter, "settings return restores actual native fraction after largest-font reflow");
                }
            }
        }
        finally { ((IDisposable)Field(dialog, "session")).Dispose(); }
        foreach(Vector2 size in new[]{new Vector2(320,240),new Vector2(375,812),new Vector2(812,375),new Vector2(1280,720),new Vector2(768,1024)})
        foreach(int theme in new[]{0,1})
        {
            var import=ImportFixture(theme);
            try
            {
                Call(import,"Layout",size);Widget.LayoutWidgetsHierarchy(import,size);Widget.LayoutWidgetsHierarchy(import,size);
                foreach(string name in new[]{"back","close","system","up","refresh"})
                {
                    var button=(ButtonWidget)Field(import,name);
                    Vector2 start=button.WidgetToScreen(Vector2.Zero),end=button.WidgetToScreen(button.ActualSize);
                    check(button.ActualSize.X>=48&&button.ActualSize.Y>=48,"import controls meet Android touch minimum: "+name);
                    check(start.X>=8&&start.Y>=8&&end.X<=size.X-8&&end.Y<=size.Y-8,"import controls keep screen gutters: "+name);
                    check(Widget.HitTestGlobal(import,button.WidgetToScreen(button.ActualSize/2),null) is ClickableWidget,"import decoration never covers native touch: "+name);
                }
                var importStatus=(LabelWidget)Field(import,"status");
                check(!importStatus.m_linesSize.HasValue||importStatus.m_linesSize.Value.X<=importStatus.ActualSize.X+1,"import status fits viewport");
                var buttons=(IList)Field(import,"rowButtons");
                var first=(ButtonWidget)buttons[0];check(first.Text=="目录 · Download","import filename labels remain literal in shared controls");
                check((string)Field(first,"Detail").GetType().GetProperty("Text",F|BindingFlags.DeclaredOnly).GetValue(Field(first,"Detail"),null)=="","import rows have no native default/localization placeholder");
            }
            finally{import.Dispose();}
        }
    }
    internal static void Render(string gameDir, string repo)
    {
        mod = typeof(CelestialCodexBlock).Assembly;
        Window.Created += () => { object window = typeof(Window).GetField("m_gameWindow", F).GetValue(null); window.GetType().GetProperty("Visible").SetValue(window, false, null); };
        Exception failure = null;
        Window.Frame += () => { try { RenderFrames(gameDir, repo); } catch (Exception e) { failure = e; } finally { Window.Close(); } };
        Window.Run(1280, 720, WindowMode.Fixed, "Celestial Codex native UI fixture");
        if (failure != null) throw failure;
    }
    static void RenderFrames(string gameDir, string repo)
    {
        MethodInfo decoder = typeof(ModsManager).Assembly.GetType("Game.SurvivalCraftModEntity").GetMethod("GetDecipherStream", F);
        using (Stream content = File.OpenRead(Path.Combine(gameDir, "Content.scpak")))
        using (Stream decoded = (Stream)decoder.Invoke(null, new object[] { content }))
        using (var zip = new ZipArchive(decoded, ZipArchiveMode.Read))
        using (Stream png = zip.GetEntry("Assets/Fonts/Pericles.png").Open())
        using (Stream lst = zip.GetEntry("Assets/Fonts/Pericles.lst").Open())
        using (BitmapFont font = BitmapFont.Initialize(png, lst))
        {
            CacheFont(font);
            var textures=new List<Texture2D>();
            foreach(string name in new[]{"Paper","Botanical"})
            using(Stream stream=File.OpenRead(Path.Combine(repo,"SCIE-Port/src/SCIEPort/Assets/Textures/CelestialCodex/"+name+".png")))
            {var texture=Texture2D.Load(stream,false,1);textures.Add(texture);((IDictionary)typeof(ContentManager).GetField("Caches",F).GetValue(null))["Textures/CelestialCodex/"+name]=new List<object>{texture};}
            var dialog = Fixture(); string output = Path.Combine(repo, "artifacts/CelestialCodex/ui-0.90.0"); Directory.CreateDirectory(output);
            try
            {
                foreach (Vector2 size in new[] { new Vector2(1280,720), new Vector2(375,812), new Vector2(812,375) })
                foreach (string show in new[] { "ShowCatalog", "ShowLocal", "ShowChapters", "ShowReading", "ShowSettings", "ShowImport" })
                foreach (int theme in new[] { 0, 1 })
                using (var target = new RenderTarget2D((int)size.X, (int)size.Y, 1, ColorFormat.Rgba8888, DepthFormat.Depth24Stencil8))
                {
                    Dialog surface=dialog;
                    if(show=="ShowImport") {surface=ImportFixture(theme);Call(surface,"Layout",size);Widget.LayoutWidgetsHierarchy(surface,size);Widget.LayoutWidgetsHierarchy(surface,size);}
                    else {Set(dialog,"local",show=="ShowLocal");Set(dialog,"theme",theme);Show(dialog,show=="ShowLocal"?"ShowCatalog":show);Layout(dialog,size);}
                    Display.RenderTarget = target; Display.Viewport = new Viewport(0,0,(int)size.X,(int)size.Y); Display.Clear(new Color(36,44,48),1,0);
                    Widget.DrawWidgetsHierarchy(surface);
                    using (Stream image = File.Create(Path.Combine(output, show + "-" + (int)size.X + "x" + (int)size.Y + "-" + theme + ".png"))) RenderTarget2D.Save(target, image, ImageFileFormat.Png, false);
                    if(surface!=dialog)surface.Dispose();
                }
            }
            finally { ((IDisposable)Field(dialog, "session")).Dispose(); Display.RenderTarget = null;foreach(var texture in textures)texture.Dispose(); }
            Console.WriteLine("Native Engine/OpenGL UI fixtures: " + output + "; synthetic data, not Android/game screenshots.");
        }
    }
}

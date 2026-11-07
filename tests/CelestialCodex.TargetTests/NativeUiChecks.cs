using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using CelestialCodex.Mod;
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
    static object Create(string name) { return Activator.CreateInstance(mod.GetType("CelestialCodex.Core." + name), true); }
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
        var dialog = (Dialog)Activator.CreateInstance(mod.GetType("CelestialCodex.UI.CodexDialog"), F, null, new object[] { null, false }, null);
        object book = Create("Book"); Set(book, "Slug", "test"); Set(book, "Title", "山海之间：给漫游者的漫长来信与星空阅读笔记"); Set(book, "Author", "云天书库"); Set(book, "SourceMode", "ai_reading"); Set(book, "Status", "ready"); Set(book, "Revision", new string('a', 64));
        for (int i = 1; i <= 30; i++)
        { object c = Create("Chapter"); Set(c, "Id", "c" + i); Set(c, "Title", i == 1 ? "1" : "山海来信 · 第 " + i + " 节"); Set(c, "Text", string.Concat(Enumerable.Repeat("夜色落下，远山与星河在纸页间缓缓展开。我们沿着旧路前行，也在阅读中遇见新的世界。\n\n", 65))); ((IList)Field(book, "Chapters")).Add(c); }
        object catalog = Create("Document"); ((IList)Field(catalog, "Books")).Add(book); Set(catalog, "Page", 1);
        Set(dialog, "catalog", catalog); Set(dialog, "book", book); Set(dialog, "chapter", ((IList)Field(book, "Chapters"))[0]);
        object position = Field(dialog, "saved"); Set(position, "Slug", "test"); Set(position, "Revision", new string('a', 64)); Set(position, "Chapter", "c1");
        return dialog;
    }
    static void Layout(Dialog dialog, Vector2 size)
    { Call(dialog, "Chrome"); Call(dialog, "Layout", size); Widget.LayoutWidgetsHierarchy(dialog, size); Widget.LayoutWidgetsHierarchy(dialog, size); }
    static void Show(Dialog dialog, string name)
    {
        // Settings normally saves on entry. Isolate this fixture from the real user-data writer.
        if (name == "ShowSettings") Set(dialog, "view", Enum.Parse(mod.GetType("CelestialCodex.UI.CodexDialog+View"), "Catalog"));
        Call(dialog, name);
    }
    internal static void Run(Assembly assembly, ZipArchive zip, Action<bool, string> check)
    {
        mod = assembly; CacheFont(Metrics(zip));
        using (Stream language = zip.GetEntry("Assets/Lang/zh-CN.json").Open()) LanguageControl.loadJson(language);
        Type literal = mod.GetType("CelestialCodex.UI.CodexLabel"); var label = (LabelWidget)Activator.CreateInstance(literal, true);
        PropertyInfo text = literal.GetProperty("Text", F | BindingFlags.DeclaredOnly);
        foreach (string value in new[] { "1", "2", "on", "[Usual:1]", "皮肤颜色", "中文书名\n\n正文😀" })
        { label.m_linesSize = new Vector2(1, 1); text.SetValue(label, value, null); check(label.Text == value && label.m_linesSize == null, "literal text survives native localization and invalidates measurement"); }
        var native = new LabelWidget { Text = "on" }; text.SetValue(label, "on", null);
        check(native.Text == "开" && label.Text == "on", "real native localization substitution reproduced and avoided");
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
            CacheFont(font); var dialog = Fixture(); string output = Path.Combine(repo, "artifacts/CelestialCodex/ui-0.2.0"); Directory.CreateDirectory(output);
            try
            {
                foreach (Vector2 size in new[] { new Vector2(1280,720), new Vector2(375,812), new Vector2(812,375) })
                foreach (string show in new[] { "ShowCatalog", "ShowChapters", "ShowReading", "ShowSettings" })
                foreach (int theme in show == "ShowReading" ? new[] { 0, 1 } : new[] { 0 })
                using (var target = new RenderTarget2D((int)size.X, (int)size.Y, 1, ColorFormat.Rgba8888, DepthFormat.Depth24Stencil8))
                {
                    Set(dialog, "theme", theme); Show(dialog, show); Layout(dialog, size);
                    Display.RenderTarget = target; Display.Viewport = new Viewport(0,0,(int)size.X,(int)size.Y); Display.Clear(new Color(36,44,48),1,0);
                    Widget.DrawWidgetsHierarchy(dialog);
                    using (Stream image = File.Create(Path.Combine(output, show + "-" + (int)size.X + "x" + (int)size.Y + "-" + theme + ".png"))) RenderTarget2D.Save(target, image, ImageFileFormat.Png, false);
                }
            }
            finally { ((IDisposable)Field(dialog, "session")).Dispose(); Display.RenderTarget = null; }
            Console.WriteLine("Native Engine/OpenGL UI fixtures: " + output + "; synthetic data, not Android/game screenshots.");
        }
    }
}

using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using Engine;
using Engine.Graphics;
using Game;

public static class SuspensionGpuBootstrap
{
    public static int Main(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve += (s, e) => {
            string name = new AssemblyName(e.Name).Name;
            foreach (string ext in new[] { ".dll", ".exe" }) { string p = Path.Combine(args[0], name + ext); if (File.Exists(p)) return Assembly.LoadFrom(p); }
            return null;
        };
        try { typeof(SuspensionGpuBootstrap).Assembly.GetType("SuspensionGpuProbe").GetMethod("Run").Invoke(null, new object[] { args[1], args[2], args[3] }); return 0; }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
public static class SuspensionGpuProbe
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static Assembly mod; static object runtime; static MethodInfo draw; static Terrain terrain; static RenderTarget2D target;
    static readonly PrimitivesRenderer3D world = new PrimitivesRenderer3D();
    static string output, assets; static int checks; static Matrix view;
    static void Check(bool b, string label) { checks++; if (!b) throw new Exception("Suspension GPU: " + label); }
    static void Put(object o, string name, object value) { for (var t = o.GetType(); t != null; t = t.BaseType) { var f = t.GetField(name, F); if (f != null) { f.SetValue(o, value); return; } } throw new Exception(name); }
    static void Cache(string name, object value)
    {
        var cache = (Dictionary<string, List<object>>)typeof(ContentManager).GetField("Caches", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        cache[name] = new List<object> { value };
    }
    static Vector3 Vec(string s) { var a = s.Split(','); return new Vector3(float.Parse(a[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(a[1], System.Globalization.CultureInfo.InvariantCulture), float.Parse(a[2], System.Globalization.CultureInfo.InvariantCulture)); }
    static Block Prepare(string name, int id)
    {
        var b = (Block)Activator.CreateInstance(mod.GetType("SCIEPort.Features." + name, true)); b.BlockIndex = id; b.MaxStacking = 40; b.DefaultIconViewScale = 1;
        foreach (var path in Directory.GetFiles(Path.Combine(assets, "Blocks"), "*.csv"))
        {
            var lines = File.ReadAllLines(path); var head = lines[0].Split(';');
            for (int n = 1; n < lines.Length; n++)
            {
                var row = lines[n].Split(';'); if (row[0] != b.GetType().Name) continue;
                int at = Array.IndexOf(head, "DefaultIconViewScale"); if (at >= 0) b.DefaultIconViewScale = float.Parse(row[at], System.Globalization.CultureInfo.InvariantCulture);
                at = Array.IndexOf(head, "DefaultIconBlockOffset"); if (at >= 0) b.DefaultIconBlockOffset = Vec(row[at]);
            }
        }
        b.Initialize(); BlocksManager.Blocks[id] = b; return b;
    }
    public static void Run(string assembly, string directory, string assetDirectory)
    {
        mod = Assembly.LoadFrom(assembly); output = directory; assets = assetDirectory;
        Window.Created += () => { var w = typeof(Window).GetField("m_gameWindow", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null); w.GetType().GetProperty("Visible").SetValue(w, false, null); };
        Exception error = null;
        Window.Frame += () => { try { Execute(); } catch (Exception e) { error = e; } finally { Window.Close(); } };
        Window.Run(960, 540, WindowMode.Fixed, "Suspended real item synthetic GPU fixture"); if (error != null) throw error;
    }
    static Color[] Frame(int[] ids, float seconds, string name, bool wall = false, float aspect = 960f / 540, Vector3? eye = null)
    {
        Display.RenderTarget = target; Display.Viewport = new Viewport(0, 0, 960, 540); Display.Clear(new Color(17, 24, 39), 1, 0);
        view = Matrix.CreateLookAt(eye ?? new Vector3(3.2f, 2.4f, -4), new Vector3(.5f, 1.6f, .5f), Vector3.UnitY)
            * Matrix.CreatePerspectiveFieldOfView(55 * MathUtils.PI / 180, aspect, .1f, 100);
        var floor = world.FlatBatch(0, DepthStencilState.Default, RasterizerState.CullNone, BlendState.Opaque);
        for (int x = -12; x < 12; x++) for (int z = -12; z < 12; z++)
            floor.QueueQuad(new Vector3(x, 0, z), new Vector3(x + 1, 0, z), new Vector3(x + 1, 0, z + 1), new Vector3(x, 0, z + 1), (x + z) % 2 == 0 ? new Color(48, 54, 64) : new Color(53, 61, 72));
        if (wall) floor.QueueQuad(new Vector3(-100, -100, -1), new Vector3(100, -100, -1), new Vector3(100, 100, -1), new Vector3(-100, 100, -1), new Color(42, 46, 58));
        world.Flush(view);
        foreach (int id in ids) draw.Invoke(runtime, new object[] { Terrain.MakeBlockValue(id), new Point3(0, 1, 0), seconds, view });
        var failed = (ICollection<int>)runtime.GetType().GetField("failed", F).GetValue(runtime);
        Check(failed.Count == 0, "production DrawItem has no suppressed rendering errors");
        var pixels = new Color[960 * 540]; target.GetData(pixels, 0, new Rectangle(0, 0, 960, 540));
        using (var s = File.Create(Path.Combine(output, name + ".png"))) RenderTarget2D.Save(target, s, Engine.Media.ImageFileFormat.Png, false);
        return pixels;
    }
    static int Diff(Color[] a, Color[] b) { int n = 0; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) n++; return n; }
    static void Execute()
    {
        LightingManager.CalculateLightingTables();
        // Native cube/flat-item geometry with an original test palette, never a packaged game atlas.
        var palette = new Texture2D(16, 16, 1, ColorFormat.Rgba8888); var texels = new byte[16 * 16 * 4];
        for (int i = 0; i < 256; i++) { texels[i * 4] = 186; texels[i * 4 + 1] = 148; texels[i * 4 + 2] = 94; texels[i * 4 + 3] = 255; }
        palette.SetData(0, texels); BlocksTexturesManager.DefaultBlocksTexture = palette;
        BlocksManager.Blocks[3] = new GraniteBlock { BlockIndex = 3, MaxStacking = 40, DefaultIconViewScale = 1 };
        Type flatType = null;
        foreach (var t in typeof(Block).Assembly.GetTypes()) if (!t.IsAbstract && t.BaseType == typeof(FlatBlock) && t.GetField("Index") != null && t.GetConstructor(Type.EmptyTypes) != null) { flatType = t; break; }
        Check(flatType != null, "native direct FlatBlock item available");
        int flatId = (int)flatType.GetField("Index").GetRawConstantValue(); var flat = (Block)Activator.CreateInstance(flatType);
        flat.BlockIndex = flatId; flat.DefaultIconViewScale = 1; BlocksManager.Blocks[flatId] = flat;
        foreach (string name in new[] { "Excalibur", "Gungnir", "PoseidonTrident", "Stormhammer", "ThunderSpear", "OmniscientEye", "DevouringEye", "AbyssReaper" })
        {
            using (var stream = File.OpenRead(Path.Combine(assets, "Models", name + ".dae"))) Cache("Models/" + name, Model.Load(stream, true));
        }
        foreach (string name in new[] { "Excalibur", "Gungnir", "PoseidonTrident", "Stormhammer", "StormhammerAwakenedGlow", "StormhammerAwakenedEffects", "StormhammerAwakenedSkills", "OmniscientEye", "DevouringEye", "AbyssReaper" })
        {
            using (var stream = File.OpenRead(Path.Combine(assets, "Textures", name + ".png"))) Cache("Textures/" + name, Texture2D.Load(stream, false, 1));
        }
        Prepare("Excalibur.ExcaliburBlock", 953); Prepare("Gungnir.GungnirBlock", 948); Prepare("Poseidon.PoseidonBlock", 951);
        Prepare("Stormhammer.StormhammerAwakenedBlock", 942); Prepare("OmniscientEye.OmniscientEyeBlock", 949);
        Prepare("DevouringEye.DevouringEyeBlock", 950); Prepare("AbyssReaper.AbyssReaperBlock", 952); Prepare("Suspension.SuspendedItemBlock", 954); Prepare("Codex.ArtifactCodexBlock", 955);
        terrain = new Terrain(); for (int x = -16; x <= 16; x += 16) for (int z = -16; z <= 16; z += 16) terrain.AllocateChunk(x, z).State = TerrainChunkState.Valid;
        var st = (SubsystemTerrain)FormatterServices.GetUninitializedObject(typeof(SubsystemTerrain)); Put(st, "<Terrain>k__BackingField", terrain);
        terrain.SetCellValueFast(0, 1, 0, Terrain.MakeBlockValue(954, 15, 0));
        runtime = Activator.CreateInstance(mod.GetType("SCIEPort.Features.Suspension.SubsystemSuspendedItem", true)); Put(runtime, "terrain", st);
        draw = runtime.GetType().GetMethod("DrawItem", F);
        var env = (DrawBlockEnvironmentData)runtime.GetType().GetField("environment", F).GetValue(runtime); env.SubsystemTerrain = null;
        target = new RenderTarget2D(960, 540, 1, ColorFormat.Rgba8888, DepthFormat.Depth24Stencil8);
        var blank = Frame(new int[0], 0, "empty-room");
        foreach (int id in new[] { 3, flatId, 942, 948, 949, 950, 951, 952, 953, 954, 955 })
        {
            var a = Frame(new[] { id }, 0, "item-" + id); var b = Frame(new[] { id }, 7.5f, "item-" + id + "-rotated");
            Check(Diff(a, blank) > (id == 954 ? 50 : 200), "real item model visibly present " + id);
            Check(Diff(a, b) > 50, "item rotates without an external stand " + id);
            int colored = 0; for (int i = 0; i < a.Length; i++) if (a[i] != blank[i] && a[i].R + a[i].G + a[i].B > 60) colored++;
            Check(colored > 10, "item materials and lighting are visible, not a black silhouette " + id);
        }
        var wallBlank = Frame(new int[0], 0, "wall-only", true); var hidden = Frame(new[] { 953 }, 0, "wall-occlusion", true);
        Check(Diff(wallBlank, hidden) == 0, "stored item is occluded by a solid foreground wall");
        var rearBlank = Frame(new int[0], 0, "rear-empty", false, 960f / 540, new Vector3(-2, 2.8f, 4));
        var rear = Frame(new[] { 953 }, 0, "sword-rear", false, 960f / 540, new Vector3(-2, 2.8f, 4));
        Check(Diff(rearBlank, rear) > 200, "backside is true 3D geometry");
        Frame(new[] { 953 }, 2, "sword-20x9", false, 20f / 9);
        // Mixed repeated draws exercise the per-item flush, including models with many overlay meshes.
        Frame(new[] { 948, 952, 953, 942, 951, 949, 950, 954, 948, 952, 953, 942, 951, 949, 950, 954 }, 3, "sixteen-draws");
        Check(env.DrawBlockMode == DrawBlockMode.UI && env.ViewProjectionMatrix == null, "item context cannot inherit player-held buffs or double-project");
        ((IDisposable)runtime).Dispose(); target.Dispose(); terrain.Dispose();
        Console.WriteLine("Suspension actual item draw / target Engine GPU: " + checks + " checks passed (synthetic scene, not game/device footage).");
    }
}

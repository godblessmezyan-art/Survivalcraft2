using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using System.Xml.Linq;
using SCIEPort.Features.CelestialCodex.Mod;
using Engine.Media;
using Game;

internal static class Program
{
    private static int checks;
    private static void Check(bool value, string name) { checks++; if (!value) throw new Exception(name); }
    private static void Main(string[] args)
    {
        // Register resolution before JIT compiling any method that references game types.
        AppDomain.CurrentDomain.AssemblyResolve += (sender, e) =>
        {
            string path = Path.Combine(args[0], new AssemblyName(e.Name).Name + ".dll");
            if (new AssemblyName(e.Name).Name == "Survivalcraft") path = Path.Combine(args[0], "Survivalcraft.exe");
            if (new AssemblyName(e.Name).Name == "SCIEPort") path = args[2];
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };
        try { Run(args[0], args[1]); if (args.Length > 3 && args[3] == "--https-probe") HttpsProbe(); }
        catch (Exception e) { Console.Error.WriteLine(e); Environment.ExitCode = 1; }
    }
    private static void Run(string gameDir, string repo)
    {
        Assembly game = typeof(Block).Assembly, mod = typeof(CelestialCodexBlock).Assembly;
        AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += (sender, e) =>
        {
            string file = Path.Combine(gameDir, new AssemblyName(e.Name).Name + ".dll");
            if (new AssemblyName(e.Name).Name == "Survivalcraft") file = Path.Combine(gameDir, "Survivalcraft.exe");
            return File.Exists(file) ? Assembly.ReflectionOnlyLoadFrom(file) : Assembly.ReflectionOnlyLoad(e.Name);
        };
        int installed = 0;
        string netMods = Path.Combine(gameDir, "NetMods");
        foreach (string package in Directory.Exists(netMods) ? Directory.EnumerateFiles(netMods, "*.netmod") : new string[0])
        {
            using (var zip = System.IO.Compression.ZipFile.OpenRead(package))
            {
                foreach (var entry in zip.Entries)
                {
                    if (!entry.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) continue;
                    Check(entry.Length <= 32 * 1024 * 1024, "Installed DLL audit bound");
                    using (Stream input = entry.Open())
                    using (var memory = new MemoryStream())
                    {
                        byte[] buffer = new byte[4096]; int read;
                        while ((read = input.Read(buffer, 0, buffer.Length)) > 0) { if (memory.Length + read > 32 * 1024 * 1024) throw new InvalidDataException("Installed DLL bound"); memory.Write(buffer, 0, read); }
                        Assembly other = Assembly.ReflectionOnlyLoad(memory.ToArray());
                        Check(other.GetName().Name != "CelestialCodex", "Remove standalone CelestialCodex before installing the integrated package");
                        if (other.GetName().Name == "SCIEPort") continue;
                        foreach (Type t in other.GetTypes())
                        {
                            var index = t.GetField("Index", BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
                            if (index != null && index.IsLiteral && index.FieldType == typeof(int)) Check((int)index.GetRawConstantValue() != 956, "Installed Index collision " + t.FullName);
                        }
                    }
                }
            }
            installed++;
        }
        foreach (Type type in game.GetTypes())
        {
            if (!type.IsSubclassOf(typeof(Block))) continue;
            FieldInfo field = type.GetField("Index", BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
            if (field != null && field.IsLiteral && field.FieldType == typeof(int)) Check((int)field.GetRawConstantValue() != CelestialCodexBlock.Index, "Native Index collision " + type.Name);
        }
        foreach (string sourceRoot in new[] { "src", "SCIE-Port/src", "SurvivalcraftRuins-Port/src" })
        foreach (string file in Directory.EnumerateFiles(Path.Combine(repo, sourceRoot), "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains("CelestialCodex") || file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)) continue;
            Check(!System.Text.RegularExpressions.Regex.IsMatch(File.ReadAllText(file), @"\b(?:Index|BlockIndex|ContentIndex)\s*=\s*956\b"), "Source Index collision " + file);
        }
        string modRoot = Path.Combine(repo, "SCIE-Port/src/SCIEPort");
        Check(mod.GetName().Name == "SCIEPort", "Reader is compiled into SCIEPort.dll");
        Check(mod.GetTypes().Count(t => t.IsSubclassOf(typeof(ModLoader)) && !t.IsAbstract) == 1, "Reuse exactly one existing ModLoader");
        Check(mod.GetTypes().Count(t => t.IsSubclassOf(typeof(Block)) && t.GetField("Index", BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)?.GetRawConstantValue() as int? == 956) == 1, "Exactly one owned block retains ID956");
        var block = new CelestialCodexBlock { BlockIndex = CelestialCodexBlock.Index };
        var air = new AirBlock();
        for (int i = 0; i < BlocksManager.m_blocks.Length; i++) BlocksManager.m_blocks[i] = air;
        BlocksManager.m_blocks[CelestialCodexBlock.Index] = block;
        BlocksManager.LoadBlocksData(File.ReadAllText(Path.Combine(modRoot, "Assets/Blocks/CelestialCodex.csv")));
        Check(!block.IsPlaceable && !block.IsCollidable && block.MaxStacking == 1, "Native CSV flags");
        Check(block.GetTextureSlotCount(956) == 1 && block.GetMeleePower(956) == 0 && block.GetProjectilePower(956) == 0, "Nonweapon/icon semantics");
        Check(block.GetCreativeValues().SequenceEqual(new[] { 956 }), "Native creative item DATA0");
        Check(block.GetDisplayName(null, 956) == "云天书典" && block.GetCategory(956) == "SCIEPort.Tools", "Item/category");
        Check(new SubsystemCelestialCodexBehavior().HandledBlocks.SequenceEqual(new[] { 956 }), "Use route");
        using (Stream image = File.OpenRead(Path.Combine(modRoot, "Assets/Textures/CelestialCodex/Book.png"))) { Image icon = Image.Load(image); Check(icon.Width == 32 && icon.Height == 32, "Native PNG decoder"); }

        var decoder = game.GetType("Game.SurvivalCraftModEntity").GetMethod("GetDecipherStream", BindingFlags.NonPublic | BindingFlags.Static);
        using (var content = File.OpenRead(Path.Combine(gameDir, "Content.scpak")))
        using (var decoded = (Stream)decoder.Invoke(null, new object[] { content }))
        using (var zip = new System.IO.Compression.ZipArchive(decoded, ZipArchiveMode.Read))
        using (var reader = new StreamReader(zip.GetEntry("Assets/Database.xml").Open()))
        {
            NativeUiChecks.Run(mod,zip,Check);
            XElement xml = XElement.Parse(reader.ReadToEnd());
            string ownXdb = Path.GetFullPath(Path.Combine(modRoot, "Assets/Database/CelestialCodex.xdb"));
            var xdb = XElement.Load(ownXdb);
            Check((string)xdb.Descendants().Single(x => (string)x.Attribute("Name") == "Class").Attribute("Value") == typeof(SubsystemCelestialCodexBehavior).FullName, "XDB resolves integrated subsystem class");
            var shared = new HashSet<string>(new[] { "00c97f0f-731e-481c-9909-eae9cc5ee940", "85023bf8-1c90-4dd1-9442-e6c13691d078" });
            foreach (var node in xdb.Descendants().Where(x => x.Attribute("Guid") != null))
            {
                string guid = (string)node.Attribute("Guid"); if (shared.Contains(guid)) continue;
                Check(!xml.Descendants().Any(x => (string)x.Attribute("Guid") == guid), "Native GUID collision");
                foreach (string sourceRoot in new[] { "src/BuilderAssistant", "SCIE-Port/src", "SurvivalcraftRuins-Port/src" })
                foreach (string file in Directory.EnumerateFiles(Path.Combine(repo, sourceRoot), "*.xdb", SearchOption.AllDirectories))
                    if (!string.Equals(Path.GetFullPath(file), ownXdb, StringComparison.OrdinalIgnoreCase) && !file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar) && !file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)) Check(!File.ReadAllText(file).Contains(guid), "Other Mod GUID collision " + file);
            }
            using (var stream = File.OpenRead(ownXdb)) ModsManager.CombineDataBase(xml, stream);
            // Deserialize the merged native tree to catch broken nesting and inheritance.
            var serializer = typeof(TemplatesDatabase.Database).Assembly.GetType("TemplatesDatabase.XmlDatabaseSerializer");
            MethodInfo load = serializer.GetMethods().Single(m => m.Name == "LoadDatabase" && m.GetParameters().Length == 1);
            object database = load.Invoke(null, new object[] { xml }); Check(database != null, "Native XDB deserialization");
            Check(xml.Descendants().Count(x => (string)x.Attribute("Name") == "CelestialCodexBehavior") == 2, "Subsystem template/member registration");
        }
        var refs = AuditMembers(mod, mod.GetTypes().Where(t => t.FullName.StartsWith("SCIEPort.Features.CelestialCodex.", StringComparison.Ordinal)).ToArray());
        string previousHttp = Path.Combine(repo, "artifacts/celestial-audit/CelestialCodex-0.1.3-negative.dll");
        if (File.Exists(previousHttp))
        {
            bool rejected = false;
            try { AuditMembers(Assembly.LoadFile(previousHttp)); }
            catch (Exception e) { if (!e.Message.StartsWith("Android trimmed HTTP member ", StringComparison.Ordinal)) throw; rejected = true; }
            Check(rejected, "BCL guard rejects 0.1.3 trimmed HTTP calls");
        }
        Check(RejectsReportedListCall(typeof(LegacyListProbe).Assembly, new[] { typeof(LegacyListProbe) }), "BCL guard rejects compiled List.Exists negative fixture");
        Check(RejectsLinkedCancellation(typeof(LegacyCancellationProbe).Assembly, new[] { typeof(LegacyCancellationProbe) }), "BCL guard rejects compiled linked CTS fixture");
        string previousNetwork = Path.Combine(repo, "artifacts/celestial-audit/CelestialCodex-0.1.2-negative.dll");
        if (File.Exists(previousNetwork))
        {
            Assembly previous = Assembly.LoadFile(previousNetwork);
            Check(RejectsLinkedCancellation(previous), "BCL guard rejects actual 0.1.2 linked CTS call");
            Console.WriteLine("Actual 0.1.2 negative DLL rejected: " + previous.ManifestModule.ModuleVersionId.ToString("N"));
        }
        string legacy = Path.Combine(repo, "artifacts/celestial-audit/CelestialCodex-0.1.0-negative.dll");
        if (File.Exists(legacy))
        {
            Assembly previous = Assembly.LoadFile(legacy);
            Check(RejectsReportedListCall(previous), "BCL guard rejects actual 0.1.0 DLL List.Exists");
            Console.WriteLine("Actual 0.1.0 negative DLL rejected: " + previous.ManifestModule.ModuleVersionId.ToString("N"));
        }
        VerifyUiFailureBoundary(mod);
        string output = Path.Combine(repo, "artifacts/celestial-audit/member-refs.txt"); Directory.CreateDirectory(Path.GetDirectoryName(output)); File.WriteAllLines(output, refs.OrderBy(x => x));
        Check(!mod.GetReferencedAssemblies().Any(a => a.Name == "CelestialCodex" || a.Name == "BuilderAssistant"), "No standalone or BuilderAssistant dependency");
        Console.WriteLine("CelestialCodex target: " + checks + " checks; " + refs.Count + " resolved member references; " + installed + " installed packages audited; native CSV/XDB/PNG passed. Windows x25.01.17 only.");
    }
    private static void HttpsProbe()
    {
        Assembly assembly = typeof(CelestialCodexBlock).Assembly;
        Type transport = assembly.GetType("SCIEPort.Features.CelestialCodex.Networking.LibraryTransport");
        object instance = Activator.CreateInstance(transport, BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { "https://wxhappylife.top/digital-study/api/game-library/v1", null }, null);
        MethodInfo get = transport.GetMethod("Get", BindingFlags.NonPublic | BindingFlags.Instance);
        MethodInfo decode = assembly.GetType("SCIEPort.Features.CelestialCodex.Core.Protocol").GetMethod("Decode", BindingFlags.NonPublic | BindingFlags.Static);
        Func<string, byte[]> fetch = path => ((System.Threading.Tasks.Task<byte[]>)get.Invoke(instance, new object[] { path, System.Threading.CancellationToken.None, null })).GetAwaiter().GetResult();
        Func<object, string, object> field = (obj, name) => obj.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(obj);
        byte[] catalogBytes = fetch("/books?page=1&pageSize=20");
        object catalog = decode.Invoke(null, new object[] { catalogBytes, null, null, null });
        var books = (System.Collections.IList)field(catalog, "Books");
        if (books.Count == 0) throw new Exception("Live catalog has no sample for reading validation");
        string slug = (string)field(books[0], "Slug");
        byte[] bookBytes = fetch("/books/" + slug);
        object detail = decode.Invoke(null, new object[] { bookBytes, slug, null, null });
        object book = field(detail, "Book"); string revision = (string)field(book, "Revision");
        var chapters = (System.Collections.IList)field(book, "Chapters");
        if (chapters.Count == 0) throw new Exception("Live sample book has no readable chapter");
        string id = (string)field(chapters[0], "Id");
        byte[] bodyBytes = fetch("/books/" + slug + "/chapters/" + id + "?revision=" + revision);
        object body = decode.Invoke(null, new object[] { bodyBytes, slug, id, revision });
        string text = (string)field(field(body, "Chapter"), "Text");
        Console.WriteLine("HTTPS live pipeline: 3 x 200; catalog=" + books.Count + "; slug=" + slug + "; chapter=" + id + "; text chars=" + text.Length + "; bytes=" + catalogBytes.Length + "/" + bookBytes.Length + "/" + bodyBytes.Length + "; revision=" + revision + ". Mod DLL transport + decoder, read only.");
    }
    private static bool RejectsReportedListCall(Assembly assembly, Type[] types = null)
    {
        try { AuditMembers(assembly, types, true); }
        catch (Exception e) { if (e.Message.StartsWith("Android unsupported collection method ", StringComparison.Ordinal) && e.Message.Contains(".Exists")) return true; throw; }
        return false;
    }
    private static bool RejectsLinkedCancellation(Assembly assembly, Type[] types = null)
    {
        try { AuditMembers(assembly, types); }
        catch (Exception e) { if (e.Message.StartsWith("Android unsupported cancellation method ", StringComparison.Ordinal)) return true; throw; }
        return false;
    }
    private static void VerifyUiFailureBoundary(Assembly assembly)
    {
        Type type = assembly.GetType("SCIEPort.Features.CelestialCodex.UI.CodexDialog");
        MethodInfo core = type.GetMethod("UpdateReader", BindingFlags.NonPublic | BindingFlags.Instance);
        Check(core != null && (core.GetMethodImplementationFlags() & MethodImplAttributes.NoInlining) != 0, "JIT boundary is not inlined into native Update");
        MethodInfo update = type.GetMethod("Update");
        Check(update.GetMethodBody().ExceptionHandlingClauses.Any(c => c.Flags == ExceptionHandlingClauseOptions.Clause && c.CatchType == typeof(Exception)), "Update has an exception boundary");
        // A partial native player has no PlayerData. Its GameWidget getter faults before layout.
        // Exercise the actual override and verify that it contains the fault without a game process.
        var dialog = (Dialog)FormatterServices.GetUninitializedObject(type);
        var player = (ComponentPlayer)FormatterServices.GetUninitializedObject(typeof(ComponentPlayer));
        type.GetProperty("Player", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(dialog, player, null);
        dialog.Update();
        Check((bool)type.GetProperty("Closed", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(dialog, null), "failed frame closes only reader");
        Check(type.GetProperty("Player", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(dialog, null) == null, "failed frame releases player reference");
        dialog.Update(); Check(true, "closed reader update stays inert");
    }
    private static HashSet<string> AuditMembers(Assembly assembly, Type[] selectedTypes = null, bool onlyReportedMissing = false)
    {
        var codes = new Dictionary<ushort, OpCode>(); foreach (FieldInfo f in typeof(OpCodes).GetFields()) if (f.FieldType == typeof(OpCode)) { var op = (OpCode)f.GetValue(null); codes[(ushort)op.Value] = op; }
        var references = new HashSet<string>();
        foreach (Type type in selectedTypes ?? assembly.GetTypes())
        {
            var methods = new List<MethodBase>(type.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
            methods.AddRange(type.GetConstructors(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
            foreach (MethodBase method in methods)
            {
                var body = method.GetMethodBody(); if (body == null) continue; byte[] il = body.GetILAsByteArray();
                for (int i = 0; i < il.Length;)
                {
                    ushort code = il[i++]; if (code == 0xfe) code = (ushort)(0xfe00 | il[i++]); var op = codes[code];
                    int length;
                    switch (op.OperandType)
                    {
                        case OperandType.InlineNone: length = 0; break;
                        case OperandType.ShortInlineI: case OperandType.ShortInlineVar: case OperandType.ShortInlineBrTarget: length = 1; break;
                        case OperandType.InlineVar: length = 2; break;
                        case OperandType.InlineI8: case OperandType.InlineR: length = 8; break;
                        case OperandType.InlineSwitch: length = 4 + BitConverter.ToInt32(il, i) * 4; break;
                        default: length = 4; break;
                    }
                    if (op.OperandType == OperandType.InlineMethod || op.OperandType == OperandType.InlineField || op.OperandType == OperandType.InlineType || op.OperandType == OperandType.InlineTok)
                    {
                        var member = method.Module.ResolveMember(BitConverter.ToInt32(il, i), type.IsGenericType ? type.GetGenericArguments() : null, method.IsGenericMethod ? method.GetGenericArguments() : null);
                        Type declaring = member.DeclaringType ?? member as Type; string name = declaring == null ? member.ToString() : declaring.FullName + "." + member.Name;
                        Check(!(name.StartsWith("System.") && (member.Name.StartsWith("TryParse") || name.Contains("System.Linq.Enumerable"))), "Android known missing API " + name);
                        Check(!(declaring != null && declaring.FullName == "System.Net.Http.HttpClientHandler" && (member.Name == "set_AllowAutoRedirect" || member.Name == "set_UseCookies" || member.Name == "set_AutomaticDecompression")) && !(name == "System.Net.Http.Headers.MediaTypeHeaderValue.get_MediaType"), "Android trimmed HTTP member " + name);
                        if (!onlyReportedMissing && declaring == typeof(System.Threading.CancellationTokenSource))
                            Check(member.Name != "CreateLinkedTokenSource" && member.Name != "CancelAfter", "Android unsupported cancellation method " + name);
                        if (declaring != null && declaring.IsGenericType && declaring.GetGenericTypeDefinition() == typeof(List<>))
                        {
                            bool forbidden = member.Name == "Exists";
                            if (!onlyReportedMissing)
                                foreach (string candidate in new[] { "Find", "FindAll", "FindIndex", "FindLast", "FindLastIndex", "RemoveAll", "Sort", "TrueForAll", "ForEach", "ConvertAll", "BinarySearch" }) if (member.Name == candidate) forbidden = true;
                            Check(!forbidden, "Android unsupported collection method " + name);
                        }
                        Check(!name.StartsWith("Game.NetWork.") && !name.StartsWith("Game.ComponentLocomotion.") && !name.StartsWith("Game.ComponentHealth.") && !name.Contains(".ChangeCell"), "Client reader side effects " + name);
                        if (declaring != null && declaring.Assembly != assembly) references.Add(name + " :: " + member);
                    }
                    i += length;
                }
            }
        }
        return references;
    }
}

// Deliberately stays in the test assembly, never in the shipped Mod.
internal static class LegacyListProbe
{
    internal static bool ContainsZero(List<int> values) { return values.Exists(IsZero); }
    private static bool IsZero(int value) { return value == 0; }
}

internal static class LegacyCancellationProbe
{
    internal static System.Threading.CancellationTokenSource Create(System.Threading.CancellationToken caller)
    { return System.Threading.CancellationTokenSource.CreateLinkedTokenSource(caller); }
}

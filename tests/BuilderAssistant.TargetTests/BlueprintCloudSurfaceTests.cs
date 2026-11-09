using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.Threading.Tasks;

internal static partial class TargetTests
{
    private static void TestBlueprintCloudRuntimeSurface()
    {
        string dll = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../../src/BuilderAssistant/bin/Release/net472/BuilderAssistant.dll"));
        Assembly mod = Assembly.LoadFrom(dll);
        var selected = new List<Type>();
        foreach (Type type in mod.GetTypes())
        {
            string ns = type.Namespace ?? "";
            if (ns == "BuilderAssistant.Networking" || ns == "BuilderAssistant.Core.Json" || ns == "BuilderAssistant.Core.Blueprints" || ns == "BuilderAssistant.Core.Furniture" || type.FullName.StartsWith("BuilderAssistant.Infrastructure.BlueprintStorage", StringComparison.Ordinal)) selected.Add(type);
        }
        Check(selected.Count > 15, "cloud audit covers transport, async state machines, JSON, import, furniture and storage");
        List<string> blocked = CloudForbiddenMembers(selected);
        Check(blocked.Count == 0, "shipping cloud path has no known missing Android members: " + string.Join("; ", blocked));
        Check(CloudForbiddenMembers(new[] { typeof(LegacyCloudParseProbe) }).Count > 0, "audit rejects the legacy Int32.TryParse pattern");
        string legacy = Environment.GetEnvironmentVariable("BUILDER_CLOUD_LEGACY_DLL");
        if (!string.IsNullOrEmpty(legacy))
        {
            Assembly previous = Assembly.Load(File.ReadAllBytes(legacy));
            var types = new List<Type>();
            foreach (Type type in previous.GetTypes()) if (type.Namespace == "BuilderAssistant.Networking" || type.Namespace == "BuilderAssistant.Core.Json") types.Add(type);
            List<string> oldBlocked = CloudForbiddenMembers(types);
            Check(oldBlocked.Count >= 3, "actual 0.25.0 DLL is rejected by the new Android guard");
            Console.WriteLine("Cloud legacy negative control: " + oldBlocked.Count + " forbidden references found.");
        }
        Console.WriteLine("Cloud shipping IL audit: " + selected.Count + " types, no known missing System.TryParse/Guid.Parse/LINQ/List helpers.");
        if (Environment.GetEnvironmentVariable("BUILDER_CLOUD_LIVE") == "1") TestShippingCloudLive(mod);
    }
    private static List<string> CloudForbiddenMembers(IEnumerable<Type> types)
    {
        var codes = new Dictionary<ushort, OpCode>();
        foreach (FieldInfo field in typeof(OpCodes).GetFields()) if (field.FieldType == typeof(OpCode)) { var op = (OpCode)field.GetValue(null); codes[(ushort)op.Value] = op; }
        var blocked = new List<string>();
        foreach (Type type in types)
        {
            const BindingFlags flags = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            var methods = new List<MethodBase>(type.GetMethods(flags)); methods.AddRange(type.GetConstructors(flags));
            foreach (MethodBase method in methods)
            {
                MethodBody body = method.GetMethodBody(); if (body == null) continue;
                byte[] il = body.GetILAsByteArray();
                for (int i = 0; i < il.Length;)
                {
                    ushort code = il[i++]; if (code == 0xfe) code = (ushort)(0xfe00 | il[i++]); OpCode op = codes[code]; int length;
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
                        MemberInfo member = method.Module.ResolveMember(BitConverter.ToInt32(il, i), type.IsGenericType ? type.GetGenericArguments() : null, method.IsGenericMethod ? method.GetGenericArguments() : null);
                        Type owner = member.DeclaringType ?? member as Type; string name = owner == null ? "" : owner.FullName ?? owner.Name;
                        bool forbidden = name.StartsWith("System.", StringComparison.Ordinal) && (member.Name.StartsWith("TryParse", StringComparison.Ordinal) || name.StartsWith("System.Linq.Enumerable", StringComparison.Ordinal));
                        if (owner == typeof(Guid) && member.Name.StartsWith("Parse", StringComparison.Ordinal)) forbidden = true;
                        if (owner == typeof(CancellationTokenSource) && (member.Name == "CreateLinkedTokenSource" || member.Name == "CancelAfter")) forbidden = true;
                        if (owner != null && owner.IsGenericType && owner.GetGenericTypeDefinition() == typeof(List<>))
                            foreach (string helper in new[] { "Exists", "Find", "FindAll", "FindIndex", "FindLast", "FindLastIndex", "RemoveAll", "Sort", "TrueForAll", "ForEach", "ConvertAll", "BinarySearch" }) if (member.Name == helper) forbidden = true;
                        if (forbidden) blocked.Add(type.FullName + "." + method.Name + " -> " + name + "." + member.Name);
                    }
                    i += length;
                }
            }
        }
        return blocked;
    }
    private static void TestShippingCloudLive(Assembly mod)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        string directory = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../../artifacts/blueprint-live-net472"));
        Type cacheType = mod.GetType("BuilderAssistant.Networking.BlueprintCloudCache", true);
        object cache = Activator.CreateInstance(cacheType, flags, null, new object[] { directory }, null);
        Type clientType = mod.GetType("BuilderAssistant.Networking.BlueprintCloudClient", true);
        object client = Activator.CreateInstance(clientType, flags, null, new object[] { cache, null, new Action<string>(Console.WriteLine) }, null);
        Task task = (Task)clientType.GetMethod("List", flags).Invoke(client, new object[] { 1, CancellationToken.None, false });
        task.GetAwaiter().GetResult(); object result = task.GetType().GetProperty("Result").GetValue(task, null);
        object page = result.GetType().GetField("Value", flags).GetValue(result);
        string error = (string)result.GetType().GetField("Error", flags).GetValue(result);
        Check(page != null, "shipping net472 client accepts the real HTTPS catalog: " + error);
        Check(!(bool)result.GetType().GetField("Offline", flags).GetValue(result), "live shipping probe uses HTTPS rather than a cache fallback");
        int total = (int)page.GetType().GetField("Total", flags).GetValue(page);
        Console.WriteLine("Shipping net472 live HTTPS catalog: total=" + total + "; no game/world loaded.");
    }
}

// Only in the desktop test executable, never in the Mod.
internal static class LegacyCloudParseProbe
{
    internal static bool Parse(string value, out int result) { return int.TryParse(value, out result); }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using SCIEPort.Features.CelestialCodex.Core;
using Engine;

namespace SCIEPort.Features.CelestialCodex.Mod
{
    /// <summary>
    /// Optional adapter for newer engine builds that expose Storage.ChooseFile.
    /// SCNET x25.01.17 has no verified signature for this API. Never statically
    /// link to newer Survivalcraft API or Android assemblies.
    /// </summary>
    internal static class BookFilePicker
    {
        private static readonly string[] Extensions = { ".txt", ".epub" };

        internal static bool TryChoose(Action<string> onChosen)
        {
            if (onChosen == null) return false;
            // Run on the game/UI thread: a native picker must not start from Task.Run.
            foreach (MethodInfo method in typeof(Storage).GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (method.Name != "ChooseFile" || method.ContainsGenericParameters) continue;
                ParameterInfo[] parameters = method.GetParameters();
                object[] args = new object[parameters.Length];
                bool callbackSeen = false, compatible = parameters.Length <= 5;
                for (int i = 0; i < parameters.Length && compatible; i++)
                {
                    Type t = parameters[i].ParameterType;
                    if (t == typeof(Action<string>) && !callbackSeen)
                    {
                        args[i] = onChosen;
                        callbackSeen = true;
                    }
                    else if (t == typeof(string[])) args[i] = Extensions;
                    else if (t == typeof(string))
                    {
                        string label = parameters[i].Name ?? "";
                        args[i] = label.IndexOf("filter", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            label.IndexOf("extension", StringComparison.OrdinalIgnoreCase) >= 0
                            ? "*.txt;*.epub" : "导入图书";
                    }
                    else if (parameters[i].IsOptional) args[i] = parameters[i].DefaultValue;
                    else compatible = false;
                }
                bool returnsTask = typeof(Task<string>).IsAssignableFrom(method.ReturnType);
                bool returnsString = method.ReturnType == typeof(string);
                if (!compatible || (!callbackSeen && !returnsTask && !returnsString)) continue;
                try
                {
                    object value = method.Invoke(null, args);
                    if (returnsTask)
                    {
                        Task<string> task = value as Task<string>;
                        if (task != null) CompleteSelection(task,onChosen);
                    }
                    else if (returnsString) onChosen(value as string);
                    return true;
                }
                catch (TargetInvocationException) { /* Unsupported by this game host; fall back. */ }
                catch (Exception) { /* Do not crash the reader on an optional bridge. */ }
            }
            return false;
        }

        private static async void CompleteSelection(Task<string> task,Action<string> onChosen)
        {
            string path=null;
            try { path=await task.ConfigureAwait(false); } catch(Exception) { }
            try { onChosen(path); } catch(Exception) { }
        }

        internal static string Root { get { return "app:/"; } }

        internal static string Parent(string path)
        {
            if (string.IsNullOrEmpty(path) || path == Root) return Root;
            string trimmed = path.TrimEnd('/');
            int i = trimmed.LastIndexOf('/');
            return i < Root.Length - 1 ? Root : trimmed.Substring(0, i + 1);
        }

        internal static string Child(string directory, string name)
        {
            if (!SafeComponent(name) || string.IsNullOrEmpty(directory) ||
                !directory.StartsWith(Root, StringComparison.OrdinalIgnoreCase) ||
                directory.IndexOf("..", StringComparison.Ordinal) >= 0)
                throw new ArgumentException("Invalid virtual file path");
            string result = Storage.CombinePaths(directory, name);
            if (!result.StartsWith(Root, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Virtual path outside app storage");
            return result;
        }

        internal static bool SafeComponent(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length > 180 ||
                name == "." || name == ".." || name.IndexOf('/') >= 0 ||
                name.IndexOf('\\') >= 0 || name.IndexOf(':') >= 0)
                return false;
            foreach (char c in name) if (char.IsControl(c)) return false;
            return true;
        }

        internal static List<string> Directories(string path)
        {
            var output = new List<string>();
            // ListDirectoryNames is not part of the proven SCNET ABI, so probe it.
            MethodInfo method = typeof(Storage).GetMethod("ListDirectoryNames",
                BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
            if (method == null) return output;
            try
            {
                object values = method.Invoke(null, new object[] { path });
                IEnumerable sequence = values as IEnumerable;
                if (sequence == null) return output;
                int seen = 0;
                foreach (object item in sequence)
                {
                    if (++seen > 512) break;
                    string name = item as string;
                    if (SafeComponent(name)) output.Add(name);
                }
            }
            catch (Exception) { /* Browser remains available for root files. */ }
            for (int i = 1; i < output.Count; i++)
            {
                string value = output[i]; int j = i - 1;
                while (j >= 0 && StringComparer.OrdinalIgnoreCase.Compare(output[j], value) > 0)
                { output[j + 1] = output[j]; j--; }
                output[j + 1] = value;
            }
            return output;
        }

        internal static List<string> BookFiles(string path)
        {
            var output = new List<string>();
            try
            {
                int seen = 0;
                foreach (string name in Storage.ListFileNames(path))
                {
                    if (++seen > 512) break;
                    if (SafeComponent(name) && BookImportService.Supported(name)) output.Add(name);
                }
            }
            catch (Exception) { /* Surface empty folder, not an unhandled UI crash. */ }
            for (int i = 1; i < output.Count; i++)
            {
                string value = output[i]; int j = i - 1;
                while (j >= 0 && StringComparer.OrdinalIgnoreCase.Compare(output[j], value) > 0)
                { output[j + 1] = output[j]; j--; }
                output[j + 1] = value;
            }
            return output;
        }
    }
}

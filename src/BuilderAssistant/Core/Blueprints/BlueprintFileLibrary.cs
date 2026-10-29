using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BuilderAssistant.Core.Blueprints
{
    /// <summary>Game-independent file operations, scoped to one blueprint directory.</summary>
    internal sealed class BlueprintFileLibrary
    {
        private readonly string directory;
        public BlueprintFileLibrary(string directory) { this.directory = directory; }

        private string FilePath(string stem)
        {
            // Never sanitize a selected name into a DIFFERENT file when loading/deleting.
            if (string.IsNullOrEmpty(stem) || stem.IndexOfAny(new[] { '/', '\\', ':' }) >= 0 ||
                stem == "." || stem == ".." || Path.GetFileName(stem) != stem)
                throw new IOException("蓝图文件名无效。");
            return Path.Combine(directory, stem + ".json");
        }

        public List<string> ListNames(out string error)
        {
            error = null;
            var names = new List<string>();
            if (string.IsNullOrEmpty(directory)) { error = "游戏尚未提供用户数据目录。"; return names; }
            try
            {
                if (Directory.Exists(directory))
                    foreach (string f in Directory.GetFiles(directory, "*.json")) names.Add(Path.GetFileNameWithoutExtension(f));
                names.Sort(StringComparer.Ordinal);
            }
            catch (Exception e) { error = "列举蓝图失败：" + e.Message; }
            return names;
        }

        public string Save(string name, string json, out string filename)
        {
            filename = null;
            if (string.IsNullOrEmpty(directory)) return "游戏尚未提供用户数据目录。";
            try
            {
                Directory.CreateDirectory(directory);
                string error, collision;
                var existing = ListNames(out error);
                if (error != null) return error;
                string stem = BlueprintNamePolicy.ResolveUnique(name, existing, out collision);
                string path = FilePath(stem);
                // Write fully before publishing the .json so interrupted saves cannot appear valid.
                string pending = Path.Combine(directory, "." + Guid.NewGuid().ToString("N") + ".tmp");
                try
                {
                    File.WriteAllText(pending, json, new UTF8Encoding(false));
                    File.Move(pending, path); // fails rather than overwriting, including races
                }
                finally { if (File.Exists(pending)) File.Delete(pending); }
                filename = stem + ".json";
                return null;
            }
            catch (Exception e) { return "保存蓝图失败：" + e.Message; }
        }

        public string Load(string stem, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(directory)) { error = "游戏尚未提供用户数据目录。"; return null; }
            try
            {
                string path = FilePath(stem);
                if (!File.Exists(path)) { error = "找不到蓝图文件：" + stem + ".json"; return null; }
                // UTF-8 uses at most four bytes per character; reject oversized disk input before allocation.
                if (new FileInfo(path).Length > BlueprintImportService.MaxImportChars * 4L)
                { error = "蓝图文件过大。"; return null; }
                return File.ReadAllText(path, Encoding.UTF8);
            }
            catch (Exception e) { error = "读取蓝图失败：" + e.Message; return null; }
        }

        public string Delete(string stem)
        {
            if (string.IsNullOrEmpty(directory)) return "游戏尚未提供用户数据目录。";
            try
            {
                string path = FilePath(stem);
                if (!File.Exists(path)) return "找不到蓝图文件：" + stem + ".json";
                File.Delete(path);
                return null;
            }
            catch (Exception e) { return "删除蓝图失败：" + e.Message; }
        }
    }
}

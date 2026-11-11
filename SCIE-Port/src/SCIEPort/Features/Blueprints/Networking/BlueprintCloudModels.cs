using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using SCIEPort.Features.Blueprints.Core.Blueprints;
using SCIEPort.Features.Blueprints.Core.Json;

namespace SCIEPort.Features.Blueprints.Networking
{
    internal sealed class CloudBlueprintEntry
    {
        internal string Id, Sha256, Name;
        internal int Revision, SchemaVersion, SizeX, SizeY, SizeZ, BlockCount, ByteLength;
        internal string Key { get { return Id + "-r" + Revision + "-" + Sha256; } }
        internal void Match(Blueprint bp)
        {
            if (bp.SchemaVersion != SchemaVersion || bp.SizeX != SizeX || bp.SizeY != SizeY || bp.SizeZ != SizeZ || bp.CellCount != BlockCount || bp.Name != Name)
                throw new InvalidDataException("蓝图与云端版本信息不一致。");
        }
    }
    internal sealed class CloudBlueprintPage
    {
        internal int Page, Total;
        internal readonly List<CloudBlueprintEntry> Items = new List<CloudBlueprintEntry>();
    }
    internal sealed class CloudResult<T> where T : class
    {
        internal T Value;
        internal bool Offline, CacheFailed;
        internal string Error;
    }
    internal sealed class CloudBlueprintDownload
    {
        internal Blueprint Blueprint;
        internal string Json;
        internal CloudBlueprintEntry Entry;
    }
    internal static class BlueprintCloudProtocol
    {
        internal const int MaxBytes = 2 * 1024 * 1024, PageSize = 20;
        internal static string Hash(byte[] bytes)
        {
            using (var sha = SHA256.Create())
            { var b = new StringBuilder(); foreach (byte value in sha.ComputeHash(bytes)) b.Append(value.ToString("x2")); return b.ToString(); }
        }
        internal static bool Hex(string text, int length)
        {
            if (text == null || text.Length != length) return false;
            foreach (char c in text) if (!(c >= '0' && c <= '9') && !(c >= 'a' && c <= 'f')) return false;
            return true;
        }
        internal static bool Id(string id)
        {
            if (id == null || id.Length != 36 || id[14] != '4' || "89ab".IndexOf(id[19]) < 0) return false;
            for (int i = 0; i < id.Length; i++)
                if (i == 8 || i == 13 || i == 18 || i == 23) { if (id[i] != '-') return false; }
                else if (!(id[i] >= '0' && id[i] <= '9') && !(id[i] >= 'a' && id[i] <= 'f')) return false;
            return true;
        }
        internal static JsonValue Parse(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0 || bytes.Length > MaxBytes) throw new InvalidDataException("响应长度无效。");
            string text = new UTF8Encoding(false, true).GetString(bytes);
            // Guard the existing recursive JSON reader before entering it, including unknown fields.
            int depth = 0; bool quoted = false, escaped = false;
            foreach (char c in text)
            {
                if (quoted) { if (escaped) escaped = false; else if (c == '\\') escaped = true; else if (c == '"') quoted = false; }
                else if (c == '"') quoted = true;
                else if (c == '{' || c == '[') { if (++depth > 32) throw new InvalidDataException("JSON 嵌套过深。"); }
                else if (c == '}' || c == ']') depth--;
            }
            JsonValue root = JsonValue.Parse(text); Unique(root);
            if (!root.IsObject) throw new InvalidDataException("响应需要 JSON 对象。");
            return root;
        }
        private static void Unique(JsonValue node)
        {
            if (node.IsObject)
            {
                var keys = new HashSet<string>(StringComparer.Ordinal);
                foreach (var member in node.Members) { if (!keys.Add(member.Key)) throw new InvalidDataException("JSON 字段重复。"); Unique(member.Value); }
            }
            else if (node.IsArray) foreach (JsonValue value in node.Items) Unique(value);
        }
        internal static int Integer(JsonValue node, string key, int min, int max)
        { int value; if (node == null || !node.TryGetInt(key, out value) || value < min || value > max) throw new InvalidDataException("版本或元信息无效：" + key); return value; }
        private static string String(JsonValue node, string key, int max)
        {
            string value;
            if (node == null || !node.TryGetString(key, out value) || string.IsNullOrWhiteSpace(value) || value.Length > max) throw new InvalidDataException("元信息无效：" + key);
            foreach (char c in value) if (c < 32) throw new InvalidDataException("元信息含控制字符。");
            return value;
        }
        internal static CloudBlueprintEntry Entry(JsonValue node)
        {
            Integer(node, "apiVersion", 1, 1);
            if (String(node, "status", 16) != "published" || String(node, "kind", 80) != BlueprintJson.KindConstant) throw new InvalidDataException("蓝图未发布或类型不支持。");
            var entry = new CloudBlueprintEntry { Id = String(node, "id", 36), Sha256 = String(node, "sha256", 64), Name = String(node, "name", 160),
                Revision = Integer(node, "revision", 1, int.MaxValue - 1), SchemaVersion = Integer(node, "schemaVersion", 1, 3),
                BlockCount = Integer(node, "blockCount", 1, 10000), ByteLength = Integer(node, "byteLength", 1, MaxBytes),
                SizeX = Integer(node.Get("size"), "x", 1, 64), SizeY = Integer(node.Get("size"), "y", 1, 64), SizeZ = Integer(node.Get("size"), "z", 1, 64) };
            if (!Id(entry.Id) || !Hex(entry.Sha256, 64)) throw new InvalidDataException("蓝图 ID 或 SHA-256 无效。");
            return entry;
        }
        internal static CloudBlueprintPage Page(byte[] bytes, int page)
        {
            JsonValue root = Parse(bytes); Integer(root, "apiVersion", 1, 1); Integer(root, "page", page, page); Integer(root, "pageSize", PageSize, PageSize);
            var result = new CloudBlueprintPage { Page = page, Total = Integer(root, "total", 0, 200000) };
            JsonValue items = root.Get("items");
            int expected = Math.Min(PageSize, Math.Max(0, result.Total - (page - 1) * PageSize));
            if (items == null || !items.IsArray || items.Items.Count != expected) throw new InvalidDataException("蓝图库分页信息无效。");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonValue item in items.Items) { var entry = Entry(item); if (!ids.Add(entry.Id)) throw new InvalidDataException("重复蓝图 ID。"); result.Items.Add(entry); }
            return result;
        }
        internal static CloudBlueprintDownload Download(byte[] bytes, CloudBlueprintEntry entry)
        {
            if (bytes == null || bytes.Length != entry.ByteLength || Hash(bytes) != entry.Sha256) throw new InvalidDataException("蓝图 SHA-256 校验失败，请刷新后重试。");
            JsonValue root = Parse(bytes); // Only guards encoding/depth/duplicates; never interprets blueprint cells here.
            if (Integer(root, "schemaVersion", 1, 3) != entry.SchemaVersion) throw new InvalidDataException("蓝图版本不一致。");
            if (String(root, "kind", 80) != BlueprintJson.KindConstant) throw new InvalidDataException("蓝图类型不一致。");
            string json = new UTF8Encoding(false, true).GetString(bytes);
            BlueprintImportResult parsed = BlueprintImportService.FromString(json, null);
            if (!parsed.Success) throw new InvalidDataException(parsed.Detail);
            entry.Match(parsed.Blueprint);
            return new CloudBlueprintDownload { Blueprint = parsed.Blueprint, Json = json, Entry = entry };
        }
    }
}

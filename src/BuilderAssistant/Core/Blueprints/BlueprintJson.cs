using System;
using System.Collections.Generic;
using System.Text;
using BuilderAssistant.Core.Json;
using BuilderAssistant.Core.Safety;

namespace BuilderAssistant.Core.Blueprints
{
    /// <summary>
    /// The single generic blueprint exchange format (JSON) that turns a pure
    /// <see cref="Blueprint"/> into portable bytes and back. This is the bridge
    /// between "reliable copy / paste / hand-authored structures" and the future
    /// AI-generated buildings: everything downstream reuses the SAME
    /// Blueprint + BuildPlanner + BuildTask + undo path, so a blueprint that
    /// round-trips through JSON reproduces the exact same block list.
    ///
    /// schemaVersion 1 model:
    ///   {
    ///     "schemaVersion": 1,
    ///     "kind": "builderassistant.blueprint",
    ///     "name": "…",
    ///     "size": { "x":…, "y":…, "z":… },         // advisory; authoritatively recomputed
    ///     "materials": [ { "id":0, "key":"StoneBase", "block":"CobblestoneBlock" }, … ],
    ///     "cells": [ { "x":.., "y":.., "z":.., "m":0 }, … ]   // relative, integer
    ///   }
    ///
    /// Forward-compatibility is *reserved* but never silently faked: if a file
    /// carries per-cell orientation/data/property payloads or a buildOps list that
    /// this schema cannot reproduce, Decode rejects it with a clear reason instead
    /// of placing wrong blocks (AGENTS.md: no garbage writes).
    /// </summary>
    internal static class BlueprintJson
    {
        public const int CurrentSchemaVersion = 1;
        public const string KindConstant = "builderassistant.blueprint";

        /// <summary>Serialize a blueprint to deterministic, stable JSON text.</summary>
        public static string Serialize(Blueprint blueprint)
        {
            if (blueprint == null) throw new ArgumentNullException("blueprint");

            // Assign a stable material id per distinct key, in first-appearance order
            // over the (already deterministically sorted) cell list.
            var keyToId = new Dictionary<string, int>(StringComparer.Ordinal);
            var orderedKeys = new List<string>();
            for (int i = 0; i < blueprint.Cells.Count; i++)
            {
                string key = blueprint.Cells[i].MaterialKey ?? string.Empty;
                int id;
                if (!keyToId.TryGetValue(key, out id))
                {
                    id = orderedKeys.Count;
                    keyToId[key] = id;
                    orderedKeys.Add(key);
                }
            }

            var root = JsonValue.NewObject();
            root.Add("schemaVersion", JsonValue.NewNumber(CurrentSchemaVersion));
            root.Add("kind", JsonValue.NewString(KindConstant));
            root.Add("name", JsonValue.NewString(blueprint.Name ?? string.Empty));

            var size = JsonValue.NewObject();
            size.Add("x", JsonValue.NewNumber(blueprint.SizeX));
            size.Add("y", JsonValue.NewNumber(blueprint.SizeY));
            size.Add("z", JsonValue.NewNumber(blueprint.SizeZ));
            root.Add("size", size);

            var materials = JsonValue.NewArray();
            for (int i = 0; i < orderedKeys.Count; i++)
            {
                string key = orderedKeys[i];
                var mat = JsonValue.NewObject();
                mat.Add("id", JsonValue.NewNumber(i));
                mat.Add("key", JsonValue.NewString(key));
                // A logical key has no concrete block yet; a copied blueprint uses the
                // vanilla block type name AS its key, so block == key for copies. For
                // generated blueprints block is left empty and resolved at build time.
                mat.Add("block", JsonValue.NewString(IsBlockTypeName(key) ? key : string.Empty));
                materials.Add(mat);
            }
            root.Add("materials", materials);

            var cells = JsonValue.NewArray();
            for (int i = 0; i < blueprint.Cells.Count; i++)
            {
                BlueprintCell c = blueprint.Cells[i];
                var cell = JsonValue.NewObject();
                cell.Add("x", JsonValue.NewNumber(c.Relative.X));
                cell.Add("y", JsonValue.NewNumber(c.Relative.Y));
                cell.Add("z", JsonValue.NewNumber(c.Relative.Z));
                cell.Add("m", JsonValue.NewNumber(keyToId[c.MaterialKey ?? string.Empty]));
                cells.Add(cell);
            }
            root.Add("cells", cells);

            return root.ToJson();
        }

        /// <summary>
        /// Parse + validate JSON text into a blueprint. Never throws on bad content:
        /// returns a structured result. Malformed JSON, unknown schema, duplicate or
        /// fractional coordinates, dangling material ids, empty sets, over-cap counts
        /// or reserved-but-unsupported payloads all fail with a clear message and no
        /// partial blueprint.
        /// </summary>
        public static BlueprintJsonDecodeResult Decode(string json)
        {
            var result = new BlueprintJsonDecodeResult();

            if (string.IsNullOrEmpty(json))
            {
                result.AddError("蓝图内容为空。");
                return result;
            }

            JsonValue root;
            try
            {
                root = JsonValue.Parse(json);
            }
            catch (JsonParseException ex)
            {
                result.AddError("JSON 解析失败：" + ex.Message);
                return result;
            }

            if (!root.IsObject)
            {
                result.AddError("蓝图根节点必须是 JSON 对象。");
                return result;
            }

            // --- schemaVersion ---
            int version;
            if (!root.TryGetInt("schemaVersion", out version))
            {
                result.AddError("缺少或非法的 schemaVersion（必须为整数）。");
                return result;
            }
            if (version > CurrentSchemaVersion)
            {
                result.AddError("蓝图 schemaVersion=" + version + " 高于本 Mod 支持的 " + CurrentSchemaVersion + "，请升级 Mod 而非降级重建（拒绝劣质生成）。");
                return result;
            }
            if (version < 1)
            {
                result.AddError("蓝图 schemaVersion=" + version + " 无效。");
                return result;
            }

            // --- kind (advisory) ---
            string kind;
            if (root.TryGetString("kind", out kind) && !string.IsNullOrEmpty(kind) &&
                !string.Equals(kind, KindConstant, StringComparison.Ordinal))
            {
                result.AddError("蓝图 kind=" + kind + " 不是本 Mod 的建筑蓝图格式。");
                return result;
            }

            // --- name ---
            string name;
            if (!root.TryGetString("name", out name) || string.IsNullOrEmpty(name))
            {
                name = "blueprint";
            }

            // --- material table: id -> key ---
            var idToKey = new Dictionary<int, string>();
            JsonValue materials = root.Get("materials");
            if (materials != null && materials.IsArray)
            {
                for (int i = 0; i < materials.Items.Count; i++)
                {
                    JsonValue mat = materials.Items[i];
                    if (!mat.IsObject) { result.AddError("materials[" + i + "] 必须是对象。"); continue; }
                    int id;
                    if (!mat.TryGetInt("id", out id)) { result.AddError("materials[" + i + "] 缺少整数 id。"); continue; }
                    string key;
                    if (!mat.TryGetString("key", out key) || string.IsNullOrEmpty(key))
                    {
                        result.AddError("materials[" + i + "] 缺少非空 key。");
                        continue;
                    }
                    if (idToKey.ContainsKey(id))
                    {
                        result.AddError("materials 存在重复 id " + id + "。");
                        continue;
                    }
                    idToKey[id] = key;
                }
            }

            // --- cells ---
            JsonValue cells = root.Get("cells");
            if (cells == null || !cells.IsArray)
            {
                result.AddError("缺少 cells 数组。");
                return result;
            }
            if (cells.Items.Count == 0)
            {
                result.AddError("cells 为空，蓝图没有任何方块。");
                return result;
            }
            if (cells.Items.Count > BuildSafetyLimits.MaxBlocksPerOperation)
            {
                result.AddError("cells 数量 " + cells.Items.Count + " 超过每事务上限 " + BuildSafetyLimits.MaxBlocksPerOperation + "。");
                return result;
            }

            var seen = new HashSet<CellPos>();
            var list = new List<BlueprintCell>(cells.Items.Count);
            for (int i = 0; i < cells.Items.Count; i++)
            {
                JsonValue cell = cells.Items[i];
                if (!cell.IsObject) { result.AddError("cells[" + i + "] 必须是对象。"); continue; }

                int x, y, z;
                if (!cell.TryGetInt("x", out x) || !cell.TryGetInt("y", out y) || !cell.TryGetInt("z", out z))
                {
                    result.AddError("cells[" + i + "] 的 x/y/z 必须是整数。");
                    continue;
                }

                // Reserved compatibility: orientation / data / property payloads that
                // schema 1 cannot reproduce must be rejected loudly, never guessed.
                if (HasUnsupportedCellFeature(cell))
                {
                    result.AddError("cells[" + i + "] 含本版本无法复现的朝向/数据/复杂方块属性（o/d/props），已拒绝以避免劣质生成。");
                    continue;
                }

                var pos = new CellPos(x, y, z);
                if (!seen.Add(pos))
                {
                    result.AddError("cells[" + i + "] 坐标重复 " + pos + "。");
                    continue;
                }

                string materialKey;
                int mid;
                if (cell.TryGetInt("m", out mid))
                {
                    if (!idToKey.TryGetValue(mid, out materialKey))
                    {
                        result.AddError("cells[" + i + "] 引用了不存在的材料 id " + mid + "。");
                        continue;
                    }
                }
                else if (cell.TryGetString("key", out materialKey) && !string.IsNullOrEmpty(materialKey))
                {
                    // inline material, allowed
                }
                else
                {
                    result.AddError("cells[" + i + "] 缺少材料（m 或 key）。");
                    continue;
                }

                list.Add(new BlueprintCell(pos, materialKey));
            }

            if (result.HasErrors)
            {
                return result; // hard-stop: do not build a partial/invalid blueprint
            }

            // Top-level reserved build operations are not executable in schema 1.
            JsonValue buildOps = root.Get("buildOps");
            if (buildOps != null && buildOps.IsArray && buildOps.Items.Count > 0)
            {
                result.AddError("蓝图包含本版本不支持的 buildOps（" + buildOps.Items.Count + " 项），已拒绝以避免部分执行。");
                return result;
            }

            Blueprint bp = Blueprint.Normalize(name, version, list);

            if (bp.CellCount > BuildSafetyLimits.MaxBlocksPerOperation)
            {
                result.AddError("规范化后方块数量 " + bp.CellCount + " 超过上限。");
                return result;
            }
            if (bp.SizeX > BuildSafetyLimits.MaxSelectionAxisLength ||
                bp.SizeY > BuildSafetyLimits.MaxSelectionAxisLength ||
                bp.SizeZ > BuildSafetyLimits.MaxSelectionAxisLength)
            {
                result.AddError("蓝图尺寸超过单轴上限 " + BuildSafetyLimits.MaxSelectionAxisLength + "（实际 " + bp.SizeX + "x" + bp.SizeY + "x" + bp.SizeZ + "）。");
                return result;
            }

            // Consistency: if the file declared a size, it must match the recomputed one.
            JsonValue declaredSize = root.Get("size");
            if (declaredSize != null && declaredSize.IsObject)
            {
                int sx, sy, sz;
                bool any = declaredSize.TryGetInt("x", out sx) |
                           declaredSize.TryGetInt("y", out sy) |
                           declaredSize.TryGetInt("z", out sz);
                if (any && (declaredSize.TryGetInt("x", out sx) && sx != bp.SizeX ||
                            declaredSize.TryGetInt("y", out sy) && sy != bp.SizeY ||
                            declaredSize.TryGetInt("z", out sz) && sz != bp.SizeZ))
                {
                    result.AddError("声明的 size 与 cells 实际包围盒不一致，已拒绝。");
                    return result;
                }
            }

            result.Blueprint = bp;
            return result;
        }

        private static bool HasUnsupportedCellFeature(JsonValue cell)
        {
            return cell.Get("o") != null || cell.Get("d") != null || cell.Get("props") != null;
        }

        /// <summary>
        /// Heuristic: does a material key look like a concrete vanilla block CLR type
        /// name (e.g. "CobblestoneBlock") rather than an abstract logical key
        /// (e.g. "StoneBase")? Only used to fill the advisory "block" field on save.
        /// </summary>
        private static bool IsBlockTypeName(string key)
        {
            return !string.IsNullOrEmpty(key) &&
                   key.Length > 5 &&
                   key.EndsWith("Block", StringComparison.Ordinal);
        }
    }

    /// <summary>Result of decoding blueprint JSON: either a valid Blueprint or errors.</summary>
    internal sealed class BlueprintJsonDecodeResult
    {
        private readonly List<string> _errors = new List<string>();

        public Blueprint Blueprint { get; set; }
        public bool Success { get { return _errors.Count == 0 && Blueprint != null; } }
        public bool HasErrors { get { return _errors.Count > 0; } }
        public IReadOnlyList<string> Errors { get { return _errors; } }
        public string FirstError { get { return _errors.Count > 0 ? _errors[0] : null; } }

        public void AddError(string message)
        {
            _errors.Add(message);
        }

        public string JoinedErrors()
        {
            return string.Join("; ", _errors.ToArray());
        }
    }
}

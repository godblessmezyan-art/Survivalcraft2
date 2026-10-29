using System;
using System.Collections.Generic;

namespace BuilderAssistant.Core.Blueprints
{
    /// <summary>
    /// Game-free entry point that turns a raw pasted / hand-authored JSON *string*
    /// straight into a validated <see cref="Blueprint"/>, reusing the exact same
    /// <see cref="BlueprintJson.Decode"/> parser + structural rules as file import
    /// (no second JSON implementation). An optional material-resolver callback lets the
    /// caller add its own "is this block buildable here?" pre-flight (the game passes
    /// its VanillaBlockPalette; offline tests pass a fake), so an unsupported blueprint
    /// is rejected up front with a precise reason.
    ///
    /// This class never touches terrain, files, or the game: it is pure and fully
    /// covered by the offline suite. On any failure it returns Success=false with a
    /// clear reason and a null blueprint, so callers can guarantee they never clobber
    /// the current clipboard on a bad paste.
    /// </summary>
    internal static class BlueprintImportService
    {
        /// <summary>
        /// Hard ceiling on the raw pasted text (in chars) before we even hand it to the
        /// parser. A legal blueprint is at most 10000 cells; this leaves
        /// generous headroom while stopping a pathological multi-megabyte paste from
        /// stalling the main thread inside the tokenizer.
        /// </summary>
        public const int MaxImportChars = 2 * 1024 * 1024;

        /// <param name="json">The full blueprint JSON text (clipboard paste / text box).</param>
        /// <param name="isMaterialSupported">
        /// Optional pre-flight: given a material key, return false if the target cannot
        /// rebuild it. When null, only structural validation runs.
        /// </param>
        public static BlueprintImportResult FromString(string json, Func<string, bool> isMaterialSupported)
        {
            var result = new BlueprintImportResult();

            if (string.IsNullOrWhiteSpace(json))
            {
                result.Fail("粘贴的文本为空。");
                return result;
            }

            string trimmed = json.Trim();
            if (trimmed.Length == 0)
            {
                result.Fail("粘贴的文本为空。");
                return result;
            }

            // Cheap shape gate before spending a tokenize on obviously-not-JSON text.
            if (trimmed[0] != '{')
            {
                result.Fail("内容不像蓝图 JSON（应以 { 开头的对象）。");
                return result;
            }

            if (trimmed.Length > MaxImportChars)
            {
                result.Fail("输入过大（" + trimmed.Length + " 字符，上限 " + MaxImportChars + "），可能是误粘贴，已拒绝。");
                return result;
            }

            // Reuse the single canonical parser + structural validator. No re-implementation.
            BlueprintJsonDecodeResult dec = BlueprintJson.Decode(json);
            if (!dec.Success)
            {
                result.Fail(dec.FirstError ?? "蓝图解析失败。", dec.JoinedErrors());
                return result;
            }

            Blueprint bp = dec.Blueprint;

            // Material pre-flight (de-duplicated keys), mirroring the paste-time check so
            // the player learns about unsupported blocks now instead of mid-build.
            if (isMaterialSupported != null)
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                var bad = new List<string>();
                for (int i = 0; i < bp.Cells.Count; i++)
                {
                    string key = bp.Cells[i].MaterialKey ?? string.Empty;
                    if (seen.Add(key) && !isMaterialSupported(key))
                    {
                        bad.Add(key.Length == 0 ? "(空材料)" : key);
                    }
                }
                if (bad.Count > 0)
                {
                    result.Fail("蓝图含本版本尚不能重建的方块（" + string.Join("、", bad.ToArray()) +
                        "）。为避免劣质生成未载入。");
                    return result;
                }
            }

            result.Ok(bp);
            return result;
        }
    }

    /// <summary>Outcome of a text/paste import: either a validated blueprint or a reason.</summary>
    internal sealed class BlueprintImportResult
    {
        public bool Success;
        public Blueprint Blueprint;
        /// <summary>Short, single-line reason suitable for a toast.</summary>
        public string Reason;
        /// <summary>Full joined detail for logs when several problems exist.</summary>
        public string Detail;
        public string DisplayName;
        public int SizeX;
        public int SizeY;
        public int SizeZ;
        public int BlockCount;

        internal void Fail(string reason, string detail = null)
        {
            Success = false;
            Blueprint = null;
            Reason = reason;
            Detail = detail ?? reason;
        }

        internal void Ok(Blueprint bp)
        {
            Success = true;
            Blueprint = bp;
            DisplayName = bp.Name;
            SizeX = bp.SizeX;
            SizeY = bp.SizeY;
            SizeZ = bp.SizeZ;
            BlockCount = bp.CellCount;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BuilderAssistant.Core.Blueprints;

namespace BuilderAssistant.Infrastructure
{
    /// <summary>
    /// Persists blueprint JSON documents to disk for the Mod, WITHOUT hardcoding any
    /// device path. The base directory is taken from the game's own resolved user-data
    /// location (<see cref="ModsManager.UserDataPath"/>) and a Mod-private subfolder is
    /// created beneath it, so the exact root is whatever SCNET/Android chose - we never
    /// assume "/sdcard/...". Every operation is defensive: a null/empty base path, a
    /// missing directory, or any IO exception is reported as a clear failure string
    /// rather than an unhandled crash, because on-device file access is the part we
    /// cannot prove from the desktop (Android: 待验证).
    ///
    /// The CONTENT itself (JSON to/from Blueprint) is fully covered by the offline test
    /// suite; this class only moves opaque text to and from a file.
    /// </summary>
    internal static class BlueprintStorage
    {
        private const string SubDir = "BuilderAssistant/blueprints";
        private const int MaxNameLength = 48;

        /// <summary>
        /// The game-provided root of this Mod's blueprint library, or null when the
        /// game has not supplied a user-data path yet.
        /// </summary>
        public static string BaseDirectory
        {
            get
            {
                string root = ModsManager.UserDataPath;
                if (string.IsNullOrEmpty(root)) return null;
                return Path.Combine(root, SubDir);
            }
        }

        /// <summary>
        /// Turn a user/AI/CJK display name into a safe, portable file stem. Delegated to
        /// <see cref="BlueprintNamePolicy"/>: ASCII characters are kept, and a pure-CJK
        /// name gets a stable short hash suffix so two different Chinese names no longer
        /// collapse to the same "blueprint" file and clobber each other. The readable
        /// name always stays inside the JSON "name" field regardless of the stem.
        /// </summary>
        public static string SanitizeName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "blueprint";
            return BlueprintNamePolicy.ToStem(raw);
        }

        /// <summary>
        /// Write blueprint JSON to the library WITHOUT ever overwriting an existing file.
        /// The directory is created on demand, the display name is mapped to a portable
        /// stem, and if that stem is already taken it is auto-numbered (_2, _3, ...) so a
        /// fresh blueprint cannot silently clobber a previous one. Returns null on success,
        /// else a failure message. <paramref name="savedFileName"/> is the actual written
        /// file name (including any collision suffix).
        /// </summary>
        public static string Save(string name, string json, out string savedFileName)
        {
            savedFileName = null;
            string dir = BaseDirectory;
            if (dir == null) return "游戏尚未提供用户数据目录，无法保存蓝图。";
            try
            {
                Directory.CreateDirectory(dir);

                var existing = new List<string>();
                foreach (string f in Directory.GetFiles(dir, "*.json"))
                {
                    existing.Add(Path.GetFileNameWithoutExtension(f));
                }

                string collided;
                string stem = BlueprintNamePolicy.ResolveUnique(name, existing, out collided);
                string path = Path.Combine(dir, stem + ".json");
                File.WriteAllText(path, json, new UTF8Encoding(false));
                savedFileName = stem + ".json";
                return null;
            }
            catch (Exception ex)
            {
                return "保存蓝图失败：" + ex.Message;
            }
        }

        /// <summary>Read a saved blueprint JSON by file stem. Returns null json on failure.</summary>
        public static string Load(string name, out string error)
        {
            error = null;
            string dir = BaseDirectory;
            if (dir == null) { error = "游戏尚未提供用户数据目录，无法读取蓝图。"; return null; }
            string safe = SanitizeName(name);
            string path = Path.Combine(dir, safe + ".json");
            try
            {
                if (!File.Exists(path)) { error = "找不到蓝图文件：" + safe + ".json"; return null; }
                return File.ReadAllText(path, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                error = "读取蓝图失败：" + ex.Message;
                return null;
            }
        }

        /// <summary>Enumerate saved blueprint file stems (without extension).</summary>
        public static List<string> ListNames(out string error)
        {
            error = null;
            var names = new List<string>();
            string dir = BaseDirectory;
            if (dir == null) { error = "游戏尚未提供用户数据目录。"; return names; }
            try
            {
                if (!Directory.Exists(dir)) return names;
                foreach (string file in Directory.GetFiles(dir, "*.json"))
                {
                    names.Add(Path.GetFileNameWithoutExtension(file));
                }
                names.Sort(StringComparer.Ordinal);
            }
            catch (Exception ex)
            {
                error = "列举蓝图失败：" + ex.Message;
            }
            return names;
        }

        /// <summary>Load a file and decode it straight into a validated blueprint.</summary>
        public static Blueprint LoadBlueprint(string name, out string error)
        {
            string json = Load(name, out error);
            if (json == null) return null;
            BlueprintJsonDecodeResult decode = BlueprintJson.Decode(json);
            if (!decode.Success)
            {
                error = decode.JoinedErrors();
                return null;
            }
            error = null;
            return decode.Blueprint;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace BuilderAssistant.Core.Blueprints
{
    /// <summary>
    /// Pure, game-free naming rules for the blueprint library. Turns a human
    /// (possibly CJK) display name into a safe, portable file stem and resolves
    /// collisions WITHOUT overwriting an existing file.
    ///
    /// This exists because the previous policy stripped every non-ASCII character,
    /// so an all-Chinese name collapsed to "blueprint" and two such blueprints
    /// silently overwrote each other while losing their readable name. Here the
    /// readable name always lives inside the JSON "name" field (preserved verbatim);
    /// the file stem is only the portable handle. Different display names therefore
    /// must never map to the same stem, and an existing stem is never clobbered.
    /// </summary>
    internal static class BlueprintNamePolicy
    {
        public const int MaxStemLength = 48;

        /// <summary>
        /// Keep [A-Za-z0-9_-] only, trimmed of leading/trailing separators and capped
        /// to <see cref="MaxStemLength"/>. Returns "" for a name with no portable chars
        /// (e.g. pure CJK) — callers must then use <see cref="ToStem"/> which adds a
        /// stable hash so distinct names stay distinct.
        /// </summary>
        public static string Sanitize(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;
            var sb = new StringBuilder();
            for (int i = 0; i < raw.Length && sb.Length < MaxStemLength; i++)
            {
                char c = raw[i];
                bool keep = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') ||
                            (c >= '0' && c <= '9') || c == '_' || c == '-';
                if (keep) sb.Append(c);
            }
            return sb.ToString().Trim('_', '-');
        }

        /// <summary>
        /// Deterministic portable file stem for a display name. If the name has any
        /// ASCII-safe characters they form the stem; if it is pure non-ASCII (CJK) we
        /// prefix a stable content hash so two different Chinese names yield two
        /// different stems (never both "blueprint").
        /// </summary>
        public static string ToStem(string displayName)
        {
            string baseStem = Sanitize(displayName);
            if (!string.IsNullOrEmpty(baseStem)) return baseStem;

            // Pure non-portable name (or empty): derive a short, stable, collision-
            // resistant hash of the ORIGINAL name. Keep the readable name in the JSON.
            string hash = StableHash(displayName ?? string.Empty);
            return "bp_" + hash;
        }

        /// <summary>
        /// Resolve a stem that does not collide with any already-present file stem.
        /// On a fresh name returns <see cref="ToStem"/> directly. If that stem is taken,
        /// appends _2, _3, ... (truncating the base so the total stays within
        /// <see cref="MaxStemLength"/>) until free. Never returns an existing stem, so
        /// the caller's write cannot overwrite. Returns null via <paramref name="collidedWith"/>
        /// the original stem when numbering kicked in (for messaging).
        /// </summary>
        public static string ResolveUnique(string displayName, IReadOnlyCollection<string> existingStems, out string collidedWith)
        {
            collidedWith = null;
            string stem = ToStem(displayName);
            if (!Contains(existingStems, stem)) return stem;

            collidedWith = stem;
            int n = 2;
            string candidate;
            do
            {
                string suffix = "_" + n;
                int keep = MaxStemLength - suffix.Length;
                if (keep < 1) keep = 1;
                string head = stem.Length > keep ? stem.Substring(0, keep) : stem;
                candidate = head.TrimEnd('_', '-') + suffix;
                n++;
            }
            while (Contains(existingStems, candidate) && n <= 1000);

            return candidate;
        }

        private static bool Contains(IReadOnlyCollection<string> set, string s)
        {
            if (set == null) return false;
            foreach (string x in set)
            {
                if (string.Equals(x, s, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        /// <summary>FNV-1a over the UTF-16 code units, rendered as 8 lowercase hex chars.</summary>
        public static string StableHash(string s)
        {
            uint h = 2166136261u;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                h ^= (uint)(c & 0xFF);
                h *= 16777619u;
                h ^= (uint)((c >> 8) & 0xFF);
                h *= 16777619u;
            }
            return h.ToString("x8");
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CelestialCodex.Core
{
    // Small bounded JSON reader: objects, arrays, UTF-16 strings, integer numbers, bool and null.
    // Protocol V1 contains no floating point numbers. No dynamic, reflection serializer or JSON DLL.
    internal sealed class StrictJson
    {
        private readonly string text;
        private int pos, nodes;
        private StrictJson(string text) { this.text = text; }
        internal static object Parse(byte[] bytes)
        {
            if (bytes == null || bytes.Length > Limits.MaxFileBytes) throw new InvalidDataException("JSON size");
            string text = new UTF8Encoding(false, true).GetString(bytes, 0, bytes.Length);
            var reader = new StrictJson(text);
            object value = reader.Value(0); reader.Space();
            if (reader.pos != text.Length) throw new InvalidDataException("Trailing JSON");
            return value;
        }
        private void Space() { while (pos < text.Length && (text[pos] == ' ' || text[pos] == '\n' || text[pos] == '\r' || text[pos] == '\t')) pos++; }
        private bool Eat(char c) { Space(); if (pos < text.Length && text[pos] == c) { pos++; return true; } return false; }
        private void Require(char c) { if (!Eat(c)) throw new InvalidDataException("JSON token"); }
        private object Value(int depth)
        {
            if (depth > 12 || ++nodes > 10000) throw new InvalidDataException("JSON complexity");
            Space(); if (pos == text.Length) throw new InvalidDataException("JSON end");
            char c = text[pos];
            if (c == '"') return String();
            if (Eat('{'))
            {
                var obj = new Dictionary<string, object>(StringComparer.Ordinal);
                if (Eat('}')) return obj;
                do { string key = String(); if (key.Length > 80 || obj.ContainsKey(key)) throw new InvalidDataException("JSON key"); Require(':'); obj.Add(key, Value(depth + 1)); } while (Eat(','));
                Require('}'); return obj;
            }
            if (Eat('['))
            {
                var array = new List<object>(); if (Eat(']')) return array;
                do { if (array.Count >= 512) throw new InvalidDataException("JSON array"); array.Add(Value(depth + 1)); } while (Eat(','));
                Require(']'); return array;
            }
            foreach (string word in new[] { "true", "false", "null" })
                if (pos + word.Length <= text.Length && string.CompareOrdinal(text, pos, word, 0, word.Length) == 0)
                { pos += word.Length; return word == "null" ? null : (object)(word == "true"); }
            bool negative = Eat('-'); int start = pos; long number = 0;
            while (pos < text.Length && text[pos] >= '0' && text[pos] <= '9')
            {
                if (pos - start >= 9) throw new InvalidDataException("JSON integer range");
                number = number * 10 + text[pos++] - '0';
            }
            if (pos == start || (pos - start > 1 && text[start] == '0')) throw new InvalidDataException("JSON number");
            return negative ? -number : number;
        }
        private int Hex()
        {
            if (pos + 4 > text.Length) throw new InvalidDataException("JSON unicode");
            int value = 0;
            for (int i = 0; i < 4; i++) { char c = text[pos++]; int n = c >= '0' && c <= '9' ? c - '0' : c >= 'a' && c <= 'f' ? c - 'a' + 10 : c >= 'A' && c <= 'F' ? c - 'A' + 10 : -1; if (n < 0) throw new InvalidDataException("JSON unicode"); value = value * 16 + n; }
            return value;
        }
        private string String()
        {
            Require('"'); var result = new StringBuilder(); bool ended = false;
            while (pos < text.Length)
            {
                char c = text[pos++]; if (c == '"') { ended = true; break; }
                if (c < 32) throw new InvalidDataException("JSON control");
                if (c == '\\')
                {
                    if (pos == text.Length) throw new InvalidDataException("JSON escape");
                    char e = text[pos++];
                    switch (e) { case '"': c = '"'; break; case '\\': c = '\\'; break; case '/': c = '/'; break; case 'n': c = '\n'; break; case 'r': c = '\r'; break; case 't': c = '\t'; break; case 'b': c = '\b'; break; case 'f': c = '\f'; break; case 'u': c = (char)Hex(); break; default: throw new InvalidDataException("JSON escape"); }
                }
                result.Append(c); if (result.Length > 16000) throw new InvalidDataException("JSON string size");
            }
            if (!ended) throw new InvalidDataException("JSON string end");
            string value = result.ToString();
            for (int i = 0; i < value.Length; i++)
                if (char.IsHighSurrogate(value[i])) { if (++i >= value.Length || !char.IsLowSurrogate(value[i])) throw new InvalidDataException("JSON surrogate"); }
                else if (char.IsLowSurrogate(value[i])) throw new InvalidDataException("JSON surrogate");
            return value;
        }
        internal static string Quote(string value)
        {
            var b = new StringBuilder("\"");
            foreach (char c in value) { if (c == '"' || c == '\\') b.Append('\\').Append(c); else if (c < 32) b.Append(' '); else b.Append(c); }
            return b.Append('"').ToString();
        }
    }
    internal static class Limits
    {
        internal const int MaxFileBytes = 262144, MaxFiles = 128, MaxTotalBytes = 8 * 1024 * 1024;
    }
}

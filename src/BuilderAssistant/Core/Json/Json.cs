using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace BuilderAssistant.Core.Json
{
    /// <summary>The JSON node kinds this minimal engine understands.</summary>
    internal enum JsonKind
    {
        Object,
        Array,
        String,
        Number,
        Bool,
        Null,
    }

    /// <summary>
    /// A tiny, dependency-free JSON value tree plus a strict recursive-descent
    /// reader and a compact writer. It lives in <c>Core</c> so blueprint
    /// import/export can be proven by the offline test runner WITHOUT the game
    /// assemblies AND still compile into the net472 mod without pulling in any
    /// NuGet package (System.Text.Json is not available on net472). The parser is
    /// deliberately strict: no trailing commas, no comments, no NaN/Infinity -
    /// a malformed blueprint must fail loudly rather than import garbage.
    /// </summary>
    internal sealed class JsonValue
    {
        public readonly JsonKind Kind;

        // Object members, kept in insertion order so serialization is deterministic.
        private readonly List<KeyValuePair<string, JsonValue>> _members;
        // Array items.
        private readonly List<JsonValue> _items;

        private readonly string _string;
        private readonly string _numberRaw; // exact lexical form, so ints round-trip
        private readonly bool _bool;

        private JsonValue(JsonKind kind, string s, string numberRaw, bool b)
        {
            Kind = kind;
            _string = s;
            _numberRaw = numberRaw;
            _bool = b;
            if (kind == JsonKind.Object) _members = new List<KeyValuePair<string, JsonValue>>();
            if (kind == JsonKind.Array) _items = new List<JsonValue>();
        }

        public static JsonValue NewObject() { return new JsonValue(JsonKind.Object, null, null, false); }
        public static JsonValue NewArray() { return new JsonValue(JsonKind.Array, null, null, false); }
        public static JsonValue NewString(string s) { return new JsonValue(JsonKind.String, s ?? string.Empty, null, false); }
        public static JsonValue NewNumber(long n) { return new JsonValue(JsonKind.Number, null, n.ToString(CultureInfo.InvariantCulture), false); }
        public static JsonValue NewNumberRaw(string token) { return new JsonValue(JsonKind.Number, null, token, false); }
        public static JsonValue NewBool(bool b) { return new JsonValue(JsonKind.Bool, null, null, b); }
        public static JsonValue Null { get { return new JsonValue(JsonKind.Null, null, null, false); } }

        public bool IsObject { get { return Kind == JsonKind.Object; } }
        public bool IsArray { get { return Kind == JsonKind.Array; } }
        public bool IsString { get { return Kind == JsonKind.String; } }
        public bool IsNumber { get { return Kind == JsonKind.Number; } }

        public string StringValue { get { return Kind == JsonKind.String ? _string : null; } }
        public string NumberRaw { get { return Kind == JsonKind.Number ? _numberRaw : null; } }
        public bool BoolValue { get { return Kind == JsonKind.Bool && _bool; } }

        public IReadOnlyList<KeyValuePair<string, JsonValue>> Members { get { return _members; } }
        public IReadOnlyList<JsonValue> Items { get { return _items; } }

        public void Add(string key, JsonValue value)
        {
            if (Kind != JsonKind.Object) throw new InvalidOperationException("Add(key) on non-object");
            _members.Add(new KeyValuePair<string, JsonValue>(key, value));
        }

        public void Add(JsonValue value)
        {
            if (Kind != JsonKind.Array) throw new InvalidOperationException("Add(item) on non-array");
            _items.Add(value);
        }

        /// <summary>First member with the given key, or null if absent/not an object.</summary>
        public JsonValue Get(string key)
        {
            if (_members == null) return null;
            for (int i = 0; i < _members.Count; i++)
            {
                if (string.Equals(_members[i].Key, key, StringComparison.Ordinal)) return _members[i].Value;
            }
            return null;
        }

        /// <summary>True + the value when a numeric member parses exactly to an Int32.</summary>
        public bool TryGetInt(string key, out int result)
        {
            result = 0;
            JsonValue v = Get(key);
            return v != null && v.TryAsInt(out result);
        }

        public bool TryGetString(string key, out string result)
        {
            result = null;
            JsonValue v = Get(key);
            if (v != null && v.Kind == JsonKind.String) { result = v._string; return true; }
            return false;
        }

        /// <summary>Exact integer interpretation of a number node (rejects fractions/overflow).</summary>
        public bool TryAsInt(out int result)
        {
            result = 0;
            if (Kind != JsonKind.Number || _numberRaw == null) return false;
            long l;
            if (!TryParseInt64(_numberRaw, out l)) return false;
            if (l < int.MinValue || l > int.MaxValue) return false;
            result = (int)l;
            return true;
        }

        private static bool TryParseInt64(string s, out long value)
        {
            value = 0;
            if (string.IsNullOrEmpty(s)) return false;
            int i = 0;
            bool neg = false;
            if (s[0] == '-') { neg = true; i = 1; }
            else if (s[0] == '+') { i = 1; }
            if (i >= s.Length) return false;
            long acc = 0;
            for (; i < s.Length; i++)
            {
                char c = s[i];
                if (c < '0' || c > '9') return false; // rejects '.', 'e', stray chars
                acc = acc * 10 + (c - '0');
                if (acc > 9223372036854775807L) return false; // overflow guard
            }
            value = neg ? -acc : acc;
            return true;
        }

        // ---- Writer ----
        public string ToJson()
        {
            var sb = new StringBuilder();
            Write(sb);
            return sb.ToString();
        }

        internal void Write(StringBuilder sb)
        {
            switch (Kind)
            {
                case JsonKind.Object:
                    sb.Append('{');
                    if (_members.Count > 0)
                    {
                        for (int i = 0; i < _members.Count; i++)
                        {
                            if (i > 0) sb.Append(',');
                            WriteString(sb, _members[i].Key);
                            sb.Append(':');
                            _members[i].Value.Write(sb);
                        }
                    }
                    sb.Append('}');
                    break;
                case JsonKind.Array:
                    sb.Append('[');
                    for (int i = 0; i < _items.Count; i++)
                    {
                        if (i > 0) sb.Append(',');
                        _items[i].Write(sb);
                    }
                    sb.Append(']');
                    break;
                case JsonKind.String:
                    WriteString(sb, _string);
                    break;
                case JsonKind.Number:
                    sb.Append(_numberRaw);
                    break;
                case JsonKind.Bool:
                    sb.Append(_bool ? "true" : "false");
                    break;
                default:
                    sb.Append("null");
                    break;
            }
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            if (s != null)
            {
                for (int i = 0; i < s.Length; i++)
                {
                    char c = s[i];
                    switch (c)
                    {
                        case '"': sb.Append("\\\""); break;
                        case '\\': sb.Append("\\\\"); break;
                        case '\b': sb.Append("\\b"); break;
                        case '\f': sb.Append("\\f"); break;
                        case '\n': sb.Append("\\n"); break;
                        case '\r': sb.Append("\\r"); break;
                        case '\t': sb.Append("\\t"); break;
                        default:
                            if (c < ' ')
                            {
                                sb.Append("\\u");
                                sb.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                            }
                            else
                            {
                                sb.Append(c); // UTF-8 file content carries raw unicode (CJK names) fine
                            }
                            break;
                    }
                }
            }
            sb.Append('"');
        }

        // ---- Strict reader ----
        /// <summary>Parse JSON text or throw <see cref="JsonParseException"/> with an offset.</summary>
        public static JsonValue Parse(string text)
        {
            if (text == null) throw new JsonParseException("null input", 0);
            var p = new JsonReader(text);
            p.SkipWhitespace();
            JsonValue v = p.ParseValue();
            p.SkipWhitespace();
            if (!p.AtEnd) throw new JsonParseException("trailing characters after top-level value", p.Position);
            return v;
        }

        private sealed class JsonReader
        {
            private readonly string _s;
            private int _i;

            public JsonReader(string s) { _s = s; _i = 0; }
            public int Position { get { return _i; } }
            public bool AtEnd { get { return _i >= _s.Length; } }

            private char Current { get { return _s[_i]; } }

            public void SkipWhitespace()
            {
                while (!AtEnd)
                {
                    char c = Current;
                    if (c == ' ' || c == '\t' || c == '\n' || c == '\r') _i++;
                    else break;
                }
            }

            public JsonValue ParseValue()
            {
                if (AtEnd) throw new JsonParseException("unexpected end of input", _i);
                char c = Current;
                switch (c)
                {
                    case '{': return ParseObject();
                    case '[': return ParseArray();
                    case '"': return JsonValue.NewString(ParseString());
                    case 't': ExpectLiteral("true"); return JsonValue.NewBool(true);
                    case 'f': ExpectLiteral("false"); return JsonValue.NewBool(false);
                    case 'n': ExpectLiteral("null"); return JsonValue.Null;
                    default:
                        if (c == '-' || (c >= '0' && c <= '9')) return ParseNumber();
                        throw new JsonParseException("unexpected character '" + c + "'", _i);
                }
            }

            private JsonValue ParseObject()
            {
                var obj = JsonValue.NewObject();
                _i++; // consume {
                SkipWhitespace();
                if (!AtEnd && Current == '}') { _i++; return obj; }
                while (true)
                {
                    SkipWhitespace();
                    if (AtEnd || Current != '"') throw new JsonParseException("expected object key string", _i);
                    string key = ParseString();
                    SkipWhitespace();
                    if (AtEnd || Current != ':') throw new JsonParseException("expected ':' after object key", _i);
                    _i++; // consume :
                    SkipWhitespace();
                    JsonValue val = ParseValue();
                    obj.Add(key, val);
                    SkipWhitespace();
                    if (AtEnd) throw new JsonParseException("unterminated object", _i);
                    if (Current == ',') { _i++; continue; }
                    if (Current == '}') { _i++; return obj; }
                    throw new JsonParseException("expected ',' or '}' in object", _i);
                }
            }

            private JsonValue ParseArray()
            {
                var arr = JsonValue.NewArray();
                _i++; // consume [
                SkipWhitespace();
                if (!AtEnd && Current == ']') { _i++; return arr; }
                while (true)
                {
                    SkipWhitespace();
                    JsonValue val = ParseValue();
                    arr.Add(val);
                    SkipWhitespace();
                    if (AtEnd) throw new JsonParseException("unterminated array", _i);
                    if (Current == ',') { _i++; continue; }
                    if (Current == ']') { _i++; return arr; }
                    throw new JsonParseException("expected ',' or ']' in array", _i);
                }
            }

            private string ParseString()
            {
                var sb = new StringBuilder();
                _i++; // consume opening quote
                while (true)
                {
                    if (AtEnd) throw new JsonParseException("unterminated string", _i);
                    char c = Current;
                    if (c == '"') { _i++; return sb.ToString(); }
                    if (c == '\\')
                    {
                        _i++;
                        if (AtEnd) throw new JsonParseException("unterminated escape", _i);
                        char e = Current;
                        _i++;
                        switch (e)
                        {
                            case '"': sb.Append('"'); break;
                            case '\\': sb.Append('\\'); break;
                            case '/': sb.Append('/'); break;
                            case 'b': sb.Append('\b'); break;
                            case 'f': sb.Append('\f'); break;
                            case 'n': sb.Append('\n'); break;
                            case 'r': sb.Append('\r'); break;
                            case 't': sb.Append('\t'); break;
                            case 'u':
                                if (_i + 4 > _s.Length) throw new JsonParseException("truncated \\u escape", _i);
                                string hex = _s.Substring(_i, 4);
                                int code;
                                if (!int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code))
                                    throw new JsonParseException("invalid \\u escape '" + hex + "'", _i);
                                sb.Append((char)code);
                                _i += 4;
                                break;
                            default:
                                throw new JsonParseException("invalid escape '\\" + e + "'", _i - 1);
                        }
                    }
                    else
                    {
                        sb.Append(c);
                        _i++;
                    }
                }
            }

            private JsonValue ParseNumber()
            {
                int start = _i;
                if (!AtEnd && (Current == '-' || Current == '+')) _i++;
                while (!AtEnd && Current >= '0' && Current <= '9') _i++;
                if (!AtEnd && Current == '.')
                {
                    _i++;
                    while (!AtEnd && Current >= '0' && Current <= '9') _i++;
                }
                if (!AtEnd && (Current == 'e' || Current == 'E'))
                {
                    _i++;
                    if (!AtEnd && (Current == '+' || Current == '-')) _i++;
                    while (!AtEnd && Current >= '0' && Current <= '9') _i++;
                }
                string token = _s.Substring(start, _i - start);
                if (!IsValidJsonNumber(token))
                    throw new JsonParseException("invalid number '" + token + "'", start);
                // Blueprint coordinates, sizes and material ids are always integers.
                // A fractional/exponent number is legal JSON but not meaningful here;
                // we keep it as a raw token and let the validator reject non-integer use.
                return JsonValue.NewNumberRaw(token);
            }

            private static bool IsValidJsonNumber(string t)
            {
                int len = t.Length;
                if (len == 0) return false;
                int i = 0;
                if (t[0] == '+' || t[0] == '-') i = 1;
                int digitsStart = i;
                while (i < len && t[i] >= '0' && t[i] <= '9') i++;
                if (i == digitsStart) return false; // need at least one integer digit
                if (i < len && t[i] == '.')
                {
                    i++;
                    int fracStart = i;
                    while (i < len && t[i] >= '0' && t[i] <= '9') i++;
                    if (i == fracStart) return false; // '.' must be followed by digits
                }
                if (i < len && (t[i] == 'e' || t[i] == 'E'))
                {
                    i++;
                    if (i < len && (t[i] == '+' || t[i] == '-')) i++;
                    int expStart = i;
                    while (i < len && t[i] >= '0' && t[i] <= '9') i++;
                    if (i == expStart) return false; // exponent needs digits
                }
                return i == len; // no trailing garbage
            }

            private void ExpectLiteral(string literal)
            {
                if (_i + literal.Length > _s.Length)
                    throw new JsonParseException("unexpected end (expected " + literal + ")", _i);
                for (int k = 0; k < literal.Length; k++)
                {
                    if (_s[_i + k] != literal[k])
                        throw new JsonParseException("invalid token, expected " + literal, _i);
                }
                _i += literal.Length;
            }
        }
    }

    /// <summary>Raised when JSON text is malformed. Carries the offending offset.</summary>
    internal sealed class JsonParseException : Exception
    {
        public int Offset { get; private set; }

        public JsonParseException(string message, int offset)
            : base(message + " (at offset " + offset + ")")
        {
            Offset = offset;
        }
    }
}

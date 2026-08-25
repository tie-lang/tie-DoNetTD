// Copyright (c) 2026 TIE-LANG organization. All rights reserved.
// Licensed under the TIE-LANG Open Source License v1.1 (see LICENSE in the repository root).

using System.Globalization;
using System.Numerics;
using System.Text;

namespace DoNetTD.Convert;

/// <summary>
/// tie:data 与 JSON 的双向零依赖转换。
/// 映射约定：
/// - object ↔ <see cref="TieTable"/>（保留出现顺序）
/// - array ↔ <see cref="TieArray"/>
/// - 整数形态数字 ↔ <see cref="TieInteger"/>（无后缀）；小数/指数 ↔ <see cref="TieFloat"/>（无后缀）
/// - true/false ↔ <see cref="TieBool"/>；null ↔ <see cref="TieValue.Null"/>
/// - trit zero ↔ 数字 0、正负 trit ↔ ±1（有损，JSON 无三值类型）
/// - char ↔ 单字符字符串
/// </summary>
public static class TieJson
{
    // ---------- tie → JSON ----------

    /// <summary>把 tie:data 节点序列化为 JSON 文本。</summary>
    public static string ToJson(TieValue value, bool indented = false)
    {
        if (value is null) throw new ArgumentNullException(nameof(value));
        var sb = new StringBuilder(256);
        WriteJson(value, sb, indented, 0);
        return sb.ToString();
    }

    private static void WriteJson(TieValue value, StringBuilder sb, bool indented, int depth)
    {
        switch (value.Kind)
        {
            case TieValueKind.Null:
                sb.Append("null");
                return;
            case TieValueKind.Bool:
                sb.Append(((TieBool)value).Value ? "true" : "false");
                return;
            case TieValueKind.Trit:
                sb.Append(((TieTrit)value).Value.ToString(CultureInfo.InvariantCulture));
                return;
            case TieValueKind.Char:
                WriteJsonString(((TieChar)value).AsString(), sb);
                return;
            case TieValueKind.String:
                WriteJsonString(((TieString)value).Value, sb);
                return;
            case TieValueKind.Integer:
                sb.Append(((TieInteger)value).Value.ToString(CultureInfo.InvariantCulture));
                return;
            case TieValueKind.Float:
                sb.Append(TieWriter.FormatFloat(((TieFloat)value).Value, TieFloatSuffix.None));
                return;
            case TieValueKind.Array:
                WriteJsonArray((TieArray)value, sb, indented, depth);
                return;
            case TieValueKind.Table:
                WriteJsonObject((TieTable)value, sb, indented, depth);
                return;
        }
    }

    private static void WriteJsonArray(TieArray array, StringBuilder sb, bool indented, int depth)
    {
        if (array.Count == 0)
        {
            sb.Append("[]");
            return;
        }
        sb.Append('[');
        for (int i = 0; i < array.Count; i++)
        {
            if (i > 0) sb.Append(',');
            if (indented)
            {
                sb.Append('\n').Append(' ', 2 * (depth + 1));
            }
            WriteJson(array[i], sb, indented, depth + 1);
        }
        if (indented)
        {
            sb.Append('\n').Append(' ', 2 * depth);
        }
        sb.Append(']');
    }

    private static void WriteJsonObject(TieTable table, StringBuilder sb, bool indented, int depth)
    {
        if (table.Count == 0)
        {
            sb.Append("{}");
            return;
        }
        sb.Append('{');
        bool first = true;
        foreach (var kv in table.Items) // JSON 对象保留插入序
        {
            if (!first) sb.Append(',');
            first = false;
            if (indented)
            {
                sb.Append('\n').Append(' ', 2 * (depth + 1));
            }
            WriteJsonString(kv.Key, sb);
            sb.Append(indented ? ": " : ":");
            WriteJson(kv.Value, sb, indented, depth + 1);
        }
        if (indented)
        {
            sb.Append('\n').Append(' ', 2 * depth);
        }
        sb.Append('}');
    }

    private static void WriteJsonString(string s, StringBuilder sb)
    {
        sb.Append('"');
        foreach (var ch in s)
        {
            switch (ch)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (ch < 0x20)
                    {
                        sb.Append("\\u").Append(((int)ch).ToString("X4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        sb.Append(ch);
                    }
                    break;
            }
        }
        sb.Append('"');
    }

    // ---------- JSON → tie ----------

    /// <summary>解析 JSON 文本为 tie:data 节点。非法 JSON 抛 <see cref="TieParseException"/>。</summary>
    public static TieValue FromJson(string json)
    {
        if (json is null) throw new ArgumentNullException(nameof(json));
        var reader = new JsonReader(json.NormalizeNewlines());
        var value = reader.ParseValue(0);
        reader.SkipWs();
        if (!reader.Eof)
        {
            reader.Fail("JSON 文档末尾有多余内容");
        }
        return value;
    }

    private sealed class JsonReader
    {
        private readonly string _s;
        private int _pos;
        private int _line = 1;
        private int _col = 1;

        public JsonReader(string s) => _s = s;

        public bool Eof => _pos >= _s.Length;
        private char Peek => _s[_pos];

        public void Fail(string message) =>
            throw TieParseException.Single(message, _line, _col, _pos);

        public void SkipWs()
        {
            while (!Eof && (Peek == ' ' || Peek == '\t' || Peek == '\n' || Peek == '\r'))
            {
                Advance();
            }
        }

        private void Advance()
        {
            if (_pos >= _s.Length) return;
            if (_s[_pos] == '\n') { _line++; _col = 1; }
            else _col++;
            _pos++;
        }

        public TieValue ParseValue(int depth)
        {
            SkipWs();
            if (Eof) Fail("JSON 值意外结束");
            if (depth > 256) Fail("JSON 嵌套过深");
            char c = Peek;
            switch (c)
            {
                case '{': return ParseObject(depth);
                case '[': return ParseArray(depth);
                case '"': return new TieString(ParseString());
                case 't': ExpectWord("true"); return TieBool.True.Clone();
                case 'f': ExpectWord("false"); return TieBool.False.Clone();
                case 'n': ExpectWord("null"); return TieNull.Instance;
                default:
                    if (c == '-' || (c >= '0' && c <= '9')) return ParseNumber();
                    Fail($"意外字符 '{c}'");
                    return TieNull.Instance;
            }
        }

        private void ExpectWord(string word)
        {
            foreach (var ch in word)
            {
                if (Eof || Peek != ch) Fail($"JSON 字面量不完整（应为 {word}）");
                Advance();
            }
        }

        private TieValue ParseObject(int depth)
        {
            Advance(); // '{'
            var table = new TieTable();
            SkipWs();
            if (Eof) Fail("JSON 对象未闭合");
            if (Peek == '}') { Advance(); return table; }
            while (true)
            {
                SkipWs();
                if (Eof) Fail("JSON 对象未闭合");
                if (Peek != '"') Fail("JSON 对象键必须是字符串");
                var key = ParseString();
                SkipWs();
                if (Eof || Peek != ':') Fail("JSON 对象键后缺少冒号");
                Advance();
                var value = ParseValue(depth + 1);
                table.Set(key, value); // 重复键后值覆盖（与 tie 解析策略一致）
                SkipWs();
                if (Eof) Fail("JSON 对象未闭合");
                if (Peek == ',') { Advance(); continue; }
                if (Peek == '}') { Advance(); return table; }
                Fail("JSON 对象条目后缺少 ',' 或 '}'");
            }
        }

        private TieValue ParseArray(int depth)
        {
            Advance(); // '['
            var array = new TieArray();
            SkipWs();
            if (Eof) Fail("JSON 数组未闭合");
            if (Peek == ']') { Advance(); return array; }
            while (true)
            {
                array.Add(ParseValue(depth + 1));
                SkipWs();
                if (Eof) Fail("JSON 数组未闭合");
                if (Peek == ',') { Advance(); continue; }
                if (Peek == ']') { Advance(); return array; }
                Fail("JSON 数组元素后缺少 ',' 或 ']'");
            }
        }

        private string ParseString()
        {
            Advance(); // '"'
            var sb = new StringBuilder();
            while (true)
            {
                if (Eof) Fail("JSON 字符串未闭合");
                char c = Peek;
                if (c == '"') { Advance(); return sb.ToString(); }
                if (c == '\\')
                {
                    Advance();
                    if (Eof) Fail("JSON 转义不完整");
                    char e = Peek;
                    switch (e)
                    {
                        case '"': sb.Append('"'); Advance(); break;
                        case '\\': sb.Append('\\'); Advance(); break;
                        case '/': sb.Append('/'); Advance(); break;
                        case 'b': sb.Append('\b'); Advance(); break;
                        case 'f': sb.Append('\f'); Advance(); break;
                        case 'n': sb.Append('\n'); Advance(); break;
                        case 'r': sb.Append('\r'); Advance(); break;
                        case 't': sb.Append('\t'); Advance(); break;
                        case 'u':
                            Advance();
                            sb.Append(ReadUnicodeEscape());
                            break;
                        default:
                            Fail($"JSON 未知转义 \\{e}");
                            break;
                    }
                    continue;
                }
                if (c < 0x20) Fail("JSON 字符串含未转义控制字符");
                sb.Append(c);
                Advance();
            }
        }

        private char ReadUnicodeEscape()
        {
            int cp = 0;
            for (int i = 0; i < 4; i++)
            {
                if (Eof || !IsAsciiHex(Peek)) Fail("\\u 转义需要四位十六进制");
                char h = Peek;
                cp = cp * 16 + (h <= '9' ? h - '0' : char.ToLowerInvariant(h) - 'a' + 10);
                Advance();
            }
            return (char)cp;
        }

        private static bool IsAsciiHex(char c) =>
            (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

        private TieValue ParseNumber()
        {
            int start = _pos;
            bool isFloat = false;
            if (Peek == '-') Advance();
            bool anyDigit = false;
            while (!Eof && (Peek >= '0' && Peek <= '9')) { anyDigit = true; Advance(); }
            if (!anyDigit) Fail("JSON 数字缺少整数部分");
            if (!Eof && Peek == '.')
            {
                isFloat = true;
                Advance();
                bool frac = false;
                while (!Eof && (Peek >= '0' && Peek <= '9')) { frac = true; Advance(); }
                if (!frac) Fail("JSON 数字小数部分缺少数字");
            }
            if (!Eof && (Peek == 'e' || Peek == 'E'))
            {
                isFloat = true;
                Advance();
                if (!Eof && (Peek == '+' || Peek == '-')) Advance();
                bool exp = false;
                while (!Eof && (Peek >= '0' && Peek <= '9')) { exp = true; Advance(); }
                if (!exp) Fail("JSON 指数部分缺少数字");
            }
            var text = _s.Substring(start, _pos - start);
            if (!isFloat)
            {
                return new TieInteger(BigInteger.Parse(text, CultureInfo.InvariantCulture));
            }
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            {
                Fail($"无法解析 JSON 数字 \"{text}\"");
            }
            return new TieFloat(d);
        }
    }
}

internal static class StringNormalizeExtensions
{
    internal static string NormalizeNewlines(this string text) =>
        text.Replace("\r\n", "\n").Replace("\r", "\n");
}

// Copyright (c) 2026 TIE-LANG organization. All rights reserved.
// Licensed under the TIE-LANG Open Source License v1.1 (see LICENSE in the repository root).

using System.Globalization;
using System.Text;

namespace DoNetTD;

/// <summary>
/// tie:data 写出器（内部实现）。
/// 默认按官方语义输出：表键 strcmp 字节序排序、4 空格缩进美化、条目尾逗号。
/// </summary>
internal sealed class TieWriter
{
    private readonly StringBuilder _sb = new StringBuilder();
    private readonly TieWriteOptions _opt;
    private int _depth;

    private TieWriter(TieWriteOptions options)
    {
        _opt = options;
    }

    public static string Write(TieDocument document, TieWriteOptions? options)
    {
        var opt = options ?? new TieWriteOptions();
        var w = new TieWriter(opt);
        if (opt.EmitHeader)
        {
            w._sb.Append("type tie<data>\n");
            if (opt.Pretty)
            {
                w._sb.Append('\n');
            }
        }
        w.WriteValue(document.Root);
        if (opt.Pretty)
        {
            w._sb.Append('\n'); // 美化模式以换行收尾（文本文件惯例）；紧凑模式输出纯单行
        }
        return w._sb.ToString();
    }

    // ---------- 值分发 ----------

    private void WriteValue(TieValue value)
    {
        switch (value.Kind)
        {
            case TieValueKind.Null:
                throw new InvalidOperationException(
                    "TieNull 无法写为 tie:data（语法没有 null 字面量）；请改用其他节点或在 JSON 转换层处理");
            case TieValueKind.Bool:
                _sb.Append(((TieBool)value).Value ? "true" : "false");
                return;
            case TieValueKind.Trit:
                if (((TieTrit)value).Value != 0)
                {
                    throw new InvalidOperationException(
                        "非零 trit 没有无类型的 tie:data 字面量形式（true/false 会与布尔混淆），仅支持 zero 的往返");
                }
                _sb.Append("zero");
                return;
            case TieValueKind.Char:
                _sb.Append('\'').Append(EscapeCharBody(((TieChar)value).Codepoint)).Append('\'');
                return;
            case TieValueKind.String:
                _sb.Append('"').Append(EscapeStringBody(((TieString)value).Value)).Append('"');
                return;
            case TieValueKind.Integer:
            case TieValueKind.Float:
                _sb.Append(value.ToString());
                return;
            case TieValueKind.Array:
                WriteArray((TieArray)value);
                return;
            case TieValueKind.Table:
                WriteTable((TieTable)value);
                return;
            default:
                throw new InvalidOperationException("未知节点种类: " + value.Kind);
        }
    }

    // ---------- 容器 ----------

    private void WriteArray(TieArray array)
    {
        if (array.Count == 0)
        {
            _sb.Append("[]");
            return;
        }

        if (_opt.Pretty && _opt.CompactArraysOfScalars && AllScalars(array))
        {
            _sb.Append('[');
            for (int i = 0; i < array.Count; i++)
            {
                if (i > 0)
                {
                    _sb.Append(", ");
                }
                WriteValue(array[i]);
            }
            _sb.Append(']');
            return;
        }

        WriteEntries(array.Count, i => WriteValue(array[i]));
    }

    private void WriteTable(TieTable table)
    {
        if (table.Count == 0)
        {
            // 空 [] 是空数组；空表没有独立字面量形态，与官方解析语义一致（首条目区分表/数组）。
            _sb.Append("[]");
            return;
        }

        IEnumerable<KeyValuePair<string, TieValue>> entries =
            _opt.KeyOrder == TableKeyOrder.SortStrcmp ? table.InStrcmpOrder() : table.Items;

        var materialized = new List<KeyValuePair<string, TieValue>>(entries);
        WriteEntries(materialized.Count,
            i =>
            {
                var kv = materialized[i];
                _sb.Append('"').Append(EscapeStringBody(kv.Key)).Append("\": ");
                WriteValue(kv.Value);
            });
    }

    /// <summary>容器条目的统一排版：美化多行缩进+逗号策略 / 紧凑单行。</summary>
    private void WriteEntries(int count, Action<int> writeEntry)
    {
        _sb.Append('[');

        if (_opt.Pretty)
        {
            _depth++;
            for (int i = 0; i < count; i++)
            {
                _sb.Append('\n');
                AppendIndent(_depth);
                writeEntry(i);
                bool isLast = i == count - 1;
                if (!isLast || _opt.TrailingComma)
                {
                    _sb.Append(',');
                }
            }
            _depth--;
            _sb.Append('\n');
            AppendIndent(_depth);
        }
        else
        {
            for (int i = 0; i < count; i++)
            {
                if (i > 0)
                {
                    _sb.Append(", ");
                }
                writeEntry(i);
            }
            if (_opt.TrailingComma && count > 0)
            {
                _sb.Append(',');
            }
        }

        _sb.Append(']');
    }

    private void AppendIndent(int depth)
    {
        for (int i = 0; i < depth; i++)
        {
            _sb.Append(_opt.Indent);
        }
    }

    private static bool AllScalars(TieArray array)
    {
        foreach (var item in array.Items)
        {
            if (item.Kind is TieValueKind.Array or TieValueKind.Table)
            {
                return false;
            }
        }
        return true;
    }

    // ---------- 浮点与转义 ----------

    /// <summary>
    /// 浮点字面量格式化：R 最短往返表示；整数形态补 ".0" 与整数字面量区分；
    /// f32 后缀转换到 float 精度再格式化并附后缀。NaN/无穷不可表达，抛异常。
    /// </summary>
    internal static string FormatFloat(double value, TieFloatSuffix suffix)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            throw new InvalidOperationException("NaN/Infinity 不是合法的 tie:data 浮点字面量");
        }
        double v = value;
        if (suffix == TieFloatSuffix.F32)
        {
            float f = (float)value;
            if (float.IsNaN(f) || float.IsInfinity(f))
            {
                throw new InvalidOperationException("该 double 转 f32 后溢出为 NaN/Infinity，无法表达");
            }
            v = f;
        }
        var text = v.ToString("R", CultureInfo.InvariantCulture);
        if (!text.Contains('.') && !text.Contains('e') && !text.Contains('E'))
        {
            text += ".0";
        }
        return suffix == TieFloatSuffix.F32 ? text + "f32" : text;
    }

    /// <summary>字符串内容转义（不含两端引号）：" \ \n \r \t 必转；&lt;0x20 控制字符用 \u00XX；其余原样。</summary>
    internal static string EscapeStringBody(string s)
    {
        var sb = new StringBuilder(s.Length + 8);
        foreach (var ch in s)
        {
            AppendEscaped(sb, ch);
        }
        return sb.ToString();
    }

    /// <summary>char 字面量内容转义（不含两端单引号）。</summary>
    internal static string EscapeCharBody(int codepoint)
    {
        var sb = new StringBuilder(4);
        if (codepoint > 0xFFFF)
        {
            sb.Append(char.ConvertFromUtf32(codepoint));
            return sb.ToString();
        }
        AppendEscaped(sb, (char)codepoint);
        return sb.ToString();
    }

    private static void AppendEscaped(StringBuilder sb, char c)
    {
        switch (c)
        {
            case '"': sb.Append("\\\""); return;
            case '\\': sb.Append("\\\\"); return;
            case '\n': sb.Append("\\n"); return;
            case '\r': sb.Append("\\r"); return;
            case '\t': sb.Append("\\t"); return;
            default:
                if (c < 0x20)
                {
                    sb.Append("\\u").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
                }
                else
                {
                    sb.Append(c);
                }
                return;
        }
    }
}

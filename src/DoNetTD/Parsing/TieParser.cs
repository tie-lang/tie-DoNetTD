// Copyright (c) 2026 TIE-LANG organization. All rights reserved.
// Licensed under the TIE-LANG Open Source License v1.1 (see LICENSE in the repository root).

using System.Globalization;
using System.Numerics;
using System.Text;

namespace DoNetTD;

/// <summary>
/// tie:data 递归下降解析器（内部实现）。
/// 行为对齐官方 compiler/config.tie 解析器并做语言级超集扩展：
/// 空白与 // 行注释、逗号可选且容忍尾逗号、表/数组共用 [...] 靠首条目区分、
/// 六种官方转义（\" \\ \/ \n \r \t）加库扩展转义（\0 \b \f \uXXXX）、
/// 窄整数/浮点后缀、指数形式、zero trit 与 char 字面量。
/// </summary>
internal sealed class TieParser
{
    private readonly string _s;
    private readonly TieParseOptions _opt;
    private int _pos;
    private int _line = 1;
    private int _col = 1;
    private bool _hasDataHeader;

    // CollectAllErrors：累计诊断（快照随每次抛出携带）。
    private readonly List<TieDiagnostic> _errors = new List<TieDiagnostic>();
    private bool _capNoted;

    // 注释捕获：值上方紧邻的行注释先入 pending，值解析完成后统一挂为前导；
    // 与最近完成值同行的行注释直接挂为该值的尾随。
    private readonly List<string> _pendingLeading = new List<string>();
    private TieValue? _lastValue;
    private int _lastValueEndLine = -1;

    private TieParser(string normalized, TieParseOptions options)
    {
        _s = normalized;
        _opt = options;
    }

    public static TieDocument Parse(string text, TieParseOptions? options)
    {
        var opt = options ?? TieParseOptions.Default;
        // 归一化换行（与 tie-prep 壳层的 CRLF→LF 归一一致），位置按归一后文本计。
        var normalized = NormalizeNewlines(text);
        var p = new TieParser(normalized, opt);
        return p.Run();
    }

    private TieDocument Run()
    {
        if (_opt.RequireHeader)
        {
            ParseHeader(required: true);
        }
        else
        {
            ParseHeader(required: false);
        }

        SkipTrivia();
        TieValue root;
        try
        {
            root = ParseValue(0);
        }
        catch (TieParseException) when (_opt.CollectAllErrors)
        {
            // 根值失败：吞掉异常继续收集（文档已无有效根，最终统一抛出）。
            while (!Eof)
            {
                Advance();
            }
            root = TieNull.Instance;
        }

        SkipTrivia();
        if (!Eof)
        {
            Report("文档根值之后有多余内容");
        }

        if (!_opt.AllowScalarRoot && root.Kind != TieValueKind.Array && root.Kind != TieValueKind.Table)
        {
            Report("tie:data 根值必须是表或数组（当前 AllowScalarRoot=false）");
        }

        // 收集模式：有错误则整体失败并携带完整诊断列表；宽容模式在 Report 内已抛。
        if (_opt.CollectAllErrors && _errors.Count > 0)
        {
            throw new TieParseException(new List<TieDiagnostic>(_errors));
        }

        return new TieDocument(root, _hasDataHeader);
    }

    // ---------- 头部识别 ----------

    /// <summary>
    /// 尝试在文档开头识别 "type tie&lt;data&gt;" 声明。头部必须出现在文件最前面；
    /// 出现其他角色头视为解析失败（这不是数据文档）。
    /// </summary>
    private void ParseHeader(bool required)
    {
        int savePos = _pos, saveLine = _line, saveCol = _col;

        SkipTrivia();
        if (MatchWord("type"))
        {
            SkipHorizontalWs();
            if (!MatchWord("tie"))
            {
                Fail("头部声明应为 type tie<角色>");
            }
            SkipHorizontalWs();
            if (Peek == '<')
            {
                Advance();
                SkipHorizontalWs();
                int roleStart = _pos;
                while (!Eof && char.IsLetter(Peek))
                {
                    Advance();
                }
                var role = _s.Substring(roleStart, _pos - roleStart);
                SkipHorizontalWs();
                if (Peek != '>')
                {
                    Fail("头部声明缺少 '>'");
                }
                Advance();
                if (role != "data")
                {
                    Fail($"文件角色为 tie<{role}>，不是 tie<data> 数据文档");
                }
                _hasDataHeader = true;
            }
            else
            {
                Fail("头部声明缺少角色标注（应为 type tie<data>）");
            }
            return;
        }

        // 没有 type 开头：回退到起点。
        _pos = savePos; _line = saveLine; _col = saveCol;
        if (required)
        {
            Fail("缺少 type tie<data> 头部声明（RequireHeader=true）");
        }
    }

    // ---------- 词法辅助 ----------

    private bool Eof => _pos >= _s.Length;

    private char Peek => _s[_pos];

    private char PeekAt(int ahead) =>
        _pos + ahead < _s.Length ? _s[_pos + ahead] : '\0';

    private void Advance()
    {
        if (_pos >= _s.Length) return;
        if (_s[_pos] == '\n')
        {
            _line++;
            _col = 1;
        }
        else
        {
            _col++;
        }
        _pos++;
    }

    /// <summary>跳过空白（空格/制表/换行/回车）与 // 行注释。</summary>
    private void SkipTrivia()
    {
        while (!Eof)
        {
            char c = Peek;
            if (c == ' ' || c == '\t' || c == '\n' || c == '\r')
            {
                Advance();
            }
            else if (c == '/' && PeekAt(1) == '/')
            {
                int commentLine = _line;
                Advance();
                Advance();
                int start = _pos;
                while (!Eof && Peek != '\n')
                {
                    Advance();
                }
                var text = _s.Substring(start, _pos - start).Trim();
                // 同行紧跟最近完成值 → 尾随注释；否则挂起为下一值的前导。
                if (_lastValue is not null && commentLine == _lastValueEndLine && _pendingLeading.Count == 0)
                {
                    _lastValue.TrailingComment = text;
                }
                else
                {
                    _pendingLeading.Add(text);
                }
            }
            else
            {
                return;
            }
        }
    }

    private void SkipHorizontalWs()
    {
        while (!Eof && (Peek == ' ' || Peek == '\t'))
        {
            Advance();
        }
    }

    /// <summary>匹配整词关键字（后一字符不是字母/数字/下划线才算命中）。</summary>
    private bool MatchWord(string word)
    {
        if (_pos + word.Length > _s.Length) return false;
        if (string.CompareOrdinal(_s, _pos, word, 0, word.Length) != 0) return false;
        int after = _pos + word.Length;
        if (after < _s.Length)
        {
            char c = _s[after];
            if (char.IsLetterOrDigit(c) || c == '_') return false;
        }
        for (int i = 0; i < word.Length; i++)
        {
            Advance();
        }
        return true;
    }

    /// <summary>
    /// 统一错误出口：宽容模式立即抛首个错误；收集模式记入 _errors 后同样以异常
    /// 作为控制流交由容器循环捕获恢复（诊断已在列表里，最终统一抛出）。
    /// </summary>
    private void Fail(string message)
    {
        if (!_opt.CollectAllErrors)
        {
            throw TieParseException.Single(message, _line, _col, _pos);
        }
        RecordError(message);
        throw new TieParseException(new List<TieDiagnostic>(_errors));
    }

    /// <summary>仅记录一条错误（不抛出）。供收集模式的根级路径使用。</summary>
    private void Report(string message) => Fail(message);

    private void RecordError(string message)
    {
        if (_errors.Count >= _opt.MaxErrors)
        {
            if (!_capNoted)
            {
                _capNoted = true;
                _errors.Add(new TieDiagnostic(TieDiagnosticSeverity.Error,
                    $"错误超过 {_opt.MaxErrors} 个，停止收集", _line, _col, _pos));
            }
            return;
        }
        _errors.Add(new TieDiagnostic(TieDiagnosticSeverity.Error, message, _line, _col, _pos));
    }

    /// <summary>错误恢复：跳过字符直到条目分隔符（','）、容器闭合（']'）或 EOF。</summary>
    private void Resync()
    {
        while (!Eof)
        {
            char c = Peek;
            if (c == ',' || c == ']')
            {
                return;
            }
            Advance();
        }
    }

    // ---------- 值解析 ----------

    /// <summary>
    /// 解析一个值并完成注释挂载：挂起的前导注释落到该值上，
    /// 同时把它登记为「最近完成值」供同行尾随注释归属。
    /// </summary>
    private TieValue ParseValue(int depth)
    {
        var v = ParseValueCore(depth);
        foreach (var c in _pendingLeading)
        {
            v.LeadingComments.Add(c);
        }
        _pendingLeading.Clear();
        _lastValue = v;
        _lastValueEndLine = _line;
        return v;
    }

    private TieValue ParseValueCore(int depth)
    {
        SkipTrivia();
        if (Eof)
        {
            Fail("值意外结束");
        }

        CheckDepth(depth);
        char c = Peek;
        switch (c)
        {
            case '[':
                return ParseContainer(depth);
            case '"':
                return new TieString(ParseString());
            case '\'':
                return ParseCharLiteral();
            case '-':
            case >= '0' and <= '9':
                return ParseNumber();
            default:
                if (char.IsLetter(c) || c == '_')
                {
                    return ParseKeyword();
                }
                Fail($"意外字符 '{Printable(c)}'");
                return TieNull.Instance; // 不可达，满足编译器
        }
    }

    private void CheckDepth(int depth)
    {
        if (depth > _opt.MaxDepth)
        {
            Fail($"嵌套过深（超过 {_opt.MaxDepth} 层）");
        }
    }

    private TieValue ParseKeyword()
    {
        int start = _pos;
        while (!Eof && (char.IsLetterOrDigit(Peek) || Peek == '_'))
        {
            Advance();
        }
        var word = _s.Substring(start, _pos - start);
        switch (word)
        {
            case "true":
                return TieBool.True.Clone();
            case "false":
                return TieBool.False.Clone();
            case "zero":
                return new TieTrit(0);
            default:
                Fail($"无法识别的字面量 \"{word}\"（tie:data 标量关键字只有 true/false/zero）");
                return TieNull.Instance; // 不可达
        }
    }

    // ---------- 容器 ----------

    private TieValue ParseContainer(int depth)
    {
        // 进入时当前字符必为 '['。
        Advance(); // '['
        CheckDepth(depth + 1);
        SkipTrivia();

        if (Eof)
        {
            Fail("容器未闭合（缺少 ']'）");
        }
        if (Peek == ']')
        {
            Advance();
            return new TieArray();
        }

        if (Peek == '"' && LooksLikeTableEntry())
        {
            return ParseTableBody(depth + 1);
        }
        return ParseArrayBody(depth + 1);
    }

    /// <summary>
    /// 向前看判断 '[' 后的首个字符串是否是表键（其后跳过空白是冒号）。
    /// 调用点保证当前字符为 '"'。判断过程恢复现场。
    /// </summary>
    private bool LooksLikeTableEntry()
    {
        int savePos = _pos, saveLine = _line, saveCol = _col;
        try
        {
            ParseString(); // 抛异常说明字符串本身非法——按非表处理，让数组路径给出更贴切的报错
            SkipTrivia();
            return !Eof && Peek == ':';
        }
        catch (TieParseException)
        {
            return false;
        }
        finally
        {
            _pos = savePos; _line = saveLine; _col = saveCol;
        }
    }

    private TieTable ParseTableBody(int depth)
    {
        var table = new TieTable();
        while (true)
        {
            SkipTrivia();
            if (Eof)
            {
                Fail("表未闭合（缺少 ']'）");
            }
            if (Peek == ']')
            {
                Advance();
                return table;
            }

            if (Peek != '"')
            {
                Fail("表条目必须以 \"key\": 形式开始");
            }
            var key = ParseString();

            SkipTrivia();
            if (Eof || Peek != ':')
            {
                Fail($"键 \"{key}\" 后缺少冒号");
            }
            Advance(); // ':'

            TieValue value;
            try
            {
                value = ParseValue(depth);
            }
            catch (TieParseException) when (_opt.CollectAllErrors)
            {
                // 条目级恢复：跳到分隔/闭合符继续收集后续错误。
                Resync();
                if (!Eof && Peek == ',')
                {
                    Advance();
                }
                continue;
            }

            if (table.ContainsKey(key))
            {
                if (_opt.StrictDuplicateKeys)
                {
                    Fail($"重复的表键 \"{key}\"");
                }
                table.Set(key, value); // 后值覆盖，保持原插入位置
            }
            else
            {
                table.Set(key, value);
            }

            SkipTrivia();
            if (!Eof && Peek == ',')
            {
                Advance(); // 逗号可选：消费与否都继续
            }
        }
    }

    private TieArray ParseArrayBody(int depth)
    {
        var array = new TieArray();
        while (true)
        {
            SkipTrivia();
            if (Eof)
            {
                Fail("数组未闭合（缺少 ']'）");
            }
            if (Peek == ']')
            {
                Advance();
                return array;
            }

            TieValue item;
            try
            {
                item = ParseValue(depth);
            }
            catch (TieParseException) when (_opt.CollectAllErrors)
            {
                Resync();
                if (!Eof && Peek == ',')
                {
                    Advance();
                }
                continue;
            }
            array.Add(item);

            SkipTrivia();
            if (!Eof && Peek == ',')
            {
                Advance(); // 逗号可选
            }
        }
    }

    // ---------- 字符串 ----------

    /// <summary>解析含引号的字符串字面量，返回解码后的内容。</summary>
    private string ParseString()
    {
        if (Peek != '"')
        {
            Fail("字符串应以双引号开始");
        }
        Advance();
        var sb = new StringBuilder();
        while (true)
        {
            if (Eof)
            {
                Fail("字符串未闭合");
            }
            char c = Peek;
            if (c == '"')
            {
                Advance();
                return sb.ToString();
            }
            if (c == '\\')
            {
                ReadEscape(sb);
                continue;
            }
            if (c == '\n')
            {
                Fail("字符串中不允许裸换行（请用 \\n 转义）");
            }
            sb.Append(c);
            Advance();
        }
    }

    /// <summary>
    /// 读取一个转义序列追加到 sb。官方六种：\" \\ \/ \n \r \t；
    /// 库扩展四种：\0 \b \f \uXXXX。未知转义报错（与官方一致）。
    /// </summary>
    private void ReadEscape(StringBuilder sb)
    {
        Advance(); // 反斜杠
        if (Eof)
        {
            Fail("转义序列不完整");
        }
        char e = Peek;
        switch (e)
        {
            case '"': sb.Append('"'); break;
            case '\\': sb.Append('\\'); break;
            case '/': sb.Append('/'); break;
            case 'n': sb.Append('\n'); break;
            case 'r': sb.Append('\r'); break;
            case 't': sb.Append('\t'); break;
            case '0': sb.Append('\0'); break;
            case 'b': sb.Append('\b'); break;
            case 'f': sb.Append('\f'); break;
            case 'u':
                Advance();
                sb.Append(ReadUnicodeEscape());
                return;
            default:
                Fail($"未知转义 \\{Printable(e)}");
                return; // 不可达
        }
        Advance();
    }

    /// <summary>读取 \uXXXX 的四位十六进制，返回对应字符。调用点已消费 'u'。</summary>
    private char ReadUnicodeEscape()
    {
        int cp = 0;
        for (int i = 0; i < 4; i++)
        {
            if (Eof)
            {
                Fail("\\u 转义不完整");
            }
            char h = Peek;
            int d = IsAsciiHex(h) ? HexValue(h) : -1;
            if (d < 0)
            {
                Fail($"\\u 含非十六进制字符 '{Printable(h)}'");
            }
            cp = cp * 16 + d;
            Advance();
        }
        return (char)cp;
    }

    private static bool IsAsciiHex(char c) =>
        (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

    private static int HexValue(char h) =>
        h <= '9' ? h - '0' : (char.ToLowerInvariant(h) - 'a' + 10);

    // ---------- 字符字面量 ----------

    private TieChar ParseCharLiteral()
    {
        Advance(); // '\''
        var sb = new StringBuilder();
        if (Eof)
        {
            Fail("char 字面量未闭合");
        }
        if (Peek == '\\' )
        {
            ReadEscape(sb);
        }
        else if (Peek == '\'')
        {
            Fail("char 字面量为空");
        }
        else
        {
            char c = Peek;
            sb.Append(c);
            Advance();
            // 直接输入增补平面字符（代理对）：合并为一个码点。
            if (char.IsHighSurrogate(c) && !Eof && char.IsLowSurrogate(Peek))
            {
                sb.Append(Peek);
                Advance();
            }
        }

        if (Eof || Peek != '\'')
        {
            Fail("char 字面量必须恰好一个码点并以单引号闭合");
        }
        Advance(); // '\''

        int codepoint = CodepointOf(sb.ToString());
        if (codepoint < 0)
        {
            Fail("char 字面量必须恰好一个码点");
        }
        return new TieChar(codepoint);
    }

    private static int CodepointOf(string s)
    {
        if (s.Length == 1) return s[0];
        if (s.Length == 2 && char.IsSurrogatePair(s[0], s[1]))
        {
            return 0x10000 + ((s[0] - 0xD800) << 10) + (s[1] - 0xDC00);
        }
        return -1;
    }

    // ---------- 数字 ----------

    private TieValue ParseNumber()
    {
        int start = _pos;
        bool negative = false;
        if (Peek == '-')
        {
            negative = true;
            Advance();
        }

        bool anyIntDigit = false;
        while (!Eof && (Peek >= '0' && Peek <= '9'))
        {
            anyIntDigit = true;
            Advance();
        }
        if (!anyIntDigit)
        {
            Fail(negative ? "负号后缺少数字" : "数字缺少整数部分");
        }

        bool isFloat = false;
        if (!Eof && Peek == '.')
        {
            Advance();
            bool anyFrac = false;
            while (!Eof && (Peek >= '0' && Peek <= '9'))
            {
                anyFrac = true;
                Advance();
            }
            if (!anyFrac)
            {
                Fail("数字小数部分缺少数字");
            }
            isFloat = true;
        }
        if (!Eof && (Peek == 'e' || Peek == 'E'))
        {
            Advance();
            if (!Eof && (Peek == '+' || Peek == '-'))
            {
                Advance();
            }
            bool anyExp = false;
            while (!Eof && (Peek >= '0' && Peek <= '9'))
            {
                anyExp = true;
                Advance();
            }
            if (!anyExp)
            {
                Fail("指数部分缺少数字");
            }
            isFloat = true;
        }

        // 后缀：紧随的字母开头的字母数字段。
        string? suffix = null;
        if (!Eof && char.IsLetter(Peek))
        {
            int sufStart = _pos;
            while (!Eof && char.IsLetterOrDigit(Peek))
            {
                Advance();
            }
            suffix = _s.Substring(sufStart, _pos - sufStart);
        }

        var digitsText = _s.Substring(start, _pos - start);
        // 数值主体去掉后缀再交给解析器。
        var numeric = suffix is null ? digitsText : digitsText.Substring(0, digitsText.Length - suffix!.Length);

        if (isFloat)
        {
            TieFloatSuffix fsuf;
            if (suffix is null) fsuf = TieFloatSuffix.None;
            else if (suffix == "f32") fsuf = TieFloatSuffix.F32;
            else if (suffix == "f64") fsuf = TieFloatSuffix.F64;
            else
            {
                Fail($"浮点后缀 \"{suffix}\" 无效（只允许 f32/f64）");
                return TieNull.Instance; // 不可达，满足编译器与可空分析
            }
            double d;
            if (!double.TryParse(numeric, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
            {
                Fail($"无法解析浮点数 \"{numeric}\"");
            }
            return new TieFloat(d, fsuf);
        }

        TieIntegerSuffix isuf;
        switch (suffix)
        {
            case null: isuf = TieIntegerSuffix.None; break;
            case "i8": isuf = TieIntegerSuffix.I8; break;
            case "i16": isuf = TieIntegerSuffix.I16; break;
            case "i32": isuf = TieIntegerSuffix.I32; break;
            case "i64": isuf = TieIntegerSuffix.I64; break;
            case "i128": isuf = TieIntegerSuffix.I128; break;
            case "u8": isuf = TieIntegerSuffix.U8; break;
            case "u16": isuf = TieIntegerSuffix.U16; break;
            case "u32": isuf = TieIntegerSuffix.U32; break;
            case "u64": isuf = TieIntegerSuffix.U64; break;
            case "u128": isuf = TieIntegerSuffix.U128; break;
            default:
                Fail($"整数后缀 \"{suffix}\" 无效（允许 i8..i128/u8..u128）");
                return TieNull.Instance; // 不可达
        }
        if (!BigInteger.TryParse(numeric, NumberStyles.Integer, CultureInfo.InvariantCulture, out var big))
        {
            Fail($"无法解析整数 \"{numeric}\"");
        }
        return new TieInteger(big, isuf);
    }

    // ---------- 工具 ----------

    internal static string NormalizeNewlines(string text) =>
        text.Replace("\r\n", "\n").Replace("\r", "\n");

    internal static string Printable(char c) =>
        c >= 0x20 && c < 0x7F ? c.ToString() : $"U+{(int)c:X4}";
}

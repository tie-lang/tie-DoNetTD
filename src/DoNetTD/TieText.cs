// Copyright (c) 2026 TIE-LANG organization. All rights reserved.
// Licensed under the TIE-LANG Open Source License v1.1 (see LICENSE in the repository root).

namespace DoNetTD;

/// <summary>字符串节点："..." 双引号字符串。内容为 UTF-16 字符串（tie 侧为 UTF-8 文本）。</summary>
public sealed class TieString : TieValue
{
    private string _value = "";

    /// <summary>字符串内容。</summary>
    public string Value { get => _value; set { EnsureMutable(); _value = value ?? throw new ArgumentNullException(nameof(value)); } }

    /// <summary>构造字符串节点；value 不允许 null。</summary>
    public TieString(string value)
    {
        _value = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <inheritdoc />
    public override TieValueKind Kind => TieValueKind.String;

    /// <inheritdoc />
    protected override TieValue CloneCore() => new TieString(Value);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is TieString s && s.Value == Value;

    /// <inheritdoc />
    public override int GetHashCode() => Value.GetHashCode();

    /// <summary>输出带引号的转义形态（诊断用途）。</summary>
    public override string ToString() => "\"" + TieWriter.EscapeStringBody(Value) + "\"";
}

/// <summary>
/// 字符节点：'a' 单引号字符字面量。tie 的 char 是 Unicode 码点，
/// 以 int 存储完整码点（含增补平面）。
/// </summary>
public sealed class TieChar : TieValue
{
    /// <summary>Unicode 码点值（0..0x10FFFF，不含代理区）。</summary>
    public int Codepoint { get => _codepoint; set { EnsureMutable(); _codepoint = value; } }

    private int _codepoint;

    /// <summary>构造字符节点；codepoint 必须是合法标量码点。</summary>
    public TieChar(int codepoint)
    {
        if (codepoint < 0 || codepoint > 0x10FFFF || (codepoint >= 0xD800 && codepoint <= 0xDFFF))
        {
            throw new ArgumentOutOfRangeException(nameof(codepoint), "非法 Unicode 码点");
        }
        Codepoint = codepoint;
    }

    /// <summary>从 .NET char 构造（BMP 字符）。</summary>
    public TieChar(char c) : this((int)c) { }

    /// <inheritdoc />
    public override TieValueKind Kind => TieValueKind.Char;

    /// <inheritdoc />
    protected override TieValue CloneCore() => new TieChar(Codepoint);

    /// <summary>转为单字符字符串（可能长 1-2 个 UTF-16 单元）。</summary>
    public string AsString() => char.ConvertFromUtf32(Codepoint);

    /// <summary>BMP 码点直接取 char；增补平面字符抛 <see cref="InvalidOperationException"/>。</summary>
    public char AsChar()
    {
        if (Codepoint > 0xFFFF)
        {
            throw new InvalidOperationException("增补平面码点无法用单个 char 表达，请使用 Codepoint 或 AsString()");
        }
        return (char)Codepoint;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is TieChar c && c.Codepoint == Codepoint;

    /// <inheritdoc />
    public override int GetHashCode() => Codepoint;

    /// <summary>输出 'a' 形态（诊断用途）。</summary>
    public override string ToString() => "'" + TieWriter.EscapeCharBody(Codepoint) + "'";
}

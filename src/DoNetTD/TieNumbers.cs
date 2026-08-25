// Copyright (c) 2026 TIE-LANG organization. All rights reserved.
// Licensed under the TIE-LANG Open Source License v1.1 (see LICENSE in the repository root).

using System.Numerics;

namespace DoNetTD;

/// <summary>整数节点：内部以 BigInteger 存储，支撑 i8..i128/u8..u128 全部窄类型。</summary>
public sealed class TieInteger : TieValue
{
    /// <summary>整数值。</summary>
    public BigInteger Value { get; set; }

    /// <summary>
    /// 窄后缀。<see cref="TieIntegerSuffix.None"/> 表示未标注（tie 语义默认 i64），
    /// 写出时不附加后缀；显式解析到 "42i32" 时为 I32 并在写出时保留。
    /// </summary>
    public TieIntegerSuffix Suffix { get; set; }

    /// <summary>构造整数节点（无后缀）。</summary>
    public TieInteger(BigInteger value) : this(value, TieIntegerSuffix.None) { }

    /// <summary>构造整数节点并指定窄后缀。</summary>
    public TieInteger(BigInteger value, TieIntegerSuffix suffix)
    {
        Value = value;
        Suffix = suffix;
    }

    /// <inheritdoc />
    public override TieValueKind Kind => TieValueKind.Integer;

    /// <inheritdoc />
    public override TieValue Clone() => new TieInteger(Value, Suffix);

    /// <summary>取 long 值；越界抛 <see cref="OverflowException"/>。</summary>
    public long AsLong() => (long)Value;

    /// <summary>取 int 值；越界抛 <see cref="OverflowException"/>。</summary>
    public int AsInt() => (int)Value;

    /// <summary>取 double 近似值。</summary>
    public double AsDouble() => (double)Value;

    /// <inheritdoc />
    // 数值相等不区分后缀：42i32 与 42i64 相等。
    public override bool Equals(object? obj) => obj is TieInteger i && i.Value == Value;

    /// <inheritdoc />
    public override int GetHashCode() => Value.GetHashCode();

    /// <summary>输出原始字面量形态，如 "42"、"7u8"、"-3i32"。</summary>
    public override string ToString()
    {
        var text = Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return Suffix == TieIntegerSuffix.None ? text : text + SuffixText(Suffix);
    }

    internal static string SuffixText(TieIntegerSuffix suffix) => suffix switch
    {
        TieIntegerSuffix.I8 => "i8",
        TieIntegerSuffix.I16 => "i16",
        TieIntegerSuffix.I32 => "i32",
        TieIntegerSuffix.I64 => "i64",
        TieIntegerSuffix.I128 => "i128",
        TieIntegerSuffix.U8 => "u8",
        TieIntegerSuffix.U16 => "u16",
        TieIntegerSuffix.U32 => "u32",
        TieIntegerSuffix.U64 => "u64",
        TieIntegerSuffix.U128 => "u128",
        _ => string.Empty,
    };
}

/// <summary>浮点节点：double 存储，可带 f32/f64 后缀；无后缀按 tie 语义为 f64。</summary>
public sealed class TieFloat : TieValue
{
    /// <summary>浮点值。</summary>
    public double Value { get; set; }

    /// <summary>窄后缀；<see cref="TieFloatSuffix.None"/> 为默认 f64。</summary>
    public TieFloatSuffix Suffix { get; set; }

    /// <summary>构造浮点节点（默认 f64）。</summary>
    public TieFloat(double value) : this(value, TieFloatSuffix.None) { }

    /// <summary>构造浮点节点并指定后缀。</summary>
    public TieFloat(double value, TieFloatSuffix suffix)
    {
        Value = value;
        Suffix = suffix;
    }

    /// <inheritdoc />
    public override TieValueKind Kind => TieValueKind.Float;

    /// <inheritdoc />
    public override TieValue Clone() => new TieFloat(Value, Suffix);

    /// <summary>取 float 值。</summary>
    public float AsSingle() => (float)Value;

    /// <summary>取 double 值。</summary>
    public double AsDouble() => Value;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is TieFloat f && f.Value.Equals(Value) && f.Suffix == Suffix;

    /// <inheritdoc />
    public override int GetHashCode() => Value.GetHashCode() ^ (int)Suffix * 397;

    /// <summary>输出字面量形态，如 "3.14"、"1.5f32"；保证带小数点或指数以与整数区分。</summary>
    public override string ToString() => TieWriter.FormatFloat(Value, Suffix);
}

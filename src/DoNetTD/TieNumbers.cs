// Copyright (c) 2026 TIE-LANG organization. All rights reserved.
// Licensed under the TIE-LANG Open Source License v1.1 (see LICENSE in the repository root).

using System.Numerics;

namespace DoNetTD;

/// <summary>整数节点：内部以 BigInteger 存储，支撑 i8..i128/u8..u128 全部窄类型。</summary>
public sealed class TieInteger : TieValue
{
    private BigInteger _value;
    private TieIntegerSuffix _suffix;

    /// <summary>整数值。</summary>
    public BigInteger Value { get => _value; set { EnsureMutable(); _value = value; } }

    /// <summary>
    /// 窄后缀。<see cref="TieIntegerSuffix.None"/> 表示未标注（tie 语义默认 i64），
    /// 写出时不附加后缀；显式解析到 "42i32" 时为 I32 并在写出时保留。
    /// </summary>
    public TieIntegerSuffix Suffix { get => _suffix; set { EnsureMutable(); _suffix = value; } }

    /// <summary>构造整数节点（无后缀）。</summary>
    public TieInteger(BigInteger value) : this(value, TieIntegerSuffix.None) { }

    /// <summary>构造整数节点并指定窄后缀。</summary>
    public TieInteger(BigInteger value, TieIntegerSuffix suffix)
    {
        _value = value;
        _suffix = suffix;
    }

    /// <inheritdoc />
    public override TieValueKind Kind => TieValueKind.Integer;

    /// <inheritdoc />
    protected override TieValue CloneCore() => new TieInteger(Value, Suffix);

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
    private double _value;
    private TieFloatSuffix _suffix;

    /// <summary>浮点值。F32 后缀的节点存储的是 float 规范化后的值（写出/相等的语义基准）。</summary>
    public double Value { get => _value; set { EnsureMutable(); _value = Canonicalize(value, Suffix); } }

    /// <summary>窄后缀；<see cref="TieFloatSuffix.None"/> 为默认 f64。切换到 F32 时立即规范化存储值。</summary>
    public TieFloatSuffix Suffix
    {
        get => _suffix;
        set { EnsureMutable(); _suffix = value; _value = Canonicalize(_value, value); }
    }

    /// <summary>构造浮点节点（默认 f64）。</summary>
    public TieFloat(double value) : this(value, TieFloatSuffix.None) { }

    /// <summary>
    /// 构造浮点节点并指定后缀。F32 节点会把存储值规范化为 float 精度——
    /// 这保证 写出→重解析 的往返相等，也使 Equals 以字面量真实精度为准。
    /// </summary>
    public TieFloat(double value, TieFloatSuffix suffix)
    {
        _suffix = suffix;
        _value = Canonicalize(value, suffix);
    }

    private static double Canonicalize(double v, TieFloatSuffix suffix) =>
        suffix == TieFloatSuffix.F32 ? (double)(float)v : v;

    /// <inheritdoc />
    public override TieValueKind Kind => TieValueKind.Float;

    /// <inheritdoc />
    protected override TieValue CloneCore() => new TieFloat(Value, Suffix);

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

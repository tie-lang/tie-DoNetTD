// Copyright (c) 2026 TIE-LANG organization. All rights reserved.
// Licensed under the TIE-LANG Open Source License v1.1 (see LICENSE in the repository root).

using System.Numerics;

namespace DoNetTD;

/// <summary>tie:data 空值节点。tie:data 语法没有 null 字面量，写出时抛异常；用于 JSON 互转与编程构造。</summary>
public sealed class TieNull : TieValue
{
    /// <summary>全局唯一实例。</summary>
    public static TieNull Instance { get; } = new TieNull();

    private TieNull() { }

    /// <inheritdoc />
    public override TieValueKind Kind => TieValueKind.Null;

    /// <inheritdoc />
    protected override TieValue CloneCore() => Instance;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is TieNull;

    /// <inheritdoc />
    public override int GetHashCode() => unchecked((int)TieValueKind.Null);

    /// <summary>固定返回 "null"（诊断用途；该形态不是合法 tie:data 输出）。</summary>
    public override string ToString() => "null";
}

/// <summary>布尔节点：true / false。</summary>
public sealed class TieBool : TieValue
{
    private bool _value;

    /// <summary>布尔值。</summary>
    public bool Value { get => _value; set { EnsureMutable(); _value = value; } }

    /// <summary>构造布尔节点。</summary>
    public TieBool(bool value) => Value = value;

    /// <summary>便捷构造 true 节点。</summary>
    public static TieBool True { get; } = new TieBool(true);

    /// <summary>便捷构造 false 节点。</summary>
    public static TieBool False { get; } = new TieBool(false);

    /// <inheritdoc />
    public override TieValueKind Kind => TieValueKind.Bool;

    /// <inheritdoc />
    protected override TieValue CloneCore() => new TieBool(Value);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is TieBool b && b.Value == Value;

    /// <inheritdoc />
    public override int GetHashCode() => Value ? 1 : 0;

    /// <summary>输出 tie 字面量形式："true" / "false"。</summary>
    public override string ToString() => Value ? "true" : "false";
}

/// <summary>
/// 平衡三进制节点：-1 / 0 / +1。解析器仅接受裸关键字 zero（值 0）；
/// 正负值可编程构造，但非零 trit 没有无类型的 tie:data 字面量形式，
/// 写出时抛 <see cref="InvalidOperationException"/>。
/// </summary>
public sealed class TieTrit : TieValue
{
    private int _value;

    /// <summary>三进制值：仅允许 -1、0、+1。</summary>
    public int Value { get => _value; set { EnsureMutable(); _value = value; } }

    /// <summary>构造 trit 节点；value 仅允许 -1/0/+1。</summary>
    public TieTrit(int value)
    {
        if (value < -1 || value > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "trit 值只能是 -1、0 或 +1");
        }
        Value = value;
    }

    /// <summary>zero（0）。</summary>
    public static TieTrit Zero { get; } = new TieTrit(0);

    /// <summary>+1。</summary>
    public static TieTrit Positive { get; } = new TieTrit(1);

    /// <summary>-1。</summary>
    public static TieTrit Negative { get; } = new TieTrit(-1);

    /// <inheritdoc />
    public override TieValueKind Kind => TieValueKind.Trit;

    /// <inheritdoc />
    protected override TieValue CloneCore() => new TieTrit(Value);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is TieTrit t && t.Value == Value;

    /// <inheritdoc />
    public override int GetHashCode() => Value;

    /// <summary>输出关键字形式："+1"/"zero"/"-1"（注意 ±1 不是合法无类型 tie:data 字面量）。</summary>
    public override string ToString() =>
        Value == 0 ? "zero" : (Value > 0 ? "+1" : "-1");
}

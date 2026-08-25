// Copyright (c) 2026 TIE-LANG organization. All rights reserved.
// Licensed under the TIE-LANG Open Source License v1.1 (see LICENSE in the repository root).

namespace DoNetTD;

/// <summary>
/// tie:data 文档节点的抽象基类。所有节点都是可变引用类型；
/// 结构相等通过 <see cref="Equals(object)"/> 递归比较（表按键集合比较、数组按序比较、
/// 整数按数值比较不区分后缀）。
/// </summary>
public abstract class TieValue
{
    /// <summary>节点种类。</summary>
    public abstract TieValueKind Kind { get; }

    /// <summary>共享的空值单例。</summary>
    public static TieNull Null => TieNull.Instance;

    /// <summary>深拷贝当前子树（返回的新节点与本节点 Equals 相等）。</summary>
    public abstract TieValue Clone();

    /// <summary>
    /// 结构相等：递归比较。整数只比数值（42i32 与 42i64 相等）；
    /// 表按键集合+各键值比较（与插入顺序无关）；数组逐元素按序比较。
    /// </summary>
    public abstract override bool Equals(object? obj);

    /// <summary>与 <see cref="Equals(object)"/> 一致的散列。</summary>
    public abstract override int GetHashCode();

    /// <summary>结构相等运算符（等同 <see cref="Equals(object)"/>）。</summary>
    public static bool operator ==(TieValue? left, TieValue? right)
    {
        if (left is null) return right is null;
        return left.Equals(right);
    }

    /// <summary>结构不等运算符。</summary>
    public static bool operator !=(TieValue? left, TieValue? right) => !(left == right);
}

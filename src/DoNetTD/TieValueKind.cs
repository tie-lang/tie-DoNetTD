// Copyright (c) 2026 TIE-LANG organization. All rights reserved.
// Licensed under the TIE-LANG Open Source License v1.1 (see LICENSE in the repository root).

namespace DoNetTD;

/// <summary>
/// tie:data 节点种类。与 tie 语言基本类型体系对应：
/// 标量七种 + 数组 + 表。
/// </summary>
public enum TieValueKind
{
    /// <summary>空值。tie:data 语法本身没有 null 字面量，此节点用于 JSON 互转与编程构造。</summary>
    Null,

    /// <summary>布尔：<c>true</c> / <c>false</c>。</summary>
    Bool,

    /// <summary>平衡三进制：解析器接受裸关键字 <c>zero</c>（值 0）；正负值仅可编程构造。</summary>
    Trit,

    /// <summary>字符字面量：<c>'a'</c>，Unicode 码点。</summary>
    Char,

    /// <summary>字符串：<c>"..."</c> 双引号。</summary>
    String,

    /// <summary>整数：十进制，可带窄后缀（i8..u128），内部以 <see cref="System.Numerics.BigInteger"/> 存储。</summary>
    Integer,

    /// <summary>浮点：小数/指数形式，可带 f32/f64 后缀，默认 f64。</summary>
    Float,

    /// <summary>数组：<c>[v1, v2]</c>。</summary>
    Array,

    /// <summary>表（键值对集合）：键恒为字符串，tie 语义下按键 strcmp 字节序有序存储。</summary>
    Table,
}

// Copyright (c) 2026 TIE-LANG organization. All rights reserved.
// Licensed under the TIE-LANG Open Source License v1.1 (see LICENSE in the repository root).

namespace DoNetTD;

/// <summary>
/// 整数窄类型后缀。<see cref="None"/> 表示未显式标注（按 tie 语言语义默认 i64），
/// 写出时不附加后缀文本。
/// </summary>
public enum TieIntegerSuffix
{
    /// <summary>无后缀（默认 i64 语义）。</summary>
    None,

    I8,
    I16,
    I32,
    I64,
    I128,
    U8,
    U16,
    U32,
    U64,
    U128,
}

/// <summary>
/// 浮点窄类型后缀。<see cref="DoNetTD.TieFloatSuffix.None"/> 表示未显式标注（默认 f64）。
/// </summary>
public enum TieFloatSuffix
{
    /// <summary>无后缀（默认 f64 语义）。</summary>
    None,

    F32,
    F64,
}

/// <summary>
/// 表键的枚举顺序：<see cref="SortStrcmp"/> 为 tie map 官方语义（按键 strcmp 字节序，
/// 即 UTF-8 字节序列字典序），<see cref="InsertionOrder"/> 保留插入序。
/// </summary>
public enum TableKeyOrder
{
    /// <summary>按键 strcmp 字节序排序（UTF-8 字节序，tie map 官方输出顺序）。</summary>
    SortStrcmp,

    /// <summary>保留插入序。</summary>
    InsertionOrder,
}

/// <summary>诊断严重级别。</summary>
public enum TieDiagnosticSeverity
{
    /// <summary>错误：解析或校验失败。</summary>
    Error,

    /// <summary>警告：不影响结果。</summary>
    Warning,
}

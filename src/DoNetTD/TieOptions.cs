// Copyright (c) 2026 TIE-LANG organization. All rights reserved.
// Licensed under the TIE-LANG Open Source License v1.1 (see LICENSE in the repository root).

namespace DoNetTD;

/// <summary>解析选项。全部有合理默认值，通常直接传 null 使用默认。</summary>
public sealed class TieParseOptions
{
    /// <summary>重复键是否报错；默认 false——后值覆盖前值（保持原插入位置）。</summary>
    public bool StrictDuplicateKeys { get; set; }

    /// <summary>是否强制要求文件头部出现 type tie&lt;data&gt; 声明；默认 false。</summary>
    public bool RequireHeader { get; set; }

    /// <summary>允许标量作为根值；默认 true（宽容）。tie:data 实践中根是表或数组，设为 false 可收紧校验。</summary>
    public bool AllowScalarRoot { get; set; } = true;

    /// <summary>容器最大嵌套深度，超过报「嵌套过深」；默认 256。</summary>
    public int MaxDepth { get; set; } = 256;

    /// <summary>
    /// 收集全部错误而非首个即停。开启后解析器在容器条目级做恢复与重同步，
    /// 最终以 <see cref="TieParseException"/> 抛出完整诊断列表（类似编译器的批量报错）。
    /// 默认 false。
    /// </summary>
    public bool CollectAllErrors { get; set; }

    /// <summary>收集模式下的错误数上限，超过即中止（防病态输入拖死）；默认 100。</summary>
    public int MaxErrors { get; set; } = 100;

    /// <summary>共享默认选项实例。</summary>
    public static TieParseOptions Default { get; } = new TieParseOptions();
}

/// <summary>写出选项。</summary>
public sealed class TieWriteOptions
{
    /// <summary>美化输出（多行缩进）；默认 true。false 时输出紧凑单行。</summary>
    public bool Pretty { get; set; } = true;

    /// <summary>缩进文本；默认 4 空格（tie 官方样例风格）。</summary>
    public string Indent { get; set; } = "    ";

    /// <summary>表键输出顺序；默认 SortStrcmp（tie map 官方语义）。</summary>
    public TableKeyOrder KeyOrder { get; set; } = TableKeyOrder.SortStrcmp;

    /// <summary>容器条目末尾补逗号；默认 true（官方样例风格且被解析器容忍）。</summary>
    public bool TrailingComma { get; set; } = true;

    /// <summary>是否在文档开头输出 "type tie&lt;data&gt;" 头部声明；默认 false。</summary>
    public bool EmitHeader { get; set; }

    /// <summary>纯标量数组压成一行输出（如 ["a", "b", "c"]）；默认 false。仅 Pretty 模式生效。</summary>
    public bool CompactArraysOfScalars { get; set; }

    /// <summary>
    /// 还原节点上挂载的注释（前导逐行输出、尾随跟在值后）。仅 Pretty 模式生效；
    /// 紧凑模式天然丢弃注释。默认 false。
    /// </summary>
    public bool PreserveComments { get; set; }
}

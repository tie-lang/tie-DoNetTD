// Copyright (c) 2026 TIE-LANG organization. All rights reserved.
// Licensed under the TIE-LANG Open Source License v1.1 (see LICENSE in the repository root).

namespace DoNetTD;

/// <summary>诊断条目：级别、中文消息、位置（行列从 1 计；来源不明时为 0）。</summary>
public sealed record TieDiagnostic(
    TieDiagnosticSeverity Severity,
    string Message,
    int Line,
    int Column,
    int Position)
{
    /// <summary>人类可读形态："第 L 行，第 C 列: 消息"；无位置信息时仅消息。</summary>
    public override string ToString()
    {
        if (Line <= 0)
        {
            return Message;
        }
        return $"第 {Line} 行，第 {Column} 列: {Message}";
    }
}

/// <summary>
/// tie:data 解析异常。携带首个错误的 <see cref="TieDiagnostic"/>；
/// 通过 <see cref="TieDocument.TryParse"/> 可拿到非抛出的诊断列表形式。
/// </summary>
public sealed class TieParseException : Exception
{
    /// <summary>导致失败的诊断（通常一条）。</summary>
    public IReadOnlyList<TieDiagnostic> Diagnostics { get; }

    /// <summary>用诊断构造解析异常。</summary>
    public TieParseException(IReadOnlyList<TieDiagnostic> diagnostics)
        : base(diagnostics.Count > 0 ? diagnostics[0].ToString() : "tie:data 解析失败")
    {
        Diagnostics = diagnostics;
    }

    internal static TieParseException Single(string message, int line, int column, int position)
    {
        return new TieParseException(new[]
        {
            new TieDiagnostic(TieDiagnosticSeverity.Error, message, line, column, position),
        });
    }
}

// Copyright (c) 2026 TIE-LANG organization. All rights reserved.
// Licensed under the TIE-LANG Open Source License v1.1 (see LICENSE in the repository root).

namespace DoNetTD.Convert;

/// <summary>文档级便捷扩展：直接把根值映射为 .NET 对象。</summary>
public static class TieDocumentObjectExtensions
{
    /// <summary>把文档根值映射为 <typeparamref name="T"/> 实例。</summary>
    public static T? ToObject<T>(this TieDocument document)
    {
        if (document is null) throw new ArgumentNullException(nameof(document));
        return TieObjectMapper.ToObject<T>(document.Root);
    }
}

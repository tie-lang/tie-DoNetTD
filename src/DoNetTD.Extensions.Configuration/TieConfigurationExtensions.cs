// Copyright (c) 2026 TIE-LANG organization. All rights reserved.
// Licensed under the TIE-LANG Open Source License v1.1 (see LICENSE in the repository root).

using Microsoft.Extensions.Configuration;

namespace DoNetTD.Extensions.Configuration;

/// <summary><see cref="IConfigurationBuilder"/> 的 tie:data 扩展。</summary>
public static class TieConfigurationExtensions
{
    /// <summary>把 tie:data 文件加入配置源。</summary>
    /// <param name="builder">配置构建器。</param>
    /// <param name="path">tie:data 文件相对/绝对路径。</param>
    /// <param name="optional">文件不存在时是否忽略（false 则抛异常）。</param>
    /// <param name="reloadOnChange">文件变更时自动重载。</param>
    public static IConfigurationBuilder AddTieFile(
        this IConfigurationBuilder builder,
        string path,
        bool optional = false,
        bool reloadOnChange = false)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        return builder.Add(new TieConfigurationSource
        {
            Path = path,
            Optional = optional,
            ReloadOnChange = reloadOnChange,
        });
    }
}

// Copyright (c) 2026 TIE-LANG organization. All rights reserved.
// Licensed under the TIE-LANG Open Source License v1.1 (see LICENSE in the repository root).

namespace DoNetTD.Tests;

/// <summary>官方样例 fixture（docs/cli.md 与 compiler/config.tie 原文）。</summary>
internal static class Fixtures
{
    /// <summary>docs/cli.md 多文件并行编译配置原文。</summary>
    public const string CliConfig =
        """
        type tie<data>
        [
            "advanced": [
                "enabled": true,
                "threads": 0,        // 0 = 按 CPU 核数自动
            ],
            "cache": [
                "size": 268435456,   // 256MB
                "storage": "memory", // memory / file
                "path": ".tie-cache",
            ],
        ]
        """;

    /// <summary>compiler/config.tie 头部注释的完整配置样例（去注释重排为合法文本）。</summary>
    public const string FullConfig =
        """
        type tie<data>
        [
            "target": "win-x64",
            "opt": 2,
            "debug": true,
            "profile": "dev",
            "tiec": [
                "backend": "win32",
                "features": ["async", "macro", "unsafe"],
                "emit": "exe",
                "link": ["user32", "gdi32"],
                "bounds_check": true,
            ],
            "prep": [
                "modules": ["migrate_str_v1.tie"],
                "strict_roles": true,
            ],
            "pkg": [
                "registry": "https://reg.tie-lang.org",
                "cache_dir": ".tie/deps",
                "verify_signature": true,
            ],
            "roles": [
                "test": ["tests/"],
                "bench": ["bench/"],
            ],
            "advanced": [ "enabled": true, "threads": 0 ],
            "cache": [ "size": 268435456, "storage": "memory", "path": ".tie-cache" ],
        ]
        """;
}

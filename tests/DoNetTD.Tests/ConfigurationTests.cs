// Copyright (c) 2026 TIE-LANG organization. All rights reserved.
// Licensed under the TIE-LANG Open Source License v1.1 (see LICENSE in the repository root).

using Microsoft.Extensions.Configuration;
using Xunit;
using DoNetTD.Extensions.Configuration;

namespace DoNetTD.Tests;

/// <summary>MECI 桥接：tie:data 作为 IConfiguration 配置源。</summary>
public class ConfigurationTests
{
    [Fact]
    public void AddTieFile_FlatReads()
    {
        const string tie = """
            [
                "target": "win-x64",
                "opt": 2,
                "debug": true,
                "tiec": [
                    "backend": "win32",
                    "features": ["async", "macro"],
                ],
            ]
            """;
        var path = "tdc-cfg-" + Guid.NewGuid().ToString("N") + ".data.tie";
        File.WriteAllText(path, tie);
        try
        {
            var config = new ConfigurationBuilder()
                .AddTieFile(path)
                .Build();

            Assert.Equal("win-x64", config["target"]);
            Assert.Equal("2", config["opt"]);
            Assert.Equal("true", config["debug"]);
            Assert.Equal("win32", config["tiec:backend"]);
            Assert.Equal("async", config["tiec:features:0"]);
            Assert.Equal("macro", config["tiec:features:1"]);

            // 绑定到 POCO（MECI 标准能力）
            var bound = config.GetSection("tiec").Get<TiecSec>();
            Assert.NotNull(bound);
            Assert.Equal("win32", bound!.Backend);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private sealed class TiecSec
    {
        public string Backend { get; set; } = "";
    }

    [Fact]
    public void AddTieFile_Optional_MissingFileOk()
    {
        var config = new ConfigurationBuilder()
            .AddTieFile(Path.Combine(Path.GetTempPath(), "no-such-" + Guid.NewGuid().ToString("N") + ".tie"),
                optional: true)
            .Build();
        Assert.Null(config["anything"]);
    }
}

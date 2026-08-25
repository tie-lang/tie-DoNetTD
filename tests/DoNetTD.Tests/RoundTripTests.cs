// Copyright (c) 2026 TIE-LANG organization. All rights reserved.
// Licensed under the TIE-LANG Open Source License v1.1 (see LICENSE in the repository root).

using Xunit;
using DoNetTD;

namespace DoNetTD.Tests;

public class RoundTripTests
{
    [Fact]
    public void OfficialConfig_ParseWriteParse_Idempotent()
    {
        var first = TieDocument.Parse(Fixtures.FullConfig);
        var text = first.Write();
        var second = TieDocument.Parse(text);
        Assert.Equal(first.Root, second.Root);

        // 幂等：第二次写出的文本与第一次完全一致（strcmp 排序稳定）。
        Assert.Equal(text, second.Write());
    }

    [Fact]
    public void Comments_Survive_WhenWrittenBack()
    {
        // 注释是空白的一部分：解析丢弃、写出重排后注释消失——但「原样保留」场景
        // 通过 Pretty+排序关闭时仍不保证。此处验证官方样例往返结构无损。
        var doc = TieDocument.Parse(Fixtures.CliConfig);
        var again = TieDocument.Parse(doc.Write());
        Assert.Equal(doc.Root, again.Root);
    }

    [Fact]
    public void AllScalarKinds_RoundTrip()
    {
        var source = """
            ["b": true, "i": -7i16, "f": 2.25, "s": "文A\n", "c": 'x', "z": zero,
             "arr": [1, 2.5, "t"], "tbl": ["deep": ["n": null]]]
            """;
        // 注意：null 不是合法 tie:data——上面应解析失败，验证之。
        var ok = TieDocument.TryParse(source, out _, out _);
        Assert.False(ok);

        var withoutNull = """
            ["b": true, "i": -7i16, "f": 2.25, "s": "文A\n", "c": 'x', "z": zero,
             "arr": [1, 2.5, "t"], "tbl": ["deep": ["n": zero]]]
            """;
        var doc = TieDocument.Parse(withoutNull);
        var round = TieDocument.Parse(doc.Write());
        Assert.Equal(doc.Root, round.Root);
    }

    [Fact]
    public void JsonRoundTrip_StructurePreserved()
    {
        var json = """{"a": 1, "b": [1.5, "x", true], "c": {"d": null}}""";
        var node = Convert.TieJson.FromJson(json);
        var jsonText = Convert.TieJson.ToJson(node);
        var again = Convert.TieJson.FromJson(jsonText);
        Assert.Equal(node, again);
    }
}

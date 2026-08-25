// Copyright (c) 2026 TIE-LANG organization. All rights reserved.
// Licensed under the TIE-LANG Open Source License v1.1 (see LICENSE in the repository root).

using Xunit;
using DoNetTD;

namespace DoNetTD.Tests;

/// <summary>注释保留往返（v0.2）。</summary>
public class CommentRoundTripTests
{
    [Fact]
    public void OfficialCliConfig_TrailingComments_Attached()
    {
        var doc = TieDocument.Parse(Fixtures.CliConfig);
        var advanced = (TieTable)((TieTable)doc.Root)["advanced"]!;
        // "threads": 0,        // 0 = 按 CPU 核数自动
        Assert.Equal("0 = 按 CPU 核数自动", advanced["threads"]!.TrailingComment);

        var cache = (TieTable)((TieTable)doc.Root)["cache"]!;
        Assert.Equal("256MB", cache["size"]!.TrailingComment);
        Assert.Equal("memory / file", cache["storage"]!.TrailingComment);
        // enabled 无注释
        Assert.Null(advanced["enabled"]!.TrailingComment);
        Assert.False(advanced["enabled"]!.HasComments);
    }

    [Fact]
    public void LeadingComments_FlushedOntoNextValue()
    {
        var doc = TieDocument.Parse("""
            [
                // 目标平台
                // 只能是 win-x64 或 linux-x64
                "target": "win-x64",
            ]
            """);
        var target = ((TieTable)doc.Root)["target"]!;
        Assert.Equal(2, target.LeadingComments.Count);
        Assert.Equal("目标平台", target.LeadingComments[0]);
        Assert.Equal("只能是 win-x64 或 linux-x64", target.LeadingComments[1]);
    }

    [Fact]
    public void Write_PreserveComments_RoundTrips()
    {
        var doc = TieDocument.Parse(Fixtures.CliConfig);
        var text = doc.Write(new TieWriteOptions { PreserveComments = true });
        var again = TieDocument.Parse(text);

        // 注释内容与归属一致（键序重排不影响随键注释）
        var adv1 = (TieTable)((TieTable)doc.Root)["advanced"]!;
        var adv2 = (TieTable)((TieTable)again.Root)["advanced"]!;
        Assert.Equal(adv1["threads"]!.TrailingComment, adv2["threads"]!.TrailingComment);
        Assert.Equal(adv1["enabled"]!.HasComments, adv2["enabled"]!.HasComments);

        var c1 = (TieTable)((TieTable)doc.Root)["cache"]!;
        var c2 = (TieTable)((TieTable)again.Root)["cache"]!;
        Assert.Equal(c1["size"]!.TrailingComment, c2["size"]!.TrailingComment);
        Assert.Equal(c1["path"]!.TrailingComment, c2["path"]!.TrailingComment);

        // 再写一次文本稳定（幂等）
        Assert.Equal(text, again.Write(new TieWriteOptions { PreserveComments = true }));
    }

    [Fact]
    public void Comments_NotEmitted_WithoutOption()
    {
        var doc = TieDocument.Parse(Fixtures.CliConfig);
        var text = doc.Write(); // 默认不还原注释
        Assert.DoesNotContain("//", text);
    }

    [Fact]
    public void Clone_CarriesComments()
    {
        var node = TieDocument.Parse("""["k": 1] // 尾""").Root;
        Assert.NotNull(node.TrailingComment);
        var copy = node.Clone();
        Assert.Equal(node.TrailingComment, copy.TrailingComment);
    }

    [Fact]
    public void Equality_IgnoresComments()
    {
        var withC = TieDocument.Parse("""["k": 1] // x""").Root;
        var withoutC = TieDocument.Parse("""["k": 1]""").Root;
        Assert.Equal(withoutC, withC); // 相等不受注释影响
    }
}

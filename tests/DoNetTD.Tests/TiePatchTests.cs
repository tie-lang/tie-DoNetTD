// Copyright (c) 2026 TIE-LANG organization. All rights reserved.
// Licensed under the TIE-LANG Open Source License v1.1 (see LICENSE in the repository root).

using Xunit;
using DoNetTD;
using DoNetTD.Advanced;

namespace DoNetTD.Tests;

/// <summary>Diff → Patch → Apply 闭环。</summary>
public class TiePatchTests
{
    [Fact]
    public void DiffToPatch_RoundTrip()
    {
        var a = TieDocument.Parse("""
            ["target": "win-x64", "opt": 2, "old": true]
            """);
        var b = TieDocument.Parse("""
            ["target": "linux-x64", "opt": 3, "added": ["x"]]
            """);

        var diffs = TieDiff.Compare(a.Root, b.Root);
        var patch = TiePatch.ToPatch(diffs);

        // 补丁文档本身是合法 tie:data，可写可解析
        var patchText = TieDocument.FromValue(patch).Write();
        var reparsedPatch = TieDocument.Parse(patchText).Root;

        var result = TiePatch.ApplyTo(a.Root, reparsedPatch);
        Assert.Equal(b.Root, result);
        // 原 a 未被修改
        Assert.Equal(2L, ((TieInteger)((TieTable)a.Root)["opt"]!).AsLong());
    }

    [Fact]
    public void Apply_InPlace()
    {
        var target = TieDocument.Parse("""["k": 1]""");
        var patch = TieDocument.Parse("""
            [["op": "changed", "path": "k", "value": 2],
             ["op": "added", "path": "n", "value": "x"],
             ["op": "removed", "path": "ghost"]]
            """).Root;

        TiePatch.Apply(target.Root, patch);
        // 直接断言结构
        Assert.Equal(2L, ((TieInteger)((TieTable)target.Root)["k"]!).AsLong());
        Assert.Null(((TieTable)target.Root)["ghost"]); // removed 无命中默认宽容跳过
        Assert.NotNull(((TieTable)target.Root)["n"]);
    }

    [Fact]
    public void Apply_StrictMode_ThrowsOnMissingRemove()
    {
        var target = TieDocument.Parse("""["k": 1]""").Root;
        var patch = TieDocument.Parse("""[["op": "removed", "path": "nope"]]""").Root;
        Assert.Throws<InvalidOperationException>(() => TiePatch.Apply(target, patch, throwOnMismatch: true));
    }

    [Fact]
    public void Apply_MalformedOps_Throw()
    {
        var target = TieDocument.Parse("""["k": 1]""").Root;
        Assert.Throws<InvalidOperationException>(() => TiePatch.Apply(target,
            TieDocument.Parse("""[["op": "explode", "path": "k"]]""").Root));
        Assert.Throws<InvalidOperationException>(() => TiePatch.Apply(target,
            TieDocument.Parse("""[["op": "added", "path": "k"]]""").Root)); // 缺 value

        var notArray = new TieTable().SetItem("not", new TieString("array"));
        Assert.Throws<InvalidOperationException>(() => TiePatch.Apply(target, notArray)); // 根不是数组
    }
}

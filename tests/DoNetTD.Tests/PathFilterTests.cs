// Copyright (c) 2026 TIE-LANG organization. All rights reserved.
// Licensed under the TIE-LANG Open Source License v1.1 (see LICENSE in the repository root).

using Xunit;
using DoNetTD;
using DoNetTD.Advanced;

namespace DoNetTD.Tests;

/// <summary>TiePath v0.3 增强：负索引、区间切片、过滤器。</summary>
public class PathFilterTests
{
    private static readonly TieValue Sample = TieDocument.Parse("""
        [
            "items": [
                ["name": "dev", "opt": 0],
                ["name": "ci", "opt": 2],
                ["name": "prod", "opt": 3],
            ],
            "mixed": ["a", "b", "c", "d"],
        ]
        """).Root;

    [Fact]
    public void NegativeIndex_FromEnd()
    {
        Assert.Equal("prod", ((TieString)((TieTable)TiePath.Get(Sample, "items[-1]")!)["name"]!).Value);
        Assert.Equal("ci", ((TieString)((TieTable)TiePath.Get(Sample, "items[-2]")!)["name"]!).Value);
        // 混合数组
        Assert.Equal("d", ((TieString)TiePath.Get(Sample, "mixed[-1]")!).Value);
    }

    [Fact]
    public void RangeSlice_LeftClosedRightOpen()
    {
        var all = TiePath.GetAll(Sample, "mixed[1..3]");
        Assert.Equal(2, all.Count); // 下标 1、2
        Assert.Equal("b", ((TieString)all[0]).Value);
        Assert.Equal("c", ((TieString)all[1]).Value);

        Assert.Equal(4, TiePath.GetAll(Sample, "mixed[..]").Count);   // 全部
        Assert.Equal(2, TiePath.GetAll(Sample, "mixed[..2]").Count);  // 前 2 个
        Assert.Equal(2, TiePath.GetAll(Sample, "mixed[2..]").Count);  // 从下标 2 到末尾

        Assert.Empty(TiePath.GetAll(Sample, "mixed[99..100]"));       // 空区间合法
    }

    [Fact]
    public void Filter_NumericComparison()
    {
        var hits = TiePath.GetAll(Sample, """items[?(@.opt>1)]""");
        Assert.Equal(2, hits.Count); // ci(2)、prod(3)

        var first = TiePath.Get(Sample, """items[?(@.opt>=2)]""");
        Assert.Equal("ci", ((TieString)((TieTable)first!)["name"]!).Value);

        Assert.Equal("dev", ((TieString)((TieTable)TiePath.Get(Sample, """items[?(@.opt<1)]""")!)["name"]!).Value);
        Assert.Null(TiePath.Get(Sample, """items[?(@.opt==9)]"""));
    }

    [Fact]
    public void Filter_Equality_OnStrings_AndTableValues()
    {
        var byName = TiePath.Get(Sample, """items[?(@.name=="ci")]""");
        Assert.NotNull(byName);
        Assert.Equal(2L, ((TieInteger)((TieTable)byName!)["opt"]!).AsLong());

        // 表过滤：直接在容器上过滤其子表（@ 指向每个子值）
        var tableRoot = TieDocument.Parse("""
            ["servers": ["a": ["on": true], "b": ["on": false]]]
            """).Root;
        var onServers = TiePath.GetAll(tableRoot, """servers[?(@.on==true)]""");
        Assert.Single(onServers);
        Assert.True(((TieBool)((TieTable)onServers[0])["on"]!).Value);
    }

    [Fact]
    public void Chained_FilterThenIndex()
    {
        // 过滤结果逐候选应用后续段：候选是表时 [n] 无意义（返回空），
        // 候选是数组时 [n] 取每个数组的第 n 个元素。
        var all = TiePath.GetAll(Sample, """items[?(@.opt>1)]""");
        Assert.Equal(2, all.Count);
        Assert.Equal("prod", ((TieString)((TieTable)all[1])["name"]!).Value);

        var grid = TieDocument.Parse("""
            ["rows": [["r": 1, "v": 11], ["r": 2, "v": 21]]]
            """).Root;
        // 过滤后再链式取字段（@ 仅支持直接键；@.key[n] 索引形式不在 v0.3 语法内）
        var vs = TiePath.GetAll(grid, """rows[?(@.r>1)].v""");
        Assert.Single(vs);
        Assert.Equal(21L, ((TieInteger)vs[0]).AsLong());
    }

    [Fact]
    public void SetRemove_RejectRangeAndFilter()
    {
        Assert.Throws<ArgumentException>(() => TiePath.Set(Sample, "mixed[1..2]", new TieInteger(1)));
        Assert.Throws<ArgumentException>(() => TiePath.Remove(Sample, """items[?(@.opt>1)]"""));
    }

    [Fact]
    public void MalformedFilters_Throw()
    {
        Assert.Throws<ArgumentException>(() => TiePath.Get(Sample, """items[?(@.opt~1)]"""));   // 非法运算符
        Assert.Throws<ArgumentException>(() => TiePath.Get(Sample, """items[?(x.opt>1)]"""));   // 缺 @.
        Assert.Throws<ArgumentException>(() => TiePath.Get(Sample, """items[?(@.k>"unterminated]""")); // 未闭合
    }
}

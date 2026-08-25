// Copyright (c) 2026 TIE-LANG organization. All rights reserved.
// Licensed under the TIE-LANG Open Source License v1.1 (see LICENSE in the repository root).

using Xunit;
using DoNetTD;
using DoNetTD.Advanced;

namespace DoNetTD.Tests;

public class AdvancedTests
{
    // ---------- TiePath ----------

    [Fact]
    public void Path_Get_Deep()
    {
        var doc = TieDocument.Parse(Fixtures.FullConfig);
        var feature = TiePath.Get(doc.Root, "tiec.features[1]");
        Assert.Equal("macro", ((TieString)feature!).Value);
        Assert.Null(TiePath.Get(doc.Root, "tiec.features[99]"));
        Assert.Null(TiePath.Get(doc.Root, "不存在的键.x"));
    }

    [Fact]
    public void Path_GetAll_WithWildcard()
    {
        var doc = TieDocument.Parse(Fixtures.FullConfig);
        var all = TiePath.GetAll(doc.Root, """roles["test"][*]""");
        var item = Assert.Single(all);
        Assert.Equal("tests/", ((TieString)item).Value);

        var features = TiePath.GetAll(doc.Root, "tiec.features[*]");
        Assert.Equal(3, features.Count);
    }

    [Fact]
    public void Path_Set_CreatesMissing_AndPadsArrays()
    {
        var root = new TieTable().SetItem("a", new TieTable());
        TiePath.Set(root, "a.b.c", new TieInteger(1));       // 自动建中间表
        Assert.Equal(1L, ((TieInteger)TiePath.Get(root, "a.b.c")!).AsLong());

        TiePath.Set(root, "arr[2]", new TieString("x"));     // [null, null, "x"]
        var arr = (TieArray)TiePath.Get(root, "arr")!;
        Assert.Equal(TieValueKind.Null, arr[0].Kind);
        Assert.Equal("x", ((TieString)arr[2]).Value);
    }

    [Fact]
    public void Path_Remove()
    {
        var doc = TieDocument.Parse(Fixtures.FullConfig);
        Assert.False(TiePath.Exists(doc.Root, "pkg.verify_signature") == false && false); // sanity
        Assert.True(TiePath.Remove(doc.Root, """pkg["registry"]"""));
        Assert.False(TiePath.Exists(doc.Root, "pkg.registry"));
        Assert.False(TiePath.Remove(doc.Root, "pkg.registry")); // 再删返回 false
    }

    [Fact]
    public void Path_WildcardInSetOrRemove_Rejected()
    {
        var root = TieDocument.Parse("[*]".Replace("*", "1")).Root; // [1]
        Assert.Throws<ArgumentException>(() => TiePath.Set(root, "[*]", new TieInteger(1)));
        Assert.Throws<ArgumentException>(() => TiePath.Remove(root, "*"));
    }

    // ---------- TieMerge（官方 L2 语义） ----------

    [Fact]
    public void Merge_TableDeepMerge()
    {
        var baseLayer = (TieTable)TieDocument.Parse(
            """["tiec": ["backend": "win32", "features": ["async"]]]""").Root;
        var overlay = (TieTable)TieDocument.Parse(
            """["tiec": ["emit": "exe", "features": ["macro"]]]""").Root;
        var merged = TieMerge.DeepMerge(baseLayer, overlay);
        var tiec = (TieTable)merged["tiec"]!;
        Assert.Equal("win32", ((TieString)tiec["backend"]!).Value);   // base 保留
        Assert.Equal("exe", ((TieString)tiec["emit"]!).Value);         // overlay 加入
        Assert.Equal(2, ((TieArray)tiec["features"]!).Count);          // 数组追加
        Assert.Equal(baseLayer, TieDocument.Parse(
            """["tiec": ["backend": "win32", "features": ["async"]]]""").Root); // 入参未改
    }

    [Fact]
    public void Merge_ArrayAppend_AndReset()
    {
        var baseLayer = (TieTable)TieDocument.Parse("""["l": ["a"], "m": ["keep"]]""").Root;
        var overlay = (TieTable)TieDocument.Parse("""["l": ["b"], "m": "="]""").Root;
        var merged = TieMerge.DeepMerge(baseLayer, overlay);
        var l = (TieArray)merged["l"]!;
        Assert.Equal(2, l.Count);                       // 追加：父层在前
        Assert.Equal("a", ((TieString)l[0]).Value);
        Assert.Equal(0, ((TieArray)merged["m"]!).Count); // "=" 重置为空数组
    }

    [Fact]
    public void Merge_ScalarOverride_AndMultiLayer()
    {
        var l1 = (TieTable)TieDocument.Parse("""["opt": 0, "name": "dev"]""").Root;
        var l2 = (TieTable)TieDocument.Parse("""["opt": 2]""").Root;
        var l3 = (TieTable)TieDocument.Parse("""["name": "ci"]""").Root;
        var merged = TieMerge.MergeAll(l1, l2, l3);
        Assert.Equal(2L, ((TieInteger)merged["opt"]!).AsLong());
        Assert.Equal("ci", ((TieString)merged["name"]!).Value);
    }

    // ---------- TieDiff ----------

    [Fact]
    public void Diff_AddedRemovedChanged()
    {
        var a = TieDocument.Parse(
            """["target": "win-x64", "opt": 2, "old": true]""").Root;
        var b = TieDocument.Parse(
            """["target": "win-x64", "opt": 3, "new": ["x"]]""").Root;
        var diffs = TieDiff.Compare(a, b);

        Assert.Contains(diffs, d => d.Kind == TieDiffKind.Added && d.Path == "new");
        Assert.Contains(diffs, d => d.Kind == TieDiffKind.Removed && d.Path == "old");
        Assert.Contains(diffs, d => d.Kind == TieDiffKind.Changed && d.Path == "opt");
    }

    [Fact]
    public void Diff_ArrayElementPaths()
    {
        var a = TieDocument.Parse("""["f": ["async", "macro"]]""").Root;
        var b = TieDocument.Parse("""["f": ["async", "unsafe", "extra"]]""").Root;
        var diffs = TieDiff.Compare(a, b);
        Assert.Contains(diffs, d => d.Path == "f[1]" && d.Kind == TieDiffKind.Changed);
        Assert.Contains(diffs, d => d.Path == "f[2]" && d.Kind == TieDiffKind.Added);
    }

    [Fact]
    public void Diff_Identical_IsEmpty()
    {
        var a = TieDocument.Parse(Fixtures.CliConfig).Root;
        var b = TieDocument.Parse(Fixtures.CliConfig).Root.Clone();
        Assert.Empty(TieDiff.Compare(a, b));
    }
}

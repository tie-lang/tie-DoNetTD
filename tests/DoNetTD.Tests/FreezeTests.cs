// Copyright (c) 2026 TIE-LANG organization. All rights reserved.
// Licensed under the TIE-LANG Open Source License v1.1 (see LICENSE in the repository root).

using Xunit;
using DoNetTD;

namespace DoNetTD.Tests;

/// <summary>不可变只读视图（Freeze）。</summary>
public class FreezeTests
{
    [Fact]
    public void FrozenTable_RejectsMutations()
    {
        var table = new TieTable().SetItem("k", new TieInteger(1)).Frozen();
        Assert.True(table.IsFrozen);
        Assert.Throws<InvalidOperationException>(() => table.Set("x", new TieInteger(2)));
        Assert.Throws<InvalidOperationException>(() => table.Remove("k"));
        Assert.Throws<InvalidOperationException>(table.Clear);
        Assert.Throws<InvalidOperationException>(() => table["y"] = new TieInteger(3));
        Assert.Throws<InvalidOperationException>(() => ((TieInteger)table["k"]!).Value = 99);
        // 数据未被破坏
        Assert.Equal(1L, ((TieInteger)table["k"]!).AsLong());
        Assert.False(table.ContainsKey("x"));
    }

    [Fact]
    public void FrozenArray_RejectsMutations()
    {
        var arr = new TieArray(new TieString("a")).Frozen();
        Assert.Throws<InvalidOperationException>(() => arr.Add(new TieString("b")));
        Assert.Throws<InvalidOperationException>(() => arr.With(new TieString("c")));
        Assert.Throws<InvalidOperationException>(() => arr.Insert(0, new TieString("d")));
        Assert.Throws<InvalidOperationException>(() => arr.RemoveAt(0));
        Assert.Throws<InvalidOperationException>(arr.Clear);
        Assert.Throws<InvalidOperationException>(() => arr[0] = new TieString("z"));
        Assert.Equal("a", ((TieString)arr[0]).Value);
    }

    [Fact]
    public void FrozenScalars_RejectValueWrites()
    {
        Assert.Throws<InvalidOperationException>(() => { var b = new TieBool(true).Frozen(); b.Value = false; });
        Assert.Throws<InvalidOperationException>(() => { var t = new TieTrit(0).Frozen(); t.Value = 1; });
        Assert.Throws<InvalidOperationException>(() => { var s = new TieString("x").Frozen(); s.Value = "y"; });
        Assert.Throws<InvalidOperationException>(() => { var c = new TieChar('a').Frozen(); c.Codepoint = 'b'; });

        var i = new TieInteger(1).Frozen();
        Assert.Throws<InvalidOperationException>(() => i.Value = 2);
        Assert.Throws<InvalidOperationException>(() => i.Suffix = TieIntegerSuffix.I8);

        var f = new TieFloat(1.5).Frozen();
        Assert.Throws<InvalidOperationException>(() => f.Value = 2.5);
        Assert.Throws<InvalidOperationException>(() => f.Suffix = TieFloatSuffix.F32);

        // 未冻结的照常可写
        var free = new TieBool(true);
        free.Value = false;
        Assert.False(free.Value);
    }

    [Fact]
    public void DeepFreeze_ReachesModule()
    {
        var root = TieDocument.Parse(Fixtures.FullConfig).Root.Freeze();
        var tiec = (TieTable)((TieTable)root)["tiec"]!;
        Assert.True(tiec.IsFrozen);
        Assert.True(((TieArray)tiec["features"]!).IsFrozen);
        Assert.Throws<InvalidOperationException>(() => tiec.Set("new", new TieInteger(1)));
        Assert.Throws<InvalidOperationException>(() =>
            ((TieArray)tiec["features"]!).Add(new TieString("extra")));
    }

    [Fact]
    public void Clone_EscapesFrozen()
    {
        var frozen = new TieTable().SetItem("a", new TieInteger(1)).Frozen();
        var copy = frozen.Clone();
        Assert.False(copy.IsFrozen);
        ((TieTable)copy).Set("b", new TieInteger(2)); // 不抛
        Assert.Equal(2, ((TieTable)copy).Count);

        // 深层同样可变，且不影响冻结原树
        var deepFrozen = TieDocument.Parse("""["t": ["x": 1]]""").Root.Frozen();
        var deepCopy = (TieTable)deepFrozen.Clone();
        ((TieTable)deepCopy["t"]!).Set("y", new TieInteger(2));
        Assert.True(deepFrozen.IsFrozen);                       // 原树保持冻结
        Assert.False(((TieTable)((TieTable)deepCopy)["t"]!).IsFrozen);
        Assert.Equal(1L, ((TieInteger)((TieTable)((TieTable)deepFrozen)["t"]!)["x"]!).AsLong());
    }

    [Fact]
    public void Freeze_DoesNotAffectEqualityOrWrite()
    {
        var a = TieDocument.Parse(Fixtures.CliConfig);
        var text = a.Write();
        a.Root.Freeze();
        Assert.Equal(text, a.Write()); // 冻结后写出一致
        var b = TieDocument.Parse(Fixtures.CliConfig);
        Assert.Equal(b.Root, a.Root);
    }
}

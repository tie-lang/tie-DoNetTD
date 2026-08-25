// Copyright (c) 2026 TIE-LANG organization. All rights reserved.
// Licensed under the TIE-LANG Open Source License v1.1 (see LICENSE in the repository root).

using System.Numerics;
using Xunit;
using DoNetTD;

namespace DoNetTD.Tests;

public class ModelCreationTests
{
    [Fact]
    public void ChainedCreation_EquivalentToParsedOfficialConfig()
    {
        var root = new TieTable()
            .SetItem("advanced", new TieTable()
                .SetItem("enabled", TieBool.True)
                .SetItem("threads", new TieInteger(0)))
            .SetItem("cache", new TieTable()
                .SetItem("size", new TieInteger(268435456))
                .SetItem("storage", new TieString("memory"))
                .SetItem("path", new TieString(".tie-cache")));

        var created = TieDocument.FromValue(root);
        var parsed = TieDocument.Parse(Fixtures.CliConfig);

        // 结构相等：与插入顺序无关、与后缀无关。
        Assert.Equal(parsed.Root, created.Root);
        Assert.Equal(created.Root.GetHashCode(), parsed.Root.GetHashCode());
    }

    [Fact]
    public void Equality_IgnoresSuffix_ButNotValue()
    {
        TieValue a = new TieInteger(42, TieIntegerSuffix.I32);
        TieValue b = new TieInteger(42, TieIntegerSuffix.U8);
        TieValue c = new TieInteger(43);
        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }

    [Fact]
    public void TableEquality_OrderInsensitive_ArrayEquality_OrderSensitive()
    {
        var t1 = new TieTable().SetItem("a", new TieInteger(1)).SetItem("b", new TieInteger(2));
        var t2 = new TieTable().SetItem("b", new TieInteger(2)).SetItem("a", new TieInteger(1));
        Assert.Equal(t1, t2);

        var a1 = new TieArray(new TieInteger(1), new TieInteger(2));
        var a2 = new TieArray(new TieInteger(2), new TieInteger(1));
        Assert.NotEqual(a1, a2);
    }

    [Fact]
    public void Clone_IsIndependent()
    {
        var original = TieDocument.Parse(Fixtures.FullConfig).Root.Clone();
        ((TieTable)((TieTable)original)["tiec"]!).Remove("features");
        var fresh = (TieTable)TieDocument.Parse(Fixtures.FullConfig).Root;
        Assert.NotNull(((TieTable)fresh["tiec"]!)["features"]);
    }

    [Fact]
    public void Set_ExistingKey_Replaces_KeepsPosition()
    {
        var t = new TieTable().SetItem("a", new TieInteger(1)).SetItem("b", new TieInteger(2));
        t.Set("a", new TieInteger(99));
        Assert.Equal(new[] { "a", "b" }, t.Keys);
        Assert.Equal(99L, ((TieInteger)t["a"]!).AsLong());
    }

    [Fact]
    public void IndexerNull_Removes()
    {
        var t = new TieTable().SetItem("a", new TieInteger(1));
        t["a"] = null!;
        Assert.False(t.ContainsKey("a"));
    }

    [Fact]
    public void Trit_ConstructionValidation()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TieTrit(2));
        Assert.Equal(0, TieTrit.Zero.Value);
    }

    [Fact]
    public void BigInteger_IntegersSupported()
    {
        var big = BigInteger.Parse("170141183460469231731687303715884105727"); // i128 max
        var doc = TieDocument.FromValue(new TieInteger(big, TieIntegerSuffix.I128));
        Assert.Equal(big + "i128", doc.Root.ToString());
    }
}

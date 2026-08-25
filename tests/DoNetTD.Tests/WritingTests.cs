// Copyright (c) 2026 TIE-LANG organization. All rights reserved.
// Licensed under the TIE-LANG Open Source License v1.1 (see LICENSE in the repository root).

using Xunit;
using DoNetTD;

namespace DoNetTD.Tests;

public class WritingTests
{
    [Fact]
    public void DefaultKeyOrder_IsStrcmpByteOrder()
    {
        // "z" < "中" (E4..) < "é" (C3..)? UTF-8: "z"=0x7A, "中"=0xE4B8AD, "é"=0xC3A9
        // 字节序：0x7A < 0xC3 < 0xE4 → z, é, 中（与 UTF-16 Ordinal 不同！）
        var table = new TieTable()
            .SetItem("中", new TieInteger(1))
            .SetItem("z", new TieInteger(2))
            .SetItem("é", new TieInteger(3));
        var text = TieDocument.FromValue(table).Write();
        var zPos = text.IndexOf("\"z\"");
        var ePos = text.IndexOf("\"é\"");
        var cPos = text.IndexOf("\"中\"");
        Assert.True(zPos >= 0 && ePos > zPos && cPos > ePos, text);
    }

    [Fact]
    public void InsertionOrder_Option()
    {
        var table = new TieTable()
            .SetItem("b", new TieInteger(1))
            .SetItem("a", new TieInteger(2));
        var opts = new TieWriteOptions { KeyOrder = TableKeyOrder.InsertionOrder };
        var text = TieDocument.FromValue(table).Write(opts);
        Assert.True(text.IndexOf("\"b\"") < text.IndexOf("\"a\""));
    }

    [Fact]
    public void PrettyFormat_MatchesOfficialStyle()
    {
        var doc = TieDocument.FromValue(new TieTable().SetItem("k", new TieArray(new TieInteger(1), new TieInteger(2))));
        var text = doc.Write();
        Assert.Contains("[\n", text);
        Assert.Contains("    \"k\": [\n", text);
        Assert.EndsWith("]\n", text);
        Assert.Contains(",\n]", text); // 尾逗号默认开
    }

    [Fact]
    public void TrailingComma_CanDisable()
    {
        var doc = TieDocument.FromValue(new TieTable().SetItem("a", new TieInteger(1)));
        var text = doc.Write(new TieWriteOptions { TrailingComma = false });
        Assert.DoesNotContain(",\n]", text);
    }

    [Fact]
    public void EmitHeader_WritesRoleLine()
    {
        var doc = TieDocument.FromValue(new TieTable().SetItem("a", new TieInteger(1)), withHeader: true);
        var text = doc.Write(new TieWriteOptions { EmitHeader = true });
        Assert.StartsWith("type tie<data>\n\n", text);
    }

    [Fact]
    public void CompactArraysOfScalars()
    {
        var doc = TieDocument.FromValue(new TieTable().SetItem(
            "list", new TieArray(new TieString("a"), new TieString("b"))));
        var text = doc.Write(new TieWriteOptions { CompactArraysOfScalars = true });
        Assert.Contains("""["a", "b"]""", text);
    }

    [Fact]
    public void Floats_RoundTripSafe_Formatted()
    {
        Assert.Equal("3.14", TieFloatToString(new TieFloat(3.14)));
        Assert.Equal("5.0", TieFloatToString(new TieFloat(5.0)));   // 整数形态补 .0
        Assert.Equal("1.5f32", TieFloatToString(new TieFloat(1.5, TieFloatSuffix.F32)));
        double original = 0.1 + 0.2;
        var node = new TieFloat(original);
        var reparsed = (TieFloat)TieDocument.Parse(TieFloatToString(node)).Root;
        Assert.Equal(original, reparsed.Value); // R 最短往返保证位相等
    }

    private static string TieFloatToString(TieFloat f) => f.ToString()!;

    [Fact]
    public void Null_And_NonZeroTrit_WriteThrows()
    {
        Assert.Throws<InvalidOperationException>(() =>
            TieDocument.FromValue(TieValue.Null).Write());
        Assert.Throws<InvalidOperationException>(() =>
            TieDocument.FromValue(TieTrit.Positive).Write());
        // zero 可以写
        Assert.Equal("zero\n", TieDocument.FromValue(TieTrit.Zero).Write());
    }

    [Fact]
    public void CompactMode_SingleLine()
    {
        var doc = TieDocument.Parse(Fixtures.CliConfig);
        var compact = doc.Write(new TieWriteOptions { Pretty = false });
        Assert.DoesNotContain('\n', compact);
    }
}

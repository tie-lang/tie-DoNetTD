// Copyright (c) 2026 TIE-LANG organization. All rights reserved.
// Licensed under the TIE-LANG Open Source License v1.1 (see LICENSE in the repository root).

using Xunit;
using DoNetTD;
using DoNetTD.Convert;

namespace DoNetTD.Tests;

public class JsonConversionTests
{
    [Fact]
    public void FromJson_Types()
    {
        var v = TieJson.FromJson("""{"i": 42, "f": 1.5, "s": "x", "b": false, "z": null}""");
        var t = (TieTable)v;
        Assert.IsType<TieInteger>(t["i"]);
        Assert.Equal(42L, ((TieInteger)t["i"]!).AsLong());
        Assert.IsType<TieFloat>(t["f"]);
        Assert.IsType<TieBool>(t["b"]);
        Assert.IsType<TieNull>(t["z"]);
    }

    [Fact]
    public void ToJson_EscapesControlChars()
    {
        var s = new TieString("a\u0001b\"c\\d\ne");
        var json = TieJson.ToJson(s);
        Assert.Equal("\"a\\u0001b\\\"c\\\\d\\ne\"", json);
    }

    [Fact]
    public void ToJson_IndentedFormatting()
    {
        var table = new TieTable().SetItem("k", new TieArray(new TieInteger(1)));
        var json = TieJson.ToJson(table, indented: true);
        Assert.StartsWith("{", json);
        Assert.Contains("\n  \"k\": [", json);
        Assert.Contains("\n  ]\n}", json);
    }

    [Theory]
    [InlineData("{\"a\": }")]       // 截断值
    [InlineData("{a: 1}")]          // 键非字符串
    [InlineData("[1, 2")]           // 未闭合
    [InlineData("01x")]             // 非法数字后缀
    [InlineData("{} extra")]        // 多余内容
    public void FromJson_InvalidInput_FailsWithPosition(string json)
    {
        Assert.Throws<TieParseException>(() => TieJson.FromJson(json));
    }

    [Fact]
    public void BigNumbers_ThroughJson()
    {
        const string bigText = "123456789012345678901234567890";
        var v = TieJson.FromJson("{\"n\": " + bigText + "}");
        Assert.Equal(System.Numerics.BigInteger.Parse(bigText), ((TieInteger)((TieTable)v)["n"]!).Value);
    }

    [Fact]
    public void TritAndChar_MapLossy()
    {
        var json = TieJson.ToJson(new TieTable()
            .SetItem("zero", TieTrit.Zero)
            .SetItem("plus", TieTrit.Positive)
            .SetItem("ch", new TieChar('好')));
        Assert.Contains("\"zero\":0", json);
        Assert.Contains("\"plus\":1", json);
        Assert.Contains("\"ch\":\"好\"", json);
    }
}

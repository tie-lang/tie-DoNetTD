// Copyright (c) 2026 TIE-LANG organization. All rights reserved.
// Licensed under the TIE-LANG Open Source License v1.1 (see LICENSE in the repository root).

using Xunit;
using DoNetTD;

namespace DoNetTD.Tests;

public class ParsingTests
{
    [Fact]
    public void OfficialCliConfig_Parses()
    {
        var doc = TieDocument.Parse(Fixtures.CliConfig);
        Assert.True(doc.HasHeader);
        var root = Assert.IsType<TieTable>(doc.Root);
        var advanced = (TieTable)root["advanced"]!;
        Assert.True(((TieBool)advanced["enabled"]!).Value);
        Assert.Equal(0L, ((TieInteger)advanced["threads"]!).AsLong());
        var cache = (TieTable)root["cache"]!;
        Assert.Equal(268435456L, ((TieInteger)cache["size"]!).AsLong());
        Assert.Equal("memory", ((TieString)cache["storage"]!).Value);
    }

    [Fact]
    public void OfficialFullConfig_DeepAccess()
    {
        var doc = TieDocument.Parse(Fixtures.FullConfig);
        var root = (TieTable)doc.Root;
        var features = (TieArray)((TieTable)root["tiec"]!)["features"]!;
        Assert.Equal(new[] { "async", "macro", "unsafe" },
            features.Items.Select(i => ((TieString)i).Value));
        Assert.Equal("tests/", ((TieString)((TieArray)((TieTable)root["roles"]!)["test"]!)[0]).Value);
        Assert.Equal("https://reg.tie-lang.org", ((TieString)((TieTable)root["pkg"]!)["registry"]!).Value);
        Assert.True(((TieBool)root["debug"]!).Value);
    }

    [Fact]
    public void NarrowSuffixes_Preserved()
    {
        var doc = TieDocument.Parse("""["a": 42i32, "b": 7u8, "c": -3i16]""");
        var root = (TieTable)doc.Root;
        Assert.Equal(TieIntegerSuffix.I32, ((TieInteger)root["a"]!).Suffix);
        Assert.Equal(TieIntegerSuffix.U8, ((TieInteger)root["b"]!).Suffix);
        Assert.Equal(-3L, ((TieInteger)root["c"]!).AsLong());
    }

    [Fact]
    public void U128Max_BigInteger()
    {
        var doc = TieDocument.Parse(
            """["m": 340282366920938463463374607431768211455u128]""");
        var node = (TieInteger)((TieTable)doc.Root)["m"]!;
        Assert.Equal(TieIntegerSuffix.U128, node.Suffix);
        Assert.Equal(System.Numerics.BigInteger.Parse("340282366920938463463374607431768211455"), node.Value);
    }

    [Fact]
    public void Floats_WithSuffixAndExponent()
    {
        var doc = TieDocument.Parse("""["a": 3.14, "b": 1.5f32, "c": -2e3, "d": 2.0f64]""");
        var root = (TieTable)doc.Root;
        Assert.Equal(3.14, ((TieFloat)root["a"]!).Value);
        Assert.Equal(TieFloatSuffix.F32, ((TieFloat)root["b"]!).Suffix);
        Assert.Equal(-2000.0, ((TieFloat)root["c"]!).Value); // -2e3
    }

    [Fact]
    public void Keywords_TrueFalseZero_CharLiteral()
    {
        var doc = TieDocument.Parse("""["t": true, "f": false, "z": zero, "ch": '中', "nl": '\n']""");
        var root = (TieTable)doc.Root;
        Assert.IsType<TieBool>(root["t"]);
        Assert.IsType<TieBool>(root["f"]);
        Assert.IsType<TieTrit>(root["z"]);
        Assert.Equal('中', ((TieChar)root["ch"]!).Codepoint);
        Assert.Equal('\n', ((TieChar)root["nl"]!).AsChar());
    }

    [Fact]
    public void StringEscapes_OfficialSixPlusExtensions()
    {
        var doc = TieDocument.Parse("""["s": "引号\" 反斜\\ 换行\n 制表\t 斜杠/ \u0041 😀"]""");
        var s = ((TieString)((TieTable)doc.Root)["s"]!).Value;
        Assert.Contains("\"", s);
        Assert.Contains("\\", s);
        Assert.Contains("\n", s);
        Assert.Contains("/", s);
        Assert.Contains("A", s);
        Assert.Contains("😀", s);
    }

    [Fact]
    public void OptionalCommas_AndTrailingComma()
    {
        // 官方语义：条目间逗号可选，尾逗号容忍。
        var doc = TieDocument.Parse("""["a": 1, "b": 2 "c": 3,]""");
        Assert.Equal(3, ((TieTable)doc.Root).Count);
        var arr = TieDocument.Parse("[1 2, 3,]").Root;
        Assert.Equal(3, ((TieArray)arr).Count);
    }

    [Fact]
    public void FirstEntryDecides_TableVsArray()
    {
        Assert.IsType<TieTable>(TieDocument.Parse("""["k": 1]""").Root);
        Assert.IsType<TieArray>(TieDocument.Parse("""["just a string"]""").Root);
        Assert.IsType<TieArray>(TieDocument.Parse("[]").Root); // 空 → 数组
    }

    [Fact]
    public void DuplicateKeys_LastWins_OrStrictError()
    {
        var doc = TieDocument.Parse("""["k": 1, "k": 2]""");
        Assert.Equal(2L, ((TieInteger)((TieTable)doc.Root)["k"]!).AsLong());

        var ok = TieDocument.TryParse("""["k": 1, "k": 2]""",
            out _, out var diags, new TieParseOptions { StrictDuplicateKeys = true });
        Assert.False(ok);
        Assert.Contains(diags, d => d.Message.Contains("重复"));
    }

    [Fact]
    public void RequireHeader_Option()
    {
        var ok = TieDocument.TryParse("""["a": 1]""", out _,
            out _, new TieParseOptions { RequireHeader = true });
        Assert.False(ok);

        var ok2 = TieDocument.TryParse(Fixtures.CliConfig, out _,
            out _, new TieParseOptions { RequireHeader = true });
        Assert.True(ok2);
    }

    [Fact]
    public void WrongRole_Fails()
    {
        var ok = TieDocument.TryParse("type tie<logic>\nfunc main() {}", out _, out _);
        Assert.False(ok);
    }

    [Fact]
    public void MaxDepth_Guarded()
    {
        var deep = "[" + new string('[', 300) + "]" + new string(']', 300);
        var ok = TieDocument.TryParse(deep, out _, out _,
            new TieParseOptions { MaxDepth = 256 });
        Assert.False(ok);
    }

    [Theory]
    [InlineData("""["s": "未闭合]""", "字符串")]
    [InlineData("""["s": "非法转义\q"]""", "未知转义")]
    [InlineData("""["a": 1, "b" 2]""", "冒号")]
    [InlineData("""[1: 2]""", "意外")]
    [InlineData("""["a": ]""", "")]  // 截断值
    [InlineData("""type tie<data> ["a": 1]]""", "多余内容")]
    public void ErrorDiagnostics_HaveLineColumn(string text, string keyword)
    {
        var ok = TieDocument.TryParse(text, out _, out var diags);
        Assert.False(ok);
        var first = Assert.Single(diags);
        Assert.Equal(TieDiagnosticSeverity.Error, first.Severity);
        if (!string.IsNullOrEmpty(keyword))
        {
            Assert.Contains(keyword, first.Message);
        }
        Assert.True(first.Line >= 1);
    }

    [Fact]
    public void ScalarRoot_AllowedByDefault_Rejectable()
    {
        Assert.Equal(42L, TieDocument.Parse("42").Root is TieInteger i ? i.AsLong() : -1);

        var ok = TieDocument.TryParse("42", out _, out _,
            new TieParseOptions { AllowScalarRoot = false });
        Assert.False(ok);
    }
}

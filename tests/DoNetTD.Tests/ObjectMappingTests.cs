// Copyright (c) 2026 TIE-LANG organization. All rights reserved.
// Licensed under the TIE-LANG Open Source License v1.1 (see LICENSE in the repository root).

using Xunit;
using DoNetTD;
using DoNetTD.Convert;

namespace DoNetTD.Tests;

public class ObjectMappingTests
{
    private enum Level { Low, High }

    private sealed class Tiec
    {
        public string Backend { get; set; } = "";
        public List<string> Features { get; set; } = new();
        public int Opt { get; set; }
        public double Ratio { get; set; }
        public Level? Mode { get; set; }
    }

    private sealed class Config
    {
        public string Target { get; set; } = "";
        public bool Debug { get; set; }
        public Tiec Tiec { get; set; } = new();
        public long Opt { get; set; }
    }

    [Fact]
    public void ToObject_NestedPoco()
    {
        var doc = TieDocument.Parse(Fixtures.FullConfig);
        var cfg = doc.ToObject<Config>()!;
        Assert.Equal("win-x64", cfg.Target);
        Assert.True(cfg.Debug);
        Assert.Equal("win32", cfg.Tiec.Backend);
        Assert.Contains("macro", cfg.Tiec.Features);

        // 根上的 opt 映射到 long 属性；ratio/mode 缺失 → 默认值/空
        Assert.Equal(2L, cfg.Opt);
        Assert.Null(cfg.Tiec.Mode);
    }

    [Fact]
    public void FromObject_PocoRoundTrip()
    {
        var cfg = new Config
        {
            Target = "linux-x64",
            Debug = true,
            Opt = 1024,
            Tiec = new Tiec { Backend = "llvm", Features = new List<string> { "a" }, Opt = 2, Ratio = 0.5, Mode = Level.High },
        };
        var node = TieObjectMapper.FromObject(cfg);
        var back = TieObjectMapper.ToObject<Config>(node)!;
        Assert.Equal(cfg.Target, back.Target);
        Assert.Equal(cfg.Debug, back.Debug);
        Assert.Equal(cfg.Opt, back.Opt);
        Assert.Equal(cfg.Tiec.Backend, back.Tiec.Backend);
        Assert.Equal(2, back.Tiec.Opt);
        Assert.Equal(0.5, back.Tiec.Ratio);
        Assert.Equal(Level.High, back.Tiec.Mode);
    }

    [Fact]
    public void Enum_WrittenAsName_ReadFromEither()
    {
        var node = TieObjectMapper.FromObject(Level.High);
        Assert.Equal("High", ((TieString)node).Value);
        var parsed = (TieString)TieDocument.Parse("\"low\"").Root;
        Assert.Equal(Level.Low, TieObjectMapper.ToObject<Level>(parsed));
        var numeric = (TieInteger)TieDocument.Parse("1").Root;
        Assert.Equal(Level.High, TieObjectMapper.ToObject<Level>(numeric));
    }

    [Fact]
    public void Collections_AndDictionaries()
    {
        var arr = TieDocument.Parse("[10, 20]").Root;
        var list = TieObjectMapper.ToObject<List<int>>(arr)!;
        Assert.Equal(new[] { 10, 20 }, list);
        var arrayBack = TieObjectMapper.ToObject<int[]>(arr)!;
        Assert.Equal(2, arrayBack.Length);

        var dict = new Dictionary<string, object?> { ["a"] = 1, ["b"] = "x" };
        var tableNode = TieObjectMapper.FromObject(dict);
        Assert.Equal(2, ((TieTable)tableNode).Count);

        var asDict = TieObjectMapper.ToObject<Dictionary<string, object?>>((TieTable)TieDocument.Parse("""["k": 7]""").Root)!;
        Assert.Equal(7L, ((TieInteger)asDict["k"]!).AsLong());
    }

    [Fact]
    public void RangeErrors_Throw()
    {
        var big = TieDocument.Parse("99999999999").Root;
        Assert.ThrowsAny<Exception>(() => TieObjectMapper.ToObject<int>(big));

        var text = TieDocument.Parse("\"hi\"").Root;
        Assert.Throws<InvalidCastException>(() => TieObjectMapper.ToObject<int>(text));
    }

    [Fact]
    public void GuidDateTime_AsStrings()
    {
        var g = Guid.Parse("0b9c8a52-3f4e-4d6e-9f2a-123456789abc");
        var node = TieObjectMapper.FromObject(g);
        Assert.Equal(g, TieObjectMapper.ToObject<Guid>(node));

        var dt = new DateTime(2026, 8, 25, 12, 0, 0, DateTimeKind.Utc);
        var dtNode = TieObjectMapper.FromObject(dt);
        Assert.Equal(dt, TieObjectMapper.ToObject<DateTime>(dtNode));
    }
}

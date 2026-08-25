// Copyright (c) 2026 TIE-LANG organization. All rights reserved.
// Licensed under the TIE-LANG Open Source License v1.1 (see LICENSE in the repository root).

using Xunit;
using DoNetTD;
using DoNetTD.Advanced;
using DoNetTD.Convert;
using DoNetTD.Schema;

namespace DoNetTD.Tests;

/// <summary>v0.2 新功能：多诊断收集、环境变量插值、表达式树路径、Schema 推导。</summary>
public class V2FeaturesTests
{
    // ---------- CollectAllErrors ----------

    [Fact]
    public void CollectAllErrors_ReportsMultiple()
    {
        const string bad = """
            [
                "a": "未闭合,
                "b": 1,
                "c": 非法字面量,
                "d": 2,
            ]
            """;
        var ok = TieDocument.TryParse(bad, out _, out var diags,
            new TieParseOptions { CollectAllErrors = true });
        Assert.False(ok);
        Assert.True(diags.Count >= 2, $"期望收集多条错误，实际 {diags.Count}");
        Assert.Contains(diags, d => d.Message.Contains("字符串"));
        Assert.Contains(diags, d => d.Message.Contains("无法识别"));
    }

    [Fact]
    public void CollectAllErrors_Off_ByDefault()
    {
        const string bad = """
            [
                "a": "未闭合,
                "b": 非法,
            ]
            """;
        TieDocument.TryParse(bad, out _, out var diags);
        Assert.Single(diags); // 宽容模式只报首个
    }

    // ---------- 环境变量插值 ----------

    [Fact]
    public void Interpolate_ExpandsVars_AndDollarEscape()
    {
        var vars = new Dictionary<string, string> { ["REG"] = "https://reg.example" };
        var value = TieInterpolate.ExpandString("${REG}/pkg $$5 ${UNSET_XYZ}", vars);
        Assert.Equal("https://reg.example/pkg $5 ${UNSET_XYZ}", value);
    }

    [Fact]
    public void Interpolate_MissingBehaviors()
    {
        Assert.Equal("${NOPE}", TieInterpolate.ExpandString("${NOPE}",
            new Dictionary<string, string>(), MissingVarBehavior.Keep));
        Assert.Equal("", TieInterpolate.ExpandString("${NOPE}",
            new Dictionary<string, string>(), MissingVarBehavior.Empty));
        Assert.Throws<InvalidOperationException>(() => TieInterpolate.ExpandString(
            "${NOPE}", new Dictionary<string, string>(), MissingVarBehavior.Error));
    }

    [Fact]
    public void Interpolate_TreeExpansion_ReturnsNewTree()
    {
        var vars = new Dictionary<string, string> { ["NAME"] = "tie" };
        var original = TieDocument.Parse("""["greet": "hi ${NAME}", "n": 1]""").Root;
        var expanded = TieInterpolate.Expand(original, vars);
        Assert.Equal("hi tie", ((TieString)((TieTable)expanded)["greet"]!).Value);
        Assert.Equal("hi ${NAME}", ((TieString)((TieTable)original)["greet"]!).Value); // 原树不动
    }

    // ---------- 表达式树路径 ----------

    private sealed class Cfg
    {
        public TiecSec Tiec { get; set; } = new();
        public string Target { get; set; } = "";
    }

    private sealed class TiecSec
    {
        public System.Collections.Generic.List<string> Features { get; set; } = new();
    }

    [Fact]
    public void PathOf_ExtractsCamelCasePath()
    {
        Assert.Equal("tiec.features", TiePathOf.Of((Cfg c) => c.Tiec.Features));
        Assert.Equal("target", TiePathOf.Of((Cfg c) => c.Target));
    }

    [Fact]
    public void PathOf_GetSet_Works()
    {
        var root = TieDocument.Parse(Fixtures.FullConfig).Root;
        var first = TiePathOf.Get<Cfg>(root, c => c.Tiec.Features);
        Assert.Equal("async", ((TieString)((DoNetTD.TieArray)first!)[0]).Value);

        TiePathOf.Set<Cfg>(root, c => c.Target, new TieString("linux-x64"));
        Assert.Equal("linux-x64", ((TieString)TiePath.Get(root, "target")!).Value);
        Assert.True(TiePathOf.Remove<Cfg>(root, c => c.Target));
        Assert.Null(TiePath.Get(root, "target"));
    }

    // ---------- Schema 推导 ----------

    [Fact]
    public void Infer_SingleSample_AllFieldsRequired()
    {
        var sample = TieDocument.Parse("""
            ["name": "x", "opt": 2, "tags": ["a"]]
            """).Root;
        var schema = TieSchemaInference.InferFrom(sample);

        // 通过：结构一致的文档
        Assert.Empty(TieSchemaValidator.Validate(sample.Clone(), schema));

        // 缺字段报错（全必需策略）
        var missing = TieDocument.Parse("""["name": "x"]""").Root;
        var errors = TieSchemaValidator.Validate(missing, schema);
        Assert.Contains(errors, e => e.Message.Contains("缺少必需字段") && e.Message.Contains("opt"));

        // 类型不符报错
        var wrongType = TieDocument.Parse("""["name": "x", "opt": "high", "tags": []]""").Root;
        Assert.NotEmpty(TieSchemaValidator.Validate(wrongType, schema));
    }

    [Fact]
    public void Infer_MultiSamples_UnionsKeys_OptionalWhenPartial()
    {
        var s1 = TieDocument.Parse("""["a": 1, "b": "x"]""").Root;
        var s2 = TieDocument.Parse("""["a": 2]""").Root; // b 缺失 → 可选；a 两样例都是整数 → Integer
        var schema = TieSchemaInference.InferFrom(new[] { s1, s2 });

        var probe = TieDocument.Parse("""["a": 3]""").Root;
        Assert.Empty(TieSchemaValidator.Validate(probe, schema)); // b 可选

        var badA = TieDocument.Parse("""["a": "s"]""").Root;
        Assert.NotEmpty(TieSchemaValidator.Validate(badA, schema)); // a 必须整数
    }
}

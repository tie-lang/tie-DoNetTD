// Copyright (c) 2026 TIE-LANG organization. All rights reserved.
// Licensed under the TIE-LANG Open Source License v1.1 (see LICENSE in the repository root).

using BenchmarkDotNet.Attributes;
using DoNetTD;
using DoNetTD.Advanced;
using DoNetTD.Convert;

namespace DoNetTD.Benchmarks;

/// <summary>
/// 性能基准。本地运行：dotnet run -c Release --project benchmarks/DoNetTD.Benchmarks
/// （BenchmarkDotNet 基准不在 CI 中执行——耗时长且对环境敏感。）
/// </summary>
[MemoryDiagnoser]
public class ParseWriteBenchmarks
{
    private const string ConfigText = """
        type tie<data>
        [
            "target": "win-x64",
            "opt": 2,
            "debug": true,
            "tiec": [
                "backend": "win32",
                "features": ["async", "macro", "unsafe"],
                "emit": "exe",
                "link": ["user32", "gdi32"],
                "bounds_check": true,
            ],
            "pkg": [
                "registry": "https://reg.tie-lang.org",
                "cache_dir": ".tie/deps",
                "verify_signature": true,
            ],
            "roles": [
                "test": ["tests/"],
                "bench": ["bench/"],
            ],
        ]
        """;

    private TieDocument _doc = null!;

    [GlobalSetup]
    public void Setup() => _doc = TieDocument.Parse(ConfigText);

    [Benchmark]
    public TieDocument Parse() => TieDocument.Parse(ConfigText);

    [Benchmark]
    public string Write() => _doc.Write();

    [Benchmark]
    public string WriteCompact() => _doc.Write(new TieWriteOptions { Pretty = false });
}

[MemoryDiagnoser]
public class ConvertBenchmarks
{
    private readonly TieDocument _doc =
        TieDocument.Parse("""["a": 1, "b": ["c": ["d": "x", "e": true], "f": [1, 2, 3]]]""");

    [Benchmark]
    public string ToJson() => TieJson.ToJson(_doc.Root);

    [Benchmark]
    public TieValue FromJson() => TieJson.FromJson(
        """{"a": 1, "b": {"c": {"d": "x", "e": true}, "f": [1, 2, 3]}}""");

    [Benchmark]
    public object ToObject() => TieObjectMapper.ToObject<Poco>(_doc.Root);

    [Benchmark]
    public TieValue FromObject() => TieObjectMapper.FromObject(new Poco { A = 1, B = new Poco { A = 2 } });

    public sealed class Poco
    {
        public long A { get; set; }
        public Poco? B { get; set; }
        public string? D { get; set; }
        public bool E { get; set; }
    }
}

[MemoryDiagnoser]
public class AdvancedBenchmarks
{
    private readonly TieDocument _doc = TieDocument.Parse("""
        ["items": [["id": 0, "v": 10], ["id": 1, "v": 20], ["id": 2, "v": 30]]]
        """);
    private readonly TieTable _base = (TieTable)TieDocument.Parse("""["a": ["x": 1], "l": [1]]""").Root;
    private readonly TieTable _overlay = (TieTable)TieDocument.Parse("""["a": ["y": 2], "l": [2]]""").Root;

    [Benchmark]
    public TieValue? PathGet() => TiePath.Get(_doc.Root, """items[?(@.v>15)].id""");

    [Benchmark]
    public IReadOnlyList<TieDiffEntry> Diff() => TieDiff.Compare(_base, _overlay);

    [Benchmark]
    public TieTable Merge() => TieMerge.DeepMerge(_base, _overlay);
}

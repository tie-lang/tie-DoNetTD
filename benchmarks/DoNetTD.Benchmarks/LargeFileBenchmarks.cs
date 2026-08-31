// Copyright (c) 2026 TIE-LANG organization. All rights reserved.
// Licensed under the TIE-LANG Open Source License v1.1 (see LICENSE in the repository root).

using BenchmarkDotNet.Attributes;
using DoNetTD;

namespace DoNetTD.Benchmarks;

/// <summary>
/// 大文件基准：行式表与嵌套配置形态的解析，以及行式表写出基参考。
/// 本地运行：dotnet run -c Release --project benchmarks/DoNetTD.Benchmarks -- --large [--size 10]
/// </summary>
[MemoryDiagnoser]
public class LargeFileBenchmarks
{
    [Params(10, 50, 100)]
    public int SizeMB { get; set; }

    private string _rowText = null!;
    private string _nestedText = null!;
    private TieDocument _doc = null!;

    [GlobalSetup]
    public void Setup()
    {
        int target = SizeMB * 1024 * 1024;
        _rowText = TdCorpus.GenerateRowTable(target);
        _nestedText = TdCorpus.GenerateNested(target);
        // 预热并保留一份写出来作为 Write 基参考。
        _doc = TieDocument.Parse(_rowText);
    }

    [Benchmark]
    public TieDocument ParseRowTable() => TieDocument.Parse(_rowText);

    [Benchmark]
    public TieDocument ParseNested() => TieDocument.Parse(_nestedText);

    [Benchmark]
    public string WriteRowTable() => _doc.Write();
}
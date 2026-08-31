// Copyright (c) 2026 TIE-LANG organization. All rights reserved.
// Licensed under the TIE-LANG Open Source License v1.1 (see LICENSE in the repository root).

using System.Diagnostics;
using System.Globalization;
using System.Text;
using DoNetTD;

namespace DoNetTD.Benchmarks;

/// <summary>
/// 峰值内存探针：BenchmarkDotNet 之外的正则 Console 运行，
/// 单次贴合真实场景，测量解析大文档的耗时与内存峰值增量。
/// 结果以简单 ASCII 表格打印。
/// </summary>
public static class PeakMemoryProbe
{
    public static void Run(int sizeMB)
    {
        int target = sizeMB * 1024 * 1024;
        string rowText = TdCorpus.GenerateRowTable(target);
        string nestedText = TdCorpus.GenerateNested(target);

        Console.WriteLine($"== Peak Memory Probe (SizeMB = {sizeMB}) ==");
        Console.WriteLine($"   row     text bytes     = {rowText.Length,12}  ({Mb(rowText.Length),8:F1} MB)");
        Console.WriteLine($"   nested  text bytes     = {nestedText.Length,12}  ({Mb(nestedText.Length),8:F1} MB)");
        Console.WriteLine();

        ProbeOne("RowTable", rowText);
        ProbeOne("Nested ", nestedText);
    }

    private static void ProbeOne(string label, string text)
    {
        // 清基线：先强制回收让 GC.GetTotalMemory/WorkingSet 尽量干净。
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long beforeWorking = Environment.WorkingSet;
        long beforeTotal = GC.GetTotalMemory(false);

        var sw = Stopwatch.StartNew();
        var doc = TieDocument.Parse(text);
        sw.Stop();

        long afterWorking = Environment.WorkingSet;
        long afterTotal = GC.GetTotalMemory(false);
        long allocated = GC.GetTotalAllocatedBytes(true);

        Console.WriteLine($"--- {label} ---");
        Console.WriteLine($"   input bytes      = {text.Length,12}  ({Mb(text.Length),8:F1} MB)");
        Console.WriteLine($"   parse time       = {sw.ElapsedMilliseconds,8} ms");
        Console.WriteLine($"   allocated        = {Mb(allocated),10:F1} MB");
        Console.WriteLine($"   working-set delta= {Mb(afterWorking - beforeWorking),8:F1} MB");
        Console.WriteLine($"   GC total   delta = {Mb(afterTotal - beforeTotal),8:F1} MB");
        Console.WriteLine();
    }

    private static double Mb(long bytes) => bytes / (1024.0 * 1024.0);
}
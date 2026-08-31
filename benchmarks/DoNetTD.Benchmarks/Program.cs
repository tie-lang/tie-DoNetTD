// Copyright (c) 2026 TIE-LANG organization. All rights reserved.
// Licensed under the TIE-LANG Open Source License v1.1 (see LICENSE in the repository root).

using BenchmarkDotNet.Running;
using DoNetTD.Benchmarks;

// 命令行参数（顶层解析，不依赖第三方 CLI 库）：
//   --large       只运行大文件基准（LargeFileBenchmarks），随后运行峰值内存探针
//   --probe       只运行峰值内存探针（不跑 BenchmarkDotNet）
//   --size <MB>   探针目标大小，默认 10（可写 --size=10 或 --size 10）
// 无参数时保持原有三套基准行为不变。
int sizeMB = 10;
bool large = false;
bool probeOnly = false;
for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--large":
            large = true;
            break;
        case "--probe":
            probeOnly = true;
            break;
        case "--size":
            if (i + 1 < args.Length && int.TryParse(args[i + 1], out int s))
            {
                sizeMB = s;
                i++;
            }
            else
            {
                Console.Error.WriteLine("--size 需要一个整数参数，例如 --size 10");
                return 2;
            }
            break;
        default:
            const string prefix = "--size=";
            if (args[i].StartsWith(prefix, StringComparison.Ordinal) &&
                int.TryParse(args[i].Substring(prefix.Length), out int sv))
            {
                sizeMB = sv;
            }
            else
            {
                Console.Error.WriteLine($"未知参数: {args[i]}");
                return 2;
            }
            break;
    }
}

if (probeOnly)
{
    PeakMemoryProbe.Run(sizeMB);
    return 0;
}

if (large)
{
    BenchmarkRunner.Run<LargeFileBenchmarks>();
    PeakMemoryProbe.Run(sizeMB);
    return 0;
}

BenchmarkRunner.Run<ParseWriteBenchmarks>();
BenchmarkRunner.Run<ConvertBenchmarks>();
BenchmarkRunner.Run<AdvancedBenchmarks>();
return 0;

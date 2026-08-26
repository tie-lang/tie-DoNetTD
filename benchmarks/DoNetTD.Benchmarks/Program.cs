// Copyright (c) 2026 TIE-LANG organization. All rights reserved.
// Licensed under the TIE-LANG Open Source License v1.1 (see LICENSE in the repository root).

using BenchmarkDotNet.Running;
using DoNetTD.Benchmarks;

BenchmarkRunner.Run<ParseWriteBenchmarks>();
BenchmarkRunner.Run<ConvertBenchmarks>();
BenchmarkRunner.Run<AdvancedBenchmarks>();

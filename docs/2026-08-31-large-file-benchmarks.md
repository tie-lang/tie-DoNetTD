# 大文件解析基准套件（Phase 1）基线报告

> 对应仓库 `benchmarks/DoNetTD.Benchmarks/LargeFileBenchmarks.cs` + `Corpus/TdCorpus.cs` + `PeakMemoryProbe.cs`。
> 首轮基线，仅记录当前解析/写出性能与峰值内存；后续 Phase 优化后在此对比。

## 方法

- **机器**：12th Gen Intel Core i5-12490F，Windows 11 专业版 Insider Preview，12 逻辑核。
- **运行时**：.NET 8.0.29，X64 RyuJIT AVX2，GC=Concurrent Workstation（BenchmarkDotNet 自动切「高性能」电源计划）。
- **文档形态**（`TdCorpus`，固定种子、内容纯 ASCII、字节可预测、同 size 输出逐字节一致）：
  - **行式表（RowTable）**：模拟数据包/玩家数据，外层表含 `rows` 数组，每行 `["id","name","active","score"]`，键密集重复、id 递增、布尔交替。
  - **嵌套（Nested）**：模拟配置类，顶层含多个组、深浅嵌套（sub 表 + 小数组），键重复少、字符串/整数/布尔混合。
- **规模**：10 / 50 / 100 MB（生成目标字节 = SizeMB × 1024 × 1024，实际输出略超）。
- **运行方式**：
  - 全量 BenchmarkDotNet（9 个用例）：`dotnet run -c Release --project benchmarks/DoNetTD.Benchmarks -- --large --size 10`
  - 仅在需快速单发探针时：`--probe --size 10`（本机全量 BDN 约 20 分钟）。
- 探针流程：`GC.Collect()` 清基线 → 采样 `GC.GetTotalMemory`/`Environment.WorkingSet` → `TieDocument.Parse` → 复采样，输出分配总量与峰值增量。

## 首轮基线数字

### BenchmarkDotNet（`[MemoryDiagnoser]`）

| 方法 | SizeMB | Mean | StdDev | Allocated |
|---|---:|---:|---:|---:|
| ParseRowTable | 10    | 364.2 ms | 21.15 ms | 229.49 MB |
| ParseNested   | 10    | 312.6 ms | 15.60 ms | 195.31 MB |
| WriteRowTable | 10    | 104.7 ms |  4.41 ms | 220.33 MB |
| ParseRowTable | 50    | 1741.5 ms | 81.00 ms | 1137.58 MB |
| ParseNested   | 50    | 1394.0 ms | 26.33 ms |  968.64 MB |
| WriteRowTable | 50    |  502.3 ms | 30.40 ms | 1092.69 MB |
| ParseRowTable | 100   | 3337.9 ms | 95.28 ms | 2262.28 MB |
| ParseNested   | 100   | 2791.1 ms | 55.81 ms | 1935.66 MB |
| WriteRowTable | 100   | 1069.9 ms | 77.57 ms | 2176.41 MB |

结论（首轮）：解析耗时随规模近似线性（~33 ms/MB 行式、~28 ms/MB 嵌套）；分配量显著高于输入规模（10MB 输入 ≈ 23 倍，100MB 输入 ≈ 22 倍）。写出行式表基参考 ~10.7 ms/MB。

### 峰值内存探针（`--probe --size 10`）

| 形态  | 输入字节 / MB | 解析耗时 ms | 分配总量 MB | 工作集增量 MB | GC Total 增量 MB |
|---|---|---:|---:|---:|---:|
| RowTable | 10,485,804 / 10.0 | 536 | 368.6 | 108.0 | 110.7 |
| Nested   | 10,485,848 / 10.0 | 362 | 563.9 | −31.9* | 88.0 |

> *工作集增量为负：同进程内两形态顺序执行，RowTable 已先把进程工作集撑起，故 Nested 段相对基准呈现回落。单发参考取 RowTable 首段（108.0 MB）。

<p>

## 优化后对比

（留空，等待后续 Phase 填写。）

<p>

## 复现命令

```bash
# 全量 BDN 大文件基准（含探针），~20 分钟
dotnet run -c Release --project benchmarks/DoNetTD.Benchmarks -- --large --size 10
# 仅单发峰值探针，秒级
dotnet run -c Release --project benchmarks/DoNetTD.Benchmarks -- --probe --size 10
```
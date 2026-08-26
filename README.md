# tie-DoNetTD

<p align="center">
  <strong>tie:data 的 .NET 解析库</strong><br>
  解析 · 写入 · 创建 · 转化 · 路径查询 · 深合并 · 校验 · 注释保留 · 命令行工具
</p>

[![ci](https://github.com/TIE-LANG/tie-DoNetTD/actions/workflows/ci.yml/badge.svg)](https://github.com/TIE-LANG/tie-DoNetTD/actions/workflows/ci.yml)

**DoNetTD** 让 .NET 应用直接读写 [tie 语言](https://github.com/TIE-LANG) 的 `type tie<data>` 数据交换格式——
与 tiec 编译器消费的配置文件完全兼容，并完整支持语言级字面量（窄整数后缀、trit、char）。

| 包 | 用途 |
|---|---|
| `DoNetTD` | 核心库（零第三方依赖），netstandard2.0 + net8.0 |
| `DoNetTD.Extensions.Configuration` | `IConfiguration` 配置源桥接（支持 optional / reloadOnChange） |
| `TdcTool` | `tdc` dotnet 命令行工具（格式化/校验/互转/合并/路径存取） |

## 安装

```bash
dotnet add package DoNetTD
```

## 格式速览

```tie
type tie<data>
[
    "target": "win-x64",
    "opt": 2,                    // 整数（可带窄后缀 42i32 / 7u8）
    "debug": true,               // 布尔；三值 trit 用 zero
    "tiec": [
        "features": ["async", "macro"],
        "ratio": 1.5f32,         // 浮点后缀 f32/f64，默认 f64
    ],
]
```

要点：表与数组都用 `[...]`，靠首个条目区分（`"key": value` 即表）；`//` 行注释；
逗号可选、容忍尾逗号。空 `[]` 是空数组。

## 快速开始

### 解析

```csharp
using DoNetTD;

var doc = TieDocument.ParseFile("tie.config");
var table = (TieTable)doc.Root;
Console.WriteLine(table["target"]);            // "win-x64"

// 非抛出式 + 中文诊断（行列号）
if (!TieDocument.TryParse(text, out var doc2, out var diags))
    Console.WriteLine(diags[0]);               // 第 3 行，第 12 列: 字符串未闭合
```

### 创建

```csharp
var root = new TieTable()
    .SetItem("target", new TieString("win-x64"))
    .SetItem("opt", new TieInteger(2))
    .SetItem("features", new TieArray(new TieString("async"), new TieString("macro")));
var doc = TieDocument.FromValue(root);
```

### 写入

```csharp
doc.WriteToFile("tie.config");                 // 官方风格：4 空格缩进 + 尾逗号
File.WriteAllText("p.tie", doc.Write());       // 表键默认按 strcmp 字节序排序（tie map 语义）
doc.Write(new TieWriteOptions {
    KeyOrder = TableKeyOrder.InsertionOrder,   // 或保留插入序
    Pretty = false,                            // 紧凑单行
    EmitHeader = true,                         // 输出 type tie<data> 头
});
```

### 转化

```csharp
// JSON 双向（内置手写转换器，无 System.Text.Json 依赖）
string json = TieJson.ToJson(doc.Root, indented: true);
TieValue back = TieJson.FromJson(json);

// POCO 映射：表↔属性、数组↔List/数组、枚举按名、Guid/DateTime↔字符串
class Config { public string Target { get; set; } = ""; public int Opt { get; set; } }
var cfg = doc.ToObject<Config>()!;             // 文档级扩展方法
TieValue node = TieObjectMapper.FromObject(cfg);
```

## 高级功能

### 路径存取（TiePath）

```csharp
using DoNetTD.Advanced;

TiePath.Get(root, "tiec.features[0]");          // 取值
TiePath.GetAll(root, "roles[\"test\"][*]");     // 通配枚举
TiePath.Set(root, "cache.size", new TieInteger(1024)); // 缺键自动建容器、索引自动扩容
TiePath.Remove(root, "pkg.registry");
TiePath.Exists(root, "debug");
```

### 官方 L2 分层深合并（TieMerge）

与 tiec 构建配置的合并语义逐条一致：

```csharp
// 表∩表 → 递归深合并；数组∩数组 → 追加（父层在前）
// overlay 值为 "=" 且 base 是数组 → 重置为空数组；其余 → overlay 覆盖
var merged = TieMerge.DeepMerge(builtinDefaults, userConfig);
var final = TieMerge.MergeAll(builtin, user, profileLayer, cliOverrides);
```

### 差异比较（TieDiff）

```csharp
foreach (var d in TieDiff.Compare(oldDoc.Root, newDoc.Root))
    Console.WriteLine($"{d.Kind}: {d.Path}");   // Changed: opt / Added: cache.size / Removed: old
```

### Schema 校验

```csharp
using DoNetTD.Schema;

var schema = TieSchema.Object(b => b
    .Required("target", TieSchema.String())
    .Required("opt", TieSchema.Integer().Min(0).Max(3))
    .Optional("features", TieSchema.ArrayOf(TieSchema.String())));

IReadOnlyList<TieDiagnostic> errors = TieSchemaValidator.Validate(doc.Root, schema);
```

### JsonNode 互转桥（仅 net8.0）

```csharp
using DoNetTD.Bridge;

System.Text.Json.Nodes.JsonNode? node = TieJsonNodeBridge.ToJsonNode(tieValue);
TieValue back = TieJsonNodeBridge.FromJsonNode(node);
```

## v0.2 新功能

### 注释保留往返

```csharp
var doc = TieDocument.ParseFile("tie.config");
doc.Root["opt"]!.TrailingComment;          // 解析时自动挂载（前导 LeadingComments 同理）
doc.WriteToFile("tie.config", new TieWriteOptions { PreserveComments = true });
// 编辑配置不丢注释——JSON 库做不到的事
```

### 环境变量插值

```csharp
using DoNetTD.Convert;

// "registry": "${REG}/pkg" → 展开；$$ 为字面 $；未命中可 Keep/Empty/Error
var expanded = TieInterpolate.Expand(doc.Root);
```

### 强类型路径（表达式树）

```csharp
using DoNetTD.Advanced;

TiePath.Get(root, TiePathOf.Of((Config c) => c.Tiec.Features)); // "tiec.features"，重构安全
TiePathOf.Set(root, (Config c) => c.Target, new TieString("linux-x64"));
```

### Schema 推导

```csharp
using DoNetTD.Schema;

var schema = TieSchemaInference.InferFrom(sampleDoc.Root);   // 样例即契约
var errors = TieSchemaValidator.Validate(otherDoc.Root, schema);
```

### 多诊断收集

```csharp
TieDocument.TryParse(text, out _, out var all,
    new TieParseOptions { CollectAllErrors = true }); // 一次拿到全部错误（上限 100）
```

### tdc 命令行

```bash
dotnet tool install -g TdcTool
tdc fmt tie.config -w --preserve-comments   # 格式化并保注释写回
tdc check *.data.tie                        # 批量校验（收集全部诊断）
tdc to-json app.data.tie -i                 # 转 JSON
tdc from-json pkg.json --header             # 转 tie:data
tdc merge defaults.tie profile.tie -o out   # 官方 L2 分层合并
tdc get app.tie cache.size                  # 路径取值
tdc set app.tie opt 3 -w                    # 路径写入
```

### 接入 .NET 通用配置

```csharp
// DoNetTD.Extensions.Configuration
builder.Configuration.AddTieFile("tie.config", optional: true, reloadOnChange: true);
Console.WriteLine(config["tiec:backend"]);   // 展平约定与 JSON 提供程序一致
```

## 数据模型

| 节点 | tie:data 形态 | 说明 |
|---|---|---|
| `TieTable` | `["k": v, ...]` | 插入序存储 + strcmp 序枚举 |
| `TieArray` | `[v1, v2]` | 支持 LINQ/foreach |
| `TieInteger` | `42i32` / `7u8` | BigInteger 支撑 i128/u128；后缀保留往返 |
| `TieFloat` | `1.5f32` / `3.14` | R 最短往返格式化 |
| `TieBool` / `TieTrit` | `true` / `zero` | trit ±1 仅可编程构造（写出抛异常） |
| `TieChar` | `'a'` | Unicode 码点 |
| `TieString` | `"..."` | 官方六种转义 + `\0\b\f\uXXXX` 扩展 |
| `TieNull` | —（无语法形态） | 供 JSON 互转；写出抛异常 |

所有节点实现结构相等（表按键集合比较、数组按序比较、整数不分后缀）与 `Clone()` 深拷贝。

## 与官方实现的兼容性

- ✅ 完整兼容 tiec `config.tie` 解析器接受的一切文档（六种转义、可选逗号、首条目区分表/数组）
- ➕ 语言级超集：窄整数/浮点后缀、指数形式、`zero`、char 字面量（官方 config 子集不含）
- 📌 键序遵循 tie map 官方语义：strcmp（UTF-8 字节序），非 .NET Ordinal

## v0.3 新功能

### 不可变只读视图（Freeze）

```csharp
var frozen = doc.Root.Frozen();          // 深冻结整棵子树
frozen["opt"] = 3;                       // 抛 InvalidOperationException
frozen.Clone();                          // Clone 出的副本总是可变——修改冻结树的正规途径
```

### Diff → Patch 回放（配置漂移修复闭环）

```csharp
using DoNetTD.Advanced;

var patch = TiePatch.ToPatch(TieDiff.Compare(baseline, actual)); // 补丁文档本身是合法 tie:data
TiePatch.ApplyTo(baseline, patch);       // == actual（结构相等）
```

### TiePath 过滤器与切片

```csharp
TiePath.Get(root, "items[-1]");              // 负索引（从尾计数）
TiePath.GetAll(root, "mixed[1..3]");         // 区间切片，左闭右开；[..2] [2..] [..] 均可
TiePath.GetAll(root, """items[?(@.opt>1)]""");      // 数值比较 > >= < <= == !=
TiePath.GetAll(root, """servers[?(@.on==true)]"""); // 布尔与字符串字面量
```

### 性能与质量

- BenchmarkDotNet 基准套件（benchmarks/，本地 `dotnet run -c Release --project benchmarks/DoNetTD.Benchmarks`）
- Fuzz 测试：随机文档往返幂等 + 破坏输入零崩溃（已借此修复一处 EOF 边界越界）
- CI 覆盖率门禁：行覆盖 ≥80%，不达标即红

## 构建与测试

```bash
dotnet build -c Release
dotnet test -c Release      # 114 个测试
dotnet run --project examples/DoNetTD.Demo
dotnet pack -c Release      # DoNetTD / Extensions.Configuration / TdcTool
```

## License

本仓库按 **TIE-LANG Open Source License v1.1** 授权发布（全文见 [LICENSE](LICENSE)）：
你可自由使用、修改并分发本软件源码，包括用于商业产品，仅需保留版权声明并附本许可证；
用该库开发的自有软件完全归你所有，不附带任何署名义务。

This repository is released under the **TIE-LANG Open Source License v1.1**.

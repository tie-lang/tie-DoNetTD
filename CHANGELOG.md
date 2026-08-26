# Changelog

所有显著变更记录于此。格式参考 Keep a Changelog；版本遵循语义化版本。

## [0.3.0] - 2026-08-26

### 新增
- **不可变只读视图**：`TieValue.Frozen()` / `.Frozen<T>()` 深冻结整棵子树，全部可变点（含标量属性）守卫抛异常；`Clone()` 出的副本总是可变
- **Diff→Patch 回放**：`TiePatch.ToPatch/Apply/ApplyTo`——补丁文档本身是合法 tie:data，配置漂移修复闭环
- **TiePath 增强**：负索引 `[-1]`、区间切片 `[1..3]`/`[..2]`/`[2..]`（左闭右开）、过滤器 `[?(@.k>1)]`（数值比较 / 字符串序比较 / 布尔相等）
- **质量四件套**：
  - Fuzz 套件：随机文档往返幂等 ×400 + 破坏输入零崩溃 ×1300（借此修复容器循环 EOF 边界越界）
  - CI 行覆盖率门禁 ≥80%（coverlet.msbuild）
  - 主库 `TreatWarningsAsErrors`
  - BenchmarkDotNet 基准套件（解析/写入/JSON/POCO/Path/Diff/Merge）

### 变更
- F32 后缀的 `TieFloat` 构造时规范化到 float 精度——写出/相等以字面量真实精度为准

## [0.2.0] - 2026-08-26

### 新增
- **注释保留往返**：解析挂载前导/尾随注释，`PreserveComments` 写出还原——编辑 tie.config 不丢注释
- **多诊断收集**：`CollectAllErrors` 条目级恢复重同步，一次报全部错误（上限可调）
- **环境变量插值**：`TieInterpolate.Expand`（`${VAR}` / `$$` 转义 / Keep|Empty|Error 三种缺失策略）
- **强类型路径**：`TiePathOf.Of((Config c) => c.Tiec.Opt)` 表达式树提取路径，重构安全
- **Schema 推导**：`TieSchemaInference.InferFrom` 单/多样例反向生成校验规则
- **tdc dotnet tool**：fmt / check / to-json / from-json / merge / get / set 七命令
- **DoNetTD.Extensions.Configuration 包**：`AddTieFile` 把 tie:data 接入 IConfiguration（optional/reloadOnChange）

## [0.1.0] - 2026-08-26

### 首个版本
- 解析：递归下降，兼容官方 tiec config.tie 全部行为 + 语言级超集（窄整数后缀 i8..u128、指数形式、trit `zero`、char 字面量）；中文行列号诊断
- 写入：表键 strcmp UTF-8 字节序排序（tie map 官方语义）、4 空格缩进、尾逗号、R 最短往返浮点
- 创建：链式 Builder API、结构相等（表按键集/数组按序/整数不分后缀）、深拷贝
- 转化：手写 JSON 双向转换、POCO 映射（零第三方依赖）、JsonNode 互转桥（net8.0）
- 高级：TiePath 路径存取、TieMerge 官方 L2 分层深合并、TieDiff 差异比较、Schema 校验器
- 多目标 netstandard2.0 + net8.0

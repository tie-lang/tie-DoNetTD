// Copyright (c) 2026 TIE-LANG organization. All rights reserved.
// Licensed under the TIE-LANG Open Source License v1.1 (see LICENSE in the repository root).

using DoNetTD;
using DoNetTD.Advanced;

// ============================================================
// DoNetTD 快速演示：解析 → 取值 → 改值 → 路径操作 → 写回
// ============================================================

const string sample = """
    type tie<data>
    [
        "target": "win-x64",
        "opt": 2,
        "debug": true,
        "tiec": [
            "backend": "win32",
            "features": ["async", "macro", "unsafe"],
        ],
    ]
    """;

// 1. 解析（带行列号诊断的中文报错）
var doc = TieDocument.Parse(sample);
Console.WriteLine("== 解析官方样例 ==");
Console.WriteLine($"HasHeader={doc.HasHeader}, Root 是 {doc.Root.Kind}");

// 2. 取值：链式索引或路径表达式
var backend = (TieString)((TieTable)((TieTable)doc.Root)["tiec"]!)["backend"]!;
Console.WriteLine($"backend = {backend.Value}");
Console.WriteLine($"路径取值 features[0] = {TiePath.Get(doc.Root, "tiec.features[0]")}");

// 3. 改值 + 路径写入（缺键自动建容器）
((TieTable)doc.Root)["opt"] = new TieInteger(3);
TiePath.Set(doc.Root, "cache.size", new TieInteger(268435456));

// 4. 写回（默认表键 strcmp 字节序排序、4 空格缩进、尾逗号——tie 官方风格）
Console.WriteLine("== 写回 ==");
Console.WriteLine(doc.Write());

// 5. JSON 互转与 POCO 映射
var json = DoNetTD.Convert.TieJson.ToJson(doc.Root);
Console.WriteLine("== JSON ==");
Console.WriteLine(json);

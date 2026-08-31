// Copyright (c) 2026 TIE-LANG organization. All rights reserved.
// Licensed under the TIE-LANG Open Source License v1.1 (see LICENSE in the repository root).

using System.Text;

namespace DoNetTD.Benchmarks;

/// <summary>
/// 大文件基准的合成文档生成器。
/// 全部内容为 ASCII（子节数 == 字节数），固定种子，相同 size 的输出逐字节一致，可在多次运行间验证。
/// </summary>
public static class TdCorpus
{
    /// <summary>
    /// 行式表形态：模拟数据包 / 玩家数据。外层表含 "rows" 数组，
    /// 每个元素是一行表（["id","name","active","score"]），键密集重复、
    /// id 递增、name 格式化、布尔交替、浮点由固定种子随机。
    /// 生成到略超出 targetBytes 为止。
    /// </summary>
    public static string GenerateRowTable(int targetBytes)
    {
        var sb = new StringBuilder(targetBytes + 256);
        sb.Append("[\n");
        sb.Append("    \"rows\": [\n");
        var rng = new Random(12345);
        long id = 1;
        int bytes = 0;
        while (bytes < targetBytes)
        {
            bool active = (id & 1) == 0;            // 交替布尔
            int whole = rng.Next(0, 10000);         // score 整数部分
            int frac = rng.Next(0, 100);            // score 小数部分(固定两位)
            var row = $"        [\"id\": {id}, \"name\": \"player_{id}\", \"active\": {active.ToString().ToLowerInvariant()}, \"score\": {whole}.{frac:D2}],\n";
            bytes += row.Length;
            sb.Append(row);
            id++;
        }
        sb.Append("    ],\n");
        sb.Append("]\n");
        return sb.ToString();
    }

    /// <summary>
    /// 嵌套混合形态：模拟配置类。顶层含多个组，键重复少、
    /// 深浅嵌套（sub 表、小数组），字符串/整数/布尔混合。
    /// 两种组形态交替出现以增加多样性。生成到略超出 targetBytes 为止。
    /// </summary>
    public static string GenerateNested(int targetBytes)
    {
        var sb = new StringBuilder(targetBytes + 512);
        sb.Append("[\n");
        var rng = new Random(67890);
        int group = 0;
        int bytes = 0;
        while (bytes < targetBytes)
        {
            int a = rng.Next(1, 100000);
            int ports0 = rng.Next(1, 65535);
            int ports1 = rng.Next(1, 65535);
            bool on = (group & 1) == 0;
            string block;
            if (on)
            {
                block = $"""
                        "service_{group}": [
                            "desc": "gateway-{group}",
                            "instance": {a},
                            "enabled": true,
                            "runtime": [
                                "threads": {(a % 64) + 1},
                                "heap_mb": 4096,
                                "sinks": ["console", "file", "syslog"],
                            ],
                            "ports": [{ports0}, {ports1}],
                            "tag": "stable",
                        ],

                        """;
            }
            else
            {
                block = $"""
                        "service_{group}": [
                            "desc": "worker-{group}",
                            "delay_ms": {a % 500},
                            "active": false,
                            "limit": [
                                "max_conn": 1024,
                                "timeout_ms": 30000,
                                "tls_only": true,
                            ],
                            "labels": ["zone-a", "zone-b"],
                            "prio": {(a % 9) + 1},
                        ],

                        """;
            }
            bytes += block.Length;
            sb.Append(block);
            group++;
        }
        sb.Append("]\n");
        return sb.ToString();
    }
}
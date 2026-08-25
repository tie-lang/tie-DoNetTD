// Copyright (c) 2026 TIE-LANG organization. All rights reserved.
// Licensed under the TIE-LANG Open Source License v1.1 (see LICENSE in the repository root).

using Microsoft.Extensions.Configuration;

namespace DoNetTD.Extensions.Configuration;

/// <summary>
/// tie:data 文件配置源。等价于 AddJsonFile 的用法体验：
/// <c>builder.AddTieFile("tie.config", optional: true, reloadOnChange: true)</c>。
/// </summary>
public sealed class TieConfigurationSource : FileConfigurationSource
{
    /// <inheritdoc />
    public override IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        EnsureDefaults(builder);
        return new TieConfigurationProvider(this);
    }
}

/// <summary>tie:data 文件配置提供程序（继承官方 FileConfigurationProvider，自带 watch/optional 支持）。</summary>
public sealed class TieConfigurationProvider : FileConfigurationProvider
{
    /// <summary>用给定配置源构造。</summary>
    public TieConfigurationProvider(TieConfigurationSource source) : base(source) { }

    /// <summary>从流读取 tie:data 文本并展平为键值对。</summary>
    public override void Load(Stream stream)
    {
        var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        var text = reader.ReadToEnd();
        var doc = TieDocument.Parse(text);
        Flatten(doc.Root, string.Empty, data);
        Data = data;
    }

    /// <summary>
    /// 展平约定与 JSON 提供程序一致：层级用 ":" 连接，数组按下标编号
    /// （"tiec:features:0"）。标量转字符串：bool→true/false，数字不变文化十进制。
    /// </summary>
    private static void Flatten(TieValue node, string prefix, IDictionary<string, string> sink)
    {
        switch (node.Kind)
        {
            case TieValueKind.Table:
                foreach (var kv in ((TieTable)node).Items)
                {
                    var key = prefix.Length == 0 ? kv.Key : prefix + ConfigurationPath.KeyDelimiter + kv.Key;
                    Flatten(kv.Value, key, sink);
                }
                break;
            case TieValueKind.Array:
                var arr = (TieArray)node;
                for (int i = 0; i < arr.Count; i++)
                {
                    Flatten(arr[i], $"{prefix}{ConfigurationPath.KeyDelimiter}{i}", sink);
                }
                break;
            case TieValueKind.Null:
                break;
            default:
                sink[prefix] = ScalarToText(node);
                break;
        }
    }

    private static string ScalarToText(TieValue v) => v.Kind switch
    {
        TieValueKind.Bool => ((TieBool)v).Value ? "true" : "false",
        TieValueKind.String => ((TieString)v).Value,
        TieValueKind.Char => ((TieChar)v).AsString(),
        _ => v.ToString() ?? string.Empty, // Integer/Float/Trit 的字面量文本无引号
    };
}

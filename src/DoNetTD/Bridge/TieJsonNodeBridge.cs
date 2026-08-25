// Copyright (c) 2026 TIE-LANG organization. All rights reserved.
// Licensed under the TIE-LANG Open Source License v1.1 (see LICENSE in the repository root).

#if NET8_0_OR_GREATER

using System.Text.Json.Nodes;

namespace DoNetTD.Bridge;

/// <summary>
/// tie:data 与 System.Text.Json 的 <see cref="JsonNode"/> 互转桥（仅 net8.0+ 目标提供）。
/// 映射约定与 <see cref="Convert.TieJson"/> 一致：
/// JsonObject ↔ 表、JsonArray ↔ 数组、数字按整数/小数形态分流、null ↔ TieNull、
/// trit ↔ 数字、char ↔ 字符串。
/// </summary>
public static class TieJsonNodeBridge
{
    /// <summary>tie:data 节点 → JsonNode（新实例，独立可变）。</summary>
    public static JsonNode? ToJsonNode(TieValue value)
    {
        if (value is null) throw new ArgumentNullException(nameof(value));
        switch (value.Kind)
        {
            case TieValueKind.Null:
                return null;
            case TieValueKind.Bool:
                return JsonValue.Create(((TieBool)value).Value);
            case TieValueKind.Trit:
                return JsonValue.Create(((TieTrit)value).Value);
            case TieValueKind.Char:
                return JsonValue.Create(((TieChar)value).AsString());
            case TieValueKind.String:
                return JsonValue.Create(((TieString)value).Value);
            case TieValueKind.Integer:
                return JsonValue.Create(((TieInteger)value).AsLong());
            case TieValueKind.Float:
                return JsonValue.Create(((TieFloat)value).Value);
            case TieValueKind.Array:
                var array = new JsonArray();
                foreach (var item in ((TieArray)value).Items)
                {
                    array.Add(ToJsonNode(item));
                }
                return array;
            case TieValueKind.Table:
                var obj = new JsonObject();
                foreach (var kv in ((TieTable)value).Items)
                {
                    obj[kv.Key] = ToJsonNode(kv.Value);
                }
                return obj;
            default:
                throw new InvalidOperationException("未知节点种类: " + value.Kind);
        }
    }

    /// <summary>JsonNode → tie:data 节点。整数形态的数字映射为无后缀 TieInteger。</summary>
    public static TieValue FromJsonNode(JsonNode? node)
    {
        if (node is null) return TieNull.Instance;
        if (node is JsonValue v)
        {
            if (v.TryGetValue<bool>(out var b)) return new TieBool(b);
            if (v.TryGetValue<long>(out var l)) return new TieInteger(l);
            if (v.TryGetValue<double>(out var d)) return new TieFloat(d);
            if (v.TryGetValue<string>(out var s)) return s is null ? TieNull.Instance : new TieString(s);
            throw new NotSupportedException("无法识别的 JsonValue 基元类型");
        }
        if (node is JsonArray array)
        {
            var result = new TieArray();
            foreach (var item in array)
            {
                result.Add(FromJsonNode(item));
            }
            return result;
        }
        if (node is JsonObject obj)
        {
            var table = new TieTable();
            foreach (var kv in obj)
            {
                table.Set(kv.Key, FromJsonNode(kv.Value));
            }
            return table;
        }
        throw new NotSupportedException("无法识别的 JsonNode 类型");
    }
}

#endif // NET8_0_OR_GREATER

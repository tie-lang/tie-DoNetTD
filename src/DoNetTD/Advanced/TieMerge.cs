// Copyright (c) 2026 TIE-LANG organization. All rights reserved.
// Licensed under the TIE-LANG Open Source License v1.1 (see LICENSE in the repository root).

namespace DoNetTD.Advanced;

/// <summary>
/// 官方 L2 分层深合并语义（与 tiec 构建配置的合并规则一致）：
/// - 同键双方都是表 → 递归深合并；
/// - 同键双方都是数组 → 追加合并（父层在前，本层在后）；
/// - overlay 中该键值为字符串 "=" 且 base 是数组 → 重置为空数组；
/// - 其余（标量 / 类型不匹配）→ overlay 覆盖。
/// 不修改入参，全部返回新节点。
/// </summary>
public static class TieMerge
{
    /// <summary>重置标记字符串：overlay 数组键的值设为 "=" 时清空 base 数组。</summary>
    public const string ResetMarker = "=";

    /// <summary>把 overlay 深合并进 base，返回新表（不修改任何入参）。</summary>
    public static TieTable DeepMerge(TieTable baseLayer, TieTable overlayLayer)
    {
        if (baseLayer is null) throw new ArgumentNullException(nameof(baseLayer));
        if (overlayLayer is null) throw new ArgumentNullException(nameof(overlayLayer));

        var result = (TieTable)baseLayer.Clone();
        foreach (var kv in overlayLayer.Items)
        {
            bool hasBase = result.TryGet(kv.Key, out var baseValue);
            if (!hasBase || baseValue is null)
            {
                result.Set(kv.Key, kv.Value.Clone());
                continue;
            }

            // "=" 重置语义：仅对数组生效（官方 §5.2：layer 值为 "=" 时重置为空列表）
            if (baseValue.Kind == TieValueKind.Array &&
                kv.Value.Kind == TieValueKind.String &&
                ((TieString)kv.Value).Value == ResetMarker)
            {
                result.Set(kv.Key, new TieArray());
                continue;
            }

            if (baseValue.Kind == TieValueKind.Table && kv.Value.Kind == TieValueKind.Table)
            {
                result.Set(kv.Key, DeepMerge((TieTable)baseValue, (TieTable)kv.Value));
                continue;
            }

            if (baseValue.Kind == TieValueKind.Array && kv.Value.Kind == TieValueKind.Array)
            {
                var merged = new TieArray();
                foreach (var item in ((TieArray)baseValue).Items)
                {
                    merged.Add(item.Clone());
                }
                foreach (var item in ((TieArray)kv.Value).Items)
                {
                    merged.Add(item.Clone());
                }
                result.Set(kv.Key, merged);
                continue;
            }

            result.Set(kv.Key, kv.Value.Clone());
        }
        return result;
    }

    /// <summary>按顺序依次合并多层（后者优先），等价于逐层 DeepMerge。</summary>
    public static TieTable MergeAll(params TieTable[] layers)
    {
        if (layers is null || layers.Length == 0)
        {
            throw new ArgumentException("至少需要一层", nameof(layers));
        }
        var acc = (TieTable)layers[0].Clone();
        for (int i = 1; i < layers.Length; i++)
        {
            acc = DeepMerge(acc, layers[i]);
        }
        return acc;
    }
}

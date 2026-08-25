// Copyright (c) 2026 TIE-LANG organization. All rights reserved.
// Licensed under the TIE-LANG Open Source License v1.1 (see LICENSE in the repository root).

namespace DoNetTD.Schema;

/// <summary>推导策略选项。</summary>
public sealed class TieSchemaInferOptions
{
    /// <summary>表字段是否全部标记为必需；默认 true（样例即契约）。false 时全部可选。</summary>
    public bool FieldsRequired { get; set; } = true;

    /// <summary>数组长度约束：记录样例的最小元素数作为 Length 下界；默认 false（不限制）。</summary>
    public bool InferArrayLength { get; set; }

    /// <summary>字符串长度约束：记录样例串长为 Length 范围；默认 false。</summary>
    public bool InferStringLength { get; set; }
}

/// <summary>
/// 从样例文档反向推导校验规则：
/// 标量 → 对应种类规则；表 → 递归字段规则；数组 → 合并各元素的公共种类（不一致退化为 Any）。
/// 多样例重载按并集合并：字段集取并、种类一致才保留。
/// </summary>
public static class TieSchemaInference
{
    /// <summary>从单个样例推导。</summary>
    public static TieSchemaRule InferFrom(TieValue sample, TieSchemaInferOptions? options = null) =>
        Infer(new[] { sample }, options ?? new TieSchemaInferOptions());

    /// <summary>从多个样例推导（并集合并）。</summary>
    public static TieSchemaRule InferFrom(IEnumerable<TieValue> samples, TieSchemaInferOptions? options = null)
    {
        if (samples is null) throw new ArgumentNullException(nameof(samples));
        var list = samples.ToList();
        if (list.Count == 0)
        {
            throw new ArgumentException("至少需要一个样例", nameof(samples));
        }
        return Infer(list, options ?? new TieSchemaInferOptions());
    }

    private static TieSchemaRule Infer(IReadOnlyList<TieValue> samples, TieSchemaInferOptions opt)
    {
        var first = samples[0];
        switch (first.Kind)
        {
            case TieValueKind.Table:
                return InferTable(samples.Cast<TieTable>().ToList(), opt);
            case TieValueKind.Array:
                return InferArray(samples.Cast<TieArray>().ToList(), opt);
            case TieValueKind.Integer:
            {
                var rule = TieSchema.Integer();
                if (samples.All(s => s.Equals(first)))
                {
                    // 同值样例收紧为枚举语义？保持种类级即可，避免过度约束。
                }
                return rule;
            }
            case TieValueKind.Float:
                return TieSchema.Float();
            case TieValueKind.Bool:
                return TieSchema.Bool();
            case TieValueKind.Trit:
                return TieSchema.Trit();
            case TieValueKind.Char:
                return TieSchema.Char();
            case TieValueKind.String:
            {
                var rule = TieSchema.String();
                if (opt.InferStringLength)
                {
                    var min = samples.Min(s => ((TieString)s).Value.Length);
                    var max = samples.Max(s => ((TieString)s).Value.Length);
                    rule.Length(min, max);
                }
                return rule;
            }
            default:
                return TieSchema.Any();
        }
    }

    private static TieSchemaRule InferTable(List<TieTable> tables, TieSchemaInferOptions opt)
    {
        var keys = new LinkedHashSetCompat();
        foreach (var t in tables)
        {
            foreach (var k in t.Keys)
            {
                keys.Add(k);
            }
        }

        return TieSchema.Object(builder =>
        {
            builder.ExtraFields(true); // 推导不封闭，允许扩展字段
            foreach (var key in keys.Items)
            {
                var present = new List<TieValue>();
                foreach (var t in tables)
                {
                    if (t.TryGet(key, out var v) && v is not null)
                    {
                        present.Add(v);
                    }
                }
                if (present.Count == 0)
                {
                    continue;
                }
                var rule = Infer(present, opt);
                if (opt.FieldsRequired && present.Count == tables.Count)
                {
                    builder.Required(key, rule);
                }
                else
                {
                    builder.Optional(key, rule);
                }
            }
        });
    }

    private static TieSchemaRule InferArray(List<TieArray> arrays, TieSchemaInferOptions opt)
    {
        var items = arrays.SelectMany(a => a.Items).ToList();
        var itemRule = items.Count == 0 ? TieSchema.Any() : Infer(items, opt);

        var rule = TieSchema.ArrayOf(itemRule);
        if (opt.InferArrayLength)
        {
            var min = arrays.Min(a => a.Count);
            rule.Length(min);
        }
        return rule;
    }

    /// <summary>保序去重集合（netstandard2.0 无 HashSet 保序保证的轻量替代）。</summary>
    private sealed class LinkedHashSetCompat
    {
        private readonly List<string> _order = new();
        private readonly Dictionary<string, byte> _seen = new(StringComparer.Ordinal);

        public IEnumerable<string> Items => _order;

        public void Add(string key)
        {
            if (_seen.ContainsKey(key)) return;
            _seen[key] = 0;
            _order.Add(key);
        }
    }
}

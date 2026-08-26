// Copyright (c) 2026 TIE-LANG organization. All rights reserved.
// Licensed under the TIE-LANG Open Source License v1.1 (see LICENSE in the repository root).

namespace DoNetTD.Advanced;

/// <summary>
/// 差异补丁：把 <see cref="TieDiff"/> 的结果序列化为 tie:data 补丁文档并回放应用。
/// 补丁文档本身就是合法的 tie:data——数组内每条是一个操作表：
///
/// <code>
/// [
///     ["op": "added",   "path": "cache.size", "value": 1024],
///     ["op": "removed", "path": "old"],
///     ["op": "changed", "path": "opt", "value": 3],
/// ]
/// </code>
///
/// 典型闭环：Compare(基准, 实际) → ToPatch → 传输/存储 → ApplyTo(基准副本) == 实际。
/// </summary>
public static class TiePatch
{
    /// <summary>操作种类键值（补丁文档中的 "op" 字段取值）。</summary>
    public const string OpAdded = "added";
    public const string OpRemoved = "removed";
    public const string OpChanged = "changed";

    /// <summary>把差异列表编码为 tie:data 补丁文档（新节点）。</summary>
    public static TieArray ToPatch(IReadOnlyList<TieDiffEntry> diffs)
    {
        if (diffs is null) throw new ArgumentNullException(nameof(diffs));
        var patch = new TieArray();
        foreach (var d in diffs)
        {
            var entry = new TieTable()
                .SetItem("op", new TieString(d.Kind switch
                {
                    TieDiffKind.Added => OpAdded,
                    TieDiffKind.Removed => OpRemoved,
                    _ => OpChanged,
                }))
                .SetItem("path", new TieString(d.Path));
            if (d.Kind != TieDiffKind.Removed && d.NewValue is not null)
            {
                entry.Set("value", d.NewValue.Clone());
            }
            patch.Add(entry);
        }
        return patch;
    }

    /// <summary>把补丁应用到目标树（原地修改）。默认宽容：路径无命中的 removed 静默跳过。</summary>
    public static void Apply(TieValue target, TieValue patchDoc, bool throwOnMismatch = false)
    {
        if (target is null) throw new ArgumentNullException(nameof(target));
        if (patchDoc is null) throw new ArgumentNullException(nameof(patchDoc));

        foreach (var entry in EnumerateEntries(patchDoc))
        {
            var table = (TieTable)entry;
            var op = RequireString(table, "op");
            var path = RequireString(table, "path");
            switch (op)
            {
                case OpAdded:
                case OpChanged:
                    if (!table.TryGet("value", out var v) || v is null)
                    {
                        throw new InvalidOperationException($"补丁条目 {op} 缺少 \"value\"（路径 {path}）");
                    }
                    TiePath.Set(target, path, v);
                    break;
                case OpRemoved:
                    if (!TiePath.Remove(target, path) && throwOnMismatch)
                    {
                        throw new InvalidOperationException($"补丁 removed 无命中（路径 {path}）");
                    }
                    break;
                default:
                    throw new InvalidOperationException($"未知补丁操作 \"{op}\"（允许 added/removed/changed）");
            }
        }
    }

    /// <summary>克隆源树后应用补丁，返回新树（不修改 source）。</summary>
    public static TieValue ApplyTo(TieValue source, TieValue patchDoc, bool throwOnMismatch = false)
    {
        var copy = source.Clone();
        Apply(copy, patchDoc, throwOnMismatch);
        return copy;
    }

    private static IEnumerable<TieValue> EnumerateEntries(TieValue patchDoc)
    {
        if (patchDoc.Kind != TieValueKind.Array)
        {
            throw new InvalidOperationException("补丁文档根必须是数组");
        }
        foreach (var entry in ((TieArray)patchDoc).Items)
        {
            if (entry.Kind != TieValueKind.Table)
            {
                throw new InvalidOperationException("补丁条目必须是表");
            }
            yield return entry;
        }
    }

    private static string RequireString(TieTable t, string key)
    {
        if (!t.TryGet(key, out var v) || v is not TieString s)
        {
            throw new InvalidOperationException($"补丁条目缺少字符串字段 \"{key}\"");
        }
        return s.Value;
    }
}

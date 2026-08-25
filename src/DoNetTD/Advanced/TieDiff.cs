// Copyright (c) 2026 TIE-LANG organization. All rights reserved.
// Licensed under the TIE-LANG Open Source License v1.1 (see LICENSE in the repository root).

namespace DoNetTD.Advanced;

/// <summary>差异种类。</summary>
public enum TieDiffKind
{
    /// <summary>b 有而 a 没有。</summary>
    Added,

    /// <summary>a 有而 b 没有。</summary>
    Removed,

    /// <summary>两边都有但不相等（含容器类型不同）。</summary>
    Changed,
}

/// <summary>
/// 一条差异记录：<see cref="Path"/> 为规范路径文本（点分键 + [n] + ["key"]），
/// 新旧值是原节点引用（调用方不得修改）。
/// </summary>
public sealed record TieDiffEntry(string Path, TieDiffKind Kind, TieValue? OldValue, TieValue? NewValue);

/// <summary>
/// 两棵 tie:data 树的差异比较。表按键集合对比；数组按下标逐项对比，
/// 多出/缺失的下标记为 Added/Removed。整数比较不分后缀（结构相等语义）。
/// </summary>
public static class TieDiff
{
    /// <summary>比较 a → b 的全部差异，路径序输出。</summary>
    public static IReadOnlyList<TieDiffEntry> Compare(TieValue a, TieValue b)
    {
        if (a is null) throw new ArgumentNullException(nameof(a));
        if (b is null) throw new ArgumentNullException(nameof(b));
        var entries = new List<TieDiffEntry>();
        Walk(a, b, string.Empty, entries);
        return entries;
    }

    private static void Walk(TieValue a, TieValue b, string path, List<TieDiffEntry> sink)
    {
        if (a.Equals(b))
        {
            return;
        }
        if (a.Kind == TieValueKind.Table && b.Kind == TieValueKind.Table)
        {
            WalkTables((TieTable)a, (TieTable)b, path, sink);
            return;
        }
        if (a.Kind == TieValueKind.Array && b.Kind == TieValueKind.Array)
        {
            WalkArrays((TieArray)a, (TieArray)b, path, sink);
            return;
        }
        sink.Add(new TieDiffEntry(path.Length == 0 ? "$" : path, TieDiffKind.Changed, a, b));
    }

    private static void WalkTables(TieTable a, TieTable b, string path, List<TieDiffEntry> sink)
    {
        // 移除与变更（沿 a 的插入序）
        foreach (var kv in a.Items)
        {
            var child = JoinChild(path, kv.Key);
            if (b.TryGet(kv.Key, out var bv))
            {
                Walk(kv.Value, bv!, child, sink);
            }
            else
            {
                sink.Add(new TieDiffEntry(child, TieDiffKind.Removed, kv.Value, null));
            }
        }
        // 新增（沿 b 的插入序）
        foreach (var kv in b.Items)
        {
            if (!a.ContainsKey(kv.Key))
            {
                sink.Add(new TieDiffEntry(JoinChild(path, kv.Key), TieDiffKind.Added, null, kv.Value));
            }
        }
    }

    private static void WalkArrays(TieArray a, TieArray b, string path, List<TieDiffEntry> sink)
    {
        int min = Math.Min(a.Count, b.Count);
        for (int i = 0; i < min; i++)
        {
            Walk(a[i], b[i], $"{path}[{i}]", sink);
        }
        for (int i = min; i < a.Count; i++)
        {
            sink.Add(new TieDiffEntry($"{path}[{i}]", TieDiffKind.Removed, a[i], null));
        }
        for (int i = min; i < b.Count; i++)
        {
            sink.Add(new TieDiffEntry($"{path}[{i}]", TieDiffKind.Added, null, b[i]));
        }
    }

    private static string JoinChild(string parent, string key)
    {
        var seg = TiePath.FormatKeySegment(key);
        return parent.Length == 0 ? seg
            : seg.StartsWith("[") ? parent + seg
            : parent + "." + seg;
    }
}

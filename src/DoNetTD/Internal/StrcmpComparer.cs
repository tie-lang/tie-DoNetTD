// Copyright (c) 2026 TIE-LANG organization. All rights reserved.
// Licensed under the TIE-LANG Open Source License v1.1 (see LICENSE in the repository root).

namespace DoNetTD;

/// <summary>
/// tie strcmp 字节序比较器：把字符串按 UTF-8 字节序列做字典序比较。
/// 这是 tie map 的官方键序。注意它与 .NET string.Ordinal 不同：
/// 增补平面字符（代理对，UTF-16 首单元 D800-DFFF）在 Ordinal 下排在
/// U+E000..U+FFFF 的 BMP 字符之前，而 UTF-8 字节序按码点排列。
/// </summary>
public sealed class StrcmpComparer : IComparer<string>
{
    /// <summary>共享实例。</summary>
    public static StrcmpComparer Instance { get; } = new StrcmpComparer();

    private StrcmpComparer() { }

    /// <summary>按 UTF-8 字节序比较两字符串。</summary>
    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return -1;
        if (y is null) return 1;

        // 快路径：ASCII 段内逐字符比（UTF-8 字节序与码点序在 ASCII 段一致）。
        int min = Math.Min(x.Length, y.Length);
        for (int i = 0; i < min; i++)
        {
            char cx = x[i], cy = y[i];
            if (cx != cy)
            {
                if (cx < 0x80 && cy < 0x80) return cx.CompareTo(cy);
                return CompareUtf8(x, y); // 非 ASCII 差异 → 字节级比较
            }
        }
        // 无字符差异：一方是另一方的前缀。UTF-8 是前缀码，字节序下短者在前。
        if (x.Length == y.Length) return 0;
        return x.Length.CompareTo(y.Length);
    }

    private static int CompareUtf8(string x, string y)
    {
        var bx = System.Text.Encoding.UTF8.GetBytes(x);
        var by = System.Text.Encoding.UTF8.GetBytes(y);
        int n = Math.Min(bx.Length, by.Length);
        for (int i = 0; i < n; i++)
        {
            if (bx[i] != by[i]) return bx[i].CompareTo(by[i]);
        }
        return bx.Length.CompareTo(by.Length);
    }
}

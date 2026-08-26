// Copyright (c) 2026 TIE-LANG organization. All rights reserved.
// Licensed under the TIE-LANG Open Source License v1.1 (see LICENSE in the repository root).

using System.Text;

namespace DoNetTD.Advanced;

/// <summary>
/// 路径表达式存取：点分键 + [n] 数字索引 + ["key"] 引号键 + 通配（* 或 [*]）。
/// 示例："tiec.features[0]"、"cache.size"、'roles["test"][*]'。
/// 通配仅用于读取（<see cref="GetAll"/> 枚举全部命中）；写入/删除路径不允许通配。
/// </summary>
public static class TiePath
{
    // ---------- 读取 ----------

    /// <summary>按路径取值：返回首个命中；任一段不存在返回 null。</summary>
    public static TieValue? Get(TieValue root, string path)
    {
        var all = GetAll(root, path);
        return all.Count > 0 ? all[0] : null;
    }

    /// <summary>按路径取全部命中（通配展开）。空通配结果为空列表而非 null。</summary>
    public static IReadOnlyList<TieValue> GetAll(TieValue root, string path)
    {
        if (root is null) throw new ArgumentNullException(nameof(root));
        if (string.IsNullOrEmpty(path)) throw new ArgumentException("路径不能为空", nameof(path));

        var segments = ParsePath(path);
        var current = new List<TieValue> { root };
        foreach (var seg in segments)
        {
            var next = new List<TieValue>();
            foreach (var node in current)
            {
                Collect(node, seg, next);
            }
            if (next.Count == 0)
            {
                return Array.Empty<TieValue>();
            }
            current = next;
        }
        return current;
    }

    /// <summary>路径是否存在命中。</summary>
    public static bool Exists(TieValue root, string path) => Get(root, path) is not null;

    private static void Collect(TieValue node, PathSegment seg, List<TieValue> sink)
    {
        switch (seg.Type)
        {
            case SegmentKind.Key:
                if (node.Kind == TieValueKind.Table &&
                    ((TieTable)node).TryGet(seg.Key!, out var found) && found is not null)
                {
                    sink.Add(found);
                }
                break;
            case SegmentKind.Index:
            {
                if (node.Kind != TieValueKind.Array)
                {
                    break;
                }
                var arr = (TieArray)node;
                int idx = seg.Index!.Value;
                if (idx < 0)
                {
                    idx += arr.Count; // 负索引从尾部计数
                }
                if (idx >= 0 && idx < arr.Count)
                {
                    sink.Add(arr[idx]);
                }
                break;
            }
            case SegmentKind.Range:
            {
                if (node.Kind != TieValueKind.Array)
                {
                    break;
                }
                var arr = (TieArray)node;
                int from = Math.Max(0, seg.RangeFrom ?? 0);
                int to = Math.Min(arr.Count, seg.RangeTo ?? arr.Count); // 左闭右开
                for (int i = from; i < to; i++)
                {
                    sink.Add(arr[i]);
                }
                break;
            }
            case SegmentKind.Filter:
            {
                IEnumerable<TieValue> candidates = node.Kind switch
                {
                    TieValueKind.Array => ((TieArray)node).Items,
                    TieValueKind.Table => ((TieTable)node).Items.Select(kv => kv.Value),
                    _ => System.Linq.Enumerable.Empty<TieValue>(),
                };
                foreach (var item in candidates)
                {
                    if (item.Kind == TieValueKind.Table && MatchesFilter((TieTable)item, seg))
                    {
                        sink.Add(item);
                    }
                }
                break;
            }
            case SegmentKind.Wildcard:
                if (node.Kind == TieValueKind.Array)
                {
                    foreach (var item in ((TieArray)node).Items)
                    {
                        sink.Add(item);
                    }
                }
                else if (node.Kind == TieValueKind.Table)
                {
                    foreach (var kv in ((TieTable)node).Items)
                    {
                        sink.Add(kv.Value);
                    }
                }
                break;
        }
    }

    /// <summary>过滤器谓词求值：@.key 与字面量比较；非表元素/缺键不命中。</summary>
    private static bool MatchesFilter(TieTable element, PathSegment seg)
    {
        if (!element.TryGet(seg.Key!, out var v) || v is null)
        {
            return false;
        }

        if (seg.LiteralBool.HasValue)
        {
            if (v.Kind != TieValueKind.Bool) return false;
            var equal = ((TieBool)v).Value == seg.LiteralBool.Value;
            return seg.Op switch
            {
                FilterOp.Eq => equal,
                FilterOp.Ne => !equal,
                _ => false, // 布尔不支持大小比较
            };
        }

        int cmp;
        if (seg.Literal is string litText)
        {
            if (v.Kind != TieValueKind.String) return false;
            cmp = string.CompareOrdinal(((TieString)v).Value, litText);
        }
        else
        {
            double left;
            switch (v.Kind)
            {
                case TieValueKind.Integer: left = (double)((TieInteger)v).Value; break;
                case TieValueKind.Float: left = ((TieFloat)v).Value; break;
                default: return false;
            }
            cmp = left.CompareTo(seg.LiteralNumber!.Value);
        }
        return seg.Op switch
        {
            FilterOp.Gt => cmp > 0,
            FilterOp.Gte => cmp >= 0,
            FilterOp.Lt => cmp < 0,
            FilterOp.Lte => cmp <= 0,
            FilterOp.Eq => cmp == 0,
            FilterOp.Ne => cmp != 0,
            _ => false,
        };
    }

    // ---------- 写入 ----------

    /// <summary>
    /// 按路径写入。中途缺失的键自动创建表（下一段是索引时创建数组）；
    /// 数字索引越界时以 null 填充扩容（等于长度则追加）。
    /// 路径含通配抛 ArgumentException。
    /// </summary>
    public static void Set(TieValue root, string path, TieValue value)
    {
        if (root is null) throw new ArgumentNullException(nameof(root));
        if (value is null) throw new ArgumentNullException(nameof(value));
        var segments = ParsePath(path);
        if (segments.Any(s => s.Type is SegmentKind.Wildcard or SegmentKind.Range or SegmentKind.Filter))
        {
            throw new ArgumentException("写入路径不允许包含通配符、区间或过滤器", nameof(path));
        }

        var node = root;
        for (int i = 0; i < segments.Count - 1; i++)
        {
            // 中间段的容器类型由「下一段」决定：键→表，索引→数组。
            node = StepOrCreate(node, segments[i], segments[i + 1].Type == SegmentKind.Index);
        }
        ApplyLast(node, segments[segments.Count - 1], value, remove: false, out _);
    }

    /// <summary>按路径删除；删除成功返回 true，路径不存在返回 false。</summary>
    public static bool Remove(TieValue root, string path)
    {
        if (root is null) throw new ArgumentNullException(nameof(root));
        var segments = ParsePath(path);
        if (segments.Any(s => s.Type is SegmentKind.Wildcard or SegmentKind.Range or SegmentKind.Filter))
        {
            throw new ArgumentException("删除路径不允许包含通配符、区间或过滤器", nameof(path));
        }

        var node = root;
        for (int i = 0; i < segments.Count - 1; i++)
        {
            var next = StepOrNull(node, segments[i]);
            if (next is null) return false;
            node = next;
        }
        ApplyLast(node, segments[segments.Count - 1], value: TieNull.Instance, remove: true, out var removed);
        return removed;
    }

    private static TieValue StepOrCreate(TieValue node, PathSegment seg, bool nextIsIndex)
    {
        var existing = StepOrNull(node, seg);
        if (existing is not null) return existing;

        switch (seg.Type)
        {
            case SegmentKind.Key:
                if (node.Kind != TieValueKind.Table)
                {
                    throw new InvalidOperationException($"路径段 \"{seg.Key!}\" 需要表节点，实际为 {node.Kind}");
                }
                TieValue created = nextIsIndex ? new TieArray() : (TieValue)new TieTable();
                ((TieTable)node).Set(seg.Key!, created);
                return created;
            case SegmentKind.Index:
                if (node.Kind != TieValueKind.Array)
                {
                    throw new InvalidOperationException($"路径段 [{seg.Index}] 需要数组节点，实际为 {node.Kind}");
                }
                var arr = (TieArray)node;
                while (arr.Count < seg.Index!)
                {
                    arr.Add(TieNull.Instance);
                }
                if (arr.Count == seg.Index)
                {
                    arr.Add(nextIsIndex ? new TieArray() : new TieTable()); // 追加位置给下一段用
                }
                return arr[seg.Index!.Value];
            default:
                throw new InvalidOperationException("通配段不能作为中间步骤");
        }
    }

    private static TieValue? StepOrNull(TieValue node, PathSegment seg)
    {
        switch (seg.Type)
        {
            case SegmentKind.Key:
                if (node.Kind == TieValueKind.Table && ((TieTable)node).TryGet(seg.Key!, out var v))
                {
                    return v;
                }
                return null;
            case SegmentKind.Index:
                if (node.Kind == TieValueKind.Array)
                {
                    var arr = (TieArray)node;
                    if (seg.Index! >= 0 && seg.Index < arr.Count)
                    {
                        return arr[seg.Index.Value];
                    }
                }
                return null;
            default:
                throw new InvalidOperationException("通配段不能用于写入/删除导航");
        }
    }

    private static void ApplyLast(TieValue parent, PathSegment last, TieValue value, bool remove, out bool success)
    {
        success = false;
        switch (last.Type)
        {
            case SegmentKind.Key:
                if (parent.Kind != TieValueKind.Table) return;
                var table = (TieTable)parent;
                if (remove)
                {
                    success = table.Remove(last.Key!);
                }
                else
                {
                    table.Set(last.Key!, value);
                    success = true;
                }
                return;
            case SegmentKind.Index:
                if (parent.Kind != TieValueKind.Array) return;
                var arr = (TieArray)parent;
                int idx = last.Index!.Value;
                if (remove)
                {
                    if (idx >= 0 && idx < arr.Count)
                    {
                        arr.RemoveAt(idx);
                        success = true;
                    }
                    return;
                }
                while (arr.Count < idx)
                {
                    arr.Add(TieNull.Instance);
                }
                if (idx == arr.Count)
                {
                    arr.Add(value);
                }
                else
                {
                    arr[idx] = value;
                }
                success = true;
                return;
            default:
                return;
        }
    }

    // ---------- 路径解析 ----------

    internal enum SegmentKind { Key, Index, Wildcard, Range, Filter }

    /// <summary>过滤器比较运算符。</summary>
    internal enum FilterOp { Gt, Gte, Lt, Lte, Eq, Ne }

    internal readonly struct PathSegment
    {
        public SegmentKind Type { get; init; }
        public string? Key { get; init; }
        public int? Index { get; init; }
        public int? RangeFrom { get; init; }
        public int? RangeTo { get; init; }
        public FilterOp Op { get; init; }

        /// <summary>过滤字面量：字符串形态时非 null（含 true/false 关键字的字符串语义）。</summary>
        public string? Literal { get; init; }

        /// <summary>过滤字面量：数字形态时非 null。</summary>
        public double? LiteralNumber { get; init; }

        /// <summary>过滤字面量：布尔关键字（true/false）时非 null。</summary>
        public bool? LiteralBool { get; init; }
    }

    /// <summary>纯数字子串转整数（路径语法保证只含 0-9）。</summary>
    private static int ParseDigits(string s, int start, int end)
    {
        int v = 0;
        for (int k = start; k < end; k++)
        {
            v = v * 10 + (s[k] - '0');
        }
        return v;
    }

    internal static List<PathSegment> ParsePath(string path)
    {
        var segments = new List<PathSegment>();
        int i = 0;
        int n = path.Length;

        void Fail(string msg) => throw new ArgumentException($"{msg}（位置 {i}）", nameof(path));

        while (i < n)
        {
            char c = path[i];
            if (c == '.')
            {
                i++;
                continue;
            }
            if (c == '[')
            {
                i++;
                if (i >= n) Fail("路径括号未闭合");
                if (path[i] == '"')
                {
                    i++;
                    var sb = new StringBuilder();
                    while (true)
                    {
                        if (i >= n) Fail("引号键未闭合");
                        char q = path[i];
                        if (q == '"') break;
                        if (q == '\\' && i + 1 < n)
                        {
                            sb.Append(path[i + 1]);
                            i += 2;
                            continue;
                        }
                        sb.Append(q);
                        i++;
                    }
                    i++;
                    if (i >= n || path[i] != ']') Fail("引号键后缺少 ']'");
                    i++;
                    segments.Add(new PathSegment { Type = SegmentKind.Key, Key = sb.ToString() });
                }
                else if (path[i] == '*')
                {
                    i++;
                    if (i >= n || path[i] != ']') Fail("[*] 后缺少 ']'");
                    i++;
                    segments.Add(new PathSegment { Type = SegmentKind.Wildcard });
                }
                else if (path[i] == '?')
                {
                    // 过滤器：[?(@.key>5)] / [?(@.name=="x")]
                    i++;
                    if (i >= n || path[i] != '(') Fail("过滤器应为 [?(@.key op literal)]");
                    i++;
                    if (!(i + 1 < n && path[i] == '@' && path[i + 1] == '.'))
                    {
                        Fail("过滤器表达式须以 @. 开头");
                    }
                    i += 2;
                    int ks = i;
                    while (i < n && (char.IsLetterOrDigit(path[i]) || path[i] == '_'))
                    {
                        i++;
                    }
                    if (ks == i) Fail("过滤器缺少键名");
                    var fkey = path.Substring(ks, i - ks);

                    FilterOp fop;
                    if (i + 1 < n && path[i] == '>' && path[i + 1] == '=') { fop = FilterOp.Gte; i += 2; }
                    else if (i + 1 < n && path[i] == '<' && path[i + 1] == '=') { fop = FilterOp.Lte; i += 2; }
                    else if (i + 1 < n && path[i] == '=' && path[i + 1] == '=') { fop = FilterOp.Eq; i += 2; }
                    else if (i + 1 < n && path[i] == '!' && path[i + 1] == '=') { fop = FilterOp.Ne; i += 2; }
                    else if (path[i] == '>') { fop = FilterOp.Gt; i++; }
                    else if (path[i] == '<') { fop = FilterOp.Lt; i++; }
                    else { Fail("过滤器缺少比较运算符（> >= < <= == !=）"); return segments; }

                    while (i < n && path[i] == ' ') i++;
                    var seg0 = ParseFilterLiteral(path, ref i, n, Fail);
                    while (i < n && path[i] == ' ') i++;
                    if (i >= n || path[i] != ')') Fail("过滤器缺少 ')'");
                    i++;
                    if (i >= n || path[i] != ']') Fail("过滤器后缺少 ']'");
                    i++;
                    segments.Add(new PathSegment
                    {
                        Type = SegmentKind.Filter,
                        Key = fkey,
                        Op = fop,
                        Literal = seg0.Item1,
                        LiteralNumber = seg0.Item2,
                        LiteralBool = seg0.Item3,
                    });
                }
                else
                {
                    // 数字索引 / 负索引 / 区间切片 [a..b)
                    bool negative = false;
                    int start = i;
                    if (path[i] == '-')
                    {
                        negative = true;
                        i++;
                    }
                    int ds = i;
                    while (i < n && (path[i] >= '0' && path[i] <= '9'))
                    {
                        i++;
                    }

                    if (!negative && i < n && i + 1 < n && path[i] == '.' && path[i + 1] == '.')
                    {
                        // 区间：from 可空，to 可空，左闭右开
                        int? from = ds == i ? null : ParseDigits(path, ds, i);
                        i += 2;
                        int ts = i;
                        while (i < n && (path[i] >= '0' && path[i] <= '9'))
                        {
                            i++;
                        }
                        int? to = ts == i ? null : ParseDigits(path, ts, i);
                        if (i >= n || path[i] != ']') Fail("[..] 后缺少 ']'");
                        i++;
                        segments.Add(new PathSegment { Type = SegmentKind.Range, RangeFrom = from, RangeTo = to });
                    }
                    else
                    {
                        if (ds == i || i >= n || path[i] != ']')
                        {
                            Fail("[ ] 内应为数字索引、负索引、区间、引号键、过滤器或 *");
                        }
                        var idxText = path.Substring(start, i - start);
                        if (!int.TryParse(idxText, out int idx))
                        {
                            Fail($"无效的数组索引 [{idxText}]");
                        }
                        i++; // ']'
                        segments.Add(new PathSegment { Type = SegmentKind.Index, Index = idx });
                    }
                }
                continue;
            }
            if (c == '*')
            {
                segments.Add(new PathSegment { Type = SegmentKind.Wildcard });
                i++;
                continue;
            }
            if (char.IsLetterOrDigit(c) || c == '_' || c == '-')
            {
                int start = i;
                while (i < n && (char.IsLetterOrDigit(path[i]) || path[i] == '_' || path[i] == '-'))
                {
                    i++;
                }
                segments.Add(new PathSegment { Type = SegmentKind.Key, Key = path.Substring(start, i - start) });
                continue;
            }
            Fail($"意外字符 '{c}'");
        }
        if (segments.Count == 0)
        {
            throw new ArgumentException("路径为空或不含任何段", nameof(path));
        }
        return segments;
    }

    /// <summary>解析过滤器比较字面量：带引号字符串、数字或 true/false 关键字。i 停在字面量之后。</summary>
    private static (string? text, double? number, bool? flag) ParseFilterLiteral(
        string path, ref int i, int n, Action<string> fail)
    {
        if (i < n && path[i] == '"')
        {
            i++;
            var sb = new StringBuilder();
            while (true)
            {
                if (i >= n) fail("过滤器字符串未闭合");
                char q = path[i];
                if (q == '"') break;
                if (q == '\\' && i + 1 < n)
                {
                    sb.Append(path[i + 1]);
                    i += 2;
                    continue;
                }
                sb.Append(q);
                i++;
            }
            i++;
            return (sb.ToString(), null, null);
        }

        // 布尔关键字
        if (i + 4 <= n && string.CompareOrdinal(path, i, "true", 0, 4) == 0)
        {
            i += 4;
            return (null, null, true);
        }
        if (i + 5 <= n && string.CompareOrdinal(path, i, "false", 0, 5) == 0)
        {
            i += 5;
            return (null, null, false);
        }

        int s = i;
        if (i < n && path[i] == '-')
        {
            i++;
        }
        while (i < n && (char.IsDigit(path[i]) || path[i] == '.' ||
                         path[i] == 'e' || path[i] == 'E'))
        {
            i++;
        }
        var raw = path.Substring(s, i - s);
        if (raw.Length > 0 && double.TryParse(raw,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var num))
        {
            return (null, num, null);
        }
        fail("过滤器字面量应为数字、带引号字符串或 true/false");
        return (null, null, null);
    }

    /// <summary>把单键格式化为规范路径段：简单标识符裸写，否则 ["转义"]。</summary>
    internal static string FormatKeySegment(string key)
    {
        var simple = key.Length > 0 && (char.IsLetter(key[0]) || key[0] == '_');
        if (simple)
        {
            foreach (var ch in key.Skip(1))
            {
                if (!(char.IsLetterOrDigit(ch) || ch == '_' || ch == '-'))
                {
                    simple = false;
                    break;
                }
            }
        }
        if (simple)
        {
            return key;
        }
        var sb = new StringBuilder("[\"");
        foreach (var ch in key)
        {
            if (ch == '"' || ch == '\\')
            {
                sb.Append('\\');
            }
            sb.Append(ch);
        }
        sb.Append("\"]");
        return sb.ToString();
    }

    /// <summary>拼接规范路径文本（供 Diff/Schema 使用）。</summary>
    internal static string Join(string parent, string childSegment)
    =>
        parent.Length == 0 ? childSegment
        : childSegment.StartsWith("[") ? parent + childSegment
        : parent + "." + childSegment;
}

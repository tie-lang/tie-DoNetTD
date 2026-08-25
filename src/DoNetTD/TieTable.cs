// Copyright (c) 2026 TIE-LANG organization. All rights reserved.
// Licensed under the TIE-LANG Open Source License v1.1 (see LICENSE in the repository root).

using System.Text;

namespace DoNetTD;

/// <summary>
/// tie:data 表节点：键恒为字符串的键值对集合。内部保持插入序；
/// tie map 官方语义是按键 strcmp 字节序有序存储与输出，
/// 因此默认写出顺序为 <see cref="TableKeyOrder.SortStrcmp"/>（见 <see cref="InStrcmpOrder"/>）。
/// </summary>
public sealed class TieTable : TieValue
{
    // 插入序键列表 + 值字典。Set 已存在键时替换值但保持原插入位置。
    private readonly List<string> _order = new List<string>();
    private readonly Dictionary<string, TieValue> _map = new Dictionary<string, TieValue>(StringComparer.Ordinal);

    /// <summary>构造空表。</summary>
    public TieTable() { }

    /// <summary>构造表并用初始化器语义填入条目。</summary>
    public TieTable(IEnumerable<KeyValuePair<string, TieValue>> entries)
    {
        if (entries is null) throw new ArgumentNullException(nameof(entries));
        foreach (var kv in entries)
        {
            Set(kv.Key, kv.Value);
        }
    }

    /// <inheritdoc />
    public override TieValueKind Kind => TieValueKind.Table;

    /// <summary>按键插入序枚举的键列表。</summary>
    public IReadOnlyList<string> Keys => _order;

    /// <summary>条目数。</summary>
    public int Count => _order.Count;

    /// <summary>
    /// 下标读写：<c>table["key"]</c>。读取不存在键返回 null；
    /// 写入 null 视为移除该键（与 JSON 初始化器习惯一致）。
    /// </summary>
    public TieValue? this[string key]
    {
        get
        {
            KeyGuard(key);
            return _map.TryGetValue(key, out var v) ? v : null;
        }
        set
        {
            KeyGuard(key);
            if (value is null)
            {
                Remove(key);
                return;
            }
            Set(key, value);
        }
    }

    /// <summary>设置条目：已存在则替换值（保持原插入位置），否则追加到末尾。</summary>
    public void Set(string key, TieValue value)
    {
        KeyGuard(key);
        if (value is null) throw new ArgumentNullException(nameof(value), "写入 null 请用 this[key] = null 移除，或显式使用 TieValue.Null");
        if (_map.ContainsKey(key))
        {
            _map[key] = value;
            return;
        }
        _order.Add(key);
        _map[key] = value;
    }

    /// <summary>链式设置：返回自身，便于 new TieTable().Set("a", ...).Set("b", ...) 风格创建。</summary>
    public TieTable SetItem(string key, TieValue value)
    {
        Set(key, value);
        return this;
    }

    /// <summary>尝试取值：存在返回 true。</summary>
    public bool TryGet(string key, out TieValue? value)
    {
        KeyGuard(key);
        var ok = _map.TryGetValue(key, out var v);
        value = v;
        return ok;
    }

    /// <summary>移除条目：存在返回 true。</summary>
    public bool Remove(string key)
    {
        KeyGuard(key);
        if (!_map.Remove(key)) return false;
        _order.Remove(key);
        return true;
    }

    /// <summary>是否包含键。</summary>
    public bool ContainsKey(string key)
    {
        KeyGuard(key);
        return _map.ContainsKey(key);
    }

    /// <summary>按插入序枚举键值对。</summary>
    public IEnumerable<KeyValuePair<string, TieValue>> Items
    {
        get
        {
            foreach (var k in _order)
            {
                yield return new KeyValuePair<string, TieValue>(k, _map[k]);
            }
        }
    }

    /// <summary>
    /// 按键 strcmp 字节序枚举键值对——tie map 的官方输出顺序
    /// （strcmp 为 UTF-8 字节序列字典序，非 .NET 默认字符串比较）。
    /// </summary>
    public IEnumerable<KeyValuePair<string, TieValue>> InStrcmpOrder()
    {
        var sorted = new string[_order.Count];
        _order.CopyTo(sorted);
        Array.Sort(sorted, StrcmpComparer.Instance);
        foreach (var k in sorted)
        {
            yield return new KeyValuePair<string, TieValue>(k, _map[k]);
        }
    }

    /// <summary>清空全部条目。</summary>
    public void Clear()
    {
        _order.Clear();
        _map.Clear();
    }

    /// <inheritdoc />
    protected override TieValue CloneCore()
    {
        var copy = new TieTable();
        foreach (var kv in Items)
        {
            copy.Set(kv.Key, kv.Value.Clone());
        }
        return copy;
    }

    /// <inheritdoc />
    // 键集合相同且各键值 Equals 即相等，与插入序无关。
    public override bool Equals(object? obj)
    {
        if (obj is not TieTable t || t._map.Count != _map.Count) return false;
        foreach (var kv in _map)
        {
            if (!t._map.TryGetValue(kv.Key, out var other)) return false;
            if (!other.Equals(kv.Value)) return false;
        }
        return true;
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 19;
            foreach (var kv in _map)
            {
                hash ^= kv.Key.GetHashCode() * 31 + kv.Value.GetHashCode();
            }
            return hash;
        }
    }

    /// <summary>输出紧凑字面量形态（诊断用途，按键排序）。</summary>
    public override string ToString()
    {
        var sb = new StringBuilder("[");
        bool first = true;
        foreach (var kv in InStrcmpOrder())
        {
            if (!first) sb.Append(", ");
            first = false;
            sb.Append('\"').Append(TieWriter.EscapeStringBody(kv.Key)).Append("\": ").Append(kv.Value.ToString());
        }
        sb.Append(']');
        return sb.ToString();
    }

    private static void KeyGuard(string key)
    {
        if (key is null) throw new ArgumentNullException(nameof(key));
    }
}

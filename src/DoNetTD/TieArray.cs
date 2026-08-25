// Copyright (c) 2026 TIE-LANG organization. All rights reserved.
// Licensed under the TIE-LANG Open Source License v1.1 (see LICENSE in the repository root).

using System.Collections;

namespace DoNetTD;

/// <summary>
/// tie:data 数组节点：值列表，按序存储。支持 LINQ/foreach（实现 IEnumerable&lt;TieValue&gt;）。
/// </summary>
public sealed class TieArray : TieValue, IEnumerable<TieValue>
{
    private readonly List<TieValue> _items = new List<TieValue>();

    /// <summary>构造空数组。</summary>
    public TieArray() { }

    /// <summary>构造数组并依次加入初值。</summary>
    public TieArray(params TieValue[] items) : this((IEnumerable<TieValue>)items) { }

    /// <summary>构造数组并依次加入初值。</summary>
    public TieArray(IEnumerable<TieValue> items)
    {
        if (items is null) throw new ArgumentNullException(nameof(items));
        foreach (var item in items)
        {
            Add(item);
        }
    }

    /// <inheritdoc />
    public override TieValueKind Kind => TieValueKind.Array;

    /// <summary>元素只读视图（修改请用索引器/Add/Insert/RemoveAt）。</summary>
    public IReadOnlyList<TieValue> Items => _items;

    /// <summary>元素个数。</summary>
    public int Count => _items.Count;

    /// <summary>按下标读写元素；下标越界抛 ArgumentOutOfRangeException。</summary>
    public TieValue this[int index]
    {
        get => _items[index];
        set => _items[index] = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>追加元素（null 不允许，空值用 <see cref="TieValue.Null"/>）。</summary>
    public void Add(TieValue item) =>
        _items.Add(item ?? throw new ArgumentNullException(nameof(item)));

    /// <summary>链式追加：返回自身，便于 new TieArray().Add(...).Add(...) 风格创建。</summary>
    public TieArray With(TieValue item)
    {
        Add(item);
        return this;
    }

    /// <summary>在指定位置插入元素。</summary>
    public void Insert(int index, TieValue item) =>
        _items.Insert(index, item ?? throw new ArgumentNullException(nameof(item)));

    /// <summary>移除指定位置元素。</summary>
    public void RemoveAt(int index) => _items.RemoveAt(index);

    /// <summary>清空全部元素。</summary>
    public void Clear() => _items.Clear();

    /// <inheritdoc />
    protected override TieValue CloneCore()
    {
        var copy = new TieArray();
        foreach (var item in _items)
        {
            copy._items.Add(item.Clone());
        }
        return copy;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        if (obj is not TieArray a || a._items.Count != _items.Count) return false;
        for (int i = 0; i < _items.Count; i++)
        {
            if (!a._items[i].Equals(_items[i])) return false;
        }
        return true;
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            foreach (var item in _items)
            {
                hash = hash * 31 + item.GetHashCode();
            }
            return hash;
        }
    }

    /// <inheritdoc />
    public IEnumerator<TieValue> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();

    /// <summary>输出紧凑字面量形态（诊断用途）。</summary>
    public override string ToString() => "[" + string.Join(", ", _items.Select(i => i.ToString())) + "]";
}

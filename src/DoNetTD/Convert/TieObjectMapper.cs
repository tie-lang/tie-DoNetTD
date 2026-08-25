// Copyright (c) 2026 TIE-LANG organization. All rights reserved.
// Licensed under the TIE-LANG Open Source License v1.1 (see LICENSE in the repository root).

using System.Collections;
using System.Globalization;
using System.Numerics;

namespace DoNetTD.Convert;

/// <summary>
/// tie:data 节点与 .NET 对象的双向映射（POCO 支持）。
/// - 表 ↔ 公共可读写实例属性（名称精确匹配，其次忽略大小写）
/// - 数组 ↔ List&lt;T&gt; / T[] / IList&lt;T&gt; 等集合
/// - 标量 ↔ 对应基元类型；枚举按名字符串写出、按名字或数值读入
/// - Guid/DateTime ↔ 字符串（不变文化，"D"/"O" 格式）
/// </summary>
public static class TieObjectMapper
{
    // ---------- tie → .NET ----------

    /// <summary>把 tie:data 节点映射为 targetType 实例。不兼容抛 InvalidCastException / NotSupportedException。</summary>
    public static object? ToObject(TieValue value, Type targetType)
    {
        if (value is null) throw new ArgumentNullException(nameof(value));
        if (targetType is null) throw new ArgumentNullException(nameof(targetType));
        targetType = StripNullable(targetType);

        if (targetType == typeof(TieValue) || targetType.IsInstanceOfType(value))
        {
            return value;
        }

        // 枚举优先于 TypeCode（枚举的 GetTypeCode 是其底层整型，会劫持分支）。
        if (targetType.IsEnum)
        {
            if (value.Kind == TieValueKind.String)
            {
                return Enum.Parse(targetType, ((TieString)value).Value, ignoreCase: true);
            }
            if (value.Kind == TieValueKind.Integer)
            {
                var underlying = Enum.GetUnderlyingType(targetType);
                var num = global::System.Convert.ChangeType(
                    ToLong(((TieInteger)value).Value, underlying), underlying, CultureInfo.InvariantCulture);
                return Enum.ToObject(targetType, num);
            }
            throw CastError(value, targetType);
        }

        if (value.Kind == TieValueKind.Null)
        {
            if (targetType.IsClass) return null;
            throw new InvalidCastException($"null 无法映射为非空值类型 {targetType.Name}");
        }

        var tc = Type.GetTypeCode(targetType);
        switch (tc)
        {
            case TypeCode.String:
                if (value.Kind == TieValueKind.String) return ((TieString)value).Value;
                if (value.Kind == TieValueKind.Char) return ((TieChar)value).AsString();
                break;
            case TypeCode.Boolean:
                Require(value, TieValueKind.Bool, targetType);
                return ((TieBool)value).Value;
            case TypeCode.Char:
                if (value.Kind == TieValueKind.Char) return ((TieChar)value).AsChar();
                break;
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32:
            case TypeCode.Int64:
                Require(value, TieValueKind.Integer, targetType);
                return global::System.Convert.ChangeType(ToLong(((TieInteger)value).Value, targetType), tc, CultureInfo.InvariantCulture);
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.UInt32:
            case TypeCode.UInt64:
                Require(value, TieValueKind.Integer, targetType);
                return global::System.Convert.ChangeType(ToULong(((TieInteger)value).Value, targetType), tc, CultureInfo.InvariantCulture);
            case TypeCode.Single:
            case TypeCode.Double:
                if (value.Kind == TieValueKind.Float) return global::System.Convert.ChangeType(((TieFloat)value).Value, tc, CultureInfo.InvariantCulture);
                if (value.Kind == TieValueKind.Integer) return global::System.Convert.ChangeType((double)((TieInteger)value).Value, tc, CultureInfo.InvariantCulture);
                throw CastError(value, targetType);
            case TypeCode.Decimal:
                if (value.Kind == TieValueKind.Float) return (decimal)((TieFloat)value).Value;
                if (value.Kind == TieValueKind.Integer) return (decimal)((TieInteger)value).Value;
                throw CastError(value, targetType);
        }

        if (targetType == typeof(BigInteger))
        {
            Require(value, TieValueKind.Integer, targetType);
            return ((TieInteger)value).Value;
        }
        if (targetType == typeof(Guid))
        {
            Require(value, TieValueKind.String, targetType);
            return Guid.Parse(((TieString)value).Value);
        }
        if (targetType == typeof(DateTime))
        {
            Require(value, TieValueKind.String, targetType);
            return DateTime.Parse(((TieString)value).Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }

        // 集合与 POCO
        if (value.Kind == TieValueKind.Array && IsCollectionTarget(targetType, out var elementType, out var factory))
        {
            return factory(value, elementType);
        }
        if (value.Kind == TieValueKind.Table && targetType.IsClass && !targetType.IsArray)
        {
            if (IsStringDictionary(targetType, out var valueType) && valueType is not null)
            {
                var closedDict = targetType.IsGenericType && !targetType.IsInterface
                    ? targetType
                    : typeof(Dictionary<,>).MakeGenericType(typeof(string), valueType);
                var dict = (IDictionary)Activator.CreateInstance(closedDict)!;
                foreach (var kv in ((TieTable)value).Items)
                {
                    dict[kv.Key] = ToObject(kv.Value, valueType);
                }
                return dict;
            }
            return BindPoco((TieTable)value, targetType);
        }

        throw value.Kind == TieValueKind.Table
            ? new NotSupportedException($"暂不支持映射到类型 {targetType.Name}（仅支持 IDictionary<string,T> 或带无参构造的 POCO 类）")
            : CastError(value, targetType);
    }

    /// <summary>泛型版 <see cref="ToObject(TieValue, Type)"/>。</summary>
    public static T? ToObject<T>(TieValue value)
    {
        var result = ToObject(value, typeof(T));
        if (result is null) return default;
        return (T)result;
    }

    private static long ToLong(BigInteger big, Type target)
    {
        if (big < long.MinValue || big > long.MaxValue)
        {
            throw new InvalidCastException($"整数值超出 {target.Name} 范围");
        }
        var l = (long)big;
        var minMax = RangeOf(target);
        if (l < minMax.min || l > minMax.max)
        {
            throw new InvalidCastException($"整数值 {l} 超出 {target.Name} 范围");
        }
        return l;
    }

    private static ulong ToULong(BigInteger big, Type target)
    {
        if (big < ulong.MinValue || big > ulong.MaxValue)
        {
            throw new InvalidCastException($"整数值超出 {target.Name} 范围");
        }
        return (ulong)big;
    }

    private static (long min, long max) RangeOf(Type t) => Type.GetTypeCode(t) switch
    {
        TypeCode.SByte => (sbyte.MinValue, sbyte.MaxValue),
        TypeCode.Int16 => (short.MinValue, short.MaxValue),
        TypeCode.Int32 => (int.MinValue, int.MaxValue),
        _ => (long.MinValue, long.MaxValue),
    };

    private static void Require(TieValue value, TieValueKind kind, Type target)
    {
        if (value.Kind != kind)
        {
            throw new InvalidCastException($"节点种类 {kindText(value.Kind)} 无法映射为 {target.Name}（需要 {kindText(kind)}）");
        }
    }

    private static InvalidCastException CastError(TieValue value, Type target) =>
        new InvalidCastException($"节点种类 {kindText(value.Kind)} 无法映射为 {target.Name}");

    private static string kindText(TieValueKind k) => k switch
    {
        TieValueKind.Null => "null",
        TieValueKind.Bool => "布尔",
        TieValueKind.Trit => "trit",
        TieValueKind.Char => "字符",
        TieValueKind.String => "字符串",
        TieValueKind.Integer => "整数",
        TieValueKind.Float => "浮点",
        TieValueKind.Array => "数组",
        TieValueKind.Table => "表",
        _ => k.ToString(),
    };

    private static Type StripNullable(Type t) =>
        t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>)
            ? t.GetGenericArguments()[0]
            : t;

    private static bool IsCollectionTarget(Type target, out Type elementType, out Func<TieValue, Type, object> factory)
    {
        elementType = typeof(object);
        factory = null!;

        if (target.IsArray)
        {
            elementType = target.GetElementType()!;
            factory = (v, et) =>
            {
                var arr = (TieArray)v;
                var result = Array.CreateInstance(et, arr.Count);
                for (int i = 0; i < arr.Count; i++)
                {
                    result.SetValue(ToObject(arr[i], et), i);
                }
                return result;
            };
            return true;
        }
        if (target.IsGenericType && target.GetGenericTypeDefinition() == typeof(List<>))
        {
            elementType = target.GetGenericArguments()[0];
            factory = (v, et) =>
            {
                var arr = (TieArray)v;
                var list = Activator.CreateInstance(target)!;
                var add = target.GetMethod("Add")!;
                foreach (var item in arr.Items)
                {
                    add.Invoke(list, new[] { ToObject(item, et) });
                }
                return list;
            };
            return true;
        }
        if (target == typeof(System.Collections.IList))
        {
            factory = (v, _) =>
            {
                var arr = (TieArray)v;
                var list = new System.Collections.ArrayList(arr.Count);
                foreach (var item in arr.Items)
                {
                    list.Add(ToObject(item, typeof(object)));
                }
                return list;
            };
            return true;
        }
        return false;
    }

    private static bool IsStringDictionary(Type target, out Type? valueType)
    {
        valueType = null;
        if (!target.IsGenericType) return false;
        var def = target.GetGenericTypeDefinition();
        if (def != typeof(Dictionary<,>) && def != typeof(IDictionary<,>)) return false;
        var args = target.GetGenericArguments();
        if (args[0] != typeof(string)) return false;
        valueType = args[1];
        return def == typeof(Dictionary<,>) || target.IsInterface;
    }

    private static object BindPoco(TieTable table, Type targetType)
    {
        var instance = Activator.CreateInstance(targetType)
            ?? throw new InvalidCastException($"无法实例化 {targetType.Name}");
        var props = targetType.GetProperties()
            .Where(p => p.CanWrite && p.GetIndexParameters().Length == 0);
        foreach (var prop in props)
        {
            TieValue? raw = table.TryGet(prop.Name, out var v1) ? v1
                : FirstOrDefault(table.Keys, prop.Name, StringComparer.OrdinalIgnoreCase) is string k2 && table.TryGet(k2, out var v2) ? v2
                : null;
            if (raw is null) continue;
            var converted = ToObject(raw, prop.PropertyType);
            // 反射对 Nullable<T> 属性赋值需要先包装成 Nullable 实例。
            var pt = prop.PropertyType;
            if (converted is not null && pt.IsGenericType && pt.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                converted = Activator.CreateInstance(pt, converted);
            }
            prop.SetValue(instance, converted);
        }
        return instance;
    }

    private static string? FirstOrDefault(IReadOnlyList<string> keys, string name, StringComparer cmp)
    {
        foreach (var k in keys)
        {
            if (cmp.Compare(k, name) == 0) return k;
        }
        return null;
    }

    // ---------- .NET → tie ----------

    /// <summary>把 .NET 对象映射为 tie:data 节点。不支持的类型抛 NotSupportedException。</summary>
    public static TieValue FromObject(object? obj)
    {
        if (obj is null) return TieNull.Instance;
        if (obj is TieValue tv) return tv;

        var type = obj.GetType();

        // 枚举优先于 TypeCode（枚举的 GetTypeCode 是其底层整型，会劫持分支）。
        if (type.IsEnum) return new TieString(obj.ToString()!); // 按名写出，配置可读
        if (type.Name == "RuntimeType") throw new NotSupportedException("不支持映射 System.Type");

        var tc = Type.GetTypeCode(type);
        switch (tc)
        {
            case TypeCode.String: return new TieString((string)obj);
            case TypeCode.Boolean: return new TieBool((bool)obj);
            case TypeCode.Char: return new TieChar((char)obj);
            case TypeCode.SByte: return new TieInteger((sbyte)obj);
            case TypeCode.Int16: return new TieInteger((short)obj);
            case TypeCode.Int32: return new TieInteger((int)obj);
            case TypeCode.Int64: return new TieInteger((long)obj);
            case TypeCode.Byte: return new TieInteger((byte)obj);
            case TypeCode.UInt16: return new TieInteger((ushort)obj);
            case TypeCode.UInt32: return new TieInteger((uint)obj);
            case TypeCode.UInt64: return new TieInteger((ulong)obj);
            case TypeCode.Single: return new TieFloat((float)obj, TieFloatSuffix.F32);
            case TypeCode.Double: return new TieFloat((double)obj, TieFloatSuffix.F64);
            case TypeCode.Decimal: return new TieFloat((double)(decimal)obj, TieFloatSuffix.F64);
        }

        if (obj is BigInteger big) return new TieInteger(big);
        if (obj is Guid guid) return new TieString(guid.ToString("D"));
        if (obj is DateTime dt) return new TieString(dt.ToString("O", CultureInfo.InvariantCulture));

        if (obj is IDictionary<string, object?> dictObj)
        {
            var t = new TieTable();
            foreach (var kv in dictObj)
            {
                t.Set(kv.Key, FromObject(kv.Value));
            }
            return t;
        }

        // 任意 IDictionary<string, T>（含 Dictionary<string, T> 与接口实现）
        foreach (var iface in type.GetInterfaces().Prepend(type))
        {
            if (!iface.IsGenericType || iface.GetGenericTypeDefinition() != typeof(IDictionary<,>))
            {
                continue;
            }
            if (iface.GetGenericArguments()[0] != typeof(string))
            {
                continue;
            }
            var dictTable = new TieTable();
            var keys = ((IEnumerable)iface.GetProperty("Keys")!.GetValue(obj)!).Cast<object>().ToList();
            var indexer = iface.GetProperty("Item")!;
            foreach (var key in keys)
            {
                var val = indexer.GetValue(obj, new[] { key });
                dictTable.Set((string)key!, FromObject(val));
            }
            return dictTable;
        }

        if (obj is IEnumerable enumerable and not string)
        {
            var a = new TieArray();
            foreach (var item in enumerable)
            {
                a.Add(FromObject(item));
            }
            return a;
        }

        // POCO：公共可读实例属性
        var table = new TieTable();
        foreach (var prop in type.GetProperties()
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0))
        {
            table.Set(prop.Name, FromObject(prop.GetValue(obj)));
        }
        return table;
    }
}

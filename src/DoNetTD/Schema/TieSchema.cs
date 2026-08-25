// Copyright (c) 2026 TIE-LANG organization. All rights reserved.
// Licensed under the TIE-LANG Open Source License v1.1 (see LICENSE in the repository root).

using System.Numerics;

namespace DoNetTD.Schema;

/// <summary>
/// 轻量声明式校验规则。用 <see cref="TieSchema"/> 工厂方法构造，
/// 链式附加约束（Min/Max/Length/Items），经 <see cref="TieSchemaValidator.Validate"/> 执行。
/// </summary>
public sealed class TieSchemaRule
{
    internal TieValueKind Kind { get; private init; }
    internal bool AllowAny { get; private init; }
    internal BigInteger? IntMin { get; private set; }
    internal BigInteger? IntMax { get; private set; }
    internal double? FloatMin { get; private set; }
    internal double? FloatMax { get; private set; }
    internal int LengthMin { get; private set; } = -1;
    internal int LengthMax { get; private set; } = -1;
    internal TieSchemaRule? Items { get; private set; }
    internal List<(string Name, TieSchemaRule Rule, bool Required)> Fields { get; } = new();
    internal bool AllowExtraFields { get; private set; } = true;

    internal TieSchemaRule(TieValueKind kind, bool allowAny = false)
    {
        Kind = kind;
        AllowAny = allowAny;
    }

    /// <summary>整数下界（含）。</summary>
    public TieSchemaRule Min(long value) { IntMin = value; return this; }

    /// <summary>整数上界（含）。</summary>
    public TieSchemaRule Max(long value) { IntMax = value; return this; }

    /// <summary>浮点下界（含）。</summary>
    public TieSchemaRule Min(double value) { FloatMin = value; return this; }

    /// <summary>浮点上界（含）。</summary>
    public TieSchemaRule Max(double value) { FloatMax = value; return this; }

    /// <summary>字符串长度 / 数组元素数约束；max &lt; 0 表示不设上限。</summary>
    public TieSchemaRule Length(int min, int max = -1)
    {
        LengthMin = min;
        LengthMax = max;
        return this;
    }

    /// <summary>数组元素规则。</summary>
    public TieSchemaRule EachItem(TieSchemaRule itemRule)
    {
        Items = itemRule ?? throw new ArgumentNullException(nameof(itemRule));
        return this;
    }

    // ---------- 对象字段（仅 Object 规则使用） ----------

    /// <summary>要求字段存在且满足规则。</summary>
    public TieSchemaRule Field(string name, TieSchemaRule rule)
    {
        Fields.Add((name, rule, true));
        return this;
    }

    /// <summary>可选字段：出现时校验，缺失不报错。</summary>
    public TieSchemaRule OptionalField(string name, TieSchemaRule rule)
    {
        Fields.Add((name, rule, false));
        return this;
    }

    /// <summary>是否允许未声明的多余字段；默认允许。</summary>
    public TieSchemaRule ExtraFields(bool allow)
    {
        AllowExtraFields = allow;
        return this;
    }
}

/// <summary>规则工厂。所有规则的构造入口。</summary>
public static class TieSchema
{
    /// <summary>任意节点都通过。</summary>
    public static TieSchemaRule Any() => new(TieValueKind.Null, allowAny: true);

    /// <summary>布尔。</summary>
    public static TieSchemaRule Bool() => new(TieValueKind.Bool);

    /// <summary>三值 trit（-1/0/+1）。</summary>
    public static TieSchemaRule Trit() => new(TieValueKind.Trit);

    /// <summary>字符。</summary>
    public static TieSchemaRule Char() => new(TieValueKind.Char);

    /// <summary>字符串。</summary>
    public static TieSchemaRule String() => new(TieValueKind.String);

    /// <summary>整数。</summary>
    public static TieSchemaRule Integer() => new(TieValueKind.Integer);

    /// <summary>浮点。</summary>
    public static TieSchemaRule Float() => new(TieValueKind.Float);

    /// <summary>数组并指定元素规则。</summary>
    public static TieSchemaRule ArrayOf(TieSchemaRule itemRule) =>
        new TieSchemaRule(TieValueKind.Array).EachItem(itemRule);

    /// <summary>对象（表）：用 builder 声明字段。</summary>
    public static TieSchemaRule Object(Action<TieSchemaObjectBuilder> build)
    {
        if (build is null) throw new ArgumentNullException(nameof(build));
        var rule = new TieSchemaRule(TieValueKind.Table);
        build(new TieSchemaObjectBuilder(rule));
        return rule;
    }
}

/// <summary>对象规则的字段声明器（<see cref="TieSchema.Object"/> 回调参数）。</summary>
public sealed class TieSchemaObjectBuilder
{
    private readonly TieSchemaRule _rule;

    internal TieSchemaObjectBuilder(TieSchemaRule rule) => _rule = rule;

    /// <summary>要求字段存在且满足规则。</summary>
    public TieSchemaObjectBuilder Required(string name, TieSchemaRule rule)
    {
        _rule.Field(name, rule);
        return this;
    }

    /// <summary>可选字段：出现时校验，缺失不报错。</summary>
    public TieSchemaObjectBuilder Optional(string name, TieSchemaRule rule)
    {
        _rule.OptionalField(name, rule);
        return this;
    }

    /// <summary>是否允许多余字段；默认允许。</summary>
    public TieSchemaObjectBuilder ExtraFields(bool allow)
    {
        _rule.ExtraFields(allow);
        return this;
    }
}

/// <summary>校验执行器。</summary>
public static class TieSchemaValidator
{
    /// <summary>
    /// 校验根值是否满足规则。全部通过返回空列表；
    /// 否则返回错误诊断（消息内嵌路径，Line/Column 为 0）。
    /// </summary>
    public static IReadOnlyList<TieDiagnostic> Validate(TieValue root, TieSchemaRule rule)
    {
        if (root is null) throw new ArgumentNullException(nameof(root));
        if (rule is null) throw new ArgumentNullException(nameof(rule));
        var errors = new List<TieDiagnostic>();
        Check(root, rule, "$", errors);
        return errors;
    }

    private static void Fail(List<TieDiagnostic> errors, string path, string message)
    {
        errors.Add(new TieDiagnostic(
            TieDiagnosticSeverity.Error, $"{path}: {message}", 0, 0, 0));
    }

    private static void Check(TieValue node, TieSchemaRule rule, string path, List<TieDiagnostic> errors)
    {
        if (rule.AllowAny)
        {
            return;
        }
        if (node.Kind != rule.Kind)
        {
            Fail(errors, path, $"应为 {KindName(rule.Kind)}，实际为 {node.Kind}");
            return;
        }

        switch (rule.Kind)
        {
            case TieValueKind.Integer:
                var big = ((TieInteger)node).Value;
                if (rule.IntMin.HasValue && big < rule.IntMin.Value)
                {
                    Fail(errors, path, $"整数小于下界 {rule.IntMin.Value}");
                }
                if (rule.IntMax.HasValue && big > rule.IntMax.Value)
                {
                    Fail(errors, path, $"整数大于上界 {rule.IntMax.Value}");
                }
                return;

            case TieValueKind.Float:
                var d = ((TieFloat)node).Value;
                if (rule.FloatMin.HasValue && d < rule.FloatMin.Value)
                {
                    Fail(errors, path, $"浮点小于下界 {rule.FloatMin.Value}");
                }
                if (rule.FloatMax.HasValue && d > rule.FloatMax.Value)
                {
                    Fail(errors, path, $"浮点大于上界 {rule.FloatMax.Value}");
                }
                return;

            case TieValueKind.String:
                CheckLength(path, ((TieString)node).Value.Length, "字符串", rule, errors);
                return;

            case TieValueKind.Char:
                return;

            case TieValueKind.Trit:
                return;

            case TieValueKind.Array:
                var arr = (TieArray)node;
                CheckLength(path, arr.Count, "数组", rule, errors);
                if (rule.Items is not null)
                {
                    for (int i = 0; i < arr.Count; i++)
                    {
                        Check(arr[i], rule.Items, $"{path}[{i}]", errors);
                    }
                }
                return;

            case TieValueKind.Table:
                CheckTable((TieTable)node, rule, path, errors);
                return;

            default:
                return;
        }
    }

    private static void CheckTable(TieTable table, TieSchemaRule rule, string path, List<TieDiagnostic> errors)
    {
        foreach (var (name, fieldRule, required) in rule.Fields)
        {
            if (table.TryGet(name, out var child) && child is not null)
            {
                Check(child, fieldRule, TiePathJoin(path, name), errors);
            }
            else if (required)
            {
                Fail(errors, path, $"缺少必需字段 \"{name}\"");
            }
        }
        if (!rule.AllowExtraFields)
        {
            foreach (var key in table.Keys)
            {
                if (!rule.Fields.Any(f => f.Name == key))
                {
                    Fail(errors, path, $"不允许的多余字段 \"{key}\"");
                }
            }
        }
    }

    private static void CheckLength(string path, int length, string what, TieSchemaRule rule, List<TieDiagnostic> errors)
    {
        if (rule.LengthMin >= 0 && length < rule.LengthMin)
        {
            Fail(errors, path, $"{what}长度 {length} 小于最小 {rule.LengthMin}");
        }
        if (rule.LengthMax >= 0 && length > rule.LengthMax)
        {
            Fail(errors, path, $"{what}长度 {length} 大于最大 {rule.LengthMax}");
        }
    }

    private static string TiePathJoin(string parent, string key)
    {
        var seg = Advanced.TiePath.FormatKeySegment(key);
        return parent == "$" ? "$." + seg
            : seg.StartsWith("[") ? parent + seg
            : parent + "." + seg;
    }

    private static string KindName(TieValueKind k) => k switch
    {
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
}

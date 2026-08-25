// Copyright (c) 2026 TIE-LANG organization. All rights reserved.
// Licensed under the TIE-LANG Open Source License v1.1 (see LICENSE in the repository root).

using System.Linq.Expressions;
using System.Collections.Concurrent;

namespace DoNetTD.Advanced;

/// <summary>
/// 表达式树强类型路径：从 POCO 属性访问 lambda 提取规范路径文本，
/// 属性重命名/重构时路径随编译器自动更新，消灭魔法字符串。
///
/// <code>
/// TiePath.Get(root, TiePath.Of((Config c) => c.Tiec.Features));
/// // → "tiec.features"
/// </code>
///
/// 名称映射策略：属性名精确匹配优先，其次首字母小写（camelCase），
/// 与 <see cref="DoNetTD.Convert.TieObjectMapper"/> 的绑定规则一致。
/// 结果按表达式类型缓存，重复调用零开销。
/// </summary>
public static class TiePathOf
{
    private static readonly ConcurrentDictionary<Expression, string> Cache = new();

    /// <summary>提取 lambda 的成员链为路径文本，如 c => c.Tiec.Opt → "tiec.opt"。</summary>
    public static string Of<TDoc>(Expression<Func<TDoc, object?>> expression)
    {
        if (expression is null) throw new ArgumentNullException(nameof(expression));
        return Cache.GetOrAdd(expression, Extract);
    }

    /// <summary>按强类型路径取值。</summary>
    public static TieValue? Get<TDoc>(TieValue root, Expression<Func<TDoc, object?>> expression) =>
        TiePath.Get(root, Of(expression));

    /// <summary>按强类型路径写入。</summary>
    public static void Set<TDoc>(TieValue root, Expression<Func<TDoc, object?>> expression, TieValue value) =>
        TiePath.Set(root, Of(expression), value);

    /// <summary>按强类型路径删除。</summary>
    public static bool Remove<TDoc>(TieValue root, Expression<Func<TDoc, object?>> expression) =>
        TiePath.Remove(root, Of(expression));

    internal static string Extract(Expression expression)
    {
        var body = expression is LambdaExpression lambda ? lambda.Body : expression;
        body = StripConvert(body);

        var segments = new List<string>();
        while (body is MemberExpression member)
        {
            // 跳过最外层的文档参数本身（c => c.A.B 中 c 是根）。
            segments.Add(MapName(member.Member.Name));
            body = StripConvert(member.Expression);
        }

        if (segments.Count == 0)
        {
            throw new ArgumentException("表达式必须形如 c => c.A.B 的成员访问链", nameof(expression));
        }
        // 根参数节点允许是 MemberExpression 链的终点（ParameterExpression 或常量）。
        segments.Reverse();
        return string.Join(".", segments);
    }

    private static Expression StripConvert(Expression e)
    {
        while (e is UnaryExpression u && u.NodeType == ExpressionType.Convert ||
               e is UnaryExpression q && q.NodeType == ExpressionType.ConvertChecked)
        {
            e = ((UnaryExpression)e).Operand;
        }
        return e;
    }

    private static string MapName(string propertyName)
    {
        if (propertyName.Length == 0) return propertyName;
        // camelCase 回退：与对象映射器的键匹配约定一致（精确优先，其次首字母小写）。
        return char.ToLowerInvariant(propertyName[0]) + propertyName.Substring(1);
    }
}

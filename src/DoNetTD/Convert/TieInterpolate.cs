// Copyright (c) 2026 TIE-LANG organization. All rights reserved.
// Licensed under the TIE-LANG Open Source License v1.1 (see LICENSE in the repository root).

namespace DoNetTD.Convert;

/// <summary>未命中变量的处理策略。</summary>
public enum MissingVarBehavior
{
    /// <summary>保留原文（如 "${UNSET}"），默认。</summary>
    Keep,

    /// <summary>替换为空串。</summary>
    Empty,

    /// <summary>抛 <see cref="TieParseException"/> 风格的 <see cref="InvalidOperationException"/>。</summary>
    Error,
}

/// <summary>
/// 字符串环境变量插值：把 tie:data 树中字符串里的 ${VAR} 展开为环境变量值。
/// - $$ 转义为字面 $；${VAR} 取变量；
/// - 默认读进程环境变量，可用自定义变量表覆盖（值优先）；
/// - 返回新树，不修改入参。
/// </summary>
public static class TieInterpolate
{
    /// <summary>展开子树内全部字符串（含表键）。标量与结构原样克隆。</summary>
    public static TieValue Expand(TieValue value,
        IReadOnlyDictionary<string, string>? variables = null,
        MissingVarBehavior missing = MissingVarBehavior.Keep)
    {
        if (value is null) throw new ArgumentNullException(nameof(value));
        switch (value.Kind)
        {
            case TieValueKind.String:
                return new TieString(ExpandString(((TieString)value).Value, variables, missing));
            case TieValueKind.Array:
            {
                var copy = new TieArray();
                foreach (var item in ((TieArray)value).Items)
                {
                    copy.Add(Expand(item, variables, missing));
                }
                return copy;
            }
            case TieValueKind.Table:
            {
                var copy = new TieTable();
                foreach (var kv in ((TieTable)value).Items)
                {
                    copy.Set(ExpandString(kv.Key, variables, missing), Expand(kv.Value, variables, missing));
                }
                return copy;
            }
            default:
                return value.Clone();
        }
    }

    /// <summary>展开单个字符串。$$ → $；${VAR} → 变量值。</summary>
    public static string ExpandString(string text,
        IReadOnlyDictionary<string, string>? variables = null,
        MissingVarBehavior missing = MissingVarBehavior.Keep)
    {
        if (text is null) throw new ArgumentNullException(nameof(text));
        if (!text.Contains('$')) return text;

        var sb = new System.Text.StringBuilder(text.Length);
        int i = 0;
        while (i < text.Length)
        {
            char c = text[i];
            if (c != '$')
            {
                sb.Append(c);
                i++;
                continue;
            }
            // "$$"
            if (i + 1 < text.Length && text[i + 1] == '$')
            {
                sb.Append('$');
                i += 2;
                continue;
            }
            // "${VAR}"
            if (i + 1 < text.Length && text[i + 1] == '{')
            {
                int close = text.IndexOf('}', i + 2);
                if (close < 0)
                {
                    sb.Append(c); // 未闭合按字面处理
                    i++;
                    continue;
                }
                var name = text.Substring(i + 2, close - i - 2);
                sb.Append(Resolve(name, variables, missing));
                i = close + 1;
                continue;
            }
            sb.Append(c);
            i++;
        }
        return sb.ToString();
    }

    private static string Resolve(string name,
        IReadOnlyDictionary<string, string>? variables,
        MissingVarBehavior missing)
    {
        if (variables is not null && variables.TryGetValue(name, out var v))
        {
            return v;
        }
        var env = Environment.GetEnvironmentVariable(name);
        if (env is not null)
        {
            return env;
        }
        return missing switch
        {
            MissingVarBehavior.Empty => string.Empty,
            MissingVarBehavior.Error => throw new InvalidOperationException(
                $"环境变量 {name} 未设置（MissingVarBehavior.Error）"),
            _ => "${" + name + "}", // Keep
        };
    }
}

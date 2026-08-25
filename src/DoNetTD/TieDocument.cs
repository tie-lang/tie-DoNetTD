// Copyright (c) 2026 TIE-LANG organization. All rights reserved.
// Licensed under the TIE-LANG Open Source License v1.1 (see LICENSE in the repository root).

using System.Text;

namespace DoNetTD;

/// <summary>
/// 一份 tie:data 文档：可选头部信息 + 恰好一个根值。
/// 解析入口见 <see cref="Parse(string, TieParseOptions?)"/>；
/// 创建入口可用对象模型直接组装后经 <see cref="FromValue"/> 包装。
/// </summary>
public sealed class TieDocument
{
    /// <summary>根值（表/数组/标量）。</summary>
    public TieValue Root { get; set; }

    /// <summary>解析时是否识别到 type tie&lt;data&gt; 头部声明。</summary>
    public bool HasHeader { get; }

    internal TieDocument(TieValue root, bool hasHeader)
    {
        Root = root ?? throw new ArgumentNullException(nameof(root));
        HasHeader = hasHeader;
    }

    /// <summary>用给定根值构造文档；withHeader=true 时写出会带头部（需 Write 时 EmitHeader）。</summary>
    public static TieDocument FromValue(TieValue root, bool withHeader = false) =>
        new TieDocument(root, withHeader);

    // ---------- 解析 ----------

    /// <summary>解析 tie:data 文本。失败抛 <see cref="TieParseException"/>。</summary>
    public static TieDocument Parse(string text, TieParseOptions? options = null)
    {
        TextGuard(text);
        return TieParser.Parse(text, options);
    }

    /// <summary>解析 UTF-8 文件（容忍 BOM）。失败抛 <see cref="TieParseException"/>。</summary>
    public static TieDocument ParseFile(string path, TieParseOptions? options = null)
    {
        PathGuard(path);
        // 显式 UTF-8 解码并剥离 BOM，避免 File.ReadAllText 默认探测引入平台差异。
        var bytes = System.IO.File.ReadAllBytes(path);
        int offset = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
        var text = new UTF8Encoding(false).GetString(bytes, offset, bytes.Length - offset);
        return Parse(text, options);
    }

    /// <summary>
    /// 非抛出解析：成功返回 true 且 document 有效；
    /// 失败返回 false 并给出诊断列表（含行列号）。
    /// </summary>
    public static bool TryParse(string text, out TieDocument? document,
        out IReadOnlyList<TieDiagnostic> diagnostics, TieParseOptions? options = null)
    {
        document = null;
        diagnostics = Array.Empty<TieDiagnostic>();
        if (text is null)
        {
            diagnostics = new[]
            {
                new TieDiagnostic(TieDiagnosticSeverity.Error, "输入文本为 null", 0, 0, 0),
            };
            return false;
        }
        try
        {
            document = TieParser.Parse(text, options);
            return true;
        }
        catch (TieParseException ex)
        {
            diagnostics = ex.Diagnostics;
            return false;
        }
    }

    // ---------- 写入 ----------

    /// <summary>写出为文本；默认美化、表键 strcmp 排序、尾逗号。</summary>
    public string Write(TieWriteOptions? options = null) => TieWriter.Write(this, options);

    /// <summary>写出为 UTF-8（无 BOM）文件。</summary>
    public void WriteToFile(string path, TieWriteOptions? options = null)
    {
        PathGuard(path);
        System.IO.File.WriteAllText(path, Write(options), new UTF8Encoding(false));
    }

    /// <summary>等同 <see cref="Write(TieWriteOptions?)"/> 默认选项。</summary>
    public override string ToString() => Write();

    private static void TextGuard(string text)
    {
        if (text is null) throw new ArgumentNullException(nameof(text));
    }

    private static void PathGuard(string path)
    {
        if (string.IsNullOrEmpty(path)) throw new ArgumentException("路径不能为空", nameof(path));
    }
}

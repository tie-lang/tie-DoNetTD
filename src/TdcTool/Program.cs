// Copyright (c) 2026 TIE-LANG organization. All rights reserved.
// Licensed under the TIE-LANG Open Source License v1.1 (see LICENSE in the repository root).

using DoNetTD;
using DoNetTD.Advanced;

namespace TdcTool;

/// <summary>
/// tdc —— tie:data 命令行工具。
/// 用法：tdc &lt;命令&gt; [选项] [参数]
///
/// 命令：
///   fmt      格式化 tie:data 文件（默认输出 stdout，-w 写回）
///   check    校验文件合法性（收集全部诊断，非法则退出码 1）
///   to-json  tie:data → JSON
///   from-json JSON → tie:data
///   merge    按官方 L2 语义分层合并多个表文档
///   get      路径取值
///   set      路径写入（值用 tie 字面量文本）
///
/// 全局：--help 显示本帮助。退出码：0 成功 / 1 操作失败 / 2 用法错误。
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help")
        {
            PrintHelp();
            return args.Length == 0 ? 2 : 0;
        }

        try
        {
            return args[0] switch
            {
                "fmt" => CmdFmt(args[1..]),
                "check" => CmdCheck(args[1..]),
                "to-json" => CmdToJson(args[1..]),
                "from-json" => CmdFromJson(args[1..]),
                "merge" => CmdMerge(args[1..]),
                "get" => CmdGet(args[1..]),
                "set" => CmdSet(args[1..]),
                _ => Usage($"未知命令 \"{args[0]}\""),
            };
        }
        catch (UsageException ex)
        {
            Console.Error.WriteLine("用法错误: " + ex.Message);
            return 2;
        }
        catch (TieParseException ex)
        {
            Console.Error.WriteLine("解析失败:");
            foreach (var d in ex.Diagnostics)
            {
                Console.Error.WriteLine("  " + d);
            }
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("错误: " + ex.Message);
            return 1;
        }
    }

    // ---------- 命令实现 ----------

    private static int CmdFmt(string[] args)
    {
        var files = new List<string>();
        bool write = false, compact = false, insertionOrder = false, noTrailing = false, preserveComments = false;
        int indent = 4;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-w" or "--write": write = true; break;
                case "--compact": compact = true; break;
                case "--insertion-order": insertionOrder = true; break;
                case "--no-trailing": noTrailing = true; break;
                case "--preserve-comments": preserveComments = true; break;
                case "--indent":
                    indent = NextInt(args, ref i, "--indent");
                    break;
                default:
                    if (args[i].StartsWith('-'))
                    {
                        throw new UsageException($"fmt 未知选项 {args[i]}");
                    }
                    files.Add(args[i]);
                    break;
            }
        }
        RequireFiles(files, "fmt");

        var opts = new TieWriteOptions
        {
            Pretty = !compact,
            Indent = new string(' ', indent),
            KeyOrder = insertionOrder ? TableKeyOrder.InsertionOrder : TableKeyOrder.SortStrcmp,
            TrailingComma = !noTrailing,
            PreserveComments = preserveComments,
        };

        int failed = 0;
        foreach (var file in files)
        {
            var doc = TieDocument.ParseFile(file);
            var text = doc.Write(opts);
            if (write)
            {
                System.IO.File.WriteAllText(file, text, new System.Text.UTF8Encoding(false));
                Console.WriteLine($"已写回 {file}");
            }
            else
            {
                Console.Write(text);
            }
        }
        return failed == 0 ? 0 : 1;
    }

    private static int CmdCheck(string[] args)
    {
        var files = args.Where(a => !a.StartsWith('-')).ToList();
        RequireFiles(files, "check");
        bool collect = !args.Contains("--first-only");

        int badFiles = 0;
        foreach (var file in files)
        {
            var ok = TieDocument.TryParse(
                System.IO.File.ReadAllText(file), out _, out var diags,
                new TieParseOptions { CollectAllErrors = collect });
            if (ok)
            {
                Console.WriteLine($"OK   {file}");
            }
            else
            {
                badFiles++;
                Console.WriteLine($"FAIL {file}");
                foreach (var d in diags)
                {
                    Console.WriteLine("       " + d);
                }
            }
        }
        return badFiles == 0 ? 0 : 1;
    }

    private static int CmdToJson(string[] args)
    {
        string? input = null, output = null;
        bool indented = false;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-o" or "--out": output = NextValue(args, ref i, "-o"); break;
                case "--indented" or "-i": indented = true; break;
                case "--help" or "-h": PrintHelp("to-json"); return 0;
                default:
                    if (args[i].StartsWith('-')) throw new UsageException($"to-json 未知选项 {args[i]}");
                    input = args[i];
                    break;
            }
        }
        if (input is null) throw new UsageException("to-json 需要一个输入文件");

        var doc = TieDocument.ParseFile(input);
        var json = DoNetTD.Convert.TieJson.ToJson(doc.Root, indented);
        WriteOutput(output, input, ".json", json + "\n");
        return 0;
    }

    private static int CmdFromJson(string[] args)
    {
        string? input = null, output = null;
        bool header = false, pretty = true;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-o" or "--out": output = NextValue(args, ref i, "-o"); break;
                case "--header": header = true; break;
                case "--compact": pretty = false; break;
                default:
                    if (args[i].StartsWith('-')) throw new UsageException($"from-json 未知选项 {args[i]}");
                    input = args[i];
                    break;
            }
        }
        if (input is null) throw new UsageException("from-json 需要一个输入文件");

        var node = DoNetTD.Convert.TieJson.FromJson(System.IO.File.ReadAllText(input));
        var doc = TieDocument.FromValue(node, withHeader: header);
        WriteOutput(output, input, ".tie",
            doc.Write(new TieWriteOptions { EmitHeader = header, Pretty = pretty }));
        return 0;
    }

    private static int CmdMerge(string[] args)
    {
        var files = new List<string>();
        string? output = null;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-o" or "--out": output = NextValue(args, ref i, "-o"); break;
                default:
                    if (args[i].StartsWith('-')) throw new UsageException($"merge 未知选项 {args[i]}");
                    files.Add(args[i]);
                    break;
            }
        }
        if (files.Count < 2) throw new UsageException("merge 至少需要两层：<base> <overlay> [...]");

        TieTable acc = (TieTable)TieDocument.ParseFile(files[0]).Root.Clone();
        for (int i = 1; i < files.Count; i++)
        {
            var layer = TieDocument.ParseFile(files[i]).Root;
            if (layer is not TieTable t)
            {
                throw new InvalidOperationException($"层文件 {files[i]} 的根不是表");
            }
            acc = TieMerge.DeepMerge(acc, t);
        }

        var mergedDoc = TieDocument.FromValue(acc);
        if (output is null)
        {
            Console.Write(mergedDoc.Write());
        }
        else
        {
            mergedDoc.WriteToFile(output);
            Console.WriteLine($"已写出 {output}");
        }
        return 0;
    }

    private static int CmdGet(string[] args)
    {
        var rest = args.Where(a => !a.StartsWith('-')).ToList();
        if (rest.Count != 2) throw new UsageException("get 用法：tdc get <file> <path>");
        var doc = TieDocument.ParseFile(rest[0]);
        var value = TiePath.Get(doc.Root, rest[1]);
        if (value is null)
        {
            Console.Error.WriteLine($"路径无命中: {rest[1]}");
            return 1;
        }
        Console.WriteLine(value.ToString());
        return 0;
    }

    private static int CmdSet(string[] args)
    {
        var rest = new List<string>();
        bool write = false;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-w" or "--write": write = true; break;
                default:
                    if (args[i].StartsWith('-')) throw new UsageException($"set 未知选项 {args[i]}");
                    rest.Add(args[i]);
                    break;
            }
        }
        if (rest.Count != 3) throw new UsageException("set 用法：tdc set <file> <path> <tie字面量> [-w]");

        var doc = TieDocument.ParseFile(rest[0]);
        var literal = TieDocument.Parse(rest[2], new TieParseOptions { AllowScalarRoot = true }).Root;
        TiePath.Set(doc.Root, rest[1], literal);

        if (write)
        {
            doc.WriteToFile(rest[0]);
            Console.WriteLine($"已写回 {rest[0]}");
        }
        else
        {
            Console.Write(doc.Write());
        }
        return 0;
    }

    // ---------- 工具 ----------

    private static void WriteOutput(string? output, string input, string suffix, string text)
    {
        if (output is null)
        {
            Console.Write(text);
            return;
        }
        System.IO.File.WriteAllText(output, text, new System.Text.UTF8Encoding(false));
        Console.WriteLine($"已写出 {output}");
    }

    private static void RequireFiles(List<string> files, string cmd)
    {
        if (files.Count == 0) throw new UsageException($"{cmd} 至少需要一个文件");
    }

    private static string NextValue(string[] args, ref int i, string optName)
    {
        if (i + 1 >= args.Length) throw new UsageException($"{optName} 缺少值");
        return args[++i];
    }

    private static int NextInt(string[] args, ref int i, string optName)
    {
        var raw = NextValue(args, ref i, optName);
        if (!int.TryParse(raw, out int v) || v is < 0 or > 16)
        {
            throw new UsageException($"{optName} 需要 0..16 的整数");
        }
        return v;
    }

    private static int Usage(string message)
    {
        Console.Error.WriteLine("用法错误: " + message);
        Console.Error.WriteLine("运行 tdc --help 查看帮助");
        return 2;
    }

    private static void PrintHelp(string? command = null)
    {
        Console.Write(HelpText);
    }

    private const string HelpText = """
        tdc — tie:data 命令行工具 (DoNetTD)

        用法: tdc <命令> [选项] [参数]

        命令:
          fmt <files...>              格式化（stdout；-w 写回原文件）
              -w, --write             写回原文件
              --compact               紧凑单行
              --indent <N>            缩进空格数（默认 4）
              --insertion-order       表键保留插入序（默认 strcmp 字节序）
              --no-trailing           不输出尾逗号
              --preserve-comments     还原注释
          check <files...>            校验合法性（默认收集全部诊断；--first-only 只报首个）
          to-json <file.tie>          转 JSON（--indented 美化，-o 输出文件）
          from-json <file.json>       转 tie:data（--header 加角色头，--compact 紧凑）
          merge <base> <overlay...>   官方 L2 语义深合并多层配置（-o 输出）
          get <file> <path>           路径取值，如 tiec.features[0]
          set <file> <path> <literal> 路径写入，值用 tie 字面量（如 3、"x"、["a"]；-w 写回）

        退出码: 0 成功 / 1 操作失败 / 2 用法错误

        示例:
          tdc fmt tie.config -w --preserve-comments
          tdc check *.data.tie
          tdc merge defaults.tie profile.tie -o merged.tie
          tdc get app.tie cache.size
        """;
}

/// <summary>用法级错误（退出码 2）。</summary>
internal sealed class UsageException : Exception
{
    public UsageException(string message) : base(message) { }
}

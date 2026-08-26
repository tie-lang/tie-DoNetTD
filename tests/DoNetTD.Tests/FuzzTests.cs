// Copyright (c) 2026 TIE-LANG organization. All rights reserved.
// Licensed under the TIE-LANG Open Source License v1.1 (see LICENSE in the repository root).

using System.Text;
using Xunit;
using DoNetTD;

namespace DoNetTD.Tests;

/// <summary>
/// Fuzz 套件：合法文档生成往返幂等 + 随机破坏输入不崩溃不挂死。
/// 固定种子保证可复现；发现失败时种子会随断言消息输出。
/// </summary>
public class FuzzTests
{
    private const int RoundTrips = 400;
    private const int CorruptionRounds = 800;
    private const int MaxInputChars = 4096;

    // ---------- 合法文档生成 → 往返幂等 ----------

    [Fact]
    public void GeneratedDocs_RoundTrip_Idempotent()
    {
        for (int seed = 1; seed <= RoundTrips; seed++)
        {
            var rng = new Random(seed);
            var doc = TieDocument.FromValue(GenerateValue(rng, depth: 0));

            var text1 = doc.Write();
            TieValue parsed1;
            try
            {
                parsed1 = TieDocument.Parse(text1).Root;
            }
            catch (TieParseException ex)
            {
                throw new InvalidOperationException(
                    $"seed={seed} 自产文本无法解析：{ex.Message}\n---\n{text1}", ex);
            }
            Assert.True(parsed1.Equals(doc.Root), $"seed={seed} 往返结构不等");

            var text2 = TieDocument.FromValue(parsed1).Write();
            Assert.True(text1 == text2, $"seed={seed} 二次写出不稳定");
        }
    }

    /// <summary>随机生成任意 tie:data 值（含 unicode 键、全部标量种类、嵌套容器）。</summary>
    private static TieValue GenerateValue(Random rng, int depth)
    {
        if (depth >= 4) return GenerateScalar(rng);

        int kind = rng.Next(11);
        return kind switch
        {
            0 => new TieTable().SetItem(GenKey(rng), GenerateValue(rng, depth + 1)),
            1 => GenTable(rng, rng.Next(0, 4), depth),
            2 => GenArray(rng, rng.Next(0, 5), depth),
            _ => GenerateScalar(rng),
        };
    }

    private static TieTable GenTable(Random rng, int entries, int depth)
    {
        var t = new TieTable();
        for (int i = 0; i < entries; i++)
        {
            var v = GenerateValue(rng, depth + 1);
            if (v.Kind is TieValueKind.Null or TieValueKind.Trit && v is not { } ok)
            {
                continue; // Null/非零 Trit 无法写为 tie:data，跳过
            }
            var key = GenKey(rng);
            if (t.ContainsKey(key)) continue;
            t.Set(key, v);
        }
        if (t.Count == 0)
        {
            // 空表没有独立字面量（[] 解析回数组），保证至少一个条目以维持往返对称
            t.Set(GenKey(rng), GenerateScalar(rng));
        }
        return t;
    }

    private static TieArray GenArray(Random rng, int items, int depth)
    {
        var a = new TieArray();
        for (int i = 0; i < items; i++)
        {
            a.Add(GenerateValue(rng, depth + 1));
        }
        return a;
    }

    private static string GenKey(Random rng)
    {
        const string pool = "abcXYZ_0123456789中文キーé😀-.";
        int len = rng.Next(1, 8);
        var sb = new StringBuilder(len);
        for (int i = 0; i < len; i++)
        {
            sb.Append(pool[rng.Next(pool.Length)]);
        }
        return sb.ToString();
    }

    private static TieValue GenerateScalar(Random rng)
    {
        switch (rng.Next(9))
        {
            case 0: return new TieInteger(rng.Next(-1000, 1000));
            case 1: return new TieInteger(rng.Next() * (rng.Next(2) == 0 ? 1 : -1), (TieIntegerSuffix)rng.Next(0, 11));
            case 2: return new TieFloat(rng.NextDouble() * 1e6 - 5e5);
            case 3: return new TieFloat(rng.NextDouble(), TieFloatSuffix.F32);
            case 4: return TieBool.True.Clone();
            case 5: return TieBool.False.Clone();
            case 6: return TieTrit.Zero.Clone();
            case 7: return new TieChar((char)rng.Next(0x20, 0x4E00));
            default:
            {
                const string pool = "abc 中文 \" \\ \n é 😀 / \t x";
                int len = rng.Next(0, 12);
                var sb = new StringBuilder(len);
                for (int i = 0; i < len; i++)
                {
                    sb.Append(pool[rng.Next(pool.Length)]);
                }
                return new TieString(sb.ToString());
            }
        }
    }

    // ---------- 破坏输入：只许 TieParseException，不许崩溃/挂死 ----------

    [Fact]
    public void CorruptedInputs_NeverCrashWithUnexpectedExceptions()
    {
        var baseTexts = new[]
        {
            Fixtures.CliConfig,
            Fixtures.FullConfig,
            """["a": 1, "b": ["x": 'c', "y": zero], "z": 3.14e2f64]""",
        };

        for (int seed = 1; seed <= CorruptionRounds; seed++)
        {
            var rng = new Random(seed);
            var text = baseTexts[seed % baseTexts.Length];
            var chars = text.ToCharArray();

            int mutations = rng.Next(1, 6);
            for (int m = 0; m < mutations && chars.Length > 0; m++)
            {
                int pos = rng.Next(chars.Length);
                switch (rng.Next(3))
                {
                    case 0: // 删除
                        chars[pos] = '\0';
                        break;
                    case 1: // 替换为高危字符
                        chars[pos] = "\"[],:\\/"[rng.Next(7)];
                        break;
                    default: // 插入
                        if (chars.Length < MaxInputChars)
                        {
                            var list = chars.ToList();
                            list.Insert(pos, "\"[],:\\"[rng.Next(5)]);
                            chars = list.ToArray();
                        }
                        break;
                }
            }

            var corrupted = new string(chars).Replace("\0", "");
            if (corrupted.Length > MaxInputChars)
            {
                corrupted = corrupted[..MaxInputChars];
            }

            Exception? caught = null;
            try
            {
                TieDocument.Parse(corrupted);
            }
            catch (TieParseException)
            {
                // 预期路径：诊断型失败
            }
            catch (Exception ex)
            {
                caught = ex;
            }
            Assert.True(caught is null,
                $"seed={seed} 出现非预期异常 {caught?.GetType().Name}: {caught?.Message}\n---\n{corrupted}");
        }
    }

    [Fact]
    public void RandomGarbageStrings_NeverCrash()
    {
        const string alphabet = "[]\"':,\\//-01az中文\n\t zero true false eE+.";
        for (int seed = 1; seed <= 500; seed++)
        {
            var rng = new Random(seed);
            int len = rng.Next(0, 200);
            var sb = new StringBuilder(len);
            for (int i = 0; i < len; i++)
            {
                sb.Append(alphabet[rng.Next(alphabet.Length)]);
            }
            try
            {
                TieDocument.Parse(sb.ToString());
            }
            catch (TieParseException)
            {
            }
            // 其他异常类型即失败（xUnit 会把异常记为测试失败）
        }
    }
}

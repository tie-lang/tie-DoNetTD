// Copyright (c) 2026 TIE-LANG organization. All rights reserved.
// Licensed under the TIE-LANG Open Source License v1.1 (see LICENSE in the repository root).

using DoNetTD;

namespace DoNetTD.Tests;

/// <summary>
/// td 语法：文件顶层允许裸表 `[...]` 或可选表名（无 var 关键字）`identifier = [...]`。
/// 覆盖解析、表名捕获、写出与往返。
/// </summary>
public class TdSyntaxTests
{
    // ---------- 解析 ----------

    [Fact]
    public void Parse_OptionalTableName_TableRoot()
    {
        var doc = TieDocument.Parse("""pack = ["k": "v"]""");
        Assert.IsType<TieTable>(doc.Root);
        Assert.False(doc.HasHeader);
    }

    [Fact]
    public void Parse_OptionalTableName_ArrayRoot_Multi()
    {
        var doc = TieDocument.Parse("name = [1, 2, 3]");
        var root = Assert.IsType<TieArray>(doc.Root);
        Assert.Equal(3, root.Count);
    }

    [Fact]
    public void Parse_OptionalTableName_ArrayRoot_Single()
    {
        var doc = TieDocument.Parse("name = [1]");
        var root = Assert.IsType<TieArray>(doc.Root);
        Assert.Single(root.Items);
    }

    [Fact]
    public void Parse_TableName_Captured()
    {
        var doc = TieDocument.Parse("identifier = [1, 2]");
        Assert.Equal("identifier", doc.TableName);
        Assert.False(doc.HasHeader);
    }

    [Fact]
    public void Parse_DoubleTableName_IsError()
    {
        var ok = TieDocument.TryParse("a = b = [1]", out _, out _);
        Assert.False(ok);
    }

    [Fact]
    public void Parse_BareTable_StillWorks()
    {
        // 裸数组
        Assert.Equal(2, ((TieArray)TieDocument.Parse("[1, 2]").Root).Count);
        // 裸表（首条目判定）
        var tbl = TieDocument.Parse("""["k": "v"]""");
        Assert.IsType<TieTable>(tbl.Root);
        Assert.Null(tbl.TableName);
        Assert.False(tbl.HasHeader);
        // 裸空数组
        Assert.IsType<TieArray>(TieDocument.Parse("[]").Root);
    }

    [Fact]
    public void Parse_Header_WithOptionalTableName()
    {
        var doc = TieDocument.Parse("type tie<data>\ncfg = [\"k\": \"v\"]");
        Assert.True(doc.HasHeader);
        Assert.Equal("cfg", doc.TableName);
        var root = Assert.IsType<TieTable>(doc.Root);
        Assert.Equal("v", ((TieString)root["k"]!).Value);
    }

    // ---------- 写出 ----------

    [Fact]
    public void Write_OptionalTableName_Prefix()
    {
        var doc = TieDocument.FromValue(new TieArray(new TieInteger(1), new TieInteger(2)));
        var text = doc.Write(new TieWriteOptions
        {
            EmitTableName = true,
            TableName = "cfg",
            Pretty = false,
            TrailingComma = false,
        });
        Assert.Equal("cfg = [1, 2]", text);
    }

    [Fact]
    public void Write_TableName_Null_FallsBackToBareTable()
    {
        var doc = TieDocument.FromValue(new TieArray(new TieInteger(1), new TieInteger(2)));
        var text = doc.Write(new TieWriteOptions
        {
            EmitTableName = true,
            Pretty = false,
            TrailingComma = false,
        });
        Assert.Equal("[1, 2]", text);
    }

    [Fact]
    public void Write_Header_PlusTableName()
    {
        var doc = TieDocument.FromValue(new TieArray(new TieInteger(1), new TieInteger(2)));
        var text = doc.Write(new TieWriteOptions
        {
            EmitHeader = true,
            EmitTableName = true,
            TableName = "cfg",
            Pretty = false,
            TrailingComma = false,
        });
        Assert.Equal("type tie<data>\ncfg = [1, 2]", text);
    }

    // ---------- 往返 ----------

    [Fact]
    public void RoundTrip_OptionalTableName_StructurePreserved()
    {
        var doc = TieDocument.Parse("cfg = [1, 2]");
        Assert.Equal("cfg", doc.TableName);

        var text = doc.Write(new TieWriteOptions
        {
            EmitTableName = true,
            TableName = "cfg",
            Pretty = false,
            TrailingComma = false,
        });
        Assert.Equal("cfg = [1, 2]", text);

        var again = TieDocument.Parse(text);
        Assert.Equal(doc.Root, again.Root);
        Assert.Equal("cfg", again.TableName);
    }
}
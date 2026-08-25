// Copyright (c) 2026 TIE-LANG organization. All rights reserved.
// Licensed under the TIE-LANG Open Source License v1.1 (see LICENSE in the repository root).

using Xunit;
using DoNetTD;
using DoNetTD.Bridge;

namespace DoNetTD.Tests;

/// <summary>JsonNode 互转桥测试（net8 目标专属，测试项目即 net8）。</summary>
public class JsonNodeBridgeTests
{
    [Fact]
    public void RoundTrip_ThroughJsonNode()
    {
        var tieNode = TieDocument.Parse(Fixtures.FullConfig).Root;
        var jsonNode = TieJsonNodeBridge.ToJsonNode(tieNode);
        var back = TieJsonNodeBridge.FromJsonNode(jsonNode);
        Assert.Equal(tieNode, back);
    }

    [Fact]
    public void FromJsonNode_TypesAndNull()
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse("""{"i": 7, "f": 2.5, "b": true, "n": null}""");
        var table = (TieTable)TieJsonNodeBridge.FromJsonNode(node)!;
        Assert.Equal(7L, ((TieInteger)table["i"]!).AsLong());
        Assert.IsType<TieFloat>(table["f"]);
        Assert.IsType<TieBool>(table["b"]);
        Assert.IsType<TieNull>(table["n"]);
    }

    [Fact]
    public void ToJsonNode_NullValue_ReturnsNull()
    {
        Assert.Null(TieJsonNodeBridge.ToJsonNode(TieNull.Instance));
    }
}

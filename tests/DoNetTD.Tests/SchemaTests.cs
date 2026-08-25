// Copyright (c) 2026 TIE-LANG organization. All rights reserved.
// Licensed under the TIE-LANG Open Source License v1.1 (see LICENSE in the repository root).

using Xunit;
using DoNetTD;
using DoNetTD.Schema;

namespace DoNetTD.Tests;

public class SchemaTests
{
    private static TieSchemaRule OfficialConfigSchema() =>
        TieSchema.Object(b => b
            .Required("advanced", TieSchema.Object(a => a
                .Required("enabled", TieSchema.Bool())
                .Required("threads", TieSchema.Integer().Min(0).Max(64))))
            .Optional("cache", TieSchema.Object(c => c
                .Required("size", TieSchema.Integer().Min(1))
                .Optional("storage", TieSchema.String().Length(1, 32)))));

    [Fact]
    public void ValidDocument_Passes()
    {
        var doc = TieDocument.Parse(Fixtures.CliConfig);
        Assert.Empty(TieSchemaValidator.Validate(doc.Root, OfficialConfigSchema()));
    }

    [Fact]
    public void MissingRequiredField_Reported()
    {
        var doc = TieDocument.Parse("""["cache": ["size": 10]]""");
        var errors = TieSchemaValidator.Validate(doc.Root, OfficialConfigSchema());
        Assert.Contains(errors, e => e.Message.Contains("缺少必需字段") && e.Message.Contains("advanced"));
    }

    [Fact]
    public void RangeViolation_Reported_WithNestedPath()
    {
        var doc = TieDocument.Parse(
            """["advanced": ["enabled": true, "threads": 999]]""");
        var errors = TieSchemaValidator.Validate(doc.Root, OfficialConfigSchema());
        var err = Assert.Single(errors);
        Assert.Contains("$.advanced.threads", err.Message);
        Assert.Contains("上界", err.Message);
    }

    [Fact]
    public void TypeMismatch_Reported()
    {
        var doc = TieDocument.Parse(
            """["advanced": ["enabled": "yes", "threads": 1]]""");
        var errors = TieSchemaValidator.Validate(doc.Root, OfficialConfigSchema());
        Assert.Single(errors); // 类型不匹配：应为布尔
    }

    [Fact]
    public void ExtraFields_RejectedWhenDisallowed()
    {
        var schema = TieSchema.Object(b => b.Required("a", TieSchema.Any()).ExtraFields(false));
        var doc = TieDocument.Parse("""["a": 1, "sneaky": 2]""");
        var errors = TieSchemaValidator.Validate(doc.Root, schema);
        Assert.Contains(errors, e => e.Message.Contains("多余字段"));
    }

    [Fact]
    public void ArrayItemRules()
    {
        var schema = TieSchema.ArrayOf(TieSchema.Integer().Min(0));
        Assert.Empty(TieSchemaValidator.Validate(TieDocument.Parse("[0, 3]").Root, schema));
        var errors = TieSchemaValidator.Validate(TieDocument.Parse("[0, -3]").Root, schema);
        Assert.Single(errors);
        Assert.Contains("$[1]", errors[0].Message);
    }
}

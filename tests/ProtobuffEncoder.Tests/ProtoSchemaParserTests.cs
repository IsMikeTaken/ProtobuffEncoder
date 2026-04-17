using ProtobuffEncoder.Attributes;
using ProtobuffEncoder.Schema;

namespace ProtobuffEncoder.Tests;

[ProtoContract]
public class ParserRoundTripModel
{
    [ProtoField(1)] public int Id { get; set; }
    [ProtoField(2)] public string Name { get; set; } = "";
}

public class ProtoSchemaParserTests
{
    [Fact]
    public void Parse_WithCrLfAndBlankLines_ParsesTopLevelMetadata()
    {
        const string proto = "\r\nsyntax = \"proto3\";\r\n\r\npackage sample.orders;\r\n\r\nmessage Order {\r\n  string id = 1;\r\n}\r\n";

        var file = ProtoSchemaParser.Parse(proto);

        Assert.Equal("proto3", file.Syntax);
        Assert.Equal("sample.orders", file.Package);
        Assert.Single(file.Messages);
        Assert.Equal("Order", file.Messages[0].Name);
        Assert.Single(file.Messages[0].Fields);
        Assert.Equal("id", file.Messages[0].Fields[0].Name);
    }

    [Fact]
    public void Parse_NestedMessageEnumOneOfAndMap_ParsesExpectedShape()
    {
        const string proto = """
            syntax = "proto3";
            package sample;

            message Outer {
              map<string, int32> labels = 1 [deprecated = true];

              oneof payload {
                string text = 2;
                bytes binary = 3 [deprecated = true];
              }

              message Inner {
                optional string detail = 1;
              }

              enum State {
                UNKNOWN = 0;
                READY = 1;
              }
            }
            """;

        var file = ProtoSchemaParser.Parse(proto);
        var outer = Assert.Single(file.Messages);

        Assert.Equal("Outer", outer.Name);
        var mapField = Assert.Single(outer.Fields);
        Assert.True(mapField.IsMap);
        Assert.True(mapField.IsDeprecated);
        Assert.Equal("string", mapField.MapKeyType);
        Assert.Equal("int32", mapField.MapValueType);

        var oneOf = Assert.Single(outer.OneOfs);
        Assert.Equal("payload", oneOf.Name);
        Assert.Equal(2, oneOf.Fields.Count);
        Assert.Equal("payload", oneOf.Fields[0].OneOfGroup);
        Assert.True(oneOf.Fields[1].IsDeprecated);

        var nestedMessage = Assert.Single(outer.NestedMessages);
        Assert.Equal("Inner", nestedMessage.Name);
        Assert.True(Assert.Single(nestedMessage.Fields).IsOptional);

        var nestedEnum = Assert.Single(outer.NestedEnums);
        Assert.Equal("State", nestedEnum.Name);
        Assert.Equal(2, nestedEnum.Values.Count);
    }

    [Fact]
    public void Parse_GeneratedSchema_RoundTripsRepresentativeModel()
    {
        var schema = ProtoSchemaGenerator.Generate(typeof(ParserRoundTripModel));

        var file = ProtoSchemaParser.Parse(schema);
        var message = Assert.Single(file.Messages);

        Assert.Equal("ParserRoundTripModel", message.Name);
        Assert.Collection(message.Fields,
            field =>
            {
                Assert.Equal("Id", field.Name);
                Assert.Equal(1, field.FieldNumber);
            },
            field =>
            {
                Assert.Equal("Name", field.Name);
                Assert.Equal(2, field.FieldNumber);
            });
    }
}

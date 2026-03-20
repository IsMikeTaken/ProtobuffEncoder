using ProtobuffEncoder.Attributes;

namespace ProtobuffEncoder.Tests;

/// <summary>
/// Tests for attribute flexibility — new constructors, targets, and property combinations.
/// </summary>
public class AttributeFlexibilityTests
{
    [Fact]
    public void ProtoField_PositionalConstructor_SetsFieldNumber()
    {
        // Arrange
        var attr = new ProtoFieldAttribute(5);

        // Assert
        Assert.Equal(5, attr.FieldNumber);
    }

    [Fact]
    public void ProtoField_DefaultConstructor_FieldNumberIsZero()
    {
        // Arrange
        var attr = new ProtoFieldAttribute();

        // Assert
        Assert.Equal(0, attr.FieldNumber);
    }

    [Fact]
    public void ProtoField_NamedProperties_AllSettable()
    {
        // Arrange & Act
        var attr = new ProtoFieldAttribute
        {
            FieldNumber = 3,
            Name = "custom_name",
            WriteDefault = true,
            IsPacked = true,
            IsDeprecated = true,
            IsRequired = true
        };

        // Assert
        Assert.Equal(3, attr.FieldNumber);
        Assert.Equal("custom_name", attr.Name);
        Assert.True(attr.WriteDefault);
        Assert.True(attr.IsPacked);
        Assert.True(attr.IsDeprecated);
        Assert.True(attr.IsRequired);
    }

    [Fact]
    public void ProtoContract_DefaultConstructor_HasDefaults()
    {
        // Arrange
        var attr = new ProtoContractAttribute();

        // Assert
        Assert.Null(attr.Name);
        Assert.Equal(0, attr.Version);
        Assert.False(attr.ExplicitFields);
    }

    [Fact]
    public void ProtoContract_StringConstructor_SetsName()
    {
        // Arrange
        var attr = new ProtoContractAttribute("OrderResponse");

        // Assert
        Assert.Equal("OrderResponse", attr.Name);
    }

    [Fact]
    public void ProtoContract_IntConstructor_SetsVersion()
    {
        // Arrange
        var attr = new ProtoContractAttribute(2);

        // Assert
        Assert.Equal(2, attr.Version);
    }

    [Fact]
    public void ProtoContract_AllNamedProperties_Settable()
    {
        // Arrange & Act
        var attr = new ProtoContractAttribute
        {
            ExplicitFields = true,
            IncludeBaseFields = true,
            ImplicitFields = true,
            SkipDefaults = false,
            Version = 5,
            Name = "TestContract",
            Metadata = "Generated from domain model"
        };

        // Assert
        Assert.True(attr.ExplicitFields);
        Assert.True(attr.IncludeBaseFields);
        Assert.True(attr.ImplicitFields);
        Assert.False(attr.SkipDefaults);
        Assert.Equal(5, attr.Version);
        Assert.Equal("TestContract", attr.Name);
        Assert.Equal("Generated from domain model", attr.Metadata);
    }

    [Fact]
    public void ProtoContract_CanBeAppliedToEnum()
    {
        // Arrange & Act — this compiles, proving the attribute target includes enum
        var attr = typeof(AnnotatedPriority)
            .GetCustomAttributes(typeof(ProtoContractAttribute), false);

        // Assert
        Assert.Single(attr);
    }

    [ProtoContract("PriorityV1", Version = 1)]
    public enum AnnotatedPriority { Low, Medium, High }

    [Fact]
    public void ProtoService_HasVersionAndMetadata()
    {
        // Arrange
        var attr = new ProtoServiceAttribute("TestService")
        {
            Version = 2,
            Metadata = "Handles test operations"
        };

        // Assert
        Assert.Equal("TestService", attr.ServiceName);
        Assert.Equal(2, attr.Version);
        Assert.Equal("Handles test operations", attr.Metadata);
    }

    [Fact]
    public void ProtoService_DefaultVersionIsZero()
    {
        // Arrange
        var attr = new ProtoServiceAttribute("Svc");

        // Assert
        Assert.Equal(0, attr.Version);
        Assert.Null(attr.Metadata);
    }

    [Fact]
    public void ProtoMethod_ConstructorSetsMethodType()
    {
        // Arrange
        var attr = new ProtoMethodAttribute(ProtoMethodType.ServerStreaming);

        // Assert
        Assert.Equal(ProtoMethodType.ServerStreaming, attr.MethodType);
    }

    [Fact]
    public void ProtoMethod_NameOverride_Settable()
    {
        // Arrange
        var attr = new ProtoMethodAttribute(ProtoMethodType.Unary)
        {
            Name = "CustomRpcName"
        };

        // Assert
        Assert.Equal("CustomRpcName", attr.Name);
    }

    [Fact]
    public void ProtoIgnore_CanBeAppliedToProperty()
    {
        // Arrange & Act
        var prop = typeof(IgnoredFieldMessage).GetProperty("Hidden")!;
        var attr = prop.GetCustomAttributes(typeof(ProtoIgnoreAttribute), false);

        // Assert
        Assert.Single(attr);
    }

    [Fact]
    public void ProtoMap_KeyValueTypeOverrides_Settable()
    {
        // Arrange
        var attr = new ProtoMapAttribute
        {
            KeyType = "string",
            ValueType = "int32"
        };

        // Assert
        Assert.Equal("string", attr.KeyType);
        Assert.Equal("int32", attr.ValueType);
    }

    [Fact]
    public void ProtoOneOf_GroupName_IsSet()
    {
        // Arrange
        var attr = new ProtoOneOfAttribute("payment_method");

        // Assert
        Assert.Equal("payment_method", attr.GroupName);
    }

    [Fact]
    public void ProtoInclude_FieldAndType_AreSet()
    {
        // Arrange
        var attr = new ProtoIncludeAttribute(10, typeof(DogModel));

        // Assert
        Assert.Equal(10, attr.FieldNumber);
        Assert.Equal(typeof(DogModel), attr.DerivedType);
    }

    [Fact]
    public void VersionedModel_Attribute_HasCorrectVersionAndName()
    {
        // Arrange & Act
        var attr = (ProtoContractAttribute)typeof(VersionedModel)
            .GetCustomAttributes(typeof(ProtoContractAttribute), false).First();

        // Assert
        Assert.Equal("NamedContract", attr.Name);
        Assert.Equal(3, attr.Version);
    }

    [Fact]
    public void VersionOnlyModel_Attribute_HasCorrectVersion()
    {
        // Arrange & Act
        var attr = (ProtoContractAttribute)typeof(VersionOnlyModel)
            .GetCustomAttributes(typeof(ProtoContractAttribute), false).First();

        // Assert
        Assert.Equal(1, attr.Version);
    }

    [Fact]
    public void Encode_Decode_UsingPositionalFieldConstructor_RoundTrips()
    {
        // Arrange — SimpleMessage uses [ProtoField(1)] positional constructor
        var original = new SimpleMessage { Id = 42, Name = "positional" };

        // Act
        var bytes = ProtobufEncoder.Encode(original);
        var decoded = ProtobufEncoder.Decode<SimpleMessage>(bytes);

        // Assert
        Assert.Equal(42, decoded.Id);
        Assert.Equal("positional", decoded.Name);
    }

    [Fact]
    public void Encode_Decode_VersionedModel_RoundTrips()
    {
        // Arrange
        var original = new VersionedModel { Data = "v3-data" };

        // Act
        var bytes = ProtobufEncoder.Encode(original);
        var decoded = ProtobufEncoder.Decode<VersionedModel>(bytes);

        // Assert
        Assert.Equal("v3-data", decoded.Data);
    }
}

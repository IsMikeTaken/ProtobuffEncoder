namespace ProtobuffEncoder.Tests;

/// <summary>
/// Tests for OneOf semantics — only the first non-default property in a group is encoded.
/// </summary>
public class OneOfTests
{
    [Fact]
    public void Encode_Decode_OneOf_EmailSet_PreservesEmail()
    {
        // Arrange
        var original = new OneOfMessage { Email = "test@example.com", Name = "Test" };

        // Act
        var bytes = ProtobufEncoder.Encode(original);
        var decoded = ProtobufEncoder.Decode<OneOfMessage>(bytes);

        // Assert
        Assert.Equal("test@example.com", decoded.Email);
        Assert.Null(decoded.Phone); // not set
        Assert.Equal("Test", decoded.Name);
    }

    [Fact]
    public void Encode_Decode_OneOf_PhoneSet_PreservesPhone()
    {
        // Arrange
        var original = new OneOfMessage { Phone = "+31612345678", Name = "Dutch" };

        // Act
        var bytes = ProtobufEncoder.Encode(original);
        var decoded = ProtobufEncoder.Decode<OneOfMessage>(bytes);

        // Assert
        Assert.Null(decoded.Email);
        Assert.Equal("+31612345678", decoded.Phone);
    }

    [Fact]
    public void Encode_OneOf_BothSet_OnlyFirstIsWritten()
    {
        // Arrange
        var original = new OneOfMessage { Email = "both@test.com", Phone = "+1234567890" };

        // Act
        var bytes = ProtobufEncoder.Encode(original);
        var decoded = ProtobufEncoder.Decode<OneOfMessage>(bytes);

        // Assert
        Assert.Equal("both@test.com", decoded.Email);
        Assert.Null(decoded.Phone);
    }

    [Fact]
    public void Encode_OneOf_NeitherSet_BothNull()
    {
        // Arrange
        var original = new OneOfMessage { Name = "No contact" };

        // Act
        var bytes = ProtobufEncoder.Encode(original);
        var decoded = ProtobufEncoder.Decode<OneOfMessage>(bytes);

        // Assert
        Assert.Null(decoded.Email);
        Assert.Null(decoded.Phone);
        Assert.Equal("No contact", decoded.Name);
    }

    [Fact]
    public void Encode_Decode_OneOf_WithRegularFields_RoundTrips()
    {
        // Arrange
        var original = new OneOfMessage { Email = "hi@example.com", Name = "Regular stays" };

        // Act
        var bytes = ProtobufEncoder.Encode(original);
        var decoded = ProtobufEncoder.Decode<OneOfMessage>(bytes);

        // Assert
        Assert.Equal("hi@example.com", decoded.Email);
        Assert.Equal("Regular stays", decoded.Name);
    }
}

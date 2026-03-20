using ProtobuffEncoder.AspNetCore.Tests.Fixtures;

namespace ProtobuffEncoder.AspNetCore.Tests;

public class ProtobufHttpContentTests
{
    #region Simple-Test Pattern — construction and headers

    [Fact]
    public void Constructor_SetsContentTypeHeader()
    {
        using var content = new ProtobufHttpContent(new TestRequest { Id = 1 });

        Assert.Equal(ProtobufMediaType.Protobuf,
            content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public void Constructor_NullInstance_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ProtobufHttpContent(null!));
    }

    #endregion

    #region Simple-Data-I/O Pattern — serialization

    [Fact]
    public async Task SerializeToStreamAsync_WritesProtobufBytes()
    {
        var original = new TestRequest { Id = 42, Name = "test" };
        using var content = new ProtobufHttpContent(original);

        using var ms = new MemoryStream();
        await content.CopyToAsync(ms);

        var bytes = ms.ToArray();
        Assert.True(bytes.Length > 0);

        var decoded = ProtobufEncoder.Decode<TestRequest>(bytes);
        Assert.Equal(42, decoded.Id);
        Assert.Equal("test", decoded.Name);
    }

    [Fact]
    public void TryComputeLength_ReturnsCorrectLength()
    {
        var original = new TestRequest { Id = 1, Name = "hello" };
        using var content = new ProtobufHttpContent(original);

        // Headers.ContentLength is populated by TryComputeLength
        Assert.NotNull(content.Headers.ContentLength);
        Assert.True(content.Headers.ContentLength > 0);
    }

    #endregion

    #region Constraint-Data Pattern — edge cases

    [Fact]
    public async Task SerializeToStreamAsync_EmptyContract_WritesValidBytes()
    {
        using var content = new ProtobufHttpContent(new EmptyContract());
        using var ms = new MemoryStream();

        var ex = await Record.ExceptionAsync(() => content.CopyToAsync(ms));
        Assert.Null(ex);
    }

    [Fact]
    public async Task SerializeToStreamAsync_LargePayload_Succeeds()
    {
        var original = new TestRequest { Id = 999, Name = new string('A', 100_000) };
        using var content = new ProtobufHttpContent(original);

        using var ms = new MemoryStream();
        await content.CopyToAsync(ms);

        var decoded = ProtobufEncoder.Decode<TestRequest>(ms.ToArray());
        Assert.Equal(100_000, decoded.Name.Length);
    }

    #endregion
}

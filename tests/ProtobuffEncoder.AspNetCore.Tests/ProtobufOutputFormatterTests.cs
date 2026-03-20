using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Formatters;
using ProtobuffEncoder.AspNetCore.Tests.Fixtures;

namespace ProtobuffEncoder.AspNetCore.Tests;

public class ProtobufOutputFormatterTests
{
    private readonly ProtobufOutputFormatter _formatter = new();

    private static OutputFormatterWriteContext CreateContext(object? obj, Type type)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        return new OutputFormatterWriteContext(
            httpContext,
            (stream, encoding) => new StreamWriter(stream, encoding),
            type,
            obj);
    }

    #region Simple-Test Pattern — constructor and capabilities

    [Fact]
    public void Constructor_RegistersProtobufMediaType()
    {
        Assert.Contains(_formatter.SupportedMediaTypes,
            mt => mt.ToString() == ProtobufMediaType.Protobuf);
    }

    [Fact]
    public void CanWriteType_NonNullType_ReturnsTrue()
    {
        Assert.True(_formatter.CanWriteResult(
            CreateContext(new TestResponse(), typeof(TestResponse))));
    }

    #endregion

    #region Simple-Data-I/O Pattern — write protobuf body

    [Fact]
    public async Task WriteResponseBodyAsync_ValidObject_WritesProtobufBytes()
    {
        var original = new TestResponse { Id = 1, Result = "ok", Success = true };
        var context = CreateContext(original, typeof(TestResponse));

        await _formatter.WriteResponseBodyAsync(context);

        var body = (MemoryStream)context.HttpContext.Response.Body;
        var bytes = body.ToArray();
        Assert.True(bytes.Length > 0);

        var decoded = ProtobufEncoder.Decode<TestResponse>(bytes);
        Assert.Equal(1, decoded.Id);
        Assert.Equal("ok", decoded.Result);
        Assert.True(decoded.Success);
    }

    [Fact]
    public async Task WriteResponseBodyAsync_NullObject_WritesNothing()
    {
        var context = CreateContext(null, typeof(TestResponse));

        await _formatter.WriteResponseBodyAsync(context);

        var body = (MemoryStream)context.HttpContext.Response.Body;
        Assert.Equal(0, body.Length);
    }

    [Fact]
    public async Task WriteResponseBodyAsync_SetsContentLength()
    {
        var original = new TestResponse { Id = 42, Result = "test" };
        var context = CreateContext(original, typeof(TestResponse));

        await _formatter.WriteResponseBodyAsync(context);

        Assert.NotNull(context.HttpContext.Response.ContentLength);
        Assert.True(context.HttpContext.Response.ContentLength > 0);
    }

    #endregion

    #region Code-Path Pattern — different model types

    [Fact]
    public async Task WriteResponseBodyAsync_EmptyContract_Succeeds()
    {
        var context = CreateContext(new EmptyContract(), typeof(EmptyContract));
        var ex = await Record.ExceptionAsync(() => _formatter.WriteResponseBodyAsync(context));
        Assert.Null(ex);
    }

    [Fact]
    public async Task WriteResponseBodyAsync_LargePayload_Succeeds()
    {
        var original = new TestRequest { Id = 999, Name = new string('Z', 50_000) };
        var context = CreateContext(original, typeof(TestRequest));

        await _formatter.WriteResponseBodyAsync(context);

        var body = (MemoryStream)context.HttpContext.Response.Body;
        var decoded = ProtobufEncoder.Decode<TestRequest>(body.ToArray());
        Assert.Equal(50_000, decoded.Name.Length);
    }

    #endregion
}

using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using ProtobuffEncoder.AspNetCore.Tests.Fixtures;

namespace ProtobuffEncoder.AspNetCore.Tests;

public class ProtobufInputFormatterTests
{
    private readonly ProtobufInputFormatter _formatter = new();

    private static InputFormatterContext CreateContext<T>(byte[] body)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = new MemoryStream(body);
        httpContext.Request.ContentType = ProtobufMediaType.Protobuf;

        var modelState = new ModelStateDictionary();
        var metadata = new EmptyModelMetadataProvider()
            .GetMetadataForType(typeof(T));

        return new InputFormatterContext(
            httpContext,
            modelName: "model",
            modelState,
            metadata,
            (stream, encoding) => new StreamReader(stream, encoding));
    }

    #region Simple-Test Pattern — constructor and media type

    [Fact]
    public void Constructor_RegistersProtobufMediaType()
    {
        Assert.Contains(_formatter.SupportedMediaTypes,
            mt => mt.ToString() == ProtobufMediaType.Protobuf);
    }

    [Fact]
    public void CanReadType_TypeWithDefaultConstructor_ReturnsTrue()
    {
        Assert.True(_formatter.CanRead(CreateContext<TestRequest>([])));
    }

    [Fact]
    public void CanReadType_TypeWithoutDefaultConstructor_ReturnsFalse()
    {
        Assert.False(_formatter.CanRead(CreateContext<NoDefaultCtorModel>([])));
    }

    #endregion

    #region Simple-Data-I/O Pattern — read protobuf body

    [Fact]
    public async Task ReadRequestBodyAsync_ValidProtobuf_DecodesSuccessfully()
    {
        var original = new TestRequest { Id = 42, Name = "hello" };
        var bytes = ProtobufEncoder.Encode(original);

        var context = CreateContext<TestRequest>(bytes);
        var result = await _formatter.ReadRequestBodyAsync(context);

        Assert.True(result.HasError == false);
        var decoded = Assert.IsType<TestRequest>(result.Model);
        Assert.Equal(42, decoded.Id);
        Assert.Equal("hello", decoded.Name);
    }

    [Fact]
    public async Task ReadRequestBodyAsync_EmptyBody_ReturnsNoValue()
    {
        var context = CreateContext<TestRequest>([]);
        var result = await _formatter.ReadRequestBodyAsync(context);

        Assert.True(result.HasError == false);
        Assert.Null(result.Model);
    }

    #endregion

    #region Constraint-Data Pattern — boundary payloads

    [Fact]
    public async Task ReadRequestBodyAsync_EmptyContract_DecodesSuccessfully()
    {
        var bytes = ProtobufEncoder.Encode(new EmptyContract());
        // Empty contract may produce 0-byte payload
        var context = CreateContext<EmptyContract>(bytes);
        var result = await _formatter.ReadRequestBodyAsync(context);

        // Either decoded successfully or returned no-value for empty payload
        Assert.True(result.HasError == false);
    }

    [Fact]
    public async Task ReadRequestBodyAsync_LargePayload_DecodesSuccessfully()
    {
        var original = new TestRequest { Id = int.MaxValue, Name = new string('X', 10_000) };
        var bytes = ProtobufEncoder.Encode(original);

        var context = CreateContext<TestRequest>(bytes);
        var result = await _formatter.ReadRequestBodyAsync(context);

        var decoded = Assert.IsType<TestRequest>(result.Model);
        Assert.Equal(int.MaxValue, decoded.Id);
        Assert.Equal(10_000, decoded.Name.Length);
    }

    #endregion

    #region Performance-Test Pattern

    [Fact]
    public async Task ReadRequestBodyAsync_HighVolume_CompletesEfficiently()
    {
        var original = new TestRequest { Id = 1, Name = "perf" };
        var bytes = ProtobufEncoder.Encode(original);

        var sw = System.Diagnostics.Stopwatch.StartNew();

        for (int i = 0; i < 1_000; i++)
        {
            var context = CreateContext<TestRequest>(bytes);
            await _formatter.ReadRequestBodyAsync(context);
        }

        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds < 5000,
            $"1000 reads took {sw.ElapsedMilliseconds}ms");
    }

    #endregion
}

using Microsoft.CodeAnalysis.Testing;
using Xunit;
using ProtobuffEncoder.Analyzers.Tests.Helpers;
using VerifyCS = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
    ProtobuffEncoder.Analyzers.ProtoServiceAnalyzer,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace ProtobuffEncoder.Analyzers.Tests;

public class ProtoServiceAnalyzerTests
{
    private static string Wrap(string code) => AttributeStubs.Source + "\n" + code;

    // ── PROTO011: Service with no methods ──

    [Fact]
    public async Task PROTO011_ServiceWithNoMethods_Reports()
    {
        var test = Wrap("""
            namespace Test
            {
                using ProtobuffEncoder.Attributes;

                [ProtoService("EmptyService")]
                public interface {|#0:IEmptyService|} { }
            }
            """);

        await new Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerTest<ProtoServiceAnalyzer, DefaultVerifier>
        {
            TestCode = test,
            ExpectedDiagnostics =
            {
                VerifyCS.Diagnostic("PROTO011").WithLocation(0).WithArguments("IEmptyService"),
            },
        }.RunAsync();
    }

    [Fact]
    public async Task PROTO011_ServiceWithMethodButNoAttribute_Reports()
    {
        var test = Wrap("""
            namespace Test
            {
                using ProtobuffEncoder.Attributes;
                using System.Threading.Tasks;

                [ProtoService("NoAttrService")]
                public interface {|#0:INoAttrService|}
                {
                    Task<string> DoWork(string input);
                }
            }
            """);

        await new Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerTest<ProtoServiceAnalyzer, DefaultVerifier>
        {
            TestCode = test,
            ExpectedDiagnostics =
            {
                VerifyCS.Diagnostic("PROTO011").WithLocation(0).WithArguments("INoAttrService"),
            },
        }.RunAsync();
    }

    [Fact]
    public async Task PROTO011_ServiceWithProtoMethod_NoDiagnostic()
    {
        var test = Wrap("""
            namespace Test
            {
                using ProtobuffEncoder.Attributes;
                using System.Threading.Tasks;

                [ProtoService("GoodService")]
                public interface IGoodService
                {
                    [ProtoMethod(ProtoMethodType.Unary)]
                    Task<string> DoWork(string input);
                }
            }
            """);

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    // ── PROTO012: Streaming return type mismatch ──

    [Fact]
    public async Task PROTO012_ServerStreamingWithTaskReturn_Reports()
    {
        var test = Wrap("""
            namespace Test
            {
                using ProtobuffEncoder.Attributes;
                using System.Threading.Tasks;

                [ProtoService("StreamService")]
                public interface IStreamService
                {
                    [ProtoMethod(ProtoMethodType.ServerStreaming)]
                    Task<string> {|#0:StreamData|}(string input);
                }
            }
            """);

        var expected = VerifyCS.Diagnostic("PROTO012")
            .WithLocation(0)
            .WithArguments("StreamData", "IStreamService", "ServerStreaming");

        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task PROTO012_DuplexStreamingWithTaskReturn_Reports()
    {
        var test = Wrap("""
            namespace Test
            {
                using ProtobuffEncoder.Attributes;
                using System.Threading.Tasks;
                using System.Collections.Generic;

                [ProtoService("DuplexService")]
                public interface IDuplexService
                {
                    [ProtoMethod(ProtoMethodType.DuplexStreaming)]
                    Task<string> {|#0:Chat|}(IAsyncEnumerable<string> input);
                }
            }
            """);

        var expected = VerifyCS.Diagnostic("PROTO012")
            .WithLocation(0)
            .WithArguments("Chat", "IDuplexService", "DuplexStreaming");

        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task PROTO012_ServerStreamingWithAsyncEnumerable_NoDiagnostic()
    {
        var test = Wrap("""
            namespace Test
            {
                using ProtobuffEncoder.Attributes;
                using System.Collections.Generic;

                [ProtoService("OkStreamService")]
                public interface IOkStreamService
                {
                    [ProtoMethod(ProtoMethodType.ServerStreaming)]
                    IAsyncEnumerable<string> StreamData(string input);
                }
            }
            """);

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task PROTO012_UnaryWithTaskReturn_NoDiagnostic()
    {
        var test = Wrap("""
            namespace Test
            {
                using ProtobuffEncoder.Attributes;
                using System.Threading.Tasks;

                [ProtoService("UnaryService")]
                public interface IUnaryService
                {
                    [ProtoMethod(ProtoMethodType.Unary)]
                    Task<string> DoWork(string input);
                }
            }
            """);

        await VerifyCS.VerifyAnalyzerAsync(test);
    }
}

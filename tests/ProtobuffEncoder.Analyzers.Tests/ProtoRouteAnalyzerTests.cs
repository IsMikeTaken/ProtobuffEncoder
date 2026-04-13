using Microsoft.CodeAnalysis.Testing;
using Xunit;
using ProtobuffEncoder.Analyzers.Tests.Helpers;
using VerifyCS = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
    ProtobuffEncoder.Analyzers.ProtoRouteAnalyzer,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace ProtobuffEncoder.Analyzers.Tests;

public class ProtoRouteAnalyzerTests
{
    // Assembly-level attributes can't be inside a namespace block, so the `using`
    // directive must come BEFORE the stubs (which declare a namespace).
    private static string Wrap(string code) =>
        "using ProtobuffEncoder.Attributes;\n" + AttributeStubs.Source + "\n" + code;

    // ── PROTO016: Empty folder name ──

    [Fact]
    public async Task PROTO016_EmptyFolder_Reports()
    {
        var test = Wrap("""
            [{|#0:assembly: ProtoRoute("", "Request")|}]
            """);

        var expected = VerifyCS.Diagnostic("PROTO016")
            .WithLocation(0)
            .WithArguments("TestProject");

        await new Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerTest<
            ProtoRouteAnalyzer, DefaultVerifier>
        {
            TestCode = test,
            ExpectedDiagnostics = { expected },
        }.RunAsync();
    }

    [Fact]
    public async Task PROTO016_WhitespaceFolder_Reports()
    {
        var test = Wrap("""
            [{|#0:assembly: ProtoRoute("   ", "Request")|}]
            """);

        var expected = VerifyCS.Diagnostic("PROTO016")
            .WithLocation(0)
            .WithArguments("TestProject");

        await new Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerTest<
            ProtoRouteAnalyzer, DefaultVerifier>
        {
            TestCode = test,
            ExpectedDiagnostics = { expected },
        }.RunAsync();
    }

    [Fact]
    public async Task PROTO016_ValidFolder_NoDiagnostic()
    {
        var test = Wrap("""
            [assembly: ProtoRoute("requests", "Request")]
            """);

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    // ── PROTO017: No tokens ──

    [Fact]
    public async Task PROTO017_NoTokens_Reports()
    {
        var test = Wrap("""
            [{|#0:assembly: ProtoRoute("requests")|}]
            """);

        var expected = VerifyCS.Diagnostic("PROTO017")
            .WithLocation(0)
            .WithArguments("requests", "TestProject");

        await new Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerTest<
            ProtoRouteAnalyzer, DefaultVerifier>
        {
            TestCode = test,
            ExpectedDiagnostics = { expected },
        }.RunAsync();
    }

    [Fact]
    public async Task PROTO017_SingleToken_NoDiagnostic()
    {
        var test = Wrap("""
            [assembly: ProtoRoute("requests", "Request")]
            """);

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task PROTO017_MultipleTokens_NoDiagnostic()
    {
        var test = Wrap("""
            [assembly: ProtoRoute("messages", "Message", "Event", "Notification")]
            """);

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    // ── Combined: multiple routes, some valid, some not ──

    [Fact]
    public async Task PROTO016_And_PROTO017_BothInSameAssembly_BothReport()
    {
        var test = Wrap("""
            [{|#0:assembly: ProtoRoute("", "Request")|}]
            [{|#1:assembly: ProtoRoute("services")|}]
            """);

        await new Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerTest<
            ProtoRouteAnalyzer, DefaultVerifier>
        {
            TestCode = test,
            ExpectedDiagnostics =
            {
                VerifyCS.Diagnostic("PROTO016").WithLocation(0).WithArguments("TestProject"),
                VerifyCS.Diagnostic("PROTO017").WithLocation(1).WithArguments("services", "TestProject"),
            },
        }.RunAsync();
    }

    [Fact]
    public async Task ValidRoutes_NoDiagnostic()
    {
        var test = Wrap("""
            [assembly: ProtoToolOptions(ProtoPath = "Contracts/Proto")]
            [assembly: ProtoRoute("requests",  "Request", "Query")]
            [assembly: ProtoRoute("responses", "Response", "Result")]
            [assembly: ProtoRoute("messages",  "Message", "Event")]
            [assembly: ProtoRoute("services",  "Service")]
            """);

        await VerifyCS.VerifyAnalyzerAsync(test);
    }
}

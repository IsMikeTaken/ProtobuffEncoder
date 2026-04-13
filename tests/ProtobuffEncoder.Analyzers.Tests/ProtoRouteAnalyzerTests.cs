using Microsoft.CodeAnalysis.Testing;
using ProtobuffEncoder.Analyzers.Tests.Helpers;
using Xunit;
using VerifyCS = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
    ProtobuffEncoder.Analyzers.ProtoRouteAnalyzer,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace ProtobuffEncoder.Analyzers.Tests;

public class ProtoRouteAnalyzerTests
{
    // Assembly-level [assembly: ...] attributes cannot appear after a namespace
    // declaration in the same file (CS1730). The stubs are therefore injected as a
    // *second* source file so the test file stays clean: using → assembly attributes.
    private static Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerTest<ProtoRouteAnalyzer, DefaultVerifier>
        MakeTest(string testCode, params DiagnosticResult[] expected)
    {
        var test = new Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerTest<
            ProtoRouteAnalyzer, DefaultVerifier>
        {
            TestState =
            {
                Sources =
                {
                    // File 1: the actual test source (using → assembly attrs only).
                    testCode,
                    // File 2: attribute stubs in their own compilation unit.
                    ("AttributeStubs.cs", AttributeStubs.Source),
                },
            },
        };
        foreach (var d in expected)
            test.ExpectedDiagnostics.Add(d);
        return test;
    }

    // ── PROTO016: Empty folder name ──

    [Fact]
    public async Task PROTO016_EmptyFolder_Reports()
    {
        var test = MakeTest(
            """
            using ProtobuffEncoder.Attributes;

            [{|#0:assembly: ProtoRoute("", "Request")|}]
            """,
            VerifyCS.Diagnostic("PROTO016").WithLocation(0).WithArguments("TestProject"));

        await test.RunAsync();
    }

    [Fact]
    public async Task PROTO016_WhitespaceFolder_Reports()
    {
        var test = MakeTest(
            """
            using ProtobuffEncoder.Attributes;

            [{|#0:assembly: ProtoRoute("   ", "Request")|}]
            """,
            VerifyCS.Diagnostic("PROTO016").WithLocation(0).WithArguments("TestProject"));

        await test.RunAsync();
    }

    [Fact]
    public async Task PROTO016_ValidFolder_NoDiagnostic()
    {
        await MakeTest("""
            using ProtobuffEncoder.Attributes;

            [assembly: ProtoRoute("requests", "Request")]
            """).RunAsync();
    }

    // ── PROTO017: No tokens ──

    [Fact]
    public async Task PROTO017_NoTokens_Reports()
    {
        var test = MakeTest(
            """
            using ProtobuffEncoder.Attributes;

            [{|#0:assembly: ProtoRoute("requests")|}]
            """,
            VerifyCS.Diagnostic("PROTO017").WithLocation(0).WithArguments("requests", "TestProject"));

        await test.RunAsync();
    }

    [Fact]
    public async Task PROTO017_SingleToken_NoDiagnostic()
    {
        await MakeTest("""
            using ProtobuffEncoder.Attributes;

            [assembly: ProtoRoute("requests", "Request")]
            """).RunAsync();
    }

    [Fact]
    public async Task PROTO017_MultipleTokens_NoDiagnostic()
    {
        await MakeTest("""
            using ProtobuffEncoder.Attributes;

            [assembly: ProtoRoute("messages", "Message", "Event", "Notification")]
            """).RunAsync();
    }

    // ── Combined: multiple routes, some valid, some not ──

    [Fact]
    public async Task PROTO016_And_PROTO017_BothInSameAssembly_BothReport()
    {
        var test = MakeTest(
            """
            using ProtobuffEncoder.Attributes;

            [{|#0:assembly: ProtoRoute("", "Request")|}]
            [{|#1:assembly: ProtoRoute("services")|}]
            """,
            VerifyCS.Diagnostic("PROTO016").WithLocation(0).WithArguments("TestProject"),
            VerifyCS.Diagnostic("PROTO017").WithLocation(1).WithArguments("services", "TestProject"));

        await test.RunAsync();
    }

    [Fact]
    public async Task ValidRoutes_NoDiagnostic()
    {
        await MakeTest("""
            using ProtobuffEncoder.Attributes;

            [assembly: ProtoToolOptions(ProtoPath = "Contracts/Proto")]
            [assembly: ProtoRoute("requests",  "Request", "Query")]
            [assembly: ProtoRoute("responses", "Response", "Result")]
            [assembly: ProtoRoute("messages",  "Message", "Event")]
            [assembly: ProtoRoute("services",  "Service")]
            """).RunAsync();
    }
}

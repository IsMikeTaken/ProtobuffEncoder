using Xunit;
using ProtobuffEncoder.Analyzers.Tests.Helpers;
using VerifyCS = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
    ProtobuffEncoder.Analyzers.ProtoContractAnalyzer,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace ProtobuffEncoder.Analyzers.Tests;

public class ProtoContractAnalyzerTests
{
    private static string Wrap(string code) => AttributeStubs.Source + "\n" + code;

    // ── PROTO001: No serializable fields ──

    [Fact]
    public async Task PROTO001_ContractWithNoProperties_Reports()
    {
        var test = Wrap("""
            namespace Test
            {
                using ProtobuffEncoder.Attributes;

                [ProtoContract]
                public class {|#0:Empty|} { }
            }
            """);

        var expected = VerifyCS.Diagnostic("PROTO001")
            .WithLocation(0)
            .WithArguments("Empty");

        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task PROTO001_ContractWithReadWriteProperty_NoDiagnostic()
    {
        var test = Wrap("""
            namespace Test
            {
                using ProtobuffEncoder.Attributes;

                [ProtoContract]
                public class Valid
                {
                    [ProtoField(1)] public string Name { get; set; }
                }
            }
            """);

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task PROTO001_ExplicitFieldsWithNoMarkedProperties_Reports()
    {
        var test = Wrap("""
            namespace Test
            {
                using ProtobuffEncoder.Attributes;

                [ProtoContract(ExplicitFields = true)]
                public class {|#0:NoFields|}
                {
                    public string Name { get; set; }
                }
            }
            """);

        var expected = VerifyCS.Diagnostic("PROTO001")
            .WithLocation(0)
            .WithArguments("NoFields");

        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    // ── PROTO002: Duplicate field number ──

    [Fact]
    public async Task PROTO002_DuplicateFieldNumbers_Reports()
    {
        var test = Wrap("""
            namespace Test
            {
                using ProtobuffEncoder.Attributes;

                [ProtoContract]
                public class Dup
                {
                    [ProtoField(1)] public string A { get; set; }
                    [ProtoField(1)] public string {|#0:B|} { get; set; }
                }
            }
            """);

        var expected = VerifyCS.Diagnostic("PROTO002")
            .WithLocation(0)
            .WithArguments(1, "A", "B", "Dup");

        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task PROTO002_UniqueFieldNumbers_NoDiagnostic()
    {
        var test = Wrap("""
            namespace Test
            {
                using ProtobuffEncoder.Attributes;

                [ProtoContract]
                public class Ok
                {
                    [ProtoField(1)] public string A { get; set; }
                    [ProtoField(2)] public string B { get; set; }
                }
            }
            """);

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    // ── PROTO003: Missing parameterless constructor ──

    [Fact]
    public async Task PROTO003_NoParameterlessConstructor_Reports()
    {
        var test = Wrap("""
            namespace Test
            {
                using ProtobuffEncoder.Attributes;

                [ProtoContract]
                public class {|#0:NoCtor|}
                {
                    public NoCtor(int x) { }
                    [ProtoField(1)] public int X { get; set; }
                }
            }
            """);

        // PROTO003 + PROTO001 (no serialisable properties because the only prop has no setter issue — wait, X has get;set;)
        // Actually NoCtor has [ProtoField(1)] X with get;set; so PROTO001 should not fire.
        // Only PROTO003 should fire.
        var expected = VerifyCS.Diagnostic("PROTO003")
            .WithLocation(0)
            .WithArguments("NoCtor");

        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task PROTO003_ImplicitParameterlessConstructor_NoDiagnostic()
    {
        var test = Wrap("""
            namespace Test
            {
                using ProtobuffEncoder.Attributes;

                [ProtoContract]
                public class HasImplicit
                {
                    [ProtoField(1)] public int X { get; set; }
                }
            }
            """);

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    // ── PROTO004: Property without setter ──

    [Fact]
    public async Task PROTO004_PropertyWithoutSetter_Reports()
    {
        var test = Wrap("""
            namespace Test
            {
                using ProtobuffEncoder.Attributes;

                [ProtoContract]
                public class NoSetter
                {
                    public string {|#0:ReadOnly|} { get; }
                    [ProtoField(1)] public string Writable { get; set; }
                }
            }
            """);

        var expected = VerifyCS.Diagnostic("PROTO004")
            .WithLocation(0)
            .WithArguments("ReadOnly", "NoSetter");

        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task PROTO004_IgnoredPropertyWithoutSetter_NoDiagnostic()
    {
        var test = Wrap("""
            namespace Test
            {
                using ProtobuffEncoder.Attributes;

                [ProtoContract]
                public class Ignored
                {
                    [ProtoIgnore] public string ReadOnly { get; }
                    [ProtoField(1)] public string Writable { get; set; }
                }
            }
            """);

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    // ── PROTO008: Mutable struct contract ──

    [Fact]
    public async Task PROTO008_StructContract_Reports()
    {
        var test = Wrap("""
            namespace Test
            {
                using ProtobuffEncoder.Attributes;

                [ProtoContract]
                public struct {|#0:Pt|}
                {
                    [ProtoField(1)] public int X { get; set; }
                    [ProtoField(2)] public int Y { get; set; }
                }
            }
            """);

        var expected = VerifyCS.Diagnostic("PROTO008")
            .WithLocation(0)
            .WithArguments("Pt");

        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    // ── PROTO009: OneOf with single member ──

    [Fact]
    public async Task PROTO009_SingleMemberOneOf_Reports()
    {
        var test = Wrap("""
            namespace Test
            {
                using ProtobuffEncoder.Attributes;

                [ProtoContract]
                public class {|#0:SingleOneOf|}
                {
                    [ProtoField(1)]
                    [ProtoOneOf("channel")]
                    public string Email { get; set; }

                    [ProtoField(2)] public int Id { get; set; }
                }
            }
            """);

        var expected = VerifyCS.Diagnostic("PROTO009")
            .WithLocation(0)
            .WithArguments("channel", "SingleOneOf");

        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task PROTO009_TwoMemberOneOf_NoDiagnostic()
    {
        var test = Wrap("""
            namespace Test
            {
                using ProtobuffEncoder.Attributes;

                [ProtoContract]
                public class TwoOneOf
                {
                    [ProtoField(1)]
                    [ProtoOneOf("channel")]
                    public string Email { get; set; }

                    [ProtoField(2)]
                    [ProtoOneOf("channel")]
                    public string Phone { get; set; }
                }
            }
            """);

        await VerifyCS.VerifyAnalyzerAsync(test);
    }
}

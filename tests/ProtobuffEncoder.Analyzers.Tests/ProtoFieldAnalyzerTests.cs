using Xunit;
using ProtobuffEncoder.Analyzers.Tests.Helpers;
using VerifyCS = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
    ProtobuffEncoder.Analyzers.ProtoFieldAnalyzer,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace ProtobuffEncoder.Analyzers.Tests;

public class ProtoFieldAnalyzerTests
{
    private static string Wrap(string code) => AttributeStubs.Source + "\n" + code;

    // ── PROTO005: ProtoField without ProtoContract ──

    [Fact]
    public async Task PROTO005_FieldWithoutContract_Reports()
    {
        var test = Wrap("""
            namespace Test
            {
                using ProtobuffEncoder.Attributes;

                public class NoContract
                {
                    [ProtoField(1)] public string {|#0:Name|} { get; set; }
                }
            }
            """);

        var expected = VerifyCS.Diagnostic("PROTO005")
            .WithLocation(0)
            .WithArguments("Name", "NoContract");

        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task PROTO005_FieldWithContract_NoDiagnostic()
    {
        var test = Wrap("""
            namespace Test
            {
                using ProtobuffEncoder.Attributes;

                [ProtoContract]
                public class WithContract
                {
                    [ProtoField(1)] public string Name { get; set; }
                }
            }
            """);

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    // ── PROTO006: Invalid field number ──

    [Fact]
    public async Task PROTO006_NegativeFieldNumber_Reports()
    {
        var test = Wrap("""
            namespace Test
            {
                using ProtobuffEncoder.Attributes;

                [ProtoContract]
                public class Bad
                {
                    [ProtoField(-1)] public string {|#0:Name|} { get; set; }
                }
            }
            """);

        var expected = VerifyCS.Diagnostic("PROTO006")
            .WithLocation(0)
            .WithArguments(-1, "Name");

        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task PROTO006_ExceedsMaxFieldNumber_Reports()
    {
        var test = Wrap("""
            namespace Test
            {
                using ProtobuffEncoder.Attributes;

                [ProtoContract]
                public class TooBig
                {
                    [ProtoField(536870912)] public string {|#0:Name|} { get; set; }
                }
            }
            """);

        var expected = VerifyCS.Diagnostic("PROTO006")
            .WithLocation(0)
            .WithArguments(536870912, "Name");

        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task PROTO006_ValidFieldNumber_NoDiagnostic()
    {
        var test = Wrap("""
            namespace Test
            {
                using ProtobuffEncoder.Attributes;

                [ProtoContract]
                public class Ok
                {
                    [ProtoField(1)] public string Name { get; set; }
                }
            }
            """);

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    // ── PROTO007: Reserved field number ──

    [Fact]
    public async Task PROTO007_ReservedFieldNumber_Reports()
    {
        var test = Wrap("""
            namespace Test
            {
                using ProtobuffEncoder.Attributes;

                [ProtoContract]
                public class Reserved
                {
                    [ProtoField(19000)] public string {|#0:Name|} { get; set; }
                }
            }
            """);

        var expected = VerifyCS.Diagnostic("PROTO007")
            .WithLocation(0)
            .WithArguments(19000, "Name");

        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task PROTO007_UpperBoundReserved_Reports()
    {
        var test = Wrap("""
            namespace Test
            {
                using ProtobuffEncoder.Attributes;

                [ProtoContract]
                public class UpperReserved
                {
                    [ProtoField(19999)] public string {|#0:Name|} { get; set; }
                }
            }
            """);

        var expected = VerifyCS.Diagnostic("PROTO007")
            .WithLocation(0)
            .WithArguments(19999, "Name");

        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task PROTO007_JustOutsideReserved_NoDiagnostic()
    {
        var test = Wrap("""
            namespace Test
            {
                using ProtobuffEncoder.Attributes;

                [ProtoContract]
                public class NotReserved
                {
                    [ProtoField(18999)] public string A { get; set; }
                    [ProtoField(20000)] public string B { get; set; }
                }
            }
            """);

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    // ── PROTO010: Unrecognised encoding ──

    [Fact]
    public async Task PROTO010_UnrecognisedEncoding_Reports()
    {
        var test = Wrap("""
            namespace Test
            {
                using ProtobuffEncoder.Attributes;

                [ProtoContract]
                public class BadEnc
                {
                    [ProtoField(1, Encoding = "rot13")] public string {|#0:Name|} { get; set; }
                }
            }
            """);

        var expected = VerifyCS.Diagnostic("PROTO010")
            .WithLocation(0)
            .WithArguments("rot13", "Name");

        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task PROTO010_Utf8Encoding_NoDiagnostic()
    {
        var test = Wrap("""
            namespace Test
            {
                using ProtobuffEncoder.Attributes;

                [ProtoContract]
                public class GoodEnc
                {
                    [ProtoField(1, Encoding = "utf-8")] public string Name { get; set; }
                }
            }
            """);

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task PROTO010_ContractLevelBadEncoding_Reports()
    {
        var test = Wrap("""
            namespace Test
            {
                using ProtobuffEncoder.Attributes;

                [ProtoContract(DefaultEncoding = "gibberish")]
                public class {|#0:ContractEnc|}
                {
                    [ProtoField(1)] public string Name { get; set; }
                }
            }
            """);

        var expected = VerifyCS.Diagnostic("PROTO010")
            .WithLocation(0)
            .WithArguments("gibberish", "ContractEnc");

        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }
}

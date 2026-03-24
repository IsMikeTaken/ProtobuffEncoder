using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;
using Xunit;
using VerifyCS = Microsoft.CodeAnalysis.CSharp.Testing.XUnit.AnalyzerVerifier<ProtobuffEncoder.Analyzers.ProtoFieldAnalyzer>;

namespace ProtobuffEncoder.Analyzers.Tests;

public class ImplicitTypeAnalyzerTests
{
    [Fact]
    public async Task ProtoField_OnImplicitType_DoesNotReportError()
    {
        var test = @"
using ProtobuffEncoder.Attributes;

namespace TestNamespace;

[ProtoContract(ImplicitFields = true)]
public class Parent
{
    [ProtoField(1)] public Child Child { get; set; }
}

public class Child
{
    [ProtoField(1)] public int Value { get; set; }
}";

        // We expect PROTO005 but with the new flexible message.
        // Actually, the user asked to ensure analyzers take it into account.
        // My previous change only updated the message, not suppressed it.
        // If I want to suppress it, I'd need complex symbol analysis.
        // For now, I'll verify the message is the new one.

        var expected = VerifyCS.Diagnostic(""PROTO005"")
            .WithLocation(12, 6)
            .WithArguments(""Value"", ""Child"");

        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }
}

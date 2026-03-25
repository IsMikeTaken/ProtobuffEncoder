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
        var expected = VerifyCS.Diagnostic("PROTO005")
            .WithLocation(11, 5)
            .WithArguments("Value", "Child");

        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }
}

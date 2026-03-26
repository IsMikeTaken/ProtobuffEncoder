using Xunit;
using ProtobuffEncoder.Analyzers.Tests.Helpers;
using VerifyCS = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
    ProtobuffEncoder.Analyzers.ProtoIncludeAnalyzer,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace ProtobuffEncoder.Analyzers.Tests;

public class ProtoIncludeAnalyzerTests
{
    private static string Wrap(string code) => AttributeStubs.Source + "\n" + code;

    // ── PROTO013: Include field number conflicts with ProtoField ──

    [Fact]
    public async Task PROTO013_IncludeFieldNumberConflictsWithField_Reports()
    {
        var test = Wrap("""
            namespace Test
            {
                using ProtobuffEncoder.Attributes;

                [ProtoContract]
                [ProtoInclude(1, typeof(Dog))]
                public class {|#0:Animal|}
                {
                    [ProtoField(1)] public string Name { get; set; }
                }

                [ProtoContract]
                public class Dog : Animal
                {
                    [ProtoField(1)] public string Breed { get; set; }
                }
            }
            """);

        var expected = VerifyCS.Diagnostic("PROTO013")
            .WithLocation(0)
            .WithArguments(1, "Dog", "Animal");

        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task PROTO013_IncludeFieldNumberDoesNotConflict_NoDiagnostic()
    {
        var test = Wrap("""
            namespace Test
            {
                using ProtobuffEncoder.Attributes;

                [ProtoContract]
                [ProtoInclude(10, typeof(Dog))]
                public class Animal
                {
                    [ProtoField(1)] public string Name { get; set; }
                }

                [ProtoContract]
                public class Dog : Animal
                {
                    [ProtoField(1)] public string Breed { get; set; }
                }
            }
            """);

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    // ── PROTO014: ProtoInclude type is not a subclass ──

    [Fact]
    public async Task PROTO014_IncludeTypeNotDerived_Reports()
    {
        var test = Wrap("""
            namespace Test
            {
                using ProtobuffEncoder.Attributes;

                [ProtoContract]
                [ProtoInclude(10, typeof(Unrelated))]
                public class {|#0:Base|}
                {
                    [ProtoField(1)] public string Name { get; set; }
                }

                [ProtoContract]
                public class Unrelated
                {
                    [ProtoField(1)] public int Value { get; set; }
                }
            }
            """);

        var expected = VerifyCS.Diagnostic("PROTO014")
            .WithLocation(0)
            .WithArguments("Base", "Unrelated");

        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task PROTO014_IncludeTypeDerived_NoDiagnostic()
    {
        var test = Wrap("""
            namespace Test
            {
                using ProtobuffEncoder.Attributes;

                [ProtoContract]
                [ProtoInclude(10, typeof(Child))]
                public class Parent
                {
                    [ProtoField(1)] public string Name { get; set; }
                }

                [ProtoContract]
                public class Child : Parent
                {
                    [ProtoField(2)] public int Age { get; set; }
                }
            }
            """);

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task PROTO014_IncludeTypeIndirectlyDerived_NoDiagnostic()
    {
        var test = Wrap("""
            namespace Test
            {
                using ProtobuffEncoder.Attributes;

                [ProtoContract]
                [ProtoInclude(10, typeof(GrandChild))]
                public class Root
                {
                    [ProtoField(1)] public string Name { get; set; }
                }

                public class Middle : Root { }

                [ProtoContract]
                public class GrandChild : Middle
                {
                    [ProtoField(2)] public int Level { get; set; }
                }
            }
            """);

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    // ── PROTO015: ProtoMap on non-Dictionary property ──

    [Fact]
    public async Task PROTO015_MapOnList_Reports()
    {
        var test = Wrap("""
            namespace Test
            {
                using ProtobuffEncoder.Attributes;
                using System.Collections.Generic;

                [ProtoContract]
                public class BadMap
                {
                    [ProtoField(1)]
                    [ProtoMap]
                    public List<string> {|#0:Items|} { get; set; }
                }
            }
            """);

        var expected = VerifyCS.Diagnostic("PROTO015")
            .WithLocation(0)
            .WithArguments("Items", "BadMap");

        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task PROTO015_MapOnString_Reports()
    {
        var test = Wrap("""
            namespace Test
            {
                using ProtobuffEncoder.Attributes;

                [ProtoContract]
                public class StringMap
                {
                    [ProtoField(1)]
                    [ProtoMap]
                    public string {|#0:Name|} { get; set; }
                }
            }
            """);

        var expected = VerifyCS.Diagnostic("PROTO015")
            .WithLocation(0)
            .WithArguments("Name", "StringMap");

        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task PROTO015_MapOnDictionary_NoDiagnostic()
    {
        var test = Wrap("""
            namespace Test
            {
                using ProtobuffEncoder.Attributes;
                using System.Collections.Generic;

                [ProtoContract]
                public class GoodMap
                {
                    [ProtoField(1)]
                    [ProtoMap]
                    public Dictionary<string, int> Scores { get; set; }
                }
            }
            """);

        await VerifyCS.VerifyAnalyzerAsync(test);
    }
}
